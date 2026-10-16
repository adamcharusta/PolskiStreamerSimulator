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
}

/// <summary>Row of <c>game.career_secrets</c>; holds only the SHA-256 of the access secret.</summary>
public sealed class CareerSecretRow
{
    public Guid CareerId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
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
