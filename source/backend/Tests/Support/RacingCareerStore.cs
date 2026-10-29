using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Runs <paramref name="race"/> once, right before the first append, as a second tab or a retry would.</summary>
public sealed class RacingCareerStore(ICareerStore inner, Func<Task> race) : ForwardingCareerStore(inner)
{
    private bool raced;

    public override async Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (!raced)
        {
            raced = true;
            await race();
        }

        return await base.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
    }
}
