using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>
/// M3-B: what a plan risks before the player commits: the wager's terms (when the plan stakes one) and the missed
/// installments and breaches it would cause in the sponsor deals.
/// </summary>
public sealed record PlanPreview(WagerTerms? Wager, ImmutableArray<SponsorConsequence> Sponsors)
{
    /// <summary>A plan that risks nothing.</summary>
    public static PlanPreview None { get; } = new(null, ImmutableArray<SponsorConsequence>.Empty);
}
