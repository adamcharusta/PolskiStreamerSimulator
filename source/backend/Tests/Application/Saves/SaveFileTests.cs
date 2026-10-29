using System.Text.Json;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Saves;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Saves;

/// <summary>Spec M4-C §5: export of an unranked career and import as a new unranked career after full validation.</summary>
public sealed class SaveFileTests
{
    /// <summary>A schema-1 export of an M1 career after one week, kept to prove later servers still read it.</summary>
    public static readonly string Schema1Fixture = Path.Combine(AppContext.BaseDirectory, "Application", "Saves", "schema-1.json");

    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The file as the API writes it and a browser sends it back.</summary>
    public static JsonElement Json(SaveFile file) => JsonSerializer.SerializeToElement(file, ContractJson.Options);

    [Fact]
    public async Task An_export_imports_as_a_new_unranked_career_with_the_same_state()
    {
        var created = await kit.NewCareerAsync("Zapis");
        var played = await kit.PlayManualWeekAsync(created);
        var original = (await kit.Store.FindAsync(created.CareerId, Ct))!;

        var file = (await kit.ExportAsync(created.CareerId)).Value;
        var imported = (await kit.ImportAsync(Json(file))).Value;

        Assert.Equal((SaveFiles.Format, SaveFiles.Schema, "Zapis", kit.Time.GetUtcNow()), (file.Format, file.Schema, file.Nickname, file.ExportedAt));
        Assert.NotEqual(created.CareerId, imported.CareerId);
        Assert.False(imported.Career.Ranked);
        Assert.Equal((played.Version, played.LastSettledWeek, "Zapis"), (imported.Career.Version, imported.Career.LastSettledWeek, imported.Career.Nickname));
        Assert.Null(imported.Career.LastReport);
        var copy = (await kit.Store.FindAsync(imported.CareerId, Ct))!;
        Assert.Equal(StateHash.Of(original.State), StateHash.Of(copy.State));
        Assert.Empty((await kit.HistoryAsync(imported.CareerId)).Weeks);
        Assert.Equal(kit.Time.GetUtcNow().AddMonths(6), copy.ExpiresAt);
        Assert.True(RecoveryKeys.TryParse(imported.RecoveryKey, out var keyCareer, out _) && keyCareer == imported.CareerId);
    }

    [Fact]
    public async Task The_imported_career_plays_on()
    {
        var created = await kit.NewCareerAsync();
        var imported = (await kit.ImportAsync(Json((await kit.ExportAsync(created.CareerId)).Value))).Value;

        var played = await kit.PlayManualWeekAsync(imported);

        Assert.Equal(1, played.LastSettledWeek);
        Assert.Equal(0, (await kit.GetAsync(created.CareerId)).LastSettledWeek);
    }

    [Fact]
    public async Task The_schema_1_fixture_still_imports()
    {
        await kit.StartAsync();
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(Schema1Fixture, Ct));

        var imported = await kit.ImportAsync(file.RootElement);

        Assert.True(imported.IsSuccess, imported.IsSuccess ? null : imported.Error.Code);
        Assert.Equal(1, imported.Value.Career.LastSettledWeek);
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { """[]""", ErrorCodes.ImportInvalid },
        { """{"format":"other","schema":1}""", ErrorCodes.ImportInvalid },
        { """{"format":"polski-streamer-simulator-save","schema":"1"}""", ErrorCodes.ImportInvalid },
        { """{"format":"polski-streamer-simulator-save","schema":2,"nickname":"Zapis","state":{}}""", ErrorCodes.ImportNewer },
        { """{"format":"polski-streamer-simulator-save","schema":0,"nickname":"Zapis","state":{}}""", ErrorCodes.ImportInvalid },
        { """{"format":"polski-streamer-simulator-save","schema":1,"nickname":"<b>","state":{}}""", ErrorCodes.ImportInvalid },
        { """{"format":"polski-streamer-simulator-save","schema":1,"nickname":"Zapis","state":{}}""", ErrorCodes.ImportInvalid },
        { """{"format":"polski-streamer-simulator-save","schema":1,"nickname":"Zapis","state":[]}""", ErrorCodes.ImportInvalid },
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task A_malformed_file_is_rejected(string json, string code)
    {
        await kit.StartAsync();
        using var file = JsonDocument.Parse(json);

        Assert.Equal(code, (await kit.ImportAsync(file.RootElement)).Error.Code);
        Assert.Equal(0, kit.Store.Count);
    }

