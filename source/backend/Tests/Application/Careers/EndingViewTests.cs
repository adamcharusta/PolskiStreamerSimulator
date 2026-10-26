using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>M3-C career view: achievements with Polish names, and the score and summary of an ended career (#23).</summary>
public sealed class EndingViewTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_active_career_lists_every_achievement_unearned_and_no_score()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;
        Assert.Equal(11, view.Achievements.Count);
        Assert.All(view.Achievements, a => Assert.False(a.Earned));
        Assert.False(string.IsNullOrEmpty(view.Achievements[0].Name));
        Assert.Null(view.Score);
        Assert.Null(view.Summary);
    }

    [Fact]
    public async Task A_retired_career_shows_its_score_breakdown_and_summary()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;
        var retired = (await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.Retire, null, null, null, null, null))).Value.Career;
        Assert.Equal(CareerStatus.Ended, retired.Status);
        var score = retired.Score!;
        Assert.Equal(score.FollowersPoints + score.WealthPoints + score.AchievementPoints, score.BaseScore);
        Assert.Equal(1.10m, score.DifficultyMultiplier);
        Assert.Equal((EndingReason.EarlyRetirement, Reputation.Steady), (retired.Summary!.Reason, retired.Summary.Reputation));
    }

    [Fact]
    public async Task Earned_achievements_come_first()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;
        var stored = (await kit.Store.FindAsync(view.CareerId, Ct))!;
        await kit.Store.AppendAsync(
            view.CareerId,
            stored.State.StateVersion,
            stored.State with { Achievements = ["new-home"], StateVersion = stored.State.StateVersion + 1 },
            new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null),
            kit.Time.GetUtcNow(),
            Ct);
        var reread = await kit.GetAsync(view.CareerId);
        Assert.Equal(("new-home", true, 2), (reread.Achievements[0].Id, reread.Achievements[0].Earned, reread.Achievements[0].Points));
    }

    [Fact]
    public async Task An_m1_career_lists_the_m1_achievements()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM1);
        var view = (await kit.NewCareerAsync()).Career;
        Assert.Equal(["followers-1k", "followers-10k", "followers-100k", "career-completed"], view.Achievements.Select(a => a.Id));
    }
}
