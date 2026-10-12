using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Scoring;

/// <summary>
/// Every component of the final score: normalised followers <see cref="F"/>, wealth <see cref="W"/> and
/// achievements <see cref="A"/> (each 0–1), their 45/45/10 point contributions, the base score and the
/// difficulty and background multipliers applied to reach <see cref="FinalScore"/>.
/// </summary>
public sealed record ScoreBreakdown(
    Fixed F,
    Fixed W,
    Fixed A,
    long FollowersPoints,
    long WealthPoints,
    long AchievementPoints,
    long BaseScore,
    Fixed DifficultyMultiplier,
    Fixed BackgroundMultiplier,
    long FinalScore);
