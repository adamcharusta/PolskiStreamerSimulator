using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Publications and the draft in memory, with the same rules as the SQL Server store.</summary>
public sealed class InMemoryContentAdminStore(InMemoryAuditTrail audit) : IContentPublications, IContentDrafts
{
    private readonly Lock gate = new();
    private readonly List<ContentPublication> publications = [];
    private ContentDraft? draft;

    public Task<ContentPublication?> LatestAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(publications.LastOrDefault());
        }
    }

    public Task<ContentPublication?> FindAsync(int number, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(publications.SingleOrDefault(p => p.Number == number));
        }
    }

    public Task<IReadOnlyList<ContentPublication>> ListAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<ContentPublication>>([.. publications.AsEnumerable().Reverse()]);
        }
    }

    public Task<ContentPublication?> PublishAsync(NewPublication publication, AuditEntry entry, int? draftRevision, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (draftRevision is { } revision)
            {
                if (draft?.Revision != revision)
                {
                    return Task.FromResult<ContentPublication?>(null);
                }

                draft = null;
            }

            return Task.FromResult<ContentPublication?>(Add(publication, entry));
        }
    }

    public Task<ContentPublication> EnsureReleasedAsync(NewPublication release, AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!publications.Any(p => p.Digest == release.Digest && p.Kind == PublicationKind.Release))
            {
                Add(release, entry);
            }

            return Task.FromResult(publications[^1]);
        }
    }

    Task<ContentDraft?> IContentDrafts.FindAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(draft);
        }
    }

    public Task<bool> CreateAsync(ContentDraft created, AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (draft is not null)
            {
                return Task.FromResult(false);
            }

            draft = created;
            audit.Add(entry);
            return Task.FromResult(true);
        }
    }

    public Task<ContentDraft?> SaveAsync(int expectedRevision, string balanceJson, string eventsJson, AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (draft?.Revision != expectedRevision)
            {
                return Task.FromResult<ContentDraft?>(null);
            }

            draft = draft with { BalanceJson = balanceJson, EventsJson = eventsJson, Revision = expectedRevision + 1, UpdatedAt = entry.At, UpdatedBy = entry.Actor };
            audit.Add(entry);
            return Task.FromResult<ContentDraft?>(draft);
        }
    }

    public Task<bool> DeleteAsync(int expectedRevision, AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (draft?.Revision != expectedRevision)
            {
                return Task.FromResult(false);
            }

            draft = null;
            audit.Add(entry);
            return Task.FromResult(true);
        }
    }

    private ContentPublication Add(NewPublication p, AuditEntry entry)
    {
        var stored = new ContentPublication(publications.Count + 1, p.Digest, p.MechanicsHash, p.Kind, p.SourceNumber, p.Actor, p.Reason, p.At);
        publications.Add(stored);
        audit.Add(entry);
        return stored;
    }
}
