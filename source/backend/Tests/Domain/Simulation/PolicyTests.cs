using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class PolicyTests
{
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static WeekPlan Choose(IChoicePolicy policy, CareerState s)
        => policy.ChoosePlan(s, Content, TestWorld.Engine.LegalPlans(s, Content).Value).Plan;

    /// <summary>Dorm career at a given week with too little cash for the upgrade rule (needs more than 3 × 600 PLN).</summary>
    private static CareerState Poor(int week) => TestCareers.Dorm(1) with { CashPln = 500, Week = new CareerWeek(week) };

    [Fact]
    public void All_lists_five_named_policies()
    {
        Assert.Equal(["Balanced", "Grinder", "RestHeavy", "Specialist", "Rotator"], BuiltInPolicies.All.Select(p => p.Name));
        Assert.Same(BuiltInPolicies.Grinder, BuiltInPolicies.ByName("Grinder"));
        Assert.Throws<ArgumentException>(() => BuiltInPolicies.ByName("Gambler"));
    }

    [Theory]
    [InlineData(Background.DormRoomNewcomer, Category.Fps)]
    [InlineData(Background.LocalMemeCreator, Category.JustChatting)]
    [InlineData(Background.ChessClubRegular, Category.Chess)]
    [InlineData(Background.IndustryInsider, Category.JustChatting)]
    public void Balanced_streams_the_background_bonus_category(Background background, Category expected)
        => Assert.Equal(expected, Choose(BuiltInPolicies.Balanced, TestCareers.For(background, Difficulty.Normal, 1)).Category);

    [Fact]
    public void Balanced_rests_when_tired_or_burnt_out()
    {
        Assert.Equal(MainAction.Rest, Choose(BuiltInPolicies.Balanced, TestCareers.Dorm(1) with { Energy = Fixed.FromInt(29) }).Action);
        Assert.Equal(MainAction.Rest, Choose(BuiltInPolicies.Balanced, TestCareers.Dorm(1) with { Burnout = Fixed.FromInt(71) }).Action);
    }

    [Fact]
    public void Balanced_buys_the_cheapest_slot_only_with_a_large_reserve()
    {
        var s = TestCareers.Dorm(1) with { CashPln = 1_801, Week = new CareerWeek(3) };
        s = s with { Equipment = s.Equipment.SetItem(EquipmentSlot.Microphone, 1) };
        Assert.Equal(new WeekPlan(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Camera), Choose(BuiltInPolicies.Balanced, s));
        Assert.Equal(MainAction.NoAction, Choose(BuiltInPolicies.Balanced, s with { CashPln = 1_800 }).Action);
    }

    [Fact]
    public void Balanced_trains_every_fourth_week_and_calms_drama()
    {
        Assert.Equal(new WeekPlan(MainAction.TrainCategory, Category.Fps), Choose(BuiltInPolicies.Balanced, Poor(8)));
        Assert.Equal(MainAction.ManageCommunity, Choose(BuiltInPolicies.Balanced, Poor(9) with { Drama = Fixed.FromInt(41) }).Action);
        Assert.Equal(MainAction.NoAction, Choose(BuiltInPolicies.Balanced, Poor(9)).Action);
    }

    [Fact]
    public void Balanced_switches_away_from_a_bored_category_but_specialist_does_not()
    {
        var s = Poor(9);
        s = s with { Boredom = s.Boredom.With(Category.Fps, Fixed.FromInt(60)).With(Category.JustChatting, Fixed.FromInt(5)) };
        Assert.Equal(Category.Moba, Choose(BuiltInPolicies.Balanced, s).Category);
        Assert.Equal(Category.Fps, Choose(BuiltInPolicies.Specialist, s).Category);
    }

    [Fact]
    public void Grinder_trains_until_forced_to_rest()
    {
        Assert.Equal(new WeekPlan(MainAction.TrainCategory, Category.Fps), Choose(BuiltInPolicies.Grinder, TestCareers.Dorm(1) with { Energy = Fixed.FromInt(1) }));
        Assert.Equal(MainAction.Rest, Choose(BuiltInPolicies.Grinder, TestCareers.Dorm(1) with { Energy = Fixed.Zero }).Action);
    }

    [Fact]
    public void RestHeavy_rests_below_60_energy()
    {
        Assert.Equal(MainAction.Rest, Choose(BuiltInPolicies.RestHeavy, Poor(9) with { Energy = Fixed.FromInt(59) }).Action);
        Assert.Equal(MainAction.NoAction, Choose(BuiltInPolicies.RestHeavy, Poor(9) with { Energy = Fixed.FromInt(60) }).Action);
    }

    [Theory]
    [InlineData(10, Category.JustChatting)]
    [InlineData(12, Category.Moba)]
    [InlineData(14, Category.Gambling)]
    public void Rotator_cycles_categories_by_week(int week, Category expected)
        => Assert.Equal(expected, Choose(BuiltInPolicies.Rotator, TestCareers.Dorm(1) with { Week = new CareerWeek(week) }).Category);

    [Fact]
    public void Event_choice_takes_a_likely_gamble_or_plays_safe()
    {
        ChoiceOdds Odds(string id, int threshold, bool guaranteed = false)
            => new(id, guaranteed, Fixed.FromRatio(threshold, 100), threshold, [], false);

        var likely = new EventPresentation("e", [Odds("risky", 40), Odds("bold", 65), Odds("safe", 100, guaranteed: true)]);
        var unlikely = new EventPresentation("e", [Odds("risky", 40), Odds("bold", 45), Odds("safe", 100, guaranteed: true)]);
        var noSafe = new EventPresentation("e", [Odds("risky", 20), Odds("bold", 30)]);
        Assert.Equal("bold", BuiltInPolicies.Balanced.ChooseEventChoice(TestCareers.Dorm(1), likely).ChoiceId);
        Assert.Equal("safe", BuiltInPolicies.Balanced.ChooseEventChoice(TestCareers.Dorm(1), unlikely).ChoiceId);
        Assert.Equal("bold", BuiltInPolicies.Balanced.ChooseEventChoice(TestCareers.Dorm(1), noSafe).ChoiceId);
    }

    [Fact]
    public void Built_in_policies_never_retire()
        => Assert.All(BuiltInPolicies.All, p => Assert.False(p.ShouldRetire(TestCareers.Dorm(1) with { Week = new CareerWeek(1000) })));

    [Fact]
    public void M3All_lists_the_m3_policies_and_one_specialist_per_category()
    {
        Assert.Equal(
            ["Balanced", "Grinder", "RestHeavy", "Rotator", "Networker", "Specialist-JustChatting", "Specialist-Fps", "Specialist-Moba", "Specialist-Chess", "Specialist-Gambling"],
            BuiltInPolicies.M3All.Select(p => p.Name));
        Assert.Equal("Specialist-Chess", BuiltInPolicies.SpecialistOf(Category.Chess).Name);
        Assert.Same(BuiltInPolicies.SpecialistOf(Category.Moba), BuiltInPolicies.SpecialistOf(Category.Moba));
    }

    [Fact]
    public void Networker_networks_every_third_free_week()
    {
        var s = M3Play.Fresh() with { Week = new CareerWeek(3), Energy = Fixed.FromInt(80) };
        var decision = BuiltInPolicies.Networker.ChoosePlan(s, M3Play.C, M3Play.Engine.LegalPlans(s, M3Play.C).Value);
        Assert.Equal(MainAction.NetworkCollaborate, decision.Plan.Action);
        Assert.Equal("network-scheduled", decision.Reason.Code);
        Assert.Equal("3", decision.Reason.Args["everyWeeks"]);
    }

    [Fact]
    public void Networker_skips_networking_when_tired_off_schedule_or_without_the_action()
    {
        IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> Legal(CareerState s) => M3Play.Engine.LegalPlans(s, M3Play.C).Value;
        var tired = M3Play.Fresh() with { Week = new CareerWeek(3), Energy = Fixed.FromInt(39), CashPln = 500 };
        var offSchedule = M3Play.Fresh() with { Week = new CareerWeek(4), Energy = Fixed.FromInt(80), CashPln = 500 };
        Assert.NotEqual(MainAction.NetworkCollaborate, BuiltInPolicies.Networker.ChoosePlan(tired, M3Play.C, Legal(tired)).Plan.Action);
        Assert.NotEqual(MainAction.NetworkCollaborate, BuiltInPolicies.Networker.ChoosePlan(offSchedule, M3Play.C, Legal(offSchedule)).Plan.Action);
        Assert.Equal(MainAction.NoAction, Choose(BuiltInPolicies.Networker, Poor(9)).Action);
    }

    [Fact]
    public void Specialist_streams_only_its_category_even_when_bored()
    {
        var policy = BuiltInPolicies.SpecialistOf(Category.Chess);
        var s = M3Play.Fresh() with { Boredom = CategoryValues<Fixed>.Uniform(Fixed.FromInt(90)) };
        var decision = policy.ChoosePlan(s, M3Play.C, M3Play.Engine.LegalPlans(s, M3Play.C).Value);
        Assert.Equal(Category.Chess, decision.Plan.Category);
        Assert.Equal("fixed", decision.Reason.Args["categoryRule"]);
    }

    [Fact]
    public void A_rest_only_week_makes_every_policy_rest_with_fallback_rest()
    {
        var s = CareerFlags.Set(M3Play.Fresh() with { Week = new CareerWeek(3), Energy = Fixed.FromInt(20) }, "sys:rest-only", 1);
        var legal = M3Play.Engine.LegalPlans(s, M3Play.C).Value;
        Assert.All(BuiltInPolicies.M3All, policy =>
        {
            var decision = policy.ChoosePlan(s, M3Play.C, legal);
            Assert.Equal(MainAction.Rest, decision.Plan.Action);
            Assert.Equal("fallback-rest", decision.Reason.Code);
        });
    }

    [Fact]
    public void M3_policies_finish_careers_without_invariant_violations()
    {
        var c = new JsonContentSource(TestPaths.ContentM3).Load();
        foreach (var policy in BuiltInPolicies.M3All)
        {
            var start = CareerFactory.Create(c.RulesetId, c.Digest, c.Profiles[Background.ChessClubRegular], Background.ChessClubRegular, Difficulty.Normal, PronounSet.Nonbinary, 11);
            var run = CareerRunner.Run(M3Play.Engine, c, start, policy);
            Assert.Empty(run.InvariantViolations);
            Assert.Equal(CareerStatus.Ended, run.Final.Status);
        }
    }
}
