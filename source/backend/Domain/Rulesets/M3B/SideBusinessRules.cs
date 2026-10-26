using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>
/// M3-B: the optional text-only side business. Work on it raises its quality; while the quality is above zero it pays
/// weekly, costs credibility and decays. Inert without a <see cref="BalanceConstants.SideBusiness"/> section.
/// </summary>
public static class SideBusinessRules
{
    /// <summary>The permanent flag set when the business opens.</summary>
    public const string OpenedFlag = "side-business";

    private const string SideBusinessDetail = "side-business";

    /// <summary>Whether the business has quality, so it pays, costs credibility and counts for sponsors and policy.</summary>
    public static bool IsOpen(CareerState s) => s.SideBusiness is { Quality.Raw: > 0 };

    /// <summary>One week of work: opens the business on first use (with the permanent flag) and adds quality, capped at 100.</summary>
    public static CareerState Work(CareerState s, SideBusinessBalance b)
    {
        var business = s.SideBusiness ?? new SideBusiness(s.Week.Value, Fixed.Zero);
        var opened = s.SideBusiness is null ? CareerFlags.Set(s, OpenedFlag, null) : s;
        return opened with { SideBusiness = business with { Quality = Stats.Bounded(business.Quality + b.QualityPerWork) } };
    }

    /// <summary><c>round(followers × incomePerFollower × quality / 100 × incomeScale)</c>.</summary>
    public static long Income(long followers, Fixed quality, SideBusinessBalance b)
        => (Fixed.FromInt(followers) * b.IncomePerFollower * quality / Fixed.Hundred * b.IncomeScale).RoundToLong();

    /// <summary>
    /// While the business is open: <c>income:side-business</c> from the quality at the start of settlement, the weekly
    /// credibility change (stat line detail <c>side-business</c>), then the quality decays, never below zero.
    /// </summary>
    public static (CareerState State, PendingWeek Pending) Settle(CareerState s, PendingWeek p, SideBusinessBalance? b)
    {
        if (b is null || !IsOpen(s))
        {
            return (s, p);
        }

        var business = s.SideBusiness!;
        var income = new ReportLine("income:side-business", AmountPln: Income(s.Followers, business.Quality, b));
        var credited = Stats.Add(s, StatKey.Credibility, b.CredibilityPerWeek, p.Plan.Category);
        var decayed = FixedMath.Max(Fixed.Zero, business.Quality - b.QualityDecay);
        return (
            credited with { SideBusiness = business with { Quality = decayed } },
            p with { Lines = p.Lines.Add(income).AddRange(Stats.Changes(s, credited, SideBusinessDetail)) });
    }
}
