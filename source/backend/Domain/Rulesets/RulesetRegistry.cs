using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Keeps every supported ruleset by ID; never falls back to another version.</summary>
public sealed class RulesetRegistry
{
    private readonly Dictionary<string, IRuleset> rulesets;

    public RulesetRegistry(IEnumerable<IRuleset> rulesets)
    {
        var byId = rulesets.GroupBy(r => r.Id, StringComparer.Ordinal).ToList();
        var duplicate = byId.Find(g => g.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate ruleset ID '{duplicate.Key}'.", nameof(rulesets));
        }

        this.rulesets = byId.ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
    }

    public Result<IRuleset> Resolve(string rulesetId)
        => rulesets.TryGetValue(rulesetId, out var ruleset)
            ? Result<IRuleset>.Ok(ruleset)
            : Result<IRuleset>.Fail(DomainErrorCode.UnknownRuleset, rulesetId);
}
