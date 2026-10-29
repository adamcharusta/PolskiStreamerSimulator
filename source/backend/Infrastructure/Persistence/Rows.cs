using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>Row of <c>game.careers</c>: the current state snapshot and display metadata.</summary>
public sealed class CareerRow
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = "";

    public string RulesetId { get; set; } = "";

    public string ContentDigest { get; set; } = "";

    public bool Ranked { get; set; }

    public CareerStatus Status { get; set; }

    public long StateVersion { get; set; }

    public string StateJson { get; set; } = "";

    public string StateHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastActionAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>When the retention sweep deletes the career (<c>CareerRetention</c>).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The season a ranked career plays for; null for an unranked career.</summary>
    public int? SeasonId { get; set; }
}

/// <summary>Row of <c>game.seasons</c>; <see cref="Id"/> is the public season number.</summary>
public sealed class SeasonRow
{
    public int Id { get; set; }

    public string RulesetId { get; set; } = "";

    public string ContentDigest { get; set; } = "";

    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>Null while the season is open; one season at most is open.</summary>
    public DateTimeOffset? ClosedAt { get; set; }
}

/// <summary>Row of <c>game.unranked_save_slots</c>: one manual slot of an unranked career (M4-C).</summary>
public sealed class SaveSlotRow
{
    public Guid CareerId { get; set; }

    public int Slot { get; set; }

    public string StateJson { get; set; } = "";

    public int LogSequence { get; set; }

    public DateTimeOffset SavedAt { get; set; }
}

/// <summary>Row of <c>game.season_extensions</c>: one announced deadline extension, kept as its own audit record.</summary>
public sealed class SeasonExtensionRow
{
    public long Id { get; set; }

    public int SeasonId { get; set; }

    public DateTimeOffset Deadline { get; set; }

    public string Reason { get; set; } = "";

    public string Actor { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Row of <c>game.leaderboard_entries</c>. <see cref="CareerId"/> is unique and deliberately not a foreign key, so
/// the entry outlives the career's retention.
/// </summary>
public sealed class LeaderboardEntryRow
{
    public Guid Id { get; set; }

    public int SeasonId { get; set; }

    public Guid CareerId { get; set; }

    public string Nickname { get; set; } = "";

    public long Score { get; set; }

    public int WeeksPlayed { get; set; }

    public Difficulty Difficulty { get; set; }

    public EndingReason Ending { get; set; }

    public string RulesetId { get; set; } = "";

    public long FinalStateVersion { get; set; }

    public string FinalStateHash { get; set; } = "";

    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset? HiddenAt { get; set; }
}

/// <summary>
/// Row of <c>game.career_secrets</c>; holds only SHA-256 hashes of the access secret, the recovery key and the
/// creation ID with its request. Careers created before M4-A have no recovery or creation hash.
/// </summary>
public sealed class CareerSecretRow
{
    public Guid CareerId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public byte[]? RecoveryHash { get; set; }

    public byte[]? CreationHash { get; set; }

    public byte[]? CreationRequestHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RotatedAt { get; set; }
}

/// <summary>Row of the append-only <c>game.career_actions</c> log.</summary>
public sealed class CareerActionRow
{
    public Guid CareerId { get; set; }

    public int Sequence { get; set; }

    public Guid CommandId { get; set; }

    public int Step { get; set; }

    public ActionKind Kind { get; set; }

    public string RequestJson { get; set; } = "";

    public string ResponseJson { get; set; } = "";

    public string? ReportJson { get; set; }

    /// <summary>On a restore, the log sequence its slot was saved at (M4-C).</summary>
    public int? RestoredFromSequence { get; set; }

    public long StateVersionAfter { get; set; }

    public string StateHashAfter { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Row of <c>game.content_snapshots</c>: the two content files byte-for-byte under their digest.</summary>
public sealed class ContentSnapshotRow
{
    public string Digest { get; set; } = "";

    public byte[] BalanceJson { get; set; } = [];

    public byte[] EventsJson { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}
