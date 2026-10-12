using System.Text.Json;
using PolskiStreamerSimulator.BatchSimulator;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
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

        public WeekPlan ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
            => new(MainAction.ImproveProduction, Category.Fps);

        public string ChooseEventChoice(CareerState s, EventPresentation e) => e.Choices[0].ChoiceId;

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
}
