using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Scoring;

/// <summary>Minimal M1 achievement set; points feed the 10% achievement component of the score.</summary>
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

    /// <summary>Earned achievements: those already held plus follower milestones reached now and, when completed, the completion award.</summary>
    public static ImmutableHashSet<string> Evaluate(CareerState s, bool completed)
    {
        var earned = s.Achievements.Union(FollowerMilestones.Where(m => s.Followers >= m.Followers).Select(m => m.Id));
        return completed ? earned.Add(CareerCompleted) : earned;
    }

    public static int Points(ImmutableHashSet<string> achievements)
        => FollowerMilestones.Where(m => achievements.Contains(m.Id)).Sum(m => m.Points)
            + (achievements.Contains(CareerCompleted) ? CareerCompletedPoints : 0);

    /// <summary>Highest follower milestone reached, or 0.</summary>
    public static int FollowersMilestone(long followers)
        => (int)FollowerMilestones.Where(m => followers >= m.Followers).Select(m => m.Followers).DefaultIfEmpty(0).Max();
}
