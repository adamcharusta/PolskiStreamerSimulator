using System.Reflection;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.WebApp;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class ApiProblemsTests
{
    public static TheoryData<string, int> Statuses => new()
    {
        { ErrorCodes.ValidationFailed, 400 },
        { ErrorCodes.MissingSecret, 401 },
        { ErrorCodes.CareerNotFound, 404 },
        { ErrorCodes.WeekNotFound, 404 },
        { ErrorCodes.StaleVersion, 409 },
        { ErrorCodes.CareerEnded, 409 },
        { ErrorCodes.DecisionPending, 409 },
        { ErrorCodes.NoPendingDecision, 409 },
        { ErrorCodes.IllegalAction, 422 },
        { ErrorCodes.UnknownChoice, 422 },
        { ErrorCodes.EventsUnresolved, 422 },
        { ErrorCodes.NoPendingWeek, 422 },
        { ErrorCodes.WeekInProgress, 422 },
        { ErrorCodes.CommandIdReused, 422 },
        { ErrorCodes.RecoveryFailed, 404 },
        { ErrorCodes.CreationIdReused, 422 },
        { ErrorCodes.CreationClosed, 409 },
        { ErrorCodes.RateLimited, 429 },
        { ErrorCodes.CareerUnranked, 409 },
        { ErrorCodes.CareerNotEnded, 409 },
        { ErrorCodes.SubmissionClosed, 409 },
        { ErrorCodes.SeasonNotFound, 404 },
        { ErrorCodes.SeasonUnavailable, 503 },
        { ErrorCodes.CareerRanked, 409 },
        { ErrorCodes.SlotNotFound, 404 },
        { ErrorCodes.SlotEmpty, 409 },
        { ErrorCodes.ImportInvalid, 422 },
        { ErrorCodes.ImportNewer, 422 },
        { ErrorCodes.ImportUnsupported, 422 },
        { ErrorCodes.PayloadTooLarge, 413 },
        { ErrorCodes.EntryNotFound, 404 },
        { ErrorCodes.EntryHidden, 409 },
        { ErrorCodes.EntryVisible, 409 },
        { ErrorCodes.OwnerUnauthorized, 401 },
        { ErrorCodes.OwnerLoginFailed, 401 },
        { ErrorCodes.OwnerLockedOut, 423 },
        { ErrorCodes.AntiforgeryFailed, 400 },
        { ErrorCodes.DraftNotFound, 404 },
        { ErrorCodes.DraftExists, 409 },
        { ErrorCodes.DraftStale, 409 },
        { ErrorCodes.EventNotFound, 404 },
        { ErrorCodes.ContentInvalid, 422 },
        { ErrorCodes.ContentUnchanged, 409 },
        { ErrorCodes.SimulationFailed, 422 },
        { ErrorCodes.PublicationNotFound, 404 },
        { ErrorCodes.Internal, 500 },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Each_code_has_its_status_and_carries_the_code(string code, int status)
    {
        var problem = ApiProblems.From(new AppError(code, "detail"));

        Assert.Equal(status, problem.StatusCode);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void Every_error_code_is_mapped()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!);

        Assert.Equal(codes.Order(StringComparer.Ordinal), Statuses.Select(row => row.Data.Item1).Order(StringComparer.Ordinal));
    }
}
