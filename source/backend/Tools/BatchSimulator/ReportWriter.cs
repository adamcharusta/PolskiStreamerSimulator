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
        var m3 = IsM3(r);
        md.AppendLine(Invariant($"# {(m3 ? "M3" : "M1")} batch simulation report"));
        md.AppendLine();
        md.AppendLine("## Deterministic results");
        md.AppendLine();
        md.AppendLine(Invariant($"- Careers: {careers.Count:N0}; master seed {o.Seed}; policies: {string.Join(", ", o.Policies)}."));
        md.AppendLine(Invariant($"- Ruleset `{r.RulesetId}`; content {r.ContentVersion} (`{r.ContentDigest}`)."));
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
        if (m3)
        {
            AppendPaths(md, BatchStatistics.Paths(careers));
            AppendPace(md, BatchStatistics.Pace(careers));
            AppendCriteria(md, "Criteria", BatchStatistics.M3Criteria(careers));
        }

        if (IsM3B(r))
        {
            AppendEconomy(md, BatchStatistics.Economy(careers));
            AppendCriteria(md, "M3-B criteria", BatchStatistics.M3BCriteria(careers));
        }

        if (r.AchievementIds.Count > 0)
        {
            AppendAchievements(md, r);
            AppendCriteria(md, "M3-C criteria", BatchStatistics.M3CCriteria(careers, r.AchievementIds, r.AchievementPointsCap));
        }

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
                r.RulesetId,
                r.ContentVersion,
                r.ContentDigest,
                ByPolicy = BatchStatistics.By(r.Careers, c => c.Policy),
                ByBackground = BatchStatistics.By(r.Careers, c => c.Background.ToString()),
                ByDifficulty = BatchStatistics.By(r.Careers, c => c.Difficulty.ToString()),
                BySpecialist = IsM3(r) ? BatchStatistics.Paths(r.Careers) : null,
                DecisionsAndVirals = IsM3(r) ? BatchStatistics.Pace(r.Careers) : null,
                Criteria = IsM3(r) ? BatchStatistics.M3Criteria(r.Careers) : null,
                EconomyAndRisk = IsM3B(r) ? BatchStatistics.Economy(r.Careers) : null,
                M3BCriteria = IsM3B(r) ? BatchStatistics.M3BCriteria(r.Careers) : null,
                Achievements = r.AchievementIds.Count > 0 ? BatchStatistics.AchievementShares(r.Careers, r.AchievementIds) : null,
                StrategyPolicies = r.AchievementIds.Count > 0 ? BatchStatistics.Strategies(r.Careers) : null,
                M3CCriteria = r.AchievementIds.Count > 0 ? BatchStatistics.M3CCriteria(r.Careers, r.AchievementIds, r.AchievementPointsCap) : null,
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

    private static bool IsM3(BatchResult r) => M3Ruleset.IsM3(r.RulesetId);

    private static bool IsM3B(BatchResult r) => r.RulesetId == M3Ruleset.SystemsV2;

    private static void AppendAchievements(StringBuilder md, BatchResult r)
    {
        md.AppendLine("### Achievements and strategies");
        md.AppendLine();
        var points = BatchStatistics.Of(r.Careers.Select(c => (long)c.AchievementPoints));
        md.AppendLine(Invariant($"Achievement points per career p5 / p50 / p95 / max: {points.P5} / {points.P50} / {points.P95} / {points.Max} (the score counts at most {r.AchievementPointsCap})."));
        md.AppendLine();
        md.AppendLine("| Achievement | Careers | Share |");
        md.AppendLine("| --- | ---: | ---: |");
        foreach (var a in BatchStatistics.AchievementShares(r.Careers, r.AchievementIds))
        {
            md.AppendLine(Invariant($"| {a.Id} | {a.Careers:N0} | {BatchStatistics.Percent(a.Share)} |"));
        }

        md.AppendLine();
        md.AppendLine("| Strategy | Careers | Insolvency | Median cash PLN | Median drama | Median achievement points |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var s in BatchStatistics.Strategies(r.Careers))
        {
            md.AppendLine(Invariant($"| {s.Group} | {s.Careers:N0} | {BatchStatistics.Percent(s.InsolvencyRate)} | {s.MedianCash:N0} | {s.MedianDrama:F1} | {s.MedianPoints} |"));
        }

        md.AppendLine();
    }

    private static void AppendEconomy(StringBuilder md, IReadOnlyList<EconomySummary> economy)
    {
        md.AppendLine("### Economy and risk");
        md.AppendLine();
        md.AppendLine("Medians by the nearest-rank method. Sponsor share is sponsor income over all income; breach rate is sponsor penalties over paid installments plus penalties; banned counts careers that lost at least one platform; ban endings are `GlobalBan` and `AllPlatformsBanned`.");
        md.AppendLine();
        md.AppendLine("| Policy | Careers | Insolvency | Median cash PLN | Median score | Sponsor share | Installments | Breach rate | Strikes | Banned | Ban endings | Migrations |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var e in economy)
        {
            md.AppendLine(Invariant($"| {e.Group} | {e.Careers:N0} | {BatchStatistics.Percent(e.InsolvencyRate)} | {e.MedianCash:N0} | {e.MedianScore:N0} | {BatchStatistics.Percent(e.SponsorShare)} | {e.Installments:N0} | {BatchStatistics.Percent(e.BreachRate)} | {e.Strikes:N0} | {e.CareersBanned:N0} | {e.BanEndings:N0} | {e.Migrations:N0} |"));
        }

        md.AppendLine();
    }

    private static void AppendPaths(StringBuilder md, IReadOnlyList<PathSummary> paths)
    {
        md.AppendLine("### By category specialist");
        md.AppendLine();
        md.AppendLine("Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.");
        md.AppendLine();
        md.AppendLine("| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var p in paths)
        {
            md.AppendLine(Invariant($"| {p.Group} | {p.Careers:N0} | {BatchStatistics.Percent(p.CompletionRate)} | {p.MedianFollowers:N0} | {p.MedianCash:N0} | {p.MedianDrama:F1} | {p.MedianCredibility:F1} | {p.MedianViewerCv:F3} | {p.LeadingIncome} |"));
        }

        md.AppendLine();
    }

    private static void AppendPace(StringBuilder md, IReadOnlyList<EventPaceSummary> pace)
    {
        md.AppendLine("### Decisions and virals");
        md.AppendLine();
        md.AppendLine("| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var p in pace)
        {
            md.AppendLine(Invariant($"| {p.Group} | {p.Careers:N0} | {p.Decisions:N0} | {p.DecisionsPer13Weeks:F2} | {p.ViralWeeks:N0} | {p.ViralsPerCareer:F2} |"));
        }

        md.AppendLine();
    }

    private static void AppendCriteria(StringBuilder md, string title, IReadOnlyList<Criterion> criteria)
    {
        md.AppendLine($"### {title}");
        md.AppendLine();
        md.AppendLine("| Criterion | Result | Evidence |");
        md.AppendLine("| --- | --- | --- |");
        foreach (var c in criteria)
        {
            md.AppendLine(Invariant($"| {c.Name} | {c.Result} | {c.Evidence} |"));
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
