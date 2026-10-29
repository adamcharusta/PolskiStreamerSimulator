using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Saves;
using PolskiStreamerSimulator.WebApp.Careers;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Saves;

/// <summary>Manual slots, export and import of unranked careers (spec M4-C §6).</summary>
public static class SaveEndpoints
{
    private const string ProblemJson = "application/problem+json";

    [WolverineGet("/v1/careers/{id}/saves", OperationId = "listSaveSlots")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static async Task<Results<Ok<SaveSlotsView>, ProblemHttpResult>> GetSlots(
        Guid id, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<SaveSlotsView>>(new ListSaveSlots(id, Bearer.Secret(authorization)), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePut("/v1/careers/{id}/saves/{slot}", OperationId = "saveToSlot")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static async Task<Results<Ok<SaveSlotsView>, ProblemHttpResult>> PutSlot(
        Guid id, int slot, SaveToSlotRequest request, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = new SaveToSlot(id, Bearer.Secret(authorization), slot, request.ExpectedVersion);
        var result = await bus.InvokeAsync<AppResult<SaveSlotsView>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePost("/v1/careers/{id}/saves/{slot}/restore", OperationId = "restoreSlot")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static async Task<Results<Ok<ActionOutcome>, ProblemHttpResult>> PostRestore(
        Guid id, int slot, RestoreSlotRequest request, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = new RestoreSlot(id, Bearer.Secret(authorization), slot, request.CommandId, request.ExpectedVersion);
        var result = await bus.InvokeAsync<AppResult<ActionOutcome>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverineGet("/v1/careers/{id}/export", OperationId = "exportCareer")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static async Task<Results<Ok<SaveFile>, ProblemHttpResult>> GetExport(
        Guid id, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<SaveFile>>(new ExportCareer(id, Bearer.Secret(authorization)), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePost("/v1/careers/import", OperationId = "importCareer")]
    [EnableRateLimiting(CareerRateLimits.Create)]
    [RequestSizeLimit(SaveFiles.MaxBytes)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public static async Task<Results<Created<CreatedCareer>, ProblemHttpResult>> PostImport(ImportCareerRequest request, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<CreatedCareer>>(new ImportCareer(request.Save!.Value), cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/v1/careers/{result.Value.CareerId}", result.Value)
            : ApiProblems.From(result.Error);
    }
}
