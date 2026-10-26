using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Throws on the given append (1-based), as a crash in the middle of a batch would.</summary>
public sealed class FailingAppendStore(ICareerStore inner, int failOnAppend) : ICareerStore
{
    private int appends;

    public Task CreateAsync(NewCareer career, CancellationToken cancellationToken) => inner.CreateAsync(career, cancellationToken);

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindAsync(careerId, cancellationToken);

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindSecretHashAsync(careerId, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
        => inner.FindCommandAsync(careerId, commandId, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> ListReportsAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.ListReportsAsync(careerId, cancellationToken);

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.FindLatestReportAsync(careerId, cancellationToken);

    public Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
        => ++appends == failOnAppend
            ? throw new InvalidOperationException("Simulated crash.")
            : inner.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
}
