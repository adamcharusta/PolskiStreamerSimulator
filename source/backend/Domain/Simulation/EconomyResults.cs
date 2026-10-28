using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>M3-B: the week's wager, its 1-100 roll against the win threshold, and the net cash.</summary>
public sealed record WagerResult(WagerTier Tier, long StakePln, int Roll, int Threshold, bool Won, long NetPln);

/// <summary>M3-B: the policy exposure of a week and the strike roll (absent when no roll was needed).</summary>
public sealed record StrikeRoll(Platform Platform, int Exposure, int Threshold, int? Roll, bool Strike, bool Banned);

/// <summary>M3-B: a completed migration with the followers before and after.</summary>
public sealed record MigrationResult(Platform From, Platform To, Fixed Retention, long FollowersBefore, long FollowersAfter);
