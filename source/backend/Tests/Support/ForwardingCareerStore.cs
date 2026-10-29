using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Passes every call to <paramref name="inner"/>; test stores override the calls they disturb.</summary>
public abstract class ForwardingCareerStore(ICareerStore inner) : ICareerStore
{
    public virtual Task<CreateOutcome> CreateAsync(NewCareer career, CancellationToken cancellationToken) => inner.CreateAsync(career, cancellationToken);

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindAsync(careerId, cancellationToken);

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindSecretHashAsync(careerId, cancellationToken);

    public Task<CareerCredentials?> FindCredentialsAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindCredentialsAsync(careerId, cancellationToken);

    public virtual Task<Guid?> FindByCreationAsync(byte[] creationHash, CancellationToken cancellationToken) => inner.FindByCreationAsync(creationHash, cancellationToken);

    public Task<bool> ReplaceCredentialsAsync(
        Guid careerId, byte[]? secretHash, byte[]? recoveryHash, long? whileVersion, DateTimeOffset at, CancellationToken cancellationToken)
        => inner.ReplaceCredentialsAsync(careerId, secretHash, recoveryHash, whileVersion, at, cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) => inner.DeleteExpiredAsync(now, limit, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
        => inner.FindCommandAsync(careerId, commandId, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> ListReportsAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.ListReportsAsync(careerId, cancellationToken);

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.FindLatestReportAsync(careerId, cancellationToken);

    public virtual Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
        => inner.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
}
