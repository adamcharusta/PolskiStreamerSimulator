using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>Limits of the career access and owner sign-in endpoints: requests per window and client address (spec M4-A §7, M4-D §3).</summary>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    public int CreatePermits { get; set; } = 20;

    public int RecoverPermits { get; set; } = 10;

    public int OwnerSignInPermits { get; set; } = 10;

    /// <summary>Telemetry batches per minute and address (spec M4-F §2).</summary>
    public int TelemetryPermits { get; set; } = 30;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>Fixed-window policies per remote address; a rejection is Problem Details with <c>code: rate-limited</c>.</summary>
public static class CareerRateLimits
{
    public const string Create = "career-create";
    public const string Recover = "career-recover";
    public const string OwnerSignIn = "owner-sign-in";
    public const string Telemetry = "telemetry";

    public static IServiceCollection AddCareerRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>().Bind(configuration.GetSection(RateLimitOptions.Section));
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitOptions>>((limiter, limits) =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(Create, context => PerAddress(context, limits.Value.CreatePermits, limits.Value.Window));
            limiter.AddPolicy(Recover, context => PerAddress(context, limits.Value.RecoverPermits, limits.Value.Window));
            limiter.AddPolicy(OwnerSignIn, context => PerAddress(context, limits.Value.OwnerSignInPermits, limits.Value.Window));
            limiter.AddPolicy(Telemetry, context => PerAddress(context, limits.Value.TelemetryPermits, TimeSpan.FromMinutes(1)));
            limiter.OnRejected = (context, cancellationToken) => RejectAsync(context.HttpContext, cancellationToken);
        });
        return services;
    }

    // Behind a proxy the remote address is the proxy's until forwarded headers are trusted (M6, #40).
    private static RateLimitPartition<string> PerAddress(HttpContext context, int permits, TimeSpan window)
        => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });

    private static async ValueTask RejectAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = ErrorCodes.RateLimited };
        problem.Extensions["code"] = ErrorCodes.RateLimited;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
    }
}
