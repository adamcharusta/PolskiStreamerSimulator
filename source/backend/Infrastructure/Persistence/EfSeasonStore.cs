using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Leaderboard;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ISeasonStore"/>; the filtered unique index <c>ux_seasons_open</c> keeps one season open.</summary>
public sealed class EfSeasonStore(IDbContextFactory<GameDbContext> contexts) : ISeasonStore
{
    private const int RolloverAttempts = 3;

    public async Task<Season> EnsureCurrentAsync(string rulesetId, string contentDigest, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (await CurrentAsync(cancellationToken) is { } open && open.Pins(rulesetId, contentDigest))
            {
                return open;
            }

            try
            {
                await RollOverAsync(rulesetId, contentDigest, now, cancellationToken);
            }
            catch (DbUpdateException error) when (attempt < RolloverAttempts && SqlErrors.IsDuplicateKey(error))
            {
                // Another instance opened a season first; read it on the next attempt.
            }
        }
    }

    public async Task<Season?> CurrentAsync(CancellationToken cancellationToken)
        => (await QueryAsync(s => s.ClosedAt == null, cancellationToken)).SingleOrDefault();

    public async Task<Season?> FindAsync(int number, CancellationToken cancellationToken)
        => (await QueryAsync(s => s.Id == number, cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken)
        => [.. (await QueryAsync(_ => true, cancellationToken)).OrderByDescending(s => s.Number)];

    public async Task AddExtensionAsync(int number, DateTimeOffset deadline, string reason, string actor, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.SeasonExtensions.Add(new SeasonExtensionRow { SeasonId = number, Deadline = deadline, Reason = reason, Actor = actor, CreatedAt = at });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Closes the open season, if any, and opens the next one in one transaction.</summary>
    private async Task RollOverAsync(string rulesetId, string contentDigest, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Seasons.Where(s => s.ClosedAt == null).ExecuteUpdateAsync(u => u.SetProperty(s => s.ClosedAt, now), cancellationToken);
        db.Seasons.Add(new SeasonRow { RulesetId = rulesetId, ContentDigest = contentDigest, OpenedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<List<Season>> QueryAsync(System.Linq.Expressions.Expression<Func<SeasonRow, bool>> filter, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.Seasons.AsNoTracking()
            .Where(filter)
            .Select(s => new Season(
                s.Id,
                s.RulesetId,
                s.ContentDigest,
                s.OpenedAt,
                s.ClosedAt,
                db.SeasonExtensions.Where(x => x.SeasonId == s.Id).Max(x => (DateTimeOffset?)x.Deadline)))
            .ToListAsync(cancellationToken);
    }
}
