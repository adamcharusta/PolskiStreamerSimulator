using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>Persistence port for careers and their append-only command log.</summary>
public interface ICareerStore
{
    Task CreateAsync(NewCareer career, CancellationToken cancellationToken);

    Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken);

    Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>Every logged step of one command, ordered by step; empty when the command was never stored.</summary>
    Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken);

    /// <summary>The newest logged entry that carries a week report, or <c>null</c>.</summary>
    Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction, replaces the state and appends <paramref name="action"/> when the stored version equals
    /// <paramref name="expectedVersion"/>; otherwise changes nothing.
    /// </summary>
    Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
