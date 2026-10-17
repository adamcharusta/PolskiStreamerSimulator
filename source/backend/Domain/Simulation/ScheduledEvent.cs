namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>A follow-up event that becomes due in <see cref="DueWeek"/> (a live slot or a decision before that week).</summary>
public sealed record ScheduledEvent(string EventId, int DueWeek);
