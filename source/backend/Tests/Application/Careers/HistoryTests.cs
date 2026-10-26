using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>M3-D: the week history read from the command log (#24).</summary>
public sealed class HistoryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<AppResult<HistoryView>> HistoryAsync(AppTestKit kit, CreatedCareer career, string? secret = null)
        => kit.HistoryHandler().Handle(new GetHistory(career.CareerId, secret ?? career.Secret), Ct);

    [Fact]
    public async Task A_new_career_has_an_empty_history()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        Assert.Empty((await HistoryAsync(kit, career)).Value.Weeks);
    }

    [Fact]
    public async Task Manual_and_automatic_weeks_appear_in_week_order_with_their_tags()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        var view = await kit.PlayManualWeekAsync(career);
        while (view.Next.Kind == NextStepKind.ResolveDecision)
        {
            view = (await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.ResolveDecision, null, null, null, null, view.Next.Event!.Choices.First(c => c.UnavailableReason is null).ChoiceId))).Value.Career;
        }

        var batch = (await kit.RunBatchAsync(view.CareerId, view.Version)).Value;
        var weeks = (await HistoryAsync(kit, career)).Value.Weeks;
        Assert.Equal(Enumerable.Range(1, 1 + batch.WeeksCommitted), weeks.Select(w => w.Week));
        Assert.False(weeks[0].Automatic);
        Assert.All(weeks.Skip(1), w => Assert.True(w.Automatic));
        Assert.Contains("decision", weeks[1].Tags);
        Assert.Equal(batch.Weeks[0].Report.CashDelta, weeks[1].CashDelta);
    }

    [Fact]
    public async Task One_week_returns_its_full_report_and_an_unplayed_week_is_not_found()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        var played = await kit.PlayManualWeekAsync(career);
        var week = await kit.WeekHandler().Handle(new GetHistoryWeek(career.CareerId, career.Secret, 1), Ct);
        Assert.Equal(ContractJson.Serialize(played.LastReport), ContractJson.Serialize(week.Value));
        var missing = await kit.WeekHandler().Handle(new GetHistoryWeek(career.CareerId, career.Secret, 2), Ct);
        Assert.Equal(ErrorCodes.WeekNotFound, missing.Error.Code);
    }

    [Fact]
    public async Task History_needs_the_career_secret()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var career = await kit.NewCareerAsync();
        Assert.Equal(ErrorCodes.CareerNotFound, (await HistoryAsync(kit, career, "wrong")).Error.Code);
    }

    [Fact]
    public void Tags_name_the_incidents_of_a_report()
    {
        var report = TestReports.Week(3) with
        {
            Viral = new ViralView(5, 10, 2m, 100),
            DecisionRolls = [TestReports.Roll()],
            Strike = new StrikeRollView(Platform.Czatnia, 2, 1, 1, true, true),
            Income = [new ReportLineView("income:sponsor", 300, null, "x")],
            StatChanges = [new ReportLineView("week:short-stream", null, null, null)],
        };
        Assert.Equal(["viral", "strike", "ban", "sponsorPaid", "decision", "shortStream"], HistoryReader.Tags(report));
    }
}
