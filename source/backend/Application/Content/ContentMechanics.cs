using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>
/// The mechanics of content: both files without what players only read (spec M4-E §4). Two snapshots with the same
/// fingerprint play identically, so a text-only revision keeps the leaderboard season.
/// </summary>
public static class ContentMechanics
{
    /// <summary>String properties that are display text wherever they appear.</summary>
    private static readonly HashSet<string> TextProperties = new(StringComparer.Ordinal)
    {
        "title", "text", "label", "successText", "failureText", "pitch", "name", "description",

        // M1 content names its Polish texts with a suffix.
        "textPl", "labelPl",
    };

    /// <summary>Lowercase hex SHA-256 of the canonical mechanics of both files.</summary>
    public static string Hash(ContentFiles files)
    {
        var balance = Mechanics(JsonNode.Parse(files.Balance), isBalance: true);
        var events = Mechanics(JsonNode.Parse(files.Events), isBalance: false);
        var canonical = $"{Canonical(balance)}\n{Canonical(events)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>A copy of a document without its display strings and versions.</summary>
    public static JsonNode? Mechanics(JsonNode? document, bool isBalance)
    {
        var copy = document?.DeepClone();
        if (copy is JsonObject root)
        {
            if (isBalance)
            {
                root.Remove("version");
                if (root["platformBan"]?["choices"] is JsonObject labels)
                {
                    foreach (var name in labels.Where(p => p.Value is JsonValue).Select(p => p.Key).ToList())
                    {
                        labels.Remove(name);
                    }
                }
            }

            Strip(root);
        }

        return copy;
    }

    /// <summary>Compact JSON with object properties in ordinal order, so formatting and order never matter.</summary>
    public static string Canonical(JsonNode? node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, node);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj.ToList())
                {
                    var display = value is JsonValue text && text.GetValueKind() == JsonValueKind.String && TextProperties.Contains(name);

                    // Every event and sponsor version is bookkeeping for immutability (TD-112), not a rule.
                    var version = name == "version";
                    if (display || version)
                    {
                        obj.Remove(name);
                    }
                    else
                    {
                        Strip(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Strip(item);
                }

                break;
        }
    }

    private static void Write(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var (name, value) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(name);
                    Write(writer, value);
                }

                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                node.WriteTo(writer);
                break;
        }
    }
}
