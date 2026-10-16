using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>Reads the content files once, on first use.</summary>
public sealed class DirectoryCurrentContent(IOptions<ContentOptions> options) : ICurrentContent
{
    private readonly Lazy<(ContentFiles Files, ContentSnapshot Snapshot)> loaded = new(() => Load(options.Value.Directory));

    public ContentFiles Files => loaded.Value.Files;

    public ContentSnapshot Snapshot => loaded.Value.Snapshot;

    private static (ContentFiles Files, ContentSnapshot Snapshot) Load(string directory)
    {
        var files = new ContentFiles(
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.BalanceFile)),
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.EventsFile)));
        return (files, JsonContentSource.Parse(files.Balance, files.Events, directory));
    }
}
