using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class DecisionFlowTests
{
    [Fact]
    public async Task New_career_uses_the_ruleset_of_the_current_content()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var created = await kit.NewCareerAsync();
        Assert.Equal("m3-systems.1", created.Career.RulesetId);
    }

    [Fact]
    public async Task After_week_one_the_next_step_is_the_mandatory_milestone()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.PlayManualWeekAsync(await kit.NewCareerAsync());
        Assert.Equal(NextStepKind.ResolveDecision, career.Next.Kind);
        var shown = career.Next.Event!;
        Assert.Equal("first-week", shown.EventId);
        Assert.True(shown.Mandatory);

        // M3 events carry their ICU title and text as authored; the client formats them for the pronouns.
        var definition = TestContent.Of(TestPaths.ContentM3).Events.Single(e => e.Id == shown.EventId);
        Assert.Null(shown.TitleKey);
        Assert.Equal((definition.Title, definition.TextPl), (shown.Title, shown.Text));
        Assert.Equal(definition.Choices.Select(c => c.LabelPl), shown.Choices.Select(c => c.Label));
    }

    [Fact]
    public async Task Repeated_resolve_decision_returns_the_stored_outcome()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.PlayManualWeekAsync(await kit.NewCareerAsync());
        var input = new ActionInput(career.Version, ActionType.ResolveDecision, null, null, null, null, career.Next.Event!.Choices[0].ChoiceId);
        var commandId = Guid.NewGuid();
        var first = await kit.SubmitAsync(career.CareerId, commandId, input);
        var second = await kit.SubmitAsync(career.CareerId, commandId, input);
        Assert.Equal(ContractJson.Serialize(first.Value), ContractJson.Serialize(second.Value));
        Assert.NotNull(first.Value.Roll);
        Assert.Equal("first-week", first.Value.Roll.EventId);
        Assert.Equal(career.Version + 1, (await kit.GetAsync(career.CareerId)).Version);
    }

    [Fact]
    public async Task Start_week_while_a_decision_waits_is_a_conflict()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.PlayManualWeekAsync(await kit.NewCareerAsync());
        var result = await kit.SubmitAsync(career.CareerId, Guid.NewGuid(), new ActionInput(career.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null));
        Assert.Equal((ErrorCodes.DecisionPending, "decision-pending"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public async Task Resolve_decision_without_a_queued_decision_is_rejected()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;
        var result = await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.ResolveDecision, null, null, null, null, "steady"));
        Assert.Equal(ErrorCodes.NoPendingDecision, result.Error.Code);
    }

    [Fact]
    public async Task Resolve_decision_needs_a_choice()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.PlayManualWeekAsync(await kit.NewCareerAsync());
        var result = await kit.SubmitAsync(career.CareerId, Guid.NewGuid(), new ActionInput(career.Version, ActionType.ResolveDecision, null, null, null, null, null));
        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task Batch_stops_before_a_queued_decision_and_resolving_it_allows_planning()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        var batch = await kit.RunBatchAsync(career.CareerId, career.Career.Version);
        Assert.Equal(BatchStopReason.PausedForDecision, batch.Value.StopReason);
        Assert.Equal(1, batch.Value.WeeksCommitted);
        var view = batch.Value.Career;
        Assert.Equal(NextStepKind.ResolveDecision, view.Next.Kind);

        var resolved = await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.ResolveDecision, null, null, null, null, view.Next.Event!.Choices[0].ChoiceId));
        Assert.Equal(NextStepKind.PlanWeek, resolved.Value.Career.Next.Kind);
    }

    [Fact]
    public async Task The_next_report_lists_the_decision_roll()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var week2 = await kit.PlayManualWeekAsync(await kit.PlayManualWeekAsync(await kit.NewCareerAsync()));
        var report = week2.LastReport!;
        Assert.Equal(2, report.Week);
        Assert.Equal("first-week", Assert.Single(report.DecisionRolls).EventId);
        Assert.Null(report.ActionRoll);
    }

    [Fact]
    public async Task Network_plan_carries_its_odds()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;
        var network = view.Next.Plans.First(p => p.Plan.MainAction == MainAction.NetworkCollaborate);
        Assert.NotNull(network.Odds);
        Assert.False(network.Odds.Guaranteed);
        Assert.Equal(TestContent.Of(TestPaths.ContentM3).Balance.NetworkCollaborate!.LabelPl, network.Odds.Label);
        Assert.All(view.Next.Plans.Where(p => p.Plan.MainAction != MainAction.NetworkCollaborate), p => Assert.Null(p.Odds));
    }

    [Fact]
    public async Task M1_careers_keep_playing_after_the_switch()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM1);
        var m1 = await kit.NewCareerAsync();
        kit.SwitchCurrentContent(TestPaths.ContentM3);
        var played = await kit.PlayManualWeekAsync(m1);
        Assert.Equal("m1-prototype.1", played.RulesetId);
        Assert.Equal(NextStepKind.PlanWeek, played.Next.Kind);
        Assert.DoesNotContain(played.Next.Plans, p => p.Plan.MainAction == MainAction.NetworkCollaborate);
        Assert.Equal("m3-systems.1", (await kit.NewCareerAsync()).Career.RulesetId);
    }
}
