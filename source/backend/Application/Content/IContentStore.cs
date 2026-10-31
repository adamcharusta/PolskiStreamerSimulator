using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>The bytes of <c>balance.json</c> and <c>events.json</c>; their SHA-256 is the snapshot digest.</summary>
public sealed record ContentFiles(byte[] Balance, byte[] Events);

/// <summary>Port for content snapshots, kept so every career can load the content it was created with.</summary>
public interface IContentStore
{
    /// <summary>Stores the files under their digest unless already stored; returns the parsed snapshot.</summary>
    Task<ContentSnapshot> EnsureStoredAsync(ContentFiles files, CancellationToken cancellationToken);

    /// <summary>The snapshot with <paramref name="digest"/>, or <c>null</c> when it was never stored.</summary>
    Task<ContentSnapshot?> FindAsync(string digest, CancellationToken cancellationToken);

    /// <summary>The stored bytes of the snapshot with <paramref name="digest"/>, or <c>null</c>.</summary>
    Task<ContentFiles?> FindFilesAsync(string digest, CancellationToken cancellationToken);

    /// <summary>Every <c>event-version-changed:&lt;key&gt;</c> problem of <paramref name="events"/> against stored snapshots (TD-112).</summary>
    Task<IReadOnlyList<string>> VersionConflictsAsync(byte[] events, CancellationToken cancellationToken);
}
