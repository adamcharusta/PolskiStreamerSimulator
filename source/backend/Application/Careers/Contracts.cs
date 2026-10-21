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

    /// <summary>A mandatory decision between weeks must be answered before the next week can be planned.</summary>
    ResolveDecision,
}

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>A career as the client sees it at one version (and the ruleset it plays for life); also the stored response of each step.</summary>
public sealed record CareerView(
    Guid CareerId,
    string Nickname,
    string RulesetId,
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

/// <summary><see cref="Plans"/> is filled for <see cref="NextStepKind.PlanWeek"/>, <see cref="Event"/> for <see cref="NextStepKind.ResolveEvent"/> and <see cref="NextStepKind.ResolveDecision"/>.</summary>
public sealed record NextStepView(NextStepKind Kind, IReadOnlyList<PlanOptionView> Plans, EventView? Event);

public sealed record PlanView(MainAction MainAction, Category Category, Category? TrainTarget, EquipmentSlot? UpgradeSlot);

/// <summary>A plan with the reason it is disabled and, for a rolled action (networking), the odds of its roll.</summary>
public sealed record PlanOptionView(PlanView Plan, string? DisabledReason, ChoiceView? Odds);

/// <summary>An event to answer: M1 gives a title key and plain text, M3 an ICU title and text that the client formats; mandatory marks a decision between weeks.</summary>
public sealed record EventView(string EventId, string? TitleKey, string? Title, string Text, IReadOnlyList<ChoiceView> Choices, bool Mandatory);

/// <summary>A response with its odds, the viral chance a critical outcome adds and why the career cannot take it (needs-cash, needs-energy), if so.</summary>
public sealed record ChoiceView(
    string ChoiceId,
    string? LabelKey,
    string Label,
    bool Guaranteed,
    decimal Probability,
    int Threshold,
    IReadOnlyList<ModifierView> Modifiers,
    bool AllowsCritical,
    decimal ViralOnCritical,
    string? UnavailableReason);

public sealed record ModifierView(string LabelKey, decimal Value);

public sealed record RollView(string EventId, string ChoiceId, int? Roll, int Threshold, IReadOnlyList<ModifierView> Modifiers, RollOutcome Outcome);

public sealed record ReportLineView(string Code, long? AmountPln, decimal? Delta, string? Detail);

/// <summary>A viral clip: the 1–100 roll against the threshold, the viewer multiplier and the follower bonus.</summary>
public sealed record ViralView(int Roll, int Threshold, decimal Multiplier, long FollowersBonus);

/// <summary>A settled week with the rolls of the decisions answered before it, the roll of a rolled action (networking) and its viral clip.</summary>
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
    EndingReason? Ending,
    IReadOnlyList<RollView> DecisionRolls,
    RollView? ActionRoll,
    ViralView? Viral);
