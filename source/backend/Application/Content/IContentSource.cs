using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>Port that supplies a validated content snapshot; Domain never knows where content is stored.</summary>
public interface IContentSource
{
    ContentSnapshot Load();
}
