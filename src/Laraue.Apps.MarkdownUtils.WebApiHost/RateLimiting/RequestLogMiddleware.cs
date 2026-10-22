using System.Diagnostics;

namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Writes a log line per API request with the client address, user agent and body size,
/// so the top callers are a log query. Runs before the rate limiter, so rejected requests are logged too.
/// </summary>
public sealed class RequestLogMiddleware(ILogger<RequestLogMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Only the API itself: not the health checks and the metrics scraping.
        if (context.GetEndpoint() is not RouteEndpoint { RoutePattern.RawText: { } route }
            || !route.StartsWith("api/", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "API request {Route} from {ClientAddress}, user agent {UserAgent}, size {ContentLength}: {StatusCode} in {ElapsedMs} ms",
                route,
                context.GetClientAddress(),
                context.Request.Headers.UserAgent.ToString(),
                context.Request.ContentLength,
                context.Response.StatusCode,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