    [Theory]
    [InlineData("RulesetId", "m9-unknown.1", "import-unsupported")]
    [InlineData("ContentDigest", "0000000000000000000000000000000000000000000000000000000000000000", "import-unsupported")]
    [InlineData("Followers", -5, "import-invalid")]
    [InlineData("StateVersion", -1, "import-invalid")]
    [InlineData("CashPln", "lots", "import-invalid")]
    public async Task An_edited_state_is_rejected_without_touching_the_original(string field, object value, string code)
    {
        var created = await kit.NewCareerAsync();
        await kit.SaveSlotAsync(created.CareerId, 1, 0);
        var file = Edit(Json((await kit.ExportAsync(created.CareerId)).Value), state => state[field] = JsonValue.Create(value));

        var imported = await kit.ImportAsync(file);

        Assert.Equal(code, imported.Error.Code);
        Assert.Equal(1, kit.Store.Count);
        Assert.False((await kit.SlotsAsync(created.CareerId)).Value.Slots[0].Empty);
    }

    [Fact]
    public async Task A_state_on_another_rulesets_content_is_unsupported()
    {
        var m3 = AppTestKit.WithContent(TestPaths.ContentM3);
        var created = await m3.NewCareerAsync();
        await kit.StartAsync();
        var m1Digest = kit.Current.Snapshot.Digest;
        await m3.ContentStore.EnsureStoredAsync(kit.Current.Files, Ct);
        var file = Edit(Json((await m3.ExportAsync(created.CareerId)).Value), state => state["ContentDigest"] = m1Digest);

        Assert.Equal(ErrorCodes.ImportUnsupported, (await m3.ImportAsync(file)).Error.Code);
    }

    [Fact]
    public async Task A_state_whose_view_cannot_be_built_is_invalid()
    {
        var created = await kit.NewCareerAsync();
        var started = (await kit.SubmitAsync(created.CareerId, Guid.NewGuid(), new ActionInput(0, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null))).Value.Career;
        Assert.Equal(NextStepKind.ResolveEvent, started.Next.Kind);
        var file = Edit(Json((await kit.ExportAsync(created.CareerId)).Value), state => state["Pending"]!["EventIds"] = new JsonArray("missing-event"));

        Assert.Equal(ErrorCodes.ImportInvalid, (await kit.ImportAsync(file)).Error.Code);
    }

    [Fact]
    public async Task A_career_played_to_its_end_exports_far_below_the_import_limit()
    {
        var m3 = AppTestKit.WithContent(TestPaths.ContentM3);
        var created = await m3.NewCareerAsync();
        var career = created.Career;
        while (career.Status == CareerStatus.Active)
        {
            var outcome = await m3.RunBatchAsync(created.CareerId, career.Version);
            career = outcome.IsSuccess && outcome.Value.WeeksCommitted > 0
                ? outcome.Value.Career
                : await m3.PlayManualWeekAsync(career);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes((await m3.ExportAsync(created.CareerId)).Value, ContractJson.Options);
        Assert.InRange(bytes.Length, 1, SaveFiles.MaxBytes / 4);
        Assert.Equal(1040, career.LastSettledWeek);
    }

    private static JsonElement Edit(JsonElement file, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(file.GetRawText())!.AsObject();
        change(node["state"]!.AsObject());
        return JsonSerializer.SerializeToElement(node);
    }
}
