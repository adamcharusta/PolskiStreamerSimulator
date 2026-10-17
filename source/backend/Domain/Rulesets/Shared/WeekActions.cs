using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>The state after a week's main action, before its events are selected.</summary>
public sealed record ActionApplied(CareerState State, Pcg32 Rng, ImmutableArray<ReportLine> Lines);

/// <summary>Plan validation and the main actions shared by the M1 and M3 rulesets.</summary>
public static class WeekActions
{
    public static IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c, bool network = false)
        => [.. AllPlans(network).Select(plan => (plan, PlanRejection(s, c.Balance, plan, network)))];

    /// <summary>
    /// Checks the command and the plan, then applies the main action: the result carries the changed state, the
    /// advanced RNG and the action's report lines (equipment cost and stat changes).
    /// </summary>
    public static Result<ActionApplied> Apply(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion, bool network = false)
    {
        if (RuleSteps.CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<ActionApplied>.Fail(error);
        }

        if (PlanRejection(s, c.Balance, plan, network) is { } reason)
        {
            return Result<ActionApplied>.Fail(DomainErrorCode.IllegalAction, reason);
        }

        var rng = s.Rng;
        var lines = ImmutableArray.CreateBuilder<ReportLine>();
        var next = ApplyAction(s, c.Balance, plan, ref rng, lines);
        lines.AddRange(Stats.Changes(s, next, "action"));
        return Result<ActionApplied>.Ok(new ActionApplied(next, rng, lines.ToImmutable()));
    }

    /// <summary>The started week: the action's state with the selected events in a new pending week.</summary>
    public static CareerState Started(CareerState s, ActionApplied applied, WeekPlan plan, ImmutableArray<string> eventIds, Pcg32 rng)
    {
        var pending = new PendingWeek(s.Week, plan, eventIds, [], Fixed.Zero, Fixed.One, 0, applied.Lines);
        return applied.State with { Rng = rng, StateVersion = s.StateVersion + 1, Pending = pending };
    }

    /// <summary>
    /// The reason the plan cannot start, or null. <paramref name="network"/> says whether the ruleset offers
    /// <see cref="MainAction.NetworkCollaborate"/> (M3); otherwise it is rejected as <c>unsupported-action</c> before any other rule.
    /// </summary>
    public static string? PlanRejection(CareerState s, BalanceConstants b, WeekPlan plan, bool network = false)
    {
        if (plan.Action == MainAction.NetworkCollaborate && (!network || b.NetworkCollaborate is null))
        {
            return "unsupported-action";
        }

        if (s.Status == CareerStatus.Ended)
        {
            return "career-ended";
        }

        if (s.Pending is not null)
        {
            return "week-in-progress";
        }

        if ((s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred) && plan.Action != MainAction.Rest)
        {
            return "forced-rest";
        }

        if (plan.Action == MainAction.NetworkCollaborate && s.Energy < b.NetworkEnergyCost)
        {
            return "not-enough-energy";
        }

        if (plan.Action != MainAction.ImproveProduction)
        {
            return null;
        }

        if (plan.UpgradeSlot is not { } slot)
        {
            return "missing-slot";
        }

        if (s.CashPln < 0)
        {
            return "negative-cash";
        }

        var tier = s.Equipment[slot];
        if (tier >= CareerInvariants.MaxEquipmentTier)
        {
            return "max-tier";
        }

        return b.EquipmentPricePln[tier + 1] > s.CashPln ? "unaffordable" : null;
    }

    private static CareerState ApplyAction(CareerState s, BalanceConstants b, WeekPlan plan, ref Pcg32 rng, ImmutableArray<ReportLine>.Builder lines)
    {
        switch (plan.Action)
        {
            case MainAction.Rest:
                s = Stats.Add(s, StatKey.Energy, b.RestEnergy, plan.Category);
                return Stats.Add(s, StatKey.Burnout, -b.RestBurnout, plan.Category);
            case MainAction.TrainCategory:
                var target = plan.TrainTarget ?? plan.Category;
                (var points, rng) = rng.NextInt(b.TrainSkillMin, b.TrainSkillMax);
                var gain = Fixed.FromInt(points) * ProgressionRules.GrowthFactor(s.Skill[target]);
                s = s with { Skill = s.Skill.With(target, Stats.Bounded(s.Skill[target] + gain)) };
                return Stats.Add(s, StatKey.Energy, -b.TrainEnergyCost, plan.Category);
            case MainAction.ImproveProduction:
                var slot = plan.UpgradeSlot!.Value;
                var tier = s.Equipment[slot] + 1;
                var price = b.EquipmentPricePln[tier];
                lines.Add(new ReportLine("cost:equipment", AmountPln: price, Detail: slot.ToString()));
                return s with { CashPln = s.CashPln - price, Equipment = s.Equipment.SetItem(slot, tier) };
            case MainAction.ManageCommunity:
                s = Stats.Add(s, StatKey.Credibility, b.ManageCommunityCredibility, plan.Category);
                s = Stats.Add(s, StatKey.Drama, -b.ManageCommunityDramaRelief, plan.Category);
                return Stats.Add(s, StatKey.PrimaryBoredom, -b.ManageCommunityBoredomRelief, plan.Category);
            case MainAction.NetworkCollaborate:
                return Stats.Add(s, StatKey.Energy, -b.NetworkEnergyCost, plan.Category);
            case MainAction.NoAction:
                return Stats.Add(s, StatKey.Energy, b.NoActionEnergy, plan.Category);
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, null);
        }
    }

    private static IEnumerable<WeekPlan> AllPlans(bool network)
    {
        foreach (var category in Enum.GetValues<Category>())
        {
            yield return new WeekPlan(MainAction.Rest, category);
            yield return new WeekPlan(MainAction.TrainCategory, category);
            foreach (var slot in Enum.GetValues<EquipmentSlot>())
            {
                yield return new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: slot);
            }

            yield return new WeekPlan(MainAction.ManageCommunity, category);
            yield return new WeekPlan(MainAction.NoAction, category);
            if (network)
            {
                yield return new WeekPlan(MainAction.NetworkCollaborate, category);
            }
        }
    }
}
