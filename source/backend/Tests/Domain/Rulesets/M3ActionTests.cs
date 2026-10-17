using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3ActionTests
{
    [Fact]
    public void Network_plan_is_offered_with_visible_odds_in_every_category()
    {
        var s = M3Play.Fresh();
        var plans = M3Play.Engine.LegalPlans(s, M3Play.C).Value.Where(p => p.Plan.Action == MainAction.NetworkCollaborate).ToList();
        Assert.Equal(5, plans.Count);
        var odds = M3Play.Engine.PresentAction(s, M3Play.C, plans[0].Plan).Value!;
        Assert.Equal(50, odds.Threshold); // 0.35 + 0.003 × 50 credibility − 0.003 × 0 drama
        Assert.Equal(["modifier.credibility", "modifier.drama"], odds.Modifiers.Select(m => m.LabelKey));
    }

    [Fact]
    public void Network_week_costs_energy_records_its_roll_and_reports_it()
    {
        var s = M3Play.Fresh();
        var started = M3Play.Start(s, MainAction.NetworkCollaborate, Category.JustChatting);
        Assert.Equal(s.Energy - Fixed.FromInt(10), started.Energy);
        Assert.NotNull(started.Pending!.ActionRoll);
        Assert.Equal(started.Pending.ActionRoll, M3Play.SettleAll(started).Report.ActionRoll);
    }

    [Fact]
    public void Network_needs_energy()
    {
        var s = M3Play.Fresh() with { Energy = Fixed.FromInt(9) };
        Assert.All(
            M3Play.Engine.LegalPlans(s, M3Play.C).Value.Where(p => p.Plan.Action == MainAction.NetworkCollaborate),
            p => Assert.Equal("not-enough-energy", p.DisabledReason));
    }

    [Fact]
    public void M1_rejects_network()
    {
        var m1 = TestWorld.Content;
        var s = CareerFactory.Create(M1Ruleset.RulesetId, m1.Digest, m1.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 3);
        var result = M3Play.Engine.StartWeek(s, m1, new WeekPlan(MainAction.NetworkCollaborate, Category.Fps), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "unsupported-action"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Every_week_has_exactly_one_main_action_even_with_decisions()
    {
        var s = M3Play.Fresh();
        for (var i = 0; i < 40 && s.Status == CareerStatus.Active; i++)
        {
            while (s.PendingDecisions is { Length: > 0 })
            {
                s = M3Play.ResolveQueued(s, M3Play.Engine.PresentDecision(s, M3Play.C).Value.Choices[0].ChoiceId);
                Assert.Null(s.Pending);
            }

            var action = i % 3 == 0 && s.Energy >= Fixed.FromInt(10) ? MainAction.NetworkCollaborate : MainAction.Rest;
            var started = M3Play.Start(s, action);
            Assert.Equal(action, started.Pending!.Plan.Action);
            var second = M3Play.Engine.StartWeek(started, M3Play.C, new WeekPlan(MainAction.Rest, Category.Fps), started.StateVersion);
            Assert.False(second.IsSuccess);
            s = M3Play.SettleAll(started).State;
        }
    }
}
