using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

public class ContentValidatorV2Tests
{
    private static readonly ContentSnapshot Valid = M3TestContent.Valid();

    private static ContentSnapshot WithEvent(string id, Func<EventDefinition, EventDefinition> change)
        => Valid with { Events = [.. Valid.Events.Select(e => e.Id == id ? change(e) : e)] };

    [Fact]
    public void Test_content_is_valid() => Assert.Empty(ContentValidator.Validate(Valid));

    [Fact]
    public void Event_whose_every_choice_has_requirements_has_no_legal_option()
    {
        var c = WithEvent("chat-raid", e => e with
        {
            Choices = [.. e.Choices.Select(ch => ch with { Requires = new ChoiceRequirement(500, null) })],
        });
        Assert.Contains("no-legal-choice:chat-raid", ContentValidator.Validate(c));
    }

    [Fact]
    public void Missing_grammatical_branch_is_reported_with_its_field()
    {
        var c = WithEvent("chat-raid", e => e with { TextPl = "{gender, select, female {a} other {c}}" });
        Assert.Contains("text:chat-raid/text:missing-branch:male", ContentValidator.Validate(c));
    }

    [Fact]
    public void Follow_up_to_unknown_event_is_reported()
    {
        var c = WithEvent("chat-raid", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with
            {
                Outcomes = e.Choices[0].Outcomes with
                {
                    Success = [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "nope", Weeks: 2)],
                },
            }),
        });
        Assert.Contains("broken-follow-up:chat-raid/nope", ContentValidator.Validate(c));
    }

    [Fact]
    public void Follow_up_to_a_one_time_event_is_reported()
    {
        var c = WithEvent("chat-raid", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with
            {
                Outcomes = e.Choices[0].Outcomes with
                {
                    Success = [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "first-week", Weeks: 2)],
                },
            }),
        });
        Assert.Contains("follow-up-one-time:chat-raid/first-week", ContentValidator.Validate(c));
    }

    [Fact]
    public void Required_flag_that_nothing_sets_is_reported()
    {
        var c = WithEvent("chat-raid", e => e with { Eligibility = e.Eligibility with { RequiresFlags = ["ghost"] } });
        Assert.Contains("unset-flag:chat-raid/ghost", ContentValidator.Validate(c));
    }

    [Fact]
    public void Audience_effect_in_a_decision_event_is_rejected()
    {
        var c = WithEvent("test-crisis", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with
            {
                Outcomes = e.Choices[0].Outcomes with { Success = [new Effect(EffectKind.AudienceMultiplier, null, Fixed.FromInt(2))] },
            }),
        });
        Assert.Contains($"effect-phase:test-crisis/{Valid.Events.Single(e => e.Id == "test-crisis").Choices[0].Id}", ContentValidator.Validate(c));
    }

    [Fact]
    public void Missing_recovery_crisis_event_is_reported()
    {
        var c = Valid with { Events = [.. Valid.Events.Where(e => e.Id != "burnout-recovery")] };
        Assert.Contains("missing-recovery-crisis:burnout-recovery", ContentValidator.Validate(c));
    }

    [Fact]
    public void Category_without_enough_live_events_names_category_and_week()
    {
        var c = Valid with
        {
            Events = [.. Valid.Events.Select(e => e.Phase == EventPhase.Live
                ? e with { Eligibility = e.Eligibility with { Categories = [PolskiStreamerSimulator.Domain.Careers.Category.Chess] } }
                : e)],
        };
        Assert.Contains("too-few-eligible:Fps:week-1", ContentValidator.Validate(c));
    }

    [Fact]
    public void Schema_one_rejects_m3_effect_kinds()
    {
        var m1 = TestContent.Valid();
        var e = m1.Events[0];
        var changed = m1 with
        {
            Events = m1.Events.SetItem(0, e with
            {
                Choices = e.Choices.SetItem(0, e.Choices[0] with
                {
                    Outcomes = e.Choices[0].Outcomes with { Success = [new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "x")] },
                }),
            }),
        };
        Assert.Contains($"effect-invalid:{e.Id}/{e.Choices[0].Id}", ContentValidator.Validate(changed));
    }

    [Fact]
    public void Stat_delta_on_followers_is_rejected()
    {
        var c = WithEvent("test-crisis", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with
            {
                Outcomes = e.Choices[0].Outcomes with { Success = [new Effect(EffectKind.StatDelta, StatKey.Followers, Fixed.One)] },
            }),
        });
        Assert.Contains("effect-invalid:test-crisis/calm", ContentValidator.Validate(c));
    }

    [Fact]
    public void Network_choice_is_checked_like_an_event_choice_and_can_set_flags()
    {
        var network = new ChoiceDefinition("network", null, "{gender, select, female {a} male {b}}", true, Fixed.One, [], false,
            new OutcomeSet([new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "networked")], [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "x")], null, null));
        var withRequirement = WithEvent("chat-raid", e => e with { Eligibility = e.Eligibility with { RequiresFlags = ["networked"] } });
        var c = withRequirement with { Balance = Valid.Balance with { NetworkCollaborate = network } };
        var violations = ContentValidator.Validate(c);
        Assert.Contains("effect-invalid:balance.networkCollaborate/network", violations);
        Assert.Contains("text:balance.networkCollaborate/network.label:missing-branch:other", violations);
        Assert.DoesNotContain("unset-flag:chat-raid/networked", violations);
    }

    [Fact]
    public void Untriggered_decision_event_outside_career_weeks_is_unreachable()
    {
        var c = WithEvent("first-week", e => e with { Eligibility = e.Eligibility with { FromWeek = 0 } });
        Assert.Contains("unreachable-decision:first-week", ContentValidator.Validate(c));
    }

    [Fact]
    public void Optional_event_arrays_are_empty_not_default_in_both_schemas()
    {
        var m1Event = TestContent.Valid().Events[0];
        var m3Live = Valid.Events.First(e => e.Phase == EventPhase.Live);
        foreach (var e in new[] { m1Event, m3Live })
        {
            Assert.Equal((false, false, false, false), (e.Eligibility.Categories.IsDefault, e.Eligibility.RequiresFlags.IsDefault, e.Eligibility.ForbidsFlags.IsDefault, e.Tags.IsDefault));
            Assert.Empty(e.Eligibility.Categories);
            Assert.Empty(e.Tags);
        }
    }

    [Fact]
    public void Network_follow_up_to_unknown_event_is_reported_and_its_target_counts_as_triggered()
    {
        var network = Valid.Balance.NetworkCollaborate!;
        var typo = network with
        {
            Outcomes = network.Outcomes with { CriticalFailure = [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "test-crisys", Weeks: 1)] },
        };
        var violations = ContentValidator.Validate(Valid with { Balance = Valid.Balance with { NetworkCollaborate = typo } });
        Assert.Contains($"broken-follow-up:{ContentValidator.NetworkChoiceOwner}/test-crisys", violations);

        var onlyNetworkTriggers = WithEvent("test-crisis", e => e with { Eligibility = e.Eligibility with { FromWeek = 0 } });
        Assert.DoesNotContain("unreachable-decision:test-crisis", ContentValidator.Validate(onlyNetworkTriggers));
    }

    [Fact]
    public void Critical_outcomes_in_a_decision_event_are_rejected()
    {
        var c = WithEvent("test-crisis", e => e with { Choices = e.Choices.SetItem(0, e.Choices[0] with { AllowsCritical = true }) });
        Assert.Contains("critical-in-decision:test-crisis/calm", ContentValidator.Validate(c));
    }

    [Theory]
    [InlineData("noise-order")]
    [InlineData("noise-zero")]
    [InlineData("boredom-order")]
    [InlineData("boredom-negative")]
    [InlineData("reach")]
    [InlineData("conversion")]
    [InlineData("churn")]
    [InlineData("yield")]
    [InlineData("quality-weight")]
    [InlineData("viral-above-cap")]
    [InlineData("viral-negative")]
    public void Unsafe_category_profile_is_reported(string problem)
    {
        var fps = Valid.Balance.Profile(Category.Fps);
        var bad = problem switch
        {
            "noise-order" => fps with { NoiseMin = Fixed.FromRatio(12, 10), NoiseMax = Fixed.FromRatio(11, 10) },
            "noise-zero" => fps with { NoiseMin = Fixed.Zero },
            "boredom-order" => fps with { BoredomGainMin = 5, BoredomGainMax = 4 },
            "boredom-negative" => fps with { BoredomGainMin = -1 },
            "reach" => fps with { Reach = Fixed.Zero },
            "conversion" => fps with { Conversion = Fixed.Zero },
            "churn" => fps with { Churn = -Fixed.One },
            "yield" => fps with { AdYield = Fixed.Zero },
            "quality-weight" => fps with { QualitySkillWeight = Fixed.Zero },
            "viral-above-cap" => fps with { ViralBase = Fixed.FromRatio(31, 100) },
            _ => fps with { ViralBase = Fixed.FromRatio(-1, 100) },
        };
        var c = Valid with { Balance = Valid.Balance with { Categories = Valid.Balance.Categories!.With(Category.Fps, bad) } };
        Assert.Contains("balance:category:Fps", ContentValidator.Validate(c));
    }

    public static TheoryData<string> UnsafeBalances() =>
    [
        "viral:cap", "viral:negative-cap", "viral:multiplier-order", "viral:multiplier-below-one", "viral:share", "viral:follower-min",
        "short-stream:fatigue-zero", "short-stream:audience-above-one",
        "burnout-penalty:from", "burnout-penalty:per-point", "burnout-penalty:cap",
        "mastery:relief", "mastery:churn",
    ];

    [Theory]
    [MemberData(nameof(UnsafeBalances))]
    public void Unsafe_balance_section_is_reported(string problem)
    {
        var b = Valid.Balance;
        var bad = problem switch
        {
            "viral:cap" => b with { ViralChanceCap = Fixed.FromRatio(11, 10) },
            "viral:negative-cap" => b with { ViralChanceCap = Fixed.FromRatio(-1, 100) },
            "viral:multiplier-order" => b with { ViralMultiplierMin = Fixed.FromInt(4) },
            "viral:multiplier-below-one" => b with { ViralMultiplierMin = Fixed.FromRatio(9, 10) },
            "viral:share" => b with { ViralViewerShare = Fixed.FromRatio(11, 10) },
            "viral:follower-min" => b with { ViralFollowerMin = -1 },
            "short-stream:fatigue-zero" => b with { ShortStreamFatigue = Fixed.Zero },
            "short-stream:audience-above-one" => b with { ShortStreamAudience = Fixed.FromRatio(11, 10) },
            "burnout-penalty:from" => b with { BurnoutPenaltyFrom = -Fixed.One },
            "burnout-penalty:per-point" => b with { BurnoutPenaltyPerPoint = Fixed.FromRatio(-1, 100) },
            "burnout-penalty:cap" => b with { BurnoutPenaltyCap = Fixed.FromRatio(-1, 100) },
            "mastery:relief" => b with { MasteryBoredomRelief = -1 },
            _ => b with { MasteryChurnFactor = Fixed.Zero },
        };
        Assert.Contains($"balance:{problem[..problem.IndexOf(':', StringComparison.Ordinal)]}", ContentValidator.Validate(Valid with { Balance = bad }));
    }

    [Fact]
    public void Neutral_m1_balance_defaults_pass_the_new_balance_checks()
        => Assert.DoesNotContain(ContentValidator.Validate(TestContent.Valid()), v => v.StartsWith("balance:", StringComparison.Ordinal));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Unknown_schema_is_reported(int schema)
        => Assert.Contains($"schema:{schema}", ContentValidator.Validate(Valid with { Schema = schema }));

    private static readonly Effect[] StrayEffects =
    [
        new Effect(EffectKind.CashDelta, null, Fixed.FromInt(10), Target: "x"),
        new Effect(EffectKind.StatDelta, StatKey.Drama, Fixed.One, PolskiStreamerSimulator.Domain.Careers.Category.Fps),
        new Effect(EffectKind.FollowersPercent, StatKey.Drama, Fixed.One),
        new Effect(EffectKind.FollowersPercent, null, Fixed.One, Weeks: 2),
        new Effect(EffectKind.ViralChance, null, Fixed.One, Target: "x"),
        new Effect(EffectKind.SkillDelta, null, Fixed.One, PolskiStreamerSimulator.Domain.Careers.Category.Fps, Target: "x"),
        new Effect(EffectKind.SetFlag, null, Fixed.Zero, PolskiStreamerSimulator.Domain.Careers.Category.Fps, Target: "x"),
        new Effect(EffectKind.ClearFlag, null, Fixed.Zero, Target: "x", Weeks: 1),
        new Effect(EffectKind.FollowUp, StatKey.Energy, Fixed.Zero, Target: "chat-raid", Weeks: 1),
    ];

    public static TheoryData<int> StrayEffectFields() => [.. Enumerable.Range(0, StrayEffects.Length)];

    [Theory]
    [MemberData(nameof(StrayEffectFields))]
    public void Effect_with_fields_its_kind_does_not_use_is_rejected(int index)
    {
        var effect = StrayEffects[index];
        var c = WithEvent("chat-raid", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with { Outcomes = e.Choices[0].Outcomes with { Success = [effect] } }),
        });
        Assert.Contains($"effect-invalid:chat-raid/{Valid.Events[0].Choices[0].Id}", ContentValidator.Validate(c));
    }
}
