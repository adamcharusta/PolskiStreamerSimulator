using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>
/// Loads <c>balance.json</c> and <c>events.json</c> from a directory into a validated <see cref="ContentSnapshot"/>.
/// Decimal numbers are parsed from their JSON text into <see cref="Fixed"/>, never through floating point.
/// </summary>
public sealed class JsonContentSource(string directory) : IContentSource
{
    public const string BalanceFile = "balance.json";
    public const string EventsFile = "events.json";

    /// <summary>Ruleset assumed when <c>balance.json</c> names none (M1 content).</summary>
    private const string DefaultRulesetId = "m1-prototype.1";

    /// <summary>Cooldown of a schema-2 event that does not set <c>cooldownWeeks</c>.</summary>
    private const int DefaultCooldownWeeks = 13;
    private const string BurnoutPenaltySection = "burnoutPenalty";
    private const string ViralSection = "viral";
    private const string MasterySection = "mastery";
    private const string SponsorsSection = "sponsors";

    /// <summary>Balance sections allowed only with schema-2 events.</summary>
    private static readonly string[] SchemaTwoSections =
        ["networkCollaborate", "platforms", "migration", "policy", SponsorsSection, "wagers", "sideBusiness", "platformBan"];

    public ContentSnapshot Load()
        => Parse(
            File.ReadAllBytes(Path.Combine(directory, BalanceFile)),
            File.ReadAllBytes(Path.Combine(directory, EventsFile)),
            directory);

    /// <summary>
    /// Parses and validates content from the bytes of <c>balance.json</c> and <c>events.json</c>. The digest is the
    /// SHA-256 of exactly these bytes, so a stored copy reproduces it; <paramref name="source"/> names the origin in errors.
    /// </summary>
    public static ContentSnapshot Parse(byte[] balanceBytes, byte[] eventsBytes, string source)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(balanceBytes);
        hash.AppendData(eventsBytes);
        var digest = Convert.ToHexStringLower(hash.GetHashAndReset());

        using var balanceDocument = JsonDocument.Parse(balanceBytes);
        using var eventsDocument = JsonDocument.Parse(eventsBytes);
        var balanceRoot = balanceDocument.RootElement;
        var balance = ReadBalance(balanceRoot);
        var profiles = ReadProfiles(Property(balanceRoot, "profiles", BalanceFile));
        var (schema, events, sponsors) = ReadEvents(eventsDocument.RootElement);
        if (schema == 1 && SchemaTwoSections.FirstOrDefault(name => Optional(balanceRoot, name) is not null) is { } section)
        {
            throw Invalid($"{BalanceFile}.{section}", "is not allowed with schema 1 events");
        }

        var rulesetId = OptionalString(balanceRoot, "ruleset", BalanceFile) ?? DefaultRulesetId;

        var snapshot = new ContentSnapshot(balance.Version, digest, balance, events, profiles, schema, rulesetId, sponsors);
        var violations = ContentValidator.Validate(snapshot);
        if (violations.Count > 0)
        {
            throw new InvalidDataException($"Content in '{source}' is invalid: {string.Join(", ", violations)}");
        }

