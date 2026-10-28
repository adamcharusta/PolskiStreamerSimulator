using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation.Settlement;

/// <summary>Stream quality, live viewers and the follower change of one settled stream.</summary>
public sealed record AudienceResult(Fixed Quality, long LiveViewers, long Gained, long Lost, long Net, ViralResult? Viral = null);

public static class AudienceRules
{
    private static readonly Fixed QualityOffset = Fixed.FromRatio(6, 10);
    private static readonly Fixed CredibilityNeutral = Fixed.FromInt(50);

    /// <summary>Stream quality 0-100 from skill, equipment, event effects, burnout and energy (gameplay-systems §3).</summary>
    public static Fixed Quality(CareerState s, PendingWeek p, BalanceConstants b)
    {
        var primary = p.Plan.Category;
        var profile = b.Profile(primary);
        var equipmentAverage = Fixed.FromRatio(s.Equipment.Values.Sum(), s.Equipment.Count);
        return (b.QualityBase
            + ((b.QualitySkill * profile.QualitySkillWeight) * s.Skill[primary])
            + (b.QualityEquipment * equipmentAverage)
            + p.EventQuality
            - (b.QualityBurnout * s.Burnout)
            + (b.QualityEnergy * s.Energy)).Clamp(Fixed.Zero, Fixed.Hundred);
    }

    /// <summary>Applies the gameplay-systems §3 formulas with one seeded noise draw (Czatnia factors from balance). Draws nothing for viral clips.</summary>
    public static AudienceResult Compute(CareerState s, PendingWeek p, ref Pcg32 rng, BalanceConstants b)
        => Compute(s, p, ref rng, b, Fixed.Zero);

    /// <summary>
    /// As above; a positive <paramref name="viralChance"/> adds a viral roll after the noise draw: on a hit the
    /// viewers are multiplied (still under the viral cap) and a bounded follower bonus is gained.
    /// </summary>
    public static AudienceResult Compute(CareerState s, PendingWeek p, ref Pcg32 rng, BalanceConstants b, Fixed viralChance)
    {
        var primary = p.Plan.Category;
        var profile = b.Profile(primary);
        var skill = s.Skill[primary];
        var boredom = s.Boredom[primary];
        var followers = Fixed.FromInt(s.Followers);
        var quality = Quality(s, p, b);

        var reach = b.ViewersBase
            + (b.ViewersSkill * skill)
            + (b.ViewersSqrtFollowers * FixedMath.Sqrt(followers))
            + (b.ViewersLinearFollowers * followers);
        var baseline = FixedMath.Max(b.ViewersMin, reach * b.PlatformReach * profile.Reach * (QualityOffset + (quality / Fixed.Hundred)));

        (var noise, rng) = rng.NextFixed(profile.NoiseMin, profile.NoiseMax);
        var uncapped = baseline * (Fixed.One - (boredom / b.BoredomDivisor)) * p.AudienceMultiplier * noise;
        var viral = viralChance > Fixed.Zero ? RollViral(ref rng, viralChance, b) : null;
        if (viral is not null)
        {
            uncapped *= viral.Multiplier;
        }

        var live = FixedMath.Max(Fixed.Zero, FixedMath.Min(b.ViralCapMultiplier * baseline, uncapped)).RoundToLong();
        var bonus = viral is null ? 0 : ViralBonus(s.Followers, live, b);

        var gained = bonus + (Fixed.FromInt(live) * b.ConversionRate * (s.Credibility / CredibilityNeutral) * profile.Conversion).RoundToLong();
        var mastery = skill >= b.MasteryFromSkill ? b.MasteryChurnFactor : Fixed.One;
        var lost = (followers * ((b.ChurnRate + (b.ChurnBoredomFactor * boredom)) * profile.Churn * mastery)).RoundToLong();
        return new AudienceResult(quality, live, gained, lost, gained - lost, viral is null ? null : viral with { FollowersBonus = bonus });
    }

    /// <summary>One 1-100 roll against <c>round(100 × chance)</c>; a hit draws the viewer multiplier (the bonus is filled in later).</summary>
    private static ViralResult? RollViral(ref Pcg32 rng, Fixed chance, BalanceConstants b)
    {
        (var roll, rng) = rng.NextInt(1, 100);
        var threshold = (int)(chance * Fixed.Hundred).RoundToLong();
        if (roll > threshold)
        {
            return null;
        }

        (var multiplier, rng) = rng.NextFixed(b.ViralMultiplierMin, b.ViralMultiplierMax);
        return new ViralResult(roll, threshold, multiplier, 0);
    }

    private static long ViralBonus(long followers, long live, BalanceConstants b)
        => Math.Max(b.ViralFollowerMin, FixedMath.Min(b.ViralFollowerShare * Fixed.FromInt(followers), b.ViralViewerShare * Fixed.FromInt(live)).RoundToLong());
}
