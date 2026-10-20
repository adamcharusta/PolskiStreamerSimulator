using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>One visible contribution to a choice's probability, such as <c>modifier.credibility</c>.</summary>
public sealed record ModifierLine(string LabelKey, Fixed Value);

/// <summary>
/// Odds shown before a choice: success when the 1–100 roll is at most <see cref="Threshold"/>.
/// A guaranteed choice has probability 1 and threshold 100 and is never rolled. <see cref="UnavailableReason"/>
/// (<c>needs-cash</c>, <c>needs-energy</c>) is set when the career does not meet the choice's requirement.
/// </summary>
public sealed record ChoiceOdds(
    string ChoiceId,
    bool Guaranteed,
    Fixed Probability,
    int Threshold,
    ImmutableArray<ModifierLine> Modifiers,
    bool AllowsCritical,
    Fixed ViralOnCritical = default,
    string? UnavailableReason = null);

/// <summary>The next unresolved live event of the pending week with the odds of each choice.</summary>
public sealed record EventPresentation(string EventId, ImmutableArray<ChoiceOdds> Choices);
