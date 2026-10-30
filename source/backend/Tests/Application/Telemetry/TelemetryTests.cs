using System.Text.Json;
using PolskiStreamerSimulator.Application.Telemetry;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Telemetry;

/// <summary>Spec M4-F §2: only allowlisted events and values are counted, per UTC day.</summary>
public sealed class TelemetryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static TelemetryEvent Event(string? name, params (string Key, object Value)[] props)
        => new(name, props.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)));

    [Fact]
    public void Props_are_sorted_into_the_counter_key()
    {
        var key = TelemetryAllowlist.Key(Event("career_created", ("ranked", true), ("difficulty", "easy"), ("background", "dormRoomNewcomer")));

        Assert.Equal(("career_created", "background=dormRoomNewcomer;difficulty=easy;ranked=true"), key);
        Assert.Equal(("save_imported", ""), TelemetryAllowlist.Key(Event("save_imported")));
        Assert.Equal(("batch_run", "weeks=13"), TelemetryAllowlist.Key(Event("batch_run", ("weeks", 13))));
    }

    public static TheoryData<TelemetryEvent> Rejected => new()
    {
        Event(null),
        Event("page_view"),
        Event("career_created", ("nickname", "Ala")),
        Event("career_created", ("background", "somethingElse")),
        Event("batch_run", ("weeks", 7)),
        Event("week_settled", ("stage", new[] { "1-4" })),
        Event("result_published", ("careerId", Guid.NewGuid())),
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Anything_off_the_allowlist_is_skipped(TelemetryEvent e)
        => Assert.Null(TelemetryAllowlist.Key(e));

    [Fact]
    public async Task A_batch_counts_its_valid_events_for_the_day()
    {
        var store = new InMemoryTelemetryStore();
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 30, 23, 59, 0, TimeSpan.Zero));
        var handler = new RecordTelemetryHandler(store, time);

        var accepted = await handler.Handle(
            new RecordTelemetry([Event("save_restored"), Event("save_restored"), Event("page_view"), Event("batch_run", ("weeks", 4))]),
            Ct);

        Assert.Equal(3, accepted.Accepted);
        Assert.Equal(2, store.Count(new DateOnly(2026, 10, 30), "save_restored", ""));
        Assert.Equal(1, store.Count(new DateOnly(2026, 10, 30), "batch_run", "weeks=4"));
    }

    [Fact]
    public async Task Only_the_first_fifty_events_of_a_batch_count()
    {
        var store = new InMemoryTelemetryStore();
        var handler = new RecordTelemetryHandler(store, new FixedTimeProvider(AppTestKit.Start));

        var accepted = await handler.Handle(new RecordTelemetry([.. Enumerable.Repeat(Event("save_imported"), 60)]), Ct);

        Assert.Equal(TelemetryAllowlist.MaxEvents, accepted.Accepted);
    }
}
