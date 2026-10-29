using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>
/// One manual slot of an unranked career: the saved state and the log sequence of the entry that produced it (0 when
/// no logged entry did), so a restore continues that line of play (spec M4-C §3).
/// </summary>
public sealed record SaveSlot(int Number, CareerState State, int LogSequence, DateTimeOffset SavedAt);

/// <summary>Persistence port for unranked slots; slots are deleted with their career.</summary>
public interface ISaveSlotStore
{
    /// <summary>The filled slots of a career, by number.</summary>
    Task<IReadOnlyList<SaveSlot>> ListAsync(Guid careerId, CancellationToken cancellationToken);

    Task<SaveSlot?> FindAsync(Guid careerId, int number, CancellationToken cancellationToken);

    /// <summary>Writes <paramref name="state"/> into the slot, replacing what it held; the store finds the log sequence.</summary>
    Task SaveAsync(Guid careerId, int number, CareerState state, DateTimeOffset at, CancellationToken cancellationToken);
}
