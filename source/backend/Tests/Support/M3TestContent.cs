using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>
/// Schema-2 content for M3 rule tests: the M1 events as live events plus a one-time milestone in week 2, a
/// repeatable crisis and the recovery crisis. Built in code so rule tests do not depend on authored Polish content.
/// </summary>
public static class M3TestContent
{
    public const string RulesetId = "m3-systems.1";

    private static readonly Lazy<ContentSnapshot> Snapshot = new(Build);

    public static ContentSnapshot Valid() => Snapshot.Value;

    private static ContentSnapshot Build()
    {
        var m1 = TestContent.Valid();
        var live = m1.Events.Select(e => e with { Title = $"Tytuł {e.Id}", TitleKey = null }).ToImmutableArray();
        return m1 with
        {
            Schema = 2,
            RulesetId = RulesetId,
            Digest = "m3-test",
            Balance = m1.Balance with
            {
                RecoveryCrisisEventId = "burnout-recovery",
                DecisionChancePerWeek = Fixed.FromRatio(6, 100),
                NetworkEnergyCost = Fixed.FromInt(10),
                NetworkCollaborate = new ChoiceDefinition(
                    "collaborate", null, "Zagadaj do innych twórców", false, Fixed.FromRatio(35, 100),
                    [
                        new ChanceModifier(StatKey.Credibility, Fixed.FromRatio(3, 1000), null, "modifier.credibility"),
                        new ChanceModifier(StatKey.Drama, Fixed.FromRatio(-3, 1000), null, "modifier.drama"),
                    ],
                    true,
                    new OutcomeSet(
                        [new Effect(EffectKind.AudienceMultiplier, null, Fixed.FromRatio(12, 10)), new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.One)],
                        [new Effect(EffectKind.StatDelta, StatKey.Drama, Fixed.FromInt(4)), new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(-2))],
                        [new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "collab-friend", Weeks: 26)],
                        [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "test-crisis", Weeks: 1)])),
                BurnoutPenaltyFrom = Fixed.FromInt(60),
                BurnoutPenaltyPerPoint = Fixed.FromRatio(25, 10000),
                BurnoutPenaltyCap = Fixed.FromRatio(10, 100),
                ShortStreamFatigue = Fixed.FromRatio(1, 2),
                ShortStreamAudience = Fixed.FromRatio(7, 10),
                Categories = Profiles(),
                MasteryFromSkill = Fixed.FromInt(70),
                MasteryBoredomRelief = 1,
                MasteryChurnFactor = Fixed.FromRatio(85, 100),
                ViralQualityFrom = Fixed.FromInt(70),
                ViralPerQualityPoint = Fixed.FromRatio(2, 1000),
                ViralPerCritical = Fixed.FromRatio(5, 100),
                ViralChanceCap = Fixed.FromRatio(30, 100),
                ViralMultiplierMin = Fixed.FromRatio(15, 10),
                ViralMultiplierMax = Fixed.FromInt(3),
                ViralFollowerShare = Fixed.FromRatio(10, 100),
                ViralViewerShare = Fixed.FromRatio(20, 100),
                ViralFollowerMin = 100,
            },
            Events = live.AddRange(
                Decision("first-week", EventPhase.Milestone, fromWeek: 2, toWeek: 2, oneTime: true),
                Decision("test-crisis", EventPhase.Crisis, fromWeek: 1, toWeek: 1040, oneTime: false),
                Recovery()),
        };
    }

    /// <summary>The content/m3 category table: JustChatting, Fps, Moba, Chess, Gambling.</summary>
    private static CategoryValues<CategoryProfile> Profiles()
    {
        static Fixed H(int hundredths) => Fixed.FromRatio(hundredths, 100);
        return new CategoryValues<CategoryProfile>(
        [
            new(H(100), H(85), H(115), H(115), H(100), 2, 4, H(100), H(130), H(100), H(100), H(0), H(0), H(2)),
            new(H(110), H(85), H(115), H(100), H(100), 2, 4, H(100), H(100), H(110), H(120), H(0), H(0), H(3)),
            new(H(95), H(85), H(115), H(100), H(85), 1, 3, H(110), H(100), H(100), H(100), H(0), H(0), H(2)),
            new(H(75), H(92), H(108), H(105), H(70), 1, 3, H(130), H(100), H(100), H(100), H(20), H(0), H(1)),
            new(H(125), H(70), H(140), H(90), H(120), 2, 4, H(100), H(120), H(100), H(100), H(-30), H(100), H(4)),
        ]);
    }

    public static EventDefinition Decision(string id, EventPhase phase, int fromWeek, int toWeek, bool oneTime) => new(
        id,
        null,
        "{gender, select, female {Zrobiłaś krok.} male {Zrobiłeś krok.} other {Zrobiłoś krok.}}",
        1,
        13,
        new Eligibility(fromWeek, toWeek, ImmutableDictionary<StatKey, Fixed>.Empty, ImmutableDictionary<StatKey, Fixed>.Empty),
        [
            new ChoiceDefinition("calm", null, "Spokojnie", true, Fixed.One, [], false,
                new OutcomeSet([new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(1))], [], null, null)),
            new ChoiceDefinition("bold", null, "Odważnie", false, Fixed.FromRatio(1, 2), [], false,
                new OutcomeSet(
                    [new Effect(EffectKind.CashDelta, null, Fixed.FromInt(300))],
                    [new Effect(EffectKind.CashDelta, null, Fixed.FromInt(-200)), new Effect(EffectKind.FollowersPercent, null, Fixed.FromInt(-2))],
                    null,
                    null)),
        ],
        Phase: phase,
        Title: $"Tytuł {id}",
        OneTime: oneTime);

    private static EventDefinition Recovery() => Decision("burnout-recovery", EventPhase.Crisis, 1, 1040, false) with
    {
        Weight = 1,
        Choices =
        [
            new ChoiceDefinition("week-off", null, "Wolny tydzień", true, Fixed.One, [], false,
                new OutcomeSet(
                    [
                        new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "sys:rest-only", Weeks: 1),
                        new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "sys:short-stream", Weeks: 1),
                    ],
                    [],
                    null,
                    null)),
            new ChoiceDefinition("push-through", null, "Ciśniemy dalej", false, Fixed.FromRatio(1, 2), [], false,
                new OutcomeSet(
                    [new Effect(EffectKind.StatDelta, StatKey.Energy, Fixed.FromInt(10))],
                    [new Effect(EffectKind.StatDelta, StatKey.Burnout, Fixed.FromInt(10)), new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(-5))],
                    null,
                    null)),
        ],
    };
}
