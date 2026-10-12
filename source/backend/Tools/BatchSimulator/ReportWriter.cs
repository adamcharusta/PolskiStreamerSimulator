using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>
/// Writes the batch report. Everything before <c>## Performance</c> depends only on the options and content,
/// so identical arguments give byte-identical deterministic sections.
/// </summary>
public static class ReportWriter
{
    private const int ExtremeCount = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Markdown(BatchResult r, BatchOptions o)
    {
        var careers = r.Careers;
        var completed = careers.Count(c => c.Ending == EndingReason.Completed);
        var md = new StringBuilder();
        md.AppendLine("# M1 batch simulation report");
        md.AppendLine();
        md.AppendLine("## Deterministic results");
        md.AppendLine();
        md.AppendLine(Invariant($"- Careers: {careers.Count:N0}; master seed {o.Seed}; policies: {string.Join(", ", o.Policies)}."));
        md.AppendLine(Invariant($"- Ruleset `{M1Ruleset.RulesetId}`; content {r.ContentVersion} (`{r.ContentDigest}`)."));
        md.AppendLine(Invariant($"- Completion rate: {Share(completed, careers.Count)} ({completed:N0} of {careers.Count:N0})."));
        md.AppendLine();

        md.AppendLine("### Endings");
        md.AppendLine();
        md.AppendLine("| Ending | Careers | Share |");
        md.AppendLine("| --- | ---: | ---: |");
        foreach (var ending in Enum.GetValues<EndingReason>())
        {
            var count = careers.Count(c => c.Ending == ending);
            md.AppendLine(Invariant($"| {ending} | {count:N0} | {Share(count, careers.Count)} |"));
        }

        var unfinished = careers.Count(c => c.Ending is null);
        if (unfinished > 0)
        {
            md.AppendLine(Invariant($"| Stopped on violation | {unfinished:N0} | {Share(unfinished, careers.Count)} |"));
        }

        md.AppendLine();
        AppendGroups(md, "By policy", "Policy", BatchStatistics.By(careers, c => c.Policy));
        AppendGroups(md, "By background", "Background", BatchStatistics.By(careers, c => c.Background.ToString()));
        AppendGroups(md, "By difficulty", "Difficulty", BatchStatistics.By(careers, c => c.Difficulty.ToString()));

        var byScore = careers.OrderByDescending(c => c.Score).ThenBy(c => c.Index).ToArray();
        AppendCareers(md, $"Top {ExtremeCount} careers by score", byScore.Take(ExtremeCount));
        AppendCareers(md, $"Bottom {ExtremeCount} careers by score", careers.OrderBy(c => c.Score).ThenBy(c => c.Index).Take(ExtremeCount));

        md.AppendLine("### Invariant failures");
        md.AppendLine();
        var failed = careers.Where(c => c.Violations.Count > 0).ToArray();
        if (failed.Length == 0)
        {
            md.AppendLine("Invariant failures: none.");
        }

        foreach (var career in failed)
        {
            md.AppendLine(Invariant($"- Career {career.Index} (seed {career.Seed}, {career.Policy}): {string.Join("; ", career.Violations)}"));
        }

        md.AppendLine();
        md.AppendLine("## Performance");
        md.AppendLine();
        var weeks = careers.Sum(c => (long)c.Weeks);
        md.AppendLine("| Metric | Value |");
        md.AppendLine("| --- | ---: |");
        md.AppendLine(Invariant($"| Elapsed | {r.Elapsed.TotalSeconds:F3} s |"));
        md.AppendLine(Invariant($"| Careers per second | {careers.Count / Math.Max(r.Elapsed.TotalSeconds, 1e-9):F1} |"));
        md.AppendLine(Invariant($"| Simulated weeks | {weeks:N0} |"));
        md.AppendLine(Invariant($"| ns per simulated week | {NanosecondsPerWeek(r):N0} |"));
        md.AppendLine(Invariant($"| Mean wall time per career | {r.Elapsed.TotalMilliseconds / Math.Max(careers.Count, 1):F2} ms |"));
        md.AppendLine(Invariant($"| Allocated | {r.AllocatedBytes / 1_048_576.0:F1} MiB ({r.AllocatedBytes:N0} bytes) |"));
        md.AppendLine(Invariant($"| Peak working set | {r.PeakWorkingSetBytes / 1_048_576.0:F1} MiB |"));
        md.AppendLine(Invariant($"| Parallelism | {(o.MaxDegreeOfParallelism is { } p ? p.ToString(CultureInfo.InvariantCulture) : "unbounded")} on {Environment.ProcessorCount} logical processors |"));
        md.AppendLine(Invariant($"| Runtime | {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.OSDescription}) |"));
        return md.ToString();
    }

    public static string Json(BatchResult r, BatchOptions o)
    {
        var report = new
        {
            deterministic = new
            {
                CareerCount = o.Careers,
                o.Seed,
                o.Policies,
                RulesetId = M1Ruleset.RulesetId,
                r.ContentVersion,
                r.ContentDigest,
                ByPolicy = BatchStatistics.By(r.Careers, c => c.Policy),
                ByBackground = BatchStatistics.By(r.Careers, c => c.Background.ToString()),
                ByDifficulty = BatchStatistics.By(r.Careers, c => c.Difficulty.ToString()),
                r.Careers,
            },
            performance = new
            {
                ElapsedMilliseconds = (long)r.Elapsed.TotalMilliseconds,
                NsPerSimulatedWeek = NanosecondsPerWeek(r),
                r.AllocatedBytes,
                r.PeakWorkingSetBytes,
                o.MaxDegreeOfParallelism,
                Environment.ProcessorCount,
                Runtime = RuntimeInformation.FrameworkDescription,
            },
        };
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static void AppendGroups(StringBuilder md, string title, string column, IReadOnlyList<GroupSummary> groups)
    {
        md.AppendLine($"### {title}");
        md.AppendLine();
        md.AppendLine($"| {column} | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var g in groups)
        {
            md.AppendLine(Invariant($"| {g.Group} | {g.Careers:N0} | {g.Completed:N0} | {g.Insolvent:N0} | {Format(g.Followers)} | {Format(g.Cash)} | {Format(g.Score)} |"));
        }

        md.AppendLine();
    }

    private static void AppendCareers(StringBuilder md, string title, IEnumerable<CareerSummary> careers)
    {
        md.AppendLine($"### {title}");
        md.AppendLine();
        md.AppendLine("| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |");
        md.AppendLine("| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |");
        foreach (var c in careers)
        {
            md.AppendLine(Invariant($"| {c.Index} | {c.Seed} | {c.Policy} | {c.Background} | {c.Difficulty} | {c.Ending?.ToString() ?? "—"} | {c.Weeks:N0} | {c.Followers:N0} | {c.Cash:N0} | {c.Score:N0} |"));
        }

        md.AppendLine();
    }

    private static long NanosecondsPerWeek(BatchResult r)
    {
        var weeks = r.Careers.Sum(c => (long)c.Weeks);
        return weeks == 0 ? 0 : r.Elapsed.Ticks * 100 / weeks;
    }

    private static string Format(Distribution d) => Invariant($"{d.P5:N0} / {d.P50:N0} / {d.P95:N0} / {d.Max:N0}");

    /// <summary>Percentage with one decimal using integer arithmetic.</summary>
    private static string Share(int count, int total)
    {
        var tenths = total == 0 ? 0 : ((count * 1000L) + (total / 2)) / total;
        return Invariant($"{tenths / 10}.{tenths % 10}%");
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
