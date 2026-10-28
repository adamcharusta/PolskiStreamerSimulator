using System.Globalization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>
/// M3-C: plays automatic weeks by the player's <see cref="Strategy"/> (spec 2026-10-26 §5). Plan order: forced rest, a
/// due sponsor installment, the rest thresholds, community management above the drama limit, an upgrade that keeps the
/// reserve, a side-business week, then the routine; a wager is attached to a Gambling plan. A disabled choice falls back
/// to a normal stream, then to rest (<c>fallback-safe</c>).
/// </summary>
public sealed class StrategyPolicy(string name, Strategy strategy) : IChoicePolicy
{
    private static readonly Fixed BoredomLimit = Fixed.FromInt(60);
    private const int SideBusinessEveryWeeks = 4;
    private const int SideBusinessWeekOffset = 2;
    private const string Declined = "offer-declined";

    public string Name => name;

    public Strategy Strategy => strategy;

    public PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
    {
        var (category, rule) = ChooseCategory(s, c);
        category = PolicyChoices.DueOneOffCategory(s, c) ?? category;
        var (plan, code, args) = Wager(s, c, Prefer(s, c, category, legal), legal);
        (string Name, string Value)[] common = [("category", PolicyReason.Name(plan.Category)), ("categoryRule", rule)];
        if (PolicyChoices.Enabled(legal, plan))
        {
            return new PlanDecision(plan, PolicyReason.Of(code, [.. args, .. common]));
        }

        var fallback = new[] { new WeekPlan(MainAction.NoAction, plan.Category), new WeekPlan(MainAction.Rest, plan.Category) }
            .FirstOrDefault(p => PolicyChoices.Enabled(legal, p))
            ?? legal.FirstOrDefault(l => l.DisabledReason is null).Plan
            ?? new WeekPlan(MainAction.Rest, plan.Category);
        return new PlanDecision(
            fallback,
            PolicyReason.Of(
                "fallback-safe",
                [("preferred", PolicyReason.Name(plan.Action)), ("disabledReason", PolicyChoices.DisabledReason(legal, plan)), .. common]));
    }

