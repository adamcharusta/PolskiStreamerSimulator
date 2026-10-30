using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>
/// The content files shipped with the deployment, read once on first use. Startup publishes them as a release; new
/// careers pin the newest publication (spec M4-E §3).
/// </summary>
public sealed class DirectoryCurrentContent(IOptions<ContentOptions> options) : ICurrentContent
{
    private readonly Lazy<(ContentFiles Files, ContentSnapshot Snapshot, string Mechanics)> loaded = new(() => Load(options.Value.Directory));

    public ContentFiles Files => loaded.Value.Files;

    public ContentSnapshot Snapshot => loaded.Value.Snapshot;

    public string MechanicsHash => loaded.Value.Mechanics;

    private static (ContentFiles Files, ContentSnapshot Snapshot, string Mechanics) Load(string directory)
    {
        var files = new ContentFiles(
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.BalanceFile)),
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.EventsFile)));
        return (files, JsonContentSource.Parse(files.Balance, files.Events, directory), ContentMechanics.Hash(files));
    }
}
