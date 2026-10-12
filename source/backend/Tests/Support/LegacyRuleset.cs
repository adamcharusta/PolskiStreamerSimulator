using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>
/// Test-only older ruleset: behaves like M1 except that <see cref="StartWeek"/> always rejects with
/// detail "legacy", so a test can tell which ruleset the engine resolved.
/// </summary>
public sealed class LegacyRuleset(string id = LegacyRuleset.LegacyId) : IRuleset
{
    public const string LegacyId = "legacy.0";

    private readonly M1Ruleset inner = new();

    public string Id => id;

    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
        => Result<CareerState>.Fail(DomainErrorCode.IllegalAction, "legacy");

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c) => inner.LegalPlans(s, c);

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c) => inner.PresentEvent(s, c);

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
        => inner.ResolveEvent(s, c, choiceId, expectedVersion);

    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)
        => inner.SettleWeek(s, c, expectedVersion);

    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion) => inner.Retire(s, c, expectedVersion);
}
