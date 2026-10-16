using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class EndingTests
{
    private static readonly WeekEngine Engine = TestWorld.Engine;
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static CareerState ReadyToSettle(CareerState s)
    {
        s = Engine.StartWeek(s, Content, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion).Value;
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, Content, Engine.PresentEvent(s, Content).Value.Choices[0].ChoiceId, s.StateVersion).Value;
        }

        return s;
    }

    /// <summary>A settle-ready week whose settlement leaves exactly <paramref name="finalCash"/> PLN.</summary>
    private static CareerState LandingOn(long finalCash)
    {
        var s = ReadyToSettle(TestCareers.Dorm(31));
        var delta = Engine.SettleWeek(s, Content, s.StateVersion).Value.Report.CashDelta;
        return s with { CashPln = finalCash - delta };
    }

    [Fact]
    public void Cash_exactly_minus_1000_after_settlement_is_insolvency()
    {
        var s = LandingOn(-1_000);
        var (end, report) = Engine.SettleWeek(s, Content, s.StateVersion).Value;
        Assert.Equal(-1_000, end.CashPln);
        Assert.Equal(CareerStatus.Ended, end.Status);
        Assert.Equal(EndingReason.Insolvency, end.Ending);
        Assert.Equal(EndingReason.Insolvency, report.Ending);
        Assert.Equal(0, end.FinalScore!.WealthPoints);
        Assert.Equal(1, end.Week.Value);
        Assert.Null(end.Pending);
        Assert.Empty(CareerInvariants.Validate(end));
    }

    [Fact]
    public void Cash_minus_999_continues()
    {
        var s = LandingOn(-999);
        var (next, report) = Engine.SettleWeek(s, Content, s.StateVersion).Value;
        Assert.Equal(-999, next.CashPln);
        Assert.Equal(CareerStatus.Active, next.Status);
        Assert.Null(report.Ending);
        Assert.Equal(2, next.Week.Value);
        Assert.Empty(CareerInvariants.Validate(next));
    }

    [Fact]
    public void Week_1040_completes_exactly_once()
    {
        var s = TestCareers.Dorm(4) with { Week = new CareerWeek(1040), LastSettledWeek = 1039 };
        var (end, rep) = Play.OneWeek(s);
        Assert.Equal(EndingReason.Completed, end.Ending);
        Assert.Equal(EndingReason.Completed, rep.Ending);
        Assert.Equal(1040, end.Week.Value);
        Assert.Contains(Achievements.CareerCompleted, end.Achievements);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.StartWeek(end, Content, new(MainAction.Rest, Category.Fps), end.StateVersion).Error.Code);
        Assert.Empty(CareerInvariants.Validate(end));
    }

    [Fact]
    public void Insolvency_in_the_last_week_wins_over_completion()
    {
        var s = LandingOn(-1_500) with { Week = new CareerWeek(1040), LastSettledWeek = 1039 };
        s = s with { Pending = s.Pending! with { Week = new CareerWeek(1040) } };
        Assert.Equal(EndingReason.Insolvency, Engine.SettleWeek(s, Content, s.StateVersion).Value.State.Ending);
    }

    [Fact]
    public void Early_retirement_and_insolvency_have_distinct_records()
    {
        var retired = Engine.Retire(TestCareers.Dorm(31) with { Followers = 2_000, CashPln = 3_000 }, Content, 0).Value;
        var (insolvent, _) = Engine.SettleWeek(LandingOn(-1_200), Content, LandingOn(-1_200).StateVersion).Value;
        Assert.Equal(EndingReason.EarlyRetirement, retired.Ending);
        Assert.Equal(EndingReason.Insolvency, insolvent.Ending);
        Assert.NotEqual(retired.Ending, insolvent.Ending);
        Assert.True(retired.FinalScore!.WealthPoints > 0);
        Assert.Equal(0, insolvent.FinalScore!.WealthPoints);
        Assert.Equal(1, retired.StateVersion);
        Assert.DoesNotContain(Achievements.CareerCompleted, retired.Achievements);
    }

    [Fact]
    public void Retirement_is_only_possible_between_weeks()
    {
        var started = Engine.StartWeek(TestCareers.Dorm(1), Content, new WeekPlan(MainAction.Rest, Category.Fps), 0).Value;
        Assert.Equal(new DomainError(DomainErrorCode.IllegalAction, "week-in-progress"), Engine.Retire(started, Content, started.StateVersion).Error);
        Assert.Equal(DomainErrorCode.StaleVersion, Engine.Retire(TestCareers.Dorm(1), Content, 3).Error.Code);
    }

    [Fact]
    public void Every_command_on_ended_career_returns_CareerEnded_and_keeps_rng()
    {
        var ended = Engine.Retire(TestCareers.Dorm(1), Content, 0).Value;
        var v = ended.StateVersion;
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.StartWeek(ended, Content, new WeekPlan(MainAction.Rest, Category.Fps), v).Error.Code);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.PresentEvent(ended, Content).Error.Code);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.ResolveEvent(ended, Content, "welcome", v).Error.Code);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.SettleWeek(ended, Content, v).Error.Code);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.Retire(ended, Content, v).Error.Code);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.Retire(ended, Content, v + 7).Error.Code);
        Assert.Equal(TestCareers.Dorm(1).Rng, ended.Rng);
    }

    [Fact]
    public void Final_score_uses_achievements_difficulty_and_background()
    {
        var s = TestCareers.Dorm(2) with { Followers = 1_000_000, CashPln = 10_000_000 };
        var ended = Engine.Retire(s, Content, 0).Value;
        // followers-1k, -10k and -100k give 6 of 10 points; Normal 1.10, Dorm 1.00
        Assert.Equal(6, Achievements.Points(ended.Achievements));
        Assert.Equal(96_000, ended.FinalScore!.BaseScore);
        Assert.Equal(105_600, ended.FinalScore.FinalScore);
    }

    [Theory]
    [InlineData(999, new string[0])]
    [InlineData(1_000, new[] { "followers-1k" })]
    [InlineData(10_000, new[] { "followers-10k", "followers-1k" })]
    [InlineData(100_000, new[] { "followers-100k", "followers-10k", "followers-1k" })]
    public void Follower_milestones_are_awarded_once_reached(long followers, string[] expected)
        => Assert.Equal(expected, Achievements.Evaluate(TestCareers.Dorm(1) with { Followers = followers }, completed: false).Order(StringComparer.Ordinal));

    [Fact]
    public void Achievements_are_kept_after_followers_drop()
    {
        var s = TestCareers.Dorm(1) with { Achievements = TestCareers.Dorm(1).Achievements.Add("followers-10k"), Followers = 10 };
        Assert.Contains("followers-10k", Achievements.Evaluate(s, completed: false));
        Assert.Equal(10, Achievements.Points(Achievements.Evaluate(s with { Followers = 100_000 }, completed: true)));
    }

    [Fact]
    public void Settlement_records_reached_milestones()
    {
        var (s1, _) = Play.OneWeek(TestCareers.Dorm(9) with { Followers = 20_000 });
        Assert.Contains("followers-10k", s1.Achievements);
        Assert.Equal(10_000, s1.Metrics.PeakFollowersMilestone);
    }
}
