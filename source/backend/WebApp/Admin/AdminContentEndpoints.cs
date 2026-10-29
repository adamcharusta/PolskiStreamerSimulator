using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.WebApp.Careers;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Admin;

/// <summary>Creates the draft from the newest publication, or from <see cref="FromPublication"/>.</summary>
public sealed record CreateDraftRequest(int? FromPublication = null);

/// <summary>An event to put into the draft at the revision the owner read; its <c>id</c> equals the path.</summary>
public sealed record DraftEventRequest(int Revision, JsonElement Event);

/// <summary>The whole balance document at the revision the owner read.</summary>
public sealed record DraftBalanceRequest(int Revision, JsonElement Balance);

/// <summary>Publishes the draft at the revision the owner checked, for a reason kept in the audit record.</summary>
public sealed record PublishContentRequest(int Revision, string Reason);

public sealed class DraftEventRequestValidator : AbstractValidator<DraftEventRequest>
{
    public DraftEventRequestValidator() => RuleFor(r => r.Revision).GreaterThan(0);

    protected override bool PreValidate(ValidationContext<DraftEventRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

public sealed class DraftBalanceRequestValidator : AbstractValidator<DraftBalanceRequest>
{
    public DraftBalanceRequestValidator() => RuleFor(r => r.Revision).GreaterThan(0);

    protected override bool PreValidate(ValidationContext<DraftBalanceRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

public sealed class PublishContentRequestValidator : AbstractValidator<PublishContentRequest>
{
    public PublishContentRequestValidator()
    {
        RuleFor(r => r.Revision).GreaterThan(0);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(AuditActions.MaxReasonLength);
    }

    protected override bool PreValidate(ValidationContext<PublishContentRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

/// <summary>The owner's content drafts, publications and reversals (spec M4-E §7).</summary>
public static class AdminContentEndpoints
{
    private const string ProblemJson = "application/problem+json";

    [WolverineGet("/v1/admin/content", OperationId = "getContentAdmin")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    public static async Task<Ok<ContentAdminView>> GetContent(IMessageBus bus, CancellationToken cancellationToken)
        => TypedResults.Ok(await bus.InvokeAsync<ContentAdminView>(GetContentAdmin.Instance, cancellationToken));

    [WolverinePost("/v1/admin/content/draft", OperationId = "createContentDraft")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static Task<Results<Ok<ContentDraftView>, ProblemHttpResult>> PostDraft(CreateDraftRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentDraftView>(new CreateContentDraft(request.FromPublication, Actor(context)), bus, cancellationToken);

    [WolverineDelete("/v1/admin/content/draft", OperationId = "discardContentDraft")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static async Task<Results<NoContent, ProblemHttpResult>> DeleteDraft(int revision, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<bool>>(new DiscardContentDraft(revision, Actor(context)), cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : ApiProblems.From(result.Error);
    }

    [WolverinePut("/v1/admin/content/draft/events/{id}", OperationId = "putDraftEvent")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static Task<Results<Ok<ContentDraftView>, ProblemHttpResult>> PutEvent(
        string id, DraftEventRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentDraftView>(new PutDraftEvent(id, request.Revision, request.Event, Actor(context)), bus, cancellationToken);

    [WolverineDelete("/v1/admin/content/draft/events/{id}", OperationId = "removeDraftEvent")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static Task<Results<Ok<ContentDraftView>, ProblemHttpResult>> DeleteEvent(
        string id, int revision, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentDraftView>(new RemoveDraftEvent(id, revision, Actor(context)), bus, cancellationToken);

    [WolverinePut("/v1/admin/content/draft/balance", OperationId = "putDraftBalance")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public static Task<Results<Ok<ContentDraftView>, ProblemHttpResult>> PutBalance(
        DraftBalanceRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentDraftView>(new PutDraftBalance(request.Revision, request.Balance, Actor(context)), bus, cancellationToken);

    [WolverinePost("/v1/admin/content/draft/simulation", OperationId = "simulateContentDraft")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static Task<Results<Ok<SimulationView>, ProblemHttpResult>> PostSimulation(IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<SimulationView>(SimulateContentDraft.Instance, bus, cancellationToken);

    [WolverinePost("/v1/admin/content/publish", OperationId = "publishContent")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static Task<Results<Ok<ContentPublicationView>, ProblemHttpResult>> PostPublish(
        PublishContentRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentPublicationView>(new PublishContentDraft(request.Revision, request.Reason, Actor(context)), bus, cancellationToken);

    [WolverineGet("/v1/admin/content/publications", OperationId = "listContentPublications")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    public static async Task<Ok<IReadOnlyList<ContentPublicationView>>> GetPublications(IMessageBus bus, CancellationToken cancellationToken)
        => TypedResults.Ok(await bus.InvokeAsync<IReadOnlyList<ContentPublicationView>>(ListContentPublications.Instance, cancellationToken));

    [WolverinePost("/v1/admin/content/publications/{number}/revert", OperationId = "revertContentPublication")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static Task<Results<Ok<ContentPublicationView>, ProblemHttpResult>> PostRevert(
        int number, ModerationRequest request, HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentPublicationView>(new RevertContentPublication(number, request.Reason, Actor(context)), bus, cancellationToken);

    [WolverineGet("/v1/admin/content/publications/{number}/export", OperationId = "exportContentPublication")]
    [Authorize(Policy = OwnerAuthentication.Policy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static Task<Results<Ok<ContentExportView>, ProblemHttpResult>> GetExport(int number, IMessageBus bus, CancellationToken cancellationToken)
        => InvokeAsync<ContentExportView>(new ExportContentPublication(number), bus, cancellationToken);

    private static async Task<Results<Ok<T>, ProblemHttpResult>> InvokeAsync<T>(object message, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<T>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    private static string Actor(HttpContext context) => context.User.Identity?.Name ?? throw new InvalidOperationException("The owner has no name.");
}
