using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Application.Careers;

public enum BatchStopReason
{
    Completed,
    PausedForDecision,
    CareerEnded,
    Interrupted,
}

public sealed record BatchInput(long ExpectedVersion, int Weeks);

public sealed record ReasonView(string Code, IReadOnlyDictionary<string, string> Args);

public sealed record AutoChoiceView(string EventId, string ChoiceId, ReasonView Reason, RollView Roll);

public sealed record AutoWeekView(int Week, PlanView Plan, ReasonView PlanReason, IReadOnlyList<AutoChoiceView> Choices, WeekReportView Report);

public sealed record BatchSummaryView(int WeeksCommitted, long FollowersNet, long CashDelta, long IncomePln, long CostsPln, CareerStatsView Start, CareerStatsView End);

public sealed record BatchOutcome(int WeeksCommitted, BatchStopReason StopReason, IReadOnlyList<AutoWeekView> Weeks, BatchSummaryView Summary, CareerView Career);

/// <summary>What one <c>autoWeek</c> log entry stores, so a repeat can rebuild the batch result.</summary>
public sealed record AutoWeekRecord(CareerStatsView Before, CareerStatsView After, AutoWeekView Week);

public sealed record RunBatch(Guid CareerId, string? Secret, Guid CommandId, BatchInput Input)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, CommandId = {CommandId}, Input = {Input}");
        return true;
    }
}

