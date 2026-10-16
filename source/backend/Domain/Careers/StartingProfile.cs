using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>Background-specific starting values, loaded from content.</summary>
public sealed record StartingProfile(
    long CashPln,
    long Followers,
    Fixed Drama,
    Fixed Credibility,
    Fixed Energy,
    CategoryValues<Fixed> Skill,
    ImmutableDictionary<EquipmentSlot, int> Equipment,
    Fixed ScoreMultiplier);
