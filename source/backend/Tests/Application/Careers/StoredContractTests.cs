using System.Text.Json;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>
/// Stored responses and reports are read back into today's contract records on every read and repeat, and stored
/// requests are compared byte for byte with today's serialization, so rows written by an earlier version must keep
/// loading (TD-91). Responses written before M3 (contract v1) are upgraded on read: renamed fields keep their value
/// and fields added since then get the value that matches an M1 career.
/// </summary>
public sealed class StoredContractTests
{
    private static readonly Dictionary<string, string> Renamed = new(StringComparer.Ordinal) { ["textPl"] = "text", ["labelPl"] = "label" };

    private static string Golden(string file) => File.ReadAllText(TestPaths.Golden(Path.Combine("contract-v1", file))).TrimEnd();

    [Theory]
    [InlineData("action-input.json", typeof(ActionInput))]
    [InlineData("batch-input.json", typeof(BatchInput))]
    public void A_stored_v1_request_still_serializes_byte_for_byte(string file, Type type)
    {
        var json = Golden(file);

        var value = JsonSerializer.Deserialize(json, type, ContractJson.Options);

        Assert.Equal(json, JsonSerializer.Serialize(value, type, ContractJson.Options));
    }

    [Theory]
    [InlineData("week-report.json")]
    [InlineData("action-outcome.json")]
    [InlineData("action-outcome-event.json")]
    [InlineData("auto-week-record.json")]
    public void A_stored_v1_response_reads_back_without_losing_a_value(string file)
    {
        var json = Golden(file);

        var read = file switch
        {
            "week-report.json" => ContractJson.Serialize(StoredContract.ReadReport(json)),
            "auto-week-record.json" => ContractJson.Serialize(StoredContract.ReadAutoWeek(json)),
            _ => ContractJson.Serialize(StoredContract.ReadOutcome(json)),
        };

        AssertKeeps(JsonNode.Parse(json), JsonNode.Parse(read), "$");
    }

    [Fact]
    public void A_v1_step_response_gets_the_m1_values_of_the_fields_added_since()
    {
        var outcome = StoredContract.ReadOutcome(Golden("action-outcome-event.json"));

        var shown = outcome.Career.Next.Event!;
        Assert.Equal("m1-prototype.1", outcome.Career.RulesetId);
        Assert.Equal((null, false), (shown.Title, shown.Mandatory));
        Assert.All(shown.Choices, c => Assert.Equal((0m, (string?)null), (c.ViralOnCritical, c.UnavailableReason)));
        Assert.Empty(outcome.Career.LastReport!.DecisionRolls);
        Assert.Equal((null, null), (outcome.Career.LastReport.ActionRoll, outcome.Career.LastReport.Viral));
        Assert.All(StoredContract.ReadOutcome(Golden("action-outcome.json")).Career.Next.Plans, p => Assert.Null(p.Odds));
    }

    [Fact]
    public void A_current_response_reads_back_unchanged()
    {
        var current = ContractJson.Serialize(StoredContract.ReadOutcome(Golden("action-outcome-event.json")));

        Assert.Equal(current, ContractJson.Serialize(StoredContract.ReadOutcome(current)));
    }

    /// <summary>Every value of <paramref name="stored"/> is in <paramref name="read"/>, under its renamed name where it was renamed.</summary>
    private static void AssertKeeps(JsonNode? stored, JsonNode? read, string path)
    {
        switch (stored)
        {
            case JsonObject obj:
                var target = Assert.IsType<JsonObject>(read);
                foreach (var (name, value) in obj)
                {
                    var now = Renamed.GetValueOrDefault(name, name);
                    Assert.True(target.ContainsKey(now), $"{path}.{name} was lost");
                    AssertKeeps(value, target[now], $"{path}.{now}");
                }

                break;
            case JsonArray array:
                var items = Assert.IsType<JsonArray>(read);
                Assert.Equal(array.Count, items.Count);
                for (var i = 0; i < array.Count; i++)
                {
                    AssertKeeps(array[i], items[i], $"{path}[{i}]");
                }

                break;
            default:
                Assert.True(JsonNode.DeepEquals(stored, read), $"{path}: {stored?.ToJsonString()} became {read?.ToJsonString()}");
                break;
        }
    }
}
