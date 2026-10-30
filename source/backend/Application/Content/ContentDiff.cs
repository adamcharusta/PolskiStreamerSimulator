using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PolskiStreamerSimulator.Application.Content;

public enum ContentChange
{
    Added,
    Changed,
    Removed,
}

// Summaries of contract records stay on one line: the OpenAPI document copies them, and line breaks differ by OS.

/// <summary>An event or sponsor template that differs from the newest publication; mechanical unless only its texts or version changed.</summary>
public sealed record ItemDiffView(string Section, string Id, ContentChange Change, bool Mechanical, int? VersionBefore, int? VersionAfter);

/// <summary>A balance or events-file value that differs, by JSON path, with both values as JSON text.</summary>
public sealed record ValueDiffView(string Path, string? Before, string? After, bool Mechanical);

/// <summary>What a draft changes: items, values, and whether it changes mechanics (a new season), only texts, or nothing.</summary>
public sealed record ContentDiffView(IReadOnlyList<ItemDiffView> Items, IReadOnlyList<ValueDiffView> Values, bool Mechanical, bool Unchanged);

/// <summary>Compares two versions of the content files (spec M4-E §5).</summary>
public static class ContentDiff
{
    private static readonly (string Section, string Array)[] Sections = [("event", "events"), ("sponsor", "sponsors")];

    public static ContentDiffView Between(ContentFiles before, ContentFiles after)
    {
        var oldBalance = JsonNode.Parse(before.Balance);
        var newBalance = JsonNode.Parse(after.Balance);
        var oldEvents = JsonNode.Parse(before.Events) as JsonObject ?? [];
        var newEvents = JsonNode.Parse(after.Events) as JsonObject ?? [];

        var items = new List<ItemDiffView>();
        foreach (var (section, array) in Sections)
        {
            items.AddRange(Items(section, oldEvents[array] as JsonArray, newEvents[array] as JsonArray));
        }

        var values = new List<ValueDiffView>();
        Values("", oldBalance, newBalance, isBalance: true, values);
        var oldRest = WithoutSections(oldEvents);
        var newRest = WithoutSections(newEvents);
        Values("events", oldRest, newRest, isBalance: false, values);

        var unchanged = ContentMechanics.Canonical(oldBalance) == ContentMechanics.Canonical(newBalance)
            && ContentMechanics.Canonical(oldEvents) == ContentMechanics.Canonical(newEvents);
        var mechanical = ContentMechanics.Hash(before) != ContentMechanics.Hash(after);
        return new ContentDiffView(items, values, mechanical, unchanged);
    }

    private static IEnumerable<ItemDiffView> Items(string section, JsonArray? before, JsonArray? after)
    {
        var old = ById(before);
        var current = ById(after);
        foreach (var (id, item) in current)
        {
            if (!old.TryGetValue(id, out var previous))
            {
                yield return new ItemDiffView(section, id, ContentChange.Added, true, null, Version(item));
            }
            else if (ContentMechanics.Canonical(previous) != ContentMechanics.Canonical(item))
            {
                var mechanical = ContentMechanics.Canonical(ContentMechanics.Mechanics(previous, false))
                    != ContentMechanics.Canonical(ContentMechanics.Mechanics(item, false));
                yield return new ItemDiffView(section, id, ContentChange.Changed, mechanical, Version(previous), Version(item));
            }
        }

        foreach (var (id, item) in old.Where(o => !current.ContainsKey(o.Key)))
        {
            yield return new ItemDiffView(section, id, ContentChange.Removed, true, Version(item), null);
        }
    }

    private static Dictionary<string, JsonNode> ById(JsonArray? array)
    {
        var byId = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var item in array ?? [])
        {
            if (item?["id"] is JsonValue id && id.TryGetValue<string>(out var key))
            {
                byId.TryAdd(key, item);
            }
        }

        return byId;
    }

    private static int? Version(JsonNode item)
        => item["version"] is JsonValue v && v.TryGetValue<int>(out var version) ? version : null;

    private static JsonObject WithoutSections(JsonObject events)
    {
        var copy = (JsonObject)events.DeepClone();
        foreach (var (_, array) in Sections)
        {
            copy.Remove(array);
        }

        return copy;
    }

    /// <summary>Every leaf whose JSON differs; an object or array that exists on one side only is one value.</summary>
    private static void Values(string path, JsonNode? before, JsonNode? after, bool isBalance, List<ValueDiffView> diffs)
    {
        if (before is JsonObject oldObject && after is JsonObject newObject)
        {
            foreach (var name in oldObject.Select(p => p.Key).Union(newObject.Select(p => p.Key), StringComparer.Ordinal))
            {
                Values(Join(path, name), oldObject[name], newObject[name], isBalance, diffs);
            }

            return;
        }

        if (before is JsonArray oldArray && after is JsonArray newArray && oldArray.Count == newArray.Count)
        {
            for (var i = 0; i < oldArray.Count; i++)
            {
                Values($"{path}[{i.ToString(CultureInfo.InvariantCulture)}]", oldArray[i], newArray[i], isBalance, diffs);
            }

            return;
        }

        var oldText = before is null ? null : ContentMechanics.Canonical(before);
        var newText = after is null ? null : ContentMechanics.Canonical(after);
        if (oldText != newText)
        {
            diffs.Add(new ValueDiffView(path, oldText, newText, !IsDisplay(path, before ?? after, isBalance)));
        }
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";

    private static bool IsDisplay(string path, JsonNode? value, bool isBalance)
    {
        var isString = value is JsonValue v && v.GetValueKind() == JsonValueKind.String;
        if (isBalance && (path == "version" || (path.StartsWith("platformBan.choices.", StringComparison.Ordinal) && isString)))
        {
            return true;
        }

        var leaf = path[(path.LastIndexOf('.') + 1)..];
        return (isString && leaf is "title" or "text" or "label" or "successText" or "failureText" or "pitch" or "name" or "description" or "textPl" or "labelPl")
            || (leaf == "version" && path.Contains('.', StringComparison.Ordinal));
    }
}
