using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>M3-B: platform policy — drama decay, exposure, strikes and bans.</summary>
public static class PolicyRules
{
    /// <summary>The permanent system flag a <see cref="EffectKind.GlobalBan"/> sets; the career ends at the next settlement.</summary>
    public const string GlobalBanFlag = "sys:global-ban";

    private const string DramaCode = "stat:Drama";
    private const string DecayDetail = "decay";

    /// <summary>True when any of the week's lines raised drama.</summary>
    public static bool AddedDrama(IEnumerable<ReportLine> lines)
        => lines.Any(l => l.Code == DramaCode && l.Delta is { } delta && delta > Fixed.Zero);

    /// <summary>
    /// Lowers drama by <see cref="PolicyBalance.DramaDecay"/> (never below zero, line detail <c>decay</c>) when the week
    /// added no drama; unchanged without a policy section.
    /// </summary>
    public static SettledCore DecayDrama(SettledCore core, IEnumerable<ReportLine> weekLines, Category primary, PolicyBalance? p)
    {
        if (p is null || AddedDrama(weekLines))
        {
            return core;
        }

        var after = Stats.Add(core.State, StatKey.Drama, Fixed.Zero - p.DramaDecay, primary);
        return core with { State = after, StatLines = core.StatLines.AddRange(Stats.Changes(core.State, after, DecayDetail)) };
    }

    /// <summary>
    /// The plan's policy exposure from the state before the week: a Gambling stream, the wager tier's exposure, an open
    /// side business (or this plan's work on it) and drama at or above <see cref="PolicyBalance.HighDramaFrom"/>. Zero
    /// without a policy section.
    /// </summary>
    public static int Exposure(CareerState s, WeekPlan plan, ContentSnapshot c)
    {
        if (c.Balance.Policy is not { } p)
        {
            return 0;
        }

        var exposure = plan.Category == Category.Gambling ? p.GamblingExposure : 0;
        if (plan.Wager is { } tier && c.Balance.Wagers is { } w)
        {
            exposure += w.Exposure[tier];
        }

        if (plan.Action == MainAction.SideBusinessWork || SideBusinessRules.IsOpen(s))
        {
            exposure += p.SideBusinessExposure;
        }

        return exposure + (s.Drama >= p.HighDramaFrom ? p.HighDramaExposure : 0);
    }

    /// <summary><c>min(cap, exposure × chance per exposure × the platform's policy risk)</c>.</summary>
    public static Fixed Chance(int exposure, Fixed policyRisk, PolicyBalance p)
        => FixedMath.Min(p.ChanceCap, Fixed.FromInt(exposure) * p.ChancePerExposure * policyRisk);

    /// <summary>The exposure, strike chance and 1–100 threshold of a plan; null without a policy section.</summary>
    public static PolicyExposure? Preview(CareerState s, WeekPlan plan, ContentSnapshot c)
    {
        if (c.Balance.Policy is not { } p)
        {
            return null;
        }

        var exposure = Exposure(s, plan, c);
        var chance = Chance(exposure, Risk(s, c.Balance), p);
        return new PolicyExposure(exposure, chance, Threshold(chance));
    }

    /// <summary>Strikes on <paramref name="platform"/> still active after <paramref name="settledWeek"/>.</summary>
    public static int ActiveStrikes(CareerState s, Platform platform, int settledWeek, PolicyBalance p)
        => Economy.ActiveStrikes(s, platform, settledWeek, p.StrikeWeeks);

    /// <summary>
    /// At settlement: drops expired strikes; with exposure above zero draws one 1–100 roll against the strike threshold
    /// on the current platform (a hit adds a strike given in <paramref name="settledWeek"/>); then bans the current
    /// platform once its active strikes reach <see cref="PolicyBalance.StrikesToBan"/> (strikes from events count too).
    /// No draw with exposure 0 or without a policy section; the roll is null when nothing was drawn or banned.
    /// </summary>
    public static (CareerState State, StrikeRoll? Roll) Settle(CareerState s, int exposure, ContentSnapshot c, int settledWeek, ref Pcg32 rng)
    {
        if (c.Balance.Policy is not { } p)
        {
            return (s, null);
        }

        if (s.Strikes is { } strikes)
        {
            var kept = strikes.Where(x => x.GivenWeek + p.StrikeWeeks > settledWeek).ToImmutableArray();
            s = s with { Strikes = kept.IsEmpty ? null : kept };
        }

        var platform = Economy.Current(s);
        int? roll = null;
        var threshold = 0;
        var hit = false;
        if (exposure > 0)
        {
            threshold = Threshold(Chance(exposure, Risk(s, c.Balance), p));
            (var drawn, rng) = rng.NextInt(1, 100);
            roll = drawn;
            hit = drawn <= threshold;
            if (hit)
            {
                s = s with { Strikes = (s.Strikes ?? []).Add(new PolicyStrike(platform, settledWeek)) };
            }
        }

        var banned = !Economy.IsBanned(s, platform) && ActiveStrikes(s, platform, settledWeek, p) >= p.StrikesToBan;
        if (banned)
        {
            s = s with { BannedPlatforms = (s.BannedPlatforms ?? []).Add(platform) };
        }

        return exposure > 0 || banned ? (s, new StrikeRoll(platform, exposure, threshold, roll, hit, banned)) : (s, null);
    }

    /// <summary>
    /// <see cref="EndingReason.GlobalBan"/> while the global-ban flag is active; <see cref="EndingReason.AllPlatformsBanned"/>
    /// when the current platform is banned and no other platform is open; otherwise none.
    /// </summary>
    public static EndingReason? Ending(CareerState s)
    {
        if (CareerFlags.IsActive(s, GlobalBanFlag))
        {
            return EndingReason.GlobalBan;
        }

        return Economy.IsBanned(s, Economy.Current(s)) && PlatformRules.Accessible(s).IsEmpty ? EndingReason.AllPlatformsBanned : null;
    }

    /// <summary>Adds <paramref name="count"/> strikes on the current platform, given in the career's current week.</summary>
    public static CareerState AddStrikes(CareerState s, int count)
        => s with { Strikes = (s.Strikes ?? []).AddRange(Enumerable.Repeat(new PolicyStrike(Economy.Current(s), s.Week.Value), count)) };

    private static Fixed Risk(CareerState s, BalanceConstants b)
        => b.Platforms is { } platforms ? platforms[Economy.Current(s)].PolicyRisk : Fixed.One;

    private static int Threshold(Fixed chance) => (int)(chance * Fixed.Hundred).RoundToLong();
}
