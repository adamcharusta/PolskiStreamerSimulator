namespace PolskiStreamerSimulator.Domain.Rulesets;

/// <summary>Versioned rule implementation; a career always uses the ruleset it started with.</summary>
public interface IRuleset
{
    string Id { get; }
}
