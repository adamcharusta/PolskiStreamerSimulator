using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// Balance factors of one streaming category (M3 balance data): reach, viewer noise, follower conversion and churn,
/// boredom gain, income yields, the skill weight in stream quality, per-stream reputation effects and the viral base.
/// </summary>
public sealed record CategoryProfile(
    Fixed Reach,
    Fixed NoiseMin,
    Fixed NoiseMax,
    Fixed Conversion,
    Fixed Churn,
    int BoredomGainMin,
    int BoredomGainMax,
    Fixed SubYield,
    Fixed DonationYield,
    Fixed AdYield,
    Fixed QualitySkillWeight,
    Fixed CredibilityPerStream,
    Fixed DramaPerStream,
    Fixed ViralBase)
{
    /// <summary>The M1 profile: every factor is one, noise and boredom gain come from the global balance, no per-stream effects.</summary>
    public static CategoryProfile Neutral(BalanceConstants b) => new(
        Fixed.One,
        b.NoiseMin,
        b.NoiseMax,
        Fixed.One,
        Fixed.One,
        b.BoredomGainMin,
        b.BoredomGainMax,
        Fixed.One,
        Fixed.One,
        Fixed.One,
        Fixed.One,
        Fixed.Zero,
        Fixed.Zero,
        Fixed.Zero);
}
