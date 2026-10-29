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
    DateTimeOffset? ExtendedUntil = null)
{
    /// <summary>How long a closed season accepts results without an extension.</summary>
    public static readonly TimeSpan SubmissionWindow = TimeSpan.FromDays(90);

    /// <summary>The last moment a result is accepted; <c>null</c> while the season is open.</summary>
    public DateTimeOffset? SubmissionDeadline => ClosedAt is { } closed
        ? ExtendedUntil is { } extended && extended > closed + SubmissionWindow ? extended : closed + SubmissionWindow
        : null;

    /// <summary>Whether a result published at <paramref name="now"/> is accepted; the same moment for every career.</summary>
    public bool AcceptsAt(DateTimeOffset now) => SubmissionDeadline is not { } deadline || now <= deadline;

    /// <summary>Whether new careers created with this content belong to the season.</summary>
    public bool Pins(string rulesetId, string contentDigest)
        => ClosedAt is null && RulesetId == rulesetId && ContentDigest == contentDigest;
}

/// <summary>Persistence port for seasons and their deadline extensions.</summary>
public interface ISeasonStore
{
    /// <summary>
    /// Returns the open season for this content; if the open season has other content it is closed at
    /// <paramref name="now"/> and the next one opened, in one transaction. Safe to call concurrently.
    /// </summary>
    Task<Season> EnsureCurrentAsync(string rulesetId, string contentDigest, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The open season, or <c>null</c> before the first startup.</summary>
    Task<Season?> CurrentAsync(CancellationToken cancellationToken);

    Task<Season?> FindAsync(int number, CancellationToken cancellationToken);

    /// <summary>Every season, newest first.</summary>
    Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stores an extension as its own audit record.</summary>
    Task AddExtensionAsync(int number, DateTimeOffset deadline, string reason, string actor, DateTimeOffset at, CancellationToken cancellationToken);
}
