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

    public ContentSnapshot Load()
    {
        var balanceBytes = File.ReadAllBytes(Path.Combine(directory, BalanceFile));
        var eventsBytes = File.ReadAllBytes(Path.Combine(directory, EventsFile));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(balanceBytes);
        hash.AppendData(eventsBytes);
        var digest = Convert.ToHexStringLower(hash.GetHashAndReset());

        using var balanceDocument = JsonDocument.Parse(balanceBytes);
        using var eventsDocument = JsonDocument.Parse(eventsBytes);
        var balanceRoot = balanceDocument.RootElement;
        var balance = ReadBalance(balanceRoot);
        var profiles = ReadProfiles(Property(balanceRoot, "profiles", BalanceFile));
        var events = ReadArray(Property(eventsDocument.RootElement, "events", EventsFile), "events", ReadEvent);

        var snapshot = new ContentSnapshot(balance.Version, digest, balance, events, profiles);
        var violations = ContentValidator.Validate(snapshot);
        if (violations.Count > 0)
        {
            throw new InvalidDataException($"Content in '{directory}' is invalid: {string.Join(", ", violations)}");
        }

        return snapshot;
    }

    private static BalanceConstants ReadBalance(JsonElement b)
    {
        const string f = BalanceFile;
        var weights = Property(b, "scoreWeights", f);
        var difficulty = Property(b, "difficultyMultiplier", f);
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
        };
    }

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

    private static EventDefinition ReadEvent(JsonElement e, string path)
    {
        var id = ReadString(e, "id", path);
        path = $"events[{id}]";
        var eligibility = Property(e, "eligibility", path);
        return new EventDefinition(
            id,
            ReadString(e, "titleKey", path),
            ReadString(e, "textPl", path),
            ReadInt(e, "weight", path),
            ReadInt(e, "cooldownWeeks", path),
            new Eligibility(
                ReadInt(eligibility, "fromWeek", path),
                ReadInt(eligibility, "toWeek", path),
                ReadStatBounds(eligibility, "min", path),
                ReadStatBounds(eligibility, "max", path)),
            ReadArray(Property(e, "choices", path), $"{path}.choices", ReadChoice));
    }

    private static ChoiceDefinition ReadChoice(JsonElement c, string path)
    {
        var id = ReadString(c, "id", path);
        path = $"{path}/{id}";
        var guaranteed = OptionalBool(c, "guaranteed");
        var outcomes = Property(c, "outcomes", path);
        return new ChoiceDefinition(
            id,
            ReadString(c, "labelKey", path),
            ReadString(c, "labelPl", path),
            guaranteed,
            guaranteed ? Fixed.One : ReadFixed(c, "baseChance", path),
            c.TryGetProperty("modifiers", out var modifiers) ? ReadArray(modifiers, $"{path}.modifiers", ReadModifier) : [],
            OptionalBool(c, "allowsCritical"),
            new OutcomeSet(
                ReadEffects(outcomes, "success", path) ?? [],
                ReadEffects(outcomes, "failure", path) ?? [],
                ReadEffects(outcomes, "criticalSuccess", path),
                ReadEffects(outcomes, "criticalFailure", path)));
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

    private static Effect ReadEffect(JsonElement e, string path)
        => new(
            ParseEnum<EffectKind>(ReadString(e, "kind", path), path),
            e.TryGetProperty("stat", out var stat) && stat.ValueKind != JsonValueKind.Null ? ParseEnum<StatKey>(stat.GetString()!, path) : null,
            ReadFixed(e, "amount", path));

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

    private static string ReadString(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid($"{path}.{name}", "expected a string");
    }

    private static bool OptionalBool(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var value) && value.GetBoolean();

    private static long ReadLong(JsonElement obj, string name, string path) => LongValue(Property(obj, name, path), $"{path}.{name}");

    private static int ReadInt(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : throw Invalid($"{path}.{name}", "expected a 32-bit integer");
    }

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
