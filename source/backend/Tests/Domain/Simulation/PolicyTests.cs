using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class PolicyTests
{
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static WeekPlan Choose(IChoicePolicy policy, CareerState s)
        => policy.ChoosePlan(s, Content, TestWorld.Engine.LegalPlans(s, Content).Value);

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
        Assert.Equal("bold", BuiltInPolicies.Balanced.ChooseEventChoice(TestCareers.Dorm(1), likely));
        Assert.Equal("safe", BuiltInPolicies.Balanced.ChooseEventChoice(TestCareers.Dorm(1), unlikely));
        Assert.Equal("bold", BuiltInPolicies.Balanced.ChooseEventChoice(TestCareers.Dorm(1), noSafe));
    }

    [Fact]
    public void Built_in_policies_never_retire()
        => Assert.All(BuiltInPolicies.All, p => Assert.False(p.ShouldRetire(TestCareers.Dorm(1) with { Week = new CareerWeek(1000) })));
}
