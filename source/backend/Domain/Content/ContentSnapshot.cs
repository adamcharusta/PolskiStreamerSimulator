using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>Immutable, validated content and balance version; <see cref="Digest"/> identifies it inside careers.</summary>
public sealed record ContentSnapshot(
    string Version,
    string Digest,
    BalanceConstants Balance,
    ImmutableArray<EventDefinition> Events,
    ImmutableDictionary<Background, StartingProfile> Profiles);
