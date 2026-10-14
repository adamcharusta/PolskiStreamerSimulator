using System.Text.Json;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>
/// Stored responses and reports are read back into today's contract records on every read and repeat, and stored
/// requests are compared byte for byte with today's serialization, so rows written by an earlier version must keep
/// loading and serializing unchanged (TD-91).
/// </summary>
public sealed class StoredContractTests
{
    [Theory]
    [InlineData("week-report.json", typeof(WeekReportView))]
    [InlineData("action-outcome.json", typeof(ActionOutcome))]
    [InlineData("auto-week-record.json", typeof(AutoWeekRecord))]
    [InlineData("action-input.json", typeof(ActionInput))]
    [InlineData("batch-input.json", typeof(BatchInput))]
    public void A_stored_v1_row_still_reads_back_unchanged(string file, Type type)
    {
        var json = File.ReadAllText(TestPaths.Golden(Path.Combine("contract-v1", file))).TrimEnd();

        var value = JsonSerializer.Deserialize(json, type, ContractJson.Options);

        Assert.Equal(json, JsonSerializer.Serialize(value, type, ContractJson.Options));
    }
}
