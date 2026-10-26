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

/// <summary>The M3-B economy and risk measures of one policy's careers.</summary>
public sealed record EconomySummary(
    string Group,
    int Careers,
    int Insolvent,
    long MedianCash,
    long MedianScore,
    long SponsorIncome,
    long TotalIncome,
    long Installments,
    long Breaches,
    long Strikes,
    int CareersBanned,
    int BanEndings,
    long Migrations)
{
    public double SponsorShare => TotalIncome == 0 ? 0 : (double)SponsorIncome / TotalIncome;

    public double BreachRate => Installments + Breaches == 0 ? 0 : (double)Breaches / (Installments + Breaches);

    public double InsolvencyRate => Careers == 0 ? 0 : (double)Insolvent / Careers;
}

/// <summary>The share of careers that earned one achievement.</summary>
public sealed record AchievementShare(string Id, int Careers, double Share);

/// <summary>The M3-C measures of one strategy policy's careers.</summary>
public sealed record StrategySummary(string Group, int Careers, int Insolvent, long MedianCash, decimal MedianDrama, int MedianPoints)
{
    public double InsolvencyRate => Careers == 0 ? 0 : (double)Insolvent / Careers;
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
    public const double MinBalancedSolvency = 0.90;
    public const double MaxSideBusinessCashMultiple = 1.5;
    public const double MinRiskyCareersBanned = 0.01;
    public const double MaxBanEndings = 0.02;
    public const double MinSponsorShare = 0.10;
    public const string StrategyPrefix = "Strategy-";
    public const double MaxStrategyInsolvency = 0.10;
    public const decimal MinStrategyDramaGap = 10;
    public const double MinAchievementShare = 0.05;
    public const double MaxAchievementShare = 0.95;
    public const int MinSeparatingAchievements = 6;

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

    /// <summary>Economy and risk measures per policy, in order of first appearance.</summary>
    public static IReadOnlyList<EconomySummary> Economy(IEnumerable<CareerSummary> careers)
        => [.. careers.OrderBy(c => c.Index)
            .GroupBy(c => c.Policy)
            .Select(g => new EconomySummary(
                g.Key,
                g.Count(),
                g.Count(c => c.Ending == EndingReason.Insolvency),
                Median(g.Select(c => c.Cash)),
                Median(g.Select(c => c.Score)),
                g.Sum(c => c.SponsorIncome),
                g.Sum(c => c.TotalIncome),
                g.Sum(c => (long)c.Installments),
                g.Sum(c => (long)c.Breaches),
                g.Sum(c => (long)c.Strikes),
                g.Count(c => c.PlatformBans > 0),
                g.Count(c => c.Ending is EndingReason.GlobalBan or EndingReason.AllPlatformsBanned),
                g.Sum(c => (long)c.Migrations)))];

    /// <summary>
    /// Evaluates the M3-B criteria of the economy and risk spec (§9): no side business needed, gambling not dominant,
    /// the side business a trade-off, rare but real bans, sponsors that matter, and reconciliation.
    /// </summary>
    public static IReadOnlyList<Criterion> M3BCriteria(IReadOnlyList<CareerSummary> careers)
    {
        // The M3-B criteria judge the M3-B policies; the M3-C strategies have their own criteria.
        careers = [.. careers.Where(c => !c.Policy.StartsWith(StrategyPrefix, StringComparison.Ordinal))];
        var economy = Economy(careers);
        var balanced = economy.FirstOrDefault(e => e.Group == "Balanced");
        var gambler = economy.FirstOrDefault(e => e.Group == "Gambler");
        var hustler = economy.FirstOrDefault(e => e.Group == "SideHustler");
        var hunter = economy.FirstOrDefault(e => e.Group == "SponsorHunter");
        return
        [
            NoSideBusinessNeeded(balanced),
            GamblingNotDominant(balanced, gambler),
            SideBusinessTradeOff(balanced, hustler),
            RareButRealBans(careers.Count, gambler, hustler, economy.Sum(e => e.BanEndings)),
            SponsorsMatter(hunter),
            Reconciliation(careers),
        ];
    }

