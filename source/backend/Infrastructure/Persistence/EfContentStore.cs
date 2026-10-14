using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="IContentStore"/>; parsed snapshots are cached by digest.</summary>
public sealed class EfContentStore(IDbContextFactory<GameDbContext> contexts, TimeProvider time) : IContentStore
{
    private readonly ConcurrentDictionary<string, ContentSnapshot> cache = new(StringComparer.Ordinal);

    public async Task<ContentSnapshot> EnsureStoredAsync(ContentFiles files, CancellationToken cancellationToken)
    {
        var snapshot = JsonContentSource.Parse(files.Balance, files.Events, "content files");
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        if (!await db.ContentSnapshots.AnyAsync(s => s.Digest == snapshot.Digest, cancellationToken))
        {
            db.ContentSnapshots.Add(new ContentSnapshotRow
            {
                Digest = snapshot.Digest,
                BalanceJson = files.Balance,
                EventsJson = files.Events,
                CreatedAt = time.GetUtcNow(),
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
            {
                // Another API instance stored the same digest first; the stored bytes are identical.
            }
        }

        return cache.GetOrAdd(snapshot.Digest, snapshot);
    }

    public async Task<ContentSnapshot?> FindAsync(string digest, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(digest, out var cached))
        {
            return cached;
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.ContentSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Digest == digest, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var snapshot = JsonContentSource.Parse(row.BalanceJson, row.EventsJson, $"stored snapshot {digest}");
        if (!string.Equals(snapshot.Digest, digest, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Stored content snapshot {digest} hashes to {snapshot.Digest}.");
        }

        return cache.GetOrAdd(digest, snapshot);
    }
}
