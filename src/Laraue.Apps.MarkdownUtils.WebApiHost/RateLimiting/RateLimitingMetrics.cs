using System.Diagnostics.Metrics;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Requests rejected by the rate limiter, exported via OpenTelemetry. The client address is not a label
/// on purpose (cardinality) - it is in the request log instead.
/// </summary>
public sealed class RateLimitingMetrics
{
    public const string MeterName = "Laraue.Apps.MarkdownUtils.RateLimiting";

    private readonly Counter<long> _rejections;

    public RateLimitingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _rejections = meter.CreateCounter<long>(
            "markdown_utils.rate_limit.rejections",
            unit: "{request}",
            description: "Requests rejected with 429 by the rate limiter, by operation");
    }

    public void RequestRejected(RateLimitedOperation operation)
    {
        _rejections.Add(1, new KeyValuePair<string, object?>("operation", operation.ToTagValue()));
    }
}
