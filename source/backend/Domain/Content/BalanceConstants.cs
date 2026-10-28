using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

public sealed record ScoreWeights(Fixed Followers, Fixed Wealth, Fixed Achievements);

/// <summary>Named balance values from <c>balance.json</c>; property names are the PascalCase JSON keys.</summary>
public sealed record BalanceConstants
{
    public required string Version { get; init; }

    public required long LivingCostPln { get; init; }

    public required Fixed EasyUpkeepDiscount { get; init; }

    /// <summary>Weekly upkeep per slot, indexed by tier.</summary>
    public required ImmutableArray<long> EquipmentUpkeepPerTierPln { get; init; }

    /// <summary>Price to reach tier i, indexed by tier.</summary>
    public required ImmutableArray<long> EquipmentPricePln { get; init; }

    public required Fixed StreamEnergyCost { get; init; }

    public required Fixed StreamBurnoutGain { get; init; }

    public required Fixed RestEnergy { get; init; }

    public required Fixed RestBurnout { get; init; }

    public required Fixed NoActionEnergy { get; init; }

    public required Fixed TrainEnergyCost { get; init; }

    public required int TrainSkillMin { get; init; }

    public required int TrainSkillMax { get; init; }

    public required Fixed ManageCommunityCredibility { get; init; }

    public required Fixed ManageCommunityDramaRelief { get; init; }

    public required Fixed ManageCommunityBoredomRelief { get; init; }

    public required Fixed QualityBase { get; init; }

    public required Fixed QualitySkill { get; init; }

    public required Fixed QualityEquipment { get; init; }

    public required Fixed QualityBurnout { get; init; }

    public required Fixed QualityEnergy { get; init; }

    public required Fixed ViewersBase { get; init; }

    public required Fixed ViewersSkill { get; init; }

    public required Fixed ViewersSqrtFollowers { get; init; }

    public required Fixed ViewersLinearFollowers { get; init; }

    public required Fixed ViewersMin { get; init; }

    public required Fixed PlatformReach { get; init; }

    public required Fixed PlatformSubYield { get; init; }

    public required Fixed PlatformAdYield { get; init; }

    public required Fixed NoiseMin { get; init; }

    public required Fixed NoiseMax { get; init; }

    public required Fixed BoredomDivisor { get; init; }

    public required Fixed ViralCapMultiplier { get; init; }

    public required Fixed ConversionRate { get; init; }

    public required Fixed ChurnRate { get; init; }

    public required Fixed ChurnBoredomFactor { get; init; }

    public required Fixed PaidConversion { get; init; }

    public required Fixed SubPricePln { get; init; }

    public required Fixed DonationYield { get; init; }

    public required Fixed AdYield { get; init; }

    public required Fixed AdLoad { get; init; }

    public required Fixed SkillDecayPerWeek { get; init; }

    public required int SkillDecayAfterWeeks { get; init; }

    public required int BoredomGainMin { get; init; }

    public required int BoredomGainMax { get; init; }

    public required int BoredomReliefMin { get; init; }

    public required int BoredomReliefMax { get; init; }

    public required long InsolvencyThresholdPln { get; init; }

    public required long ScoreFollowersCap { get; init; }

    public required long ScoreCashCap { get; init; }

    public required ScoreWeights ScoreWeights { get; init; }

    public required int AchievementPointsCap { get; init; }

    public required ImmutableDictionary<Difficulty, Fixed> DifficultyMultiplier { get; init; }

    public required Fixed EasyChanceBonus { get; init; }

    public required int EventsPerWeekMin { get; init; }

    public required int EventsPerWeekMax { get; init; }

    /// <summary>Crisis event forced when burnout peaks (schema 2); null when content has none.</summary>
    public string? RecoveryCrisisEventId { get; init; }

    /// <summary>Weekly chance of a random milestone or crisis decision (schema 2).</summary>
    public Fixed DecisionChancePerWeek { get; init; }

    /// <summary>The networking action's collaboration choice (schema 2); null when content has none.</summary>
    public ChoiceDefinition? NetworkCollaborate { get; init; }

