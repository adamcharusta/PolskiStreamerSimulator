using System.Text;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
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

public sealed record AutoWeekView(int Week, PlanView Plan, ReasonView PlanReason, IReadOnlyList<AutoChoiceView> Choices, WeekReportView Report, IReadOnlyList<AutoOfferView> Offers);

/// <summary>A sponsor offer the batch answered before planning the week, with the policy's reason.</summary>
public sealed record AutoOfferView(string OfferId, string TemplateId, bool Accepted, ReasonView Reason);

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

        var requestJson = ContractJson.Serialize(command.Input);
        var (career, stored) = await SnapshotAsync(opened.Value.Id, command.CommandId, cancellationToken);
        if (!IsSameBatch(stored, requestJson))
        {
            return AppResult<BatchOutcome>.Fail(ErrorCodes.CommandIdReused);
        }

        var records = Records(stored);
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
        else if (Owns(career, stored))
        {
            // Nothing else happened since this batch's last week: finish it (after a crash or beside a repeat) or report the same stop.
            stop = await PlayAsync(career, content, command.CommandId, requestJson, records, cancellationToken);
        }
        else
        {
            // Another command moved the career on; report what the batch committed and never play further weeks.
            stop = records.Count == Weeks ? BatchStopReason.Completed : BatchStopReason.Interrupted;
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

    private static List<AutoWeekRecord> Records(IReadOnlyList<StoredAction> stored)
        => [.. stored.Select(a => StoredContract.ReadAutoWeek(a.ResponseJson))];

    /// <summary>Whether the career's latest change is this command's last week, so no other command moved it since.</summary>
    private static bool Owns(StoredCareer career, IReadOnlyList<StoredAction> stored)
        => stored.Count > 0 && career.State.StateVersion == stored[^1].StateVersionAfter;

    /// <summary>
    /// The career and this command's weeks, read so that no week of the command was added in between; a repeat of the
    /// same command running beside this one cannot then pass for another command.
    /// </summary>
    private async Task<(StoredCareer Career, IReadOnlyList<StoredAction> Stored)> SnapshotAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
    {
        var stored = await store.FindCommandAsync(careerId, commandId, cancellationToken);
        while (true)
        {
            var career = await store.FindAsync(careerId, cancellationToken) ?? throw new InvalidOperationException("The career disappeared.");
            var again = await store.FindCommandAsync(careerId, commandId, cancellationToken);
            if (again.Count == stored.Count)
            {
                return (career, stored);
            }

            stored = again;
        }
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
            if (await store.AppendAsync(career.Id, s.StateVersion, next, entry, time.GetUtcNow(), cancellationToken) == AppendOutcome.Appended)
            {
                records.Add(record);
                s = next;
                continue;
            }

            var (current, stored) = await SnapshotAsync(career.Id, commandId, cancellationToken);
            if (!Owns(current, stored))
            {
                return BatchStopReason.Interrupted;
            }

            // A repeat of this command committed the week first: carry on from its weeks, as it does.
            records.Clear();
            records.AddRange(Records(stored));
            s = current.State;
        }
    }

    /// <summary>Answers every open offer with the policy first (inside the week's log entry), then plans, plays and settles the week.</summary>
    private (CareerState State, AutoWeekView Week) PlayWeek(CareerState s, ContentSnapshot c)
    {
        var offers = new List<AutoOfferView>();
        foreach (var offer in s.Offers ?? [])
        {
            var decision = Policy.ChooseOffer(s, c, offer, SponsorRules.AcceptRejection(s, c, offer));
            s = Expect(engine.RespondOffer(s, c, offer.OfferId, decision.Accept, s.StateVersion));
            offers.Add(new AutoOfferView(offer.OfferId, offer.TemplateId, decision.Accept, Reason(decision.Reason)));
        }

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
        return (settled.State, new AutoWeekView(report.Week, ReportViews.Plan(plan.Plan), Reason(plan.Reason), choices, report, offers));
    }

    private BatchOutcome Outcome(StoredCareer current, ContentSnapshot c, List<AutoWeekRecord> records, BatchStopReason stop, WeekReportView? lastReport)
    {
        var now = CareerViewBuilder.Stats(current.State);
        var summary = new BatchSummaryView(
            records.Count,
            records.Sum(r => r.Week.Report.FollowersNet),
            records.Sum(r => r.Week.Report.CashDelta),
            records.Sum(r => Total(r.Week.Report.Income)),
            records.Sum(r => Total(r.Week.Report.Costs)),
            records.Count > 0 ? records[0].Before : now,
            records.Count > 0 ? records[^1].After : now);
        return new BatchOutcome(records.Count, stop, [.. records.Select(r => r.Week)], summary, views.Build(current.Id, current.Nickname, current.State, c, lastReport));
    }

    private static long Total(IReadOnlyList<ReportLineView> lines) => lines.Sum(l => l.AmountPln ?? 0);

    private static ReasonView Reason(PolicyReason reason) => new(reason.Code, reason.Args);

    // The policy only picks what the engine offers, so a rejection here is a server fault, not a player error.
    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Batch step failed: {result.Error.Code} ({result.Error.Detail}).");
}
