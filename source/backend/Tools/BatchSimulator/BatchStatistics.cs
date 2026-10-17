using System.Globalization;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>p5, median, p95 and maximum by the nearest-rank method.</summary>
public sealed record Distribution(long P5, long P50, long P95, long Max);

public sealed record GroupSummary(
    string Group,
    int Careers,
    int Completed,
    int Insolvent,
    int Retired,
    Distribution Followers,
    Distribution Cash,
    Distribution Score);

/// <summary>The M3-A path measures of one policy's careers (medians by the nearest-rank method).</summary>
public sealed record PathSummary(
    string Group,
    int Careers,
    int Completed,
    long MedianFollowers,
    long MedianCash,
    decimal MedianDrama,
    decimal MedianCredibility,
    double MedianViewerCv,
    string LeadingIncome)
{
    public double CompletionRate => Careers == 0 ? 0 : (double)Completed / Careers;
}

/// <summary>Mandatory decisions and viral clips of one policy's careers.</summary>
public sealed record EventPaceSummary(string Group, int Careers, long Weeks, long Decisions, long ViralWeeks)
{
    public double DecisionsPer13Weeks => Weeks == 0 ? 0 : Decisions * 13.0 / Weeks;

    public double ViralsPerCareer => Careers == 0 ? 0 : (double)ViralWeeks / Careers;
}

/// <summary>One spec §8 criterion: <c>PASS</c>, <c>FAIL</c> or <c>n/a</c> when the run lacks the policies it needs.</summary>
public sealed record Criterion(string Name, string Result, string Evidence);

public static class BatchStatistics
{
    public const string SpecialistPrefix = "Specialist-";
    public const string ReferencePolicy = "Rotator";
    public const decimal DistinctDramaPoints = 5;
    public const decimal DistinctCredibilityPoints = 5;
    public const double DistinctViewerCv = 0.05;
    public const double ViableCompletionRate = 0.90;
    public const double ViableFollowersShareOfRotator = 0.50;
    public const double MaxDecisionsPer13Weeks = 1.0;
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string NotApplicable = "n/a";

    /// <summary>Nearest-rank percentile of an ascending list: the value at rank <c>ceil(p × n / 100)</c>.</summary>
    public static long Percentile(IReadOnlyList<long> ascending, int percent)
        => ascending.Count == 0 ? 0 : ascending[Math.Max(0, ((percent * ascending.Count) + 99) / 100 - 1)];

    public static Distribution Of(IEnumerable<long> values)
    {
        var ascending = values.Order().ToArray();
        return new Distribution(Percentile(ascending, 5), Percentile(ascending, 50), Percentile(ascending, 95), ascending.Length == 0 ? 0 : ascending[^1]);
    }

    /// <summary>Groups careers in order of first appearance by index.</summary>
    public static IReadOnlyList<GroupSummary> By(IEnumerable<CareerSummary> careers, Func<CareerSummary, string> key)
        => [.. careers.OrderBy(c => c.Index).GroupBy(key).Select(g => Summarise(g.Key, [.. g]))];

    public static GroupSummary Summarise(string group, IReadOnlyList<CareerSummary> careers) => new(
        group,
        careers.Count,
        careers.Count(c => c.Ending == EndingReason.Completed),
        careers.Count(c => c.Ending == EndingReason.Insolvency),
        careers.Count(c => c.Ending == EndingReason.EarlyRetirement),
        Of(careers.Select(c => c.Followers)),
        Of(careers.Select(c => c.Cash)),
        Of(careers.Select(c => c.Score)));

