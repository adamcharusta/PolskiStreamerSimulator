using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Rulesets.M3;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3DecisionTests
{
    private static readonly WeekEngine Engine = new(new RulesetRegistry([new M1Ruleset(), new M3Ruleset()]));
    private static readonly ContentSnapshot C = M3TestContent.Valid();

    private static CareerState Fresh(ulong seed = 3)
        => CareerFactory.Create(M3Ruleset.RulesetId, C.Digest, C.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, seed);

    /// <summary>Plays a week without an action, or resting when the M1 forced-rest rule demands it.</summary>
    private static CareerState PlayWeek(CareerState s)
    {
        var action = s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred ? MainAction.Rest : MainAction.NoAction;
        s = Engine.StartWeek(s, C, new WeekPlan(action, Category.Fps), s.StateVersion).Value;
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var e = Engine.PresentEvent(s, C).Value;
            s = Engine.ResolveEvent(s, C, e.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return Engine.SettleWeek(s, C, s.StateVersion).Value.State;
    }

    /// <summary>Answers the first queued decision with its first choice (<c>calm</c>, or <c>week-off</c> for the recovery crisis).</summary>
    private static CareerState AnswerFirst(CareerState s)
        => Engine.ResolveDecision(s, C, Engine.PresentDecision(s, C).Value.Choices[0].ChoiceId, s.StateVersion).Value;

    [Fact]
    public void Recovery_crisis_outranks_due_decision_follow_ups()
    {
        var other = M3TestContent.Decision("other-crisis", EventPhase.Crisis, 1, 1040, oneTime: false);
        var c = C with { Events = C.Events.Add(other) };
        var s = Fresh() with
        {
            Energy = Fixed.Zero,
            Scheduled = [new ScheduledEvent("test-crisis", 1), new ScheduledEvent("other-crisis", 1)],
        };

        var rng = Pcg32.Create(1UL, 1UL);
        var opened = DecisionQueue.Open(s, c, ref rng);

        Assert.Equal(["burnout-recovery", "test-crisis"], opened.PendingDecisions!.Value);
        Assert.Equal([new ScheduledEvent("other-crisis", 1)], opened.Scheduled!.Value);
    }

    [Fact]
    public void One_time_milestone_opens_before_week_two_and_blocks_start()
    {
        var s = PlayWeek(Fresh());
        Assert.Equal(["first-week"], s.PendingDecisions);
        Assert.True(Engine.RequiresPlayerDecision(s, C).Value);
        var start = Engine.StartWeek(s, C, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion);
        Assert.Equal(DomainErrorCode.DecisionPending, start.Error.Code);
    }

    [Fact]
    public void Resolving_a_decision_applies_stats_now_and_carries_cash_into_the_next_report()
    {
        var s = PlayWeek(Fresh());
        var cash = s.CashPln;
        s = Engine.ResolveDecision(s, C, "bold", s.StateVersion).Value;
        Assert.Empty(s.PendingDecisions!.Value);
        Assert.Equal(cash, s.CashPln);
        Assert.Single(s.Carry!.Rolls);
        Assert.Contains("first-week", s.UsedOneTime!);

        s = Engine.StartWeek(s, C, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion).Value;
        Assert.Null(s.Carry);
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, C, Engine.PresentEvent(s, C).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        var (after, report) = Engine.SettleWeek(s, C, s.StateVersion).Value;
        Assert.Single(report.DecisionRolls);
        Assert.Equal(after.CashPln - s.CashPln, report.CashDelta);
        Assert.Contains(report.Income.Concat(report.Costs), l => l.Detail == "first-week");
    }

    [Fact]
    public void One_time_milestone_never_opens_twice()
    {
        var s = PlayWeek(Fresh());
        s = Engine.ResolveDecision(s, C, "calm", s.StateVersion).Value;
        for (var i = 0; i < 30 && s.Status == CareerStatus.Active; i++)
        {
            while (s.PendingDecisions is { Length: > 0 } queue)
            {
                Assert.NotEqual("first-week", queue[0]);
                s = AnswerFirst(s);
            }

            s = PlayWeek(s);
        }
    }

    [Fact]
    public void Retire_with_a_queued_decision_is_rejected()
    {
        var s = PlayWeek(Fresh());
        Assert.NotEmpty(s.PendingDecisions!.Value);
        var retired = Engine.Retire(s, C, s.StateVersion);
        Assert.Equal(DomainErrorCode.DecisionPending, retired.Error.Code);
        Assert.Equal("decision-pending", retired.Error.Detail);
    }

    [Fact]
    public void Retire_folds_the_decision_carry_into_the_final_score()
    {
        var s = PlayWeek(Fresh());
        s = Engine.ResolveDecision(s, C, "bold", s.StateVersion).Value with { Followers = 1_000 };
        var carry = s.Carry!;
        Assert.NotEqual(0, carry.CashPln);

        var retired = Engine.Retire(s, C, s.StateVersion).Value;

        var followers = 1_000 + (Fixed.FromInt(1_000) * carry.FollowersPercent / Fixed.Hundred).RoundToLong();
        Assert.Equal(s.CashPln + carry.CashPln, retired.CashPln);
        Assert.Equal(followers, retired.Followers);
        Assert.Null(retired.Carry);
        Assert.Null(retired.Scheduled);
        var folded = s with { CashPln = s.CashPln + carry.CashPln, Followers = followers, Carry = null };
        Assert.Equal(new M1Ruleset().Retire(folded, C, s.StateVersion).Value.FinalScore, retired.FinalScore);
    }

    [Fact]
    public void Start_week_checks_the_command_before_queued_decisions()
    {
        var s = PlayWeek(Fresh());
        Assert.NotEmpty(s.PendingDecisions!.Value);
        var plan = new WeekPlan(MainAction.NoAction, Category.Fps);
        Assert.Equal(DomainErrorCode.StaleVersion, Engine.StartWeek(s, C, plan, s.StateVersion - 1).Error.Code);
        var ended = s with { Status = CareerStatus.Ended };
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.StartWeek(ended, C, plan, s.StateVersion).Error.Code);
        Assert.Equal(DomainErrorCode.CareerEnded, Engine.Retire(ended, C, s.StateVersion).Error.Code);
    }

    [Fact]
    public void Resolve_decision_without_one_is_rejected()
        => Assert.Equal(DomainErrorCode.NoPendingDecision, Engine.ResolveDecision(Fresh(), C, "calm", 0).Error.Code);

    [Fact]
    public void M1_career_has_no_decisions()
    {
        var m1 = TestWorld.Content;
        var s = CareerFactory.Create(M1Ruleset.RulesetId, m1.Digest, m1.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 3);
        Assert.Equal(DomainErrorCode.NoPendingDecision, Engine.PresentDecision(s, m1).Error.Code);
    }

    [Fact]
    public void Repeatable_decisions_average_at_most_one_per_thirteen_weeks()
    {
        var opened = 0;
        var weeks = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var s = Fresh(seed);
            for (var i = 0; i < 104 && s.Status == CareerStatus.Active; i++, weeks++)
            {
                while (s.PendingDecisions is { Length: > 0 } queue)
                {
                    opened += queue[0] == "test-crisis" ? 1 : 0;
                    s = AnswerFirst(s);
                }

                s = PlayWeek(s);
            }
        }

        Assert.InRange(opened / (double)weeks, 0.0, 1.0 / 13);
    }

    [Fact]
    public void Runner_answers_queued_decisions_and_plays_m3_careers_without_violations()
    {
        foreach (var policy in BuiltInPolicies.All)
        {
            var decisions = 0;
            var result = CareerRunner.Run(Engine, C, Fresh(5), policy, (_, report) => decisions += report.DecisionRolls.Length);
            Assert.Empty(result.InvariantViolations);
            Assert.Equal(CareerStatus.Ended, result.Final.Status);
            Assert.Contains("first-week", result.Final.UsedOneTime!);
            Assert.True(decisions > 0, policy.Name);
        }
    }
}
