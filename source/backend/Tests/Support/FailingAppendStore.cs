using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Throws on the given append (1-based), as a crash in the middle of a batch would.</summary>
public sealed class FailingAppendStore(ICareerStore inner, int failOnAppend) : ForwardingCareerStore(inner)
{
    private int appends;

    public override Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
        => ++appends == failOnAppend
            ? throw new InvalidOperationException("Simulated crash.")
            : base.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
}
