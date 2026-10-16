using System.Text.Json;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>A published event version never changes: the same (id, version) must have the same JSON everywhere.</summary>
public static class EventVersions
{
    public static IReadOnlyList<string> Conflicts(byte[] candidateEvents, IEnumerable<byte[]> storedEvents)
    {
        using var candidate = JsonDocument.Parse(candidateEvents);
        var byKey = Versioned(candidate.RootElement).ToDictionary(Key, e => e, StringComparer.Ordinal);
        var conflicts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var stored in storedEvents)
        {
            using var document = JsonDocument.Parse(stored);
            foreach (var old in Versioned(document.RootElement))
            {
                if (byKey.TryGetValue(Key(old), out var current) && !JsonElement.DeepEquals(old, current))
                {
                    conflicts.Add(Key(old));
                }
            }
        }

        return [.. conflicts];
    }

    /// <summary>The exception message both content stores raise for a non-empty conflict list.</summary>
    public static string Describe(IEnumerable<string> conflicts)
        => $"Content changes published events: {string.Join(", ", conflicts.Select(k => $"event-version-changed:{k}"))}";

    private static IEnumerable<JsonElement> Versioned(JsonElement root)
        => root.TryGetProperty("schema", out var schema) && schema.GetInt32() == 2
            ? root.GetProperty("events").EnumerateArray()
            : [];

    private static string Key(JsonElement e) => $"{e.GetProperty("id").GetString()}@{e.GetProperty("version").GetInt32()}";
}
