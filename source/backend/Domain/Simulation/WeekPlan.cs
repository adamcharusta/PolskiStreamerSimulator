using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>Between-stream main actions, one per week; networking is M3 only, the last three need M3-B balance sections.</summary>
/// <remarks>
/// The summary stays on one line: the OpenAPI generator keeps an enum summary's line breaks, which differ between
/// Windows and Linux builds.
/// </remarks>
public enum MainAction
{
    Rest,
    TrainCategory,
    ImproveProduction,
    ManageCommunity,
    NoAction,
    NetworkCollaborate,
    PitchFulfillSponsor,
    SideBusinessWork,
    PlanPlatformMigration,
}

/// <summary>
/// The week's commitment: one main action and the stream's primary category. <see cref="TrainTarget"/>
/// defaults to <see cref="Category"/>; <see cref="UpgradeSlot"/> is required for <see cref="MainAction.ImproveProduction"/>.
/// M3-B adds the optional <see cref="Wager"/> stake (Gambling plans only) and the <see cref="TargetPlatform"/> that
/// <see cref="MainAction.PlanPlatformMigration"/> requires.
/// </summary>
public sealed record WeekPlan(
    MainAction Action,
    Category Category,
    Category? TrainTarget = null,
    EquipmentSlot? UpgradeSlot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WagerTier? Wager = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Platform? TargetPlatform = null);
