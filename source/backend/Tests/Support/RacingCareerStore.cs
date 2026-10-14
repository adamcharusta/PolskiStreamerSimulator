using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Runs <paramref name="race"/> once, right before the first append, as a second tab or a retry would.</summary>
public sealed class RacingCareerStore(ICareerStore inner, Func<Task> race) : ICareerStore
{
    private bool raced;

    public Task CreateAsync(NewCareer career, CancellationToken cancellationToken) => inner.CreateAsync(career, cancellationToken);

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindAsync(careerId, cancellationToken);

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindSecretHashAsync(careerId, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
        => inner.FindCommandAsync(careerId, commandId, cancellationToken);

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.FindLatestReportAsync(careerId, cancellationToken);

    public async Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (!raced)
        {
            raced = true;
            await race();
        }

        return await inner.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
    }
}
