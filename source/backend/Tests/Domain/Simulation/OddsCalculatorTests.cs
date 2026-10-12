using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class OddsCalculatorTests
{
    private static readonly BalanceConstants Balance = TestWorld.Content.Balance;

    private static ChoiceDefinition Choice(Fixed baseChance, params ChanceModifier[] modifiers)
        => new("c", "label", "Wybór", false, baseChance, [.. modifiers], false, new OutcomeSet([], [], null, null));

    [Theory]
    [InlineData(120, 950_000, 95)]
    [InlineData(-100, 50_000, 5)]
    [InlineData(55, 550_000, 55)]
    public void Probability_is_clamped_to_5_and_95(int basePercent, long expectedRaw, int expectedThreshold)
    {
        var odds = OddsCalculator.For(TestCareers.Dorm(1), Choice(Fixed.FromRatio(basePercent, 100)), Balance);
        Assert.Equal(expectedRaw, odds.Probability.Raw);
        Assert.Equal(expectedThreshold, odds.Threshold);
    }

    [Fact]
    public void Threshold_rounds_half_away_from_zero()
        => Assert.Equal(56, OddsCalculator.For(TestCareers.Dorm(1), Choice(Fixed.FromRatio(555, 1_000)), Balance).Threshold);

    [Fact]
    public void Easy_adds_ten_points_as_listed_modifier()
    {
        var normal = OddsCalculator.For(TestCareers.Dorm(1), Choice(Fixed.FromRatio(1, 2)), Balance);
        var easy = OddsCalculator.For(TestCareers.Dorm(1) with { Difficulty = Difficulty.Easy }, Choice(Fixed.FromRatio(1, 2)), Balance);
        Assert.Equal(50, normal.Threshold);
        Assert.Empty(normal.Modifiers);
        Assert.Equal(60, easy.Threshold);
        Assert.Equal([new ModifierLine("difficulty.easy", Fixed.FromRatio(1, 10))], easy.Modifiers);
    }

    [Fact]
    public void Stat_modifiers_scale_per_point_and_respect_caps()
    {
        var s = TestCareers.Dorm(1) with { Drama = Fixed.FromInt(40) }; // credibility 50
        var odds = OddsCalculator.For(
            s,
            Choice(
                Fixed.FromRatio(1, 2),
                new ChanceModifier(StatKey.Credibility, Fixed.FromRatio(3, 1_000), Fixed.FromRatio(1, 10), "modifier.credibility"),
                new ChanceModifier(StatKey.Drama, Fixed.FromRatio(-2, 1_000), null, "modifier.drama")),
            Balance);
        // 0.50 + min(0.10, 0.15) − 0.08 = 0.52
        Assert.Equal([new ModifierLine("modifier.credibility", Fixed.FromRatio(1, 10)), new ModifierLine("modifier.drama", Fixed.FromRatio(-8, 100))], odds.Modifiers);
        Assert.Equal(52, odds.Threshold);
    }

    [Fact]
    public void Primary_skill_modifier_reads_the_planned_category()
    {
        var started = TestWorld.Engine.StartWeek(TestCareers.Dorm(1), TestWorld.Content, new WeekPlan(MainAction.Rest, Category.Fps), 0).Value;
        var odds = OddsCalculator.For(started, Choice(Fixed.FromRatio(45, 100), new ChanceModifier(StatKey.PrimarySkill, Fixed.FromRatio(3, 1_000), null, "modifier.primary-skill")), Balance);
        // FPS skill 18 × 0.003 = 0.054
        Assert.Equal(Fixed.FromRatio(54, 1_000), odds.Modifiers.Single().Value);
        Assert.Equal(50, odds.Threshold);
    }

    [Fact]
    public void Guaranteed_choice_has_no_roll_odds()
    {
        var guaranteed = new ChoiceDefinition("g", "label", "Pewniak", true, Fixed.One, [], false, new OutcomeSet([], [], null, null));
        var odds = OddsCalculator.For(TestCareers.Dorm(1) with { Difficulty = Difficulty.Easy }, guaranteed, Balance);
        Assert.True(odds.Guaranteed);
        Assert.Equal(100, odds.Threshold);
        Assert.Empty(odds.Modifiers);
    }
}
