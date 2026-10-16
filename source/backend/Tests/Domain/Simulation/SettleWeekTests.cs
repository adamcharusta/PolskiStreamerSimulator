using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class SettleWeekTests
{
    private static readonly WeekEngine Engine = TestWorld.Engine;
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static CareerState StartedWithAllResolved(CareerState s, WeekPlan plan)
    {
        s = Engine.StartWeek(s, Content, plan, s.StateVersion).Value;
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var guaranteed = Engine.PresentEvent(s, Content).Value.Choices.First(c => c.Guaranteed).ChoiceId;
            s = Engine.ResolveEvent(s, Content, guaranteed, s.StateVersion).Value;
        }

        return s;
    }

    [Fact]
    public void Week_advances_once_and_clears_pending()
    {
        var (s1, rep) = Play.OneWeek(TestCareers.Dorm(2));
        Assert.Equal(2, s1.Week.Value);
        Assert.Null(s1.Pending);
        Assert.Equal(1, rep.Week.Value);
        Assert.Equal(1, s1.LastSettledWeek);
        Assert.Empty(CareerInvariants.Validate(s1));
    }

    [Fact]
    public void Settling_twice_is_rejected_without_random_draw()
    {
        var (s1, _) = Play.OneWeek(TestCareers.Dorm(2));
        var r = Engine.SettleWeek(s1, Content, s1.StateVersion);
        Assert.Equal(DomainErrorCode.WeekAlreadySettled, r.Error.Code);
    }

    [Fact]
    public void Settling_before_starting_is_rejected()
        => Assert.Equal(DomainErrorCode.NoPendingWeek, Engine.SettleWeek(TestCareers.Dorm(2), Content, 0).Error.Code);

    [Fact]
    public void Unresolved_events_block_settlement()
    {
        var s = Engine.StartWeek(TestCareers.Dorm(2), Content, new WeekPlan(MainAction.Rest, Category.Fps), 0).Value;
        Assert.Equal(DomainErrorCode.EventsUnresolved, Engine.SettleWeek(s, Content, s.StateVersion).Error.Code);
    }

    [Fact]
    public void Stale_settlement_is_rejected()
    {
        var s = StartedWithAllResolved(TestCareers.Dorm(2), new WeekPlan(MainAction.Rest, Category.Fps));
        Assert.Equal(DomainErrorCode.StaleVersion, Engine.SettleWeek(s, Content, s.StateVersion - 1).Error.Code);
    }

    [Fact]
    public void Stream_costs_energy_and_adds_burnout()
    {
        var s = StartedWithAllResolved(TestCareers.Dorm(2), new WeekPlan(MainAction.NoAction, Category.Fps));
        var (s1, report) = Engine.SettleWeek(s, Content, s.StateVersion).Value;
        // +5 no-action recovery, then −8 for the stream; guaranteed choices may also cost energy
        var eventEnergy = s.Pending!.Lines.Where(l => l.Code == "stat:Energy" && l.Detail != "action").Sum(l => l.Delta!.Value.Raw);
        Assert.Equal(Fixed.FromInt(67) + new Fixed(eventEnergy), s1.Energy);
        Assert.Equal(Fixed.FromInt(3), s1.Burnout);
        Assert.Contains(new ReportLine("stat:Energy", Delta: Fixed.FromInt(5), Detail: "action"), report.StatChanges);
        Assert.Contains(new ReportLine("stat:Burnout", Delta: Fixed.FromInt(3), Detail: "stream"), report.StatChanges);
    }

    [Fact]
    public void Skill_grows_with_diminishing_returns()
    {
        static Fixed Gain(int skill)
        {
            var s0 = TestCareers.Dorm(3);
            s0 = s0 with { Skill = s0.Skill.With(Category.Fps, Fixed.FromInt(skill)) };
            var s = StartedWithAllResolved(s0, new WeekPlan(MainAction.Rest, Category.Fps));
            return Engine.SettleWeek(s, Content, s.StateVersion).Value.State.Skill[Category.Fps] - Fixed.FromInt(skill);
        }

        Assert.Equal(Fixed.FromRatio(9, 10), Gain(10));
        Assert.Equal(Fixed.FromRatio(2, 10), Gain(90));
        Assert.Equal(Fixed.FromRatio(2, 10), Gain(99));
    }

    [Fact]
    public void Primary_category_gets_bored_while_others_recover()
    {
        var s0 = TestCareers.Dorm(4);
        s0 = s0 with { Boredom = CategoryValues<Fixed>.Uniform(Fixed.FromInt(10)) };
        var s = StartedWithAllResolved(s0, new WeekPlan(MainAction.Rest, Category.Moba));
        var s1 = Engine.SettleWeek(s, Content, s.StateVersion).Value.State;
        Assert.InRange(s1.Boredom[Category.Moba].Raw, 12_000_000, 14_000_000);
        Assert.All(Enum.GetValues<Category>().Where(c => c != Category.Moba), c => Assert.InRange(s1.Boredom[c].Raw, 8_000_000, 9_000_000));
        Assert.Equal(0, s1.InactiveWeeks[Category.Moba]);
        Assert.Equal(1, s1.InactiveWeeks[Category.Chess]);
    }

    [Fact]
    public void Unused_category_decays_only_after_13_weeks_and_not_below_start()
    {
        static Fixed ChessAfterOneWeek(int inactiveWeeks, Fixed chessSkill)
        {
            var s0 = TestCareers.Dorm(5);
            s0 = s0 with
            {
                InactiveWeeks = s0.InactiveWeeks.With(Category.Chess, inactiveWeeks),
                Skill = s0.Skill.With(Category.Chess, chessSkill),
            };
            var s = StartedWithAllResolved(s0, new WeekPlan(MainAction.Rest, Category.Fps));
            return Engine.SettleWeek(s, Content, s.StateVersion).Value.State.Skill[Category.Chess];
        }

        Assert.Equal(Fixed.FromInt(20), ChessAfterOneWeek(12, Fixed.FromInt(20)));
        Assert.Equal(Fixed.FromRatio(199, 10), ChessAfterOneWeek(13, Fixed.FromInt(20)));
        Assert.Equal(Fixed.FromInt(10), ChessAfterOneWeek(40, Fixed.FromRatio(1005, 100)));
        Assert.Equal(Fixed.FromInt(10), ChessAfterOneWeek(40, Fixed.FromInt(10)));
    }

    [Fact]
    public void Metrics_count_streams_and_category_weeks()
    {
        var (s1, _) = Play.OneWeek(TestCareers.Dorm(6), new WeekPlan(MainAction.Rest, Category.Chess));
        Assert.Equal(1, s1.Metrics.Streams);
        Assert.Equal(1, s1.Metrics.WeeksPerCategory[Category.Chess]);
        Assert.Equal(0, s1.Metrics.WeeksPerCategory[Category.Fps]);
    }

    [Fact]
    public void Report_lists_the_plan_and_recorded_rolls()
    {
        var plan = new WeekPlan(MainAction.ManageCommunity, Category.JustChatting);
        var (s1, report) = Play.OneWeek(TestCareers.Dorm(7), plan);
        Assert.Equal(plan, report.Plan);
        Assert.InRange(report.Rolls.Length, 2, 3);
        Assert.Contains(new ReportLine("stat:Credibility", Delta: Fixed.FromInt(3), Detail: "action"), report.StatChanges);
        Assert.Equal(s1.StateVersion, 2 + report.Rolls.Length);
    }

    [Fact]
    public void Expired_cooldowns_are_pruned()
    {
        var s0 = TestCareers.Dorm(8);
        s0 = s0 with { EventCooldowns = s0.EventCooldowns.Add("old", 2).Add("later", 9) };
        var (s1, _) = Play.OneWeek(s0);
        Assert.False(s1.EventCooldowns.ContainsKey("old"));
        Assert.Equal(9, s1.EventCooldowns["later"]);
    }

    [Fact]
    public void Same_seed_and_choices_produce_same_hash_after_52_weeks()
    {
        var a = Play.Weeks(TestCareers.Dorm(11), 52);
        var b = Play.Weeks(TestCareers.Dorm(11), 52);
        Assert.Equal(53, a.Week.Value);
        Assert.Equal(StateHash.Of(a), StateHash.Of(b));
        Assert.NotEqual(StateHash.Of(a), StateHash.Of(Play.Weeks(TestCareers.Dorm(12), 52)));
    }
}
