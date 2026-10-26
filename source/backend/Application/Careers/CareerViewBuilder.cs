using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Scoring;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// Builds the career view from the state; it queries the career's own ruleset for plans and odds, so it is written
/// out instead of being mapped.
/// </summary>
public sealed class CareerViewBuilder(WeekEngine engine)
{
    public CareerView Build(Guid careerId, string nickname, CareerState s, ContentSnapshot c, WeekReportView? lastReport)
        => new(
            careerId,
            nickname,
            s.RulesetId,
            s.StateVersion,
            s.Status,
            s.Ending,
            s.Background,
            s.Difficulty,
            s.Pronouns,
            s.Week.Value,
            s.LastSettledWeek,
            Stats(s),
            Next(s, c),
            lastReport,
            Economy.Current(s),
            Economy.SinceWeek(s),
            Strikes(s, c),
            s.BannedPlatforms is { } banned ? [.. banned] : [],
            [.. (s.Offers ?? []).Select(o => Offer(s, c, o))],
            [.. (s.Contracts ?? []).Select(d => Contract(s, c, d))],
            SideBusiness(s, c.Balance),
            AchievementViews(s, c.Balance),
            s.FinalScore is { } score ? Score(score) : null,
            EndingSummary.Of(s, c) is { } summary ? Summary(summary) : null);

    /// <summary>The content's achievements (or the M1 set), earned first, each in definition order.</summary>
    private static AchievementView[] AchievementViews(CareerState s, BalanceConstants b)
    {
        IEnumerable<AchievementView> all = b.Achievements is { } defs
            ? defs.Select(d => new AchievementView(d.Id, d.Name, d.Description, d.Points, s.Achievements.Contains(d.Id)))
            : Achievements.M1Ids.Select(id => new AchievementView(id, null, null, Achievements.Points([id]), s.Achievements.Contains(id)));
        return [.. all.OrderByDescending(a => a.Earned)];
    }

    private static ScoreView Score(ScoreBreakdown s) => new(
        Decimals.Of(s.F),
        Decimals.Of(s.W),
        Decimals.Of(s.A),
        s.FollowersPoints,
        s.WealthPoints,
        s.AchievementPoints,
        s.BaseScore,
        Decimals.Of(s.DifficultyMultiplier),
        Decimals.Of(s.BackgroundMultiplier),
        s.FinalScore);

    private static EndingSummaryView Summary(EndingSummary s) => new(
        s.Reason,
        s.WeeksPlayed,
        s.DominantCategory,
        s.Reputation,
        s.Platform,
        [.. s.BannedPlatforms],
        s.PeakFollowersMilestone,
        [.. s.Achievements.Select(a => a.Id)]);

    /// <summary>Strikes that still count after the last settled week, with the last settled week in which each counts.</summary>
    private static StrikeView[] Strikes(CareerState s, ContentSnapshot c)
    {
        var weeks = c.Balance.Policy?.StrikeWeeks ?? 0;
        return [.. (s.Strikes ?? []).Where(x => x.GivenWeek + weeks > s.LastSettledWeek).Select(x => new StrikeView(x.Platform, x.GivenWeek, x.GivenWeek + weeks - 1))];
    }

    private static OfferView Offer(CareerState s, ContentSnapshot c, SponsorOffer offer)
    {
        var t = SponsorRules.Template(c, offer.TemplateId);
        return new OfferView(
            offer.OfferId,
            t.Id,
            t.Name,
            t.Pitch,
            t.Kind,
            offer.PaymentPln,
            t.TermWeeks,
            t.DueEveryWeeks,
            t.RequiredCategory,
            [.. t.Platforms],
            new SponsorPrerequisitesView(
                t.Prerequisites.MinFollowers,
                t.Prerequisites.MinCredibility is { } minCredibility ? Decimals.Of(minCredibility) : null,
                t.Prerequisites.MaxDrama is { } maxDrama ? Decimals.Of(maxDrama) : null,
                t.Prerequisites.MinDrama is { } minDrama ? Decimals.Of(minDrama) : null),
            [.. t.IncompatibleWith],
            t.Breach.CashPln,
            Decimals.Of(t.Breach.Credibility),
            offer.ExpiresAfterWeek,
            SponsorRules.AcceptRejection(s, c, offer));
    }

    private static ContractView Contract(CareerState s, ContentSnapshot c, SponsorContract d)
    {
        var t = SponsorRules.Template(c, d.TemplateId);
        return new ContractView(
            d.ContractId,
            t.Id,
            t.Name,
            d.Kind,
            d.PaymentPln,
            t.RequiredCategory,
            [.. t.Platforms],
            d.NextDueWeek,
            d.TermEndWeek,
            d.Missed,
            t.Breach.CashPln,
            Decimals.Of(t.Breach.Credibility),
            d.NextDueWeek == s.Week.Value);
    }

    private static SideBusinessView? SideBusiness(CareerState s, BalanceConstants b)
    {
        if (s.SideBusiness is not { } business || b.SideBusiness is not { } balance)
        {
            return null;
        }

        var income = SideBusinessRules.IsOpen(s) ? SideBusinessRules.Income(s.Followers, business.Quality, balance) : 0;
        return new SideBusinessView(business.OpenedWeek, Decimals.Of(business.Quality), income);
    }

