using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Scoring;

/// <summary>How the audience remembers the streamer at the end of a career.</summary>
public enum Reputation
{
    /// <summary>Credibility at least 70 and drama at most 30.</summary>
    Trusted,

    /// <summary>Drama at least 60.</summary>
    Controversial,

    Steady,
}

/// <summary>An earned achievement with the points it added.</summary>
public sealed record EarnedAchievement(string Id, int Points);

/// <summary>
/// M3-C: what the ending narrative is built from (spec 2026-10-26 §7): the ending reason, the weeks played, the most
/// streamed category, the reputation, the final and the banned platforms, the peak follower milestone and the earned
/// achievements with their points, in definition order. Computed from the ended state, never stored.
/// </summary>
public sealed record EndingSummary(
    EndingReason Reason,
    int WeeksPlayed,
    Category DominantCategory,
    Reputation Reputation,
    Platform Platform,
    ImmutableArray<Platform> BannedPlatforms,
    int PeakFollowersMilestone,
    ImmutableArray<EarnedAchievement> Achievements)
{
    private static readonly Fixed TrustedCredibility = Fixed.FromInt(70);
    private static readonly Fixed TrustedDrama = Fixed.FromInt(30);
    private static readonly Fixed ControversialDrama = Fixed.FromInt(60);

    /// <summary>The summary of an ended career; null while it is active.</summary>
    public static EndingSummary? Of(CareerState s, ContentSnapshot c)
    {
        if (s.Status != CareerStatus.Ended || s.Ending is not { } reason)
        {
            return null;
        }

        return new EndingSummary(
            reason,
            s.LastSettledWeek,
            Enum.GetValues<Category>().MaxBy(category => s.Metrics.WeeksPerCategory[category]),
            ReputationOf(s),
            Economy.Current(s),
            s.BannedPlatforms is { } banned ? banned : [],
            s.Metrics.PeakFollowersMilestone,
            Earned(s, c.Balance.Achievements));
    }

    public static Reputation ReputationOf(CareerState s)
    {
        if (s.Drama >= ControversialDrama)
        {
            return Reputation.Controversial;
        }

        return s.Credibility >= TrustedCredibility && s.Drama <= TrustedDrama ? Reputation.Trusted : Reputation.Steady;
    }

    private static ImmutableArray<EarnedAchievement> Earned(CareerState s, ImmutableArray<AchievementDefinition>? definitions)
        => definitions is { } defs
            ? [.. defs.Where(d => s.Achievements.Contains(d.Id)).Select(d => new EarnedAchievement(d.Id, d.Points))]
            : [.. s.Achievements.Order(StringComparer.Ordinal).Select(id => new EarnedAchievement(id, Scoring.Achievements.Points([id])))];
}
