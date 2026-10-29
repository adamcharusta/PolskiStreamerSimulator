using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Leaderboard;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ILeaderboardStore"/>; the unique career index makes publication idempotent.</summary>
public sealed class EfLeaderboardStore(IDbContextFactory<GameDbContext> contexts) : ILeaderboardStore
{
    public async Task<LeaderboardEntry?> FindByCareerAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.LeaderboardEntries.AsNoTracking().SingleOrDefaultAsync(e => e.CareerId == careerId, cancellationToken);
        return row is null ? null : Entry(row);
    }

    public async Task<bool> AddAsync(LeaderboardEntry entry, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.LeaderboardEntries.Add(new LeaderboardEntryRow
        {
            Id = entry.Id,
            SeasonId = entry.Season,
            CareerId = entry.CareerId,
            Nickname = entry.Nickname,
            Score = entry.Score,
            WeeksPlayed = entry.WeeksPlayed,
            Difficulty = entry.Difficulty,
            Ending = entry.Ending,
            RulesetId = entry.RulesetId,
            FinalStateVersion = entry.FinalStateVersion,
            FinalStateHash = entry.FinalStateHash,
            SubmittedAt = entry.SubmittedAt,
            HiddenAt = entry.HiddenAt,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
        {
            return false;
        }
    }

    public async Task<int> RankAsync(LeaderboardEntry entry, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var visible = db.LeaderboardEntries.AsNoTracking().Where(e => e.SeasonId == entry.Season && e.HiddenAt == null);
        var ahead = await visible.CountAsync(
            e => e.Score > entry.Score
                || (e.Score == entry.Score && e.WeeksPlayed < entry.WeeksPlayed)
                || (e.Score == entry.Score && e.WeeksPlayed == entry.WeeksPlayed && e.SubmittedAt < entry.SubmittedAt),
            cancellationToken);

        // Full ties on the first three keys are rare; their career IDs are ordered here the way SQL Server orders them.
        var tied = await visible
            .Where(e => e.Score == entry.Score && e.WeeksPlayed == entry.WeeksPlayed && e.SubmittedAt == entry.SubmittedAt)
            .Select(e => e.CareerId)
            .ToListAsync(cancellationToken);
        return 1 + ahead + tied.Count(id => LeaderboardOrder.CompareIds(id, entry.CareerId) < 0);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> TopAsync(int season, int limit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.LeaderboardEntries.AsNoTracking()
            .Where(e => e.SeasonId == season && e.HiddenAt == null)
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.WeeksPlayed)
            .ThenBy(e => e.SubmittedAt)
            .ThenBy(e => e.CareerId)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(Entry)];
    }

    private static LeaderboardEntry Entry(LeaderboardEntryRow r) => new(
        r.Id,
        r.SeasonId,
        r.CareerId,
        r.Nickname,
        r.Score,
        r.WeeksPlayed,
        r.Difficulty,
        r.Ending,
        r.RulesetId,
        r.FinalStateVersion,
        r.FinalStateHash,
        r.SubmittedAt,
        r.HiddenAt);
}
