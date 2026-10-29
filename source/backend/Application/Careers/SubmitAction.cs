using System.Text;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

public enum ActionType
{
    StartWeek,
    ResolveEvent,
    SettleWeek,

    /// <summary>Answers the first queued mandatory decision between weeks with <see cref="ActionInput.ChoiceId"/>.</summary>
    ResolveDecision,

    /// <summary>Accepts or declines the sponsor offer <see cref="ActionInput.OfferId"/> between weeks (M3-B).</summary>
    RespondOffer,

    /// <summary>Ends the career between weeks with early retirement (M3-C).</summary>
    Retire,
}

/// <summary>
/// One manual step; stored as the request of the command, so a repeat must send exactly the same values. The M3-B
/// fields are omitted from JSON when null, so a stored request written before them serializes byte for byte.
/// </summary>
public sealed record ActionInput(
    long ExpectedVersion,
    ActionType Type,
    MainAction? MainAction,
    Category? Category,
    Category? TrainTarget,
    EquipmentSlot? UpgradeSlot,
    string? ChoiceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WagerTier? Wager = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Platform? TargetPlatform = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OfferId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Accept = null);

public sealed record ActionOutcome(CareerView Career, RollView? Roll, WeekReportView? Report);

public sealed record SubmitAction(Guid CareerId, string? Secret, Guid CommandId, ActionInput Input)
{
    // Wolverine logs a failed message with ToString(); the bearer secret must never reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"CareerId = {CareerId}, Secret = ***, CommandId = {CommandId}, Input = {Input}");
        return true;
    }
}

