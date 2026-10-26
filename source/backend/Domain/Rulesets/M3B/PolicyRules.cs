using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>M3-B: platform policy — drama decay, exposure, strikes and bans.</summary>
public static class PolicyRules
{
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
}
