using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Keeps every supported ruleset by ID; never falls back to another version.</summary>
public sealed class RulesetRegistry
{
    private readonly Dictionary<string, IRuleset> rulesets = new(StringComparer.Ordinal);

    public RulesetRegistry(IEnumerable<IRuleset> rulesets)
    {
        foreach (var ruleset in rulesets)
        {
            if (!this.rulesets.TryAdd(ruleset.Id, ruleset))
            {
                throw new ArgumentException($"Duplicate ruleset ID '{ruleset.Id}'.", nameof(rulesets));
            }
        }
    }

    public Result<IRuleset> Resolve(string rulesetId)
        => rulesets.TryGetValue(rulesetId, out var ruleset)
            ? Result<IRuleset>.Ok(ruleset)
            : Result<IRuleset>.Fail(DomainErrorCode.UnknownRuleset, rulesetId);
}
