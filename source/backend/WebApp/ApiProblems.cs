using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>Problem Details with a stable <c>code</c> for every API failure.</summary>
public static class ApiProblems
{
    private static readonly Dictionary<string, int> Statuses = new(StringComparer.Ordinal)
    {
        [ErrorCodes.ValidationFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MissingSecret] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.CareerNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.WeekNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.StaleVersion] = StatusCodes.Status409Conflict,
        [ErrorCodes.CareerEnded] = StatusCodes.Status409Conflict,
        [ErrorCodes.DecisionPending] = StatusCodes.Status409Conflict,
        [ErrorCodes.NoPendingDecision] = StatusCodes.Status409Conflict,
        [ErrorCodes.IllegalAction] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UnknownChoice] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.EventsUnresolved] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.NoPendingWeek] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.WeekInProgress] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CommandIdReused] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RecoveryFailed] = StatusCodes.Status404NotFound,
        [ErrorCodes.CreationIdReused] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CreationClosed] = StatusCodes.Status409Conflict,
        [ErrorCodes.RateLimited] = StatusCodes.Status429TooManyRequests,
        [ErrorCodes.CareerUnranked] = StatusCodes.Status409Conflict,
        [ErrorCodes.CareerNotEnded] = StatusCodes.Status409Conflict,
        [ErrorCodes.SubmissionClosed] = StatusCodes.Status409Conflict,
        [ErrorCodes.SeasonNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SeasonUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [ErrorCodes.CareerRanked] = StatusCodes.Status409Conflict,
        [ErrorCodes.SlotNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SlotEmpty] = StatusCodes.Status409Conflict,
        [ErrorCodes.ImportInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ImportNewer] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ImportUnsupported] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PayloadTooLarge] = StatusCodes.Status413PayloadTooLarge,
    };

    public static ProblemHttpResult From(AppError error)
    {
        var problem = new ProblemDetails
        {
            Status = Statuses.GetValueOrDefault(error.Code, StatusCodes.Status500InternalServerError),
            Title = error.Code,
            Detail = error.Detail,
        };
        problem.Extensions["code"] = error.Code;
        return TypedResults.Problem(problem);
    }

    /// <summary>Adds the code to Problem Details produced by the framework: validation (400) and unhandled errors (500).</summary>
    public static void Configure(ProblemDetailsOptions options)
        => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            if (problem.Extensions.ContainsKey("code"))
            {
                return;
            }

            if (problem.Status == StatusCodes.Status400BadRequest)
            {
                problem.Extensions["code"] = ErrorCodes.ValidationFailed;
            }
            else if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                problem.Extensions["code"] = ErrorCodes.Internal;
                problem.Detail = null;
            }
        };
}
