using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class RunBatchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<AppResult<BatchOutcome>> RunAsync(AppTestKit kit, CreatedCareer career, long version, Guid? commandId = null, int weeks = 13, ICareerStore? store = null)
        => kit.BatchHandler(store).Handle(new RunBatch(career.CareerId, career.Secret, commandId ?? Guid.NewGuid(), new BatchInput(version, weeks)), Ct);

    private static async Task<long> VersionAsync(AppTestKit kit, CreatedCareer career) => (await kit.Store.FindAsync(career.CareerId, Ct))!.State.StateVersion;

    [Fact]
    public async Task A_batch_commits_thirteen_weeks_with_their_reasons()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var outcome = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal(13, outcome.WeeksCommitted);
        Assert.Equal(BatchStopReason.Completed, outcome.StopReason);
        Assert.Equal(Enumerable.Range(1, 13), outcome.Weeks.Select(w => w.Week));
        Assert.All(outcome.Weeks, w =>
        {
            Assert.False(string.IsNullOrEmpty(w.PlanReason.Code));
            Assert.All(w.Choices, c => Assert.False(string.IsNullOrEmpty(c.Reason.Code)));
        });
        var log = await kit.Store.FindCommandAsync(career.CareerId, command, Ct);
        Assert.Equal(Enumerable.Range(1, 13), log.Select(a => a.Step));
        Assert.Equal(Enumerable.Range(1, 13), log.Select(a => a.Sequence));
        Assert.All(log, a => Assert.Equal(ActionKind.AutoWeek, a.Kind));
        Assert.True(log.Zip(log.Skip(1)).All(p => p.First.StateVersionAfter < p.Second.StateVersionAfter));
        Assert.Equal(13, outcome.Career.LastSettledWeek);
        Assert.Equal(log[^1].StateVersionAfter, outcome.Career.Version);
        Assert.Equal(13, outcome.Career.LastReport!.Week);
        Assert.Equal(outcome.Weeks.Sum(w => w.Report.FollowersNet), outcome.Summary.FollowersNet);
    }

    [Fact]
    public async Task Pausing_before_a_mandatory_week_does_not_consume_it()
    {
        var kit = new AppTestKit(PausingRuleset.Engine(5));
        var career = await kit.NewCareerAsync();

        var outcome = (await RunAsync(kit, career, career.Career.Version)).Value;

        Assert.Equal(4, outcome.WeeksCommitted);
        Assert.Equal(BatchStopReason.PausedForDecision, outcome.StopReason);
        Assert.Equal(5, outcome.Career.Week);
        Assert.Equal(4, outcome.Career.LastSettledWeek);
        Assert.Equal(NextStepKind.PlanWeek, outcome.Career.Next.Kind);
        Assert.Equal(4, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_pause_before_the_first_week_changes_nothing_and_repeats_alike()
    {
        var kit = new AppTestKit(PausingRuleset.Engine(1));
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var first = (await RunAsync(kit, career, career.Career.Version, command)).Value;
        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((0, BatchStopReason.PausedForDecision), (first.WeeksCommitted, first.StopReason));
        Assert.Equal(career.Career.Version, await VersionAsync(kit, career));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(repeat));
        Assert.Equal(0, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_batch_stops_when_the_career_ends()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var state = (await kit.Store.FindAsync(career.CareerId, Ct))!.State;
        var late = state with { StateVersion = state.StateVersion + 1, Week = new CareerWeek(1039), LastSettledWeek = 1038 };
        await kit.Store.AppendAsync(career.CareerId, state.StateVersion, late, new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null), AppTestKit.Start, Ct);

        var outcome = (await RunAsync(kit, career, late.StateVersion)).Value;

        Assert.Equal(2, outcome.WeeksCommitted);
        Assert.Equal(BatchStopReason.CareerEnded, outcome.StopReason);
        Assert.Equal(NextStepKind.Ended, outcome.Career.Next.Kind);
        Assert.Equal(ErrorCodes.CareerEnded, (await RunAsync(kit, career, outcome.Career.Version)).Error.Code);
    }

    [Fact]
    public async Task A_wrong_secret_finds_no_career()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();

        var result = await RunAsync(kit, career with { Secret = "not-the-secret" }, career.Career.Version);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Repeating_a_completed_batch_after_the_career_moved_on_still_reports_it_completed()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var first = (await RunAsync(kit, career, career.Career.Version, command)).Value;
        await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(first.Career.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct);

        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((13, BatchStopReason.Completed), (repeat.WeeksCommitted, repeat.StopReason));
        Assert.Equal(ContractJson.Serialize(first.Weeks), ContractJson.Serialize(repeat.Weeks));
        Assert.Equal(14, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_batch_needs_a_week_boundary_the_current_version_and_an_active_career()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var v = career.Career.Version;

        Assert.Equal(ErrorCodes.StaleVersion, (await RunAsync(kit, career, v + 1)).Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, (await RunAsync(kit, career, v, weeks: 5)).Error.Code);
        await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(v, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct);
        Assert.Equal(ErrorCodes.WeekInProgress, (await RunAsync(kit, career, v + 1)).Error.Code);
    }

    [Fact]
    public async Task Repeating_a_finished_batch_returns_the_same_weeks()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var first = (await RunAsync(kit, career, career.Career.Version, command)).Value;
        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(repeat));
        Assert.Equal(13, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task Repeating_a_batch_after_the_career_moved_on_plays_no_more_weeks()
    {
        var kit = new AppTestKit(PausingRuleset.Engine(5));
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        await RunAsync(kit, career, career.Career.Version, command);
        var submit = kit.SubmitHandler();
        var view = (await kit.GetHandler().Handle(new GetCareer(career.CareerId, career.Secret), Ct)).Value;
        var step = (await submit.Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(view.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct)).Value;
        while (step.Career.Next.Kind == NextStepKind.ResolveEvent)
        {
            step = (await submit.Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(step.Career.Version, ActionType.ResolveEvent, null, null, null, null, step.Career.Next.Event!.Choices[0].ChoiceId)), Ct)).Value;
        }

        await submit.Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(step.Career.Version, ActionType.SettleWeek, null, null, null, null, null)), Ct);

        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((4, BatchStopReason.Interrupted), (repeat.WeeksCommitted, repeat.StopReason));
        Assert.Equal(4, (await kit.Store.FindCommandAsync(career.CareerId, command, Ct)).Count);
        Assert.Equal(5, repeat.Career.LastSettledWeek);
    }

    [Fact]
    public async Task A_crashed_batch_resumes_where_it_stopped()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(kit, career, career.Career.Version, command, store: new FailingAppendStore(kit.Store, 4)));
        Assert.Equal(3, kit.Store.LogCount(career.CareerId));

        var resumed = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((13, BatchStopReason.Completed), (resumed.WeeksCommitted, resumed.StopReason));
        Assert.Equal(Enumerable.Range(1, 13), (await kit.Store.FindCommandAsync(career.CareerId, command, Ct)).Select(a => a.Step));
    }

    [Fact]
    public async Task Another_request_in_the_middle_interrupts_the_batch()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var racing = new RacingCareerStore(kit.Store, async () =>
        {
            var current = (await kit.Store.FindAsync(career.CareerId, Ct))!.State;
            await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(current.StateVersion, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct);
        });

        var outcome = (await RunAsync(kit, career, career.Career.Version, store: racing)).Value;

        Assert.Equal((0, BatchStopReason.Interrupted), (outcome.WeeksCommitted, outcome.StopReason));
        Assert.Equal(NextStepKind.ResolveEvent, outcome.Career.Next.Kind);
    }

    [Fact]
    public async Task A_repeat_racing_the_same_batch_answers_like_it()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        AppResult<BatchOutcome>? twin = null;
        var racing = new RacingCareerStore(kit.Store, async () => twin = await RunAsync(kit, career, career.Career.Version, command));

        var outcome = (await RunAsync(kit, career, career.Career.Version, command, store: racing)).Value;

        Assert.Equal((13, BatchStopReason.Completed), (outcome.WeeksCommitted, outcome.StopReason));
        Assert.Equal(ContractJson.Serialize(twin!.Value), ContractJson.Serialize(outcome));
        Assert.Equal(13, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_manual_command_id_cannot_be_reused_for_a_batch()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var started = (await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, command, new ActionInput(career.Career.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct)).Value;

        Assert.Equal(ErrorCodes.CommandIdReused, (await RunAsync(kit, career, started.Career.Version, command)).Error.Code);
    }
}
