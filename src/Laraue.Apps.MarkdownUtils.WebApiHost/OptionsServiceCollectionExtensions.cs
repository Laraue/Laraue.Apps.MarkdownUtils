namespace Laraue.Apps.MarkdownTranspiler.WebApi;

public static class OptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds the options to the configuration section and checks their data annotations on start,
    /// so a wrong value fails the start instead of the first request.
    /// </summary>
    public static IServiceCollection AddValidatedOptions<TOptions>(this IServiceCollection services, IConfiguration section)
        where TOptions : class
    {
        services
            .AddOptions<TOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
