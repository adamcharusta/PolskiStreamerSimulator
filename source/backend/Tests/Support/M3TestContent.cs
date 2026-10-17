using System.Collections.Immutable;
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
            },
            Events = live.AddRange(
                Decision("first-week", EventPhase.Milestone, fromWeek: 2, toWeek: 2, oneTime: true),
                Decision("test-crisis", EventPhase.Crisis, fromWeek: 1, toWeek: 1040, oneTime: false),
                Recovery()),
        };
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