    public static CareerStatsView Stats(CareerState s) => new(
        s.CashPln,
        s.Followers,
        s.LastLiveViewers,
        Decimals.Of(s.Drama),
        Decimals.Of(s.Credibility),
        Decimals.Of(s.Energy),
        Decimals.Of(s.Burnout),
        Categories(s.Skill),
        Categories(s.Boredom),
        Equipment(s.Equipment));

    public static CategoryValuesView Categories(CategoryValues<Fixed> v) => new(
        Decimals.Of(v[Category.JustChatting]),
        Decimals.Of(v[Category.Fps]),
        Decimals.Of(v[Category.Moba]),
        Decimals.Of(v[Category.Chess]),
        Decimals.Of(v[Category.Gambling]));

    public static EquipmentView Equipment(IReadOnlyDictionary<EquipmentSlot, int> tiers) => new(
        tiers[EquipmentSlot.Microphone],
        tiers[EquipmentSlot.Camera],
        tiers[EquipmentSlot.Computer],
        tiers[EquipmentSlot.Connection]);

    private NextStepView Next(CareerState s, ContentSnapshot c)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new NextStepView(NextStepKind.Ended, [], null);
        }

        if (s.Pending is null)
        {
            return Expect(engine.RequiresPlayerDecision(s, c)) && Decision(s, c) is { } decision
                ? new NextStepView(NextStepKind.ResolveDecision, [], Event(decision, c, mandatory: true))
                : new NextStepView(NextStepKind.PlanWeek, Plans(s, c), null);
        }

        return s.Pending.Resolved.Length < s.Pending.EventIds.Length
            ? new NextStepView(NextStepKind.ResolveEvent, [], Event(Expect(engine.PresentEvent(s, c)), c, mandatory: false))
            : new NextStepView(NextStepKind.SettleWeek, [], null);
    }

    /// <summary>
    /// The queued decision, or <c>null</c> for a pause that queues no decision event (the week is then planned by hand);
    /// any other failure, such as a queued event missing from the content, is a server fault rather than a dead end.
    /// </summary>
    private EventPresentation? Decision(CareerState s, ContentSnapshot c)
    {
        var decision = engine.PresentDecision(s, c);
        return !decision.IsSuccess && decision.Error.Code == DomainErrorCode.NoPendingDecision ? null : Expect(decision);
    }

    /// <summary>Every plan with its disabled reason, the odds of a rolled action (networking, a sponsor pitch) and what it risks (one plan preview each).</summary>
    private PlanOptionView[] Plans(CareerState s, ContentSnapshot c)
        => [.. Expect(engine.LegalPlans(s, c)).Select(l => new PlanOptionView(
            ReportViews.Plan(l.Plan),
            l.DisabledReason,
            Expect(engine.PresentAction(s, c, l.Plan)) is { } odds ? Choice(odds, ActionChoice(l.Plan, c.Balance)) : null,
            Expect(engine.PresentPlan(s, c, l.Plan)) is var preview && preview.Wager is { } w ? new WagerTermsView(w.Tier, w.StakePln, Decimals.Of(w.Chance), w.PayoutPln, w.LossPln) : null,
            preview.Policy is { } p ? new PolicyExposureView(p.Exposure, Decimals.Of(p.Chance), p.Threshold) : null,
            preview.Migration is { } m ? ReportViews.Migration(m) : null,
            [.. preview.Sponsors.Select(Consequence)]))];

    private static SponsorConsequenceView Consequence(SponsorConsequence x)
        => new(x.ContractId, x.TemplateId, PolicyReason.Name(x.Kind), x.PenaltyPln, Decimals.Of(x.Credibility), x.Terminates);

    /// <summary>The choice definition behind a rolled action's odds.</summary>
    private static ChoiceDefinition ActionChoice(WeekPlan plan, BalanceConstants b)
        => plan.Action == MainAction.PitchFulfillSponsor ? b.Sponsors!.Pitch : b.NetworkCollaborate!;

    private static EventView Event(EventPresentation presented, ContentSnapshot c, bool mandatory)
    {
        var definition = presented.Definition ?? c.Events.First(e => string.Equals(e.Id, presented.EventId, StringComparison.Ordinal));
        return new EventView(
            presented.EventId,
            definition.TitleKey,
            definition.Title,
            definition.TextPl,
            [.. presented.Choices.Select(o => Choice(o, definition.Choices.First(x => string.Equals(x.Id, o.ChoiceId, StringComparison.Ordinal))))],
            mandatory);
    }

    private static ChoiceView Choice(ChoiceOdds odds, ChoiceDefinition choice)
        => new(
            odds.ChoiceId,
            choice.LabelKey,
            choice.LabelPl,
            odds.Guaranteed,
            Decimals.Of(odds.Probability),
            odds.Threshold,
            [.. odds.Modifiers.Select(ReportViews.Modifier)],
            odds.AllowsCritical,
            Decimals.Of(odds.ViralOnCritical),
            odds.UnavailableReason,
            odds.Migration is { } migration ? ReportViews.Migration(migration) : null);

    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Career view failed: {result.Error.Code} ({result.Error.Detail}).");
}
