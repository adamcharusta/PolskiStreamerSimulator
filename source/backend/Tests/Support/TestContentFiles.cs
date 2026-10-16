using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The bytes of the repository's M1 content files.</summary>
public static class TestContentFiles
{
    public static byte[] Balance { get; } = File.ReadAllBytes(Path.Combine(TestPaths.ContentM1, JsonContentSource.BalanceFile));

    public static byte[] Events { get; } = File.ReadAllBytes(Path.Combine(TestPaths.ContentM1, JsonContentSource.EventsFile));
}
