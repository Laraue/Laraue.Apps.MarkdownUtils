namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Marks an endpoint as an API operation that is logged per request and limited per client.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RateLimitedOperationAttribute(RateLimitedOperation operation) : Attribute
{
    public RateLimitedOperation Operation { get; } = operation;
}

public enum RateLimitedOperation
{
    Translate,
    Transpile,
}

public static class RateLimitedOperationExtensions
{
    public static RateLimitedOperation? GetRateLimitedOperation(this HttpContext context)
    {
        return context.GetEndpoint()?.Metadata.GetMetadata<RateLimitedOperationAttribute>()?.Operation;
    }

    public static string ToTagValue(this RateLimitedOperation operation) => operation switch
    {
        RateLimitedOperation.Translate => "translate",
        RateLimitedOperation.Transpile => "transpile",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
    };
}
