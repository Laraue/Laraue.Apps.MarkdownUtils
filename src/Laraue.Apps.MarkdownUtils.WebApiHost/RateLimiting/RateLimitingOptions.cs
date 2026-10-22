using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Limits of the API requests, per client address unless said otherwise.
/// </summary>
public sealed class RateLimitingOptions
{
    [Required, ValidateObjectMembers]
    public TranslateLimits Translate { get; set; } = new();

    [Required, ValidateObjectMembers]
    public TokenBucketLimits Transpile { get; set; } = new();

    /// <summary>
    /// Retry-After sent when the rejecting limiter doesn't know when a permit frees up (the concurrency limit).
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan DefaultRetryAfter { get; set; } = TimeSpan.FromSeconds(5);
}

public class TokenBucketLimits
{
    /// <summary>
    /// Max requests a client can send at once (the burst).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TokenLimit { get; set; } = 30;

    /// <summary>
    /// Requests added back to the bucket every <see cref="ReplenishmentPeriod"/>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TokensPerPeriod { get; set; } = 2;

    [Range(typeof(TimeSpan), "00:00:00.100", "1.00:00:00")]
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);
}

public sealed class TranslateLimits : TokenBucketLimits
{
    public TranslateLimits()
    {
        TokenLimit = 3;
        TokensPerPeriod = 1;
        ReplenishmentPeriod = TimeSpan.FromSeconds(6);
    }

    /// <summary>
    /// Max translations of one client per day.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DailyLimit { get; set; } = 100;

    /// <summary>
    /// Max translations running at the same time, for all clients together. Requests over it are rejected,
    /// not queued: the limiters are chained, and a queued request would take a second token from the client's bucket.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ConcurrencyLimit { get; set; } = 4;
}
