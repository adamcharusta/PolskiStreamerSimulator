using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>What the player can do next.</summary>
public enum NextStepKind
{
    PlanWeek,
    ResolveEvent,
    SettleWeek,
    Ended,
}

/// <summary>A career as the client sees it at one version; also the stored response of each step.</summary>
public sealed record CareerView(
    Guid CareerId,
    string Nickname,
    long Version,
    CareerStatus Status,
    EndingReason? Ending,
    Background Background,
    Difficulty Difficulty,
    PronounSet Pronouns,
    int Week,
    int LastSettledWeek,
    CareerStatsView Stats,
    NextStepView Next,
    WeekReportView? LastReport);

public sealed record CareerStatsView(
    long CashPln,
    long Followers,
    long LastLiveViewers,
    decimal Drama,
    decimal Credibility,
    decimal Energy,
    decimal Burnout,
    CategoryValuesView Skill,
    CategoryValuesView Boredom,
    EquipmentView Equipment);

public sealed record CategoryValuesView(decimal JustChatting, decimal Fps, decimal Moba, decimal Chess, decimal Gambling);

public sealed record EquipmentView(int Microphone, int Camera, int Computer, int Connection);

/// <summary><see cref="Plans"/> is filled for <see cref="NextStepKind.PlanWeek"/>, <see cref="Event"/> for <see cref="NextStepKind.ResolveEvent"/>.</summary>
public sealed record NextStepView(NextStepKind Kind, IReadOnlyList<PlanOptionView> Plans, EventView? Event);

public sealed record PlanView(MainAction MainAction, Category Category, Category? TrainTarget, EquipmentSlot? UpgradeSlot);

public sealed record PlanOptionView(PlanView Plan, string? DisabledReason);

public sealed record EventView(string EventId, string TitleKey, string TextPl, IReadOnlyList<ChoiceView> Choices);

public sealed record ChoiceView(
    string ChoiceId,
    string LabelKey,
    string LabelPl,
    bool Guaranteed,
    decimal Probability,
    int Threshold,
    IReadOnlyList<ModifierView> Modifiers,
    bool AllowsCritical);

public sealed record ModifierView(string LabelKey, decimal Value);

public sealed record RollView(string EventId, string ChoiceId, int? Roll, int Threshold, IReadOnlyList<ModifierView> Modifiers, RollOutcome Outcome);

public sealed record ReportLineView(string Code, long? AmountPln, decimal? Delta, string? Detail);

public sealed record WeekReportView(
    int Week,
    PlanView Plan,
    IReadOnlyList<RollView> Rolls,
    long LiveViewers,
    long FollowersGained,
    long FollowersLost,
    long FollowersNet,
    IReadOnlyList<ReportLineView> Income,
    IReadOnlyList<ReportLineView> Costs,
    long CashDelta,
    IReadOnlyList<ReportLineView> StatChanges,
    EndingReason? Ending);
