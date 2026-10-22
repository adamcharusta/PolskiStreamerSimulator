using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// M3-B: one platform's factors. <see cref="Reach"/>, <see cref="SubYield"/> and <see cref="AdYield"/> replace the scalar
/// platform values; <see cref="CategoryReach"/> multiplies a category's reach (1 for categories the balance omits).
/// </summary>
public sealed record PlatformProfile(Fixed Reach, Fixed SubYield, Fixed AdYield, Fixed PolicyRisk, CategoryValues<Fixed> CategoryReach);

/// <summary>M3-B: the cost, timing and follower retention of a move between platforms.</summary>
public sealed record MigrationBalance(
    Fixed EnergyCost,
    int MinWeeksOnPlatform,
    int AnnounceWeeks,
    Fixed AnnounceCredibility,
    Fixed AnnouncedBonus,
    Fixed BanPenalty,
    Fixed RetentionBase,
    Fixed CredibilityDivisor,
    Fixed DramaDivisor,
    Fixed RetentionMin,
    Fixed RetentionMax,
    Fixed Jitter);

/// <summary>M3-B: policy exposure, the weekly strike chance, strike lifetime, the ban threshold and drama decay.</summary>
public sealed record PolicyBalance(
    int GamblingExposure,
    int SideBusinessExposure,
    int HighDramaExposure,
    Fixed HighDramaFrom,
    Fixed ChancePerExposure,
    Fixed ChanceCap,
    int StrikeWeeks,
    int StrikesToBan,
    Fixed DramaDecay);

/// <summary>M3-B: sponsor offer chances, contract limits, the pitch action and the background's starting leads (template IDs).</summary>
public sealed record SponsorBalance(
    Fixed OfferBase,
    Fixed OfferPerMagnitude,
    Fixed OfferCap,
    int OfferWeeks,
    int MaxContracts,
    Fixed PitchEnergyCost,
    ChoiceDefinition Pitch,
    Fixed TerminationCredibility,
    int MissesToTerminate,
    ImmutableDictionary<Background, string> StartingLeads);

/// <summary>M3-B: the stake, exposure and outcome of each wager tier.</summary>
public sealed record WagerBalance(
    ImmutableDictionary<WagerTier, long> StakePln,
    ImmutableDictionary<WagerTier, int> Exposure,
    Fixed MaxShareOfCash,
    Fixed Chance,
    Fixed PayoutMultiple,
    Fixed WinAudience,
    Fixed LossAudience,
    Fixed WinDrama,
    Fixed LossDrama);

/// <summary>M3-B: when the side business opens, what work on it costs and what it earns.</summary>
public sealed record SideBusinessBalance(
    int FromWeek,
    long MinFollowers,
    Fixed EnergyCost,
    Fixed BurnoutGain,
    Fixed QualityPerWork,
    Fixed QualityDecay,
    Fixed IncomePerFollower,
    Fixed IncomeScale,
    Fixed CredibilityPerWeek);

/// <summary>M3-B: the ICU title and text of the platform-ban decision and the label of each move choice.</summary>
public sealed record PlatformBanText(string Title, string Text, ImmutableDictionary<Platform, string> Choices);
