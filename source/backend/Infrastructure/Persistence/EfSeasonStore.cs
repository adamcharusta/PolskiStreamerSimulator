using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ISeasonStore"/>; the filtered unique index <c>ux_seasons_open</c> keeps one season open.</summary>
public sealed class EfSeasonStore(IDbContextFactory<GameDbContext> contexts) : ISeasonStore
{
    private const int RolloverAttempts = 3;

    public async Task<Season> EnsureCurrentAsync(string rulesetId, string contentDigest, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= RolloverAttempts; attempt++)
        {
            var open = await CurrentAsync(cancellationToken);
            if (open is not null && open.Pins(rulesetId, contentDigest))
            {
                return open;
            }

            try
            {
                if (await RollOverAsync(open, rulesetId, contentDigest, now, cancellationToken) is { } opened)
                {
                    return opened;
                }
            }
            catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
            {
                // Another instance opened a season first; read it on the next attempt.
            }
        }

        throw new InvalidOperationException($"The open season did not settle after {RolloverAttempts} attempts.");
    }

    public async Task<Season?> CurrentAsync(CancellationToken cancellationToken)
        => (await QueryAsync(s => s.ClosedAt == null, cancellationToken)).SingleOrDefault();

    public async Task<Season?> FindAsync(int number, CancellationToken cancellationToken)
        => (await QueryAsync(s => s.Id == number, cancellationToken)).SingleOrDefault();

    public async Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken)
        => [.. (await QueryAsync(_ => true, cancellationToken)).OrderByDescending(s => s.Number)];

    public async Task AddExtensionAsync(int number, DateTimeOffset deadline, AuditEntry audit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        // One SaveChanges is one transaction: the extension and its audit record are stored together.
        db.SeasonExtensions.Add(new SeasonExtensionRow { SeasonId = number, Deadline = deadline, Reason = audit.Reason, Actor = audit.Actor, CreatedAt = audit.At });
        db.AuditRecords.Add(EfModerationStore.Row(audit));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Closes <paramref name="seen"/>, the open season this instance read, and opens the next one in one transaction.
    /// Closing only that season keeps a slower instance from closing the season a faster one has just opened.
    /// </summary>
    /// <returns>The new season, or <c>null</c> when another instance closed <paramref name="seen"/> first.</returns>
    private async Task<Season?> RollOverAsync(Season? seen, string rulesetId, string contentDigest, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (seen is not null
            && await db.Seasons
                .Where(s => s.Id == seen.Number && s.ClosedAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ClosedAt, now), cancellationToken) == 0)
        {
            return null;
        }

        var row = new SeasonRow { RulesetId = rulesetId, ContentDigest = contentDigest, OpenedAt = now };
        db.Seasons.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Season(row.Id, rulesetId, contentDigest, now, null);
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
