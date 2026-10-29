using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>A career as stored: its current state, display metadata and the date the retention sweep deletes it.</summary>
public sealed record StoredCareer(
    Guid Id,
    string Nickname,
    CareerState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActionAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt = null);

/// <summary>
/// A career to create with the hashes of its access material; the raw secret, recovery key and creation ID are never
/// stored. <see cref="Creation"/> is null for a client that sent no creation ID.
/// </summary>
public sealed record NewCareer(
    Guid Id,
    string Nickname,
    CareerState State,
    byte[] SecretHash,
    byte[] RecoveryHash,
    CreationHashes? Creation,
    DateTimeOffset CreatedAt);

/// <summary>SHA-256 of the client's creation ID and of its canonical request (spec M4-A §4).</summary>
public sealed record CreationHashes(byte[] CreationHash, byte[] RequestHash);

/// <summary>The stored hashes of one career's access material.</summary>
public sealed record CareerCredentials(byte[] SecretHash, byte[]? RecoveryHash, byte[]? CreationRequestHash);

/// <summary>Result of <see cref="ICareerStore.CreateAsync"/>.</summary>
public enum CreateOutcome
{
    Created,

    /// <summary>Another career already holds the creation hash; nothing was stored.</summary>
    DuplicateCreation,
}

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
