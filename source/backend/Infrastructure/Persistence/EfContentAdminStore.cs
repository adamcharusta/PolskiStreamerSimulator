using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server publications and draft; each change commits with its audit record (spec M4-E §3, §5).</summary>
public sealed class EfContentAdminStore(IDbContextFactory<GameDbContext> contexts) : IContentPublications, IContentDrafts
{
    private const int DraftId = 1;

    public async Task<ContentPublication?> LatestAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.ContentPublications.AsNoTracking().OrderByDescending(p => p.Number).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Publication(row);
    }

    public async Task<ContentPublication?> FindAsync(int number, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.ContentPublications.AsNoTracking().SingleOrDefaultAsync(p => p.Number == number, cancellationToken);
        return row is null ? null : Publication(row);
    }

    public async Task<IReadOnlyList<ContentPublication>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.ContentPublications.AsNoTracking().OrderByDescending(p => p.Number).ToListAsync(cancellationToken);
        return [.. rows.Select(Publication)];
    }

    public async Task<ContentPublication?> PublishAsync(NewPublication publication, AuditEntry audit, int? draftRevision, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (draftRevision is { } revision
            && await db.ContentDrafts.Where(d => d.Id == DraftId && d.Revision == revision).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            return null;
        }

        var row = Row(publication);
        db.ContentPublications.Add(row);
        db.AuditRecords.Add(EfModerationStore.Row(audit));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Publication(row);
    }

    public async Task<ContentPublication> EnsureReleasedAsync(NewPublication release, AuditEntry audit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var kind = nameof(PublicationKind.Release);

        // Serializable: two instances starting with the same new release store it once.
        if (!await db.ContentPublications.AnyAsync(p => p.Digest == release.Digest && p.Kind == kind, cancellationToken))
        {
            db.ContentPublications.Add(Row(release));
            db.AuditRecords.Add(EfModerationStore.Row(audit));
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var latest = await db.ContentPublications.AsNoTracking().OrderByDescending(p => p.Number).FirstAsync(cancellationToken);
        return Publication(latest);
    }

    async Task<ContentDraft?> IContentDrafts.FindAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.ContentDrafts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == DraftId, cancellationToken);
        return row is null ? null : Draft(row);
    }

    public async Task<bool> CreateAsync(ContentDraft draft, AuditEntry audit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.ContentDrafts.Add(new ContentDraftRow
        {
            Id = DraftId,
            BalanceJson = draft.BalanceJson,
            EventsJson = draft.EventsJson,
            BaseNumber = draft.BaseNumber,
            Revision = draft.Revision,
            UpdatedAt = draft.UpdatedAt,
            UpdatedBy = draft.UpdatedBy,
        });
        db.AuditRecords.Add(EfModerationStore.Row(audit));
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

    public async Task<ContentDraft?> SaveAsync(int expectedRevision, string balanceJson, string eventsJson, AuditEntry audit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.ContentDrafts
            .Where(d => d.Id == DraftId && d.Revision == expectedRevision)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(d => d.BalanceJson, balanceJson)
                    .SetProperty(d => d.EventsJson, eventsJson)
                    .SetProperty(d => d.Revision, expectedRevision + 1)
                    .SetProperty(d => d.UpdatedAt, audit.At)
                    .SetProperty(d => d.UpdatedBy, audit.Actor),
                cancellationToken);
        if (updated == 0)
        {
            return null;
        }

        db.AuditRecords.Add(EfModerationStore.Row(audit));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Draft(await db.ContentDrafts.AsNoTracking().SingleAsync(d => d.Id == DraftId, cancellationToken));
    }

    public async Task<bool> DeleteAsync(int expectedRevision, AuditEntry audit, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.ContentDrafts.Where(d => d.Id == DraftId && d.Revision == expectedRevision).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            return false;
        }

        db.AuditRecords.Add(EfModerationStore.Row(audit));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static ContentPublicationRow Row(NewPublication p) => new()
    {
        Digest = p.Digest,
        MechanicsHash = p.MechanicsHash,
        Kind = p.Kind.ToString(),
        SourceNumber = p.SourceNumber,
        Actor = p.Actor,
        Reason = p.Reason,
        PublishedAt = p.At,
    };

    private static ContentPublication Publication(ContentPublicationRow r)
        => new(r.Number, r.Digest, r.MechanicsHash, Enum.Parse<PublicationKind>(r.Kind), r.SourceNumber, r.Actor, r.Reason, r.PublishedAt);

    private static ContentDraft Draft(ContentDraftRow r) => new(r.BalanceJson, r.EventsJson, r.BaseNumber, r.Revision, r.UpdatedAt, r.UpdatedBy);
}
