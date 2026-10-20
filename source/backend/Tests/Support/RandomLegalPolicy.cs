using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Test policy that picks uniformly among enabled plans and offered choices, and sometimes retires late.</summary>
public sealed class RandomLegalPolicy(ulong seed, int retireFromWeek = 900) : IChoicePolicy
{
    private Pcg32 rng = Pcg32.Create(seed, 7);

    public string Name => "RandomLegal";

    public PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
    {
        var enabled = legal.Where(l => l.DisabledReason is null).Select(l => l.Plan).ToArray();
        (var index, rng) = rng.NextInt(0, enabled.Length - 1);
        var plan = enabled[index];
        if (plan.Action != MainAction.TrainCategory)
        {
            return new PlanDecision(plan, PolicyReason.Of("random"));
        }

        (var target, rng) = rng.NextInt(0, CategoryValues.Count - 1);
        return new PlanDecision(plan with { TrainTarget = (Category)target }, PolicyReason.Of("random"));
    }

    public ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e)
    {
        var available = e.Choices.Where(o => o.UnavailableReason is null).ToArray();
        (var index, rng) = rng.NextInt(0, available.Length - 1);
        return new ChoiceDecision(available[index].ChoiceId, PolicyReason.Of("random"));
    }

    public bool ShouldRetire(CareerState s)
    {
        if (s.Week.Value < retireFromWeek)
        {
            return false;
        }

        (var roll, rng) = rng.NextInt(1, 1_000);
        return roll == 1;
    }
}
