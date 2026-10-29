using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>Persistence port for careers and their append-only command log.</summary>
public interface ICareerStore
{
    Task<CreateOutcome> CreateAsync(NewCareer career, CancellationToken cancellationToken);

    Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken);

    Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken);

    Task<CareerCredentials?> FindCredentialsAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>The career created under <paramref name="creationHash"/>, or <c>null</c>.</summary>
    Task<Guid?> FindByCreationAsync(byte[] creationHash, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the given hashes (a null one is kept). With <paramref name="whileVersion"/>, only while the career is
    /// still at that state version. Never changes the state, the last action or the expiry date.
    /// </summary>
    /// <returns><c>false</c> when the career is missing or has moved past <paramref name="whileVersion"/>.</returns>
    Task<bool> ReplaceCredentialsAsync(
        Guid careerId,
        byte[]? secretHash,
        byte[]? recoveryHash,
        long? whileVersion,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>Deletes up to <paramref name="limit"/> careers expired at <paramref name="now"/> with their secrets and log.</summary>
    /// <returns>How many careers were deleted.</returns>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Every logged step of one command, ordered by step; empty when the command was never stored.</summary>
    Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken);

    /// <summary>Every logged entry that carries a week report, in log order (M3-D history).</summary>
    Task<IReadOnlyList<StoredAction>> ListReportsAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>The newest logged entry that carries a week report, or <c>null</c>.</summary>
    Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction, replaces the state and appends <paramref name="action"/> when the stored version equals
    /// <paramref name="expectedVersion"/>; otherwise changes nothing. Sets the last action to <paramref name="at"/> and
    /// the expiry date by <see cref="CareerRetention"/> (and the ending date when the new state has ended).
    /// </summary>
    Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
