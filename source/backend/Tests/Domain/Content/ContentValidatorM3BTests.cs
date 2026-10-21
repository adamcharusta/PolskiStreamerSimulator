using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Domain.Rulesets;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

/// <summary>M3-B validator rules: sponsor templates, the policy-strike, global-ban and sponsor-offer effects, and the M3-B balance sections.</summary>
public class ContentValidatorM3BTests
{
    private const string RolledChoice = "bold";
    private const string GuaranteedChoice = "calm";

    private static readonly SponsorTemplate TrustedTea = M3BTestContent.TrustedTea;

    private static readonly ChoiceDefinition Pitch = M3BTestContent.Pitch;

    private static ContentSnapshot Valid() => M3BTestContent.Valid();

    private static ContentSnapshot WithSponsor(Func<SponsorTemplate, SponsorTemplate> change)
        => Valid() with { Sponsors = [change(TrustedTea)] };

    private static ContentSnapshot WithBalance(Func<BalanceConstants, BalanceConstants> change)
        => Valid() with { Balance = change(Valid().Balance) };

    private static ChoiceDefinition WithOutcome(ChoiceDefinition choice, string outcome, Effect effect) => choice with
    {
        Outcomes = outcome switch
        {
            "success" => choice.Outcomes with { Success = [effect] },
            "critical-success" => choice.Outcomes with { CriticalSuccess = [effect] },
            "critical-failure" => choice.Outcomes with { CriticalFailure = [effect] },
            _ => choice.Outcomes with { Failure = [effect] },
        },
    };

    /// <summary>The valid content with <paramref name="effect"/> as the only effect of one outcome of an event choice.</summary>
    private static ContentSnapshot WithEventEffect(string eventId, string choiceId, Effect effect, string outcome = "failure")
        => Valid() with
        {
            Events = [.. Valid().Events.Select(e => e.Id == eventId
                ? e with { Choices = [.. e.Choices.Select(ch => ch.Id == choiceId ? WithOutcome(ch, outcome, effect) : ch)] }
                : e)],
        };

    private static ContentSnapshot WithEventEffect(string eventId, int choice, Effect effect, string outcome = "failure")
        => WithEventEffect(eventId, Valid().Events.Single(e => e.Id == eventId).Choices[choice].Id, effect, outcome);

    private static readonly Effect Ban = new(EffectKind.GlobalBan, null, Fixed.Zero);

    [Fact]
    public void M3b_test_content_is_valid() => Assert.Empty(ContentValidator.Validate(Valid()));

    [Fact]
    public void M3_content_and_the_golden_copy_stay_valid()
    {
        Assert.Empty(ContentValidator.Validate(new JsonContentSource(TestPaths.ContentM3).Load()));
        Assert.Empty(ContentValidator.Validate(M3GoldenTests.Content));
    }

    [Fact]
    public void Valid_one_off_sponsor_is_accepted()
    {
        var c = WithSponsor(t => t with
        {
            Kind = SponsorKind.OneOff,
            PaymentPerThousandFollowersPln = 0,
            PaymentCapPln = 300,
            TermWeeks = null,
            DueEveryWeeks = null,
            IncompatibleWith = [SponsorTemplate.AdultBusiness],
            RequiredCategory = Category.Chess,
            Prerequisites = new SponsorPrerequisites(0, null, null, Fixed.FromInt(10)),
        });
        Assert.Empty(ContentValidator.Validate(c));
    }

    // ---- sponsor templates ----

