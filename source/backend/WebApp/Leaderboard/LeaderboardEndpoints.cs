using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.WebApp.Careers;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Leaderboard;

/// <summary>Publication of a career's result and the public season tables (spec M4-B §6–§7).</summary>
public static class LeaderboardEndpoints
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>How long a client or proxy may reuse a public table.</summary>
    public const string PublicCache = "public, max-age=30";

    [WolverineGet("/v1/careers/{id}/leaderboard-entry", OperationId = "getLeaderboardEntry")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static async Task<Results<Ok<EntryStatusView>, ProblemHttpResult>> GetEntry(
        Guid id, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<EntryStatusView>>(new GetEntryStatus(id, Bearer.Secret(authorization)), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePost("/v1/careers/{id}/leaderboard-entry", OperationId = "publishResult")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static async Task<Results<Ok<EntryStatusView>, ProblemHttpResult>> PostEntry(
        Guid id, PublishResultRequest request, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = new PublishResult(id, Bearer.Secret(authorization), request.Nickname);
        var result = await bus.InvokeAsync<AppResult<EntryStatusView>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverineGet("/v1/leaderboard", OperationId = "getLeaderboard")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static async Task<Results<Ok<LeaderboardView>, ProblemHttpResult>> GetLeaderboardTable(
        int? season, int? limit, HttpResponse response, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<LeaderboardView>>(new GetLeaderboard(season, limit), cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiProblems.From(result.Error);
        }

        response.Headers.CacheControl = PublicCache;
        return TypedResults.Ok(result.Value);
    }

    [WolverineGet("/v1/seasons", OperationId = "listSeasons")]
    public static async Task<Ok<IReadOnlyList<SeasonView>>> GetSeasons(HttpResponse response, IMessageBus bus, CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = PublicCache;
        return TypedResults.Ok(await bus.InvokeAsync<IReadOnlyList<SeasonView>>(ListSeasons.Instance, cancellationToken));
    }
}
