using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>The content new careers pin: its files and the parsed snapshot.</summary>
public interface ICurrentContent
{
    ContentFiles Files { get; }

    ContentSnapshot Snapshot { get; }
}
