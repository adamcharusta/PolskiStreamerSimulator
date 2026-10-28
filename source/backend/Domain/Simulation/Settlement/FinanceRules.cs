using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation.Settlement;

/// <summary>Itemised weekly money: <c>CashDelta = Σ income − Σ costs</c>, all whole PLN.</summary>
public sealed record FinanceResult(ImmutableArray<ReportLine> Income, ImmutableArray<ReportLine> Costs, long CashDelta);

public static class FinanceRules
{
    /// <summary>
    /// Stream income (subscriptions, donations, ads) from live viewers, event cash and this week's equipment
    /// purchase carried in the pending week, living cost and per-slot equipment upkeep (easy discounts upkeep).
    /// </summary>
    public static FinanceResult Compute(CareerState s, PendingWeek p, long liveViewers, BalanceConstants b)
    {
        var live = Fixed.FromInt(liveViewers);
        var profile = b.Profile(p.Plan.Category);
        var income = ImmutableArray.CreateBuilder<ReportLine>();
        income.Add(new ReportLine("income:subscriptions", (live * b.PaidConversion * b.PlatformSubYield * b.SubPricePln * profile.SubYield).RoundToLong()));
        income.Add(new ReportLine("income:donations", (live * b.DonationYield * profile.DonationYield).RoundToLong()));
        income.Add(new ReportLine("income:ads", (live * b.AdYield * b.PlatformAdYield * b.AdLoad * profile.AdYield).RoundToLong()));
        income.AddRange(p.Lines.Where(l => l.Code.StartsWith("income:", StringComparison.Ordinal)));

        var upkeep = s.Equipment.Values.Sum(tier => b.EquipmentUpkeepPerTierPln[tier]);
        var costs = ImmutableArray.CreateBuilder<ReportLine>();
        costs.Add(new ReportLine("cost:living", Discounted(s, b.LivingCostPln, b)));
        costs.Add(new ReportLine("cost:equipment-upkeep", Discounted(s, upkeep, b)));
        costs.AddRange(p.Lines.Where(l => l.Code.StartsWith("cost:", StringComparison.Ordinal)));

        var cashDelta = income.Sum(l => l.AmountPln!.Value) - costs.Sum(l => l.AmountPln!.Value);
        return new FinanceResult(income.ToImmutable(), costs.ToImmutable(), cashDelta);
    }

    private static long Discounted(CareerState s, long amount, BalanceConstants b)
        => s.Difficulty == Difficulty.Easy ? (Fixed.FromInt(amount) * (Fixed.One - b.EasyUpkeepDiscount)).RoundToLong() : amount;
}
