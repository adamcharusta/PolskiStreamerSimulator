using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>Between-stream main actions (network collaboration is M3 only); exactly one per week.</summary>
public enum MainAction
{
    Rest,
    TrainCategory,
    ImproveProduction,
    ManageCommunity,
    NoAction,
    NetworkCollaborate,
}

/// <summary>
/// The week's commitment: one main action and the stream's primary category. <see cref="TrainTarget"/>
/// defaults to <see cref="Category"/>; <see cref="UpgradeSlot"/> is required for <see cref="MainAction.ImproveProduction"/>.
/// </summary>
public sealed record WeekPlan(MainAction Action, Category Category, Category? TrainTarget = null, EquipmentSlot? UpgradeSlot = null);
