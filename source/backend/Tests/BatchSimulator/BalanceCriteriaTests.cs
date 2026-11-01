using PolskiStreamerSimulator.BatchSimulator;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.BatchSimulator;

/// <summary>The M5-E balance criteria (#36) judge synthetic careers: each bound passes inside and fails outside.</summary>
public sealed class BalanceCriteriaTests
{
    private static CareerSummary Career(string policy, long followers, long cash, long score, EndingReason ending = EndingReason.Completed)
        => new(0, 1, policy, Background.DormRoomNewcomer, Difficulty.Normal, ending, 1040, followers, cash, score, [], "hash");

    /// <summary>Twenty careers of a policy with scores spread evenly from <paramref name="low"/> to <paramref name="high"/>.</summary>
    private static IEnumerable<CareerSummary> Spread(string policy, long followers, long cash, long low, long high, int completed = 20)
        => Enumerable.Range(0, 20).Select(i => Career(policy, followers, cash, low + ((high - low) * i / 19), i < completed ? EndingReason.Completed : EndingReason.GlobalBan));

    private static IReadOnlyList<CareerSummary> Healthy() =>
    [
        .. Spread("Balanced", 4_000_000, 30_000_000, 82_000, 101_000),
        .. Spread("Migrator", 5_000_000, 40_000_000, 85_000, 104_000),
        .. Spread("Strategy-Safe", 3_500_000, 29_000_000, 82_000, 100_000),
        .. Spread("Strategy-Bold", 700_000, 10_000_000, 56_000, 96_000, completed: 8),
    ];

    private static Criterion Named(IReadOnlyList<CareerSummary> careers, string prefix)
        => BalanceCriteria.M5Criteria(careers).Single(c => c.Name.StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public void A_healthy_run_passes_every_criterion()
        => Assert.All(BalanceCriteria.M5Criteria(Healthy()), c => Assert.Equal(BatchStatistics.Pass, c.Result));

    [Fact]
    public void A_runaway_economy_fails_and_a_saturated_score_fails()
    {
        IReadOnlyList<CareerSummary> runaway = [.. Healthy().Where(c => c.Policy != "Balanced"), .. Spread("Balanced", 96_000_000, 500_000_000, 87_000, 108_900)];

        Assert.Equal(BatchStatistics.Fail, Named(runaway, "Plausible economy").Result);
        Assert.Equal(BatchStatistics.Fail, Named(runaway, "Score keeps its range").Result);
    }

    [Fact]
    public void A_policy_far_ahead_of_balanced_is_a_dominant_path()
    {
        IReadOnlyList<CareerSummary> careers = [.. Healthy().Where(c => c.Policy != "Migrator"), .. Spread("Migrator", 6_000_000, 50_000_000, 100_000, 109_000)];

        var criterion = Named(careers, "No dominant path");
        Assert.Equal(BatchStatistics.Fail, criterion.Result);
        Assert.Contains("Migrator", criterion.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bold_route_that_never_completes_does_not_compete()
    {
        IReadOnlyList<CareerSummary> careers = [.. Healthy().Where(c => c.Policy != "Strategy-Bold"), .. Spread("Strategy-Bold", 300_000, 2_000_000, 60_000, 99_000, completed: 0)];

        Assert.Equal(BatchStatistics.Fail, Named(careers, "Both risk routes compete").Result);
    }

    [Fact]
    public void Missing_policies_are_not_applicable()
        => Assert.All(BalanceCriteria.M5Criteria([Career("Grinder", 1, 1, 1)]), c => Assert.Equal(BatchStatistics.NotApplicable, c.Result));
}
