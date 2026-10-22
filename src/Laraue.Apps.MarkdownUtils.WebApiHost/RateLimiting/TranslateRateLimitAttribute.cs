namespace Laraue.Apps.MarkdownTranspiler.WebApi.RateLimiting;

/// <summary>
/// Marks the endpoint the translate limits apply to (<see cref="RateLimitingOptions.Translate"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TranslateRateLimitAttribute : Attribute;

public static class TranslateRateLimitExtensions
{
    public static bool IsTranslateRateLimited(this HttpContext context)
    {
        return context.GetEndpoint()?.Metadata.GetMetadata<TranslateRateLimitAttribute>() is not null;
    }
}
