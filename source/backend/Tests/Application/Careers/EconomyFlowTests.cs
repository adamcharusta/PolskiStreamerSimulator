using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>M3-B through the application: sponsor offers, wagers, platform bans and the batch on <c>content/m3</c>.</summary>
public class EconomyFlowTests
{
    private static readonly string[] OfferReasons = ["offer-accepted", "offer-declined"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionInput Respond(CareerView view, bool? accept, string? offerId = null)
        => new(view.Version, ActionType.RespondOffer, null, null, null, null, null, OfferId: offerId ?? view.Offers[0].OfferId, Accept: accept);

    /// <summary>Replaces the stored state of the career, as a settlement would, and returns the new view.</summary>
    private static async Task<CareerView> StoreAsync(AppTestKit kit, CareerView view, Func<CareerState, CareerState> change)
    {
        var stored = (await kit.Store.FindAsync(view.CareerId, Ct))!;
        var next = change(stored.State) with { StateVersion = stored.State.StateVersion + 1 };
        await kit.Store.AppendAsync(view.CareerId, stored.State.StateVersion, next, new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null), kit.Time.GetUtcNow(), Ct);
        return await kit.GetAsync(view.CareerId);
    }

    private static Task<CareerView> BannedAsync(AppTestKit kit, CareerView view)
        => StoreAsync(kit, view, s => s with { Week = new CareerWeek(40), LastSettledWeek = 39, BannedPlatforms = [Platform.Czatnia], PendingDecisions = ["platform-ban"] });

