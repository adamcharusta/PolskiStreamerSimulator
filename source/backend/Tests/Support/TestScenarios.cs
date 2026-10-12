using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Builds pending weeks whose next roll is known, using a single synthetic "probe" event.</summary>
public static class TestScenarios
{
    public const string ProbeEventId = "probe";
    public const int ProbeCooldownWeeks = 2;

    public static readonly ImmutableArray<Effect> ProbeSuccess =
    [
        new(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(2)),
        new(EffectKind.CashDelta, null, Fixed.FromInt(100)),
        new(EffectKind.AudienceMultiplier, null, Fixed.FromRatio(12, 10)),
        new(EffectKind.EventQuality, null, Fixed.FromInt(5)),
    ];

    public static readonly ImmutableArray<Effect> ProbeFailure =
    [
        new(EffectKind.StatDelta, StatKey.Drama, Fixed.FromInt(3)),
        new(EffectKind.CashDelta, null, Fixed.FromInt(-50)),
        new(EffectKind.AudienceMultiplier, null, Fixed.FromRatio(9, 10)),
        new(EffectKind.EventQuality, null, Fixed.FromInt(-4)),
    ];

    public static readonly ImmutableArray<Effect> ProbeCriticalSuccess = [new(EffectKind.EventQuality, null, Fixed.FromInt(8))];

    public static readonly ImmutableArray<Effect> ProbeCriticalFailure = [new(EffectKind.CashDelta, null, Fixed.FromInt(-150))];

    /// <summary>Repository content whose only event is the probe, with choices "risky", "risky-crit" and "safe".</summary>
    public static ContentSnapshot ProbeContent(Fixed baseChance) => TestWorld.Content with { Events = [ProbeEvent(baseChance)] };

    public static EventDefinition ProbeEvent(Fixed baseChance) => new(
        ProbeEventId,
        "event.probe.title",
        "Zdarzenie testowe.",
        1,
        ProbeCooldownWeeks,
        new Eligibility(1, CareerWeek.Last, ImmutableDictionary<StatKey, Fixed>.Empty, ImmutableDictionary<StatKey, Fixed>.Empty),
        [
            Risky("risky", baseChance, allowsCritical: false),
            Risky("risky-crit", baseChance, allowsCritical: true),
            new ChoiceDefinition("safe", "event.probe.safe", "Bezpiecznie", true, Fixed.One, [], false, new OutcomeSet(ProbeSuccess, [], null, null)),
        ]);

    /// <summary>A started week on probe content whose next 1–100 roll equals <paramref name="target"/>.</summary>
    public static (CareerState State, ContentSnapshot Content) PendingWithFirstRoll(int target, Fixed choiceBase, Difficulty difficulty = Difficulty.Normal)
    {
        var content = ProbeContent(choiceBase);
        for (ulong seed = 1; seed <= 10_000; seed++)
        {
            var s = TestCareers.Dorm(seed) with { Difficulty = difficulty };
            var started = TestWorld.Engine.StartWeek(s, content, new WeekPlan(MainAction.Rest, Category.Fps), s.StateVersion).Value;
            if (started.Rng.NextInt(1, 100).Value == target)
            {
                return (started, content);
            }
        }

        throw new InvalidOperationException($"No seed up to 10,000 gives a first roll of {target}.");
    }

    private static ChoiceDefinition Risky(string id, Fixed baseChance, bool allowsCritical) => new(
        id,
        $"event.probe.{id}",
        "Ryzykownie",
        false,
        baseChance,
        [],
        allowsCritical,
        new OutcomeSet(ProbeSuccess, ProbeFailure, ProbeCriticalSuccess, ProbeCriticalFailure));
}
