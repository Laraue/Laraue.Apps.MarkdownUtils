using System.Threading.RateLimiting;
using Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace Laraue.Apps.MarkdownUtils.WebApiHost.Tests;

public class MultiWindowRateLimiterTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void EveryWindowApplies()
    {
        var limiter = CreateLimiter((3, TimeSpan.FromMinutes(1)), (5, TimeSpan.FromHours(1)));

        Assert.Equal(3, AcquireMany(limiter, 10));

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(2, AcquireMany(limiter, 10)); // the minute window is reset, the hour one has 2 left

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, AcquireMany(limiter, 10));
    }

    [Fact]
    public void RejectedRequest_TakesNoPermits()
    {
        var limiter = CreateLimiter((3, TimeSpan.FromMinutes(1)), (4, TimeSpan.FromHours(1)));

        Assert.Equal(3, AcquireMany(limiter, 100)); // 97 rejected by the minute window

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, AcquireMany(limiter, 10)); // the hour window has counted only the 3 accepted
    }

    [Fact]
    public void Rejection_RetryAfterIsTheLongestFullWindow()
    {
        var limiter = CreateLimiter((1, TimeSpan.FromMinutes(1)), (1, TimeSpan.FromDays(1)));
        limiter.AttemptAcquire().Dispose();

        _time.Advance(TimeSpan.FromSeconds(20));
        using var lease = limiter.AttemptAcquire();

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.Equal(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(20), retryAfter);
    }

    [Fact]
    public void IsIdle_OnlyWhenAllWindowsEnded()
    {
        var limiter = CreateLimiter((3, TimeSpan.FromMinutes(1)), (50, TimeSpan.FromDays(1)));
        limiter.AttemptAcquire().Dispose();

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Null(limiter.IdleDuration);

        _time.Advance(TimeSpan.FromDays(1));
        Assert.NotNull(limiter.IdleDuration);
    }

    private MultiWindowRateLimiter CreateLimiter(params (int PermitLimit, TimeSpan Window)[] windows)
    {
        return new MultiWindowRateLimiter(
            windows.Select(window => new WindowLimit { PermitLimit = window.PermitLimit, Window = window.Window }),
            _time);
    }

    private static int AcquireMany(RateLimiter limiter, int count)
    {
        var acquired = 0;
        for (var i = 0; i < count; i++)
        {
            using var lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
                acquired++;
        }

        return acquired;
    }
}