    [Fact]
    public async Task Insider_career_shows_the_lead_offer_with_its_terms()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
        var offer = Assert.Single(view.Offers);
        Assert.Equal(("bank-skarbonka:1", SponsorKind.OneOff, 2), (offer.OfferId, offer.Kind, offer.ExpiresAfterWeek));
        Assert.True(offer.PaymentPln > 0);
        Assert.Equal("Bank Skarbonka", offer.Name);
        Assert.Null(offer.AcceptBlockedReason);
        Assert.Equal((Platform.Czatnia, 1), (view.Platform, view.PlatformSinceWeek));
        Assert.Empty(view.Strikes);
        Assert.Empty(view.BannedPlatforms);
        Assert.Null(view.SideBusiness);
    }

    [Fact]
    public async Task Accepting_an_offer_moves_it_to_contracts()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
        var outcome = (await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), Respond(view, accept: true))).Value;
        Assert.Empty(outcome.Career.Offers);
        var deal = Assert.Single(outcome.Career.Contracts);
        Assert.Equal(("bank-skarbonka", true), (deal.TemplateId, deal.DueThisWeek));
        Assert.Equal(NextStepKind.PlanWeek, outcome.Career.Next.Kind);
    }

    [Fact]
    public async Task Repeated_respond_offer_returns_the_stored_outcome()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
        var commandId = Guid.NewGuid();
        var first = await kit.SubmitAsync(view.CareerId, commandId, Respond(view, accept: true));
        var second = await kit.SubmitAsync(view.CareerId, commandId, Respond(view, accept: true));
        Assert.Equal(ContractJson.Serialize(first.Value), ContractJson.Serialize(second.Value));
        Assert.Equal(first.Value.Career.Version, (await kit.GetAsync(view.CareerId)).Version);
    }

    [Fact]
    public async Task Respond_offer_needs_an_offer_id_and_an_answer()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
        var noAnswer = await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), Respond(view, accept: null));
        Assert.Equal(ErrorCodes.ValidationFailed, noAnswer.Error.Code);
        var noOffer = await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.RespondOffer, null, null, null, null, null, Accept: true));
        Assert.Equal(ErrorCodes.ValidationFailed, noOffer.Error.Code);
    }

    [Fact]
    public async Task Unknown_offer_is_an_illegal_action()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
        var refused = await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), Respond(view, accept: true, offerId: "x:1"));
        Assert.Equal((ErrorCodes.IllegalAction, "unknown-offer"), (refused.Error.Code, refused.Error.Detail));
    }

    [Fact]
    public async Task Gambling_plans_show_wager_terms_and_disable_unaffordable_stakes()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;   // Dorm: 2,000 PLN
        var medium = view.Next.Plans.Single(p => p.Plan is { MainAction: MainAction.NoAction, Category: Category.Gambling, Wager: WagerTier.Medium });
        Assert.Equal((500L, 0.45m, 1_000L, 500L), (medium.Wager!.StakePln, medium.Wager.Chance, medium.Wager.PayoutPln, medium.Wager.LossPln));
        Assert.Null(medium.DisabledReason);   // 500 ≤ 25 % of 2,000
        var high = view.Next.Plans.Single(p => p.Plan is { MainAction: MainAction.NoAction, Category: Category.Gambling, Wager: WagerTier.High });
        Assert.Equal("needs-cash", high.DisabledReason);
        Assert.Equal(3, medium.Policy!.Exposure);
        Assert.Empty(medium.SponsorConsequences);
        Assert.Null(view.Next.Plans.First(p => p.Plan.MainAction == MainAction.Rest).Wager);
    }

    [Fact]
    public async Task Start_week_with_a_wager_reports_it()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync()).Career;
        var started = (await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.StartWeek, MainAction.NoAction, Category.Gambling, null, null, null, Wager: WagerTier.Low))).Value.Career;
        while (started.Next.Kind == NextStepKind.ResolveEvent)
        {
            started = (await kit.SubmitAsync(started.CareerId, Guid.NewGuid(), new ActionInput(started.Version, ActionType.ResolveEvent, null, null, null, null, started.Next.Event!.Choices.First(c => c.UnavailableReason is null).ChoiceId))).Value.Career;
        }

        var report = (await kit.SubmitAsync(started.CareerId, Guid.NewGuid(), new ActionInput(started.Version, ActionType.SettleWeek, null, null, null, null, null))).Value.Report!;
        Assert.Equal((100L, WagerTier.Low), (report.Wager!.StakePln, report.Plan.Wager));
        Assert.Contains(report.Income.Concat(report.Costs), l => l.Code is "income:wager" or "cost:wager");
        Assert.NotNull(report.Strike);
    }

    [Fact]
    public async Task Ban_decision_is_shown_with_retention_ranges()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = await BannedAsync(kit, (await kit.NewCareerAsync()).Career);
        Assert.Equal(NextStepKind.ResolveDecision, view.Next.Kind);
        var shown = view.Next.Event!;
        Assert.Equal(("platform-ban", "Ban na platformie"), (shown.EventId, shown.Title));
        Assert.Equal(["move-PulsLive", "move-Nurt"], shown.Choices.Select(c => c.ChoiceId));
        Assert.All(shown.Choices, c => Assert.True(c.Migration!.RetentionMin < c.Migration.RetentionMax));
        Assert.Equal([Platform.Czatnia], view.BannedPlatforms);
    }

    [Fact]
    public async Task Batch_stops_before_the_platform_ban_decision()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = await BannedAsync(kit, (await kit.NewCareerAsync()).Career);
        var batch = (await kit.RunBatchAsync(view.CareerId, view.Version)).Value;
        Assert.Equal((BatchStopReason.PausedForDecision, 0), (batch.StopReason, batch.WeeksCommitted));
    }

    [Fact]
    public async Task Batch_answers_offers_and_records_the_reason()
    {
        var kit = AppTestKit.WithContent(TestPaths.ContentM3);
        var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
        var batch = (await kit.RunBatchAsync(view.CareerId, view.Version)).Value;
        var answered = Assert.Single(batch.Weeks[0].Offers);
        Assert.Equal("bank-skarbonka", answered.TemplateId);
        Assert.Contains(answered.Reason.Code, OfferReasons);
    }
}
