using System.Text.Json;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Telemetry;

/// <summary>One gameplay event as the browser sends it; only allowlisted names and values are counted (spec M4-F §2).</summary>
public sealed record TelemetryEvent(string? Name, IReadOnlyDictionary<string, JsonElement>? Props = null);

/// <summary>A batch of opted-in events.</summary>
public sealed record RecordTelemetry(IReadOnlyList<TelemetryEvent> Events);

/// <summary>How many events of the batch were counted.</summary>
public sealed record TelemetryAccepted(int Accepted);

/// <summary>Persistence port: adds one to each (day, event, props) counter.</summary>
public interface ITelemetryStore
{
    Task AddAsync(DateOnly day, IReadOnlyList<(string Event, string PropsKey)> counts, CancellationToken cancellationToken);
}

/// <summary>The events and prop values that may be counted; anything else is skipped.</summary>
public static class TelemetryAllowlist
{
    public const int MaxEvents = 50;

    private static readonly string[] Booleans = ["true", "false"];

    private static readonly Dictionary<string, Dictionary<string, HashSet<string>>> Events = new(StringComparer.Ordinal)
    {
        ["career_created"] = new(StringComparer.Ordinal)
        {
            ["background"] = Names<Background>(),
            ["difficulty"] = Names<Difficulty>(),
            ["ranked"] = [.. Booleans],
        },
        ["week_settled"] = new(StringComparer.Ordinal) { ["stage"] = ["1-4", "5-13", "14-52", "53-260", "261+"] },
        ["batch_run"] = new(StringComparer.Ordinal) { ["weeks"] = ["4", "13", "52"] },
        ["career_ended"] = new(StringComparer.Ordinal)
        {
            ["ending"] = Names<EndingReason>(),
            ["ranked"] = [.. Booleans],
        },
        ["result_published"] = new(StringComparer.Ordinal),
        ["save_restored"] = new(StringComparer.Ordinal),
        ["save_imported"] = new(StringComparer.Ordinal),
    };

    /// <summary>The counter key of an allowed event, or <c>null</c> when its name, a prop or a value is not allowed.</summary>
    public static (string Event, string PropsKey)? Key(TelemetryEvent e)
    {
        if (e.Name is not { } name || !Events.TryGetValue(name, out var allowed))
        {
            return null;
        }

        var pairs = new List<string>();
        foreach (var (prop, value) in e.Props ?? new Dictionary<string, JsonElement>())
        {
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
            if (!allowed.TryGetValue(prop, out var values) || text is null || !values.Contains(text))
            {
                return null;
            }

            pairs.Add($"{prop}={text}");
        }

        pairs.Sort(StringComparer.Ordinal);
        return (name, string.Join(';', pairs));
    }

    /// <summary>camelCase names, as the API serializes these enums.</summary>
    private static HashSet<string> Names<T>()
        where T : struct, Enum
        => [.. Enum.GetNames<T>().Select(JsonNamingPolicy.CamelCase.ConvertName)];
}

public sealed class RecordTelemetryHandler(ITelemetryStore store, TimeProvider time)
{
    public async Task<TelemetryAccepted> Handle(RecordTelemetry command, CancellationToken cancellationToken)
    {
        var counts = command.Events
            .Take(TelemetryAllowlist.MaxEvents)
            .Select(TelemetryAllowlist.Key)
            .Where(k => k is not null)
            .Select(k => k!.Value)
            .ToList();
        if (counts.Count > 0)
        {
            await store.AddAsync(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), counts, cancellationToken);
        }

        return new TelemetryAccepted(counts.Count);
    }
}
