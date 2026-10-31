using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PolskiStreamerSimulator.Application.Telemetry;
using PolskiStreamerSimulator.WebApp.Careers;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Telemetry;

/// <summary>Opted-in gameplay events: only allowlisted names and values, counted per day (spec M4-F §2).</summary>
public sealed record TelemetryRequest(IReadOnlyList<TelemetryEvent> Events);

public sealed class TelemetryRequestValidator : AbstractValidator<TelemetryRequest>
{
    public TelemetryRequestValidator() => RuleFor(r => r.Events).NotNull().Must(e => e.Count <= TelemetryAllowlist.MaxEvents)
        .WithMessage($"Send at most {TelemetryAllowlist.MaxEvents} events.");

    protected override bool PreValidate(ValidationContext<TelemetryRequest> context, ValidationResult result)
        => RequestBodies.Present(context.InstanceToValidate, result);
}

public static class TelemetryEndpoints
{
    public const int MaxBytes = 16 * 1024;

    private const string ProblemJson = "application/problem+json";

    [WolverinePost("/v1/telemetry", OperationId = "recordTelemetry")]
    [EnableRateLimiting(CareerRateLimits.Telemetry)]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    public static async Task<Accepted<TelemetryAccepted>> PostTelemetry(TelemetryRequest request, IMessageBus bus, CancellationToken cancellationToken)
        => TypedResults.Accepted((string?)null, await bus.InvokeAsync<TelemetryAccepted>(new RecordTelemetry(request.Events), cancellationToken));
}
