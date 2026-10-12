using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// Entry point for career commands: resolves the career's own ruleset (never the newest) and checks
/// that the supplied content is the snapshot the career was created with.
/// </summary>
public sealed class WeekEngine(RulesetRegistry registry)
{
    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess ? ruleset.Value.StartWeek(s, c, plan, expectedVersion) : Result<CareerState>.Fail(ruleset.Error);
    }

    public Result<IReadOnlyList<(WeekPlan Plan, string? DisabledReason)>> LegalPlans(CareerState s, ContentSnapshot c)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess
            ? Result<IReadOnlyList<(WeekPlan Plan, string? DisabledReason)>>.Ok(ruleset.Value.LegalPlans(s, c))
            : Result<IReadOnlyList<(WeekPlan Plan, string? DisabledReason)>>.Fail(ruleset.Error);
    }

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess ? ruleset.Value.PresentEvent(s, c) : Result<EventPresentation>.Fail(ruleset.Error);
    }

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess ? ruleset.Value.ResolveEvent(s, c, choiceId, expectedVersion) : Result<CareerState>.Fail(ruleset.Error);
    }

    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess ? ruleset.Value.SettleWeek(s, c, expectedVersion) : Result<(CareerState, WeekReport)>.Fail(ruleset.Error);
    }

    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess ? ruleset.Value.Retire(s, c, expectedVersion) : Result<CareerState>.Fail(ruleset.Error);
    }

    private Result<IRuleset> Resolve(CareerState s, ContentSnapshot c)
    {
        var ruleset = registry.Resolve(s.RulesetId);
        if (ruleset.IsSuccess && !string.Equals(c.Digest, s.ContentDigest, StringComparison.Ordinal))
        {
            return Result<IRuleset>.Fail(DomainErrorCode.ContentMismatch, $"career-{s.ContentDigest}-content-{c.Digest}");
        }

        return ruleset;
    }
}
