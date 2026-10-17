using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// Immutable, validated content and balance version; <see cref="Digest"/> identifies it inside careers.
/// <see cref="Schema"/> is the event schema (1 for M1, 2 for M3) and <see cref="RulesetId"/> the ruleset the content targets.
/// </summary>
public sealed record ContentSnapshot(
    string Version,
    string Digest,
    BalanceConstants Balance,
    ImmutableArray<EventDefinition> Events,
    ImmutableDictionary<Background, StartingProfile> Profiles,
    int Schema = 1,
    string RulesetId = "m1-prototype.1");
