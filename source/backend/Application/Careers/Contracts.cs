using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Scoring;
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
    WeekReportView? LastReport,
    Platform Platform,
    int PlatformSinceWeek,
    IReadOnlyList<StrikeView> Strikes,
    IReadOnlyList<Platform> BannedPlatforms,
    IReadOnlyList<OfferView> Offers,
    IReadOnlyList<ContractView> Contracts,
    SideBusinessView? SideBusiness,
    IReadOnlyList<AchievementView> Achievements,
    ScoreView? Score,
    EndingSummaryView? Summary);

/// <summary>An achievement of the career's content with its Polish ICU name and description, its points and whether it is earned (earned first).</summary>
public sealed record AchievementView(string Id, string? Name, string? Description, int Points, bool Earned);

/// <summary>The final score: the normalised components, their points, the base score, both multipliers and the total.</summary>
public sealed record ScoreView(
    decimal Followers,
    decimal Wealth,
    decimal Achievements,
    long FollowersPoints,
    long WealthPoints,
    long AchievementPoints,
    long BaseScore,
    decimal DifficultyMultiplier,
    decimal BackgroundMultiplier,
    long FinalScore);

/// <summary>What the ending narrative is built from: reason, weeks, dominant category, reputation, platforms, peak follower milestone and earned achievements.</summary>
public sealed record EndingSummaryView(
    EndingReason Reason,
    int WeeksPlayed,
    Category DominantCategory,
    Reputation Reputation,
    Platform Platform,
    IReadOnlyList<Platform> BannedPlatforms,
    int PeakFollowersMilestone,
    IReadOnlyList<string> Achievements);

/// <summary>An active policy strike on a platform and the last settled week in which it still counts.</summary>
public sealed record StrikeView(Platform Platform, int GivenWeek, int ExpiresAfterWeek);

/// <summary>A sponsor offer with every term shown before acceptance and why it cannot be accepted now, if so.</summary>
public sealed record OfferView(
    string OfferId,
    string TemplateId,
    string Name,
    string Pitch,
    SponsorKind Kind,
    long PaymentPln,
    int? TermWeeks,
    int? DueEveryWeeks,
    Category? RequiredCategory,
    IReadOnlyList<Platform> Platforms,
    SponsorPrerequisitesView Prerequisites,
    IReadOnlyList<string> IncompatibleWith,
    long BreachCashPln,
    decimal BreachCredibility,
    int ExpiresAfterWeek,
    string? AcceptBlockedReason);

public sealed record SponsorPrerequisitesView(long MinFollowers, decimal? MinCredibility, decimal? MaxDrama, decimal? MinDrama);

/// <summary>An accepted sponsor deal: its next due week, the term end, the misses so far and whether it is due this week.</summary>
public sealed record ContractView(
    string ContractId,
    string TemplateId,
    string Name,
    SponsorKind Kind,
    long PaymentPln,
    Category? RequiredCategory,
    IReadOnlyList<Platform> Platforms,
    int NextDueWeek,
    int TermEndWeek,
    int Missed,
    long BreachCashPln,
    decimal BreachCredibility,
    bool DueThisWeek);

/// <summary>The text-only side business: when it opened, its quality and the income it pays at the next settlement.</summary>
public sealed record SideBusinessView(int OpenedWeek, decimal Quality, long WeeklyIncomePln);

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

public sealed record PlanView(MainAction MainAction, Category Category, Category? TrainTarget, EquipmentSlot? UpgradeSlot, WagerTier? Wager, Platform? TargetPlatform);

/// <summary>A plan with the reason it is disabled, the odds of a rolled action and what it risks: wager terms, policy exposure, a platform move and sponsor consequences.</summary>
public sealed record PlanOptionView(
    PlanView Plan,
    string? DisabledReason,
    ChoiceView? Odds,
    WagerTermsView? Wager,
    PolicyExposureView? Policy,
    MigrationEstimateView? Migration,
    IReadOnlyList<SponsorConsequenceView> SponsorConsequences);

public sealed record WagerTermsView(WagerTier Tier, long StakePln, decimal Chance, long PayoutPln, long LossPln);

/// <summary>The week's policy exposure, the strike chance and the 1–100 threshold of the strike roll.</summary>
public sealed record PolicyExposureView(int Exposure, decimal Chance, int Threshold);

/// <summary>A platform move: the retention and follower range, whether this use executes it, and the target platform's factors.</summary>
public sealed record MigrationEstimateView(
    Platform From,
    Platform To,
    bool Executes,
    decimal RetentionMin,
    decimal RetentionMax,
    long FollowersMin,
    long FollowersMax,
    PlatformProfileView Target);

public sealed record PlatformProfileView(decimal Reach, decimal SubYield, decimal AdYield, decimal PolicyRisk);

/// <summary>A missed installment or breach the plan would cause: missedInstallment or breach, the penalty, the credibility change and whether the deal ends.</summary>
public sealed record SponsorConsequenceView(string ContractId, string TemplateId, string Kind, long PenaltyPln, decimal Credibility, bool Terminates);

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
    string? UnavailableReason,
    MigrationEstimateView? Migration);

public sealed record ModifierView(string LabelKey, decimal Value);

public sealed record RollView(string EventId, string ChoiceId, int? Roll, int Threshold, IReadOnlyList<ModifierView> Modifiers, RollOutcome Outcome);

public sealed record ReportLineView(string Code, long? AmountPln, decimal? Delta, string? Detail);

/// <summary>A viral clip: the 1–100 roll against the threshold, the viewer multiplier and the follower bonus.</summary>
public sealed record ViralView(int Roll, int Threshold, decimal Multiplier, long FollowersBonus);

/// <summary>A settled week with the decision rolls, the action roll, its viral clip, the strike roll, the wager and a platform move.</summary>
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
    ViralView? Viral,
    StrikeRollView? Strike,
    WagerResultView? Wager,
    MigrationView? Migration);

/// <summary>The week's policy exposure and strike roll (no roll without exposure), and whether it banned the platform.</summary>
public sealed record StrikeRollView(Platform Platform, int Exposure, int Threshold, int? Roll, bool Strike, bool Banned);

/// <summary>The week's wager: the stake, the 1–100 roll against the win threshold and the net cash.</summary>
public sealed record WagerResultView(WagerTier Tier, long StakePln, int Roll, int Threshold, bool Won, long NetPln);

/// <summary>A completed platform move with the retention and the followers before and after.</summary>
public sealed record MigrationView(Platform From, Platform To, decimal Retention, long FollowersBefore, long FollowersAfter);
