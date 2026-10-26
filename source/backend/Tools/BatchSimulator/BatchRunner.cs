using System.Diagnostics;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>
/// Summary of one simulated career; <see cref="Ending"/> is null only when the run stopped on a violation. The init
/// members are the M3 measures (also filled for M1 careers): the most streamed category, final drama and credibility,
/// the coefficient of variation of weekly live viewers, the income code with the largest total, the decisions answered
/// and the weeks with a viral clip; then the M3-B measures: sponsor and total income, paid installments, sponsor
/// penalties, policy strikes, platform bans, migrations and weeks whose cash did not reconcile with their report lines.
/// </summary>
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
    string FinalHash)
{
    public Category DominantCategory { get; init; }

    public decimal Drama { get; init; }

    public decimal Credibility { get; init; }

    public double ViewerCv { get; init; }

    public string LeadingIncome { get; init; } = "";

    public int Decisions { get; init; }

    public int ViralWeeks { get; init; }

    public long SponsorIncome { get; init; }

    public long TotalIncome { get; init; }

    public int Installments { get; init; }

    public int Breaches { get; init; }

    public int Strikes { get; init; }

    public int PlatformBans { get; init; }

    public int Migrations { get; init; }

    public int ReconciliationViolations { get; init; }
}

public sealed record BatchResult(
    string RulesetId,
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

    /// <summary>Every built-in policy by name: the M1 list followed by the M3-only and M3-B-only ones.</summary>
    public static IReadOnlyList<IChoicePolicy> KnownPolicies { get; } = [.. BuiltInPolicies.All.Concat(BuiltInPolicies.M3CAll).Distinct()];

    /// <summary>
    /// The policies offered for content of <paramref name="rulesetId"/>: <see cref="BuiltInPolicies.M3CAll"/> for
    /// <c>m3-systems.2</c>, <see cref="BuiltInPolicies.M3All"/> for <c>m3-systems.1</c>, otherwise the M1 list.
    /// </summary>
    public static IReadOnlyList<IChoicePolicy> PoliciesFor(string rulesetId) => rulesetId switch
    {
        M3Ruleset.SystemsV2 => BuiltInPolicies.M3CAll,
        M3Ruleset.SystemsV1 => BuiltInPolicies.M3All,
        _ => BuiltInPolicies.All,
    };

    public static BatchResult Run(BatchOptions o) => Run(o, KnownPolicies);

    /// <summary>
    /// Runs every career independently and in parallel. Career <c>i</c> uses seed <c>SplitMix64(seed + i)</c>,
    /// policy <c>policies[i mod count]</c> and the background and difficulty of <see cref="Assign"/>; results are
    /// stored by index so the order never depends on scheduling.
    /// </summary>
    internal static BatchResult Run(BatchOptions o, IReadOnlyList<IChoicePolicy> availablePolicies)
    {
        var content = new JsonContentSource(o.ContentDirectory).Load();
        var engine = new WeekEngine(new RulesetRegistry([new M1Ruleset(), .. M3Ruleset.Versions]));
        var policies = o.Policies.Select(name => availablePolicies.Single(p => p.Name == name)).ToArray();
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
                var (background, difficulty) = Assign(i, policies.Length);
                var policy = policies[i % policies.Length];
                var start = CareerFactory.Create(content, background, difficulty, PronounSet.Woman, seed);
                var weeks = new WeekTally(start.CashPln);
                var run = CareerRunner.Run(engine, content, start, policy, weeks.Add);
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
                    run.FinalHash)
                {
                    DominantCategory = Enum.GetValues<Category>().MaxBy(category => final.Metrics.WeeksPerCategory[category]),
                    Drama = ToDecimal(final.Drama),
                    Credibility = ToDecimal(final.Credibility),
                    ViewerCv = weeks.ViewerCv,
                    LeadingIncome = final.Metrics.IncomeBySource
                        .OrderByDescending(source => source.Value)
                        .ThenBy(source => source.Key, StringComparer.Ordinal)
                        .Select(source => source.Key)
                        .FirstOrDefault() ?? "",
                    Decisions = weeks.Decisions,
                    ViralWeeks = weeks.ViralWeeks,
                    SponsorIncome = weeks.SponsorIncome,
                    TotalIncome = weeks.TotalIncome,
                    Installments = weeks.Installments,
                    Breaches = weeks.Breaches,
                    Strikes = weeks.Strikes,
                    PlatformBans = weeks.PlatformBans,
                    Migrations = weeks.Migrations,
                    ReconciliationViolations = weeks.ReconciliationViolations,
                };
            });
        stopwatch.Stop();

        using var process = Process.GetCurrentProcess();
        return new BatchResult(
            content.RulesetId,
            content.Version,
            content.Digest,
            results,
            stopwatch.Elapsed,
            GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore,
            process.PeakWorkingSet64);
    }

    /// <summary>
    /// Background and difficulty of career <paramref name="index"/>. With an odd policy count (the M1 list): background
    /// <c>i mod 4</c> and difficulty Normal/Easy alternating every four careers. With an even count that rule would pair
    /// each policy with only two backgrounds, so policy <c>p = i mod count</c> in round <c>k = i div count</c> plays
    /// combination <c>(p + k) mod 8</c>: background <c>combination mod 4</c>, Normal for combinations 0–3, Easy for 4–7.
    /// </summary>
    public static (Background Background, Difficulty Difficulty) Assign(int index, int policyCount)
    {
        var backgrounds = Enum.GetValues<Background>();
        var combination = policyCount % 2 == 1 ? index : ((index % policyCount) + (index / policyCount)) % (2 * backgrounds.Length);
        return (backgrounds[combination % backgrounds.Length], combination / backgrounds.Length % 2 == 0 ? Difficulty.Normal : Difficulty.Easy);
    }

    private const string SponsorIncomeCode = "income:sponsor";
    private const string SponsorPenaltyCode = "cost:sponsor-penalty";

    private static decimal ToDecimal(Fixed value) => (decimal)value.Raw / Fixed.Scale;

    /// <summary>Running totals over a career's settled weeks; <paramref name="startCash"/> is the cash before the first week.</summary>
    private sealed class WeekTally(long startCash)
    {
        private long cash = startCash;
        private int weeks;
        private double viewers;
        private double viewersSquared;

        public int Decisions { get; private set; }

        public int ViralWeeks { get; private set; }

        public long SponsorIncome { get; private set; }

        public long TotalIncome { get; private set; }

        public int Installments { get; private set; }

        public int Breaches { get; private set; }

        public int Strikes { get; private set; }

        public int PlatformBans { get; private set; }

        public int Migrations { get; private set; }

        /// <summary>Weeks whose income minus costs differs from the cash change or from the career's cash movement.</summary>
        public int ReconciliationViolations { get; private set; }

        /// <summary>Population standard deviation over mean of weekly live viewers; zero without viewers.</summary>
        public double ViewerCv
        {
            get
            {
                if (weeks == 0 || viewers <= 0)
                {
                    return 0;
                }

                var mean = viewers / weeks;
                return Math.Sqrt(Math.Max(0, (viewersSquared / weeks) - (mean * mean))) / mean;
            }
        }

        public void Add(CareerState after, WeekReport report)
        {
            var income = report.Income.Sum(l => l.AmountPln ?? 0);
            var costs = report.Costs.Sum(l => l.AmountPln ?? 0);
            ReconciliationViolations += income - costs != report.CashDelta || report.CashDelta != after.CashPln - cash ? 1 : 0;
            cash = after.CashPln;
            TotalIncome += income;
            SponsorIncome += report.Income.Where(l => l.Code == SponsorIncomeCode).Sum(l => l.AmountPln ?? 0);
            Installments += report.Income.Count(l => l.Code == SponsorIncomeCode);
            Breaches += report.Costs.Count(l => l.Code == SponsorPenaltyCode);
            Strikes += report.Strike is { Strike: true } ? 1 : 0;
            PlatformBans += report.Strike is { Banned: true } ? 1 : 0;
            Migrations += report.Migration is null ? 0 : 1;
            weeks++;
            viewers += report.LiveViewers;
            viewersSquared += (double)report.LiveViewers * report.LiveViewers;
            Decisions += report.DecisionRolls.IsDefault ? 0 : report.DecisionRolls.Length;
            ViralWeeks += report.Viral is null ? 0 : 1;
        }
    }
}
