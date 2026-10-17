using System.Globalization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>
/// The simulation policies used by tests and the batch simulator: <see cref="All"/> for M1 content and
/// <see cref="M3All"/> for M3 content. None of them retires early.
/// </summary>
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

    /// <summary>Balanced rules plus <see cref="MainAction.NetworkCollaborate"/> every third week with enough energy (M3).</summary>
    public static IChoicePolicy Networker { get; } = new RulePolicy("Networker", CategoryMode.BonusUnlessBored, restBelowEnergy: 30, network: true);

    /// <summary>The M1 policies.</summary>
    public static IReadOnlyList<IChoicePolicy> All { get; } = [Balanced, Grinder, RestHeavy, Specialist, Rotator];

    private static readonly IChoicePolicy[] Specialists =
        [.. Enum.GetValues<Category>().Select(category => new RulePolicy($"Specialist-{category}", CategoryMode.Fixed, restBelowEnergy: 30, fixedCategory: category))];

    /// <summary>The M3 policies: the M1 ones without the bonus specialist, the networker and one specialist per category.</summary>
    public static IReadOnlyList<IChoicePolicy> M3All { get; } =
        [Balanced, Grinder, RestHeavy, Rotator, Networker, .. Enum.GetValues<Category>().Select(SpecialistOf)];

    /// <summary>Balanced rules on <paramref name="category"/> for the whole career, however bored (name <c>Specialist-{Category}</c>).</summary>
    public static IChoicePolicy SpecialistOf(Category category) => Specialists[(int)category];

    public static IChoicePolicy ByName(string name)
        => All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown policy '{name}'.", nameof(name));

    private enum CategoryMode
    {
        Bonus,
        BonusUnlessBored,
        Rotate,
        Fixed,
    }

    private sealed class RulePolicy(
        string name,
        CategoryMode categoryMode,
        int restBelowEnergy,
        bool grind = false,
        bool network = false,
        Category fixedCategory = default) : IChoicePolicy
    {
        private static readonly Fixed BurnoutLimit = Fixed.FromInt(70);
        private static readonly Fixed BoredomLimit = Fixed.FromInt(60);
        private static readonly Fixed DramaLimit = Fixed.FromInt(40);
        private static readonly Fixed GambleThreshold = Fixed.FromRatio(1, 2);
        private const int UpgradeReserveMultiple = 3;
        private const int TrainEveryWeeks = 4;
        private const int NetworkEveryWeeks = 3;
        private const int NetworkFromEnergy = 40;
        private const string RestOnly = "rest-only";

        public string Name => name;

        public PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            var (category, categoryRule) = ChooseCategory(s, c);
            var (preferred, code, args) = Prefer(s, c, category);
            (string Name, string Value)[] common = [("category", PolicyReason.Name(category)), ("categoryRule", categoryRule)];
            var restOnly = legal.Any(l => l.DisabledReason == RestOnly);
            return !restOnly && legal.Any(l => l.Plan == preferred && l.DisabledReason is null)
                ? new PlanDecision(preferred, PolicyReason.Of(code, [.. args, .. common]))
                : new PlanDecision(
                    new WeekPlan(MainAction.Rest, category),
                    PolicyReason.Of("fallback-rest", [("preferred", PolicyReason.Name(preferred.Action)), .. common]));
        }

        /// <summary>Highest-probability gamble when it is at least even odds, otherwise the guaranteed response.</summary>
        public ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e)
        {
            var bestGamble = e.Choices.Where(o => !o.Guaranteed).OrderByDescending(o => o.Probability).FirstOrDefault();
            if (bestGamble is not null && bestGamble.Probability >= GambleThreshold)
            {
                return new ChoiceDecision(
                    bestGamble.ChoiceId,
                    PolicyReason.Of("gamble-best-odds", ("probability", bestGamble.Probability.ToString()), ("threshold", GambleThreshold.ToString())));
            }

            var guaranteed = e.Choices.FirstOrDefault(o => o.Guaranteed);
            if (guaranteed is not null)
            {
                return new ChoiceDecision(
                    guaranteed.ChoiceId,
                    bestGamble is null
                        ? PolicyReason.Of("take-guaranteed")
                        : PolicyReason.Of("take-guaranteed", ("bestProbability", bestGamble.Probability.ToString())));
            }

            var only = bestGamble ?? e.Choices[0];
            return new ChoiceDecision(only.ChoiceId, PolicyReason.Of("only-gambles", ("probability", only.Probability.ToString())));
        }

        public bool ShouldRetire(CareerState s) => false;

        private (WeekPlan Plan, string Code, (string Name, string Value)[] Args) Prefer(CareerState s, ContentSnapshot c, Category category)
        {
            var rest = new WeekPlan(MainAction.Rest, category);
            var forcedRest = s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred;
            (string Name, string Value)[] tiredness = [("energy", s.Energy.ToString()), ("burnout", s.Burnout.ToString())];
            if (grind)
            {
                return forcedRest
                    ? (rest, "forced-rest", tiredness)
                    : (new WeekPlan(MainAction.TrainCategory, category), "train-every-week", []);
            }

            if (forcedRest)
            {
                return (rest, "forced-rest", tiredness);
            }

            if (s.Energy < Fixed.FromInt(restBelowEnergy))
            {
                return (rest, "rest-low-energy", [("energy", s.Energy.ToString()), ("threshold", restBelowEnergy.ToString(CultureInfo.InvariantCulture))]);
            }

            if (s.Burnout > BurnoutLimit)
            {
                return (rest, "rest-high-burnout", [("burnout", s.Burnout.ToString()), ("limit", BurnoutLimit.ToString())]);
            }

            if (network && c.Balance.NetworkCollaborate is not null && s.Week.Value % NetworkEveryWeeks == 0 && s.Energy >= Fixed.FromInt(NetworkFromEnergy))
            {
                return (new WeekPlan(MainAction.NetworkCollaborate, category), "network-scheduled", [("everyWeeks", NetworkEveryWeeks.ToString(CultureInfo.InvariantCulture))]);
            }

            var upgradable = Enum.GetValues<EquipmentSlot>().Where(slot => s.Equipment[slot] < CareerInvariants.MaxEquipmentTier).ToArray();
            if (upgradable.Length > 0)
            {
                var cheapest = upgradable.MinBy(slot => s.Equipment[slot]);
                var price = c.Balance.EquipmentPricePln[s.Equipment[cheapest] + 1];
                if (s.CashPln > UpgradeReserveMultiple * price)
                {
                    return (
                        new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: cheapest),
                        "upgrade-equipment",
                        [("slot", PolicyReason.Name(cheapest)), ("pricePln", price.ToString(CultureInfo.InvariantCulture)), ("cashPln", s.CashPln.ToString(CultureInfo.InvariantCulture))]);
                }
            }

            if (s.Week.Value % TrainEveryWeeks == 0)
            {
                return (new WeekPlan(MainAction.TrainCategory, category), "train-scheduled", [("everyWeeks", TrainEveryWeeks.ToString(CultureInfo.InvariantCulture))]);
            }

            return s.Drama > DramaLimit
                ? (new WeekPlan(MainAction.ManageCommunity, category), "manage-community-drama", [("drama", s.Drama.ToString()), ("limit", DramaLimit.ToString())])
                : (new WeekPlan(MainAction.NoAction, category), "no-action-default", []);
        }

        private (Category Category, string Rule) ChooseCategory(CareerState s, ContentSnapshot c)
        {
            if (categoryMode == CategoryMode.Rotate)
            {
                return ((Category)(s.Week.Value % CategoryValues.Count), "rotation");
            }

            if (categoryMode == CategoryMode.Fixed)
            {
                return (fixedCategory, "fixed");
            }

            var startingSkill = c.Profiles[s.Background].Skill;
            var bonus = Enum.GetValues<Category>().MaxBy(category => startingSkill[category]);
            if (categoryMode == CategoryMode.BonusUnlessBored && s.Boredom[bonus] >= BoredomLimit)
            {
                return (Enum.GetValues<Category>().MinBy(category => s.Boredom[category]), "least-bored");
            }

            return (bonus, "bonus");
        }
    }
}
