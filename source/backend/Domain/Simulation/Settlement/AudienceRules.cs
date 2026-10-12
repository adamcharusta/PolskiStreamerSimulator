using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation.Settlement;

/// <summary>Stream quality, live viewers and the follower change of one settled stream.</summary>
public sealed record AudienceResult(Fixed Quality, long LiveViewers, long Gained, long Lost, long Net);

public static class AudienceRules
{
    private static readonly Fixed QualityOffset = Fixed.FromRatio(6, 10);
    private static readonly Fixed CredibilityNeutral = Fixed.FromInt(50);

    /// <summary>Applies the gameplay-systems §3 formulas with one seeded noise draw (Czatnia factors from balance).</summary>
    public static AudienceResult Compute(CareerState s, PendingWeek p, ref Pcg32 rng, BalanceConstants b)
    {
        var primary = p.Plan.Category;
        var skill = s.Skill[primary];
        var boredom = s.Boredom[primary];
        var followers = Fixed.FromInt(s.Followers);
        var equipmentAverage = Fixed.FromRatio(s.Equipment.Values.Sum(), s.Equipment.Count);

        var quality = (b.QualityBase
            + (b.QualitySkill * skill)
            + (b.QualityEquipment * equipmentAverage)
            + p.EventQuality
            - (b.QualityBurnout * s.Burnout)
            + (b.QualityEnergy * s.Energy)).Clamp(Fixed.Zero, Fixed.Hundred);

        var reach = b.ViewersBase
            + (b.ViewersSkill * skill)
            + (b.ViewersSqrtFollowers * FixedMath.Sqrt(followers))
            + (b.ViewersLinearFollowers * followers);
        var baseline = FixedMath.Max(b.ViewersMin, reach * b.PlatformReach * (QualityOffset + (quality / Fixed.Hundred)));

        (var noise, rng) = rng.NextFixed(b.NoiseMin, b.NoiseMax);
        var uncapped = baseline * (Fixed.One - (boredom / b.BoredomDivisor)) * p.AudienceMultiplier * noise;
        var live = FixedMath.Max(Fixed.Zero, FixedMath.Min(b.ViralCapMultiplier * baseline, uncapped)).RoundToLong();

        var gained = (Fixed.FromInt(live) * b.ConversionRate * (s.Credibility / CredibilityNeutral)).RoundToLong();
        var lost = (followers * (b.ChurnRate + (b.ChurnBoredomFactor * boredom))).RoundToLong();
        return new AudienceResult(quality, live, gained, lost, gained - lost);
    }
}
