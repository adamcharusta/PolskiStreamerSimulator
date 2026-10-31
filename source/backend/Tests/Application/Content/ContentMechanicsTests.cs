using System.Text;
using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Content;

/// <summary>Spec M4-E §4–§5: the mechanics fingerprint and the classified diff.</summary>
public sealed class ContentMechanicsTests
{
    private static readonly ContentFiles M3 = new TestCurrentContent(TestPaths.ContentM3).Files;

    /// <summary>The M3 files with one change to the events document.</summary>
    internal static ContentFiles WithEvents(Action<JsonObject> change, ContentFiles? files = null)
    {
        var source = files ?? M3;
        var events = JsonNode.Parse(source.Events)!.AsObject();
        change(events);
        return source with { Events = Encoding.UTF8.GetBytes(DraftDocuments.Write(events)) };
    }

    internal static ContentFiles WithBalance(Action<JsonObject> change)
    {
        var balance = JsonNode.Parse(M3.Balance)!.AsObject();
        change(balance);
        return M3 with { Balance = Encoding.UTF8.GetBytes(DraftDocuments.Write(balance)) };
    }

    internal static JsonObject FirstEvent(JsonObject events) => events["events"]![0]!.AsObject();

    [Fact]
    public void Formatting_and_property_order_do_not_change_the_fingerprint()
    {
        var reformatted = WithEvents(_ => { });

        Assert.NotEqual(M3.Events, reformatted.Events);
        Assert.Equal(ContentMechanics.Hash(M3), ContentMechanics.Hash(reformatted));
        Assert.True(ContentDiff.Between(M3, reformatted).Unchanged);
    }

    public static TheoryData<string> TextFields => ["title", "text"];

    [Theory]
    [MemberData(nameof(TextFields))]
    public void An_event_text_and_its_version_are_not_mechanics(string field)
    {
        var fixedText = WithEvents(events =>
        {
            var first = FirstEvent(events);
            first[field] = "Poprawiony tekst";
            first["version"] = first["version"]!.GetValue<int>() + 1;
        });

        var diff = ContentDiff.Between(M3, fixedText);

        Assert.Equal(ContentMechanics.Hash(M3), ContentMechanics.Hash(fixedText));
        Assert.False(diff.Mechanical);
        Assert.False(diff.Unchanged);
        var item = Assert.Single(diff.Items);
        Assert.Equal((ContentChange.Changed, false, 1, 2), (item.Change, item.Mechanical, item.VersionBefore, item.VersionAfter));
    }

    [Fact]
    public void A_choice_label_and_outcome_texts_are_not_mechanics()
    {
        var relabelled = WithEvents(events =>
        {
            var choice = FirstEvent(events)["choices"]![0]!.AsObject();
            choice["label"] = "Inna etykieta";
            choice["successText"] = "Inny sukces";
            choice["failureText"] = "Inna porażka";
        });

        Assert.Equal(ContentMechanics.Hash(M3), ContentMechanics.Hash(relabelled));
    }

    [Fact]
    public void A_weight_odds_or_effect_change_is_mechanics()
    {
        var weight = WithEvents(events => FirstEvent(events)["weight"] = 9);
        var odds = WithEvents(events => FirstEvent(events)["choices"]![0]!["baseChance"] = 0.55);
        var cooldown = WithEvents(events => FirstEvent(events)["cooldownWeeks"] = 5);

        Assert.All(new[] { weight, odds, cooldown }, files => Assert.NotEqual(ContentMechanics.Hash(M3), ContentMechanics.Hash(files)));
        Assert.True(ContentDiff.Between(M3, odds).Items.Single().Mechanical);
    }

    [Fact]
    public void A_balance_constant_is_mechanics_and_its_version_label_is_not()
    {
        var constant = WithBalance(balance => balance["livingCostPln"] = 120);
        var version = WithBalance(balance => balance["version"] = "m3.2-poprawka");

        var diff = ContentDiff.Between(M3, constant);

        Assert.NotEqual(ContentMechanics.Hash(M3), ContentMechanics.Hash(constant));
        Assert.Equal(ContentMechanics.Hash(M3), ContentMechanics.Hash(version));
        var value = Assert.Single(diff.Values);
        Assert.Equal(("livingCostPln", "100", "120", true), (value.Path, value.Before, value.After, value.Mechanical));
        Assert.False(Assert.Single(ContentDiff.Between(M3, version).Values).Mechanical);
    }

    [Fact]
    public void Added_and_removed_events_are_mechanics()
    {
        var removed = WithEvents(events => events["events"]!.AsArray().RemoveAt(0));
        var id = FirstEvent(JsonNode.Parse(M3.Events)!.AsObject())["id"]!.GetValue<string>();

        var diff = ContentDiff.Between(M3, removed);
        var back = ContentDiff.Between(removed, M3);

        Assert.True(diff.Mechanical);
        Assert.Equal(("event", id, ContentChange.Removed), (diff.Items.Single().Section, diff.Items.Single().Id, diff.Items.Single().Change));
        Assert.Equal(ContentChange.Added, back.Items.Single().Change);
    }
}
