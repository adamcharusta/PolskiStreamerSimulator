using System.Text;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure;

public class EventVersionsTests
{
    private static byte[] Events(string body) => Encoding.UTF8.GetBytes($$"""{ "schema": 2, "events": [ {{body}} ] }""");

    [Fact]
    public void Same_version_with_different_content_conflicts()
    {
        var stored = Events("""{ "id": "a", "version": 1, "text": "x" }""");
        var candidate = Events("""{ "id": "a", "version": 1, "text": "y" }""");
        Assert.Equal(["a@1"], EventVersions.Conflicts(candidate, [stored]));
    }

    [Fact]
    public void Whitespace_and_property_order_do_not_conflict()
    {
        var stored = Events("""{ "id": "a", "version": 1, "text": "x" }""");
        var candidate = Events("""{"text":"x","version":1,"id":"a"}""");
        Assert.Empty(EventVersions.Conflicts(candidate, [stored]));
    }

    [Fact]
    public void New_version_does_not_conflict()
    {
        var stored = Events("""{ "id": "a", "version": 1, "text": "x" }""");
        var candidate = Events("""{ "id": "a", "version": 2, "text": "y" }""");
        Assert.Empty(EventVersions.Conflicts(candidate, [stored]));
    }

    [Fact]
    public void Schema_one_files_are_ignored()
    {
        var m1 = Encoding.UTF8.GetBytes("""{ "events": [ { "id": "a", "textPl": "x" } ] }""");
        Assert.Empty(EventVersions.Conflicts(Events("""{ "id": "a", "version": 1, "text": "y" }"""), [m1]));
    }

    private static byte[] WithSponsor(long paymentPln, int version = 1) => Encoding.UTF8.GetBytes($$"""
        { "schema": 2, "events": [ { "id": "a", "version": 1, "text": "x" } ],
          "sponsors": [ { "id": "trusted-tea", "version": {{version}}, "paymentPln": {{paymentPln}} } ] }
        """);

    [Fact]
    public void Changed_sponsor_under_the_same_version_conflicts()
        => Assert.Equal(["sponsor:trusted-tea@1"], EventVersions.Conflicts(WithSponsor(301), [WithSponsor(300)]));

    [Fact]
    public void Changed_sponsor_under_a_new_version_does_not_conflict()
        => Assert.Empty(EventVersions.Conflicts(WithSponsor(301, version: 2), [WithSponsor(300)]));

    [Fact]
    public void An_event_and_a_sponsor_with_the_same_id_and_version_do_not_clash()
    {
        var stored = Encoding.UTF8.GetBytes("""{ "schema": 2, "events": [ { "id": "trusted-tea", "version": 1, "text": "x" } ] }""");
        Assert.Empty(EventVersions.Conflicts(WithSponsor(300), [stored]));
    }

    [Fact]
    public async Task The_in_memory_store_refuses_a_changed_event_under_the_same_version()
    {
        var store = new InMemoryContentStore();
        await store.EnsureStoredAsync(new(TestContentFiles.Balance, TestContentFiles.SchemaTwoEvents()), TestContext.Current.CancellationToken);

        var changed = new PolskiStreamerSimulator.Application.Content.ContentFiles(TestContentFiles.Balance, TestContentFiles.SchemaTwoEvents(" Zmieniono."));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.EnsureStoredAsync(changed, TestContext.Current.CancellationToken));

        Assert.Contains("event-version-changed:", error.Message, StringComparison.Ordinal);
    }
}
