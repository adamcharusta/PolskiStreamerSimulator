using System.Text.Json.Nodes;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure;

public sealed class JsonContentSourceTests : IDisposable
{
    private readonly string tempDirectory = Directory.CreateTempSubdirectory("pss-content-").FullName;

    public void Dispose() => Directory.Delete(tempDirectory, recursive: true);

    private string CopyContent(Func<string, string>? editBalance = null, Func<string, string>? editEvents = null)
    {
        var balance = File.ReadAllText(Path.Combine(TestPaths.ContentM1, "balance.json"));
        var events = File.ReadAllText(Path.Combine(TestPaths.ContentM1, "events.json"));
        File.WriteAllText(Path.Combine(tempDirectory, "balance.json"), editBalance?.Invoke(balance) ?? balance);
        File.WriteAllText(Path.Combine(tempDirectory, "events.json"), editEvents?.Invoke(events) ?? events);
        return tempDirectory;
    }

    [Fact]
    public void Repository_content_is_valid()
    {
        var c = new JsonContentSource(TestPaths.ContentM1).Load();
        Assert.Empty(ContentValidator.Validate(c));
        Assert.Matches("^[0-9a-f]{64}$", c.Digest);
        Assert.Equal(5, c.Events.Length);
        Assert.Equal("m1.1", c.Version);
    }

    [Fact]
    public void Digest_is_stable_for_identical_files()
        => Assert.Equal(new JsonContentSource(TestPaths.ContentM1).Load().Digest, new JsonContentSource(CopyContent()).Load().Digest);

    [Fact]
    public void Digest_changes_when_balance_changes()
    {
        var original = new JsonContentSource(TestPaths.ContentM1).Load();
        var edited = new JsonContentSource(CopyContent(b => b.Replace("\"livingCostPln\": 100", "\"livingCostPln\": 101", StringComparison.Ordinal))).Load();
        Assert.Equal(101, edited.Balance.LivingCostPln);
        Assert.NotEqual(original.Digest, edited.Digest);
    }

    [Fact]
    public void Decimal_values_parse_exactly()
    {
        var b = new JsonContentSource(TestPaths.ContentM1).Load().Balance;
        Assert.Equal(350_000, b.QualitySkill.Raw);
        Assert.Equal(100, b.ChurnBoredomFactor.Raw);
        Assert.Equal(1_100_000, b.DifficultyMultiplier[Difficulty.Normal].Raw);
        Assert.Equal(-1_000, b.InsolvencyThresholdPln);
        Assert.Equal([0L, 600, 1_500, 3_500, 8_000], b.EquipmentPricePln);
    }

    [Fact]
    public void Profiles_are_loaded_for_every_background()
    {
        var c = new JsonContentSource(TestPaths.ContentM1).Load();
        var dorm = c.Profiles[Background.DormRoomNewcomer];
        Assert.Equal(2_000, dorm.CashPln);
        Assert.Equal(Fixed.FromInt(18), dorm.Skill[Category.Fps]);
        Assert.Equal(Fixed.One, dorm.ScoreMultiplier);
        Assert.Equal(Fixed.FromRatio(90, 100), c.Profiles[Background.IndustryInsider].ScoreMultiplier);
        Assert.Equal(2, c.Profiles[Background.IndustryInsider].Equipment[EquipmentSlot.Camera]);
    }

    [Fact]
    public void Choices_modifiers_and_effects_are_loaded()
    {
        var raid = new JsonContentSource(TestPaths.ContentM1).Load().Events.Single(e => e.Id == "chat-raid");
        var welcome = raid.Choices.Single(c => c.Id == "welcome");
        Assert.False(welcome.Guaranteed);
        Assert.Equal(Fixed.FromRatio(55, 100), welcome.BaseChance);
        Assert.Equal(StatKey.Credibility, welcome.Modifiers.Single().Stat);
        Assert.Equal(Fixed.FromRatio(2, 1_000), welcome.Modifiers.Single().PerPoint);
        Assert.True(raid.Choices.Single(c => c.Id == "ignore").Guaranteed);
        Assert.NotEmpty(welcome.Outcomes.Success);
    }