    public ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e) => PolicyChoices.Event(e, GambleThreshold(strategy.Risk));

    /// <summary>
    /// Accepts what the engine would accept when the strategy accepts offers and the deal does not clash with it: a
    /// required category other than the focus, a gambling deal while the strategy never streams Gambling, a deal that
    /// forbids gambling while it focuses Gambling or wagers, a deal that forbids the side business while it runs one.
    /// </summary>
    public OfferDecision ChooseOffer(CareerState s, ContentSnapshot c, SponsorOffer offer, string? blockedReason)
    {
        var t = SponsorRules.Template(c, offer.TemplateId);
        var reason = blockedReason ?? (strategy.AcceptOffers ? Clash(t) : "strategy");
        return reason is null
            ? new OfferDecision(true, PolicyReason.Of("offer-accepted", ("templateId", t.Id)))
            : new OfferDecision(false, PolicyReason.Of(Declined, ("templateId", t.Id), ("reason", reason)));
    }

    public bool ShouldRetire(CareerState s) => false;

    /// <summary>Gamble threshold of a risk appetite: 65 %, 50 % or 35 %.</summary>
    public static Fixed GambleThreshold(RiskAppetite risk) => risk switch
    {
        RiskAppetite.Safe => Fixed.FromRatio(65, 100),
        RiskAppetite.Bold => Fixed.FromRatio(35, 100),
        _ => Fixed.FromRatio(1, 2),
    };

    /// <summary>The drama above which the strategy manages the community: 25, 40 or 60.</summary>
    public static Fixed DramaLimit(RiskAppetite risk) => risk switch
    {
        RiskAppetite.Safe => Fixed.FromInt(25),
        RiskAppetite.Bold => Fixed.FromInt(60),
        _ => Fixed.FromInt(40),
    };

    private bool StreamsGambling => strategy.Focus == Category.Gambling || strategy.CategoryPlan == CategoryPlan.Rotate;

    private string? Clash(SponsorTemplate t)
    {
        if (t.RequiredCategory is { } required && strategy.Focus is { } focus && required != focus)
        {
            return "category";
        }

        if (PolicyChoices.IsGamblingDeal(t) && !StreamsGambling)
        {
            return SponsorTemplate.Gambling;
        }

        if (t.IncompatibleWith.Contains(SponsorTemplate.Gambling) && (StreamsGambling || strategy.Wager is not null))
        {
            return SponsorTemplate.Gambling;
        }

        return t.IncompatibleWith.Contains(SponsorTemplate.AdultBusiness) && strategy.SideBusiness ? SponsorTemplate.AdultBusiness : null;
    }

    private (WeekPlan Plan, string Code, (string Name, string Value)[] Args) Prefer(
        CareerState s, ContentSnapshot c, Category category, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
    {
        var rest = new WeekPlan(MainAction.Rest, category);
        if (s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred)
        {
            return (rest, "forced-rest", [("energy", s.Energy.ToString()), ("burnout", s.Burnout.ToString())]);
        }

        if (PolicyChoices.DueInstallment(s, c, category) is var (due, templateId))
        {
            return (due, "sponsor-due", [("templateId", templateId)]);
        }

        if (s.Energy < Fixed.FromInt(strategy.RestBelowEnergy))
        {
            return (rest, "rest-low-energy", [("energy", s.Energy.ToString()), ("threshold", Text(strategy.RestBelowEnergy))]);
        }

        if (s.Burnout > Fixed.FromInt(strategy.RestAboveBurnout))
        {
            return (rest, "rest-high-burnout", [("burnout", s.Burnout.ToString()), ("limit", Text(strategy.RestAboveBurnout))]);
        }

        var dramaLimit = DramaLimit(strategy.Risk);
        if (s.Drama > dramaLimit)
        {
            return (new WeekPlan(MainAction.ManageCommunity, category), "manage-community-drama", [("drama", s.Drama.ToString()), ("limit", dramaLimit.ToString())]);
        }

        if (Upgrade(s, c, category) is { } upgrade)
        {
            return upgrade;
        }

        var side = new WeekPlan(MainAction.SideBusinessWork, category);
        if (strategy.SideBusiness && s.Week.Value % SideBusinessEveryWeeks == SideBusinessWeekOffset && PolicyChoices.Enabled(legal, side))
        {
            return (side, "strategy-side-business", []);
        }

        var step = (s.Week.Value - 1) % strategy.Routine.Length;
        var action = strategy.Routine[step];
        return (new WeekPlan(action, category), "routine-step", [("step", Text(step + 1)), ("of", Text(strategy.Routine.Length)), ("action", PolicyReason.Name(action))]);
    }

    /// <summary>The lowest-tier slot that is not maxed, when its price leaves the reserve and at least the price again.</summary>
    private (WeekPlan Plan, string Code, (string Name, string Value)[] Args)? Upgrade(CareerState s, ContentSnapshot c, Category category)
    {
        var upgradable = Enum.GetValues<EquipmentSlot>().Where(slot => s.Equipment[slot] < CareerInvariants.MaxEquipmentTier).ToArray();
        if (!strategy.Upgrades || upgradable.Length == 0)
        {
            return null;
        }

        var cheapest = upgradable.MinBy(slot => s.Equipment[slot]);
        var price = c.Balance.EquipmentPricePln[s.Equipment[cheapest] + 1];
        return s.CashPln - price >= Math.Max(strategy.ReservePln, 2 * price)
            ? (new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: cheapest),
                "strategy-upgrade",
                [("slot", PolicyReason.Name(cheapest)), ("pricePln", Text(price)), ("reservePln", Text(strategy.ReservePln))])
            : null;
    }

    /// <summary>Stakes the strategy's wager on a Gambling plan that is not a rest, when it is legal and leaves the reserve.</summary>
    private (WeekPlan Plan, string Code, (string Name, string Value)[] Args) Wager(
        CareerState s,
        ContentSnapshot c,
        (WeekPlan Plan, string Code, (string Name, string Value)[] Args) preferred,
        IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
    {
        if (strategy.Wager is not { } tier
            || c.Balance.Wagers is not { } wagers
            || preferred.Plan is not { Category: Category.Gambling, Action: not MainAction.Rest } plan)
        {
            return preferred;
        }

        var staked = plan with { Wager = tier };
        return s.CashPln - wagers.StakePln[tier] >= strategy.ReservePln && PolicyChoices.Enabled(legal, staked)
            ? (staked, "strategy-wager", [("tier", PolicyReason.Name(tier)), ("routine", preferred.Code)])
            : preferred;
    }

    private (Category Category, string Rule) ChooseCategory(CareerState s, ContentSnapshot c)
    {
        var (category, rule) = RuleCategory(s, c);
        return category == Category.Gambling && strategy.CategoryPlan != CategoryPlan.Focus && PolicyChoices.GamblingForbidden(s, c)
            ? (Enum.GetValues<Category>().Where(x => x != Category.Gambling).MinBy(x => s.Boredom[x]), "sponsor-safe")
            : (category, rule);
    }

    private (Category Category, string Rule) RuleCategory(CareerState s, ContentSnapshot c)
    {
        switch (strategy.CategoryPlan)
        {
            case CategoryPlan.Rotate:
                return ((Category)(s.Week.Value % CategoryValues.Count), "rotation");
            case CategoryPlan.Focus:
                return (strategy.Focus!.Value, "fixed");
            default:
                var startingSkill = c.Profiles[s.Background].Skill;
                var bonus = Enum.GetValues<Category>().MaxBy(category => startingSkill[category]);
                return s.Boredom[bonus] >= BoredomLimit
                    ? (Enum.GetValues<Category>().MinBy(category => s.Boredom[category]), "least-bored")
                    : (bonus, "bonus");
        }
    }

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
}
