using System.Globalization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>
/// The simulation policies used by tests and the batch simulator: <see cref="All"/> for M1 content,
/// <see cref="M3All"/> for <c>m3-systems.1</c> content and <see cref="M3BAll"/> for <c>m3-systems.2</c>. None of them retires early.
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

    /// <summary>Gambling specialist that stakes the <see cref="WagerTier.Medium"/> wager whenever it is enabled (M3-B).</summary>
    public static IChoicePolicy Gambler { get; } = new RulePolicy("Gambler", CategoryRule.FixedOn(Category.Gambling), restBelowEnergy: 30, PlanStyle.Gamble);

    /// <summary>Balanced rules plus work on the side business every fourth free week when it is enabled (M3-B).</summary>
    public static IChoicePolicy SideHustler { get; } = new RulePolicy("SideHustler", CategoryRule.BonusUnlessBored, restBelowEnergy: 30, PlanStyle.SideHustle);

    /// <summary>Balanced rules plus a sponsor pitch every third free week when it is enabled (M3-B).</summary>
    public static IChoicePolicy SponsorHunter { get; } = new RulePolicy("SponsorHunter", CategoryRule.BonusUnlessBored, restBelowEnergy: 30, PlanStyle.SponsorHunt);

    /// <summary>Balanced rules plus a move to PulsLive: announced in the first enabled week, executed the week after (M3-B).</summary>
    public static IChoicePolicy Migrator { get; } = new RulePolicy("Migrator", CategoryRule.BonusUnlessBored, restBelowEnergy: 30, PlanStyle.Migrate);

    /// <summary>The M3-C simulator strategies (spec 2026-10-26 §9): the defaults, a cautious and a bold player.</summary>
    public static IReadOnlyList<IChoicePolicy> Strategies { get; } =
    [
        new StrategyPolicy("Strategy-Default", Strategy.Default),
        new StrategyPolicy("Strategy-Safe", Strategy.Default with
        {
            Risk = RiskAppetite.Safe,
            RestBelowEnergy = 50,
            ReservePln = 2_000,
            Routine = [MainAction.NoAction, MainAction.ManageCommunity, MainAction.TrainCategory],
        }),
        new StrategyPolicy("Strategy-Bold", Strategy.Default with
        {
            Risk = RiskAppetite.Bold,
            CategoryPlan = CategoryPlan.Focus,
            Focus = Category.Gambling,
            Wager = WagerTier.Low,
            Routine = [MainAction.NoAction, MainAction.NetworkCollaborate],
        }),
    ];

    /// <summary>The M1 policies.</summary>
    public static IReadOnlyList<IChoicePolicy> All { get; } = [Balanced, Grinder, RestHeavy, Specialist, Rotator];

    private static readonly IChoicePolicy[] Specialists =
        [.. Enum.GetValues<Category>().Select(category => new RulePolicy($"Specialist-{category}", CategoryRule.FixedOn(category), restBelowEnergy: 30))];

    /// <summary>
    /// The M3 policies: the M1 ones without the bonus specialist, the networker and one specialist per category.
    /// Frozen: <c>M3GoldenTests</c> pins their <c>m3-systems.1</c> results.
    /// </summary>
    public static IReadOnlyList<IChoicePolicy> M3All { get; } =
        [Balanced, Grinder, RestHeavy, Rotator, Networker, .. Enum.GetValues<Category>().Select(SpecialistOf)];

    /// <summary>The <c>m3-systems.2</c> policies: <see cref="M3All"/> plus the gambler, the side hustler, the sponsor hunter and the migrator.</summary>
    public static IReadOnlyList<IChoicePolicy> M3BAll { get; } = [.. M3All, Gambler, SideHustler, SponsorHunter, Migrator];

    /// <summary>The <c>m3-systems.2</c> list with achievements and strategies: <see cref="M3BAll"/> plus <see cref="Strategies"/>.</summary>
    public static IReadOnlyList<IChoicePolicy> M3CAll { get; } = [.. M3BAll, .. Strategies];

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

    /// <summary>
    /// How a policy fills a week it does not have to rest: the balanced rules, training every week, or the balanced
    /// rules plus networking, a wager, side-business work, sponsor pitches or a platform move.
    /// </summary>
    private enum PlanStyle
    {
        Balanced,
        Grind,
        Network,
        Gamble,
        SideHustle,
        SponsorHunt,
        Migrate,
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
        private const int SideBusinessEveryWeeks = 4;
        private const int SideBusinessWeekOffset = 2;
        private const int PitchEveryWeeks = 3;
        private const int PitchWeekOffset = 1;
        private const Platform MigrationTarget = Platform.PulsLive;
        private const string RestOnly = "rest-only";
        private const string Declined = "offer-declined";
        private const string EveryWeeksArg = "everyWeeks";

        public string Name => name;

        public PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            var (category, categoryRuleName) = ChooseCategory(s, c);
            var (preferred, code, args) = Wager(Prefer(s, c, category, legal), legal);
            (string Name, string Value)[] common = [("category", PolicyReason.Name(preferred.Category)), ("categoryRule", categoryRuleName)];
            var restOnly = legal.Any(l => l.DisabledReason == RestOnly);
            return !restOnly && PolicyChoices.Enabled(legal, preferred)
                ? new PlanDecision(preferred, PolicyReason.Of(code, [.. args, .. common]))
                : new PlanDecision(
                    new WeekPlan(MainAction.Rest, preferred.Category),
                    PolicyReason.Of("fallback-rest", [("preferred", PolicyReason.Name(preferred.Action)), .. common]));
        }

        /// <summary>
        /// The platform-ban decision: the move that keeps the most followers, then the target with the larger reach.
        /// Otherwise, among the choices the career can take: the highest-probability gamble when it is at least even
        /// odds, otherwise the guaranteed response.
        /// </summary>
        public ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e) => PolicyChoices.Event(e, GambleThreshold);

        /// <summary>
        /// Accepts an offer the engine would accept unless the deal clashes with how the policy plays: gambling deals
        /// for a policy that does not stream Gambling, deals forbidding gambling for a Gambling specialist, deals
        /// forbidding the side business for the side hustler, and a required category a fixed-category policy never streams.
        /// </summary>
        public OfferDecision ChooseOffer(CareerState s, ContentSnapshot c, SponsorOffer offer, string? blockedReason)
        {
            var t = SponsorRules.Template(c, offer.TemplateId);
            var clash = blockedReason ?? Clash(t);
            return clash is null
                ? new OfferDecision(true, PolicyReason.Of("offer-accepted", ("templateId", t.Id)))
                : new OfferDecision(false, PolicyReason.Of(Declined, ("templateId", t.Id), ("reason", clash)));
        }

        public bool ShouldRetire(CareerState s) => false;

        private string? Clash(SponsorTemplate t)
        {
            var gamblingSpecialist = categoryRule.FixedCategory == Category.Gambling;
            if (!gamblingSpecialist && PolicyChoices.IsGamblingDeal(t))
            {
                return SponsorTemplate.Gambling;
            }

            if (gamblingSpecialist && t.IncompatibleWith.Contains(SponsorTemplate.Gambling))
            {
                return SponsorTemplate.Gambling;
            }

            if (style == PlanStyle.SideHustle && t.IncompatibleWith.Contains(SponsorTemplate.AdultBusiness))
            {
                return SponsorTemplate.AdultBusiness;
            }

            return t.RequiredCategory is { } required && categoryRule.FixedCategory is { } fixedCategory && required != fixedCategory ? "category" : null;
        }

        /// <summary>
        /// Forced rest first; then a due sponsor installment (a due one-off sets the category); then a grinder trains,
        /// while the other styles try rest, networking, upgrades, their own action and the routine in turn.
        /// </summary>
        private Preference Prefer(CareerState s, ContentSnapshot c, Category category, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            if (s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred)
            {
                return new(new WeekPlan(MainAction.Rest, category), "forced-rest", [("energy", s.Energy.ToString()), ("burnout", s.Burnout.ToString())]);
            }

            if (PreferDue(s, c, category) is { } due)
            {
                return due;
            }

            category = PolicyChoices.DueOneOffCategory(s, c) ?? category;
            return style == PlanStyle.Grind
                ? new(new WeekPlan(MainAction.TrainCategory, category), "train-every-week", [])
                : PreferRest(s, category)
                    ?? PreferNetwork(s, c, category)
                    ?? PreferUpgrade(s, c, category)
                    ?? PreferStyle(s, category, legal)
                    ?? PreferRoutine(s, category);
        }

        /// <summary>A pitch that fulfils the first installment due this week, in the contract's category.</summary>
        private static Preference? PreferDue(CareerState s, ContentSnapshot c, Category category)
            => PolicyChoices.DueInstallment(s, c, category) is var (plan, templateId)
                ? new(plan, "sponsor-due", [("templateId", templateId)])
                : null;

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
                    ? new(new WeekPlan(MainAction.NetworkCollaborate, category), "network-scheduled", [(EveryWeeksArg, NetworkEveryWeeks.ToString(CultureInfo.InvariantCulture))])
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

        /// <summary>The M3-B styles' own action in a free week, when the engine enables it.</summary>
        private Preference? PreferStyle(CareerState s, Category category, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            Preference? candidate = style switch
            {
                PlanStyle.SideHustle when s.Week.Value % SideBusinessEveryWeeks == SideBusinessWeekOffset
                    => new(new WeekPlan(MainAction.SideBusinessWork, category), "side-business-scheduled", [(EveryWeeksArg, SideBusinessEveryWeeks.ToString(CultureInfo.InvariantCulture))]),
                PlanStyle.SponsorHunt when s.Week.Value % PitchEveryWeeks == PitchWeekOffset
                    => new(new WeekPlan(MainAction.PitchFulfillSponsor, category), "pitch-scheduled", [(EveryWeeksArg, PitchEveryWeeks.ToString(CultureInfo.InvariantCulture))]),
                PlanStyle.Migrate when Economy.Current(s) != MigrationTarget
                    => new(new WeekPlan(MainAction.PlanPlatformMigration, category, TargetPlatform: MigrationTarget), "migration-scheduled", [("platform", PolicyReason.Name(MigrationTarget))]),
                _ => null,
            };
            return candidate is not null && PolicyChoices.Enabled(legal, candidate.Plan) ? candidate : null;
        }

        private static Preference PreferRoutine(CareerState s, Category category)
        {
            if (s.Week.Value % TrainEveryWeeks == 0)
            {
                return new(new WeekPlan(MainAction.TrainCategory, category), "train-scheduled", [(EveryWeeksArg, TrainEveryWeeks.ToString(CultureInfo.InvariantCulture))]);
            }

            return s.Drama > DramaLimit
                ? new(new WeekPlan(MainAction.ManageCommunity, category), "manage-community-drama", [("drama", s.Drama.ToString()), ("limit", DramaLimit.ToString())])
                : new(new WeekPlan(MainAction.NoAction, category), "no-action-default", []);
        }

        /// <summary>The gambler stakes the medium wager on any non-rest Gambling plan whose wager variant is enabled.</summary>
        private Preference Wager(Preference preferred, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            var staked = preferred.Plan with { Wager = WagerTier.Medium };
            return style == PlanStyle.Gamble && preferred.Plan is { Category: Category.Gambling, Action: not MainAction.Rest } && PolicyChoices.Enabled(legal, staked)
                ? new(staked, "wager-scheduled", [("tier", PolicyReason.Name(WagerTier.Medium)), ("routine", preferred.Code)])
                : preferred;
        }

        /// <summary>
        /// The category rule's choice; a policy that is not fixed on a category never picks Gambling while a contract that
        /// forbids gambling runs and takes the least bored other category instead.
        /// </summary>
        private (Category Category, string Rule) ChooseCategory(CareerState s, ContentSnapshot c)
        {
            var (category, rule) = RuleCategory(s, c);
            return category == Category.Gambling && categoryRule.FixedCategory is null && PolicyChoices.GamblingForbidden(s, c)
                ? (Enum.GetValues<Category>().Where(x => x != Category.Gambling).MinBy(x => s.Boredom[x]), "sponsor-safe")
                : (category, rule);
        }

        private (Category Category, string Rule) RuleCategory(CareerState s, ContentSnapshot c)
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
