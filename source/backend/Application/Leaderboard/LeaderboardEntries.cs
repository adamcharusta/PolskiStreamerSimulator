using System.Data.SqlTypes;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Leaderboard;

/// <summary>
/// A published result: the public fields and the receipt to audit it (final state version and hash). It keeps the
/// career ID without a reference, so it outlives the career (spec M4-B §6). The owner hides it with a reason (M4-D §5).
/// </summary>
public sealed record LeaderboardEntry(
    Guid Id,
    int Season,
    Guid CareerId,
    string Nickname,
    long Score,
    int WeeksPlayed,
    Difficulty Difficulty,
    EndingReason Ending,
    string RulesetId,
    long FinalStateVersion,
    string FinalStateHash,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? HiddenAt = null,
    string? HiddenReason = null);

/// <summary>
/// Leaderboard order: higher score, then fewer weeks, then earlier publication, then career ID (spec M4-B §7). Career
/// IDs compare as SQL Server orders <c>uniqueidentifier</c>, so the order matches the database.
/// </summary>
public sealed class LeaderboardOrder : IComparer<LeaderboardEntry>
{
    public static readonly LeaderboardOrder Instance = new();

    public int Compare(LeaderboardEntry? x, LeaderboardEntry? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? 1 : -1;
        }

        var order = y.Score.CompareTo(x.Score);
        order = order != 0 ? order : x.WeeksPlayed.CompareTo(y.WeeksPlayed);
        order = order != 0 ? order : x.SubmittedAt.CompareTo(y.SubmittedAt);
        return order != 0 ? order : CompareIds(x.CareerId, y.CareerId);
    }

    public static int CompareIds(Guid x, Guid y) => new SqlGuid(x).CompareTo(new SqlGuid(y));
}

/// <summary>Persistence port for leaderboard entries.</summary>
public interface ILeaderboardStore
{
    Task<LeaderboardEntry?> FindByCareerAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>Stores the entry unless its career already has one.</summary>
    /// <returns><c>false</c> when the career already has an entry; nothing was stored.</returns>
    Task<bool> AddAsync(LeaderboardEntry entry, CancellationToken cancellationToken);

    /// <summary>One plus the number of visible entries of the season ordered before <paramref name="entry"/>.</summary>
    Task<int> RankAsync(LeaderboardEntry entry, CancellationToken cancellationToken);

    /// <summary>The first <paramref name="limit"/> visible entries of the season in leaderboard order.</summary>
    Task<IReadOnlyList<LeaderboardEntry>> TopAsync(int season, int limit, CancellationToken cancellationToken);
}
