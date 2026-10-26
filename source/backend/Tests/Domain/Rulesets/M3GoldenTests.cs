using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

/// <summary>
/// Pins the final state hash of full careers on the frozen M3-A content (<c>m3-systems.1</c>, a byte copy in
/// <c>Tests/Golden/m3-systems.1</c>). Any change that alters an <c>m3-systems.1</c> result, draw order or the
/// canonical JSON of its state fails here. Regenerate only for a deliberate <c>m3-systems.1</c> change (never in M3-B).
/// </summary>
public class M3GoldenTests
{
    private const string FileName = "m3-golden-hashes.txt";

    public static ContentSnapshot Content { get; } = new JsonContentSource(TestPaths.Golden("m3-systems.1")).Load();

    public static IReadOnlyList<string> Compute()
    {
        var lines = new List<string>();
        var backgrounds = Enum.GetValues<Background>();
        foreach (var policy in BuiltInPolicies.M3All)
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var background = backgrounds[(int)(seed % (ulong)backgrounds.Length)];
                var difficulty = seed % 2 == 0 ? Difficulty.Easy : Difficulty.Normal;
                var c = Content;
                var start = CareerFactory.Create(c.RulesetId, c.Digest, c.Profiles[background], background, difficulty, PronounSet.Nonbinary, seed);
                var result = CareerRunner.Run(M3Play.Engine, c, start, policy);
                Assert.Empty(result.InvariantViolations);
                lines.Add($"{policy.Name} {seed} {result.FinalHash}");
            }
        }

        return lines;
    }

    [Fact]
    public void M3_systems_1_careers_reproduce_their_recorded_final_hashes()
    {
        Assert.Equal("m3-systems.1", Content.RulesetId);
        var path = Path.Combine(AppContext.BaseDirectory, "Domain", "Rulesets", FileName);
        var actual = Compute();
        // Regenerating writes the source copy, but the comparison reads the copy in the output directory, so the
        // first regenerating run fails; run it twice (the second run picks up the new file).
        if (Environment.GetEnvironmentVariable("UPDATE_M3_GOLDEN") == "1")
        {
            File.WriteAllLines(Path.Combine(TestPaths.RepositoryRoot, "source", "backend", "Tests", "Domain", "Rulesets", FileName), actual);
        }

        Assert.Equal(File.ReadAllLines(path), actual);
    }
}