    [Theory]
    [InlineData("version", "sponsor-version:trusted-tea")]
    [InlineData("payment-zero", "sponsor-payment:trusted-tea")]
    [InlineData("per-thousand-negative", "sponsor-payment:trusted-tea")]
    [InlineData("cap-below-payment", "sponsor-payment:trusted-tea")]
    [InlineData("breach-negative", "sponsor-breach:trusted-tea")]
    [InlineData("breach-above-cap", "sponsor-breach:trusted-tea")]
    [InlineData("breach-credibility-low", "sponsor-breach:trusted-tea")]
    [InlineData("breach-credibility-positive", "sponsor-breach:trusted-tea")]
    [InlineData("contract-without-term", "sponsor-term:trusted-tea")]
    [InlineData("contract-without-due", "sponsor-term:trusted-tea")]
    [InlineData("contract-zero-term", "sponsor-term:trusted-tea")]
    [InlineData("contract-zero-due", "sponsor-term:trusted-tea")]
    [InlineData("contract-uneven-term", "sponsor-term:trusted-tea")]
    [InlineData("one-off-with-term", "sponsor-term:trusted-tea")]
    [InlineData("one-off-with-due", "sponsor-term:trusted-tea")]
    [InlineData("min-credibility-above-100", "sponsor-no-path:trusted-tea")]
    [InlineData("max-drama-negative", "sponsor-no-path:trusted-tea")]
    [InlineData("min-drama-above-max", "sponsor-no-path:trusted-tea")]
    [InlineData("min-followers-negative", "sponsor-no-path:trusted-tea")]
    [InlineData("gambling-forbids-gambling", "sponsor-no-path:trusted-tea")]
    [InlineData("incompatible-unknown", "sponsor-incompatible:trusted-tea/alcohol")]
    [InlineData("weight-zero", "sponsor-weight:trusted-tea")]
    [InlineData("name-branch", "text:sponsor:trusted-tea/name:missing-branch:male")]
    [InlineData("pitch-empty", "text:sponsor:trusted-tea/pitch:empty")]
    public void Invalid_sponsor_template_is_reported(string problem, string violation)
    {
        var c = WithSponsor(t => problem switch
        {
            "version" => t with { Version = 0 },
            "payment-zero" => t with { PaymentPln = 0 },
            "per-thousand-negative" => t with { PaymentPerThousandFollowersPln = -1 },
            "cap-below-payment" => t with { PaymentCapPln = 299, Breach = new SponsorBreach(100, Fixed.FromInt(-3)) },
            "breach-negative" => t with { Breach = new SponsorBreach(-1, Fixed.FromInt(-3)) },
            "breach-above-cap" => t with { Breach = new SponsorBreach(1_001, Fixed.FromInt(-3)) },
            "breach-credibility-low" => t with { Breach = new SponsorBreach(200, Fixed.FromInt(-21)) },
            "breach-credibility-positive" => t with { Breach = new SponsorBreach(200, Fixed.FromRatio(1, 10)) },
            "contract-without-term" => t with { TermWeeks = null },
            "contract-without-due" => t with { DueEveryWeeks = null },
            "contract-zero-term" => t with { TermWeeks = 0 },
            "contract-zero-due" => t with { DueEveryWeeks = 0 },
            "contract-uneven-term" => t with { TermWeeks = 10 },
            "one-off-with-term" => t with { Kind = SponsorKind.OneOff, DueEveryWeeks = null },
            "one-off-with-due" => t with { Kind = SponsorKind.OneOff, TermWeeks = null },
            "min-credibility-above-100" => t with { Prerequisites = t.Prerequisites with { MinCredibility = Fixed.FromInt(101) } },
            "max-drama-negative" => t with { Prerequisites = t.Prerequisites with { MaxDrama = Fixed.FromInt(-1) } },
            "min-drama-above-max" => t with { Prerequisites = t.Prerequisites with { MinDrama = Fixed.FromInt(41) } },
            "min-followers-negative" => t with { Prerequisites = t.Prerequisites with { MinFollowers = -1 } },
            "gambling-forbids-gambling" => t with { RequiredCategory = Category.Gambling },
            "incompatible-unknown" => t with { IncompatibleWith = [SponsorTemplate.Gambling, "alcohol"] },
            "weight-zero" => t with { Weight = 0 },
            "name-branch" => t with { Name = "{gender, select, female {a} other {c}}" },
            _ => t with { Pitch = "" },
        });
        Assert.Contains(violation, ContentValidator.Validate(c));
    }

    [Fact]
    public void Duplicate_sponsor_template_is_reported()
    {
        var c = Valid() with { Sponsors = [TrustedTea, TrustedTea with { Version = 2 }] };
        var violations = ContentValidator.Validate(c);
        Assert.Contains("sponsor-duplicate:trusted-tea", violations);
        Assert.Single(violations, v => v.StartsWith("sponsor-duplicate:", StringComparison.Ordinal));
    }

    [Fact]
    public void Breach_larger_than_the_payment_cap_is_rejected()
        => Assert.Contains("sponsor-breach:trusted-tea", ContentValidator.Validate(WithSponsor(t => t with { Breach = new SponsorBreach(1_001, Fixed.FromInt(-3)) })));

    [Fact]
    public void Gambling_sponsor_that_forbids_gambling_has_no_path()
        => Assert.Contains("sponsor-no-path:trusted-tea", ContentValidator.Validate(WithSponsor(t => t with { RequiredCategory = Category.Gambling })));

