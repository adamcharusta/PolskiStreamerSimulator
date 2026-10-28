using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>One settled week in the history list: its plan, audience and cash, whether a batch played it, and its incident tags.</summary>
public sealed record WeekSummaryView(int Week, PlanView Plan, long LiveViewers, long FollowersNet, long CashDelta, bool Automatic, IReadOnlyList<string> Tags);

/// <summary>Every settled week of a career, in week order (M3-D).</summary>
public sealed record HistoryView(IReadOnlyList<WeekSummaryView> Weeks);

public sealed record GetHistory(Guid CareerId, string? Secret)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***");
        return true;
    }
}

public sealed record GetHistoryWeek(Guid CareerId, string? Secret, int Week)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, Week = {Week}");
        return true;
    }
}

/// <summary>Reads the stored reports of a career's log: what the player was shown, week by week (TD-119).</summary>
public static class HistoryReader
{
    /// <summary>The settled weeks, the latest report of each week number winning (a week is settled once).</summary>
    public static IReadOnlyList<(WeekReportView Report, bool Automatic)> Weeks(IReadOnlyList<StoredAction> reports)
        => [.. reports
            .Where(a => a.ReportJson is not null)
            .Select(a => (Report: StoredContract.ReadReport(a.ReportJson!), Automatic: a.Kind == ActionKind.AutoWeek))
            .GroupBy(x => x.Report.Week)
            .Select(g => g.Last())
            .OrderBy(x => x.Report.Week)];

    public static WeekSummaryView Summary(WeekReportView r, bool automatic)
        => new(r.Week, r.Plan, r.LiveViewers, r.FollowersNet, r.CashDelta, automatic, Tags(r));

    /// <summary>The incidents of a week, in a fixed order, as stable tags the client filters and localizes.</summary>
    public static IReadOnlyList<string> Tags(WeekReportView r)
    {
        var tags = new List<string>();
        Add(tags, r.Viral is not null, "viral");
        Add(tags, r.Strike is { Strike: true }, "strike");
        Add(tags, r.Strike is { Banned: true }, "ban");
        Add(tags, r.Migration is not null, "migration");
        Add(tags, r.Wager is not null, "wager");
        Add(tags, r.Income.Any(l => l.Code == "income:sponsor"), "sponsorPaid");
        Add(tags, r.Costs.Any(l => l.Code == "cost:sponsor-penalty"), "sponsorPenalty");
        Add(tags, r.DecisionRolls.Count > 0, "decision");
        Add(tags, r.StatChanges.Any(l => l.Code == "week:short-stream"), "shortStream");
        Add(tags, r.Ending is not null, "ending");
        return tags;
    }

    private static void Add(List<string> tags, bool when, string tag)
    {
        if (when)
        {
            tags.Add(tag);
        }
    }
}

public sealed class GetHistoryHandler(CareerAccess access, ICareerStore store)
{
    public async Task<AppResult<HistoryView>> Handle(GetHistory query, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(query.CareerId, query.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<HistoryView>.Fail(opened.Error);
        }

        var reports = await store.ListReportsAsync(opened.Value.Id, cancellationToken);
        return AppResult<HistoryView>.Ok(new HistoryView([.. HistoryReader.Weeks(reports).Select(w => HistoryReader.Summary(w.Report, w.Automatic))]));
    }
}

public sealed class GetHistoryWeekHandler(CareerAccess access, ICareerStore store)
{
    public async Task<AppResult<WeekReportView>> Handle(GetHistoryWeek query, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(query.CareerId, query.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<WeekReportView>.Fail(opened.Error);
        }

        var reports = await store.ListReportsAsync(opened.Value.Id, cancellationToken);
        var week = HistoryReader.Weeks(reports).FirstOrDefault(w => w.Report.Week == query.Week);
        return week.Report is null
            ? AppResult<WeekReportView>.Fail(ErrorCodes.WeekNotFound, $"week-{query.Week}")
            : AppResult<WeekReportView>.Ok(week.Report);
    }
}
