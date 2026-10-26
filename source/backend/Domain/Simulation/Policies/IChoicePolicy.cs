using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>
/// Automated decision-maker. Manual and automated play call the same engine commands; a policy only picks
/// among the plans and choices the engine offers.
/// </summary>
public interface IChoicePolicy
{
    string Name { get; }

    PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal);

    ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e);

    /// <summary>
    /// M3-B: accepts or declines an open sponsor offer between weeks; <paramref name="blockedReason"/> is the engine's
    /// refusal of an acceptance (null when it would accept). Declines by default.
    /// </summary>
    OfferDecision ChooseOffer(CareerState s, ContentSnapshot c, SponsorOffer offer, string? blockedReason)
        => new(false, PolicyReason.Of("offer-declined", ("templateId", offer.TemplateId), ("reason", blockedReason ?? "policy")));

    bool ShouldRetire(CareerState s);
}
