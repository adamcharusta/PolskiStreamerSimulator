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
}
