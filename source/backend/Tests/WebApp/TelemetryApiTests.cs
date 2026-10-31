using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;
using PolskiStreamerSimulator.WebApp.Telemetry;

namespace PolskiStreamerSimulator.Tests.WebApp;

/// <summary>Spec M4-F §2 over HTTP on SQL Server: daily counts only, no cookies, bounded requests.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TelemetryApiTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_events_are_counted_per_day_and_nothing_else_is_stored()
    {
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var body = new
        {
            events = new object[]
            {
                new { name = "career_created", props = new { background = "dormRoomNewcomer", difficulty = "normal", ranked = true } },
                new { name = "career_created", props = new { background = "dormRoomNewcomer", difficulty = "normal", ranked = true } },
                new { name = "career_created", props = new { nickname = "Ala" } },
                new { name = "save_imported" },
            },
        };

        var first = await client.PostAsJsonAsync("/v1/telemetry", body, Ct);
        var second = await client.PostAsJsonAsync("/v1/telemetry", body, Ct);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(3, (await first.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accepted").GetInt32());
        Assert.DoesNotContain(first.Headers, h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        var rows = await db.TelemetryDaily.AsNoTracking().OrderBy(r => r.Event).ToListAsync(Ct);
        Assert.Equal(
            [("career_created", "background=dormRoomNewcomer;difficulty=normal;ranked=true", 4L), ("save_imported", "", 2L)],
            rows.Select(r => (r.Event, r.PropsKey, r.Count)));
        Assert.All(rows, r => Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), r.Day));
    }

    [Fact]
    public async Task Oversized_and_overlong_batches_are_refused()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var client = api.CreateClient();
        using var huge = new StringContent("{\"events\":[{\"name\":\"" + new string('a', TelemetryEndpoints.MaxBytes) + "\"}]}", Encoding.UTF8, "application/json");

        var tooBig = await client.PostAsync("/v1/telemetry", huge, Ct);
        var tooMany = await client.PostAsJsonAsync("/v1/telemetry", new { events = Enumerable.Repeat(new { name = "save_imported" }, 51) }, Ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }
}
