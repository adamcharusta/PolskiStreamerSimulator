using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;

namespace PolskiStreamerSimulator.Domain.Rulesets.Shared;

/// <summary>The state after a week's main action, before its events are selected.</summary>
public sealed record ActionApplied(CareerState State, Pcg32 Rng, ImmutableArray<ReportLine> Lines);

/// <summary>Plan validation and the main actions shared by the M1 and M3 rulesets.</summary>
public static class WeekActions
{
    /// <summary>
    /// A ruleset's own plan rule, checked after the shared ended and in-progress rules: the reason the plan cannot
    /// start in the state, or null.
    /// </summary>
    public delegate string? RulesetRule(CareerState s, WeekPlan plan);

    public static IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c, RulesetRule rule)
        => [.. AllPlans(s, c.Balance).Select(plan => (plan, PlanRejection(s, c, plan, rule)))];

    /// <summary>
    /// Checks the command and the plan, then applies the main action: the result carries the changed state, the
    /// advanced RNG and the action's report lines (equipment cost and stat changes).
    /// </summary>
    public static Result<ActionApplied> Apply(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion, RulesetRule rule)
    {
        if (RuleSteps.CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<ActionApplied>.Fail(error);
        }

        if (PlanRejection(s, c, plan, rule) is { } reason)
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
    /// The reason the plan cannot start, or null. An action (or a wager) whose balance section the content lacks is
    /// <c>unsupported-action</c>, before any other rule: <see cref="MainAction.NetworkCollaborate"/> needs the
    /// collaboration choice (M3); the pitch, the side business, the migration and the wager need their M3-B sections.
    /// After the shared and ruleset rules come the action's own rules (the upgrade's included), then the stake's.
    /// </summary>
    public static string? PlanRejection(CareerState s, ContentSnapshot c, WeekPlan plan, RulesetRule rule)
    {
        var b = c.Balance;
        if (!IsSupported(b, plan))
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

        return rule(s, plan) ?? ActionRejection(s, c, plan) ?? WagerRejection(s, b, plan);
    }

    private static bool IsSupported(BalanceConstants b, WeekPlan plan)
    {
        var action = plan.Action switch
        {
            MainAction.NetworkCollaborate => b.NetworkCollaborate is not null,
            MainAction.PitchFulfillSponsor => b.Sponsors is not null,
            MainAction.SideBusinessWork => b.SideBusiness is not null,
            MainAction.PlanPlatformMigration => b.Platforms is not null && b.Migration is not null,
            _ => true,
        };
        return action && (plan.Wager is null || b.Wagers is not null);
    }

    private static string? ActionRejection(CareerState s, ContentSnapshot c, WeekPlan plan)
    {
        var b = c.Balance;
        if (plan.Wager is not null && plan.Category != Category.Gambling)
        {
            return "wager-category";
        }

        return plan.Action switch
        {
            MainAction.NetworkCollaborate => EnergyRejection(s, b.NetworkEnergyCost),
            MainAction.PitchFulfillSponsor => EnergyRejection(s, b.Sponsors!.PitchEnergyCost) ?? PitchRejection(s, c),
            MainAction.SideBusinessWork => SideBusinessRejection(s, b.SideBusiness!),
            MainAction.PlanPlatformMigration => MigrationRejection(s, b.Migration!, plan),
            MainAction.ImproveProduction => UpgradeRejection(s, b, plan),
            _ => null,
        };
    }

    private static string? EnergyRejection(CareerState s, Fixed cost) => s.Energy < cost ? "not-enough-energy" : null;

    /// <summary>A pitch needs a due installment to fulfil or a sponsor that could make an offer.</summary>
    private static string? PitchRejection(CareerState s, ContentSnapshot c)
        => SponsorRules.DueThisWeek(s).IsEmpty && SponsorRules.Eligible(s, c).Count == 0 ? "no-sponsor-fits" : null;

    /// <summary>Side-business work is legal from <see cref="SideBusinessBalance.FromWeek"/> with enough followers.</summary>
    private static string? SideBusinessRejection(CareerState s, SideBusinessBalance side)
    {
        if (EnergyRejection(s, side.EnergyCost) is { } energy)
        {
            return energy;
        }

        if (s.Week.Value < side.FromWeek)
        {
            return "too-early";
        }

        return s.Followers < side.MinFollowers ? "needs-followers" : null;
    }

    /// <summary>A move needs another platform as its target, enough weeks on the current one and a target that has not banned the career.</summary>
    private static string? MigrationRejection(CareerState s, MigrationBalance migration, WeekPlan plan)
    {
        if (plan.TargetPlatform is not { } target)
        {
            return "missing-target";
        }

        if (target == Economy.Current(s))
        {
            return "same-platform";
        }

        if (EnergyRejection(s, migration.EnergyCost) is { } energy)
        {
            return energy;
        }

        if (s.Week.Value - Economy.SinceWeek(s) < migration.MinWeeksOnPlatform)
        {
            return "too-soon";
        }

        return Economy.IsBanned(s, target) ? "platform-banned" : null;
    }

    /// <summary>A stake must fit the share of spendable cash (after the plan's upgrade and a negative decision carry).</summary>
    private static string? WagerRejection(CareerState s, BalanceConstants b, WeekPlan plan)
        => plan.Wager is { } tier && !WagerRules.Affordable(b.Wagers!.StakePln[tier], WagerRules.Spendable(s, b, plan), b.Wagers)
            ? "needs-cash"
            : null;

    private static string? UpgradeRejection(CareerState s, BalanceConstants b, WeekPlan plan)
    {
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
            case MainAction.PitchFulfillSponsor:
                return Stats.Add(s, StatKey.Energy, -b.Sponsors!.PitchEnergyCost, plan.Category);
            case MainAction.SideBusinessWork:
                s = Stats.Add(s, StatKey.Energy, -b.SideBusiness!.EnergyCost, plan.Category);
                return SideBusinessRules.Work(Stats.Add(s, StatKey.Burnout, b.SideBusiness.BurnoutGain, plan.Category), b.SideBusiness);
            case MainAction.PlanPlatformMigration:
                return Stats.Add(s, StatKey.Energy, -b.Migration!.EnergyCost, plan.Category);
            case MainAction.NoAction:
                return Stats.Add(s, StatKey.Energy, b.NoActionEnergy, plan.Category);
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, null);
        }
    }

    /// <summary>
    /// Every plan the content defines, per category: the M1 and M3-A plans, then (M3-B) the pitch, side-business work
    /// and a migration to each other platform in enum order. With wagers, each Gambling plan is followed by its
    /// <c>Low</c>, <c>Medium</c> and <c>High</c> variants. Without M3-B sections this is exactly the M3-A sequence.
    /// </summary>
    public static IEnumerable<WeekPlan> AllPlans(CareerState s, BalanceConstants b)
    {
        foreach (var category in Enum.GetValues<Category>())
        {
            foreach (var plan in CategoryPlans(s, b, category))
            {
                yield return plan;
                if (category != Category.Gambling || b.Wagers is null)
                {
                    continue;
                }

                foreach (var tier in Enum.GetValues<WagerTier>())
                {
                    yield return plan with { Wager = tier };
                }
            }
        }
    }

    private static IEnumerable<WeekPlan> CategoryPlans(CareerState s, BalanceConstants b, Category category)
    {
        yield return new WeekPlan(MainAction.Rest, category);
        yield return new WeekPlan(MainAction.TrainCategory, category);
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            yield return new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: slot);
        }

        yield return new WeekPlan(MainAction.ManageCommunity, category);
        yield return new WeekPlan(MainAction.NoAction, category);
        if (b.NetworkCollaborate is not null)
        {
            yield return new WeekPlan(MainAction.NetworkCollaborate, category);
        }

        if (b.Sponsors is not null)
        {
            yield return new WeekPlan(MainAction.PitchFulfillSponsor, category);
        }

        if (b.SideBusiness is not null)
        {
            yield return new WeekPlan(MainAction.SideBusinessWork, category);
        }

        if (b.Platforms is null || b.Migration is null)
        {
            yield break;
        }

        var current = Economy.Current(s);
        foreach (var target in Enum.GetValues<Platform>().Where(p => p != current))
        {
            yield return new WeekPlan(MainAction.PlanPlatformMigration, category, TargetPlatform: target);
        }
    }
}
