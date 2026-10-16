namespace PolskiStreamerSimulator.Domain.Simulation;

public enum DomainErrorCode
{
    IllegalAction,
    StaleVersion,
    WeekAlreadySettled,
    NoPendingWeek,
    EventsUnresolved,
    CareerEnded,
    UnknownChoice,
    UnknownRuleset,
    ContentMismatch,

    /// <summary>A decision command arrived while no mandatory decision is queued.</summary>
    NoPendingDecision,

    /// <summary>A week cannot start while mandatory decisions are queued.</summary>
    DecisionPending,
}

/// <summary>A rejected command; <see cref="Detail"/> is a stable machine-readable reason.</summary>
public sealed record DomainError(DomainErrorCode Code, string Detail);
