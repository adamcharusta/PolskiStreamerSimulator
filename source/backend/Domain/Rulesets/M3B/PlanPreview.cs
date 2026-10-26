using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3B;

/// <summary>M3-B: a plan's policy exposure, its strike chance and the 1–100 threshold the strike roll is drawn against.</summary>
public sealed record PolicyExposure(int Exposure, Fixed Chance, int Threshold);

/// <summary>
/// M3-B: what a plan risks before the player commits: the wager's terms (when the plan stakes one), the missed
/// installments and breaches it would cause in the sponsor deals, its policy exposure (with a policy section) and the estimate of a platform move.
/// </summary>
public sealed record PlanPreview(WagerTerms? Wager, ImmutableArray<SponsorConsequence> Sponsors, PolicyExposure? Policy = null, MigrationEstimate? Migration = null)
{
    /// <summary>A plan that risks nothing.</summary>
    public static PlanPreview None { get; } = new(null, ImmutableArray<SponsorConsequence>.Empty);
}
