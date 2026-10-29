using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Application.Content;

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>Percentiles 10, 50 and 90 of one measure over the simulated careers.</summary>
public sealed record SpreadView(long P10, long P50, long P90);

/// <summary>One simulated batch: how many careers, their endings, score and length spread, and every broken invariant or error.</summary>
public sealed record SimulationRunView(
    string Digest,
    int Careers,
    IReadOnlyDictionary<string, int> Endings,
    SpreadView Score,
    SpreadView Weeks,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Errors);

/// <summary>The draft and the current content replayed on the same seeds, policies, backgrounds and difficulties.</summary>
public sealed record SimulationView(SimulationRunView Draft, SimulationRunView Current, bool Passed);

/// <summary>Deterministic careers on a candidate snapshot (spec M4-E §6).</summary>
public sealed class ContentSimulation(WeekEngine engine)
{
    public const int Careers = 40;
    public const ulong Seed = 42;

    /// <summary>Whether content of <paramref name="rulesetId"/> can be played and simulated here.</summary>
    public bool Supports(string rulesetId) => engine.Supports(rulesetId);

    public SimulationRunView Run(ContentSnapshot content)
    {
        var policies = content.RulesetId switch
        {
            M3Ruleset.SystemsV2 => BuiltInPolicies.M3CAll,
            M3Ruleset.SystemsV1 => BuiltInPolicies.M3All,
            _ => BuiltInPolicies.All,
        };
        var backgrounds = Enum.GetValues<Background>();
        var results = new (CareerRunResult? Result, string? Error)[Careers];
        Parallel.For(0, Careers, i =>
        {
            try
            {
                var seed = SplitMix64(unchecked(Seed + (ulong)i));
                var difficulty = i / backgrounds.Length % 2 == 0 ? Difficulty.Normal : Difficulty.Easy;
                var start = CareerFactory.Create(content, backgrounds[i % backgrounds.Length], difficulty, PronounSet.Woman, seed);
                results[i] = (CareerRunner.Run(engine, content, start, policies[i % policies.Count]), null);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or KeyNotFoundException or IndexOutOfRangeException or NullReferenceException or OverflowException)
            {
                results[i] = (null, $"career {i}: {error.GetType().Name}: {error.Message}");
            }
        });

        var finished = results.Where(r => r.Result is not null).Select(r => r.Result!).ToList();
        return new SimulationRunView(
            content.Digest,
            Careers,
            finished
                .GroupBy(r => r.Final.Ending?.ToString() ?? "none", StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            Spread(finished.Select(r => r.Final.FinalScore?.FinalScore ?? 0)),
            Spread(finished.Select(r => (long)r.WeeksPlayed)),
            [.. results.SelectMany((r, i) => (r.Result?.InvariantViolations ?? []).Select(v => $"career {i}: {v}"))],
            [.. results.Where(r => r.Error is not null).Select(r => r.Error!)]);
    }

    /// <summary>The seed mixer of the batch simulator, so these careers match its reports for the same index.</summary>
    public static ulong SplitMix64(ulong value)
    {
        var z = unchecked(value + 0x9E3779B97F4A7C15UL);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    private static SpreadView Spread(IEnumerable<long> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0
            ? new SpreadView(0, 0, 0)
            : new SpreadView(At(sorted, 0.1), At(sorted, 0.5), At(sorted, 0.9));
    }

    private static long At(long[] sorted, double quantile) => sorted[(int)Math.Floor(quantile * (sorted.Length - 1))];
}
