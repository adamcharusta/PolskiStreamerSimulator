using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PolskiStreamerSimulator.Application.Saves;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>Spec M4-C §6: slots, export and import over HTTP.</summary>
public sealed class SaveApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    [Fact]
    public async Task Save_play_and_restore_return_the_saved_week()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var saved = await client.PutSlotAsync(id, secret, 1, new { expectedVersion = 0 }, Ct);
        var played = (await client.PlayWeekAsync(id, secret, career, Ct)).GetProperty("career");

        var restored = await client.PostRestoreAsync(id, secret, 1, new { commandId = Guid.NewGuid(), expectedVersion = played.GetProperty("version").GetInt64() }, Ct);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var slot = (await BodyAsync(saved)).GetProperty("slots")[0];
        Assert.Equal((1, false, 1), (slot.GetProperty("slot").GetInt32(), slot.GetProperty("empty").GetBoolean(), slot.GetProperty("week").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var view = (await BodyAsync(restored)).GetProperty("career");
        Assert.Equal(0, view.GetProperty("lastSettledWeek").GetInt32());
        Assert.Equal(played.GetProperty("version").GetInt64() + 1, view.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task An_export_imports_as_a_new_career()
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);
        var file = await BodyAsync(await client.GetExportAsync(id, secret, Ct));

        var imported = await client.PostAsJsonAsync("/v1/careers/import", new { save = file }, Ct);

        Assert.Equal("polski-streamer-simulator-save", file.GetProperty("format").GetString());
        Assert.Equal(HttpStatusCode.Created, imported.StatusCode);
        var body = await BodyAsync(imported);
        var newId = body.GetProperty("careerId").GetGuid();
        Assert.NotEqual(id, newId);
        Assert.Equal($"/v1/careers/{newId}", imported.Headers.Location!.OriginalString);
        Assert.False(body.GetProperty("career").GetProperty("ranked").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetCareerAsync(newId, body.GetProperty("secret").GetString(), Ct)).StatusCode);
    }

    [Fact]
    public async Task Ranked_careers_get_409_on_every_save_endpoint()
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct, ranked: true);

        var responses = new[]
        {
            await client.GetSlotsAsync(id, secret, Ct),
            await client.PutSlotAsync(id, secret, 1, new { expectedVersion = 0 }, Ct),
            await client.PostRestoreAsync(id, secret, 1, new { commandId = Guid.NewGuid(), expectedVersion = 0 }, Ct),
            await client.GetExportAsync(id, secret, Ct),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("career-ranked", await response.CodeAsync(Ct));
        }
    }

    [Fact]
    public async Task Slot_errors_have_their_codes()
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);

        var missing = await client.PutSlotAsync(id, secret, 4, new { expectedVersion = 0 }, Ct);
        var empty = await client.PostRestoreAsync(id, secret, 2, new { commandId = Guid.NewGuid(), expectedVersion = 0 }, Ct);
        var stale = await client.PutSlotAsync(id, secret, 1, new { expectedVersion = 7 }, Ct);
        var noBody = await client.PutSlotAsync(id, secret, 1, null!, Ct);
        var anonymous = await client.GetSlotsAsync(id, null, Ct);

        Assert.Equal((HttpStatusCode.NotFound, "slot-not-found"), (missing.StatusCode, await missing.CodeAsync(Ct)));
        Assert.Equal((HttpStatusCode.Conflict, "slot-empty"), (empty.StatusCode, await empty.CodeAsync(Ct)));
        Assert.Equal((HttpStatusCode.Conflict, "stale-version"), (stale.StatusCode, await stale.CodeAsync(Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task A_rejected_import_creates_nothing()
    {
        var before = api.Store.Count;

        var newer = await client.PostAsJsonAsync("/v1/careers/import", new { save = new { format = SaveFiles.Format, schema = 2 } }, Ct);
        var missing = await client.PostAsJsonAsync("/v1/careers/import", new { }, Ct);

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "import-newer"), (newer.StatusCode, await newer.CodeAsync(Ct)));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(before, api.Store.Count);
    }

    [Fact]
    public async Task An_oversized_import_is_refused()
    {
        var padding = new string('a', SaveFiles.MaxBytes);
        using var content = new StringContent("{\"save\":{\"format\":\"" + padding + "\"}}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/v1/careers/import", content, Ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