    public Fixed NetworkEnergyCost { get; init; }

    /// <summary>Share of the regular stream's energy cost and burnout gain in a short stream (M3 week off); default 1.</summary>
    public Fixed ShortStreamFatigue { get; init; } = Fixed.One;

    /// <summary>Audience multiplier of a short stream (M3 week off); default 1.</summary>
    public Fixed ShortStreamAudience { get; init; } = Fixed.One;

    /// <summary>Per-category balance profiles (M3); null in M1, where every category is neutral.</summary>
    public CategoryValues<CategoryProfile>? Categories { get; init; }

    /// <summary>Skill from which a category counts as mastered; M1 default 101 is unreachable.</summary>
    public Fixed MasteryFromSkill { get; init; } = Fixed.FromInt(101);

    /// <summary>Burnout above which odds lose points; M1 default 101 is unreachable.</summary>
    public Fixed BurnoutPenaltyFrom { get; init; } = Fixed.FromInt(101);

    /// <summary>Chance lost per burnout point above <see cref="BurnoutPenaltyFrom"/>.</summary>
    public Fixed BurnoutPenaltyPerPoint { get; init; }

    /// <summary>Largest chance the burnout penalty can remove.</summary>
    public Fixed BurnoutPenaltyCap { get; init; }

    /// <summary>Boredom gain a master saves per stream.</summary>
    public int MasteryBoredomRelief { get; init; }

    /// <summary>Multiplier of the churn rate for a master.</summary>
    public Fixed MasteryChurnFactor { get; init; } = Fixed.One;

    /// <summary>Quality above which each point adds viral chance; all viral values are zero in M1, which never draws.</summary>
    public Fixed ViralQualityFrom { get; init; }

    /// <summary>Viral chance per quality point above <see cref="ViralQualityFrom"/>.</summary>
    public Fixed ViralPerQualityPoint { get; init; }

    /// <summary>Viral chance added by each critical outcome of the week.</summary>
    public Fixed ViralPerCritical { get; init; }

    /// <summary>Largest viral chance of a stream.</summary>
    public Fixed ViralChanceCap { get; init; }

    public Fixed ViralMultiplierMin { get; init; }

    public Fixed ViralMultiplierMax { get; init; }

    /// <summary>Share of the follower count a viral clip can add (bounded by <see cref="ViralViewerShare"/> of the live viewers).</summary>
    public Fixed ViralFollowerShare { get; init; }

    public Fixed ViralViewerShare { get; init; }

    /// <summary>Follower bonus of a viral clip for a small career.</summary>
    public int ViralFollowerMin { get; init; }

    /// <summary>M3-B platform factors; null when the content has no platforms, and the scalar platform values apply.</summary>
    public ImmutableDictionary<Platform, PlatformProfile>? Platforms { get; init; }

    /// <summary>M3-B platform migration; null when content has none.</summary>
    public MigrationBalance? Migration { get; init; }

    /// <summary>M3-B policy strikes and bans; null when content has none, and no strike is ever rolled.</summary>
    public PolicyBalance? Policy { get; init; }

    /// <summary>M3-B sponsor offers and the pitch action; null when content has none.</summary>
    public SponsorBalance? Sponsors { get; init; }

    /// <summary>M3-B wagers; null when content has none.</summary>
    public WagerBalance? Wagers { get; init; }

    /// <summary>M3-B side business; null when content has none.</summary>
    public SideBusinessBalance? SideBusiness { get; init; }

    /// <summary>M3-B texts of the platform-ban decision; null when content has none.</summary>
    public PlatformBanText? PlatformBan { get; init; }

    /// <summary>M3-C achievements; null keeps the hard-coded M1 set.</summary>
    public ImmutableArray<AchievementDefinition>? Achievements { get; init; }

    /// <summary>The profile of <paramref name="category"/>; neutral when no profiles are configured.</summary>
    public CategoryProfile Profile(Category category) => Categories is { } all ? all[category] : CategoryProfile.Neutral(this);
}
