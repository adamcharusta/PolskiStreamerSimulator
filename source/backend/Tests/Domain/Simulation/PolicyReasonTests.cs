using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public sealed class PolicyReasonTests
{
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static CareerState Career() => TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 7);

    private static PlanDecision Plan(IChoicePolicy policy, CareerState s)
        => policy.ChoosePlan(s, Content, TestWorld.Engine.LegalPlans(s, Content).Value);

    [Fact]
    public void Low_energy_rest_names_the_energy_and_threshold()
    {
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(20) });

        Assert.Equal(MainAction.Rest, decision.Plan.Action);
        Assert.Equal("rest-low-energy", decision.Reason.Code);
        Assert.Equal("20.000000", decision.Reason.Args["energy"]);
        Assert.Equal("30", decision.Reason.Args["threshold"]);
        Assert.Equal("bonus", decision.Reason.Args["categoryRule"]);
        Assert.Equal(PolicyReason.Name(decision.Plan.Category), decision.Reason.Args["category"]);
    }

    [Fact]
    public void Exhaustion_is_forced_rest()
        => Assert.Equal("forced-rest", Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.Zero }).Reason.Code);

    [Fact]
    public void High_burnout_rest_names_the_limit()
    {
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(80), Burnout = Fixed.FromInt(75) });

        Assert.Equal("rest-high-burnout", decision.Reason.Code);
        Assert.Equal("70.000000", decision.Reason.Args["limit"]);
    }

    [Fact]
    public void A_rich_career_upgrades_its_cheapest_slot()
    {
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(80), CashPln = 1_000_000 });

        Assert.Equal(MainAction.ImproveProduction, decision.Plan.Action);
        Assert.Equal("upgrade-equipment", decision.Reason.Code);
        Assert.Equal(PolicyReason.Name(decision.Plan.UpgradeSlot!.Value), decision.Reason.Args["slot"]);
        Assert.Equal("1000000", decision.Reason.Args["cashPln"]);
    }

    [Fact]
    public void Every_fourth_week_trains()
    {
        // Enough cash to train, too little to reach the upgrade reserve.
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(80), CashPln = 500, Week = new CareerWeek(4) });

        Assert.Equal(MainAction.TrainCategory, decision.Plan.Action);
        Assert.Equal("train-scheduled", decision.Reason.Code);
        Assert.Equal("4", decision.Reason.Args["everyWeeks"]);
    }

    [Fact]
    public void A_bored_bonus_category_is_switched_and_says_so()
    {
        var s = Career() with { Energy = Fixed.FromInt(80), Boredom = CategoryValues<Fixed>.Uniform(Fixed.FromInt(90)).With(Category.Chess, Fixed.Zero) };

        var decision = Plan(BuiltInPolicies.Balanced, s);

        Assert.Equal(Category.Chess, decision.Plan.Category);
        Assert.Equal("least-bored", decision.Reason.Args["categoryRule"]);
    }

    [Fact]
    public void The_rotator_names_its_rotation()
        => Assert.Equal("rotation", Plan(BuiltInPolicies.Rotator, Career() with { Energy = Fixed.FromInt(80) }).Reason.Args["categoryRule"]);

    [Fact]
    public void Event_choices_name_the_odds_they_weighed()
    {
        var even = new EventPresentation("e", [new ChoiceOdds("bold", false, Fixed.FromRatio(3, 5), 60, [], false), new ChoiceOdds("safe", true, Fixed.One, 0, [], false)]);
        var unlikely = new EventPresentation("e", [new ChoiceOdds("bold", false, Fixed.FromRatio(2, 5), 40, [], false), new ChoiceOdds("safe", true, Fixed.One, 0, [], false)]);
        var gamblesOnly = new EventPresentation("e", [new ChoiceOdds("bold", false, Fixed.FromRatio(2, 5), 40, [], false)]);

        var gamble = BuiltInPolicies.Balanced.ChooseEventChoice(Career(), even);
        var guaranteed = BuiltInPolicies.Balanced.ChooseEventChoice(Career(), unlikely);
        var forced = BuiltInPolicies.Balanced.ChooseEventChoice(Career(), gamblesOnly);

        Assert.Equal(("bold", "gamble-best-odds", "0.600000"), (gamble.ChoiceId, gamble.Reason.Code, gamble.Reason.Args["probability"]));
        Assert.Equal(("safe", "take-guaranteed", "0.400000"), (guaranteed.ChoiceId, guaranteed.Reason.Code, guaranteed.Reason.Args["bestProbability"]));
        Assert.Equal(("bold", "only-gambles"), (forced.ChoiceId, forced.Reason.Code));
    }
}
