using Mapster;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Careers;

/// <summary>Career endpoints: each maps a request to a handler message and the result to JSON or Problem Details.</summary>
public static class CareerEndpoints
{
    private const string ProblemJson = "application/problem+json";

    [WolverinePost("/v1/careers", OperationId = "createCareer")]
    [ProducesResponseType<CreatedCareer>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public static async Task<IResult> PostCareer(CreateCareerRequest request, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<CreatedCareer>>(request.Adapt<CreateCareer>(), cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/v1/careers/{result.Value.CareerId}", result.Value)
            : ApiProblems.From(result.Error);
    }

    [WolverineGet("/v1/careers/{id}", OperationId = "getCareer")]
    [ProducesResponseType<CareerView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static async Task<IResult> GetCareerById(
        Guid id, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<CareerView>>(new GetCareer(id, Bearer.Secret(authorization)), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePost("/v1/careers/{id}/actions", OperationId = "submitAction")]
    [ProducesResponseType<ActionOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static async Task<IResult> PostAction(
        Guid id, ActionRequest request, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = new SubmitAction(id, Bearer.Secret(authorization), request.CommandId, request.Adapt<ActionInput>());
        var result = await bus.InvokeAsync<AppResult<ActionOutcome>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }
}
