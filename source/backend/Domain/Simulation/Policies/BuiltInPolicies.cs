using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>The M1 simulation policies used by tests and the batch simulator. None of them retires early.</summary>
public static class BuiltInPolicies
{
    /// <summary>Rule-based play on the background's bonus category, switching away while it is boring.</summary>
    public static IChoicePolicy Balanced { get; } = new RulePolicy("Balanced", CategoryMode.BonusUnlessBored, restBelowEnergy: 30);

    /// <summary>Trains the bonus category every week and rests only when forced.</summary>
    public static IChoicePolicy Grinder { get; } = new RulePolicy("Grinder", CategoryMode.Bonus, restBelowEnergy: 0, grind: true);

    /// <summary>Balanced rules, but rests whenever energy is below 60.</summary>
    public static IChoicePolicy RestHeavy { get; } = new RulePolicy("RestHeavy", CategoryMode.BonusUnlessBored, restBelowEnergy: 60);

    /// <summary>Balanced rules on one category for the whole career.</summary>
    public static IChoicePolicy Specialist { get; } = new RulePolicy("Specialist", CategoryMode.Bonus, restBelowEnergy: 30);

    /// <summary>Balanced rules with the primary category rotating by week.</summary>
    public static IChoicePolicy Rotator { get; } = new RulePolicy("Rotator", CategoryMode.Rotate, restBelowEnergy: 30);

    public static IReadOnlyList<IChoicePolicy> All { get; } = [Balanced, Grinder, RestHeavy, Specialist, Rotator];

    public static IChoicePolicy ByName(string name)
        => All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown policy '{name}'.", nameof(name));

    private enum CategoryMode
    {
        Bonus,
        BonusUnlessBored,
        Rotate,
    }

    private sealed class RulePolicy(string name, CategoryMode categoryMode, int restBelowEnergy, bool grind = false) : IChoicePolicy
    {
        private static readonly Fixed BurnoutLimit = Fixed.FromInt(70);
        private static readonly Fixed BoredomLimit = Fixed.FromInt(60);
        private static readonly Fixed DramaLimit = Fixed.FromInt(40);
        private static readonly Fixed GambleThreshold = Fixed.FromRatio(1, 2);
        private const int UpgradeReserveMultiple = 3;
        private const int TrainEveryWeeks = 4;

        public string Name => name;

        public WeekPlan ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            var category = ChooseCategory(s, c);
            var rest = new WeekPlan(MainAction.Rest, category);
            var preferred = Prefer(s, c, category);
            return legal.Any(l => l.Plan == preferred && l.DisabledReason is null) ? preferred : rest;
        }

        /// <summary>Highest-probability gamble when it is at least even odds, otherwise the guaranteed response.</summary>
        public string ChooseEventChoice(CareerState s, EventPresentation e)
        {
            var bestGamble = e.Choices.Where(o => !o.Guaranteed).OrderByDescending(o => o.Probability).FirstOrDefault();
            if (bestGamble is not null && bestGamble.Probability >= GambleThreshold)
            {
                return bestGamble.ChoiceId;
            }

            return (e.Choices.FirstOrDefault(o => o.Guaranteed) ?? bestGamble ?? e.Choices[0]).ChoiceId;
        }

        public bool ShouldRetire(CareerState s) => false;

        private WeekPlan Prefer(CareerState s, ContentSnapshot c, Category category)
        {
            var forcedRest = s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred;
            if (grind)
            {
                return forcedRest ? new WeekPlan(MainAction.Rest, category) : new WeekPlan(MainAction.TrainCategory, category);
            }

            if (forcedRest || s.Energy < Fixed.FromInt(restBelowEnergy) || s.Burnout > BurnoutLimit)
            {
                return new WeekPlan(MainAction.Rest, category);
            }

            var upgradable = Enum.GetValues<EquipmentSlot>().Where(slot => s.Equipment[slot] < CareerInvariants.MaxEquipmentTier).ToArray();
            if (upgradable.Length > 0)
            {
                var cheapest = upgradable.MinBy(slot => s.Equipment[slot]);
                var price = c.Balance.EquipmentPricePln[s.Equipment[cheapest] + 1];
                if (s.CashPln > UpgradeReserveMultiple * price)
                {
                    return new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: cheapest);
                }
            }

            if (s.Week.Value % TrainEveryWeeks == 0)
            {
                return new WeekPlan(MainAction.TrainCategory, category);
            }

            return new WeekPlan(s.Drama > DramaLimit ? MainAction.ManageCommunity : MainAction.NoAction, category);
        }

        private Category ChooseCategory(CareerState s, ContentSnapshot c)
        {
            if (categoryMode == CategoryMode.Rotate)
            {
                return (Category)(s.Week.Value % CategoryValues.Count);
            }

            var startingSkill = c.Profiles[s.Background].Skill;
            var bonus = Enum.GetValues<Category>().MaxBy(category => startingSkill[category]);
            if (categoryMode == CategoryMode.BonusUnlessBored && s.Boredom[bonus] >= BoredomLimit)
            {
                return Enum.GetValues<Category>().MinBy(category => s.Boredom[category]);
            }

            return bonus;
        }
    }
}
