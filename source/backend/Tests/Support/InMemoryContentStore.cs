using System.Collections.Concurrent;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

public sealed class InMemoryContentStore : IContentStore
{
    private readonly ConcurrentDictionary<string, ContentSnapshot> snapshots = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, byte[]> storedEvents = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, ContentFiles> storedFiles = new(StringComparer.Ordinal);

    public InMemoryContentStore(params ContentSnapshot[] initial)
    {
        foreach (var snapshot in initial)
        {
            snapshots[snapshot.Digest] = snapshot;
        }
    }

    public Task<ContentSnapshot> EnsureStoredAsync(ContentFiles files, CancellationToken cancellationToken)
    {
        var snapshot = JsonContentSource.Parse(files.Balance, files.Events, "memory");
        if (!snapshots.ContainsKey(snapshot.Digest))
        {
            var conflicts = EventVersions.Conflicts(files.Events, storedEvents.Values);
            if (conflicts.Count > 0)
            {
                throw new InvalidDataException(EventVersions.Describe(conflicts));
            }

            storedEvents[snapshot.Digest] = files.Events;
        }

        storedFiles.TryAdd(snapshot.Digest, files);

        return Task.FromResult(snapshots.GetOrAdd(snapshot.Digest, snapshot));
    }

    public Task<ContentSnapshot?> FindAsync(string digest, CancellationToken cancellationToken)
        => Task.FromResult(snapshots.TryGetValue(digest, out var snapshot) ? snapshot : null);

    public Task<ContentFiles?> FindFilesAsync(string digest, CancellationToken cancellationToken)
        => Task.FromResult(storedFiles.TryGetValue(digest, out var files) ? files : null);

    public Task<IReadOnlyList<string>> VersionConflictsAsync(byte[] events, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([.. EventVersions.Conflicts(events, storedEvents.Values).Select(k => $"event-version-changed:{k}")]);
}
