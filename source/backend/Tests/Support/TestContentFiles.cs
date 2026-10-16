using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The bytes of the repository's M1 content files, and a schema-2 rewrite of the events.</summary>
public static class TestContentFiles
{
    public static byte[] Balance { get; } = File.ReadAllBytes(Path.Combine(TestPaths.ContentM1, JsonContentSource.BalanceFile));

    public static byte[] Events { get; } = File.ReadAllBytes(Path.Combine(TestPaths.ContentM1, JsonContentSource.EventsFile));

    /// <summary>The M1 events rewritten as schema-2 live events at version 1; <paramref name="textSuffix"/> changes every event text.</summary>
    public static byte[] SchemaTwoEvents(string textSuffix = "")
    {
        var root = JsonNode.Parse(Events)!.AsObject();
        var events = root["events"]!.AsArray();
        foreach (var node in events)
        {
            var e = node!.AsObject();
            e["version"] = 1;
            e["phase"] = "live";
            e["title"] = $"Tytuł {e["id"]}";
            e["text"] = e["textPl"]!.GetValue<string>() + textSuffix;
            e.Remove("textPl");
            e.Remove("titleKey");
            foreach (var choice in e["choices"]!.AsArray())
            {
                var c = choice!.AsObject();
                c["label"] = c["labelPl"]!.GetValue<string>();
                c.Remove("labelPl");
                c.Remove("labelKey");
            }
        }

        root["schema"] = 2;
        return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
    }
}
