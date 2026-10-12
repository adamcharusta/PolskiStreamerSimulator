using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>Rejects content that a career could not safely run on, before any career starts.</summary>
public static class ContentValidator
{
    private const long MaxCashEffectPln = 1_000_000;

    public static IReadOnlyList<string> Validate(ContentSnapshot c)
    {
        var violations = new List<string>();
        var b = c.Balance;
        if (b.EventsPerWeekMin < 1 || b.EventsPerWeekMin > b.EventsPerWeekMax)
        {
            violations.Add("balance:events-per-week");
        }

        if (b.EquipmentPricePln.Length != CareerInvariants.MaxEquipmentTier + 1
            || b.EquipmentUpkeepPerTierPln.Length != CareerInvariants.MaxEquipmentTier + 1)
        {
            violations.Add("balance:equipment-tiers");
        }

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in c.Events)
        {
            ValidateEvent(e, eventIds, violations);
        }

        ValidateWeeklySupply(c, violations);
        foreach (var background in Enum.GetValues<Background>())
        {
            if (!c.Profiles.ContainsKey(background))
            {
                violations.Add($"missing-profile:{background}");
            }
        }

        return violations;
    }

    private static void ValidateEvent(EventDefinition e, HashSet<string> eventIds, List<string> violations)
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
        foreach (var choice in e.Choices.IsDefault ? [] : e.Choices)
        {
            if (!choiceIds.Add(choice.Id))
            {
                violations.Add($"duplicate-choice:{e.Id}/{choice.Id}");
            }

            if (!choice.Guaranteed && (choice.BaseChance < Fixed.Zero || choice.BaseChance > Fixed.One))
            {
                violations.Add($"chance-range:{e.Id}/{choice.Id}");
            }

            var outcomes = choice.Outcomes;
            var effects = outcomes.Success
                .Concat(outcomes.Failure)
                .Concat(outcomes.CriticalSuccess ?? [])
                .Concat(outcomes.CriticalFailure ?? []);
            if (!effects.All(IsValidEffect))
            {
                violations.Add($"effect-invalid:{e.Id}/{choice.Id}");
            }
        }
    }

    private static bool HasValidWeeks(EventDefinition e)
        => e.Eligibility.FromWeek >= CareerWeek.First
            && e.Eligibility.ToWeek <= CareerWeek.Last
            && e.Eligibility.FromWeek <= e.Eligibility.ToWeek;

    private static bool IsValidEffect(Effect effect) => effect.Kind switch
    {
        EffectKind.StatDelta => effect.Stat is not null && Abs(effect.Amount) <= Fixed.Hundred,
        EffectKind.CashDelta => effect.Stat is null
            && effect.Amount.Raw % Fixed.Scale == 0
            && Abs(effect.Amount) <= Fixed.FromInt(MaxCashEffectPln),
        EffectKind.AudienceMultiplier => effect.Stat is null && effect.Amount > Fixed.Zero && effect.Amount <= Fixed.FromInt(10),
        EffectKind.EventQuality => effect.Stat is null && Abs(effect.Amount) <= Fixed.Hundred,
        _ => false,
    };

    private static Fixed Abs(Fixed value) => value < Fixed.Zero ? -value : value;

    /// <summary>Every week needs enough events by week range alone; reports the first week of each short run.</summary>
    private static void ValidateWeeklySupply(ContentSnapshot c, List<string> violations)
    {
        var events = c.Events.Where(HasValidWeeks).ToArray();
        var previousWeekShort = false;
        for (var week = CareerWeek.First; week <= CareerWeek.Last; week++)
        {
            var eligible = events.Count(e => e.Eligibility.FromWeek <= week && week <= e.Eligibility.ToWeek);
            var isShort = eligible < c.Balance.EventsPerWeekMax;
            if (isShort && !previousWeekShort)
            {
                violations.Add($"too-few-eligible:week-{week}");
            }

            previousWeekShort = isShort;
        }
    }
}
