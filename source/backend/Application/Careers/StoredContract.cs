using System.Text.Json;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Domain.Rulesets;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// Reads stored responses and reports into today's contract records (TD-91). Rows written before M3 (contract v1)
/// are upgraded first: <c>textPl</c> and <c>labelPl</c> become <c>text</c> and <c>label</c>, and each field added
/// since gets the value it has for an M1 career, the only ruleset of contract v1. Current rows pass unchanged.
/// </summary>
public static class StoredContract
{
    public static ActionOutcome ReadOutcome(string json) => Read<ActionOutcome>(json, UpgradeOutcome);

    public static WeekReportView ReadReport(string json) => Read<WeekReportView>(json, UpgradeReport);

    public static AutoWeekRecord ReadAutoWeek(string json) => Read<AutoWeekRecord>(json, record => UpgradeReport(record["week"]?["report"] as JsonObject));

    private static T Read<T>(string json, Action<JsonObject> upgrade)
    {
        var node = JsonNode.Parse(json) as JsonObject ?? throw new JsonException($"Stored {typeof(T).Name} JSON is not an object.");
        upgrade(node);
        return node.Deserialize<T>(ContractJson.Options) ?? throw new JsonException($"Stored {typeof(T).Name} JSON was null.");
    }

    private static void UpgradeOutcome(JsonObject outcome)
    {
        UpgradeCareer(outcome["career"] as JsonObject);
        UpgradeReport(outcome["report"] as JsonObject);
    }

    private static void UpgradeCareer(JsonObject? career)
    {
        if (career is null)
        {
            return;
        }

        Default(career, "rulesetId", M1Ruleset.RulesetId);
        UpgradeReport(career["lastReport"] as JsonObject);
        if (career["next"] is not JsonObject next)
        {
            return;
        }

        foreach (var plan in Objects(next["plans"]))
        {
            Default(plan, "odds", null);
        }

        if (next["event"] is JsonObject shown)
        {
            UpgradeEvent(shown);
        }
    }

    private static void UpgradeEvent(JsonObject shown)
    {
        Default(shown, "title", null);
        Rename(shown, "textPl", "text");
        Default(shown, "mandatory", false);
        foreach (var choice in Objects(shown["choices"]))
        {
            Rename(choice, "labelPl", "label");
            Default(choice, "viralOnCritical", 0);
            Default(choice, "unavailableReason", null);
        }
    }

    private static void UpgradeReport(JsonObject? report)
    {
        if (report is null)
        {
            return;
        }

        Default(report, "decisionRolls", new JsonArray());
        Default(report, "actionRoll", null);
        Default(report, "viral", null);
    }

    private static IEnumerable<JsonObject> Objects(JsonNode? array)
        => array is JsonArray items ? items.OfType<JsonObject>() : [];

    private static void Default(JsonObject target, string name, JsonNode? value)
    {
        if (!target.ContainsKey(name))
        {
            target[name] = value;
        }
    }

    private static void Rename(JsonObject target, string from, string to)
    {
        if (target.TryGetPropertyValue(from, out var value))
        {
            target.Remove(from);
            Default(target, to, value);
        }
    }
}
