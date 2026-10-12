using FsCheck;
using FsCheck.Xunit;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Scoring;

public class ScoreCalculatorTests
{
    private static readonly BalanceConstants Balance = TestWorld.Content.Balance;

    private static ScoreBreakdown Score(long followers, long cash, int points = 0, Difficulty d = Difficulty.Easy, Fixed? background = null, EndingReason ending = EndingReason.Completed)
        => ScoreCalculator.Compute(followers, cash, points, d, background ?? Fixed.One, ending, Balance);

    [Fact]
    public void Score_of_zero_career_is_zero()
    {
        var s = Score(0, 0);
        Assert.Equal(0, s.BaseScore);
        Assert.Equal(0, s.FinalScore);
        Assert.Equal(Fixed.Zero, s.F);
        Assert.Equal(Fixed.Zero, s.W);
        Assert.Equal(Fixed.Zero, s.A);
    }

    [Fact]
    public void Max_values_give_100000_before_multipliers()
    {
        var s = Score(1_000_000, 10_000_000, 10);
        Assert.Equal(Fixed.One, s.F);
        Assert.Equal(Fixed.One, s.W);
        Assert.Equal(Fixed.One, s.A);
        Assert.Equal(45_000, s.FollowersPoints);
        Assert.Equal(45_000, s.WealthPoints);
        Assert.Equal(10_000, s.AchievementPoints);
        Assert.Equal(100_000, s.BaseScore);
        Assert.Equal(100_000, s.FinalScore);
    }

    [Fact]
    public void Components_are_capped_at_one()
    {
        var s = Score(50_000_000, 900_000_000, 25);
        Assert.Equal(100_000, s.BaseScore);
    }

    [Fact]
    public void Normal_and_background_multipliers_apply()
    {
        // F = 1 → 45,000; W = 0; A = 5/10 → 5,000; base 50,000 × 1.10 × 0.90
        var s = Score(1_000_000, 0, 5, Difficulty.Normal, Fixed.FromRatio(90, 100));
        Assert.Equal(50_000, s.BaseScore);
        Assert.Equal(Fixed.FromRatio(110, 100), s.DifficultyMultiplier);
        Assert.Equal(Fixed.FromRatio(90, 100), s.BackgroundMultiplier);
        Assert.Equal(49_500, s.FinalScore);
    }

    [Fact]
    public void Log_normalization_matches_a_hand_computed_value()
    {
        // F = log10(1 + 999) / log10(1 + 10^6) = 3 / 6.0000004 ≈ 0.5; points = round(45,000 × 0.5) = 22,500
        var s = Score(999, 0);
        Assert.InRange(s.FollowersPoints, 22_499, 22_500);
    }

    [Fact]
    public void Insolvency_counts_wealth_as_zero()
    {
        Assert.Equal(0, Score(5_000, 50_000, ending: EndingReason.Insolvency).WealthPoints);
        Assert.True(Score(5_000, 50_000, ending: EndingReason.EarlyRetirement).WealthPoints > 0);
    }

    [Fact]
    public void Negative_cash_counts_as_zero_wealth() => Assert.Equal(0, Score(5_000, -800, ending: EndingReason.Completed).WealthPoints);

    [Property]
    public bool Score_is_monotonic_in_followers(PositiveInt a, PositiveInt b)
    {
        var (low, high) = a.Get <= b.Get ? (a.Get, b.Get) : (b.Get, a.Get);
        return Score(low, 25_000, 3).FinalScore <= Score(high, 25_000, 3).FinalScore;
    }

    [Property]
    public bool Score_is_monotonic_in_cash(PositiveInt a, PositiveInt b)
    {
        var (low, high) = a.Get <= b.Get ? (a.Get, b.Get) : (b.Get, a.Get);
        return Score(12_000, low, 3).FinalScore <= Score(12_000, high, 3).FinalScore;
    }
}
