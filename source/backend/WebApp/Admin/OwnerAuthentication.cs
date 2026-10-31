using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.WebApp.Admin;

/// <summary>
/// The owner's sign-in (spec M4-D §3–§4): Identity with lockout and hashed recovery codes, a strict cookie that answers
/// 401 instead of redirecting, data-protection keys in SQL Server, and the anti-forgery header on admin writes.
/// </summary>
public static class OwnerAuthentication
{
    public const string Policy = "owner";
    public const string CookieName = "pss.owner";
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public const string AdminPrefix = "/v1/admin";

    /// <summary>How long a sign-in lasts; it is not extended by use.</summary>
    public static readonly TimeSpan SessionLength = TimeSpan.FromHours(8);

    /// <summary>The sign-in steps; they come before the anti-forgery token exists, so they are not checked.</summary>
    private static readonly string[] SignInPaths = [$"{AdminPrefix}/login", $"{AdminPrefix}/login/code"];

    public static IServiceCollection AddOwnerAuthentication(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddDataProtection().SetApplicationName("polski-streamer-simulator").PersistKeysToDbContext<GameDbContext>();
        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                // owner-create generates 24 random characters (about 139 bits); character classes add nothing to that.
                options.Password.RequiredLength = 16;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddUserStore<OwnerUserStore>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        var secure = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = CookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = secure;
            cookie.ExpireTimeSpan = SessionLength;
            cookie.SlidingExpiration = false;
            cookie.Events.OnRedirectToLogin = context => RejectAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.OwnerUnauthorized);
            cookie.Events.OnRedirectToAccessDenied = context => RejectAsync(context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.OwnerUnauthorized);
        });
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, cookie =>
        {
            cookie.Cookie.Name = $"{CookieName}.2fa";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = secure;
        });
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
            .RequireAuthenticatedUser());
        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeader;
            options.Cookie.Name = "pss.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = secure;
        });
        return services;
    }

    /// <summary>
    /// Marks every admin response <c>no-store</c> and rejects a signed-in admin <c>POST</c>, <c>PUT</c> or <c>DELETE</c>
    /// without a valid anti-forgery token. Anonymous requests pass on to the
    /// authorization check, which answers 401.
    /// </summary>
    public static IApplicationBuilder UseOwnerAntiforgery(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Path.StartsWithSegments(AdminPrefix, StringComparison.Ordinal))
            {
                // Owner data must never be served from a browser or proxy cache (spec M4-D §6).
                context.Response.Headers.CacheControl = "no-store";
            }

            if ((HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method) || HttpMethods.IsDelete(request.Method))
                && request.Path.StartsWithSegments(AdminPrefix, StringComparison.Ordinal)
                && !SignInPaths.Contains(request.Path.Value, StringComparer.Ordinal)
                && context.User.Identity?.IsAuthenticated == true)
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    await RejectAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.AntiforgeryFailed);
                    return;
                }
            }

            await next(context);
        });

    private static async Task RejectAsync(HttpContext context, int status, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = code };
        problem.Extensions["code"] = code;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", context.RequestAborted);
    }
}
