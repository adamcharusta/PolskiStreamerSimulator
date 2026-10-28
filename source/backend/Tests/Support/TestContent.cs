using System.Collections.Concurrent;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

public static class TestContent
{
    private static readonly Lazy<ContentSnapshot> Snapshot = new(() => new JsonContentSource(TestPaths.ContentM1).Load());

    private static readonly ConcurrentDictionary<string, ContentSnapshot> Loaded = new(StringComparer.Ordinal);

    /// <summary>The repository's M1 content snapshot.</summary>
    public static ContentSnapshot Valid() => Snapshot.Value;

    /// <summary>The snapshot of a content directory, loaded once (the M1 one is <see cref="Valid"/>).</summary>
    public static ContentSnapshot Of(string directory)
        => string.Equals(directory, TestPaths.ContentM1, StringComparison.Ordinal)
            ? Valid()
            : Loaded.GetOrAdd(directory, d => new JsonContentSource(d).Load());
}
