using System.Text.Json;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Domain.Rulesets;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// Reads stored responses and reports into today's contract records (TD-91). Rows written before M3 (contract v1)
/// are upgraded first: <c>textPl</c> and <c>labelPl</c> become <c>text</c> and <c>label</c>, and each field added
/// since gets the value it has for an M1 career, the only ruleset of contract v1. Rows written before M3-B get the M3-A
/// defaults of the M3-B fields (Czatnia since week 1, no strikes, offers, deals or side business, no wager, platform
/// move, policy exposure or sponsor consequence). Current rows pass unchanged.
/// </summary>
public static class StoredContract
{
    public static ActionOutcome ReadOutcome(string json) => Read<ActionOutcome>(json, UpgradeOutcome);

    public static WeekReportView ReadReport(string json) => Read<WeekReportView>(json, UpgradeReport);

    public static AutoWeekRecord ReadAutoWeek(string json) => Read<AutoWeekRecord>(json, UpgradeAutoWeek);

    private static void UpgradeAutoWeek(JsonObject record)
    {
        if (record["week"] is not JsonObject week)
        {
            return;
        }

        Default(week, "offers", new JsonArray());
        UpgradePlan(week["plan"] as JsonObject);
        UpgradeReport(week["report"] as JsonObject);
    }

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
        Default(career, "platform", "czatnia");
        Default(career, "platformSinceWeek", 1);
        Default(career, "strikes", new JsonArray());
        Default(career, "bannedPlatforms", new JsonArray());
        Default(career, "offers", new JsonArray());
        Default(career, "contracts", new JsonArray());
        Default(career, "sideBusiness", null);
        UpgradeReport(career["lastReport"] as JsonObject);
        if (career["next"] is not JsonObject next)
        {
            return;
        }

        foreach (var option in Objects(next["plans"]))
        {
            Default(option, "odds", null);
            Default(option, "wager", null);
            Default(option, "policy", null);
            Default(option, "migration", null);
            Default(option, "sponsorConsequences", new JsonArray());
            UpgradePlan(option["plan"] as JsonObject);
            if (option["odds"] is JsonObject odds)
            {
                UpgradeChoice(odds);
            }
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
            UpgradeChoice(choice);
        }
    }

    private static void UpgradeChoice(JsonObject choice)
    {
        Default(choice, "viralOnCritical", 0);
        Default(choice, "unavailableReason", null);
        Default(choice, "migration", null);
    }

    private static void UpgradePlan(JsonObject? plan)
    {
        if (plan is null)
        {
            return;
        }

        Default(plan, "wager", null);
        Default(plan, "targetPlatform", null);
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
        Default(report, "strike", null);
        Default(report, "wager", null);
        Default(report, "migration", null);
        UpgradePlan(report["plan"] as JsonObject);
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
