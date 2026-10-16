using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Scoring;

public static class ScoreCalculator
{
    private static readonly Fixed MaxPoints = Fixed.FromInt(100_000);

    /// <summary>
    /// <c>F = clamp(0, 1, log10(1 + followers) / log10(1 + followersCap))</c>, <c>W</c> likewise for positive cash
    /// (insolvency counts as zero wealth), <c>A = min(1, points / cap)</c>; each contributes
    /// <c>round(100,000 × weight × component)</c> and <c>final = round(base × difficulty × background)</c>.
    /// </summary>
    public static ScoreBreakdown Compute(
        long followers,
        long cashPln,
        int achievementPoints,
        Difficulty difficulty,
        Fixed backgroundMultiplier,
        EndingReason ending,
        BalanceConstants b)
    {
        var wealth = ending == EndingReason.Insolvency ? 0 : Math.Max(0, cashPln);
        var f = Normalise(Math.Max(0, followers), b.ScoreFollowersCap);
        var w = Normalise(wealth, b.ScoreCashCap);
        var a = Fixed.FromRatio(Math.Clamp(achievementPoints, 0, b.AchievementPointsCap), b.AchievementPointsCap);

        var followersPoints = (MaxPoints * b.ScoreWeights.Followers * f).RoundToLong();
        var wealthPoints = (MaxPoints * b.ScoreWeights.Wealth * w).RoundToLong();
        var achievementScore = (MaxPoints * b.ScoreWeights.Achievements * a).RoundToLong();
        var baseScore = followersPoints + wealthPoints + achievementScore;
        var difficultyMultiplier = b.DifficultyMultiplier[difficulty];
        var finalScore = (Fixed.FromInt(baseScore) * difficultyMultiplier * backgroundMultiplier).RoundToLong();
        return new ScoreBreakdown(f, w, a, followersPoints, wealthPoints, achievementScore, baseScore, difficultyMultiplier, backgroundMultiplier, finalScore);
    }

    private static Fixed Normalise(long value, long cap)
        => (FixedMath.Log10(Fixed.FromInt(1 + value)) / FixedMath.Log10(Fixed.FromInt(1 + cap))).Clamp(Fixed.Zero, Fixed.One);
}
