using System.Threading.RateLimiting;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Fixed windows of one client checked together (e.g. 3 per minute and 50 per day): a request takes a permit
/// from every window only when all of them have one, so a rejected request costs nothing. Chained limiters can't
/// do that - an earlier one keeps its permit when a later one rejects, and the middleware tries the chain twice.
/// Each window starts at the first request after the previous one ended. No queue: over the limit is a rejection.
/// </summary>
public sealed class MultiWindowRateLimiter : RateLimiter
{
    private readonly Window[] _windows;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private long _lastActivity;

    public MultiWindowRateLimiter(IEnumerable<WindowLimit> windows, TimeProvider? timeProvider = null)
    {
        _windows = windows.Select(window => new Window(window.PermitLimit, window.Window)).ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastActivity = _timeProvider.GetTimestamp();
    }

    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (_lock)
            {
                var now = _timeProvider.GetTimestamp();
                return _windows.Any(window => window.IsActive(now, _timeProvider))
                    ? null
                    : _timeProvider.GetElapsedTime(_lastActivity, now);
            }
        }
    }

    public override RateLimiterStatistics GetStatistics()
    {
        lock (_lock)
        {
            var now = _timeProvider.GetTimestamp();
            return new RateLimiterStatistics
            {
                CurrentAvailablePermits = _windows.Min(window => window.Available(now, _timeProvider)),
            };
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        lock (_lock)
        {
            var now = _timeProvider.GetTimestamp();

            var full = _windows.Where(window => window.Available(now, _timeProvider) < permitCount).ToArray();
            if (full.Length > 0)
                return new WindowLease(false, full.Max(window => window.ResetsIn(now, _timeProvider)));

            foreach (var window in _windows)
                window.Take(permitCount, now, _timeProvider);

            _lastActivity = now;

            return new WindowLease(true, null);
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(AttemptAcquireCore(permitCount));
    }

    private sealed class Window(int permitLimit, TimeSpan length)
    {
        private long _startedAt;
        private int _taken;

        public bool IsActive(long now, TimeProvider timeProvider)
        {
            return _taken > 0 && timeProvider.GetElapsedTime(_startedAt, now) < length;
        }

        public int Available(long now, TimeProvider timeProvider)
        {
            return IsActive(now, timeProvider) ? permitLimit - _taken : permitLimit;
        }

        public TimeSpan ResetsIn(long now, TimeProvider timeProvider)
        {
            return length - timeProvider.GetElapsedTime(_startedAt, now);
        }

        public void Take(int permitCount, long now, TimeProvider timeProvider)
        {
            if (!IsActive(now, timeProvider))
            {
                _startedAt = now;
                _taken = 0;
            }

            _taken += permitCount;
        }
    }

    private sealed class WindowLease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public override bool IsAcquired { get; } = isAcquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is { } value && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
