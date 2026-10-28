using System.Text.Json;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>
/// A published event or sponsor template version never changes: the same (id, version) must have the same JSON
/// everywhere. Events are keyed <c>{id}@{version}</c> and sponsor templates <c>sponsor:{id}@{version}</c>.
/// </summary>
public static class EventVersions
{
    public static IReadOnlyList<string> Conflicts(byte[] candidateEvents, IEnumerable<byte[]> storedEvents)
    {
        using var candidate = JsonDocument.Parse(candidateEvents);
        var byKey = Versioned(candidate.RootElement).ToDictionary(x => x.Key, x => x.Json, StringComparer.Ordinal);
        var conflicts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var stored in storedEvents)
        {
            using var document = JsonDocument.Parse(stored);
            foreach (var (key, old) in Versioned(document.RootElement))
            {
                if (byKey.TryGetValue(key, out var current) && !JsonElement.DeepEquals(old, current))
                {
                    conflicts.Add(key);
                }
            }
        }

        return [.. conflicts];
    }

    /// <summary>The exception message both content stores raise for a non-empty conflict list.</summary>
    public static string Describe(IEnumerable<string> conflicts)
        => $"Content changes published events: {string.Join(", ", conflicts.Select(k => $"event-version-changed:{k}"))}";

    private static (string Key, JsonElement Json)[] Versioned(JsonElement root)
    {
        if (!root.TryGetProperty("schema", out var schema) || schema.GetInt32() != 2)
        {
            return [];
        }

        var events = root.GetProperty("events").EnumerateArray().Select(e => (Key(e), e));
        var sponsors = root.TryGetProperty("sponsors", out var templates) && templates.ValueKind == JsonValueKind.Array
            ? templates.EnumerateArray().Select(t => ($"sponsor:{Key(t)}", t))
            : [];
        return [.. events, .. sponsors];
    }

    private static string Key(JsonElement e) => $"{e.GetProperty("id").GetString()}@{e.GetProperty("version").GetInt32()}";
}
