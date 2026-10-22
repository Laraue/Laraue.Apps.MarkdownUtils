using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Limits of the translate requests.
/// </summary>
public sealed class RateLimitingOptions
{
    [Required, ValidateObjectMembers]
    public TranslateLimits Translate { get; set; } = new();

    /// <summary>
    /// Retry-After sent when the rejecting limiter doesn't know when a permit frees up (the concurrency limit).
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan DefaultRetryAfter { get; set; } = TimeSpan.FromSeconds(5);
}

public sealed class TranslateLimits
{
    /// <summary>
    /// Max translations of one client per window, all of them apply (e.g. 3 per minute and 50 per day).
    /// Empty by default: the configuration binder adds to a list instead of replacing it.
    /// </summary>
    [Required, MinLength(1), ValidateEnumeratedItems]
    public List<WindowLimit> Windows { get; set; } = [];

    /// <summary>
    /// Max translations running at the same time, for all clients together. Requests over it are rejected, not queued.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ConcurrencyLimit { get; set; } = 4;
}

public sealed class WindowLimit
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan Window { get; set; }
}
