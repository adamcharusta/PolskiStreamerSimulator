using System.Diagnostics;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>Summary of one simulated career; <see cref="Ending"/> is null only when the run stopped on a violation.</summary>
public sealed record CareerSummary(
    int Index,
    ulong Seed,
    string Policy,
    Background Background,
    Difficulty Difficulty,
    EndingReason? Ending,
    int Weeks,
    long Followers,
    long Cash,
    long Score,
    IReadOnlyList<string> Violations,
    string FinalHash);

public sealed record BatchResult(
    string ContentVersion,
    string ContentDigest,
    IReadOnlyList<CareerSummary> Careers,
    TimeSpan Elapsed,
    long AllocatedBytes,
    long PeakWorkingSetBytes);

public static class BatchRunner
{
    /// <summary>One output of the SplitMix64 generator whose state is <paramref name="value"/> before the step.</summary>
    public static ulong SplitMix64(ulong value)
    {
        unchecked
        {
            var z = value + 0x9E3779B97F4A7C15;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            return z ^ (z >> 31);
        }
    }

    public static BatchResult Run(BatchOptions o) => Run(o, BuiltInPolicies.All);

    /// <summary>
    /// Runs every career independently and in parallel. Career <c>i</c> uses seed <c>SplitMix64(seed + i)</c>,
    /// background <c>i mod 4</c>, difficulty Normal/Easy alternating every four careers and policy
    /// <c>policies[i mod count]</c>; results are stored by index so the order never depends on scheduling.
    /// </summary>
    internal static BatchResult Run(BatchOptions o, IReadOnlyList<IChoicePolicy> availablePolicies)
    {
        var content = new JsonContentSource(o.ContentDirectory).Load();
        var engine = new WeekEngine(new RulesetRegistry([new M1Ruleset()]));
        var policies = o.Policies.Select(name => availablePolicies.Single(p => p.Name == name)).ToArray();
        var backgrounds = Enum.GetValues<Background>();
        var results = new CareerSummary[o.Careers];

        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        Parallel.For(
            0,
            o.Careers,
            new ParallelOptions { MaxDegreeOfParallelism = o.MaxDegreeOfParallelism ?? -1 },
            i =>
            {
                var seed = SplitMix64(unchecked(o.Seed + (ulong)i));
                var background = backgrounds[i % backgrounds.Length];
                var difficulty = i / backgrounds.Length % 2 == 0 ? Difficulty.Normal : Difficulty.Easy;
                var policy = policies[i % policies.Length];
                var start = CareerFactory.Create(M1Ruleset.RulesetId, content.Digest, content.Profiles[background], background, difficulty, PronounSet.Woman, seed);
                var run = CareerRunner.Run(engine, content, start, policy);
                var final = run.Final;
                results[i] = new CareerSummary(
                    i,
                    seed,
                    policy.Name,
                    background,
                    difficulty,
                    final.Ending,
                    run.WeeksPlayed,
                    final.Followers,
                    final.CashPln,
                    final.FinalScore?.FinalScore ?? 0,
                    run.InvariantViolations,
                    run.FinalHash);
            });
        stopwatch.Stop();

        using var process = Process.GetCurrentProcess();
        return new BatchResult(
            content.Version,
            content.Digest,
            results,
            stopwatch.Elapsed,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore,
            process.PeakWorkingSet64);
    }
}
