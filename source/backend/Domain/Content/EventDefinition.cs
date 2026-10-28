using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>Stats that event modifiers, eligibility and effects can read or change.</summary>
public enum StatKey
{
    Drama,
    Credibility,
    Energy,
    Burnout,

    /// <summary>Skill in the week's primary category.</summary>
    PrimarySkill,

    /// <summary>Boredom in the week's primary category.</summary>
    PrimaryBoredom,

    /// <summary>Follower count; read-only in eligibility and modifiers, never the target of a stat delta.</summary>
    Followers,
}

/// <summary>When an event is shown: inside a stream (live) or as a decision between weeks (milestone, crisis).</summary>
public enum EventPhase
{
    Live,
    Milestone,
    Crisis,
}

public enum EffectKind
{
    /// <summary>Adds <see cref="Effect.Amount"/> to <see cref="Effect.Stat"/>, clamped to 0–100.</summary>
    StatDelta,

    /// <summary>Adds a whole-PLN amount to the week's event cash.</summary>
    CashDelta,

    /// <summary>Multiplies the week's live-audience multiplier.</summary>
    AudienceMultiplier,

    /// <summary>Adds to the week's stream quality.</summary>
    EventQuality,

    /// <summary>Changes followers by <see cref="Effect.Amount"/> percent (schema 2).</summary>
    FollowersPercent,

    /// <summary>Adds <see cref="Effect.Amount"/> to the skill of <see cref="Effect.Category"/> (schema 2).</summary>
    SkillDelta,

    /// <summary>Adds <see cref="Effect.Amount"/> percentage points to the week's viral chance (schema 2).</summary>
    ViralChance,

    /// <summary>Sets flag <see cref="Effect.Target"/>, for <see cref="Effect.Weeks"/> weeks or permanently (schema 2).</summary>
    SetFlag,

    /// <summary>Clears flag <see cref="Effect.Target"/> (schema 2).</summary>
    ClearFlag,

    /// <summary>Schedules event <see cref="Effect.Target"/> in <see cref="Effect.Weeks"/> weeks (schema 2).</summary>
    FollowUp,

    /// <summary>Gives <see cref="Effect.Amount"/> (1–2) policy strikes on the current platform (M3-B).</summary>
    PolicyStrike,

    /// <summary>Bans the career on every platform, which ends it; amount 0 (M3-B).</summary>
    GlobalBan,

    /// <summary>Opens an offer from sponsor template <see cref="Effect.Target"/>; amount 0 (M3-B).</summary>
    SponsorOffer,
}

/// <summary>
/// One change applied by an outcome. <see cref="Category"/>, <see cref="Target"/> (a flag, event or sponsor template ID) and
/// <see cref="Weeks"/> are used only by the schema-2 kinds.
/// </summary>
public sealed record Effect(EffectKind Kind, StatKey? Stat, Fixed Amount, Category? Category = null, string? Target = null, int? Weeks = null);

/// <summary>Minimum resources a choice needs to be offered (schema 2).</summary>
public sealed record ChoiceRequirement(long? MinCashPln, Fixed? MinEnergy);

/// <summary>Adds <c>min(Cap, stat × PerPoint)</c> to a choice's probability.</summary>
public sealed record ChanceModifier(StatKey Stat, Fixed PerPoint, Fixed? Cap, string LabelKey);

public sealed record OutcomeSet(
    ImmutableArray<Effect> Success,
    ImmutableArray<Effect> Failure,
    ImmutableArray<Effect>? CriticalSuccess,
    ImmutableArray<Effect>? CriticalFailure);

public sealed record ChoiceDefinition(
    string Id,
    string? LabelKey,
    string LabelPl,
    bool Guaranteed,
    Fixed BaseChance,
    ImmutableArray<ChanceModifier> Modifiers,
    bool AllowsCritical,
    OutcomeSet Outcomes,
    ChoiceRequirement? Requires = null,
    string? SuccessText = null,
    string? FailureText = null);

/// <summary>
/// Weeks and inclusive stat bounds in which an event may be selected; in schema 2 also the categories (empty means
/// every category) and the flags that must be set or must not be set; in M3-B also the platforms (empty means every
/// platform) and the minimum number of active policy strikes on the current platform.
/// </summary>
public sealed record Eligibility(
    int FromWeek,
    int ToWeek,
    ImmutableDictionary<StatKey, Fixed> Min,
    ImmutableDictionary<StatKey, Fixed> Max,
    ImmutableArray<Category> Categories = default,
    ImmutableArray<string> RequiresFlags = default,
    ImmutableArray<string> ForbidsFlags = default,
    ImmutableArray<Platform> Platforms = default,
    int MinStrikes = 0)
{
    /// <summary>Allowed categories; empty (never default) means every category.</summary>
    public ImmutableArray<Category> Categories { get; init; } = Categories.IsDefault ? [] : Categories;

    public ImmutableArray<string> RequiresFlags { get; init; } = RequiresFlags.IsDefault ? [] : RequiresFlags;

    public ImmutableArray<string> ForbidsFlags { get; init; } = ForbidsFlags.IsDefault ? [] : ForbidsFlags;

    /// <summary>Platforms on which the event may be shown; empty (never default) means every platform.</summary>
    public ImmutableArray<Platform> Platforms { get; init; } = Platforms.IsDefault ? [] : Platforms;
}

/// <summary>
/// An event. Schema 1 uses <see cref="TitleKey"/> and plain <see cref="TextPl"/>; schema 2 uses <see cref="Title"/> and
/// ICU text in <see cref="TextPl"/>, plus <see cref="Version"/>, <see cref="Phase"/>, <see cref="OneTime"/> and <see cref="Tags"/>.
/// </summary>
public sealed record EventDefinition(
    string Id,
    string? TitleKey,
    string TextPl,
    int Weight,
    int CooldownWeeks,
    Eligibility Eligibility,
    ImmutableArray<ChoiceDefinition> Choices,
    int Version = 1,
    EventPhase Phase = EventPhase.Live,
    string? Title = null,
    bool OneTime = false,
    ImmutableArray<string> Tags = default)
{
    /// <summary>Content tags; empty (never default) when the event has none.</summary>
    public ImmutableArray<string> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}
