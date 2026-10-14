using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>M1 rules that demand a player decision before one week; stands in for the mandatory events of M3.</summary>
public sealed class PausingRuleset(int pauseBeforeWeek) : IRuleset
{
    private readonly M1Ruleset inner = new();

    public string Id => inner.Id;

    public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c) => s.Pending is null && s.Week.Value == pauseBeforeWeek;

    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion) => inner.StartWeek(s, c, plan, expectedVersion);

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c) => inner.LegalPlans(s, c);

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c) => inner.PresentEvent(s, c);

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion) => inner.ResolveEvent(s, c, choiceId, expectedVersion);

    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion) => inner.SettleWeek(s, c, expectedVersion);

    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion) => inner.Retire(s, c, expectedVersion);

    public static WeekEngine Engine(int pauseBeforeWeek) => new(new RulesetRegistry([new PausingRuleset(pauseBeforeWeek)]));
}