    public static PathSummary Path(string group, IReadOnlyList<CareerSummary> careers) => new(
        group,
        careers.Count,
        careers.Count(c => c.Ending == EndingReason.Completed),
        Median(careers.Select(c => c.Followers)),
        Median(careers.Select(c => c.Cash)),
        Median(careers.Select(c => c.Drama)),
        Median(careers.Select(c => c.Credibility)),
        Median(careers.Select(c => c.ViewerCv)),
        careers.GroupBy(c => c.LeadingIncome, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "");

    /// <summary>Path measures of every <c>Specialist-*</c> policy in order of first appearance, then of the Rotator for reference.</summary>
    public static IReadOnlyList<PathSummary> Paths(IEnumerable<CareerSummary> careers)
        => [.. careers.OrderBy(c => c.Index)
            .GroupBy(c => c.Policy)
            .Where(g => g.Key.StartsWith(SpecialistPrefix, StringComparison.Ordinal) || g.Key == ReferencePolicy)
            .OrderBy(g => g.Key == ReferencePolicy ? 1 : 0)
            .Select(g => Path(g.Key, [.. g]))];

    public static IReadOnlyList<EventPaceSummary> Pace(IEnumerable<CareerSummary> careers)
        => [.. careers.OrderBy(c => c.Index)
            .GroupBy(c => c.Policy)
            .Select(g => new EventPaceSummary(g.Key, g.Count(), g.Sum(c => (long)c.Weeks), g.Sum(c => (long)c.Decisions), g.Sum(c => (long)c.ViralWeeks)))];

    /// <summary>Evaluates the M3-A criteria of the spec (§8): distinct paths, viable specialization and pacing.</summary>
    public static IReadOnlyList<Criterion> M3Criteria(IReadOnlyList<CareerSummary> careers)
    {
        var paths = Paths(careers);
        var specialists = paths.Where(p => p.Group.StartsWith(SpecialistPrefix, StringComparison.Ordinal)).ToArray();
        var rotator = paths.FirstOrDefault(p => p.Group == ReferencePolicy);
        var balanced = Pace(careers).FirstOrDefault(p => p.Group == "Balanced");
        return [DistinctPaths(specialists), ViableSpecialization(specialists, rotator), Pacing(balanced)];
    }

    private static Criterion DistinctPaths(PathSummary[] specialists)
    {
        const string name = "Distinct paths (#16)";
        if (specialists.Length < 2)
        {
            return new Criterion(name, NotApplicable, "Needs at least two category specialists.");
        }

        var alike = new List<string>();
        for (var i = 0; i < specialists.Length; i++)
        {
            for (var j = i + 1; j < specialists.Length; j++)
            {
                if (Alike(specialists[i], specialists[j]))
                {
                    alike.Add($"{specialists[i].Group} ~ {specialists[j].Group}");
                }
            }
        }

        return alike.Count == 0
            ? new Criterion(name, Pass, Invariant($"No two of {specialists.Length} specialists match on leading income, median drama (within {DistinctDramaPoints}), median credibility (within {DistinctCredibilityPoints}) and viewer CV (within {DistinctViewerCv:F2})."))
            : new Criterion(name, Fail, $"Matching on all four measures: {string.Join("; ", alike)}.");
    }

    private static bool Alike(PathSummary a, PathSummary b)
        => string.Equals(a.LeadingIncome, b.LeadingIncome, StringComparison.Ordinal)
            && Math.Abs(a.MedianDrama - b.MedianDrama) <= DistinctDramaPoints
            && Math.Abs(a.MedianCredibility - b.MedianCredibility) <= DistinctCredibilityPoints
            && Math.Abs(a.MedianViewerCv - b.MedianViewerCv) <= DistinctViewerCv;

    private static Criterion ViableSpecialization(PathSummary[] specialists, PathSummary? rotator)
    {
        const string name = "Viable specialization (#16, REQ-08)";
        if (specialists.Length == 0 || rotator is null)
        {
            return new Criterion(name, NotApplicable, "Needs the category specialists and the Rotator.");
        }

        var failing = specialists
            .Where(p => p.CompletionRate < ViableCompletionRate || p.MedianFollowers < ViableFollowersShareOfRotator * rotator.MedianFollowers)
            .Select(p => Invariant($"{p.Group} (completion {Percent(p.CompletionRate)}, followers {FollowerShare(p, rotator)} of Rotator)"))
            .ToArray();
        var lowestCompletion = specialists.MinBy(p => p.CompletionRate)!;
        var lowestFollowers = specialists.MinBy(p => p.MedianFollowers)!;
        return failing.Length == 0
            ? new Criterion(name, Pass, Invariant($"Lowest completion {Percent(lowestCompletion.CompletionRate)} ({lowestCompletion.Group}); lowest median followers {lowestFollowers.MedianFollowers:N0} = {FollowerShare(lowestFollowers, rotator)} of the Rotator median {rotator.MedianFollowers:N0} ({lowestFollowers.Group})."))
            : new Criterion(name, Fail, Invariant($"Below {Percent(ViableCompletionRate)} completion or {Percent(ViableFollowersShareOfRotator)} of the Rotator median followers ({rotator.MedianFollowers:N0}): {string.Join("; ", failing)}."));
    }

    private static string FollowerShare(PathSummary p, PathSummary rotator)
        => rotator.MedianFollowers == 0 ? NotApplicable : Percent((double)p.MedianFollowers / rotator.MedianFollowers);

    private static Criterion Pacing(EventPaceSummary? balanced)
    {
        const string name = "Pacing";
        if (balanced is null)
        {
            return new Criterion(name, NotApplicable, "Needs the Balanced policy.");
        }

        return new Criterion(
            name,
            balanced.DecisionsPer13Weeks <= MaxDecisionsPer13Weeks ? Pass : Fail,
            Invariant($"Balanced answers {balanced.DecisionsPer13Weeks:F2} mandatory decisions per 13 weeks (limit {MaxDecisionsPer13Weeks:F0})."));
    }

    private static T Median<T>(IEnumerable<T> values)
        where T : IComparable<T>
    {
        var ascending = values.Order().ToArray();
        return ascending.Length == 0 ? default! : ascending[Math.Max(0, ((50 * ascending.Length) + 99) / 100 - 1)];
    }

    /// <summary>A ratio as a percentage with one decimal (<c>92.5%</c>).</summary>
    public static string Percent(double ratio) => Invariant($"{ratio * 100:F1}%");

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
