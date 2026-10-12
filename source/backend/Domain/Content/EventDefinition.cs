using System.Collections.Immutable;
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
}

public sealed record Effect(EffectKind Kind, StatKey? Stat, Fixed Amount);

/// <summary>Adds <c>min(Cap, stat × PerPoint)</c> to a choice's probability.</summary>
public sealed record ChanceModifier(StatKey Stat, Fixed PerPoint, Fixed? Cap, string LabelKey);

public sealed record OutcomeSet(
    ImmutableArray<Effect> Success,
    ImmutableArray<Effect> Failure,
    ImmutableArray<Effect>? CriticalSuccess,
    ImmutableArray<Effect>? CriticalFailure);

public sealed record ChoiceDefinition(
    string Id,
    string LabelKey,
    string LabelPl,
    bool Guaranteed,
    Fixed BaseChance,
    ImmutableArray<ChanceModifier> Modifiers,
    bool AllowsCritical,
    OutcomeSet Outcomes);

/// <summary>Weeks and inclusive stat bounds in which an event may be selected.</summary>
public sealed record Eligibility(
    int FromWeek,
    int ToWeek,
    ImmutableDictionary<StatKey, Fixed> Min,
    ImmutableDictionary<StatKey, Fixed> Max);

public sealed record EventDefinition(
    string Id,
    string TitleKey,
    string TextPl,
    int Weight,
    int CooldownWeeks,
    Eligibility Eligibility,
    ImmutableArray<ChoiceDefinition> Choices);
