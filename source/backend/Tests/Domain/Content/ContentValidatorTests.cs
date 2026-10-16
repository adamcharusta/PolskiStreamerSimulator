using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

public class ContentValidatorTests
{
    private static readonly ContentSnapshot Valid = TestContent.Valid();

    private static ContentSnapshot WithFirstEvent(Func<EventDefinition, EventDefinition> change)
        => Valid with { Events = Valid.Events.SetItem(0, change(Valid.Events[0])) };

    private static ContentSnapshot WithFirstChoice(Func<ChoiceDefinition, ChoiceDefinition> change)
        => WithFirstEvent(e => e with { Choices = e.Choices.SetItem(0, change(e.Choices[0])) });

    [Fact]
    public void Repository_content_is_valid() => Assert.Empty(ContentValidator.Validate(Valid));

    [Fact]
    public void Too_few_eligible_events_names_the_week()
    {
        var c = Valid with { Events = [.. Valid.Events.Take(2)] };
        Assert.Contains("too-few-eligible:week-1", ContentValidator.Validate(c));
    }

    [Fact]
    public void Too_few_eligible_events_names_a_late_gap()
    {
        var c = Valid with
        {
            Events = [.. Valid.Events.Select((e, i) => i < 3 ? e with { Eligibility = e.Eligibility with { ToWeek = 500 } } : e)],
        };
        Assert.Equal(["too-few-eligible:week-501"], ContentValidator.Validate(c));
    }

    [Fact]
    public void Chance_outside_0_1_is_rejected()
    {
        var c = WithFirstChoice(choice => choice with { BaseChance = Fixed.FromRatio(12, 10) });
        Assert.Equal([$"chance-range:{Valid.Events[0].Id}/{Valid.Events[0].Choices[0].Id}"], ContentValidator.Validate(c));
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var c = Valid with { Events = Valid.Events.Add(Valid.Events[0]) };
        Assert.Contains($"duplicate-event:{Valid.Events[0].Id}", ContentValidator.Validate(c));
    }

    [Fact]
    public void Duplicate_choice_ids_are_rejected()
    {
        var c = WithFirstEvent(e => e with { Choices = e.Choices.Add(e.Choices[0]) });
        Assert.Equal([$"duplicate-choice:{Valid.Events[0].Id}/{Valid.Events[0].Choices[0].Id}"], ContentValidator.Validate(c));
    }

    [Fact]
    public void Event_without_choices_is_rejected()
    {
        var c = WithFirstEvent(e => e with { Choices = [] });
        Assert.Equal([$"no-choices:{Valid.Events[0].Id}"], ContentValidator.Validate(c));
    }

    [Fact]
    public void Nonpositive_weight_is_rejected()
    {
        var c = WithFirstEvent(e => e with { Weight = 0 });
        Assert.Equal([$"weight-nonpositive:{Valid.Events[0].Id}"], ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData(0, 1040)]
    [InlineData(1, 1041)]
    [InlineData(600, 500)]
    public void Week_range_outside_the_career_is_rejected(int from, int to)
    {
        var c = WithFirstEvent(e => e with { Eligibility = e.Eligibility with { FromWeek = from, ToWeek = to } });
        Assert.Contains($"week-range:{Valid.Events[0].Id}", ContentValidator.Validate(c));
    }

    [Fact]
    public void Missing_profile_is_rejected()
    {
        var c = Valid with { Profiles = Valid.Profiles.Remove(Background.ChessClubRegular) };
        Assert.Equal(["missing-profile:ChessClubRegular"], ContentValidator.Validate(c));
    }

    [Fact]
    public void Inverted_events_per_week_range_is_rejected()
        => Assert.Equal(["balance:events-per-week"], ContentValidator.Validate(Valid with { Balance = Valid.Balance with { EventsPerWeekMin = 4 } }));

    [Fact]
    public void Equipment_tables_must_cover_every_tier()
        => Assert.Equal(["balance:equipment-tiers"], ContentValidator.Validate(Valid with { Balance = Valid.Balance with { EquipmentPricePln = [0, 600] } }));

    [Fact]
    public void Stat_effect_without_a_stat_is_rejected()
    {
        var c = WithFirstChoice(choice => choice with
        {
            Outcomes = choice.Outcomes with { Success = [new Effect(EffectKind.StatDelta, null, Fixed.One)] },
        });
        Assert.Equal([$"effect-invalid:{Valid.Events[0].Id}/{Valid.Events[0].Choices[0].Id}"], ContentValidator.Validate(c));
    }

    [Fact]
    public void Fractional_cash_effect_is_rejected()
    {
        var c = WithFirstChoice(choice => choice with
        {
            Outcomes = choice.Outcomes with { Failure = [new Effect(EffectKind.CashDelta, null, Fixed.FromRatio(1, 2))] },
        });
        Assert.Equal([$"effect-invalid:{Valid.Events[0].Id}/{Valid.Events[0].Choices[0].Id}"], ContentValidator.Validate(c));
    }
}