    private static Criterion NoSideBusinessNeeded(EconomySummary? balanced)
    {
        const string name = "No side business needed (#20)";
        if (balanced is null)
        {
            return new Criterion(name, NotApplicable, "Needs the Balanced policy.");
        }

        var solvent = 1 - balanced.InsolvencyRate;
        return new Criterion(name, solvent >= MinBalancedSolvency ? Pass : Fail, Invariant($"Balanced, which never opens the side business, avoids insolvency in {Percent(solvent)} of careers (at least {Percent(MinBalancedSolvency)})."));
    }

    private static Criterion GamblingNotDominant(EconomySummary? balanced, EconomySummary? gambler)
    {
        const string name = "Gambling is not dominant (#20)";
        if (balanced is null || gambler is null)
        {
            return new Criterion(name, NotApplicable, "Needs the Balanced and Gambler policies.");
        }

        // Scores saturate at the cap for most policies (an M5 balance issue), so a tie on the score does not make gambling dominant.
        var pass = gambler.MedianCash < balanced.MedianCash && gambler.MedianScore <= balanced.MedianScore;
        return new Criterion(name, pass ? Pass : Fail, Invariant($"Gambler median cash {gambler.MedianCash:N0} vs Balanced {balanced.MedianCash:N0}; median score {gambler.MedianScore:N0} vs {balanced.MedianScore:N0}; Gambler insolvency {Percent(gambler.InsolvencyRate)}."));
    }

    private static Criterion SideBusinessTradeOff(EconomySummary? balanced, EconomySummary? hustler)
    {
        const string name = "Side business is a trade-off (#20)";
        if (balanced is null || hustler is null || balanced.MedianCash <= 0)
        {
            return new Criterion(name, NotApplicable, "Needs the Balanced and SideHustler policies and a positive Balanced median cash.");
        }

        var multiple = (double)hustler.MedianCash / balanced.MedianCash;
        var pass = multiple >= 1.0 && multiple <= MaxSideBusinessCashMultiple;
        return new Criterion(name, pass ? Pass : Fail, Invariant($"SideHustler median cash {hustler.MedianCash:N0} = {multiple:F2}x Balanced {balanced.MedianCash:N0} (between 1.00x and {MaxSideBusinessCashMultiple:F2}x)."));
    }

    private static Criterion RareButRealBans(int careerCount, EconomySummary? gambler, EconomySummary? hustler, int banEndings)
    {
        const string name = "Bans are rare but real (#19)";
        if (gambler is null || hustler is null || careerCount == 0)
        {
            return new Criterion(name, NotApplicable, "Needs the Gambler and SideHustler policies.");
        }

        var risky = gambler.Careers + hustler.Careers;
        var banned = risky == 0 ? 0 : (double)(gambler.CareersBanned + hustler.CareersBanned) / risky;
        var endings = (double)banEndings / careerCount;
        var pass = banned >= MinRiskyCareersBanned && endings < MaxBanEndings;
        return new Criterion(name, pass ? Pass : Fail, Invariant($"{Percent(banned)} of Gambler and SideHustler careers lost a platform (at least {Percent(MinRiskyCareersBanned)}); {Percent(endings)} of all careers ended on a global or total ban (below {Percent(MaxBanEndings)})."));
    }

    private static Criterion SponsorsMatter(EconomySummary? hunter)
    {
        const string name = "Sponsors matter (#18)";
        if (hunter is null)
        {
            return new Criterion(name, NotApplicable, "Needs the SponsorHunter policy.");
        }

        return new Criterion(name, hunter.SponsorShare >= MinSponsorShare ? Pass : Fail, Invariant($"SponsorHunter earns {Percent(hunter.SponsorShare)} of its income from sponsors (at least {Percent(MinSponsorShare)}); breach rate {Percent(hunter.BreachRate)}."));
    }

    private static Criterion Reconciliation(IReadOnlyList<CareerSummary> careers)
    {
        var violations = careers.Sum(c => (long)c.ReconciliationViolations);
        return new Criterion("Reconciliation (#18)", violations == 0 ? Pass : Fail, Invariant($"{violations:N0} weeks whose cash change differs from their report lines."));
    }

