using System.Collections.Concurrent;
using PolskiStreamerSimulator.Application.Telemetry;

namespace PolskiStreamerSimulator.Tests.Support;

public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    private readonly ConcurrentDictionary<(DateOnly, string, string), long> counts = new();

    public Task AddAsync(DateOnly day, IReadOnlyList<(string Event, string PropsKey)> events, CancellationToken cancellationToken)
    {
        foreach (var (name, props) in events)
        {
            counts.AddOrUpdate((day, name, props), 1, (_, n) => n + 1);
        }

        return Task.CompletedTask;
    }

    public long Count(DateOnly day, string name, string props) => counts.GetValueOrDefault((day, name, props));
}
