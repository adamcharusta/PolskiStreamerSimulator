using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3RecoveryTests
{
    private static CareerState Exhausted() => M3Play.Fresh() with { Energy = Fixed.FromInt(5), Burnout = Fixed.FromInt(99) };

    private static CareerState InCrisis() => M3Play.SettleAll(M3Play.Start(Exhausted(), MainAction.NoAction)).State;

    private static CareerState AtRecovery()
    {
        var s = InCrisis();
        while (s.PendingDecisions!.Value[0] != "burnout-recovery")
        {
            s = M3Play.ResolveQueued(s, "calm");
        }

        return s;
    }

    private static CareerState Drained(CareerState s)
    {
        while (s.PendingDecisions is { Length: > 0 })
        {
            s = M3Play.ResolveQueued(s, "calm");
        }

        return s;
    }

    [Fact]
    public void Zero_energy_after_settlement_opens_the_recovery_crisis()
        => Assert.Contains("burnout-recovery", InCrisis().PendingDecisions!.Value);

    [Fact]
    public void Week_off_leaves_only_rest_and_shortens_the_stream()
    {
        var s = Drained(M3Play.ResolveQueued(AtRecovery(), "week-off"));

        var legal = M3Play.Engine.LegalPlans(s, M3Play.C).Value;
        Assert.All(legal.Where(p => p.Plan.Action != MainAction.Rest), p => Assert.Equal("rest-only", p.DisabledReason));
        Assert.Contains(legal, p => p.Plan.Action == MainAction.Rest && p.DisabledReason is null);

        var (after, report) = M3Play.SettleAll(M3Play.Start(s, MainAction.Rest));
        Assert.Equal(-Fixed.FromInt(4), report.StatChanges.Single(l => l.Code == "stat:Energy" && l.Detail == "stream").Delta);
        Assert.False(CareerFlags.IsActive(after, "sys:rest-only"));
    }

    [Fact]
    public void Push_through_failure_still_allows_any_plan()
    {
        var s = Drained(M3Play.ResolveQueuedUntil(AtRecovery(), "push-through", RollOutcome.Failure));

        Assert.Contains(M3Play.Engine.LegalPlans(s, M3Play.C).Value, p => p.Plan.Action == MainAction.TrainCategory && p.DisabledReason is null);
    }

    [Fact]
    public void M3_never_applies_M1_forced_rest()
    {
        var s = M3Play.Fresh() with { Energy = Fixed.Zero };
        Assert.Contains(M3Play.Engine.LegalPlans(s, M3Play.C).Value, p => p.Plan.Action == MainAction.NoAction && p.DisabledReason is null);
    }
}
