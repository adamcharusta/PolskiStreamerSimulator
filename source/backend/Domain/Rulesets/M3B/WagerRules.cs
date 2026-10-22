using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>The terms shown before a wager: the stake, the win chance, the payout of a win and the loss.</summary>
public sealed record WagerTerms(WagerTier Tier, long StakePln, Fixed Chance, long PayoutPln, long LossPln);

/// <summary>M3-B: the fictional-PLN wager on a Gambling stream.</summary>
public static class WagerRules
{
    private const string WagerDetail = "wager";

    /// <summary>The payout of a win is the stake times <see cref="WagerBalance.PayoutMultiple"/>; a loss costs the stake.</summary>
    public static WagerTerms Terms(WagerTier tier, WagerBalance w)
    {
        var stake = w.StakePln[tier];
        return new WagerTerms(tier, stake, w.Chance, (Fixed.FromInt(stake) * w.PayoutMultiple).RoundToLong(), stake);
    }

    /// <summary>
    /// Rolls the plan's wager once (1–100 against <c>round(100 × chance)</c>). A win adds the net win as
    /// <c>income:wager</c>, a loss the stake as <c>cost:wager</c>; both settle with the week. The audience multiplier
    /// and drama change at once (stat line detail <c>wager</c>), and the result is kept for the report.
    /// </summary>
    public static CareerState Roll(CareerState s, WeekPlan plan, WagerBalance w, ref PendingWeek pending, ref Pcg32 rng)
    {
        var terms = Terms(plan.Wager!.Value, w);
        var threshold = (int)(w.Chance * Fixed.Hundred).RoundToLong();
        (var roll, rng) = rng.NextInt(1, 100);
        var won = roll <= threshold;
        var net = won ? terms.PayoutPln - terms.StakePln : -terms.LossPln;
        var line = won
            ? new ReportLine("income:wager", AmountPln: net, Detail: terms.Tier.ToString())
            : new ReportLine("cost:wager", AmountPln: terms.LossPln, Detail: terms.Tier.ToString());
        var after = Stats.Add(s, StatKey.Drama, won ? w.WinDrama : w.LossDrama, plan.Category);
        pending = pending with
        {
            AudienceMultiplier = pending.AudienceMultiplier * (won ? w.WinAudience : w.LossAudience),
            Lines = pending.Lines.Add(line).AddRange(Stats.Changes(s, after, WagerDetail)),
            Wager = new WagerResult(terms.Tier, terms.StakePln, roll, threshold, won, net),
        };
        return after;
    }

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