    // ---- effects ----

    private static readonly Dictionary<string, Effect> InvalidM3bEffects = new(StringComparer.Ordinal)
    {
        ["strike-zero"] = new Effect(EffectKind.PolicyStrike, null, Fixed.Zero),
        ["strike-three"] = new Effect(EffectKind.PolicyStrike, null, Fixed.FromInt(3)),
        ["strike-fraction"] = new Effect(EffectKind.PolicyStrike, null, Fixed.FromRatio(3, 2)),
        ["strike-target"] = new Effect(EffectKind.PolicyStrike, null, Fixed.One, Target: "x"),
        ["strike-stat"] = new Effect(EffectKind.PolicyStrike, StatKey.Drama, Fixed.One),
        ["ban-amount"] = new Effect(EffectKind.GlobalBan, null, Fixed.One),
        ["ban-weeks"] = new Effect(EffectKind.GlobalBan, null, Fixed.Zero, Weeks: 1),
        ["ban-category"] = new Effect(EffectKind.GlobalBan, null, Fixed.Zero, Category.Fps),
        ["offer-no-target"] = new Effect(EffectKind.SponsorOffer, null, Fixed.Zero),
        ["offer-amount"] = new Effect(EffectKind.SponsorOffer, null, Fixed.One, Target: "trusted-tea"),
        ["offer-weeks"] = new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "trusted-tea", Weeks: 2),
    };

    public static TheoryData<string> InvalidM3bEffectNames() => [.. InvalidM3bEffects.Keys];

    [Theory]
    [MemberData(nameof(InvalidM3bEffectNames))]
    public void Malformed_m3b_effect_is_rejected(string name)
    {
        var c = WithEventEffect("test-crisis", RolledChoice, InvalidM3bEffects[name]);
        Assert.Contains($"effect-invalid:test-crisis/{RolledChoice}", ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Whole_policy_strike_of_one_or_two_is_valid(int amount)
    {
        var c = WithEventEffect("tech-failure", 0, new Effect(EffectKind.PolicyStrike, null, Fixed.FromInt(amount)));
        Assert.Empty(ContentValidator.Validate(c));
    }

    [Fact]
    public void Sponsor_offer_naming_a_known_template_is_valid()
    {
        var c = WithEventEffect("tech-failure", 0, new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "trusted-tea"), "success");
        Assert.Empty(ContentValidator.Validate(c));
    }

    [Fact]
    public void Sponsor_offer_naming_an_unknown_template_is_reported()
    {
        var c = WithEventEffect("tech-failure", 0, new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "nope"), "success");
        Assert.Contains("unknown-sponsor:tech-failure/nope", ContentValidator.Validate(c));
    }

    [Fact]
    public void Sponsor_offer_in_the_network_choice_is_checked()
    {
        var network = WithOutcome(Valid().Balance.NetworkCollaborate!, "success", new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "nope"));
        var c = WithBalance(b => b with { NetworkCollaborate = network });
        Assert.Contains($"unknown-sponsor:{ContentValidator.NetworkChoiceOwner}/nope", ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData(EffectKind.PolicyStrike, 1)]
    [InlineData(EffectKind.GlobalBan, 0)]
    public void Schema_one_rejects_m3b_effect_kinds(EffectKind kind, int amount)
    {
        var m1 = TestContent.Valid();
        var e = m1.Events[0];
        var changed = m1 with
        {
            Events = m1.Events.SetItem(0, e with { Choices = e.Choices.SetItem(0, WithOutcome(e.Choices[0], "failure", new Effect(kind, null, Fixed.FromInt(amount)))) }),
        };
        Assert.Contains($"effect-invalid:{e.Id}/{e.Choices[0].Id}", ContentValidator.Validate(changed));
    }

    // ---- global ban placement ----

    [Fact]
    public void Global_ban_on_a_live_event_is_rejected()
    {
        var c = WithEventEffect("tech-failure", 0, Ban);
        Assert.Contains("global-ban-placement:tech-failure/" + c.Events.First(e => e.Id == "tech-failure").Choices[0].Id, ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("critical-failure")]
    public void Global_ban_on_a_crisis_failure_of_a_rolled_choice_is_valid(string outcome)
    {
        var c = WithEventEffect("test-crisis", RolledChoice, Ban, outcome);
        Assert.Contains(Ban, AllEffects(c.Events.Single(e => e.Id == "test-crisis").Choices.Single(ch => ch.Id == RolledChoice)));
        Assert.DoesNotContain(ContentValidator.Validate(c), v => v.StartsWith("global-ban-placement", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("test-crisis", RolledChoice, "success")]
    [InlineData("test-crisis", RolledChoice, "critical-success")]
    [InlineData("test-crisis", GuaranteedChoice, "failure")]
    [InlineData("first-week", RolledChoice, "failure")]
    public void Global_ban_outside_a_crisis_failure_of_a_rolled_choice_is_rejected(string eventId, string choiceId, string outcome)
    {
        var c = WithEventEffect(eventId, choiceId, Ban, outcome);
        Assert.Contains($"global-ban-placement:{eventId}/{choiceId}", ContentValidator.Validate(c));
    }

    [Fact]
    public void Global_ban_in_the_network_choice_is_rejected()
    {
        var network = WithOutcome(Valid().Balance.NetworkCollaborate!, "failure", Ban);
        var c = WithBalance(b => b with { NetworkCollaborate = network });
        Assert.Contains($"global-ban-placement:{ContentValidator.NetworkChoiceOwner}/collaborate", ContentValidator.Validate(c));
    }

    [Fact]
    public void Global_ban_in_the_pitch_choice_is_rejected()
    {
        var c = WithBalance(b => b with { Sponsors = b.Sponsors! with { Pitch = WithOutcome(Pitch, "failure", Ban) } });
        var violations = ContentValidator.Validate(c);
        Assert.Contains($"global-ban-placement:{ContentValidator.PitchChoiceOwner}/pitch", violations);
        Assert.Contains("balance:pitch", violations);
    }

    private static IEnumerable<Effect> AllEffects(ChoiceDefinition choice)
        => choice.Outcomes.Success.Concat(choice.Outcomes.Failure).Concat(choice.Outcomes.CriticalSuccess ?? []).Concat(choice.Outcomes.CriticalFailure ?? []);

    // ---- balance sections ----

    private static BalanceConstants WithPlatform(BalanceConstants b, Platform platform, Func<PlatformProfile, PlatformProfile> change)
        => b with { Platforms = b.Platforms!.SetItem(platform, change(b.Platforms[platform])) };

    [Theory]
    [InlineData("platforms:missing", "balance:platforms")]
    [InlineData("platforms:reach", "balance:platforms")]
    [InlineData("platforms:sub-yield", "balance:platforms")]
    [InlineData("platforms:ad-yield", "balance:platforms")]
    [InlineData("platforms:category-reach", "balance:platforms")]
    [InlineData("platforms:risk", "balance:platforms")]
    [InlineData("migration:order", "balance:migration")]
    [InlineData("migration:min-negative", "balance:migration")]
    [InlineData("migration:max-above-one", "balance:migration")]
    [InlineData("migration:credibility-divisor", "balance:migration")]
    [InlineData("migration:drama-divisor", "balance:migration")]
    [InlineData("migration:jitter", "balance:migration")]
    [InlineData("policy:cap-above-one", "balance:policy")]
    [InlineData("policy:cap-negative", "balance:policy")]
    [InlineData("policy:strikes-to-ban", "balance:policy")]
    [InlineData("policy:strike-weeks", "balance:policy")]
    public void Unsafe_platform_section_is_reported(string problem, string violation)
    {
        var c = WithBalance(b => problem switch
        {
            "platforms:missing" => b with { Platforms = b.Platforms!.Remove(Platform.Nurt) },
            "platforms:reach" => WithPlatform(b, Platform.Nurt, p => p with { Reach = Fixed.Zero }),
            "platforms:sub-yield" => WithPlatform(b, Platform.Nurt, p => p with { SubYield = Fixed.Zero }),
            "platforms:ad-yield" => WithPlatform(b, Platform.Nurt, p => p with { AdYield = -Fixed.One }),
            "platforms:category-reach" => WithPlatform(b, Platform.Nurt, p => p with { CategoryReach = p.CategoryReach.With(Category.Moba, Fixed.Zero) }),
            "platforms:risk" => WithPlatform(b, Platform.Nurt, p => p with { PolicyRisk = Fixed.FromRatio(-1, 100) }),
            "migration:order" => b with { Migration = b.Migration! with { RetentionMin = Fixed.FromRatio(9, 10) } },
            "migration:min-negative" => b with { Migration = b.Migration! with { RetentionMin = Fixed.FromRatio(-1, 100) } },
            "migration:max-above-one" => b with { Migration = b.Migration! with { RetentionMax = Fixed.FromRatio(101, 100) } },
            "migration:credibility-divisor" => b with { Migration = b.Migration! with { CredibilityDivisor = Fixed.Zero } },
            "migration:drama-divisor" => b with { Migration = b.Migration! with { DramaDivisor = -Fixed.One } },
            "migration:jitter" => b with { Migration = b.Migration! with { Jitter = Fixed.FromRatio(-1, 100) } },
            "policy:cap-above-one" => b with { Policy = b.Policy! with { ChanceCap = Fixed.FromRatio(11, 10) } },
            "policy:cap-negative" => b with { Policy = b.Policy! with { ChanceCap = Fixed.FromRatio(-1, 100) } },
            "policy:strikes-to-ban" => b with { Policy = b.Policy! with { StrikesToBan = 0 } },
            _ => b with { Policy = b.Policy! with { StrikeWeeks = 0 } },
        });
        Assert.Contains(violation, ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData("wagers:missing-stake", "balance:wagers")]
    [InlineData("wagers:missing-exposure", "balance:wagers")]
    [InlineData("wagers:stake", "balance:wagers")]
    [InlineData("wagers:chance", "balance:wagers")]
    [InlineData("wagers:payout", "balance:wagers")]
    [InlineData("wagers:share-zero", "balance:wagers")]
    [InlineData("wagers:share-above-one", "balance:wagers")]
    [InlineData("side-business:from-week-zero", "balance:side-business")]
    [InlineData("side-business:from-week-late", "balance:side-business")]
    [InlineData("side-business:quality-per-work", "balance:side-business")]
    [InlineData("side-business:quality-decay", "balance:side-business")]
    [InlineData("sponsors:base", "balance:sponsors")]
    [InlineData("sponsors:per-magnitude", "balance:sponsors")]
    [InlineData("sponsors:cap", "balance:sponsors")]
    [InlineData("sponsors:base-above-cap", "balance:sponsors")]
    [InlineData("sponsors:max-contracts", "balance:sponsors")]
    [InlineData("sponsors:misses", "balance:sponsors")]
    [InlineData("pitch:cash", "balance:pitch")]
    [InlineData("pitch:critical", "balance:pitch")]
    public void Unsafe_economy_section_is_reported(string problem, string violation)
    {
        var c = WithBalance(b => problem switch
        {
            "wagers:missing-stake" => b with { Wagers = b.Wagers! with { StakePln = b.Wagers.StakePln.Remove(WagerTier.High) } },
            "wagers:missing-exposure" => b with { Wagers = b.Wagers! with { Exposure = b.Wagers.Exposure.Remove(WagerTier.Low) } },
            "wagers:stake" => b with { Wagers = b.Wagers! with { StakePln = b.Wagers.StakePln.SetItem(WagerTier.Low, 0) } },
            "wagers:chance" => b with { Wagers = b.Wagers! with { Chance = Fixed.FromRatio(11, 10) } },
            "wagers:payout" => b with { Wagers = b.Wagers! with { PayoutMultiple = Fixed.FromRatio(9, 10) } },
            "wagers:share-zero" => b with { Wagers = b.Wagers! with { MaxShareOfCash = Fixed.Zero } },
            "wagers:share-above-one" => b with { Wagers = b.Wagers! with { MaxShareOfCash = Fixed.FromRatio(11, 10) } },
            "side-business:from-week-zero" => b with { SideBusiness = b.SideBusiness! with { FromWeek = 0 } },
            "side-business:from-week-late" => b with { SideBusiness = b.SideBusiness! with { FromWeek = 1041 } },
            "side-business:quality-per-work" => b with { SideBusiness = b.SideBusiness! with { QualityPerWork = Fixed.FromInt(101) } },
            "side-business:quality-decay" => b with { SideBusiness = b.SideBusiness! with { QualityDecay = -Fixed.One } },
            "sponsors:base" => b with { Sponsors = b.Sponsors! with { OfferBase = Fixed.FromRatio(-1, 100) } },
            "sponsors:per-magnitude" => b with { Sponsors = b.Sponsors! with { OfferPerMagnitude = Fixed.FromRatio(11, 10) } },
            "sponsors:cap" => b with { Sponsors = b.Sponsors! with { OfferCap = Fixed.FromRatio(11, 10) } },
            "sponsors:base-above-cap" => b with { Sponsors = b.Sponsors! with { OfferBase = Fixed.FromRatio(21, 100) } },
            "sponsors:max-contracts" => b with { Sponsors = b.Sponsors! with { MaxContracts = 0 } },
            "sponsors:misses" => b with { Sponsors = b.Sponsors! with { MissesToTerminate = 0 } },
            "pitch:cash" => b with { Sponsors = b.Sponsors! with { Pitch = WithOutcome(Pitch, "success", new Effect(EffectKind.CashDelta, null, Fixed.FromInt(100))) } },
            _ => b with { Sponsors = b.Sponsors! with { Pitch = Pitch with { AllowsCritical = true } } },
        });
        Assert.Contains(violation, ContentValidator.Validate(c));
    }

    [Fact]
    public void Pitch_choice_is_checked_like_an_event_choice()
    {
        var pitch = Pitch with { LabelPl = "{gender, select, female {a} male {b}}", BaseChance = Fixed.FromRatio(11, 10) };
        var violations = ContentValidator.Validate(WithBalance(b => b with { Sponsors = b.Sponsors! with { Pitch = pitch } }));
        Assert.Contains($"text:{ContentValidator.PitchChoiceOwner}/pitch.label:missing-branch:other", violations);
        Assert.Contains($"chance-range:{ContentValidator.PitchChoiceOwner}/pitch", violations);
    }

    [Fact]
    public void Starting_lead_naming_an_unknown_template_is_reported()
    {
        var c = WithBalance(b => b with { Sponsors = b.Sponsors! with { StartingLeads = b.Sponsors.StartingLeads.SetItem(Background.ChessClubRegular, "nope") } });
        Assert.Contains("missing-lead:ChessClubRegular/nope", ContentValidator.Validate(c));
    }

    [Fact]
    public void Starting_lead_the_background_cannot_accept_is_reported()
    {
        var c = WithBalance(b => b with { Sponsors = b.Sponsors! with { StartingLeads = b.Sponsors.StartingLeads.SetItem(Background.DormRoomNewcomer, TrustedTea.Id) } });
        var violations = ContentValidator.Validate(c);
        Assert.Contains("lead-unmet:DormRoomNewcomer/trusted-tea", violations);
        Assert.DoesNotContain("lead-unmet:IndustryInsider/trusted-tea", violations);
    }

    [Fact]
    public void Starting_lead_failing_a_drama_bound_is_reported()
    {
        var c = WithSponsor(t => t with { Prerequisites = t.Prerequisites with { MinDrama = Fixed.FromInt(20) } });
        Assert.Contains("lead-unmet:IndustryInsider/trusted-tea", ContentValidator.Validate(c));
    }

    [Fact]
    public void Platform_ban_texts_are_checked_as_icu()
    {
        var ban = Valid().Balance.PlatformBan!;
        var c = WithBalance(b => b with
        {
            PlatformBan = ban with { Title = "{gender, select, female {a} other {c}}", Choices = ban.Choices.SetItem(Platform.Nurt, "") },
        });
        var violations = ContentValidator.Validate(c);
        Assert.Contains("text:platformBan/title:missing-branch:male", violations);
        Assert.Contains("text:platformBan/choices.Nurt:empty", violations);
    }

    [Fact]
    public void Platform_without_a_ban_choice_label_is_reported()
    {
        var ban = Valid().Balance.PlatformBan!;
        var c = WithBalance(b => b with { PlatformBan = ban with { Choices = ban.Choices.Remove(Platform.PulsLive) } });
        Assert.Contains("balance:platform-ban", ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData("platform-ban")]
    [InlineData("migration")]
    public void Platforms_without_the_ban_decision_or_migration_are_reported(string missing)
    {
        var c = WithBalance(b => missing == "migration" ? b with { Migration = null } : b with { PlatformBan = null });
        Assert.Contains("balance:platform-ban-missing", ContentValidator.Validate(c));
    }

    [Fact]
    public void Side_business_flag_is_set_by_the_engine()
    {
        Assert.Contains("side-business", (IReadOnlySet<string>)ContentValidator.EngineFlags);
        var c = Valid() with
        {
            Events = [.. Valid().Events.Select(e => e.Id == "tech-failure" ? e with { Eligibility = e.Eligibility with { RequiresFlags = ["side-business"] } } : e)],
        };
        Assert.DoesNotContain(ContentValidator.Validate(c), v => v.StartsWith("unset-flag:", StringComparison.Ordinal));
    }
}
