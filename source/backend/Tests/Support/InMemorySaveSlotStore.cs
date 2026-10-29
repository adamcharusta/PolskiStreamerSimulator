using System.Collections.Concurrent;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary><see cref="ISaveSlotStore"/> over an <see cref="InMemoryCareerStore"/>: slots of a deleted career are gone, as with the cascade.</summary>
public sealed class InMemorySaveSlotStore(InMemoryCareerStore careers) : ISaveSlotStore
{
    private readonly ConcurrentDictionary<(Guid Career, int Number), SaveSlot> slots = new();

    public Task<IReadOnlyList<SaveSlot>> ListAsync(Guid careerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<SaveSlot> list = careers.Contains(careerId)
            ? [.. slots.Where(s => s.Key.Career == careerId).Select(s => s.Value).OrderBy(s => s.Number)]
            : [];
        return Task.FromResult(list);
    }

    public Task<SaveSlot?> FindAsync(Guid careerId, int number, CancellationToken cancellationToken)
        => Task.FromResult(careers.Contains(careerId) ? slots.GetValueOrDefault((careerId, number)) : null);

    public Task SaveAsync(Guid careerId, int number, CareerState state, DateTimeOffset at, CancellationToken cancellationToken)
    {
        slots[(careerId, number)] = new SaveSlot(number, state, careers.SequenceOf(careerId, state.StateVersion), at);
        return Task.CompletedTask;
    }
}
