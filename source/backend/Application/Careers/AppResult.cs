namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Stable error codes; WebApp maps each to an HTTP status.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation-failed";
    public const string MissingSecret = "missing-secret";
    public const string CareerNotFound = "career-not-found";
    public const string StaleVersion = "stale-version";
    public const string CareerEnded = "career-ended";
    public const string IllegalAction = "illegal-action";
    public const string UnknownChoice = "unknown-choice";
    public const string EventsUnresolved = "events-unresolved";
    public const string NoPendingWeek = "no-pending-week";
    public const string WeekInProgress = "week-in-progress";
    public const string CommandIdReused = "command-id-reused";
    public const string DecisionPending = "decision-pending";
    public const string NoPendingDecision = "no-pending-decision";
    public const string WeekNotFound = "week-not-found";
    public const string RecoveryFailed = "recovery-failed";
    public const string CreationIdReused = "creation-id-reused";
    public const string CreationClosed = "creation-closed";
    public const string RateLimited = "rate-limited";
    public const string Internal = "internal-error";
}

/// <summary>A rejected request: a stable <see cref="Code"/> and an optional machine-readable detail.</summary>
public sealed record AppError(string Code, string? Detail = null);

/// <summary>Either a value or an <see cref="AppError"/>; handlers return it instead of throwing for rejected requests.</summary>
public sealed class AppResult<T>
{
    private readonly T? value;
    private readonly AppError? error;

    private AppResult(T? value, AppError? error)
    {
        this.value = value;
        this.error = error;
    }

    public bool IsSuccess => error is null;

    public T Value => IsSuccess ? value! : throw new InvalidOperationException($"Result failed with {error!.Code}.");

    public AppError Error => error ?? throw new InvalidOperationException("Result succeeded; there is no error.");

    public static AppResult<T> Ok(T value) => new(value, null);

    public static AppResult<T> Fail(AppError error) => new(default, error);

    public static AppResult<T> Fail(string code, string? detail = null) => new(default, new AppError(code, detail));
}
