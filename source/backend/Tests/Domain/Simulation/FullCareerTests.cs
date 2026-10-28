using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class FullCareerTests
{
    public static TheoryData<string, Background> PoliciesAndBackgrounds()
    {
        var data = new TheoryData<string, Background>();
        foreach (var policy in BuiltInPolicies.All)
        {
            foreach (var background in Enum.GetValues<Background>())
            {
                data.Add(policy.Name, background);
            }
        }

        return data;
    }

    private static CareerRunResult RunBalancedDorm(ulong seed, PronounSet pronouns = PronounSet.Woman)
        => CareerRunner.Run(TestWorld.Engine, TestWorld.Content, TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, seed, pronouns), BuiltInPolicies.Balanced);

    [Theory]
    [MemberData(nameof(PoliciesAndBackgrounds))]
    public void Every_policy_and_background_reaches_a_valid_ending(string policy, Background bg)
    {
        var r = CareerRunner.Run(TestWorld.Engine, TestWorld.Content, TestCareers.For(bg, Difficulty.Normal, seed: 42), BuiltInPolicies.ByName(policy));
        Assert.Empty(r.InvariantViolations);
        Assert.Equal(CareerStatus.Ended, r.Final.Status);
        Assert.NotNull(r.Final.FinalScore);
        Assert.Equal(r.Final.Metrics.Streams, r.WeeksPlayed);
        Assert.Equal(r.Final.Ending == EndingReason.Completed ? 1040 : r.WeeksPlayed, r.Final.Week.Value);
    }

    [Fact]
    public void Seed_42_balanced_dorm_matches_golden_hash()
    {
        var r = RunBalancedDorm(42);
        var expected = File.ReadAllText(TestPaths.Golden("full-career-seed-42.txt")).Trim();
        Assert.True(expected == r.FinalHash, $"Golden hash mismatch. Actual: {r.FinalHash} (ending {r.Final.Ending}, week {r.Final.Week.Value}, score {r.Final.FinalScore?.FinalScore})");
    }

    [Fact]
    public void Same_seed_gives_the_same_career_and_other_seeds_differ()
    {
        Assert.Equal(RunBalancedDorm(7).FinalHash, RunBalancedDorm(7).FinalHash);
        Assert.NotEqual(RunBalancedDorm(7).FinalHash, RunBalancedDorm(8).FinalHash);
    }

    [Fact]
    public void Runner_reports_every_settled_week()
    {
        var weeks = new List<int>();
        var r = CareerRunner.Run(
            TestWorld.Engine,
            TestWorld.Content,
            TestCareers.Dorm(3),
            BuiltInPolicies.RestHeavy,
            (_, report) => weeks.Add(report.Week.Value));
        Assert.Equal(Enumerable.Range(1, r.WeeksPlayed), weeks);
    }

    [Fact]
    public void Runner_stops_at_the_first_invariant_violation()
    {
        var broken = TestCareers.Dorm(3) with { Followers = -1 };
        var r = CareerRunner.Run(TestWorld.Engine, TestWorld.Content, broken, BuiltInPolicies.Balanced);
        Assert.Equal(["start: followers-negative"], r.InvariantViolations);
        Assert.Equal(0, r.WeeksPlayed);
    }

    [Fact]
    public void Runner_records_rejected_commands()
    {
        var r = CareerRunner.Run(TestWorld.Engine, TestWorld.Content with { Digest = "other" }, TestCareers.Dorm(3), BuiltInPolicies.Balanced);
        var violation = Assert.Single(r.InvariantViolations);
        Assert.StartsWith("week 1 present decision: ContentMismatch", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void Pronouns_do_not_change_outcomes()
    {
        var hashes = Enum.GetValues<PronounSet>()
            .Select(p => RunBalancedDorm(42, p).Final with { Pronouns = PronounSet.Woman })
            .Select(StateHash.Of)
            .Distinct();
        Assert.Single(hashes);
    }
}
