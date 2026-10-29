using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Admin;

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>The next sign-in step after a correct password: always the second factor.</summary>
public sealed record OwnerLoginStep(string Next);

/// <summary>The signed-in owner, the anti-forgery token for admin writes, and how many recovery codes are left.</summary>
public sealed record OwnerSessionView(string Username, string CsrfToken, int RecoveryCodesLeft);

/// <summary>The owner's sign-in, session, moderation, season extension and audit endpoints (spec M4-D §6).</summary>
public static class AdminEndpoints
{
    private const string ProblemJson = "application/problem+json";

    [WolverinePost("/v1/admin/login", OperationId = "ownerLogin")]
    [EnableRateLimiting(CareerRateLimits.OwnerSignIn)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public static async Task<Results<Ok<OwnerLoginStep>, ProblemHttpResult>> PostLogin(OwnerLoginRequest request, HttpContext context)
    {
        var signIn = SignIn(context);
        var result = await signIn.PasswordSignInAsync(request.Username, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            return TypedResults.Ok(new OwnerLoginStep("code"));
        }

        if (result.Succeeded)
        {
            // The owner always has a second factor; a sign-in without one is never accepted.
            await signIn.SignOutAsync();
        }

        return ApiProblems.From(new AppError(result.IsLockedOut ? ErrorCodes.OwnerLockedOut : ErrorCodes.OwnerLoginFailed, null));
    }

    [WolverinePost("/v1/admin/login/code", OperationId = "ownerLoginCode")]
    [EnableRateLimiting(CareerRateLimits.OwnerSignIn)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public static async Task<Results<Ok<OwnerSessionView>, ProblemHttpResult>> PostLoginCode(
        OwnerCodeRequest request, HttpContext context, IAntiforgery antiforgery)
    {
        var signIn = SignIn(context);
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return ApiProblems.From(new AppError(ErrorCodes.OwnerLoginFailed, "sign in with the password first"));
        }

        var result = request.RecoveryCode is { } recoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(recoveryCode)
            : await signIn.TwoFactorAuthenticatorSignInAsync(request.Code!.Replace(" ", "", StringComparison.Ordinal), isPersistent: false, rememberClient: false);
        if (!result.Succeeded)
        {
            return ApiProblems.From(new AppError(result.IsLockedOut ? ErrorCodes.OwnerLockedOut : ErrorCodes.OwnerLoginFailed, null));
        }

        // The token is bound to the signed-in owner, whom this request does not carry yet.
        context.User = await signIn.CreateUserPrincipalAsync(user);
        return TypedResults.Ok(await SessionAsync(user, context, signIn.UserManager, antiforgery));
    }

    [WolverineGet("/v1/admin/session", OperationId = "getOwnerSession")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    public static async Task<Results<Ok<OwnerSessionView>, ProblemHttpResult>> GetSession(HttpContext context, IAntiforgery antiforgery)
    {
        var users = SignIn(context).UserManager;
        var user = await users.GetUserAsync(context.User);
        return user is null
            ? ApiProblems.From(new AppError(ErrorCodes.OwnerUnauthorized, null))
            : TypedResults.Ok(await SessionAsync(user, context, users, antiforgery));
    }

    [WolverinePost("/v1/admin/logout", OperationId = "ownerLogout")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    public static async Task<NoContent> PostLogout(HttpContext context)
    {
        await SignIn(context).SignOutAsync();
        return TypedResults.NoContent();
    }

    [WolverineGet("/v1/admin/leaderboard", OperationId = "listModeratedEntries")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static async Task<Results<Ok<ModeratedSeasonView>, ProblemHttpResult>> GetEntries(int? season, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<ModeratedSeasonView>>(new ListModeratedEntries(season), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePost("/v1/admin/leaderboard/{entryId}/hide", OperationId = "hideEntry")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static Task<Results<Ok<ModeratedEntryView>, ProblemHttpResult>> PostHide(
        Guid entryId, ModerationRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => ModerateAsync(new ModerateEntry(entryId, true, request.Reason, Actor(context)), bus, cancellationToken);

    [WolverinePost("/v1/admin/leaderboard/{entryId}/restore", OperationId = "restoreEntry")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static Task<Results<Ok<ModeratedEntryView>, ProblemHttpResult>> PostRestore(
        Guid entryId, ModerationRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => ModerateAsync(new ModerateEntry(entryId, false, request.Reason, Actor(context)), bus, cancellationToken);

    [WolverinePost("/v1/admin/seasons/{number}/extension", OperationId = "extendSeason")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static async Task<Results<Ok<SeasonView>, ProblemHttpResult>> PostExtension(
        int number, SeasonExtensionRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = new ExtendSeason(number, request.Until, request.Reason, Actor(context));
        var result = await bus.InvokeAsync<AppResult<SeasonView>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverineGet("/v1/admin/audit", OperationId = "listAudit")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    public static async Task<Ok<IReadOnlyList<AuditRecordView>>> GetAudit(int? limit, IMessageBus bus, CancellationToken cancellationToken)
        => TypedResults.Ok(await bus.InvokeAsync<IReadOnlyList<AuditRecordView>>(new ListAudit(limit), cancellationToken));

    private static async Task<Results<Ok<ModeratedEntryView>, ProblemHttpResult>> ModerateAsync(ModerateEntry command, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<ModeratedEntryView>>(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    // Identity's services are built per request by factories that Wolverine's generated code cannot inline.
    private static SignInManager<IdentityUser> SignIn(HttpContext context) => context.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();

    private static string Actor(HttpContext context) => context.User.Identity?.Name ?? throw new InvalidOperationException("The owner has no name.");

    private static async Task<OwnerSessionView> SessionAsync(IdentityUser user, HttpContext context, UserManager<IdentityUser> users, IAntiforgery antiforgery)
        => new(
            user.UserName!,
            antiforgery.GetAndStoreTokens(context).RequestToken!,
            await users.CountRecoveryCodesAsync(user));
}
