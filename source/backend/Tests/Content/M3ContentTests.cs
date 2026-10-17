using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Content;

public class M3ContentTests
{
    private static readonly ContentSnapshot C = new JsonContentSource(TestPaths.ContentM3).Load();

    [Fact]
    public void Loads_valid_for_the_m3_ruleset() => Assert.Equal(("m3-systems.1", 2), (C.RulesetId, C.Schema));

    [Fact]
    public void Has_the_representative_set()
    {
        var live = C.Events.Where(e => e.Phase == EventPhase.Live).ToList();
        foreach (var category in Enum.GetValues<Category>())
        {
            Assert.Equal(5, live.Count(e => e.Eligibility.Categories is [var only] && only == category));
        }

        Assert.True(live.Count(e => e.Eligibility.Categories.IsDefaultOrEmpty) >= 8);
        Assert.True(C.Events.Count(e => e.Phase != EventPhase.Live) >= 4);
        Assert.InRange(C.Events.Length, 38, 45);
    }

    [Fact]
    public void Every_gendered_text_lists_three_forms()
    {
        var texts = C.Events
            .SelectMany(e => new[] { e.Title, e.TextPl }.Concat(e.Choices.SelectMany(ch => new[] { ch.LabelPl, ch.SuccessText, ch.FailureText })))
            .OfType<string>()
            .Where(t => t.Contains("{gender", StringComparison.Ordinal));
        Assert.All(texts, t =>
        {
            Assert.Contains("female {", t, StringComparison.Ordinal);
            Assert.Contains(" male {", t, StringComparison.Ordinal);
            Assert.Contains("other {", t, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Arc_and_triggered_events_are_wired()
    {
        var effects = C.Events.SelectMany(e => e.Choices).SelectMany(ch => ch.Outcomes.Success.Concat(ch.Outcomes.Failure)).ToList();
        Assert.Contains(effects, f => f.Kind == EffectKind.FollowUp && f.Target == "rivalry-crisis");
        Assert.Contains(C.Balance.NetworkCollaborate!.Outcomes.CriticalFailure!.Value, f => f.Target == "collab-fallout");
        Assert.Equal("burnout-recovery", C.Balance.RecoveryCrisisEventId);
        Assert.Contains(C.Events, e => e.Id == "first-week" && e.OneTime && e.Eligibility is { FromWeek: 2, ToWeek: 2 });
    }
}
