using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Application.Leaderboard;

/// <summary>
/// A leaderboard season: the ruleset and content version new careers pinned while it was open (spec M4-B §4).
/// <see cref="ExtendedUntil"/> is the latest announced deadline extension, if any.
/// </summary>
public sealed record Season(
    int Number,
    string RulesetId,
    string ContentDigest,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ExtendedUntil = null,
    string? MechanicsHash = null)
{
    /// <summary>How long a closed season accepts results without an extension.</summary>
    public static readonly TimeSpan SubmissionWindow = TimeSpan.FromDays(90);

    /// <summary>The last moment a result is accepted; <c>null</c> while the season is open.</summary>
    public DateTimeOffset? SubmissionDeadline
    {
        get
        {
            if (ClosedAt is not { } closed)
            {
                return null;
            }

            var standard = closed + SubmissionWindow;
            return ExtendedUntil > standard ? ExtendedUntil : standard;
        }
    }

    /// <summary>Whether a result published at <paramref name="now"/> is accepted; the same moment for every career.</summary>
    public bool AcceptsAt(DateTimeOffset now) => SubmissionDeadline is not { } deadline || now <= deadline;

    /// <summary>
    /// Whether new careers with this ruleset and mechanics fingerprint belong to the open season; a text-only revision
    /// keeps the fingerprint (spec M4-E §4).
    /// </summary>
    public bool Pins(string rulesetId, string mechanicsHash)
        => ClosedAt is null && RulesetId == rulesetId && MechanicsHash == mechanicsHash;
}

/// <summary>Persistence port for seasons and their deadline extensions.</summary>
public interface ISeasonStore
{
    /// <summary>
    /// Returns the open season when it pins <paramref name="rulesetId"/> and <paramref name="mechanicsHash"/>; an open
    /// season stored before M4-E with the same <paramref name="contentDigest"/> adopts the fingerprint. Otherwise closes it
    /// and opens the next one, which records <paramref name="contentDigest"/> as the content it opened with.
    /// </summary>
    Task<Season> EnsureCurrentAsync(string rulesetId, string contentDigest, string mechanicsHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The open season, or <c>null</c> before the first startup.</summary>
    Task<Season?> CurrentAsync(CancellationToken cancellationToken);

    Task<Season?> FindAsync(int number, CancellationToken cancellationToken);

    /// <summary>Every season, newest first.</summary>
    Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stores an extension as its own audit record.</summary>
    /// <summary>Stores the later deadline with its audit record in one transaction (spec M4-D §5).</summary>
    Task AddExtensionAsync(int number, DateTimeOffset deadline, AuditEntry audit, CancellationToken cancellationToken);
}
