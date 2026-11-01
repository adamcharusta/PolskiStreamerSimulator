using static System.FormattableString;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>
/// The M5-E balance criteria (#36, design 2026-11-02 §2): a plausible economy, a score that keeps its range, no
/// dominant path, and two competitive risk routes (REQ-07). The bounds are agent proposals for owner review.
/// </summary>
public static class BalanceCriteria
{
    public const long MinBalancedFollowers = 1_000_000;
    public const long MaxBalancedFollowers = 10_000_000;
    public const long MinBalancedCash = 10_000_000;
    public const long MaxBalancedCash = 100_000_000;

    /// <summary>The largest final score of the M3 content: 100,000 × 1.1 (normal) × 1.0 (dorm-room newcomer).</summary>
    public const long MaxScore = 110_000;

    public const double MaxBalancedP95ShareOfMax = 0.95;
    public const double MinBalancedSpreadShareOfMax = 0.10;
    public const double MaxScoreLeadOverBalanced = 1.10;
    public const double MinBoldCompletion = 0.25;
    public const double MinBoldScoreShareOfSafe = 0.80;

    public static IReadOnlyList<Criterion> M5Criteria(IReadOnlyList<CareerSummary> careers)
    {
        var groups = BatchStatistics.By(careers, c => c.Policy);
        var balanced = groups.FirstOrDefault(g => g.Group == "Balanced");
        var safe = groups.FirstOrDefault(g => g.Group == "Strategy-Safe");
        var bold = groups.FirstOrDefault(g => g.Group == "Strategy-Bold");
        return [PlausibleEconomy(balanced), ScoreRange(balanced), NoDominantPath(groups, balanced), RiskRoutes(safe, bold)];
    }

    private static Criterion PlausibleEconomy(GroupSummary? balanced)
    {
        const string name = "Plausible economy (#36)";
        if (balanced is null)
        {
            return new Criterion(name, BatchStatistics.NotApplicable, "Needs the Balanced policy.");
        }

        var (followers, cash) = (balanced.Followers.P50, balanced.Cash.P50);
        var pass = followers is >= MinBalancedFollowers and <= MaxBalancedFollowers && cash is >= MinBalancedCash and <= MaxBalancedCash;
        return new Criterion(name, pass ? BatchStatistics.Pass : BatchStatistics.Fail, Invariant($"Balanced median followers {followers:N0} (between {MinBalancedFollowers:N0} and {MaxBalancedFollowers:N0}) and cash {cash:N0} PLN (between {MinBalancedCash:N0} and {MaxBalancedCash:N0})."));
    }

    private static Criterion ScoreRange(GroupSummary? balanced)
    {
        const string name = "Score keeps its range (#36)";
        if (balanced is null)
        {
            return new Criterion(name, BatchStatistics.NotApplicable, "Needs the Balanced policy.");
        }

        var score = balanced.Score;
        var spread = score.P95 - score.P5;
        var pass = score.P95 <= MaxBalancedP95ShareOfMax * MaxScore && spread >= MinBalancedSpreadShareOfMax * MaxScore;
        return new Criterion(name, pass ? BatchStatistics.Pass : BatchStatistics.Fail, Invariant($"Balanced score p95 {score.P95:N0} = {BatchStatistics.Percent((double)score.P95 / MaxScore)} of {MaxScore:N0} (at most {BatchStatistics.Percent(MaxBalancedP95ShareOfMax)}); p5–p95 spread {spread:N0} (at least {BatchStatistics.Percent(MinBalancedSpreadShareOfMax)} of the maximum)."));
    }

    private static Criterion NoDominantPath(IReadOnlyList<GroupSummary> groups, GroupSummary? balanced)
    {
        const string name = "No dominant path (#36)";
        if (balanced is null || balanced.Score.P50 == 0)
        {
            return new Criterion(name, BatchStatistics.NotApplicable, "Needs the Balanced policy with a positive median score.");
        }

        var leader = groups.MaxBy(g => g.Score.P50)!;
        var lead = (double)leader.Score.P50 / balanced.Score.P50;
        return new Criterion(name, lead <= MaxScoreLeadOverBalanced ? BatchStatistics.Pass : BatchStatistics.Fail, Invariant($"Highest median score {leader.Score.P50:N0} ({leader.Group}) = {lead:F2}x Balanced {balanced.Score.P50:N0} (at most {MaxScoreLeadOverBalanced:F2}x)."));
    }

    private static Criterion RiskRoutes(GroupSummary? safe, GroupSummary? bold)
    {
        const string name = "Both risk routes compete (REQ-07, #36)";
        if (safe is null || bold is null || safe.Score.P50 == 0)
        {
            return new Criterion(name, BatchStatistics.NotApplicable, "Needs Strategy-Safe and Strategy-Bold.");
        }

        var completion = (double)bold.Completed / bold.Careers;
        var share = (double)bold.Score.P50 / safe.Score.P50;
        var (boldSpread, safeSpread) = (bold.Score.P95 - bold.Score.P5, safe.Score.P95 - safe.Score.P5);
        var pass = completion >= MinBoldCompletion && share >= MinBoldScoreShareOfSafe && boldSpread > safeSpread;
        return new Criterion(name, pass ? BatchStatistics.Pass : BatchStatistics.Fail, Invariant($"Strategy-Bold completes {BatchStatistics.Percent(completion)} (at least {BatchStatistics.Percent(MinBoldCompletion)}); median score {bold.Score.P50:N0} = {BatchStatistics.Percent(share)} of Strategy-Safe {safe.Score.P50:N0} (at least {BatchStatistics.Percent(MinBoldScoreShareOfSafe)}); p5–p95 spread {boldSpread:N0} vs {safeSpread:N0} (wider)."));
    }
}
