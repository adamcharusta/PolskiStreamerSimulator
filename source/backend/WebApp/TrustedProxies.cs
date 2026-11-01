using Microsoft.AspNetCore.HttpOverrides;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>
/// The client address behind the production ingress (M5-D, #38). Rate limits are per client address, so behind a
/// proxy every player would share the proxy's address. <c>ForwardedHeaders:KnownNetworks</c> lists the proxy networks
/// (CIDR) whose <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> are trusted; without it the headers are ignored,
/// so no client can choose its own rate-limit address.
/// </summary>
public static class TrustedProxies
{
    public const string Section = "ForwardedHeaders:KnownNetworks";

    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var networks = configuration.GetSection(Section).Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
        return services;
    }

    /// <summary>Reads the forwarded headers only when trusted networks are configured.</summary>
    public static IApplicationBuilder UseTrustedProxies(this IApplicationBuilder app, IConfiguration configuration)
        => configuration.GetSection(Section).Get<string[]>() is { Length: > 0 } ? app.UseForwardedHeaders() : app;
}