    [Fact]
    public void More_than_six_decimals_are_rejected()
    {
        var dir = CopyContent(b => b.Replace("\"qualitySkill\": 0.35", "\"qualitySkill\": 0.3500001", StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => new JsonContentSource(dir).Load());
        Assert.Contains("qualitySkill", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_content_lists_validator_codes()
    {
        var dir = CopyContent(editEvents: e => e.Replace("\"weight\": 3", "\"weight\": 0", StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => new JsonContentSource(dir).Load());
        Assert.Contains("weight-nonpositive:chat-raid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parsing_the_file_bytes_gives_the_loaded_snapshot()
    {
        var parsed = JsonContentSource.Parse(TestContentFiles.Balance, TestContentFiles.Events, "bytes");
        var loaded = new JsonContentSource(TestPaths.ContentM1).Load();

        Assert.Equal(loaded.Digest, parsed.Digest);
        Assert.Equal(loaded.Events.Select(e => e.Id), parsed.Events.Select(e => e.Id));
    }

    [Fact]
    public void Any_byte_change_changes_the_digest()
    {
        var padded = TestContentFiles.Balance.Concat("\n"u8.ToArray()).ToArray();

        Assert.NotEqual(
            JsonContentSource.Parse(TestContentFiles.Balance, TestContentFiles.Events, "a").Digest,
            JsonContentSource.Parse(padded, TestContentFiles.Events, "b").Digest);
    }

    [Fact]
    public void The_stored_m1_1_snapshot_still_parses_to_its_digest()
    {
        // Careers pin the digest of the content they started with, and stored snapshots are re-parsed on load,
        // so the reader must keep accepting this frozen copy of the first published content (TD-91).
        var balance = File.ReadAllBytes(TestPaths.Golden(Path.Combine("content-m1.1", JsonContentSource.BalanceFile)));
        var events = File.ReadAllBytes(TestPaths.Golden(Path.Combine("content-m1.1", JsonContentSource.EventsFile)));

        var snapshot = JsonContentSource.Parse(balance, events, "frozen content m1.1");

        Assert.Equal("37e4f6d0a7a4c4a175b0383613b8fe5dfc5393bca08c94dca76c126b054c4bb2", snapshot.Digest);
    }

    [Fact]
    public void Invalid_bytes_name_their_source()
    {
        var error = Assert.Throws<InvalidDataException>(
            () => JsonContentSource.Parse(TestContentFiles.Balance, """{"events":[]}"""u8.ToArray(), "stored snapshot x"));

        Assert.Contains("'stored snapshot x'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_two_event_reads_icu_text_phase_and_flags()
    {
        var events = """
            { "schema": 2, "events": [ {
              "id": "x", "version": 2, "phase": "crisis",
              "title": "T", "text": "{gender, select, female {a} male {b} other {c}}",
              "weight": 1, "oneTime": true, "tags": ["rivalry"],
              "eligibility": { "fromWeek": 1, "toWeek": 1040, "categories": ["Chess"], "requiresFlags": ["f"] },
              "choices": [ { "id": "ok", "label": "L", "guaranteed": true,
                "outcomes": { "success": [ { "kind": "SetFlag", "flag": "f", "weeks": 4, "amount": 0 } ] } } ] } ] }
            """u8.ToArray();
        var e = JsonContentSource.ParseEvents(events).Single();
        Assert.Equal((2, EventPhase.Crisis, "T", 13, true), (e.Version, e.Phase, e.Title, e.CooldownWeeks, e.OneTime));
        Assert.Equal([Category.Chess], e.Eligibility.Categories);
        Assert.Equal(("f", 4), (e.Choices[0].Outcomes.Success[0].Target, e.Choices[0].Outcomes.Success[0].Weeks));
    }

    [Fact]
    public void Schema_two_choice_reads_label_texts_requirements_and_effect_targets()
    {
        var events = """
            { "schema": 2, "events": [ {
              "id": "x", "version": 1, "phase": "live", "title": "T", "text": "Tekst",
              "weight": 1, "cooldownWeeks": 4,
              "eligibility": { "fromWeek": 1, "toWeek": 1040, "forbidsFlags": ["g"] },
              "choices": [ { "id": "ok", "label": "L", "baseChance": 0.5, "successText": "S", "failureText": "F",
                "requires": { "minCashPln": 500, "minEnergy": 12.5 },
                "outcomes": {
                  "success": [ { "kind": "FollowUp", "event": "y", "weeks": 2, "amount": 0 },
                               { "kind": "SkillDelta", "category": "Fps", "amount": 3 } ],
                  "failure": [ { "kind": "ClearFlag", "flag": "g", "amount": 0 } ] } } ] } ] }
            """u8.ToArray();
        var e = JsonContentSource.ParseEvents(events).Single();
        var choice = e.Choices[0];
        Assert.Equal((EventPhase.Live, 4, false, "Tekst"), (e.Phase, e.CooldownWeeks, e.OneTime, e.TextPl));
        Assert.Equal(["g"], e.Eligibility.ForbidsFlags);
        Assert.Equal((null, "L", "S", "F"), (choice.LabelKey, choice.LabelPl, choice.SuccessText, choice.FailureText));
        Assert.Equal(new ChoiceRequirement(500, Fixed.FromRatio(25, 2)), choice.Requires);
        Assert.Equal(new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "y", Weeks: 2), choice.Outcomes.Success[0]);
        Assert.Equal(new Effect(EffectKind.SkillDelta, null, Fixed.FromInt(3), Category.Fps), choice.Outcomes.Success[1]);
        Assert.Equal("g", choice.Outcomes.Failure[0].Target);
    }

    [Fact]
    public void Schema_one_content_defaults_to_schema_one_and_the_m1_ruleset()
    {
        var c = new JsonContentSource(TestPaths.ContentM1).Load();
        Assert.Equal((1, "m1-prototype.1"), (c.Schema, c.RulesetId));
        Assert.All(c.Events, e => Assert.Equal((1, EventPhase.Live, false), (e.Version, e.Phase, e.OneTime)));
        Assert.Null(c.Balance.RecoveryCrisisEventId);
        Assert.Equal(Fixed.Zero, c.Balance.DecisionChancePerWeek);
    }

    [Fact]
    public void Balance_reads_ruleset_and_m3_settings()
    {
        var dir = CopyContent(b => b.Replace(
            "\"eventsPerWeekMin\": 2,",
            """
            "eventsPerWeekMin": 2, "ruleset": "m1-prototype.1", "recoveryCrisisEventId": "chat-raid",
            "decisionChancePerWeek": 0.06, "networkEnergyCost": 15,
            """,
            StringComparison.Ordinal));
        var b = new JsonContentSource(dir).Load().Balance;
        Assert.Equal(("chat-raid", Fixed.FromRatio(6, 100), Fixed.FromInt(15)), (b.RecoveryCrisisEventId, b.DecisionChancePerWeek, b.NetworkEnergyCost));
        Assert.Null(b.NetworkCollaborate);
    }

    [Fact]
    public void Schema_one_content_rejects_a_network_collaboration()
    {
        var dir = CopyContent(b => b.Replace(
            "\"eventsPerWeekMin\": 2,",
            """
            "eventsPerWeekMin": 2,
            "networkCollaborate": { "id": "network", "label": "Współpraca", "guaranteed": true,
              "outcomes": { "success": [ { "kind": "StatDelta", "stat": "Credibility", "amount": 2 } ] } },
            """,
            StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => new JsonContentSource(dir).Load());
        Assert.Contains("balance.json.networkCollaborate", error.Message, StringComparison.Ordinal);
    }

    private const string TrustedTea = """
        [{ "id": "trusted-tea", "version": 1, "name": "Herbata Spokój", "pitch": "Spokojna marka szuka spokojnego kanału.",
           "kind": "contract", "paymentPln": 300, "paymentPerThousandFollowersPln": 20, "paymentCapPln": 1000,
           "termWeeks": 12, "dueEveryWeeks": 4, "platforms": [], "prerequisites": { "minFollowers": 1000, "minCredibility": 40, "maxDrama": 40 },
           "incompatibleWith": ["gambling"], "breach": { "cashPln": 200, "credibility": -3 }, "weight": 3 }]
        """;

    /// <summary>The schema-2 test events with a <c>sponsors</c> array.</summary>
    private static byte[] SchemaTwoWith(string sponsors) => WithSponsors(TestContentFiles.SchemaTwoEvents(), sponsors);

    /// <summary>The schema-1 repository events with the same <c>sponsors</c> array, which schema 1 does not allow.</summary>
    private static byte[] SchemaOneWithSponsors() => WithSponsors(TestContentFiles.Events, TrustedTea);

    private static byte[] WithSponsors(byte[] events, string sponsors)
    {
        var root = JsonNode.Parse(events)!.AsObject();
        root["sponsors"] = JsonNode.Parse(sponsors);
        return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] OneLiveEvent(string eligibility) => System.Text.Encoding.UTF8.GetBytes($$"""
        { "schema": 2, "events": [ {
          "id": "x", "version": 1, "phase": "live", "title": "T", "text": "t", "weight": 1,
          "eligibility": { "fromWeek": 1, "toWeek": 1040, {{eligibility}} },
          "choices": [ { "id": "ok", "label": "L", "guaranteed": true, "outcomes": {} } ] } ] }
        """);

    [Fact]
    public void Schema_two_reads_sponsor_templates()
    {
        var events = SchemaTwoWith(sponsors: TrustedTea);
        var c = JsonContentSource.Parse(TestContentFiles.Balance, events, "test");
        var t = Assert.Single(c.Sponsors);
        Assert.Equal(("trusted-tea", 1, "Herbata Spokój", 3), (t.Id, t.Version, t.Name, t.Weight));
        Assert.Equal((SponsorKind.Contract, 300L, 20L, 1000L, 12, 4), (t.Kind, t.PaymentPln, t.PaymentPerThousandFollowersPln, t.PaymentCapPln, t.TermWeeks, t.DueEveryWeeks));
        Assert.Equal(1000L, t.Prerequisites.MinFollowers);
        Assert.Equal(Fixed.FromInt(40), t.Prerequisites.MinCredibility);
        Assert.Equal(Fixed.FromInt(40), t.Prerequisites.MaxDrama);
        Assert.Null(t.Prerequisites.MinDrama);
        Assert.Null(t.RequiredCategory);
        Assert.True(t.Platforms.IsEmpty);
        Assert.True(t.Tags.IsEmpty);
        Assert.Equal(["gambling"], t.IncompatibleWith.ToArray());
        Assert.Equal(new SponsorBreach(200, Fixed.FromInt(-3)), t.Breach);
    }

    [Fact]
    public void Schema_two_reads_a_one_off_sponsor_with_its_optional_fields()
    {
        var events = SchemaTwoWith(sponsors: """
            [{ "id": "hype-drink", "version": 2, "name": "N", "pitch": "P", "kind": "oneOff",
               "paymentPln": 500, "paymentPerThousandFollowersPln": 0, "paymentCapPln": 500, "requiredCategory": "Fps",
               "platforms": ["PulsLive", "Nurt"], "prerequisites": { "minDrama": 10 }, "breach": { "cashPln": 0, "credibility": 0 },
               "weight": 1, "tags": ["sponsor"] }]
            """);
        var t = Assert.Single(JsonContentSource.Parse(TestContentFiles.Balance, events, "test").Sponsors);
        Assert.Equal((SponsorKind.OneOff, (int?)null, (int?)null, Category.Fps), (t.Kind, t.TermWeeks, t.DueEveryWeeks, t.RequiredCategory));
        Assert.Equal([Platform.PulsLive, Platform.Nurt], t.Platforms.ToArray());
        Assert.Equal(new SponsorPrerequisites(0, null, null, Fixed.FromInt(10)), t.Prerequisites);
        Assert.True(t.IncompatibleWith.IsEmpty);
        Assert.Equal(["sponsor"], t.Tags.ToArray());
    }

    [Fact]
    public void Content_without_sponsors_has_none()
    {
        Assert.True(JsonContentSource.Parse(TestContentFiles.Balance, TestContentFiles.SchemaTwoEvents(), "test").Sponsors.IsEmpty);
        Assert.True(new JsonContentSource(TestPaths.ContentM1).Load().Sponsors.IsEmpty);
    }

    [Fact]
    public void Sponsors_are_rejected_in_schema_one()
    {
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(TestContentFiles.Balance, SchemaOneWithSponsors(), "test"));
        Assert.Contains("events.json.sponsors", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_sponsor_kind_is_rejected()
    {
        var events = SchemaTwoWith(sponsors: TrustedTea.Replace("\"contract\"", "\"barter\"", StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(TestContentFiles.Balance, events, "test"));
        Assert.Contains("unknown SponsorKind 'Barter'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Eligibility_reads_platforms_and_min_strikes()
    {
        var e = JsonContentSource.ParseEvents(OneLiveEvent("\"platforms\": [\"Nurt\"], \"minStrikes\": 1")).Single();
        Assert.Equal([Platform.Nurt], e.Eligibility.Platforms.ToArray());
        Assert.Equal(1, e.Eligibility.MinStrikes);
    }

    [Fact]
    public void Eligibility_without_platforms_allows_every_platform_and_needs_no_strikes()
    {
        var e = JsonContentSource.ParseEvents(OneLiveEvent("\"categories\": []")).Single();
        Assert.True(e.Eligibility.Platforms.IsEmpty);
        Assert.Equal(0, e.Eligibility.MinStrikes);
    }

    [Fact]
    public void Unknown_platform_in_eligibility_is_rejected()
    {
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.ParseEvents(OneLiveEvent("\"platforms\": [\"Kick\"]")));
        Assert.Contains("unknown Platform 'Kick'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Effects_read_the_m3b_kinds_and_a_sponsor_target()
    {
        var events = System.Text.Encoding.UTF8.GetBytes("""
            { "schema": 2, "events": [ {
              "id": "x", "version": 1, "phase": "crisis", "title": "T", "text": "t", "weight": 1,
              "eligibility": { "fromWeek": 1, "toWeek": 1040 },
              "choices": [ { "id": "ok", "label": "L", "baseChance": 0.5,
                "outcomes": {
                  "success": [ { "kind": "SponsorOffer", "sponsor": "trusted-tea", "amount": 0 } ],
                  "failure": [ { "kind": "PolicyStrike", "amount": 2 }, { "kind": "GlobalBan", "amount": 0 } ] } } ] } ] }
            """);
        var outcomes = JsonContentSource.ParseEvents(events).Single().Choices[0].Outcomes;
        Assert.Equal(new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "trusted-tea"), outcomes.Success[0]);
        Assert.Equal(new Effect(EffectKind.PolicyStrike, null, Fixed.FromInt(2)), outcomes.Failure[0]);
        Assert.Equal(new Effect(EffectKind.GlobalBan, null, Fixed.Zero), outcomes.Failure[1]);
    }

    /// <summary>The seven M3-B balance sections with the proposed values.</summary>
    private const string M3bSections = """
        {
          "platforms": {
            "Czatnia": { "reach": 1.00, "subYield": 1.00, "adYield": 1.00, "policyRisk": 1.00 },
            "PulsLive": { "reach": 1.20, "subYield": 0.85, "adYield": 1.10, "policyRisk": 1.30, "categoryReach": { "Fps": 1.10, "Moba": 1.10, "Chess": 0.90 } },
            "Nurt": { "reach": 0.85, "subYield": 1.25, "adYield": 0.70, "policyRisk": 0.80, "categoryReach": { "Chess": 1.20, "JustChatting": 1.10, "Gambling": 0.80 } }
          },
          "migration": { "energyCost": 6, "minWeeksOnPlatform": 26, "announceWeeks": 4, "announceCredibility": -1, "announcedBonus": 0.10,
            "banPenalty": 0.10, "retentionBase": 0.40, "credibilityDivisor": 250, "dramaDivisor": 500, "retentionMin": 0.20,
            "retentionMax": 0.80, "jitter": 0.05 },
          "policy": { "gamblingExposure": 1, "sideBusinessExposure": 1, "highDramaExposure": 1, "highDramaFrom": 70,
            "chancePerExposure": 0.015, "chanceCap": 0.15, "strikeWeeks": 26, "strikesToBan": 3, "dramaDecay": 1 },
          "sponsors": { "offerBase": 0.08, "offerPerMagnitude": 0.02, "offerCap": 0.20, "offerWeeks": 2, "maxContracts": 2,
            "pitchEnergyCost": 8,
            "pitch": { "id": "pitch", "label": "Rozpisz ofertę dla sponsorów", "baseChance": 0.15,
              "successText": "Ktoś odpisał.", "failureText": "Cisza w skrzynce.",
              "outcomes": { "success": [ { "kind": "StatDelta", "stat": "Credibility", "amount": 1 } ] } },
            "terminationCredibility": -5, "missesToTerminate": 2, "startingLeads": { "IndustryInsider": "trusted-tea" } },
          "wagers": { "stakePln": { "Low": 100, "Medium": 500, "High": 2000 }, "exposure": { "Low": 1, "Medium": 2, "High": 3 },
            "maxShareOfCash": 0.25, "chance": 0.45, "payoutMultiple": 2, "winAudience": 1.15, "lossAudience": 1.05,
            "winDrama": 2, "lossDrama": 3 },
          "sideBusiness": { "fromWeek": 52, "minFollowers": 2000, "energyCost": 12, "burnoutGain": 4, "qualityPerWork": 15,
            "qualityDecay": 3, "incomePerFollower": 0.0005, "incomeScale": 10, "credibilityPerWeek": -0.2 },
          "platformBan": { "title": "Ban na platformie", "text": "Platforma zamyka ci kanał na stałe. Dokąd przenosisz społeczność?",
            "choices": { "Czatnia": "Przenieś się na Czatnię", "PulsLive": "Przenieś się na PulsLive", "Nurt": "Przenieś się na Nurt" } }
        }
        """;

    /// <summary>The repository balance plus the M3-B sections; <paramref name="edit"/> changes the sections first.</summary>
    private static byte[] M3bBalance(Action<JsonObject>? edit = null)
    {
        var root = JsonNode.Parse(TestContentFiles.Balance)!.AsObject();
        var sections = JsonNode.Parse(M3bSections)!.AsObject();
        edit?.Invoke(sections);
        foreach (var name in sections.Select(x => x.Key).ToList())
        {
            var section = sections[name];
            sections.Remove(name);
            root[name] = section;
        }

        return System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] M3bEvents() => SchemaTwoWith(sponsors: TrustedTea);

    [Fact]
    public void Balance_reads_the_achievements()
    {
        var balance = M3bBalance(sections => sections["achievements"] = JsonNode.Parse("""
            [ { "id": "fans", "name": "Fani", "description": "10 000 obserwujących", "kind": "followers", "threshold": 10000, "points": 1 },
              { "id": "done", "name": "Meta", "description": "Cała kariera", "kind": "completed", "points": 2 },
              { "id": "partner", "name": "Partner", "description": "Raty", "kind": "sponsorInstallments", "threshold": 24, "points": 2 } ]
            """));
        var defs = JsonContentSource.Parse(balance, M3bEvents(), "test").Balance.Achievements!.Value;
        Assert.Equal(new AchievementDefinition("fans", "Fani", "10 000 obserwujących", AchievementKind.Followers, Fixed.FromInt(10_000), 1), defs[0]);
        Assert.Equal((AchievementKind.Completed, (Fixed?)null), (defs[1].Kind, defs[1].Threshold));
        Assert.Equal(AchievementKind.SponsorInstallments, defs[2].Kind);
    }

    [Fact]
    public void Unknown_achievement_kind_is_rejected()
    {
        var balance = M3bBalance(sections => sections["achievements"] = JsonNode.Parse("""[ { "id": "x", "name": "X", "description": "X", "kind": "luck", "points": 1 } ]"""));
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(balance, M3bEvents(), "test"));
        Assert.Contains("achievements[0].kind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Balance_without_achievements_leaves_them_null()
        => Assert.Null(new JsonContentSource(TestPaths.Golden("m3-systems.1")).Load().Balance.Achievements);

    [Fact]
    public void Balance_without_m3b_sections_leaves_them_null()
    {
        var b = new JsonContentSource(TestPaths.Golden("m3-systems.1")).Load().Balance;
        Assert.Null(b.Platforms);
        Assert.Null(b.Migration);
        Assert.Null(b.Policy);
        Assert.Null(b.Sponsors);
        Assert.Null(b.Wagers);
        Assert.Null(b.SideBusiness);
        Assert.Null(b.PlatformBan);
    }

    [Fact]
    public void Balance_reads_the_m3b_sections()
    {
        var c = JsonContentSource.Parse(M3bBalance(), M3bEvents(), "test");
        var b = c.Balance;
        Assert.Equal(Fixed.FromRatio(13, 10), b.Platforms![Platform.PulsLive].PolicyRisk);
        Assert.Equal(Fixed.FromRatio(11, 10), b.Platforms[Platform.PulsLive].CategoryReach[Category.Fps]);
        Assert.Equal(Fixed.One, b.Platforms[Platform.PulsLive].CategoryReach[Category.Gambling]);
        Assert.Equal(Fixed.One, b.Platforms[Platform.Czatnia].CategoryReach[Category.Chess]);
        Assert.Equal(Fixed.FromRatio(125, 100), b.Platforms[Platform.Nurt].SubYield);
        Assert.Equal(26, b.Migration!.MinWeeksOnPlatform);
        Assert.Equal((Fixed.FromInt(-1), Fixed.FromInt(250), Fixed.FromRatio(5, 100)), (b.Migration.AnnounceCredibility, b.Migration.CredibilityDivisor, b.Migration.Jitter));
        Assert.Equal(Fixed.FromRatio(15, 1000), b.Policy!.ChancePerExposure);
        Assert.Equal((26, 3, Fixed.FromInt(70)), (b.Policy.StrikeWeeks, b.Policy.StrikesToBan, b.Policy.HighDramaFrom));
        Assert.Equal(2000L, b.Wagers!.StakePln[WagerTier.High]);
        Assert.Equal(3, b.Wagers.Exposure[WagerTier.High]);
        Assert.Equal((Fixed.FromRatio(45, 100), Fixed.FromInt(2)), (b.Wagers.Chance, b.Wagers.PayoutMultiple));
        Assert.Equal(52, b.SideBusiness!.FromWeek);
        Assert.Equal((2000L, Fixed.FromRatio(5, 10_000), Fixed.FromRatio(-2, 10)), (b.SideBusiness.MinFollowers, b.SideBusiness.IncomePerFollower, b.SideBusiness.CredibilityPerWeek));
        Assert.Equal(Fixed.FromRatio(15, 100), b.Sponsors!.Pitch.BaseChance);
        Assert.Equal(("Rozpisz ofertę dla sponsorów", "Ktoś odpisał."), (b.Sponsors.Pitch.LabelPl, b.Sponsors.Pitch.SuccessText));
        Assert.Equal((2, 2, Fixed.FromInt(8)), (b.Sponsors.OfferWeeks, b.Sponsors.MaxContracts, b.Sponsors.PitchEnergyCost));
        Assert.Equal("trusted-tea", b.Sponsors.StartingLeads[Background.IndustryInsider]);
        Assert.Equal(3, b.PlatformBan!.Choices.Count);
        Assert.Equal(("Ban na platformie", "Przenieś się na Nurt"), (b.PlatformBan.Title, b.PlatformBan.Choices[Platform.Nurt]));
    }

    [Fact]
    public void A_section_missing_one_value_is_rejected()
    {
        var balance = M3bBalance(sections => sections["policy"]!.AsObject().Remove("chanceCap"));
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(balance, M3bEvents(), "test"));
        Assert.Equal("balance.json.policy.chanceCap: is missing.", error.Message);
    }

    [Fact]
    public void An_unknown_key_in_a_keyed_section_is_rejected()
    {
        var balance = M3bBalance(sections => sections["wagers"]!["stakePln"]!.AsObject().Add("Max", 9000));
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(balance, M3bEvents(), "test"));
        Assert.Contains("unknown WagerTier 'Max'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_duplicate_key_in_a_keyed_section_is_rejected()
    {
        var balance = System.Text.Encoding.UTF8.GetString(M3bBalance());
        const string stakes = "\"stakePln\":{\"Low\":100,";
        Assert.Contains(stakes, balance, StringComparison.Ordinal);
        var duplicated = System.Text.Encoding.UTF8.GetBytes(balance.Replace(stakes, stakes + "\"Low\":200,", StringComparison.Ordinal));
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(duplicated, M3bEvents(), "test"));
        Assert.Equal("balance.json.wagers.stakePln.Low: is a duplicate key.", error.Message);
    }

    [Theory]
    [InlineData("platforms")]
    [InlineData("migration")]
    [InlineData("policy")]
    [InlineData("sponsors")]
    [InlineData("wagers")]
    [InlineData("sideBusiness")]
    [InlineData("platformBan")]
    public void M3b_sections_are_rejected_with_schema_one_events(string section)
    {
        var balance = M3bBalance(sections =>
        {
            foreach (var name in sections.Select(x => x.Key).Where(name => name != section).ToList())
            {
                sections.Remove(name);
            }
        });
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(balance, TestContentFiles.Events, "test"));
        Assert.Equal($"balance.json.{section}: is not allowed with schema 1 events.", error.Message);
    }

    [Theory]
    [InlineData("""{ "schema": 3, "events": [] }""", "events.json.schema")]
    [InlineData("""{ "schema": 2, "events": [ { "id": "x", "version": 1, "phase": "boss", "title": "T", "text": "t", "weight": 1, "eligibility": { "fromWeek": 1, "toWeek": 2 }, "choices": [] } ] }""", "events[x].phase")]
    [InlineData("""{ "schema": 2, "events": [ { "id": "x", "version": 1, "phase": "live", "title": "T", "text": "t", "weight": 1, "eligibility": { "fromWeek": 1, "toWeek": 2 }, "choices": [ { "id": "c", "label": "L", "guaranteed": true, "requires": {}, "outcomes": {} } ] } ] }""", "events[x].choices[0]/c.requires")]
    [InlineData("""{ "schema": 2, "events": [ { "id": "x", "version": 1, "phase": "live", "title": "T", "text": "t", "weight": 1, "oneTime": "yes", "eligibility": { "fromWeek": 1, "toWeek": 2 }, "choices": [] } ] }""", "events[x].oneTime")]
    public void Invalid_schema_two_input_names_its_path(string json, string path)
    {
        var error = Assert.Throws<InvalidDataException>(() => JsonContentSource.ParseEvents(System.Text.Encoding.UTF8.GetBytes(json)));
        Assert.StartsWith(path, error.Message, StringComparison.Ordinal);
    }
}
