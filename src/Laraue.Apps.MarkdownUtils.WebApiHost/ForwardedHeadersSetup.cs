using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Laraue.Apps.MarkdownTranspiler.WebApi;

public static class ForwardedHeadersSetup
{
    /// <summary>
    /// The app runs behind nginx, so the connection address is nginx's. Takes the client address from
    /// X-Forwarded-For, but only from the proxies listed in the "ForwardedHeaders" section
    /// (KnownProxies - addresses, KnownNetworks - CIDR ranges); without them only loopback is trusted.
    /// </summary>
    public static IServiceCollection AddProxyForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders");
        var knownProxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in knownProxies)
                options.KnownProxies.Add(IPAddress.Parse(proxy));

            foreach (var network in knownNetworks)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

        return services;
    }
}
