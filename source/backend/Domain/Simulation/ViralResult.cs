using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// A viral clip of one stream: the 1-100 roll against the published threshold, the viewer multiplier and the
/// follower bonus it added to the week's gained followers.
/// </summary>
public sealed record ViralResult(int Roll, int Threshold, Fixed Multiplier, long FollowersBonus);
