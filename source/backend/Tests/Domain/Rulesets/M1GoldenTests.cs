using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

/// <summary>
/// Pins the final state hash of full M1 careers. Any refactoring that changes an M1 result, draw order or the
/// canonical JSON of an M1 state fails here. Regenerate only for a deliberate M1 change (never in M3).
/// </summary>
public class M1GoldenTests
{
    private const string FileName = "m1-golden-hashes.txt";

    public static IReadOnlyList<string> Compute()
    {
        var lines = new List<string>();
        var backgrounds = Enum.GetValues<Background>();
        foreach (var policy in BuiltInPolicies.All)
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var background = backgrounds[(int)(seed % (ulong)backgrounds.Length)];
                var difficulty = seed % 2 == 0 ? Difficulty.Easy : Difficulty.Normal;
                var c = TestWorld.Content;
                var start = CareerFactory.Create(M1Ruleset.RulesetId, c.Digest, c.Profiles[background], background, difficulty, PronounSet.Woman, seed);
                var result = CareerRunner.Run(TestWorld.Engine, c, start, policy);
                lines.Add($"{policy.Name} {seed} {result.FinalHash}");
            }
        }

        return lines;
    }

    [Fact]
    public void M1_careers_reproduce_their_recorded_final_hashes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Domain", "Rulesets", FileName);
        var actual = Compute();
        // Regenerating writes the source copy, but the comparison reads the copy in the output directory, so the
        // first regenerating run fails; run it twice (the second run picks up the new file).
        if (Environment.GetEnvironmentVariable("UPDATE_M1_GOLDEN") == "1")
        {
            File.WriteAllLines(Path.Combine(TestPaths.RepositoryRoot, "source", "backend", "Tests", "Domain", "Rulesets", FileName), actual);
        }

        Assert.Equal(File.ReadAllLines(path), actual);
    }
}
