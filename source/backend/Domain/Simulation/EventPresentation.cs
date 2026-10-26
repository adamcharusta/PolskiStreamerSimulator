using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>One visible contribution to a choice's probability, such as <c>modifier.credibility</c>.</summary>
public sealed record ModifierLine(string LabelKey, Fixed Value);

/// <summary>
/// Odds shown before a choice: success when the 1–100 roll is at most <see cref="Threshold"/>.
/// A guaranteed choice has probability 1 and threshold 100 and is never rolled. <see cref="UnavailableReason"/>
/// (<c>needs-cash</c>, <c>needs-energy</c>) is set when the career does not meet the choice's requirement;
/// <see cref="Migration"/> (M3-B) is the estimate of a platform move the choice makes.
/// </summary>
public sealed record ChoiceOdds(
    string ChoiceId,
    bool Guaranteed,
    Fixed Probability,
    int Threshold,
    ImmutableArray<ModifierLine> Modifiers,
    bool AllowsCritical,
    Fixed ViralOnCritical = default,
    string? UnavailableReason = null,
    MigrationEstimate? Migration = null);

/// <summary>
/// The next unresolved live event of the pending week (or the next decision) with the odds of each choice;
/// <see cref="Definition"/> is set for a ruleset-built decision that is not in the content (M3-B <c>platform-ban</c>).
/// </summary>
public sealed record EventPresentation(string EventId, ImmutableArray<ChoiceOdds> Choices, EventDefinition? Definition = null);