public sealed class SubmitActionHandler(
    CareerAccess access,
    ICareerStore store,
    IContentStore contents,
    WeekEngine engine,
    CareerViewBuilder views,
    TimeProvider time)
{
    public async Task<AppResult<ActionOutcome>> Handle(SubmitAction command, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<ActionOutcome>.Fail(opened.Error);
        }

        // Before serializing: the contract writes enum names only, so an undefined value cannot even be stored.
        if (!Defined(command.Input))
        {
            return AppResult<ActionOutcome>.Fail(ErrorCodes.ValidationFailed, "an enum value is not defined");
        }

        var requestJson = ContractJson.Serialize(command.Input);
        if (await RepeatAsync(command, requestJson, cancellationToken) is { } repeat)
        {
            return repeat;
        }

        var career = opened.Value;
        if (career.State.StateVersion != command.Input.ExpectedVersion)
        {
            return AppResult<ActionOutcome>.Fail(ErrorCodes.StaleVersion, $"current-{career.State.StateVersion}");
        }

        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        var step = Execute(career.State, content, command.Input);
        if (!step.IsSuccess)
        {
            return AppResult<ActionOutcome>.Fail(step.Error);
        }

        var (next, roll, report) = step.Value;
        var lastReport = report ?? await store.LastReportAsync(career.Id, cancellationToken);
        var now = time.GetUtcNow();
        var outcome = new ActionOutcome(views.Build(career.Id, career.Nickname, next, content, lastReport, CareerRetention.ExpiresAt(next, now)), roll, report);
        var entry = new NewAction(
            command.CommandId,
            0,
            KindOf(command.Input.Type),
            requestJson,
            ContractJson.Serialize(outcome),
            report is null ? null : ContractJson.Serialize(report));

        var appended = await store.AppendAsync(career.Id, command.Input.ExpectedVersion, next, entry, now, cancellationToken);
        if (appended == AppendOutcome.Appended)
        {
            return AppResult<ActionOutcome>.Ok(outcome);
        }

        // A concurrent request won: the same command (answer with its stored result) or another one (a stale version).
        return await RepeatAsync(command, requestJson, cancellationToken)
            ?? AppResult<ActionOutcome>.Fail(ErrorCodes.StaleVersion);
    }

    private async Task<AppResult<ActionOutcome>?> RepeatAsync(SubmitAction command, string requestJson, CancellationToken cancellationToken)
    {
        var stored = await store.FindCommandAsync(command.CareerId, command.CommandId, cancellationToken);
        if (stored.Count == 0)
        {
            return null;
        }

        return stored is [{ Step: 0 } only] && string.Equals(only.RequestJson, requestJson, StringComparison.Ordinal)
            ? AppResult<ActionOutcome>.Ok(StoredContract.ReadOutcome(only.ResponseJson))
            : AppResult<ActionOutcome>.Fail(ErrorCodes.CommandIdReused);
    }

    private AppResult<(CareerState State, RollView? Roll, WeekReportView? Report)> Execute(CareerState s, ContentSnapshot c, ActionInput input)
    {
        switch (input.Type)
        {
            case ActionType.StartWeek:
                if (input.MainAction is not { } action || input.Category is not { } category)
                {
                    return Rejected(new AppError(ErrorCodes.ValidationFailed, "startWeek needs mainAction and category"));
                }

                var plan = new WeekPlan(action, category, input.TrainTarget, input.UpgradeSlot, input.Wager, input.TargetPlatform);
                return Map(engine.StartWeek(s, c, plan, input.ExpectedVersion), next => (next, null, null));

            case ActionType.ResolveEvent:
                if (string.IsNullOrEmpty(input.ChoiceId))
                {
                    return Rejected(new AppError(ErrorCodes.ValidationFailed, "resolveEvent needs choiceId"));
                }

                return Map(engine.ResolveEvent(s, c, input.ChoiceId, input.ExpectedVersion), next => (next, RollTexts.Of(next.Pending!.Resolved[^1], c), null));

            case ActionType.ResolveDecision:
                if (string.IsNullOrEmpty(input.ChoiceId))
                {
                    return Rejected(new AppError(ErrorCodes.ValidationFailed, "resolveDecision needs choiceId"));
                }

                return Map(engine.ResolveDecision(s, c, input.ChoiceId, input.ExpectedVersion), next => (next, RollTexts.Of(next.Carry!.Rolls[^1], c), null));

            case ActionType.SettleWeek:
                return Map(engine.SettleWeek(s, c, input.ExpectedVersion), settled => (settled.State, null, ReportViews.Report(settled.Report, c)));

            case ActionType.Retire:
                return Map(engine.Retire(s, c, input.ExpectedVersion), next => (next, null, null));

            case ActionType.RespondOffer:
                if (string.IsNullOrEmpty(input.OfferId) || input.Accept is not { } accept)
                {
                    return Rejected(new AppError(ErrorCodes.ValidationFailed, "respondOffer needs offerId and accept"));
                }

                return Map(engine.RespondOffer(s, c, input.OfferId, accept, input.ExpectedVersion), next => (next, null, null));

            default:
                return Rejected(new AppError(ErrorCodes.ValidationFailed, "unknown action type"));
        }
    }

    private static AppResult<(CareerState State, RollView? Roll, WeekReportView? Report)> Map<T>(
        Result<T> result, Func<T, (CareerState State, RollView? Roll, WeekReportView? Report)> map)
        => result.IsSuccess
            ? AppResult<(CareerState, RollView?, WeekReportView?)>.Ok(map(result.Value))
            : Rejected(DomainErrors.ToAppError(result.Error));

    private static AppResult<(CareerState State, RollView? Roll, WeekReportView? Report)> Rejected(AppError error)
        => AppResult<(CareerState, RollView?, WeekReportView?)>.Fail(error);

    // The rules index tables by these values; an undefined one would be a server fault instead of a rejected request.
    private static bool Defined(ActionInput input)
        => Enum.IsDefined(input.Type)
            && (input.MainAction is not { } action || Enum.IsDefined(action))
            && (input.Category is not { } category || Enum.IsDefined(category))
            && (input.TrainTarget is not { } target || Enum.IsDefined(target))
            && (input.UpgradeSlot is not { } slot || Enum.IsDefined(slot))
            && (input.Wager is not { } wager || Enum.IsDefined(wager))
            && (input.TargetPlatform is not { } platform || Enum.IsDefined(platform));

    private static ActionKind KindOf(ActionType type) => type switch
    {
        ActionType.StartWeek => ActionKind.StartWeek,
        ActionType.ResolveEvent => ActionKind.ResolveEvent,
        ActionType.ResolveDecision => ActionKind.ResolveDecision,
        ActionType.RespondOffer => ActionKind.RespondOffer,
        ActionType.Retire => ActionKind.Retire,
        _ => ActionKind.SettleWeek,
    };
}
