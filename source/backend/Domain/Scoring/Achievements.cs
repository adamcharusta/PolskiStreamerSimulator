using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Scoring;

/// <summary>
/// Achievements and their points, which feed the 10% achievement component of the score: the minimal M1 set, or the
/// content's <c>balance.achievements</c> definitions when it has them (M3-C).
/// </summary>
public static class Achievements
{
    public const string CareerCompleted = "career-completed";

    private static readonly (string Id, long Followers, int Points)[] FollowerMilestones =
    [
        ("followers-1k", 1_000, 1),
        ("followers-10k", 10_000, 2),
        ("followers-100k", 100_000, 3),
    ];

    private const int CareerCompletedPoints = 4;

    /// <summary>The final drama at or below which a career has recovered from its scandal (<see cref="AchievementKind.DramaSurvived"/>).</summary>
    private static readonly Fixed RecoveredDrama = Fixed.FromInt(30);

    /// <summary>Earned achievements: those already held plus follower milestones reached now and, when completed, the completion award.</summary>
    public static ImmutableHashSet<string> Evaluate(CareerState s, bool completed)
    {
        var earned = s.Achievements.Union(FollowerMilestones.Where(m => s.Followers >= m.Followers).Select(m => m.Id));
        return completed ? earned.Add(CareerCompleted) : earned;
    }

    /// <summary>Earned achievements by <paramref name="definitions"/>, or by the M1 set when there are none; never revoked.</summary>
    public static ImmutableHashSet<string> Evaluate(CareerState s, bool completed, ImmutableArray<AchievementDefinition>? definitions)
        => definitions is { } defs
            ? s.Achievements.Union(defs.Where(d => Met(s, completed, d)).Select(d => d.Id))
            : Evaluate(s, completed);

    /// <summary>Points of the earned achievements by <paramref name="definitions"/>, or by the M1 set when there are none.</summary>
    public static int Points(ImmutableHashSet<string> achievements, ImmutableArray<AchievementDefinition>? definitions)
        => definitions is { } defs ? defs.Where(d => achievements.Contains(d.Id)).Sum(d => d.Points) : Points(achievements);

    private static bool Met(CareerState s, bool completed, AchievementDefinition d)
    {
        var threshold = d.Threshold ?? Fixed.Zero;
        return d.Kind switch
        {
            AchievementKind.Followers => Fixed.FromInt(s.Followers) >= threshold,
            AchievementKind.Cash => Fixed.FromInt(s.CashPln) >= threshold,
            AchievementKind.Completed => completed,
            AchievementKind.Mastery => s.Skill.Values.Any(skill => skill >= threshold),
            AchievementKind.Specialist => s.Metrics.WeeksPerCategory.Values.Any(weeks => Fixed.FromInt(weeks) >= threshold),
            AchievementKind.PlatformMove => s.PlatformSinceWeek is > 1 && s.BannedPlatforms is not { Length: > 0 },
            AchievementKind.SponsorInstallments => Fixed.FromInt(s.Metrics.SponsorInstallments ?? 0) >= threshold,
            AchievementKind.DramaSurvived => completed && (s.Metrics.PeakDrama ?? Fixed.Zero) >= threshold && s.Drama <= RecoveredDrama,
            _ => false,
        };
    }

    public static int Points(ImmutableHashSet<string> achievements)
        => FollowerMilestones.Where(m => achievements.Contains(m.Id)).Sum(m => m.Points)
            + (achievements.Contains(CareerCompleted) ? CareerCompletedPoints : 0);

    /// <summary>Highest follower milestone reached, or 0.</summary>
    public static int FollowersMilestone(long followers)
        => (int)FollowerMilestones.Where(m => followers >= m.Followers).Select(m => m.Followers).DefaultIfEmpty(0).Max();
}