    /// <summary>The share of careers earning each achievement, in definition order.</summary>
    public static IReadOnlyList<AchievementShare> AchievementShares(IReadOnlyList<CareerSummary> careers, IReadOnlyList<string> ids)
        => [.. ids.Select(id =>
        {
            var count = careers.Count(c => c.Achievements.Contains(id));
            return new AchievementShare(id, count, careers.Count == 0 ? 0 : (double)count / careers.Count);
        })];

    /// <summary>The strategy policies' measures, in order of first appearance.</summary>
    public static IReadOnlyList<StrategySummary> Strategies(IEnumerable<CareerSummary> careers)
        => [.. careers.OrderBy(c => c.Index)
            .GroupBy(c => c.Policy)
            .Where(g => g.Key.StartsWith(StrategyPrefix, StringComparison.Ordinal))
            .Select(g => new StrategySummary(
                g.Key,
                g.Count(),
                g.Count(c => c.Ending == EndingReason.Insolvency),
                Median(g.Select(c => c.Cash)),
                Median(g.Select(c => c.Drama)),
                Median(g.Select(c => c.AchievementPoints))))];

    /// <summary>
    /// Evaluates the M3-C criteria of the automation and endings spec (§9): viable strategies, strategy choices that
    /// matter, and achievements that separate careers.
    /// </summary>
    public static IReadOnlyList<Criterion> M3CCriteria(IReadOnlyList<CareerSummary> careers, IReadOnlyList<string> ids, int pointsCap)
    {
        var strategies = Strategies(careers);
        var safe = strategies.FirstOrDefault(s => s.Group == "Strategy-Safe");
        var bold = strategies.FirstOrDefault(s => s.Group == "Strategy-Bold");
        var balanced = careers.Where(c => c.Policy == "Balanced").ToArray();
        return [ViableStrategies(strategies), ChoicesMatter(safe, bold), SeparatingAchievements(careers, ids, pointsCap, balanced)];
    }

    private static Criterion ViableStrategies(IReadOnlyList<StrategySummary> strategies)
    {
        const string name = "Strategies are viable (#22)";
        if (strategies.Count == 0)
        {
            return new Criterion(name, NotApplicable, "Needs the strategy policies.");
        }

        var worst = strategies.MaxBy(s => s.InsolvencyRate)!;
        return new Criterion(name, worst.InsolvencyRate <= MaxStrategyInsolvency ? Pass : Fail, Invariant($"Highest insolvency {Percent(worst.InsolvencyRate)} ({worst.Group}); limit {Percent(MaxStrategyInsolvency)}."));
    }

    private static Criterion ChoicesMatter(StrategySummary? safe, StrategySummary? bold)
    {
        const string name = "Strategy choices matter (#22)";
        if (safe is null || bold is null)
        {
            return new Criterion(name, NotApplicable, "Needs Strategy-Safe and Strategy-Bold.");
        }

        var gap = Math.Abs(bold.MedianDrama - safe.MedianDrama);
        return new Criterion(name, gap >= MinStrategyDramaGap ? Pass : Fail, Invariant($"Median final drama {safe.MedianDrama:F1} (Strategy-Safe) vs {bold.MedianDrama:F1} (Strategy-Bold); gap {gap:F1}, at least {MinStrategyDramaGap:F0}."));
    }

    private static Criterion SeparatingAchievements(IReadOnlyList<CareerSummary> careers, IReadOnlyList<string> ids, int pointsCap, CareerSummary[] balanced)
    {
        const string name = "Achievements separate careers (#23)";
        if (ids.Count == 0 || balanced.Length == 0)
        {
            return new Criterion(name, NotApplicable, "Needs content achievements and the Balanced policy.");
        }

        var separating = AchievementShares(careers, ids).Count(s => s.Share >= MinAchievementShare && s.Share <= MaxAchievementShare);
        var medianPoints = Median(balanced.Select(c => c.AchievementPoints));
        var pass = separating >= MinSeparatingAchievements && medianPoints < pointsCap;
        return new Criterion(name, pass ? Pass : Fail, Invariant($"{separating} achievements earned by {Percent(MinAchievementShare)}–{Percent(MaxAchievementShare)} of careers (at least {MinSeparatingAchievements}); Balanced median points {medianPoints} (below the cap {pointsCap})."));
    }

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
