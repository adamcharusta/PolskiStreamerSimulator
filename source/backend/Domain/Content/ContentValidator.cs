using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// Rejects content that a career could not safely run on, before any career starts. Schema-1 (M1) content gets the
/// original checks only; schema-2 content additionally gets ICU text, flag, follow-up, phase and per-category checks.
/// </summary>
public static class ContentValidator
{
    /// <summary>Pseudo event ID under which the balance's network choice is reported.</summary>
    public const string NetworkChoiceOwner = "balance.networkCollaborate";

    private const long MaxCashEffectPln = 1_000_000;

    private static readonly HashSet<EffectKind> DecisionForbidden =
        [EffectKind.AudienceMultiplier, EffectKind.EventQuality, EffectKind.ViralChance];

    public static IReadOnlyList<string> Validate(ContentSnapshot c)
    {
        var violations = new List<string>();
        ValidateBalance(c.Balance, violations);

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in c.Events)
        {
            ValidateEvent(e, c.Schema, eventIds, violations);
        }

        if (c.Balance.NetworkCollaborate is { } network)
        {
            ValidateChoice(NetworkChoiceOwner, network, c.Schema, new HashSet<string>(StringComparer.Ordinal), violations);
        }

        switch (c.Schema)
        {
            case 1:
                ValidateWeeklySupply(c, violations);
                break;
            case 2:
                ValidateSchemaTwo(c, violations);
                break;
            default:
                violations.Add($"schema:{c.Schema}");
                break;
        }

