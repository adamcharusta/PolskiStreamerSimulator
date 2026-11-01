using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Content;

public class M3ContentTests
{
    private static readonly ContentSnapshot C = new JsonContentSource(TestPaths.ContentM3).Load();

    [Fact]
    public void Loads_valid_for_the_m3_ruleset() => Assert.Equal(("m3-systems.2", 2), (C.RulesetId, C.Schema));

    private static readonly string[] M3bPrefixes = ["policy-", "wager-", "side-", "sponsor-", "puls-", "nurt-"];

    private static readonly string[] M5SampleIds =
    [
        "jc-cooking-live", "jc-reaction-queue", "fps-stream-sniper", "fps-screaming-teammate", "moba-afk-ally",
        "moba-chat-pick", "chess-engine-suspicion", "chess-bullet-marathon", "gamba-chat-picks-slot", "gamba-loss-tally",
        "x-power-cut", "x-family-visit", "x-out-of-context", "x-charity-stream", "czatnia-emote-rules", "nurt-subs-only",
        "channel-first-year", "followers-100k", "pc-breakdown", "mod-team-quits",
    ];

    private static bool IsM5Sample(EventDefinition e) => M5SampleIds.Contains(e.Id);

    private static bool IsM3b(EventDefinition e)
        => !IsM5Sample(e) && M3bPrefixes.Any(p => e.Id.StartsWith(p, StringComparison.Ordinal));

    private static bool IsM3a(EventDefinition e) => !IsM5Sample(e) && !IsM3b(e);

    [Fact]
    public void Has_the_representative_set()
    {
        var live = C.Events.Where(e => e.Phase == EventPhase.Live && IsM3a(e)).ToList();
        foreach (var category in Enum.GetValues<Category>())
        {
            Assert.Equal(5, live.Count(e => e.Eligibility.Categories is [var only] && only == category));
        }

        Assert.True(live.Count(e => e.Eligibility.Categories.IsDefaultOrEmpty) >= 8);
        Assert.True(C.Events.Count(e => e.Phase != EventPhase.Live) >= 4);
        Assert.Equal(42, C.Events.Count(IsM3a));
    }

    [Fact]
    public void Has_the_m3b_set()
    {
        Assert.Equal(12, C.Sponsors.Length);
        Assert.Equal(4, C.Sponsors.Count(t => t.Prerequisites.MaxDrama is not null && t.RequiredCategory is null && t.Platforms.IsEmpty));
        Assert.Equal(3, C.Sponsors.Count(t => t.Prerequisites.MinDrama is { } d && d >= Fixed.FromInt(50)));
        Assert.Equal(3, C.Sponsors.Count(t => t.RequiredCategory is not null));
        Assert.Equal(2, C.Sponsors.Count(t => !t.Platforms.IsEmpty));
        Assert.Equal(77, C.Events.Length);   // 42 M3-A events, the 15 M3-B ones and the 20 M5 samples
        Assert.Equal(15, C.Events.Count(IsM3b));
        Assert.Equal(4, C.Events.Count(e => e.Eligibility.RequiresFlags.Contains("side-business")));
    }

    [Fact]
    public void Has_the_m5_sample_set()
    {
        var samples = C.Events.Where(IsM5Sample).ToList();
        Assert.Equal(M5SampleIds.Length, samples.Count);
        var live = samples.Where(e => e.Phase == EventPhase.Live).ToList();
        foreach (var category in Enum.GetValues<Category>())
        {
            Assert.Equal(2, live.Count(e => e.Eligibility.Categories is [var only] && only == category));
        }

        Assert.Equal(4, live.Count(e => e.Eligibility.Categories.IsDefaultOrEmpty && e.Eligibility.Platforms.IsDefaultOrEmpty));
        Assert.Equal(2, live.Count(e => !e.Eligibility.Platforms.IsDefaultOrEmpty));
        Assert.Equal(2, samples.Count(e => e.Phase == EventPhase.Milestone));
        Assert.Equal(2, samples.Count(e => e.Phase == EventPhase.Crisis));
    }

    [Fact]
    public void Balance_has_every_m3b_section()
    {
        var b = C.Balance;
        Assert.NotNull(b.Platforms);
        Assert.Equal(Fixed.FromRatio(110, 100), b.Platforms[Platform.PulsLive].CategoryReach[Category.Fps]);
        Assert.Equal(Fixed.FromRatio(80, 100), b.Platforms[Platform.Nurt].CategoryReach[Category.Gambling]);
        Assert.Equal((100L, 500L, 2_000L), (b.Wagers!.StakePln[WagerTier.Low], b.Wagers.StakePln[WagerTier.Medium], b.Wagers.StakePln[WagerTier.High]));
        Assert.NotNull(b.Migration);
        Assert.NotNull(b.Policy);
        Assert.NotNull(b.SideBusiness);
        Assert.Equal("bank-skarbonka", b.Sponsors!.StartingLeads[Background.IndustryInsider]);
        Assert.Equal("Ban na platformie", b.PlatformBan!.Title);
    }

    [Fact]
    public void Global_ban_appears_only_in_the_privacy_crisis()
    {
        var owners = C.Events.Where(e => e.Choices.Any(ch => ch.Outcomes.Failure.Any(f => f.Kind == EffectKind.GlobalBan))).Select(e => e.Id);
        Assert.Equal(["policy-privacy-leak"], owners);
    }

    [Fact]
    public void Gambling_text_never_offers_real_money()
    {
        string[] forbidden = ["prawdziw", "wypłac", "wypłat", "doładuj", "doładow", "depozyt", "przelew", "blik", "cash-out", "cashout", "kup żeton", "kupić żeton", "kasa na konto"];
        var texts = C.Events.Where(e => e.Tags.Contains("gambling"))
            .SelectMany(e => new[] { e.Title, e.TextPl }.Concat(e.Choices.SelectMany(ch => new[] { ch.LabelPl, ch.SuccessText, ch.FailureText })))
            .Concat(C.Sponsors.Where(t => t.Tags.Contains("gambling") || t.RequiredCategory == Category.Gambling).SelectMany(t => new[] { t.Name, t.Pitch }))
            .OfType<string>()
            .ToList();
        Assert.NotEmpty(texts);
        Assert.All(texts, t => Assert.DoesNotContain(forbidden, w => t.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Every_adult_business_and_privacy_event_is_tagged()
    {
        Assert.All(C.Events.Where(e => e.Eligibility.RequiresFlags.Contains("side-business")), e => Assert.Contains("adult-business", e.Tags));
        Assert.Contains("privacy-incident", C.Events.Single(e => e.Id == "policy-privacy-leak").Tags);
        Assert.Contains("privacy-incident", C.Events.Single(e => e.Id == "side-leak-rumour").Tags);
    }

    [Fact]
    public void Every_sponsor_is_tagged_sponsor()
        => Assert.All(C.Sponsors, t => Assert.Contains("sponsor", t.Tags));

    [Fact]
    public void Has_the_m3c_achievements()
    {
        var defs = C.Balance.Achievements!.Value;
        Assert.Equal(11, defs.Length);
        Assert.Equal(Enum.GetValues<AchievementKind>().Order(), defs.Select(d => d.Kind).Distinct().Order());
        Assert.True(defs.Sum(d => d.Points) > C.Balance.AchievementPointsCap);
    }

    [Fact]
    public void Insider_lead_fits_the_insider_start()
        => Assert.Single(CareerFactory.Create(C, Background.IndustryInsider, Difficulty.Normal, PronounSet.Woman, 1).Offers!.Value);

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
