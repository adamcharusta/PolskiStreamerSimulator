using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Simulation;

public enum RollOutcome
{
    Success,
    Failure,
    CriticalSuccess,
    CriticalFailure,

    /// <summary>A choice without randomness; its success effects apply and no roll is drawn.</summary>
    Guaranteed,
}

/// <summary>
/// Recorded resolution of one live event: the 1–100 roll (absent when guaranteed), the threshold and
/// modifiers shown before the choice, and the applied outcome, so the explanation reproduces the result.
/// </summary>
public sealed record RollResult(
    string EventId,
    string ChoiceId,
    int? Roll,
    int Threshold,
    ImmutableArray<ModifierLine> Modifiers,
    RollOutcome Outcome);
