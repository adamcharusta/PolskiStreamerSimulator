using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class StrategyPolicyTests
{
    private static readonly Strategy NoUpgrades = Strategy.Default with { Upgrades = false };

    private static CareerState At(int week, long cash = 500, int energy = 60)
        => M3BPlay.Fresh() with { Week = new CareerWeek(week), LastSettledWeek = week - 1, CashPln = cash, Energy = Fixed.FromInt(energy) };

    private static PlanDecision Plan(Strategy strategy, CareerState s)
        => new StrategyPolicy("Test", strategy).ChoosePlan(s, M3BPlay.C, M3BPlay.Engine.LegalPlans(s, M3BPlay.C).Value);

    [Fact]
    public void The_free_week_follows_the_routine_step()
    {
        var strategy = NoUpgrades with { Routine = [MainAction.NoAction, MainAction.ManageCommunity] };
        var first = Plan(strategy, At(5));
        Assert.Equal((MainAction.NoAction, "routine-step", "1", "2"), (first.Plan.Action, first.Reason.Code, first.Reason.Args["step"], first.Reason.Args["of"]));
        Assert.Equal(MainAction.ManageCommunity, Plan(strategy, At(6)).Plan.Action);
        Assert.Equal("fps", first.Reason.Args["category"]);
    }

    [Fact]
    public void Forced_and_threshold_rest_come_first()
    {
        Assert.Equal("forced-rest", Plan(NoUpgrades, At(5, energy: 0)).Reason.Code);
        var tired = Plan(NoUpgrades with { RestBelowEnergy = 50 }, At(5, energy: 40));
        Assert.Equal((MainAction.Rest, "rest-low-energy"), (tired.Plan.Action, tired.Reason.Code));
        var burnt = Plan(NoUpgrades with { RestAboveBurnout = 60 }, At(5) with { Burnout = Fixed.FromInt(65) });
        Assert.Equal("rest-high-burnout", burnt.Reason.Code);
    }

    [Fact]
    public void Risk_appetite_sets_the_drama_limit()
    {
        var dramatic = At(5) with { Drama = Fixed.FromInt(30) };
        Assert.Equal("manage-community-drama", Plan(NoUpgrades with { Risk = RiskAppetite.Safe }, dramatic).Reason.Code);
        Assert.Equal("routine-step", Plan(NoUpgrades, dramatic).Reason.Code);
    }

    [Fact]
    public void Upgrades_keep_the_reserve()
    {
        var upgrade = Plan(Strategy.Default, At(5, cash: 2_000));
        Assert.Equal((MainAction.ImproveProduction, "strategy-upgrade"), (upgrade.Plan.Action, upgrade.Reason.Code));
        Assert.Equal("routine-step", Plan(Strategy.Default with { ReservePln = 1_500 }, At(5, cash: 2_000)).Reason.Code);
        Assert.Equal("routine-step", Plan(NoUpgrades, At(5, cash: 2_000)).Reason.Code);
    }

    [Fact]
    public void Wager_is_staked_on_gambling_weeks_above_the_reserve()
    {
        var gambler = NoUpgrades with { CategoryPlan = CategoryPlan.Focus, Focus = Category.Gambling, Wager = WagerTier.Medium };
        var staked = Plan(gambler, At(5, cash: 4_000));
        Assert.Equal((WagerTier.Medium, Category.Gambling, "strategy-wager"), (staked.Plan.Wager, staked.Plan.Category, staked.Reason.Code));
        Assert.Null(Plan(gambler with { ReservePln = 3_600 }, At(5, cash: 4_000)).Plan.Wager);
        Assert.Null(Plan(gambler with { Focus = Category.Chess }, At(5, cash: 4_000)).Plan.Wager);
    }

    [Fact]
    public void Side_business_runs_every_fourth_week_when_enabled()
    {
        var s = At(62) with { Followers = 3_000 };
        var worked = Plan(NoUpgrades with { SideBusiness = true }, s);
        Assert.Equal((MainAction.SideBusinessWork, "strategy-side-business"), (worked.Plan.Action, worked.Reason.Code));
        Assert.NotEqual(MainAction.SideBusinessWork, Plan(NoUpgrades with { SideBusiness = true }, At(63) with { Followers = 3_000 }).Plan.Action);
        Assert.NotEqual(MainAction.SideBusinessWork, Plan(NoUpgrades, s).Plan.Action);
    }

    [Fact]
    public void A_disabled_routine_action_falls_back_to_a_safe_plan()
    {
        var decision = Plan(NoUpgrades with { Routine = [MainAction.NetworkCollaborate], RestBelowEnergy = 0 }, At(5, energy: 8));
        Assert.Equal((MainAction.NoAction, "fallback-safe"), (decision.Plan.Action, decision.Reason.Code));
        Assert.Equal(("networkCollaborate", "not-enough-energy"), (decision.Reason.Args["preferred"], decision.Reason.Args["disabledReason"]));
    }

    [Fact]
    public void A_rest_only_week_rests_with_the_fallback_reason()
    {
        var s = CareerFlags.Set(At(5), "sys:rest-only", 1);
        var decision = Plan(NoUpgrades, s);
        Assert.Equal((MainAction.Rest, "fallback-safe"), (decision.Plan.Action, decision.Reason.Code));
        Assert.All(M3BPlay.Engine.LegalPlans(s, M3BPlay.C).Value.Where(l => l.Plan == decision.Plan), l => Assert.Null(l.DisabledReason));
    }

    [Fact]
    public void A_due_installment_is_fulfilled_first()
    {
        var s = At(16) with { Contracts = [new SponsorContract("trusted-tea:4", "trusted-tea", SponsorKind.Contract, 4, 16, 27, 400, 0)] };
        Assert.Equal((MainAction.PitchFulfillSponsor, "sponsor-due"), Plan(NoUpgrades, s) is var d ? (d.Plan.Action, d.Reason.Code) : default);
    }

    [Fact]
    public void Offers_follow_the_strategy()
    {
        var offer = new SponsorOffer("trusted-tea:1", "trusted-tea", 1, 2, 300);
        Assert.True(new StrategyPolicy("T", Strategy.Default).ChooseOffer(M3BPlay.Fresh(), M3BPlay.C, offer, null).Accept);
        var refuse = new StrategyPolicy("T", Strategy.Default with { AcceptOffers = false }).ChooseOffer(M3BPlay.Fresh(), M3BPlay.C, offer, null);
        Assert.Equal((false, "strategy"), (refuse.Accept, refuse.Reason.Args["reason"]));
        var wagering = new StrategyPolicy("T", Strategy.Default with { Wager = WagerTier.Low }).ChooseOffer(M3BPlay.Fresh(), M3BPlay.C, offer, null);
        Assert.Equal((false, "gambling"), (wagering.Accept, wagering.Reason.Args["reason"]));
        var blocked = new StrategyPolicy("T", Strategy.Default).ChooseOffer(M3BPlay.Fresh(), M3BPlay.C, offer, "contract-limit");
        Assert.Equal("contract-limit", blocked.Reason.Args["reason"]);
    }

    [Theory]
    [InlineData(RiskAppetite.Safe, "safe-choice")]
    [InlineData(RiskAppetite.Balanced, "gamble")]
    [InlineData(RiskAppetite.Bold, "gamble")]
    public void Risk_appetite_sets_the_gamble_threshold(RiskAppetite risk, string expected)
    {
        var e = new EventPresentation("x", [
            new ChoiceOdds("gamble", false, Fixed.FromRatio(55, 100), 55, [], false),
            new ChoiceOdds("safe-choice", true, Fixed.One, 100, [], false),
        ]);
        Assert.Equal(expected, new StrategyPolicy("T", Strategy.Default with { Risk = risk }).ChooseEventChoice(M3BPlay.Fresh(), e).ChoiceId);
    }

    [Fact]
    public void A_ban_moves_to_the_best_platform()
    {
        var s = M3BPlay.Fresh() with { Week = new CareerWeek(40), BannedPlatforms = [Platform.Czatnia], PendingDecisions = ["platform-ban"] };
        var decision = new StrategyPolicy("T", Strategy.Default).ChooseEventChoice(s, M3BPlay.Engine.PresentDecision(s, M3BPlay.C).Value);
        Assert.Equal(("move-PulsLive", "ban-move"), (decision.ChoiceId, decision.Reason.Code));
    }

    [Fact]
    public void Simulator_strategies_finish_careers_without_invariant_violations()
    {
        var c = new JsonContentSource(TestPaths.ContentM3).Load();
        Assert.Equal(["Strategy-Default", "Strategy-Safe", "Strategy-Bold"], BuiltInPolicies.Strategies.Select(p => p.Name));
        foreach (var policy in BuiltInPolicies.Strategies)
        {
            var run = CareerRunner.Run(M3Play.Engine, c, CareerFactory.Create(c, Background.LocalMemeCreator, Difficulty.Normal, PronounSet.Woman, 5), policy);
            Assert.Empty(run.InvariantViolations);
            Assert.Equal(CareerStatus.Ended, run.Final.Status);
        }
    }

    [Fact]
    public void M3CAll_adds_the_strategies_to_the_m3b_list()
        => Assert.Equal([.. BuiltInPolicies.M3BAll, .. BuiltInPolicies.Strategies], BuiltInPolicies.M3CAll);
}
