using Microsoft.Net.Http.Headers;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>
/// Security headers on every API response, errors included (M5-D, #38): no MIME sniffing, no framing, no referrer, a
/// content security policy that lets a JSON response load nothing, and <c>no-store</c> unless an endpoint chose its
/// own caching (the public leaderboard). HSTS belongs to the TLS-terminating ingress (M6).
/// </summary>
public static class SecurityHeaders
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Resource-Policy"] = "same-origin";
                headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
                if (!headers.ContainsKey(HeaderNames.CacheControl))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
            return next(context);
        });
}