        violations.AddRange(Enum.GetValues<Background>()
            .Where(background => !c.Profiles.ContainsKey(background))
            .Select(background => $"missing-profile:{background}"));
        return violations;
    }

    private static void ValidateBalance(BalanceConstants b, List<string> violations)
    {
        if (b.EventsPerWeekMin < 1 || b.EventsPerWeekMin > b.EventsPerWeekMax)
        {
            violations.Add("balance:events-per-week");
        }

        if (b.EquipmentPricePln.Length != CareerInvariants.MaxEquipmentTier + 1
            || b.EquipmentUpkeepPerTierPln.Length != CareerInvariants.MaxEquipmentTier + 1)
        {
            violations.Add("balance:equipment-tiers");
        }
    }

    private static void ValidateEvent(EventDefinition e, int schema, HashSet<string> eventIds, List<string> violations)
    {
        if (!eventIds.Add(e.Id))
        {
            violations.Add($"duplicate-event:{e.Id}");
        }

        if (e.Choices.IsDefaultOrEmpty)
        {
            violations.Add($"no-choices:{e.Id}");
        }

        if (e.Weight <= 0)
        {
            violations.Add($"weight-nonpositive:{e.Id}");
        }

        if (!HasValidWeeks(e))
        {
            violations.Add($"week-range:{e.Id}");
        }

        var choiceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in ChoicesOf(e))
        {
            ValidateChoice(e.Id, choice, schema, choiceIds, violations);
        }
    }

    private static void ValidateChoice(string owner, ChoiceDefinition choice, int schema, HashSet<string> choiceIds, List<string> violations)
    {
        if (!choiceIds.Add(choice.Id))
        {
            violations.Add($"duplicate-choice:{owner}/{choice.Id}");
        }

        if (!choice.Guaranteed && (choice.BaseChance < Fixed.Zero || choice.BaseChance > Fixed.One))
        {
            violations.Add($"chance-range:{owner}/{choice.Id}");
        }

        if (!AllEffects(choice).All(effect => IsValidEffect(effect, schema)))
        {
            violations.Add($"effect-invalid:{owner}/{choice.Id}");
        }
    }

    private static ImmutableArray<ChoiceDefinition> ChoicesOf(EventDefinition e) => e.Choices.IsDefault ? [] : e.Choices;

    private static IEnumerable<Effect> AllEffects(ChoiceDefinition choice)
        => choice.Outcomes.Success
            .Concat(choice.Outcomes.Failure)
            .Concat(choice.Outcomes.CriticalSuccess ?? [])
            .Concat(choice.Outcomes.CriticalFailure ?? []);

    private static bool HasValidWeeks(EventDefinition e)
        => e.Eligibility.FromWeek >= CareerWeek.First
            && e.Eligibility.ToWeek <= CareerWeek.Last
            && e.Eligibility.FromWeek <= e.Eligibility.ToWeek;

    /// <summary>
    /// Checks one effect's shape: its amount range and that only the fields its kind uses are set. The six M3 kinds are
    /// valid only in schema 2, and a stat delta on followers is never valid.
    /// </summary>
    private static bool IsValidEffect(Effect effect, int schema) => effect.Kind switch
    {
        EffectKind.StatDelta => effect.Stat is { } stat && stat != StatKey.Followers && UsesOnly(effect) && Abs(effect.Amount) <= Fixed.Hundred,
        EffectKind.CashDelta => effect.Stat is null
            && UsesOnly(effect)
            && effect.Amount.Raw % Fixed.Scale == 0
            && Abs(effect.Amount) <= Fixed.FromInt(MaxCashEffectPln),
        EffectKind.AudienceMultiplier => effect.Stat is null && UsesOnly(effect) && effect.Amount > Fixed.Zero && effect.Amount <= Fixed.FromInt(10),
        EffectKind.EventQuality => effect.Stat is null && UsesOnly(effect) && Abs(effect.Amount) <= Fixed.Hundred,
        _ => schema == 2 && effect.Stat is null && IsValidSchemaTwoEffect(effect),
    };

    private static bool IsValidSchemaTwoEffect(Effect effect) => effect.Kind switch
    {
        EffectKind.FollowersPercent => UsesOnly(effect) && Abs(effect.Amount) <= Fixed.FromInt(20),
        EffectKind.SkillDelta => effect.Category is not null && UsesOnly(effect, category: true) && Abs(effect.Amount) <= Fixed.FromInt(10),
        EffectKind.ViralChance => UsesOnly(effect) && effect.Amount >= Fixed.Zero && effect.Amount <= Fixed.FromInt(30),
        EffectKind.SetFlag => HasTarget(effect) && UsesOnly(effect, target: true, weeks: true) && effect.Weeks is null or >= 1,
        EffectKind.ClearFlag => HasTarget(effect) && UsesOnly(effect, target: true),
        EffectKind.FollowUp => HasTarget(effect) && UsesOnly(effect, target: true, weeks: true) && effect.Weeks is >= 1 and <= 52,
        _ => false,
    };

    private static bool HasTarget(Effect effect) => !string.IsNullOrEmpty(effect.Target);

    /// <summary>True when every optional field that the kind does not use is null.</summary>
    private static bool UsesOnly(Effect effect, bool category = false, bool target = false, bool weeks = false)
        => (category || effect.Category is null) && (target || effect.Target is null) && (weeks || effect.Weeks is null);

    private static Fixed Abs(Fixed value) => value < Fixed.Zero ? -value : value;

    /// <summary>Every week needs enough events by week range alone; reports the first week of each short run.</summary>
    private static void ValidateWeeklySupply(ContentSnapshot c, List<string> violations)
        => violations.AddRange(ShortRunStarts(c.Events.Where(HasValidWeeks), c.Balance.EventsPerWeekMax)
            .Select(week => $"too-few-eligible:week-{week}"));

    /// <summary>Every category needs enough live events by week range and category; reports the first week of each short run.</summary>
    private static void ValidateCategorySupply(ContentSnapshot c, List<string> violations)
    {
        var live = c.Events.Where(e => e.Phase == EventPhase.Live && HasValidWeeks(e)).ToArray();
        foreach (var category in Enum.GetValues<Category>())
        {
            var eligible = live.Where(e => e.Eligibility.Categories.IsEmpty || e.Eligibility.Categories.Contains(category));
            violations.AddRange(ShortRunStarts(eligible, c.Balance.EventsPerWeekMax)
                .Select(week => $"too-few-eligible:{category}:week-{week}"));
        }
    }

    /// <summary>The first week of each run of weeks in which fewer than <paramref name="needed"/> events are in range.</summary>
    private static IEnumerable<int> ShortRunStarts(IEnumerable<EventDefinition> events, int needed)
    {
        var ranges = events.Select(e => (e.Eligibility.FromWeek, e.Eligibility.ToWeek)).ToArray();
        var previousWeekShort = false;
        for (var week = CareerWeek.First; week <= CareerWeek.Last; week++)
        {
            var eligible = ranges.Count(r => r.FromWeek <= week && week <= r.ToWeek);
            var isShort = eligible < needed;
            if (isShort && !previousWeekShort)
            {
                yield return week;
            }

            previousWeekShort = isShort;
        }
    }

    private static void ValidateSchemaTwo(ContentSnapshot c, List<string> violations)
    {
        var ids = c.Events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var eventChoices = c.Events.SelectMany(e => ChoicesOf(e)).ToArray();
        var flagSetters = c.Balance.NetworkCollaborate is { } network ? eventChoices.Append(network) : eventChoices;
        var setFlags = TargetsOf(flagSetters, EffectKind.SetFlag);
        var followUpTargets = TargetsOf(eventChoices, EffectKind.FollowUp);

        foreach (var e in c.Events)
        {
            ValidateDecisionShape(e, violations);
            ValidateTexts(e.Id, Texts(e), violations);
            violations.AddRange(e.Eligibility.RequiresFlags
                .Where(flag => !setFlags.Contains(flag))
                .Select(flag => $"unset-flag:{e.Id}/{flag}"));
            ValidateChoiceLinks(e, ids, violations);

            var triggered = string.Equals(e.Id, c.Balance.RecoveryCrisisEventId, StringComparison.Ordinal) || followUpTargets.Contains(e.Id);
            if (e.Phase != EventPhase.Live && !HasValidWeeks(e) && !triggered)
            {
                violations.Add($"unreachable-decision:{e.Id}");
            }
        }

        if (c.Balance.NetworkCollaborate is { } networkChoice)
        {
            ValidateTexts(NetworkChoiceOwner, ChoiceTexts(networkChoice), violations);
        }

        if (c.Balance.RecoveryCrisisEventId is { } recovery
            && !c.Events.Any(e => e.Id == recovery && e.Phase == EventPhase.Crisis))
        {
            violations.Add($"missing-recovery-crisis:{recovery}");
        }

        ValidateCategorySupply(c, violations);
    }

    private static HashSet<string> TargetsOf(IEnumerable<ChoiceDefinition> choices, EffectKind kind)
        => choices
            .SelectMany(AllEffects)
            .Where(effect => effect.Kind == kind && effect.Target is not null)
            .Select(effect => effect.Target!)
            .ToHashSet(StringComparer.Ordinal);

    private static void ValidateDecisionShape(EventDefinition e, List<string> violations)
    {
        if (e.Version < 1)
        {
            violations.Add($"version:{e.Id}");
        }

        if (e.Choices.IsDefaultOrEmpty || e.Choices.All(choice => choice.Requires is not null))
        {
            violations.Add($"no-legal-choice:{e.Id}");
        }
    }

    private static void ValidateChoiceLinks(EventDefinition e, HashSet<string> ids, List<string> violations)
    {
        foreach (var choice in ChoicesOf(e))
        {
            violations.AddRange(AllEffects(choice)
                .Where(effect => effect.Kind == EffectKind.FollowUp && effect.Target is not null && !ids.Contains(effect.Target))
                .Select(effect => $"broken-follow-up:{e.Id}/{effect.Target}"));

            if (e.Phase != EventPhase.Live && AllEffects(choice).Any(effect => DecisionForbidden.Contains(effect.Kind)))
            {
                violations.Add($"effect-phase:{e.Id}/{choice.Id}");
            }
        }
    }

    private static void ValidateTexts(string owner, IEnumerable<(string Field, string? Text)> texts, List<string> violations)
    {
        foreach (var (field, text) in texts)
        {
            if (text is null)
            {
                violations.Add($"text:{owner}/{field}:missing");
            }
            else if (IcuMessage.Validate(text) is { } problem)
            {
                violations.Add($"text:{owner}/{field}:{problem}");
            }
        }
    }

    private static IEnumerable<(string Field, string? Text)> Texts(EventDefinition e)
        => new (string Field, string? Text)[] { ("title", e.Title), ("text", e.TextPl) }
            .Concat(ChoicesOf(e).SelectMany(ChoiceTexts));

    private static IEnumerable<(string Field, string? Text)> ChoiceTexts(ChoiceDefinition choice)
    {
        yield return ($"{choice.Id}.label", choice.LabelPl);
        if (choice.SuccessText is { } success)
        {
            yield return ($"{choice.Id}.success", success);
        }

        if (choice.FailureText is { } failure)
        {
            yield return ($"{choice.Id}.failure", failure);
        }
    }
}
