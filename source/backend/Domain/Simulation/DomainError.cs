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
}

/// <summary>A rejected command; <see cref="Detail"/> is a stable machine-readable reason.</summary>
public sealed record DomainError(DomainErrorCode Code, string Detail);
