using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>A career as stored: its current state and display metadata.</summary>
public sealed record StoredCareer(Guid Id, string Nickname, CareerState State, DateTimeOffset CreatedAt, DateTimeOffset LastActionAt);

/// <summary>A career to create with the hash of its access secret; the raw secret is never stored.</summary>
public sealed record NewCareer(Guid Id, string Nickname, CareerState State, byte[] SecretHash, DateTimeOffset CreatedAt);

/// <summary>What a logged command did.</summary>
public enum ActionKind
{
    StartWeek,
    ResolveEvent,
    SettleWeek,
    AutoWeek,
    ResolveDecision,
    RespondOffer,
    Retire,
}

/// <summary>A log entry to append; the store assigns its sequence.</summary>
public sealed record NewAction(Guid CommandId, int Step, ActionKind Kind, string RequestJson, string ResponseJson, string? ReportJson);

/// <summary>A logged command step.</summary>
public sealed record StoredAction(
    int Sequence,
    Guid CommandId,
    int Step,
    ActionKind Kind,
    string RequestJson,
    string ResponseJson,
    string? ReportJson,
    long StateVersionAfter,
    string StateHashAfter,
    DateTimeOffset CreatedAt);

/// <summary>Result of <see cref="ICareerStore.AppendAsync"/>.</summary>
public enum AppendOutcome
{
    Appended,
    VersionConflict,
    DuplicateCommand,
}
