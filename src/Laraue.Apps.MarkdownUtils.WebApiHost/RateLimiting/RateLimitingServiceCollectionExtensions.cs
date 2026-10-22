using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    private const string NoLimitPartition = "none";
    private const string TranslateConcurrencyPartition = "translate";

    /// <summary>
    /// Limits the endpoints marked with <see cref="RateLimitedOperationAttribute"/> per client address
    /// (the real one, so the forwarded headers must be applied before the limiter runs).
    /// Over the limit the client gets 429 with Retry-After.
    /// </summary>
    public static IServiceCollection AddClientRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<RateLimitingOptions>(configuration.GetSection("RateLimiting"));
        services.AddSingleton<RateLimitingMetrics>();

        services.AddRateLimiter(_ => { });
        services
            .AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>, RateLimitingMetrics>((limiter, options, metrics) =>
            {
                var limits = options.Value;

                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                // The concurrency limit goes first: its permit is given back when a later limiter rejects
                // the request, while the client windows only take permits when the request passes all of them.
                limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    TranslateConcurrencyLimiter(limits.Translate),
                    TranslateWindowsLimiter(limits.Translate),
                    TranspileBucketLimiter(limits.Transpile));

                limiter.OnRejected = (context, _) =>
                {
                    var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                        ? value
                        : limits.DefaultRetryAfter;

                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                    if (context.HttpContext.GetRateLimitedOperation() is { } operation)
                        metrics.RequestRejected(operation);

                    return ValueTask.CompletedTask;
                };
            });

        return services;
    }

    private static PartitionedRateLimiter<HttpContext> TranspileBucketLimiter(TokenBucketLimits bucket)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.GetRateLimitedOperation() != RateLimitedOperation.Transpile)
                return RateLimitPartition.GetNoLimiter(NoLimitPartition);

            return RateLimitPartition.GetTokenBucketLimiter(
                context.GetClientAddress(),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = bucket.TokenLimit,
                    TokensPerPeriod = bucket.TokensPerPeriod,
                    ReplenishmentPeriod = bucket.ReplenishmentPeriod,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
        });
    }

    private static PartitionedRateLimiter<HttpContext> TranslateWindowsLimiter(TranslateLimits limits)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.GetRateLimitedOperation() != RateLimitedOperation.Translate)
                return RateLimitPartition.GetNoLimiter(NoLimitPartition);

            return RateLimitPartition.Get(
                context.GetClientAddress(),
                _ => new MultiWindowRateLimiter(limits.Windows));
        });
    }

    private static PartitionedRateLimiter<HttpContext> TranslateConcurrencyLimiter(TranslateLimits limits)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (context.GetRateLimitedOperation() != RateLimitedOperation.Translate)
                return RateLimitPartition.GetNoLimiter(NoLimitPartition);

            return RateLimitPartition.GetConcurrencyLimiter(
                TranslateConcurrencyPartition,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = limits.ConcurrencyLimit,
                    QueueLimit = 0,
                });
        });
    }

    public static string GetClientAddress(this HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