        return snapshot;
    }

    /// <summary>Reads the events of an <c>events.json</c> in its declared schema, without validating them.</summary>
    public static ImmutableArray<EventDefinition> ParseEvents(byte[] eventsBytes)
    {
        using var document = JsonDocument.Parse(eventsBytes);
        return ReadEvents(document.RootElement).Events;
    }

    /// <summary>Reads the schema, the events and (schema 2 only) the sponsor templates of an <c>events.json</c>.</summary>
    private static (int Schema, ImmutableArray<EventDefinition> Events, ImmutableArray<SponsorTemplate> Sponsors) ReadEvents(JsonElement root)
    {
        var schema = OptionalInt(root, "schema", EventsFile) ?? 1;
        if (schema is not (1 or 2))
        {
            throw Invalid($"{EventsFile}.schema", $"unsupported schema {schema}");
        }

        var events = ReadArray(Property(root, "events", EventsFile), "events", (e, p) => ReadEvent(e, p, schema));
        if (Optional(root, SponsorsSection) is not { } sponsors)
        {
            return (schema, events, []);
        }

        return schema == 1
            ? throw Invalid($"{EventsFile}.sponsors", "is not allowed with schema 1 events")
            : (schema, events, ReadArray(sponsors, SponsorsSection, ReadSponsor));
    }

    /// <summary>Reads one sponsor template; term, cadence, required category, platforms, incompatibilities and tags are optional.</summary>
    private static SponsorTemplate ReadSponsor(JsonElement t, string path)
    {
        var id = ReadString(t, "id", path);
        path = $"sponsors[{id}]";
        var prerequisites = Property(t, "prerequisites", path);
        var prerequisitesPath = $"{path}.prerequisites";
        var breach = Property(t, "breach", path);
        return new SponsorTemplate(
            id,
            ReadInt(t, "version", path),
            ReadString(t, "name", path),
            ReadString(t, "pitch", path),
            ParseEnum<SponsorKind>(ToPascalCase(ReadString(t, "kind", path)), $"{path}.kind"),
            ReadLong(t, "paymentPln", path),
            ReadLong(t, "paymentPerThousandFollowersPln", path),
            ReadLong(t, "paymentCapPln", path),
            OptionalInt(t, "termWeeks", path),
            OptionalInt(t, "dueEveryWeeks", path),
            OptionalString(t, "requiredCategory", path) is { } category ? ParseEnum<Category>(category, $"{path}.requiredCategory") : null,
            ReadPlatforms(t, path),
            new SponsorPrerequisites(
                OptionalLong(prerequisites, "minFollowers", prerequisitesPath) ?? 0,
                OptionalFixed(prerequisites, "minCredibility", prerequisitesPath),
                OptionalFixed(prerequisites, "maxDrama", prerequisitesPath),
                OptionalFixed(prerequisites, "minDrama", prerequisitesPath)),
            OptionalStrings(t, "incompatibleWith", path),
            new SponsorBreach(ReadLong(breach, "cashPln", $"{path}.breach"), ReadFixed(breach, "credibility", $"{path}.breach")),
            ReadInt(t, "weight", path),
            OptionalStrings(t, "tags", path));
    }

    /// <summary>An optional <c>platforms</c> list; absent means every platform.</summary>
    private static ImmutableArray<Platform> ReadPlatforms(JsonElement obj, string path)
        => Optional(obj, "platforms") is { } platforms
            ? ReadArray(platforms, $"{path}.platforms", (p, itemPath) => ParseEnum<Platform>(StringValue(p, itemPath), itemPath))
            : [];

    private static BalanceConstants ReadBalance(JsonElement b)
    {
        const string f = BalanceFile;
        var weights = Property(b, "scoreWeights", f);
        var difficulty = Property(b, "difficultyMultiplier", f);
        var burnout = Section(b, BurnoutPenaltySection);
        var shortStream = Section(b, "shortStream");
        var viral = Section(b, ViralSection);
        var mastery = Section(b, MasterySection);
        return new BalanceConstants
        {
            Version = ReadString(b, "version", f),
            LivingCostPln = ReadLong(b, "livingCostPln", f),
            EasyUpkeepDiscount = ReadFixed(b, "easyUpkeepDiscount", f),
            EquipmentUpkeepPerTierPln = ReadArray(Property(b, "equipmentUpkeepPerTierPln", f), "equipmentUpkeepPerTierPln", (e, p) => LongValue(e, p)),
            EquipmentPricePln = ReadArray(Property(b, "equipmentPricePln", f), "equipmentPricePln", (e, p) => LongValue(e, p)),
            StreamEnergyCost = ReadFixed(b, "streamEnergyCost", f),
            StreamBurnoutGain = ReadFixed(b, "streamBurnoutGain", f),
            RestEnergy = ReadFixed(b, "restEnergy", f),
            RestBurnout = ReadFixed(b, "restBurnout", f),
            NoActionEnergy = ReadFixed(b, "noActionEnergy", f),
            TrainEnergyCost = ReadFixed(b, "trainEnergyCost", f),
            TrainSkillMin = ReadInt(b, "trainSkillMin", f),
            TrainSkillMax = ReadInt(b, "trainSkillMax", f),
            ManageCommunityCredibility = ReadFixed(b, "manageCommunityCredibility", f),
            ManageCommunityDramaRelief = ReadFixed(b, "manageCommunityDramaRelief", f),
            ManageCommunityBoredomRelief = ReadFixed(b, "manageCommunityBoredomRelief", f),
            QualityBase = ReadFixed(b, "qualityBase", f),
            QualitySkill = ReadFixed(b, "qualitySkill", f),
            QualityEquipment = ReadFixed(b, "qualityEquipment", f),
            QualityBurnout = ReadFixed(b, "qualityBurnout", f),
            QualityEnergy = ReadFixed(b, "qualityEnergy", f),
            ViewersBase = ReadFixed(b, "viewersBase", f),
            ViewersSkill = ReadFixed(b, "viewersSkill", f),
            ViewersSqrtFollowers = ReadFixed(b, "viewersSqrtFollowers", f),
            ViewersLinearFollowers = ReadFixed(b, "viewersLinearFollowers", f),
            ViewersMin = ReadFixed(b, "viewersMin", f),
            PlatformReach = ReadFixed(b, "platformReach", f),
            PlatformSubYield = ReadFixed(b, "platformSubYield", f),
            PlatformAdYield = ReadFixed(b, "platformAdYield", f),
            NoiseMin = ReadFixed(b, "noiseMin", f),
            NoiseMax = ReadFixed(b, "noiseMax", f),
            BoredomDivisor = ReadFixed(b, "boredomDivisor", f),
            ViralCapMultiplier = ReadFixed(b, "viralCapMultiplier", f),
            ConversionRate = ReadFixed(b, "conversionRate", f),
            ChurnRate = ReadFixed(b, "churnRate", f),
            ChurnBoredomFactor = ReadFixed(b, "churnBoredomFactor", f),
            PaidConversion = ReadFixed(b, "paidConversion", f),
            SubPricePln = ReadFixed(b, "subPricePln", f),
            DonationYield = ReadFixed(b, "donationYield", f),
            AdYield = ReadFixed(b, "adYield", f),
            AdLoad = ReadFixed(b, "adLoad", f),
            SkillDecayPerWeek = ReadFixed(b, "skillDecayPerWeek", f),
            SkillDecayAfterWeeks = ReadInt(b, "skillDecayAfterWeeks", f),
            BoredomGainMin = ReadInt(b, "boredomGainMin", f),
            BoredomGainMax = ReadInt(b, "boredomGainMax", f),
            BoredomReliefMin = ReadInt(b, "boredomReliefMin", f),
            BoredomReliefMax = ReadInt(b, "boredomReliefMax", f),
            InsolvencyThresholdPln = ReadLong(b, "insolvencyThresholdPln", f),
            ScoreFollowersCap = ReadLong(b, "scoreFollowersCap", f),
            ScoreCashCap = ReadLong(b, "scoreCashCap", f),
            ScoreWeights = new ScoreWeights(
                ReadFixed(weights, "followers", f),
                ReadFixed(weights, "wealth", f),
                ReadFixed(weights, "achievements", f)),
            AchievementPointsCap = ReadInt(b, "achievementPointsCap", f),
            DifficultyMultiplier = Enum.GetValues<Difficulty>()
                .ToImmutableDictionary(d => d, d => ReadFixed(difficulty, d.ToString(), f)),
            EasyChanceBonus = ReadFixed(b, "easyChanceBonus", f),
            EventsPerWeekMin = ReadInt(b, "eventsPerWeekMin", f),
            EventsPerWeekMax = ReadInt(b, "eventsPerWeekMax", f),
            RecoveryCrisisEventId = OptionalString(b, "recoveryCrisisEventId", f),
            DecisionChancePerWeek = OptionalFixed(b, "decisionChancePerWeek", f) ?? Fixed.Zero,
            NetworkEnergyCost = OptionalFixed(b, "networkEnergyCost", f) ?? Fixed.Zero,
            NetworkCollaborate = ReadNetworkChoice(b),
            ShortStreamFatigue = SectionFixed(shortStream, "fatigue", "shortStream", Fixed.One),
            ShortStreamAudience = SectionFixed(shortStream, "audience", "shortStream", Fixed.One),
            Categories = Section(b, "categories") is { } categories ? ReadCategories(categories) : null,
            BurnoutPenaltyFrom = SectionFixed(burnout, "from", BurnoutPenaltySection, Fixed.FromInt(101)),
            BurnoutPenaltyPerPoint = SectionFixed(burnout, "perPoint", BurnoutPenaltySection, Fixed.Zero),
            BurnoutPenaltyCap = SectionFixed(burnout, "cap", BurnoutPenaltySection, Fixed.Zero),
            ViralQualityFrom = SectionFixed(viral, "qualityFrom", ViralSection, Fixed.Zero),
            ViralPerQualityPoint = SectionFixed(viral, "perQualityPoint", ViralSection, Fixed.Zero),
            ViralPerCritical = SectionFixed(viral, "perCritical", ViralSection, Fixed.Zero),
            ViralChanceCap = SectionFixed(viral, "chanceCap", ViralSection, Fixed.Zero),
            ViralMultiplierMin = SectionFixed(viral, "multiplierMin", ViralSection, Fixed.Zero),
            ViralMultiplierMax = SectionFixed(viral, "multiplierMax", ViralSection, Fixed.Zero),
            ViralFollowerShare = SectionFixed(viral, "followerShare", ViralSection, Fixed.Zero),
            ViralViewerShare = SectionFixed(viral, "viewerShare", ViralSection, Fixed.Zero),
            ViralFollowerMin = SectionInt(viral, "followerMin", ViralSection, 0),
            MasteryFromSkill = SectionFixed(mastery, "fromSkill", MasterySection, Fixed.FromInt(101)),
            MasteryBoredomRelief = SectionInt(mastery, "boredomRelief", MasterySection, 0),
            MasteryChurnFactor = SectionFixed(mastery, "churnFactor", MasterySection, Fixed.One),
            Platforms = Optional(b, "platforms") is { } platforms ? ReadPlatformProfiles(platforms) : null,
            Migration = Optional(b, "migration") is { } migration ? ReadMigration(migration) : null,
            Policy = Optional(b, "policy") is { } policy ? ReadPolicy(policy) : null,
            Sponsors = Optional(b, SponsorsSection) is { } sponsors ? ReadSponsorBalance(sponsors) : null,
            Wagers = Optional(b, "wagers") is { } wagers ? ReadWagers(wagers) : null,
            SideBusiness = Optional(b, "sideBusiness") is { } sideBusiness ? ReadSideBusiness(sideBusiness) : null,
            PlatformBan = Optional(b, "platformBan") is { } platformBan ? ReadPlatformBan(platformBan) : null,
        };
    }

    /// <summary>Reads <c>platforms</c>: one profile per named platform; a category missing from <c>categoryReach</c> has factor 1.</summary>
    private static ImmutableDictionary<Platform, PlatformProfile> ReadPlatformProfiles(JsonElement platforms)
        => ReadKeyed<Platform, PlatformProfile>(platforms, $"{BalanceFile}.platforms", (p, path) =>
        {
            var reach = Optional(p, "categoryReach");
            var categoryReach = reach is { } r
                ? ReadKeyed<Category, Fixed>(r, $"{path}.categoryReach", FixedValue)
                : ImmutableDictionary<Category, Fixed>.Empty;
            return new PlatformProfile(
                ReadFixed(p, "reach", path),
                ReadFixed(p, "subYield", path),
                ReadFixed(p, "adYield", path),
                ReadFixed(p, "policyRisk", path),
                new CategoryValues<Fixed>([.. Enum.GetValues<Category>().Select(c => categoryReach.GetValueOrDefault(c, Fixed.One))]));
        });

    private static MigrationBalance ReadMigration(JsonElement m)
    {
        const string path = $"{BalanceFile}.migration";
        return new MigrationBalance(
            ReadFixed(m, "energyCost", path),
            ReadInt(m, "minWeeksOnPlatform", path),
            ReadInt(m, "announceWeeks", path),
            ReadFixed(m, "announceCredibility", path),
            ReadFixed(m, "announcedBonus", path),
            ReadFixed(m, "banPenalty", path),
            ReadFixed(m, "retentionBase", path),
            ReadFixed(m, "credibilityDivisor", path),
            ReadFixed(m, "dramaDivisor", path),
            ReadFixed(m, "retentionMin", path),
            ReadFixed(m, "retentionMax", path),
            ReadFixed(m, "jitter", path));
    }

    private static PolicyBalance ReadPolicy(JsonElement p)
    {
        const string path = $"{BalanceFile}.policy";
        return new PolicyBalance(
            ReadInt(p, "gamblingExposure", path),
            ReadInt(p, "sideBusinessExposure", path),
            ReadInt(p, "highDramaExposure", path),
            ReadFixed(p, "highDramaFrom", path),
            ReadFixed(p, "chancePerExposure", path),
            ReadFixed(p, "chanceCap", path),
            ReadInt(p, "strikeWeeks", path),
            ReadInt(p, "strikesToBan", path),
            ReadFixed(p, "dramaDecay", path));
    }

    /// <summary>Reads <c>sponsors</c>; the pitch is a schema-2 choice and <c>startingLeads</c> maps backgrounds to template IDs.</summary>
    private static SponsorBalance ReadSponsorBalance(JsonElement s)
    {
        const string path = $"{BalanceFile}.sponsors";
        return new SponsorBalance(
            ReadFixed(s, "offerBase", path),
            ReadFixed(s, "offerPerMagnitude", path),
            ReadFixed(s, "offerCap", path),
            ReadInt(s, "offerWeeks", path),
            ReadInt(s, "maxContracts", path),
            ReadFixed(s, "pitchEnergyCost", path),
            ReadChoice(Property(s, "pitch", path), $"{path}.pitch", schema: 2),
            ReadFixed(s, "terminationCredibility", path),
            ReadInt(s, "missesToTerminate", path),
            ReadKeyed<Background, string>(Property(s, "startingLeads", path), $"{path}.startingLeads", StringValue));
    }

    private static WagerBalance ReadWagers(JsonElement w)
    {
        const string path = $"{BalanceFile}.wagers";
        return new WagerBalance(
            ReadKeyed<WagerTier, long>(Property(w, "stakePln", path), $"{path}.stakePln", LongValue),
            ReadKeyed<WagerTier, int>(Property(w, "exposure", path), $"{path}.exposure", IntValue),
            ReadFixed(w, "maxShareOfCash", path),
            ReadFixed(w, "chance", path),
            ReadFixed(w, "payoutMultiple", path),
            ReadFixed(w, "winAudience", path),
            ReadFixed(w, "lossAudience", path),
            ReadFixed(w, "winDrama", path),
            ReadFixed(w, "lossDrama", path));
    }

    private static SideBusinessBalance ReadSideBusiness(JsonElement s)
    {
        const string path = $"{BalanceFile}.sideBusiness";
        return new SideBusinessBalance(
            ReadInt(s, "fromWeek", path),
            ReadLong(s, "minFollowers", path),
            ReadFixed(s, "energyCost", path),
            ReadFixed(s, "burnoutGain", path),
            ReadFixed(s, "qualityPerWork", path),
            ReadFixed(s, "qualityDecay", path),
            ReadFixed(s, "incomePerFollower", path),
            ReadFixed(s, "incomeScale", path),
            ReadFixed(s, "credibilityPerWeek", path));
    }

    private static PlatformBanText ReadPlatformBan(JsonElement p)
    {
        const string path = $"{BalanceFile}.platformBan";
        return new PlatformBanText(
            ReadString(p, "title", path),
            ReadString(p, "text", path),
            ReadKeyed<Platform, string>(Property(p, "choices", path), $"{path}.choices", StringValue));
    }

    /// <summary>
    /// Reads an object keyed by enum member names; every key must name a member at most once, and the validator checks that none is
    /// missing where all are required.
    /// </summary>
    private static ImmutableDictionary<TEnum, T> ReadKeyed<TEnum, T>(JsonElement obj, string path, Func<JsonElement, string, T> read)
        where TEnum : struct, Enum
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(path, "expected an object");
        }

        var values = ImmutableDictionary.CreateBuilder<TEnum, T>();
        foreach (var p in obj.EnumerateObject())
        {
            var key = ParseEnum<TEnum>(p.Name, path);
            if (values.ContainsKey(key))
            {
                throw Invalid($"{path}.{p.Name}", "is a duplicate key");
            }

            values.Add(key, read(p.Value, $"{path}.{p.Name}"));
        }

        return values.ToImmutable();
    }

    /// <summary>The balance's optional network choice; absent or JSON null means the action is unavailable.</summary>
    private static ChoiceDefinition? ReadNetworkChoice(JsonElement b)
        => Optional(b, "networkCollaborate") is { } network ? ReadChoice(network, $"{BalanceFile}.networkCollaborate", schema: 2) : null;

    /// <summary>An optional top-level balance section; absent means every value in it takes its M1-neutral default.</summary>
    private static JsonElement? Section(JsonElement b, string name) => b.TryGetProperty(name, out var section) ? section : null;

    /// <summary>A number inside an optional section, required once the section exists; <paramref name="fallback"/> when it does not.</summary>
    private static Fixed SectionFixed(JsonElement? section, string name, string sectionName, Fixed fallback)
        => section is { } s ? ReadFixed(s, name, $"{BalanceFile}.{sectionName}") : fallback;

    /// <summary>An integer inside an optional section, required once the section exists; <paramref name="fallback"/> when it does not.</summary>
    private static int SectionInt(JsonElement? section, string name, string sectionName, int fallback)
        => section is { } s ? ReadInt(s, name, $"{BalanceFile}.{sectionName}") : fallback;

    /// <summary>Reads the optional per-category profiles; every category is required once the section exists.</summary>
    private static CategoryValues<CategoryProfile> ReadCategories(JsonElement categories)
        => new([.. Enum.GetValues<Category>().Select(c => ReadCategory(Property(categories, c.ToString(), $"{BalanceFile}.categories"), $"{BalanceFile}.categories.{c}"))]);

    private static CategoryProfile ReadCategory(JsonElement p, string path) => new(
        ReadFixed(p, "reach", path),
        ReadFixed(p, "noiseMin", path),
        ReadFixed(p, "noiseMax", path),
        ReadFixed(p, "conversion", path),
        ReadFixed(p, "churn", path),
        ReadInt(p, "boredomGainMin", path),
        ReadInt(p, "boredomGainMax", path),
        ReadFixed(p, "subYield", path),
        ReadFixed(p, "donationYield", path),
        ReadFixed(p, "adYield", path),
        ReadFixed(p, "qualitySkillWeight", path),
        ReadFixed(p, "credibilityPerStream", path),
        ReadFixed(p, "dramaPerStream", path),
        ReadFixed(p, "viralBase", path));

    private static ImmutableDictionary<Background, StartingProfile> ReadProfiles(JsonElement profiles)
    {
        var builder = ImmutableDictionary.CreateBuilder<Background, StartingProfile>();
        foreach (var property in profiles.EnumerateObject())
        {
            var path = $"profiles.{property.Name}";
            var p = property.Value;
            var skill = Property(p, "skill", path);
            var equipment = Property(p, "equipment", path);
            builder.Add(
                ParseEnum<Background>(property.Name, path),
                new StartingProfile(
                    ReadLong(p, "cashPln", path),
                    ReadLong(p, "followers", path),
                    ReadFixed(p, "drama", path),
                    ReadFixed(p, "credibility", path),
                    ReadFixed(p, "energy", path),
                    new CategoryValues<Fixed>([.. Enum.GetValues<Category>().Select(c => ReadFixed(skill, c.ToString(), $"{path}.skill"))]),
                    Enum.GetValues<EquipmentSlot>().ToImmutableDictionary(s => s, s => ReadInt(equipment, s.ToString(), $"{path}.equipment")),
                    ReadFixed(p, "scoreMultiplier", path)));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Reads one event. Schema 1 has <c>titleKey</c>, <c>textPl</c> and a required <c>cooldownWeeks</c>; schema 2 has
    /// <c>version</c>, <c>phase</c>, <c>title</c>, ICU <c>text</c>, optional <c>oneTime</c>, <c>tags</c>, category and
    /// flag eligibility, and a <c>cooldownWeeks</c> that defaults to <see cref="DefaultCooldownWeeks"/>.
    /// </summary>
    private static EventDefinition ReadEvent(JsonElement e, string path, int schema)
    {
        var id = ReadString(e, "id", path);
        path = $"events[{id}]";
        var eligibility = Property(e, "eligibility", path);
        var definition = new EventDefinition(
            id,
            schema == 1 ? ReadString(e, "titleKey", path) : null,
            ReadString(e, schema == 1 ? "textPl" : "text", path),
            ReadInt(e, "weight", path),
            schema == 1 ? ReadInt(e, "cooldownWeeks", path) : OptionalInt(e, "cooldownWeeks", path) ?? DefaultCooldownWeeks,
            ReadEligibility(eligibility, path, schema),
            ReadArray(Property(e, "choices", path), $"{path}.choices", (c, p) => ReadChoice(c, p, schema)));
        return schema == 1 ? definition : definition with
        {
            Version = ReadInt(e, "version", path),
            Phase = ParseEnum<EventPhase>(ToPascalCase(ReadString(e, "phase", path)), $"{path}.phase"),
            Title = ReadString(e, "title", path),
            OneTime = OptionalBool(e, "oneTime", path),
            Tags = OptionalStrings(e, "tags", path),
        };
    }

    private static Eligibility ReadEligibility(JsonElement eligibility, string path, int schema)
    {
        var result = new Eligibility(
            ReadInt(eligibility, "fromWeek", path),
            ReadInt(eligibility, "toWeek", path),
            ReadStatBounds(eligibility, "min", path),
            ReadStatBounds(eligibility, "max", path));
        if (schema == 1)
        {
            return result;
        }

        var categoryList = eligibility.TryGetProperty("categories", out var categories)
            ? ReadArray(categories, $"{path}.categories", (c, p) => ParseEnum<Category>(StringValue(c, p), p))
            : [];
        return result with
        {
            Categories = categoryList,
            RequiresFlags = OptionalStrings(eligibility, "requiresFlags", path),
            ForbidsFlags = OptionalStrings(eligibility, "forbidsFlags", path),
            Platforms = ReadPlatforms(eligibility, path),
            MinStrikes = OptionalInt(eligibility, "minStrikes", path) ?? 0,
        };
    }

    /// <summary>Reads one choice; schema 1 has <c>labelKey</c>/<c>labelPl</c>, schema 2 an ICU <c>label</c>, outcome texts and requirements.</summary>
    private static ChoiceDefinition ReadChoice(JsonElement c, string path, int schema)
    {
        var id = ReadString(c, "id", path);
        path = $"{path}/{id}";
        var guaranteed = OptionalBool(c, "guaranteed", path);
        var outcomes = Property(c, "outcomes", path);
        var labelKey = schema == 1 ? ReadString(c, "labelKey", path) : null;
        var label = ReadString(c, schema == 1 ? "labelPl" : "label", path);
        var baseChance = guaranteed ? Fixed.One : ReadFixed(c, "baseChance", path);
        var modifierList = c.TryGetProperty("modifiers", out var modifiers) ? ReadArray(modifiers, $"{path}.modifiers", ReadModifier) : [];
        var choice = new ChoiceDefinition(
            id,
            labelKey,
            label,
            guaranteed,
            baseChance,
            modifierList,
            OptionalBool(c, "allowsCritical", path),
            new OutcomeSet(
                ReadEffects(outcomes, "success", path) ?? [],
                ReadEffects(outcomes, "failure", path) ?? [],
                ReadEffects(outcomes, "criticalSuccess", path),
                ReadEffects(outcomes, "criticalFailure", path)));
        if (schema == 1)
        {
            return choice;
        }

        var requirement = Optional(c, "requires") is { } requires ? ReadRequirement(requires, $"{path}.requires") : null;
        return choice with
        {
            Requires = requirement,
            SuccessText = OptionalString(c, "successText", path),
            FailureText = OptionalString(c, "failureText", path),
        };
    }

    /// <summary>Reads a choice requirement; one that sets neither minimum is rejected rather than treated as none.</summary>
    private static ChoiceRequirement ReadRequirement(JsonElement requires, string path)
    {
        var requirement = new ChoiceRequirement(OptionalLong(requires, "minCashPln", path), OptionalFixed(requires, "minEnergy", path));
        return requirement is { MinCashPln: null, MinEnergy: null }
            ? throw Invalid(path, "sets neither minCashPln nor minEnergy")
            : requirement;
    }

    private static ChanceModifier ReadModifier(JsonElement m, string path)
        => new(
            ParseEnum<StatKey>(ReadString(m, "stat", path), path),
            ReadFixed(m, "perPoint", path),
            m.TryGetProperty("cap", out var cap) && cap.ValueKind != JsonValueKind.Null ? FixedValue(cap, $"{path}.cap") : null,
            ReadString(m, "labelKey", path));

    private static ImmutableArray<Effect>? ReadEffects(JsonElement outcomes, string name, string path)
        => outcomes.TryGetProperty(name, out var effects) && effects.ValueKind != JsonValueKind.Null
            ? ReadArray(effects, $"{path}.{name}", ReadEffect)
            : null;

    /// <summary>Reads one effect; <c>flag</c>, <c>event</c> or <c>sponsor</c> becomes <see cref="Effect.Target"/>.</summary>
    private static Effect ReadEffect(JsonElement e, string path)
        => new(
            ParseEnum<EffectKind>(ReadString(e, "kind", path), path),
            OptionalString(e, "stat", path) is { } stat ? ParseEnum<StatKey>(stat, path) : null,
            ReadFixed(e, "amount", path),
            OptionalString(e, "category", path) is { } category ? ParseEnum<Category>(category, path) : null,
            OptionalString(e, "flag", path) ?? OptionalString(e, "event", path) ?? OptionalString(e, "sponsor", path),
            OptionalInt(e, "weeks", path));

    private static ImmutableDictionary<StatKey, Fixed> ReadStatBounds(JsonElement eligibility, string name, string path)
    {
        if (!eligibility.TryGetProperty(name, out var bounds))
        {
            return ImmutableDictionary<StatKey, Fixed>.Empty;
        }

        return bounds.EnumerateObject().ToImmutableDictionary(
            p => ParseEnum<StatKey>(p.Name, $"{path}.{name}"),
            p => FixedValue(p.Value, $"{path}.{name}.{p.Name}"));
    }

    private static ImmutableArray<T> ReadArray<T>(JsonElement array, string path, Func<JsonElement, string, T> read)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(path, "expected an array");
        }

        return [.. array.EnumerateArray().Select((item, i) => read(item, $"{path}[{i}]"))];
    }

    private static JsonElement Property(JsonElement obj, string name, string path)
        => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value)
            ? value
            : throw Invalid($"{path}.{name}", "is missing");

    /// <summary>Returns the property, or null when it is absent or JSON null.</summary>
    private static JsonElement? Optional(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    private static string ReadString(JsonElement obj, string name, string path) => StringValue(Property(obj, name, path), $"{path}.{name}");

    private static string? OptionalString(JsonElement obj, string name, string path)
        => Optional(obj, name) is { } value ? StringValue(value, $"{path}.{name}") : null;

    private static ImmutableArray<string> OptionalStrings(JsonElement obj, string name, string path)
        => Optional(obj, name) is { } value ? ReadArray(value, $"{path}.{name}", StringValue) : [];

    private static string StringValue(JsonElement value, string path)
        => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid(path, "expected a string");

    private static bool OptionalBool(JsonElement obj, string name, string path)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw Invalid($"{path}.{name}", "expected a boolean");
    }

    private static long ReadLong(JsonElement obj, string name, string path) => LongValue(Property(obj, name, path), $"{path}.{name}");

    private static long? OptionalLong(JsonElement obj, string name, string path)
        => Optional(obj, name) is { } value ? LongValue(value, $"{path}.{name}") : null;

    private static int ReadInt(JsonElement obj, string name, string path) => IntValue(Property(obj, name, path), $"{path}.{name}");

    private static int? OptionalInt(JsonElement obj, string name, string path)
        => Optional(obj, name) is { } value ? IntValue(value, $"{path}.{name}") : null;

    private static int IntValue(JsonElement value, string path)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : throw Invalid(path, "expected a 32-bit integer");

    private static Fixed? OptionalFixed(JsonElement obj, string name, string path)
        => Optional(obj, name) is { } value ? FixedValue(value, $"{path}.{name}") : null;

    /// <summary>Turns a camel-case JSON value such as <c>crisis</c> into its enum member name.</summary>
    private static string ToPascalCase(string value)
        => value.Length == 0 ? value : string.Concat(char.ToUpperInvariant(value[0]).ToString(), value[1..]);

    private static long LongValue(JsonElement value, string path)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : throw Invalid(path, "expected an integer");

    private static Fixed ReadFixed(JsonElement obj, string name, string path) => FixedValue(Property(obj, name, path), $"{path}.{name}");

    /// <summary>Parses a JSON number such as <c>-0.35</c> exactly; at most six decimals, no exponent.</summary>
    private static Fixed FixedValue(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw Invalid(path, "expected a number");
        }

        var text = value.GetRawText();
        if (text.AsSpan().IndexOfAny('e', 'E') >= 0)
        {
            throw Invalid(path, $"'{text}' uses an exponent");
        }

        var negative = text.StartsWith('-');
        var parts = (negative ? text[1..] : text).Split('.');
        var fraction = parts.Length == 2 ? parts[1] : string.Empty;
        if (fraction.Length > 6)
        {
            throw Invalid(path, $"'{text}' has more than six decimal places");
        }

        var whole = long.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture);
        var fractionRaw = fraction.Length == 0 ? 0 : long.Parse(fraction.PadRight(6, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        var raw = checked((whole * Fixed.Scale) + fractionRaw);
        return new Fixed(negative ? -raw : raw);
    }

    private static TEnum ParseEnum<TEnum>(string name, string path)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(name, ignoreCase: false, out var value) && Enum.IsDefined(value)
            ? value
            : throw Invalid(path, $"unknown {typeof(TEnum).Name} '{name}'");

    private static InvalidDataException Invalid(string path, string problem) => new($"{path}: {problem}.");
}
