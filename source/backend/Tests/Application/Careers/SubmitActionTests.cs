using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class SubmitActionTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionInput Start(long version, MainAction action = MainAction.Rest, EquipmentSlot? slot = null)
        => new(version, ActionType.StartWeek, action, Category.Fps, null, slot, null);

    private static ActionInput Resolve(long version, string choiceId) => new(version, ActionType.ResolveEvent, null, null, null, null, choiceId);

    private static ActionInput Settle(long version) => new(version, ActionType.SettleWeek, null, null, null, null, null);

    private Task<AppResult<ActionOutcome>> SubmitAsync(CreatedCareer career, ActionInput input, Guid? commandId = null, ICareerStore? store = null)
        => kit.SubmitHandler(store).Handle(new SubmitAction(career.CareerId, career.Secret, commandId ?? Guid.NewGuid(), input), Ct);

    private async Task<ActionOutcome> PlayWeekAsync(CreatedCareer career)
    {
        var outcome = (await SubmitAsync(career, Start(career.Career.Version))).Value;
        while (outcome.Career.Next.Kind == NextStepKind.ResolveEvent)
        {
            outcome = (await SubmitAsync(career, Resolve(outcome.Career.Version, outcome.Career.Next.Event!.Choices[0].ChoiceId))).Value;
        }

        return (await SubmitAsync(career, Settle(outcome.Career.Version))).Value;
    }

    [Fact]
    public async Task Starting_a_week_logs_the_command_and_shows_the_first_event()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var outcome = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;

        Assert.Equal(career.Career.Version + 1, outcome.Career.Version);
        Assert.Equal(NextStepKind.ResolveEvent, outcome.Career.Next.Kind);
        Assert.Null(outcome.Roll);
        Assert.Null(outcome.Report);
        var logged = Assert.Single(await kit.Store.FindCommandAsync(career.CareerId, command, Ct));
        Assert.Equal(ActionKind.StartWeek, logged.Kind);
    }

    [Fact]
    public async Task A_full_week_returns_rolls_and_a_report_that_later_reads_show()
    {
        var career = await kit.NewCareerAsync();

        var settled = await PlayWeekAsync(career);

        Assert.Equal(1, settled.Report!.Week);
        Assert.Equal(NextStepKind.PlanWeek, settled.Career.Next.Kind);
        Assert.Equal(1, settled.Career.LastSettledWeek);
        var reread = (await kit.GetHandler().Handle(new GetCareer(career.CareerId, career.Secret), Ct)).Value;
        Assert.Equal(ContractJson.Serialize(settled.Report), ContractJson.Serialize(reread.LastReport));
    }

    [Fact]
    public async Task Resolving_an_event_returns_its_roll()
    {
        var career = await kit.NewCareerAsync();
        var started = (await SubmitAsync(career, Start(career.Career.Version))).Value;
        var shown = started.Career.Next.Event!;

        var resolved = (await SubmitAsync(career, Resolve(started.Career.Version, shown.Choices[0].ChoiceId))).Value;

        Assert.Equal(shown.EventId, resolved.Roll!.EventId);
        Assert.Equal(shown.Choices[0].ChoiceId, resolved.Roll.ChoiceId);
    }

    [Fact]
    public async Task Repeating_a_command_returns_the_stored_result_without_a_second_entry()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var first = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;
        kit.Time.Now = AppTestKit.Start.AddHours(1);

        var repeat = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(repeat));
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
        Assert.Equal(AppTestKit.Start, (await kit.Store.FindAsync(career.CareerId, Ct))!.LastActionAt);
    }

    [Fact]
    public async Task Reusing_a_command_id_with_another_request_is_rejected()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var started = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;

        var reused = await SubmitAsync(career, Resolve(started.Career.Version, started.Career.Next.Event!.Choices[0].ChoiceId), command);

        Assert.Equal(ErrorCodes.CommandIdReused, reused.Error.Code);
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_stale_version_is_rejected_and_nothing_is_logged()
    {
        var career = await kit.NewCareerAsync();

        var result = await SubmitAsync(career, Start(career.Career.Version + 1));

        Assert.Equal(ErrorCodes.StaleVersion, result.Error.Code);
        Assert.Equal(0, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task Rule_rejections_have_stable_codes()
    {
        var career = await kit.NewCareerAsync();
        var v = career.Career.Version;

        Assert.Equal(ErrorCodes.NoPendingWeek, (await SubmitAsync(career, Resolve(v, "anything"))).Error.Code);
        Assert.Equal(ErrorCodes.IllegalAction, (await SubmitAsync(career, Start(v, MainAction.ImproveProduction))).Error.Code);
        var started = (await SubmitAsync(career, Start(v))).Value;
        Assert.Equal(ErrorCodes.UnknownChoice, (await SubmitAsync(career, Resolve(started.Career.Version, "no-such-choice"))).Error.Code);
        Assert.Equal(ErrorCodes.EventsUnresolved, (await SubmitAsync(career, Settle(started.Career.Version))).Error.Code);
    }

    [Fact]
    public async Task A_start_without_a_category_is_rejected()
    {
        var career = await kit.NewCareerAsync();

        var result = await SubmitAsync(career, new ActionInput(career.Career.Version, ActionType.StartWeek, MainAction.Rest, null, null, null, null));

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task Undefined_enum_values_are_rejected_before_the_rules_run()
    {
        var career = await kit.NewCareerAsync();
        var v = career.Career.Version;

        Assert.Equal(ErrorCodes.ValidationFailed, (await SubmitAsync(career, new ActionInput(v, ActionType.StartWeek, MainAction.TrainCategory, Category.Fps, (Category)99, null, null))).Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, (await SubmitAsync(career, new ActionInput(v, ActionType.StartWeek, MainAction.ImproveProduction, Category.Fps, null, (EquipmentSlot)(-1), null))).Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, (await SubmitAsync(career, new ActionInput(v, (ActionType)7, null, null, null, null, null))).Error.Code);
    }

    [Fact]
    public async Task An_ended_career_rejects_actions()
    {
        var career = await kit.NewCareerAsync();
        var state = (await kit.Store.FindAsync(career.CareerId, Ct))!.State;
        var ended = state with { StateVersion = state.StateVersion + 1, Status = CareerStatus.Ended, Ending = EndingReason.EarlyRetirement };
        await kit.Store.AppendAsync(career.CareerId, state.StateVersion, ended, new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null), AppTestKit.Start, Ct);

        var result = await SubmitAsync(career, Start(ended.StateVersion));

        Assert.Equal(ErrorCodes.CareerEnded, result.Error.Code);
    }

    [Fact]
    public async Task A_second_tab_that_loses_the_race_gets_stale_version()
    {
        var career = await kit.NewCareerAsync();
        var racing = new RacingCareerStore(kit.Store, () => SubmitAsync(career, Start(career.Career.Version)));

        var loser = await SubmitAsync(career, Start(career.Career.Version), store: racing);

        Assert.Equal(ErrorCodes.StaleVersion, loser.Error.Code);
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_duplicate_that_loses_the_race_gets_the_winners_result()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        AppResult<ActionOutcome>? winner = null;
        var racing = new RacingCareerStore(kit.Store, async () => winner = await SubmitAsync(career, Start(career.Career.Version), command));

        var loser = await SubmitAsync(career, Start(career.Career.Version), command, racing);

        Assert.Equal(ContractJson.Serialize(winner!.Value), ContractJson.Serialize(loser.Value));
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_wrong_secret_opens_nothing()
    {
        var mine = await kit.NewCareerAsync();
        var theirs = await kit.NewCareerAsync();

        var result = await kit.SubmitHandler().Handle(new SubmitAction(mine.CareerId, theirs.Secret, Guid.NewGuid(), Start(mine.Career.Version)), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }
}
