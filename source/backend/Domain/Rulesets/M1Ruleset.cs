using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Rules of the M1 simulation prototype (minimal mechanics core).</summary>
public sealed class M1Ruleset : IRuleset
{
    public const string RulesetId = "m1-prototype.1";

    private static readonly Fixed MinimumGrowthFactor = Fixed.FromRatio(1, 5);

    public string Id => RulesetId;

    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
    {
        if (CommandRejection(s, expectedVersion) is { } error)
        {
            return Result<CareerState>.Fail(error);
        }

        if (PlanRejection(s, c.Balance, plan) is { } reason)
        {
            return Result<CareerState>.Fail(DomainErrorCode.IllegalAction, reason);
        }

        var rng = s.Rng;
        var lines = ImmutableArray.CreateBuilder<ReportLine>();
        var next = ApplyAction(s, c.Balance, plan, ref rng, lines);
        var eventIds = EventSelector.Select(next, c, plan.Category, ref rng);
        var pending = new PendingWeek(s.Week, plan, eventIds, [], Fixed.Zero, Fixed.One, 0, lines.ToImmutable());
        return Result<CareerState>.Ok(next with { Rng = rng, StateVersion = s.StateVersion + 1, Pending = pending });
    }

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c)
        => [.. AllPlans().Select(plan => (plan, PlanRejection(s, c.Balance, plan)))];

    /// <summary>Training and growth slow with skill but never fall below a fifth.</summary>
    public static Fixed GrowthFactor(Fixed skill)
        => FixedMath.Max(MinimumGrowthFactor, (Fixed.Hundred - skill) / Fixed.Hundred);

    private static DomainError? CommandRejection(CareerState s, long expectedVersion)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new DomainError(DomainErrorCode.CareerEnded, "career-ended");
        }

        return expectedVersion != s.StateVersion
            ? new DomainError(DomainErrorCode.StaleVersion, $"expected-{expectedVersion}-current-{s.StateVersion}")
            : null;
    }

    private static string? PlanRejection(CareerState s, BalanceConstants b, WeekPlan plan)
    {
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
                var gain = Fixed.FromInt(points) * GrowthFactor(s.Skill[target]);
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
            case MainAction.NoAction:
                return Stats.Add(s, StatKey.Energy, b.NoActionEnergy, plan.Category);
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, null);
        }
    }

    private static IEnumerable<WeekPlan> AllPlans()
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
        }
    }
}
