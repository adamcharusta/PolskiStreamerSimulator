using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>The content new careers pin: its files, the parsed snapshot and its mechanics fingerprint (spec M4-E §4).</summary>
public interface ICurrentContent
{
    ContentFiles Files { get; }

    ContentSnapshot Snapshot { get; }

    string MechanicsHash { get; }
}

/// <summary>
/// The newest publication, switched at startup, after a publication on this instance and by the refresher for
/// publications on other instances (spec M4-E §3).
/// </summary>
public sealed class PublishedContent : ICurrentContent
{
    private volatile Current? current;

    public ContentFiles Files => Require().Files;

    public ContentSnapshot Snapshot => Require().Snapshot;

    public string MechanicsHash => Require().MechanicsHash;

    /// <summary>The number of the publication new careers pin, or <c>null</c> before startup loaded one.</summary>
    public int? Number => current?.Number;

    public void Set(int number, ContentFiles files, ContentSnapshot snapshot)
        => current = new Current(number, files, snapshot, ContentMechanics.Hash(files));

    private Current Require() => current ?? throw new InvalidOperationException("No content publication is loaded yet.");

    private sealed record Current(int Number, ContentFiles Files, ContentSnapshot Snapshot, string MechanicsHash);
}
