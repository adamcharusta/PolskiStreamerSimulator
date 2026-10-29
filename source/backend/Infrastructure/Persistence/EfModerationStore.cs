using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server moderation and audit trail: each change and its audit record commit together (spec M4-D §5).</summary>
public sealed class EfModerationStore(IDbContextFactory<GameDbContext> contexts) : IModerationStore, IAuditTrail
{
    public async Task<IReadOnlyList<LeaderboardEntry>> ListAsync(int season, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.LeaderboardEntries.AsNoTracking()
            .Where(e => e.SeasonId == season)
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.WeeksPlayed)
            .ThenBy(e => e.SubmittedAt)
            .ThenBy(e => e.CareerId)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(EfLeaderboardStore.Entry)];
    }

    public async Task<(ModerationOutcome Outcome, LeaderboardEntry? Entry)> SetHiddenAsync(
        Guid entryId, bool hidden, AuditEntry audit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.LeaderboardEntries.AsNoTracking().SingleOrDefaultAsync(e => e.Id == entryId, cancellationToken);
        if (row is null)
        {
            return (ModerationOutcome.NotFound, null);
        }

        if ((row.HiddenAt is not null) == hidden)
        {
            return (ModerationOutcome.Unchanged, EfLeaderboardStore.Entry(row));
        }

        DateTimeOffset? hiddenAt = hidden ? audit.At : null;
        var hiddenReason = hidden ? audit.Reason : null;

        // The visibility condition also locks the row: a concurrent opposite change waits, then finds nothing to change.
        var updated = await db.LeaderboardEntries
            .Where(e => e.Id == entryId && (hidden ? e.HiddenAt == null : e.HiddenAt != null))
            .ExecuteUpdateAsync(
                set => set.SetProperty(e => e.HiddenAt, hiddenAt).SetProperty(e => e.HiddenReason, hiddenReason),
                cancellationToken);
        if (updated == 0)
        {
            return (ModerationOutcome.Unchanged, EfLeaderboardStore.Entry(row));
        }

        db.AuditRecords.Add(Row(audit with
        {
            OldJson = HiddenState.Json(row.HiddenAt, row.HiddenReason),
            NewJson = HiddenState.Json(hiddenAt, hiddenReason),
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        row.HiddenAt = hiddenAt;
        row.HiddenReason = hiddenReason;
        return (ModerationOutcome.Changed, EfLeaderboardStore.Entry(row));
    }

    public async Task<IReadOnlyList<AuditRecord>> RecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.AuditRecords.AsNoTracking().OrderByDescending(r => r.Id).Take(limit).ToListAsync(cancellationToken);
        return [.. rows.Select(r => new AuditRecord(r.Id, new AuditEntry(r.Actor, r.Action, r.Target, r.Reason, r.OldJson, r.NewJson, r.At)))];
    }

    internal static AuditRecordRow Row(AuditEntry audit) => new()
    {
        At = audit.At,
        Actor = audit.Actor,
        Action = audit.Action,
        Target = audit.Target,
        Reason = audit.Reason,
        OldJson = audit.OldJson,
        NewJson = audit.NewJson,
    };
}
