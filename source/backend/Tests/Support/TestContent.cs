using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

public static class TestContent
{
    private static readonly Lazy<ContentSnapshot> Snapshot = new(() => new JsonContentSource(TestPaths.ContentM1).Load());

    /// <summary>The repository's M1 content snapshot.</summary>
    public static ContentSnapshot Valid() => Snapshot.Value;
}