/// <summary>
/// Plays up to 13 weeks with the fixed <c>Balanced</c> policy (TD-90), committing each week on its own; stops at an
/// ending, before a week that needs the player, or when another request changes the career.
/// </summary>
public sealed class RunBatchHandler(
    CareerAccess access,
    ICareerStore store,
    IContentStore contents,
    WeekEngine engine,
    CareerViewBuilder views,
    TimeProvider time)
{
    public const int Weeks = 13;

    private static readonly IChoicePolicy Policy = BuiltInPolicies.Balanced;

    public async Task<AppResult<BatchOutcome>> Handle(RunBatch command, CancellationToken cancellationToken)
    {
        if (command.Input.Weeks != Weeks)
        {
            return AppResult<BatchOutcome>.Fail(ErrorCodes.ValidationFailed, "weeks must be 13");
        }

        var opened = await access.OpenAsync(command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<BatchOutcome>.Fail(opened.Error);
        }

        var career = opened.Value;
        var requestJson = ContractJson.Serialize(command.Input);
        var stored = await store.FindCommandAsync(career.Id, command.CommandId, cancellationToken);
        if (!IsSameBatch(stored, requestJson))
        {
            return AppResult<BatchOutcome>.Fail(ErrorCodes.CommandIdReused);
        }

        var records = stored.Select(a => ContractJson.Deserialize<AutoWeekRecord>(a.ResponseJson)).ToList();
        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        BatchStopReason stop;
        if (stored.Count == 0)
        {
            if (Refusal(career.State, command.Input.ExpectedVersion) is { } refusal)
            {
                return AppResult<BatchOutcome>.Fail(refusal);
            }

            stop = await PlayAsync(career, content, command.CommandId, requestJson, records, cancellationToken);
        }
        else if (career.State.StateVersion == stored[^1].StateVersionAfter)
        {
            // Nothing happened since this batch's last week: finish it (after a crash) or report the same stop.
            stop = await PlayAsync(career, content, command.CommandId, requestJson, records, cancellationToken);
        }
        else
        {
            // The career moved on; report what the batch committed and never play further weeks.
            stop = StoppedAt(records);
        }

        var current = await store.FindAsync(career.Id, cancellationToken) ?? throw new InvalidOperationException("The career disappeared.");
        var lastReport = await store.LastReportAsync(career.Id, cancellationToken);
        return AppResult<BatchOutcome>.Ok(Outcome(current, content, records, stop, lastReport));
    }

    private static bool IsSameBatch(IReadOnlyList<StoredAction> stored, string requestJson)
        => stored.Count == 0
            || (stored.All(a => a.Kind == ActionKind.AutoWeek) && string.Equals(stored[0].RequestJson, requestJson, StringComparison.Ordinal));

    private static AppError? Refusal(CareerState s, long expectedVersion)
    {
        if (s.StateVersion != expectedVersion)
        {
            return new AppError(ErrorCodes.StaleVersion, $"current-{s.StateVersion}");
        }

        if (s.Status == CareerStatus.Ended)
        {
            return new AppError(ErrorCodes.CareerEnded);
        }

        return s.Pending is null ? null : new AppError(ErrorCodes.WeekInProgress);
    }

    private static BatchStopReason StoppedAt(List<AutoWeekRecord> records)
    {
        if (records.Count == Weeks)
        {
            return BatchStopReason.Completed;
        }

        return records[^1].Week.Report.Ending is null ? BatchStopReason.Interrupted : BatchStopReason.CareerEnded;
    }

    private async Task<BatchStopReason> PlayAsync(
        StoredCareer career, ContentSnapshot c, Guid commandId, string requestJson, List<AutoWeekRecord> records, CancellationToken cancellationToken)
    {
        var s = career.State;
        while (true)
        {
            if (s.Status == CareerStatus.Ended)
            {
                return BatchStopReason.CareerEnded;
            }

            if (records.Count == Weeks)
            {
                return BatchStopReason.Completed;
            }

            if (Expect(engine.RequiresPlayerDecision(s, c)))
            {
                return BatchStopReason.PausedForDecision;
            }

            var (next, week) = PlayWeek(s, c);
            var record = new AutoWeekRecord(CareerViewBuilder.Stats(s), CareerViewBuilder.Stats(next), week);
            var entry = new NewAction(commandId, records.Count + 1, ActionKind.AutoWeek, requestJson, ContractJson.Serialize(record), ContractJson.Serialize(week.Report));
            if (await store.AppendAsync(career.Id, s.StateVersion, next, entry, time.GetUtcNow(), cancellationToken) != AppendOutcome.Appended)
            {
                return BatchStopReason.Interrupted;
            }

            records.Add(record);
            s = next;
        }
    }

    private (CareerState State, AutoWeekView Week) PlayWeek(CareerState s, ContentSnapshot c)
    {
        var plan = Policy.ChoosePlan(s, c, Expect(engine.LegalPlans(s, c)));
        s = Expect(engine.StartWeek(s, c, plan.Plan, s.StateVersion));
        var choices = new List<AutoChoiceView>();
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var presented = Expect(engine.PresentEvent(s, c));
            var choice = Policy.ChooseEventChoice(s, presented);
            s = Expect(engine.ResolveEvent(s, c, choice.ChoiceId, s.StateVersion));
            choices.Add(new AutoChoiceView(presented.EventId, choice.ChoiceId, Reason(choice.Reason), ReportViews.Roll(s.Pending!.Resolved[^1])));
        }

        var settled = Expect(engine.SettleWeek(s, c, s.StateVersion));
        var report = ReportViews.Report(settled.Report);
        return (settled.State, new AutoWeekView(report.Week, ReportViews.Plan(plan.Plan), Reason(plan.Reason), choices, report));
    }

    private BatchOutcome Outcome(StoredCareer current, ContentSnapshot c, List<AutoWeekRecord> records, BatchStopReason stop, WeekReportView? lastReport)
    {
        var now = CareerViewBuilder.Stats(current.State);
        var summary = new BatchSummaryView(
            records.Count,
            records.Sum(r => r.Week.Report.FollowersNet),
            records.Sum(r => r.Week.Report.CashDelta),
            records.Sum(r => r.Week.Report.Income.Sum(l => l.AmountPln ?? 0)),
            records.Sum(r => r.Week.Report.Costs.Sum(l => l.AmountPln ?? 0)),
            records.Count > 0 ? records[0].Before : now,
            records.Count > 0 ? records[^1].After : now);
        return new BatchOutcome(records.Count, stop, [.. records.Select(r => r.Week)], summary, views.Build(current.Id, current.Nickname, current.State, c, lastReport));
    }

    private static ReasonView Reason(PolicyReason reason) => new(reason.Code, reason.Args);

    // The policy only picks what the engine offers, so a rejection here is a server fault, not a player error.
    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Batch step failed: {result.Error.Code} ({result.Error.Detail}).");
}
