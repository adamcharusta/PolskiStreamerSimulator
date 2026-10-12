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

    WeekPlan ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal);

    string ChooseEventChoice(CareerState s, EventPresentation e);

    bool ShouldRetire(CareerState s);
}
