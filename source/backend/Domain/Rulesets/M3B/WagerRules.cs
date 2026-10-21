using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>M3-B: the fictional-PLN wager on a Gambling stream.</summary>
public static class WagerRules
{
    /// <summary>
    /// The cash a stake may come from: current cash, minus a negative decision carry, minus the price of the plan's
    /// equipment upgrade (when the plan buys one).
    /// </summary>
    public static long Spendable(CareerState s, BalanceConstants b, WeekPlan plan)
    {
        var carried = Math.Min(0L, s.Carry?.CashPln ?? 0L);
        var upgrade = plan is { Action: MainAction.ImproveProduction, UpgradeSlot: { } slot } && s.Equipment[slot] < CareerInvariants.MaxEquipmentTier
            ? b.EquipmentPricePln[s.Equipment[slot] + 1]
            : 0L;
        return s.CashPln + carried - upgrade;
    }

    /// <summary>Whether a stake fits: spendable cash is positive and the stake is at most its <see cref="WagerBalance.MaxShareOfCash"/>.</summary>
    public static bool Affordable(long stakePln, long spendablePln, WagerBalance w)
        => spendablePln > 0 && Fixed.FromInt(stakePln) <= Fixed.FromInt(spendablePln) * w.MaxShareOfCash;
}
