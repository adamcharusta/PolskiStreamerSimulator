using System.Text.Json;
using PolskiStreamerSimulator.BatchSimulator;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;
using BatchProgram = PolskiStreamerSimulator.BatchSimulator.Program;

namespace PolskiStreamerSimulator.Tests.BatchSimulator;

public sealed class BatchRunnerTests : IDisposable
{
    private readonly string outputDirectory = Directory.CreateTempSubdirectory("pss-batch-").FullName;

    public void Dispose() => Directory.Delete(outputDirectory, recursive: true);

    /// <summary>Always asks for an upgrade without a slot, so the first week is rejected.</summary>
    private sealed class BrokenPolicy : IChoicePolicy
    {
        public string Name => "Broken";

        public PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
            => new(new WeekPlan(MainAction.ImproveProduction, Category.Fps), PolicyReason.Of("broken"));

        public ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e) => new(e.Choices[0].ChoiceId, PolicyReason.Of("broken"));

        public bool ShouldRetire(CareerState s) => false;
    }

    private static string Deterministic(string markdown) => markdown[..markdown.IndexOf("## Performance", StringComparison.Ordinal)];

    [Fact]
    public void SplitMix64_matches_the_reference_sequence()
    {
        Assert.Equal(0xE220A8397B1DCDAFUL, BatchRunner.SplitMix64(0));
        Assert.Equal(0x6E789E6AA1B965F4UL, BatchRunner.SplitMix64(0x9E3779B97F4A7C15));
    }

    [Fact]
    public void Careers_cover_every_background_difficulty_and_policy_combination()
    {
        var o = BatchOptions.Parse(["--careers", "40", "--seed", "3"]).Value;
        var careers = BatchRunner.Run(o).Careers;
        Assert.Equal(Enumerable.Range(0, 40), careers.Select(c => c.Index));
        Assert.Equal(BatchRunner.SplitMix64(3 + 5), careers[5].Seed);
        Assert.Equal(40, careers.Select(c => (c.Policy, c.Background, c.Difficulty)).Distinct().Count());
        Assert.Equal((Background.ChessClubRegular, Difficulty.Easy, "Grinder"), (careers[6].Background, careers[6].Difficulty, careers[6].Policy));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    public void Every_policy_plays_every_background_and_difficulty(int policies)
    {
        var assignments = Enumerable.Range(0, policies * 8).Select(i => (i % policies, BatchRunner.Assign(i, policies))).ToArray();
        Assert.Equal(policies * 8, assignments.Distinct().Count());
    }

    [Fact]
    public void An_odd_policy_count_keeps_the_m1_assignment()
        => Assert.All(Enumerable.Range(0, 200), i => Assert.Equal(
            (Enum.GetValues<Background>()[i % 4], i / 4 % 2 == 0 ? Difficulty.Normal : Difficulty.Easy),
            BatchRunner.Assign(i, 5)));

    [Fact]
    public void Deterministic_section_is_identical_across_runs()
    {
        var o = BatchOptions.Parse(["--careers", "40", "--seed", "7"]).Value;
        Assert.Equal(
            Deterministic(ReportWriter.Markdown(BatchRunner.Run(o), o)),
            Deterministic(ReportWriter.Markdown(BatchRunner.Run(o with { MaxDegreeOfParallelism = 1 }), o)));
    }

    [Fact]
    public void Two_hundred_careers_have_no_invariant_failures()
        => Assert.All(BatchRunner.Run(BatchOptions.Parse(["--careers", "200"]).Value).Careers, c => Assert.Empty(c.Violations));

    [Fact]
    public void Report_lists_extremes_and_cost_of_1040_weeks()
    {
        var o = BatchOptions.Parse(["--careers", "20"]).Value;
        var md = ReportWriter.Markdown(BatchRunner.Run(o), o);
        Assert.Contains("Top 5", md, StringComparison.Ordinal);
        Assert.Contains("Bottom 5", md, StringComparison.Ordinal);
        Assert.Contains("ns per simulated week", md, StringComparison.Ordinal);
        Assert.Contains("Completion rate", md, StringComparison.Ordinal);
        Assert.Contains("| Balanced |", md, StringComparison.Ordinal);
        Assert.Contains("| DormRoomNewcomer |", md, StringComparison.Ordinal);
        Assert.Contains("Invariant failures: none", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", md.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void Json_report_separates_deterministic_results_from_performance()
    {
        var o = BatchOptions.Parse(["--careers", "8"]).Value;
        using var json = JsonDocument.Parse(ReportWriter.Json(BatchRunner.Run(o), o));
        Assert.Equal(8, json.RootElement.GetProperty("deterministic").GetProperty("careers").GetArrayLength());
        Assert.True(json.RootElement.GetProperty("performance").GetProperty("nsPerSimulatedWeek").GetInt64() > 0);
    }

    [Fact]
    public void Invariant_failure_gives_exit_code_1()
    {
        using var output = new StringWriter();
        Assert.Equal(1, BatchProgram.Run(["--careers", "2", "--policies", "Broken"], output, [new BrokenPolicy()]));
        Assert.Contains("missing-slot", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Argument_error_gives_exit_code_2()
    {
        using var output = new StringWriter();
        Assert.Equal(2, BatchProgram.Run(["--nope"], output, BuiltInPolicies.All));
        Assert.Contains("Usage", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Successful_run_writes_both_reports()
    {
        using var output = new StringWriter();
        Assert.Equal(0, BatchProgram.Run(["--careers", "4", "--out", outputDirectory], output, BuiltInPolicies.All));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "report.md")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "report.json")));
        Assert.StartsWith("# M1 batch simulation report", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void M3_content_runs_the_m3_ruleset_and_policies_and_reports_the_criteria()
    {
        using var output = new StringWriter();
        Assert.Equal(0, BatchProgram.Run(["--content", TestPaths.ContentM3, "--careers", "20", "--out", outputDirectory], output, null));
        var md = output.ToString();
        Assert.StartsWith("# M3 batch simulation report", md, StringComparison.Ordinal);
        Assert.Contains("Ruleset `m3-systems.2`", md, StringComparison.Ordinal);
        Assert.Contains($"policies: {string.Join(", ", BuiltInPolicies.M3All.Select(p => p.Name))}.", md, StringComparison.Ordinal);
        Assert.Contains("### By category specialist", md, StringComparison.Ordinal);
        Assert.Contains("| Specialist-Gambling |", md, StringComparison.Ordinal);
        Assert.Contains("### Decisions and virals", md, StringComparison.Ordinal);
        Assert.Contains("### Criteria", md, StringComparison.Ordinal);
        Assert.Contains("Distinct paths", md, StringComparison.Ordinal);
        Assert.Contains("Viable specialization", md, StringComparison.Ordinal);
        Assert.Contains("Pacing", md, StringComparison.Ordinal);
        Assert.Contains("Invariant failures: none", md, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", md.ReplaceLineEndings("\n"), StringComparison.Ordinal);

        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "report.json")));
        var deterministic = json.RootElement.GetProperty("deterministic");
        Assert.Equal("m3-systems.2", deterministic.GetProperty("rulesetId").GetString());
        Assert.Equal(10, deterministic.GetProperty("careers").EnumerateArray().Select(c => c.GetProperty("policy").GetString()).Distinct().Count());
    }

    [Fact]
    public void M1_content_keeps_the_m1_policies_and_has_no_m3_sections()
    {
        using var output = new StringWriter();
        Assert.Equal(0, BatchProgram.Run(["--content", TestPaths.ContentM1, "--careers", "5"], output, null));
        var md = output.ToString();
        Assert.Contains("Ruleset `m1-prototype.1`", md, StringComparison.Ordinal);
        Assert.Contains("policies: Balanced, Grinder, RestHeavy, Specialist, Rotator.", md, StringComparison.Ordinal);
        Assert.DoesNotContain("### Criteria", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Policies_are_checked_against_the_content_ruleset()
    {
        using var output = new StringWriter();
        Assert.Equal(2, BatchProgram.Run(["--content", TestPaths.ContentM1, "--policies", "Networker"], output, null));
        Assert.Contains("Unknown policy", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Career_rows_carry_the_m3_measures()
    {
        var o = BatchOptions.Parse(["--content", TestPaths.ContentM3, "--careers", "10", "--policies", "Networker,Specialist-Gambling"], BuiltInPolicies.M3All).Value;
        var careers = BatchRunner.Run(o, BuiltInPolicies.M3All).Careers;
        var gambler = careers[1];
        Assert.Equal("Specialist-Gambling", gambler.Policy);
        Assert.Equal(Category.Gambling, gambler.DominantCategory);
        Assert.StartsWith("income:", gambler.LeadingIncome, StringComparison.Ordinal);
        Assert.InRange(gambler.Drama, 0m, 100m);
        Assert.InRange(gambler.Credibility, 0m, 100m);
        Assert.True(gambler.ViewerCv > 0);
        Assert.True(careers.Sum(c => c.Decisions) > 0);
        Assert.True(careers.Sum(c => c.ViralWeeks) > 0);
    }

    [Fact]
    public void Criteria_compare_specialists_with_each_other_and_with_the_rotator()
    {
        CareerSummary Row(string policy, long followers, decimal drama, double cv, string income = "income:subscriptions", int decisions = 0, EndingReason ending = EndingReason.Completed)
            => new(0, 0, policy, Background.DormRoomNewcomer, Difficulty.Normal, ending, 1040, followers, 0, 0, [], "h")
            {
                Drama = drama,
                Credibility = 50,
                ViewerCv = cv,
                LeadingIncome = income,
                Decisions = decisions,
            };

        var distinct = BatchStatistics.M3Criteria(
        [
            Row("Specialist-Chess", 1000, 10, 0.10),
            Row("Specialist-Moba", 1000, 30, 0.10),
            Row("Rotator", 1000, 0, 0.1),
            Row("Balanced", 1000, 0, 0.1, decisions: 80),
        ]);
        Assert.All(distinct, c => Assert.Equal("PASS", c.Result));

        var alike = BatchStatistics.M3Criteria(
        [
            Row("Specialist-Chess", 400, 10, 0.10, ending: EndingReason.Insolvency),
            Row("Specialist-Moba", 1000, 14, 0.14),
            Row("Rotator", 1000, 0, 0.1),
            Row("Balanced", 1000, 0, 0.1, decisions: 81),
        ]);
        Assert.All(alike, c => Assert.Equal("FAIL", c.Result));
        Assert.Contains("Specialist-Chess ~ Specialist-Moba", alike[0].Evidence, StringComparison.Ordinal);

        Assert.All(BatchStatistics.M3Criteria([Row("Grinder", 1, 0, 0)]), c => Assert.Equal("n/a", c.Result));
    }
}
