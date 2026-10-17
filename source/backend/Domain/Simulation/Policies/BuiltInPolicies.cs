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
    public static IChoicePolicy Balanced { get; } = new RulePolicy("Balanced", CategoryRule.BonusUnlessBored, restBelowEnergy: 30);

    /// <summary>Trains the bonus category every week and rests only when forced.</summary>
    public static IChoicePolicy Grinder { get; } = new RulePolicy("Grinder", CategoryRule.Bonus, restBelowEnergy: 0, PlanStyle.Grind);

    /// <summary>Balanced rules, but rests whenever energy is below 60.</summary>
    public static IChoicePolicy RestHeavy { get; } = new RulePolicy("RestHeavy", CategoryRule.BonusUnlessBored, restBelowEnergy: 60);

    /// <summary>Balanced rules on one category for the whole career.</summary>
    public static IChoicePolicy Specialist { get; } = new RulePolicy("Specialist", CategoryRule.Bonus, restBelowEnergy: 30);

    /// <summary>Balanced rules with the primary category rotating by week.</summary>
    public static IChoicePolicy Rotator { get; } = new RulePolicy("Rotator", CategoryRule.Rotate, restBelowEnergy: 30);

    /// <summary>Balanced rules plus <see cref="MainAction.NetworkCollaborate"/> every third week with enough energy (M3).</summary>
    public static IChoicePolicy Networker { get; } = new RulePolicy("Networker", CategoryRule.BonusUnlessBored, restBelowEnergy: 30, PlanStyle.Network);

    /// <summary>The M1 policies.</summary>
    public static IReadOnlyList<IChoicePolicy> All { get; } = [Balanced, Grinder, RestHeavy, Specialist, Rotator];

    private static readonly IChoicePolicy[] Specialists =
        [.. Enum.GetValues<Category>().Select(category => new RulePolicy($"Specialist-{category}", CategoryRule.FixedOn(category), restBelowEnergy: 30))];

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

    /// <summary>How a policy fills a week it does not have to rest: the balanced rules, training every week, or the balanced rules plus networking.</summary>
    private enum PlanStyle
    {
        Balanced,
        Grind,
        Network,
    }

    /// <summary>How a policy picks its category; <see cref="FixedCategory"/> is set only for <see cref="CategoryMode.Fixed"/>.</summary>
    private sealed record CategoryRule(CategoryMode Mode, Category? FixedCategory = null)
    {
        public static CategoryRule Bonus { get; } = new(CategoryMode.Bonus);

        public static CategoryRule BonusUnlessBored { get; } = new(CategoryMode.BonusUnlessBored);

        public static CategoryRule Rotate { get; } = new(CategoryMode.Rotate);

        public static CategoryRule FixedOn(Category category) => new(CategoryMode.Fixed, category);
    }

    /// <summary>A preferred plan with its reason code and arguments.</summary>
    private sealed record Preference(WeekPlan Plan, string Code, (string Name, string Value)[] Args);

    private sealed class RulePolicy(
        string name,
        CategoryRule categoryRule,
        int restBelowEnergy,
        PlanStyle style = PlanStyle.Balanced) : IChoicePolicy
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
            var (category, categoryRuleName) = ChooseCategory(s, c);
            var (preferred, code, args) = Prefer(s, c, category);
            (string Name, string Value)[] common = [("category", PolicyReason.Name(category)), ("categoryRule", categoryRuleName)];
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

        /// <summary>Forced rest first; then a grinder trains, while the other styles try rest, networking, upgrades and the routine in turn.</summary>
        private Preference Prefer(CareerState s, ContentSnapshot c, Category category)
        {
            if (s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred)
            {
                return new(new WeekPlan(MainAction.Rest, category), "forced-rest", [("energy", s.Energy.ToString()), ("burnout", s.Burnout.ToString())]);
            }

            return style == PlanStyle.Grind
                ? new(new WeekPlan(MainAction.TrainCategory, category), "train-every-week", [])
                : PreferRest(s, category) ?? PreferNetwork(s, c, category) ?? PreferUpgrade(s, c, category) ?? PreferRoutine(s, category);
        }

        private Preference? PreferRest(CareerState s, Category category)
        {
            var rest = new WeekPlan(MainAction.Rest, category);
            if (s.Energy < Fixed.FromInt(restBelowEnergy))
            {
                return new(rest, "rest-low-energy", [("energy", s.Energy.ToString()), ("threshold", restBelowEnergy.ToString(CultureInfo.InvariantCulture))]);
            }

            return s.Burnout > BurnoutLimit
                ? new(rest, "rest-high-burnout", [("burnout", s.Burnout.ToString()), ("limit", BurnoutLimit.ToString())])
                : null;
        }

        private Preference? PreferNetwork(CareerState s, ContentSnapshot c, Category category)
            => style == PlanStyle.Network
                && c.Balance.NetworkCollaborate is not null
                && s.Week.Value % NetworkEveryWeeks == 0
                && s.Energy >= Fixed.FromInt(NetworkFromEnergy)
                    ? new(new WeekPlan(MainAction.NetworkCollaborate, category), "network-scheduled", [("everyWeeks", NetworkEveryWeeks.ToString(CultureInfo.InvariantCulture))])
                    : null;

        /// <summary>Upgrades the lowest-tier slot that is not maxed once cash exceeds <see cref="UpgradeReserveMultiple"/> times its price.</summary>
        private static Preference? PreferUpgrade(CareerState s, ContentSnapshot c, Category category)
        {
            var upgradable = Enum.GetValues<EquipmentSlot>().Where(slot => s.Equipment[slot] < CareerInvariants.MaxEquipmentTier).ToArray();
            if (upgradable.Length == 0)
            {
                return null;
            }

            var cheapest = upgradable.MinBy(slot => s.Equipment[slot]);
            var price = c.Balance.EquipmentPricePln[s.Equipment[cheapest] + 1];
            return s.CashPln > UpgradeReserveMultiple * price
                ? new(
                    new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: cheapest),
                    "upgrade-equipment",
                    [("slot", PolicyReason.Name(cheapest)), ("pricePln", price.ToString(CultureInfo.InvariantCulture)), ("cashPln", s.CashPln.ToString(CultureInfo.InvariantCulture))])
                : null;
        }

        private static Preference PreferRoutine(CareerState s, Category category)
        {
            if (s.Week.Value % TrainEveryWeeks == 0)
            {
                return new(new WeekPlan(MainAction.TrainCategory, category), "train-scheduled", [("everyWeeks", TrainEveryWeeks.ToString(CultureInfo.InvariantCulture))]);
            }

            return s.Drama > DramaLimit
                ? new(new WeekPlan(MainAction.ManageCommunity, category), "manage-community-drama", [("drama", s.Drama.ToString()), ("limit", DramaLimit.ToString())])
                : new(new WeekPlan(MainAction.NoAction, category), "no-action-default", []);
        }

        private (Category Category, string Rule) ChooseCategory(CareerState s, ContentSnapshot c)
        {
            if (categoryRule.Mode == CategoryMode.Rotate)
            {
                return ((Category)(s.Week.Value % CategoryValues.Count), "rotation");
            }

            if (categoryRule.FixedCategory is { } fixedCategory)
            {
                return (fixedCategory, "fixed");
            }

            var startingSkill = c.Profiles[s.Background].Skill;
            var bonus = Enum.GetValues<Category>().MaxBy(category => startingSkill[category]);
            if (categoryRule.Mode == CategoryMode.BonusUnlessBored && s.Boredom[bonus] >= BoredomLimit)
            {
                return (Enum.GetValues<Category>().MinBy(category => s.Boredom[category]), "least-bored");
            }

            return (bonus, "bonus");
        }
    }
}
