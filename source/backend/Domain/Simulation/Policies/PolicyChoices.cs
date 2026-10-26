using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>Choices the rule policies and the strategy policy make the same way.</summary>
internal static class PolicyChoices
{
    public static bool Enabled(IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal, WeekPlan plan)
        => legal.Any(l => l.Plan == plan && l.DisabledReason is null);

    /// <summary>The engine's reason for refusing <paramref name="plan"/>, or <c>unknown</c> when it is not offered at all.</summary>
    public static string DisabledReason(IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal, WeekPlan plan)
        => legal.FirstOrDefault(l => l.Plan == plan).DisabledReason ?? "unknown";

    /// <summary>A pitch that fulfils the first installment due this week, in the contract's category.</summary>
    public static (WeekPlan Plan, string TemplateId)? DueInstallment(CareerState s, ContentSnapshot c, Category category)
    {
        if (SponsorRules.DueThisWeek(s) is not [var due, ..])
        {
            return null;
        }

        var t = SponsorRules.Template(c, due.TemplateId);
        return (new WeekPlan(MainAction.PitchFulfillSponsor, t.RequiredCategory ?? category), t.Id);
    }

    /// <summary>The required category of a one-off deal due this week (a one-off is paid by streaming its category).</summary>
    public static Category? DueOneOffCategory(CareerState s, ContentSnapshot c)
        => (s.Contracts ?? [])
            .Where(d => d.Kind == SponsorKind.OneOff && d.NextDueWeek == s.Week.Value)
            .Select(d => SponsorRules.Template(c, d.TemplateId).RequiredCategory)
            .FirstOrDefault(required => required is not null);

    /// <summary>True while a deal that forbids gambling runs.</summary>
    public static bool GamblingForbidden(CareerState s, ContentSnapshot c)
        => (s.Contracts ?? []).Any(d => SponsorRules.Template(c, d.TemplateId).IncompatibleWith.Contains(SponsorTemplate.Gambling));

    /// <summary>A deal that requires or advertises gambling.</summary>
    public static bool IsGamblingDeal(SponsorTemplate t)
        => t.RequiredCategory == Category.Gambling || t.Tags.Contains(SponsorTemplate.Gambling);

    /// <summary>
    /// The platform-ban decision: the move that keeps the most followers, then the larger reach. Otherwise the most
    /// likely gamble at or above <paramref name="gambleThreshold"/>, else a guaranteed choice, else the best gamble.
    /// </summary>
    public static ChoiceDecision Event(EventPresentation e, Fixed gambleThreshold)
    {
        var available = e.Choices.Where(o => o.UnavailableReason is null).ToArray();
        if (e.EventId == PlatformRules.BanDecisionId)
        {
            var move = available
                .OrderByDescending(o => o.Migration!.RetentionMax)
                .ThenByDescending(o => o.Migration!.Target.Reach)
                .First();
            return new ChoiceDecision(
                move.ChoiceId,
                PolicyReason.Of("ban-move", ("platform", PolicyReason.Name(move.Migration!.To)), ("retention", move.Migration.RetentionMax.ToString())));
        }

        var bestGamble = available.Where(o => !o.Guaranteed).OrderByDescending(o => o.Probability).FirstOrDefault();
        if (bestGamble is not null && bestGamble.Probability >= gambleThreshold)
        {
            return new ChoiceDecision(
                bestGamble.ChoiceId,
                PolicyReason.Of("gamble-best-odds", ("probability", bestGamble.Probability.ToString()), ("threshold", gambleThreshold.ToString())));
        }

        var guaranteed = available.FirstOrDefault(o => o.Guaranteed);
        if (guaranteed is not null)
        {
            return new ChoiceDecision(
                guaranteed.ChoiceId,
                bestGamble is null
                    ? PolicyReason.Of("take-guaranteed")
                    : PolicyReason.Of("take-guaranteed", ("bestProbability", bestGamble.Probability.ToString())));
        }

        var only = bestGamble ?? available[0];
        return new ChoiceDecision(only.ChoiceId, PolicyReason.Of("only-gambles", ("probability", only.Probability.ToString())));
    }
}
