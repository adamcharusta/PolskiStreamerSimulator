using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Rulesets.Shared;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class OfferTests
{
    private static readonly SponsorBalance B = M3BPlay.C.Balance.Sponsors!;

    [Theory]
    [InlineData(0, 8)]
    [InlineData(9_999, 8)]
    [InlineData(10_000, 10)]
    [InlineData(100_000, 12)]
    [InlineData(10_000_000, 16)]
    [InlineData(1_000_000_000, 20)]
    [InlineData(100_000_000_000, 20)]
    public void Offer_chance_grows_two_points_per_order_of_magnitude(long followers, int percent)
        => Assert.Equal(Fixed.FromRatio(percent, 100), SponsorRules.OfferChance(followers, B));

    [Theory]
    [InlineData(5_500, 400)]
    [InlineData(100_000, 1_000)]
    public void Payment_adds_per_thousand_followers_up_to_the_cap(long followers, long payment)
        => Assert.Equal(payment, SponsorRules.Payment(SponsorRules.Template(M3BPlay.C, "trusted-tea"), followers));

    [Fact]
    public void Insider_starts_with_the_lead_offer_and_others_do_not()
    {
        var insider = M3BPlay.Fresh(background: Background.IndustryInsider);
        var offer = Assert.Single(insider.Offers!.Value);
        Assert.Equal(("trusted-tea:1", "trusted-tea", 2), (offer.OfferId, offer.TemplateId, offer.ExpiresAfterWeek));
        Assert.Null(M3BPlay.Fresh().Offers);
    }

    [Fact]
    public void Accepting_a_contract_schedules_its_installments()
    {
        var s = Insider(week: 10);
        var accepted = M3BPlay.Respond(s, "trusted-tea:10", accept: true);
        var contract = Assert.Single(accepted.Contracts!.Value);
        Assert.Equal(("trusted-tea:10", 13, 21, 0), (contract.ContractId, contract.NextDueWeek, contract.TermEndWeek, contract.Missed));
        Assert.Empty(accepted.Offers!.Value);
        Assert.Equal(s.StateVersion + 1, accepted.StateVersion);
        Assert.Equal(s.Rng, accepted.Rng);
    }

    [Fact]
    public void Accepting_a_one_off_deal_makes_it_due_this_week()
    {
        var s = Insider(week: 10) with { Offers = [new SponsorOffer("hype-drink:10", "hype-drink", 10, 11, 400)], Drama = Fixed.FromInt(60) };
        var contract = Assert.Single(M3BPlay.Respond(s, "hype-drink:10", accept: true).Contracts!.Value);
        Assert.Equal((SponsorKind.OneOff, 10, 10, 400L), (contract.Kind, contract.NextDueWeek, contract.TermEndWeek, contract.PaymentPln));
    }

    [Fact]
    public void Declining_removes_the_offer()
        => Assert.Empty(M3BPlay.Respond(Insider(week: 10), "trusted-tea:10", accept: false).Offers!.Value);

    [Fact]
    public void Third_contract_is_refused()
        => Assert.Equal("contract-limit", Refusal(Insider(week: 10) with { Contracts = [Contract("hype-drink"), Contract("puls-gear")] }));

    [Fact]
    public void Second_contract_with_the_same_sponsor_is_refused()
        => Assert.Equal("same-sponsor", Refusal(Insider(week: 10) with { Contracts = [Contract("trusted-tea")] }));

    [Fact]
    public void Lapsed_prerequisite_is_refused_at_acceptance()
        => Assert.Equal("prerequisite:drama", Refusal(Insider(week: 10) with { Drama = Fixed.FromInt(41) }));

    [Fact]
    public void Offer_for_another_platform_is_refused_at_acceptance()
    {
        var s = Insider(week: 10) with { Followers = 6_000, Offers = [new SponsorOffer("puls-gear:10", "puls-gear", 10, 11, 500)] };
        Assert.Equal("prerequisite:platform", Refusal(s));
    }

    [Fact]
    public void Responding_to_an_expired_offer_is_rejected()
    {
        var s = M3BPlay.Fresh(background: Background.IndustryInsider);
        s = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction)).State;
        s = M3BPlay.Week(M3BPlay.AnswerDecisions(s), M3BPlay.Plan(MainAction.NoAction)).State;   // week 2 settled: the lead expired
        var result = M3BPlay.Engine.RespondOffer(s, M3BPlay.C, "trusted-tea:1", true, s.StateVersion);
        Assert.Equal((DomainErrorCode.IllegalAction, "unknown-offer"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Respond_offer_during_a_week_is_refused()
    {
        var started = M3BPlay.Start(M3BPlay.Fresh(background: Background.IndustryInsider), M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal("week-in-progress", M3BPlay.Engine.RespondOffer(started, M3BPlay.C, "trusted-tea:1", true, started.StateVersion).Error.Detail);
    }

    [Fact]
    public void Respond_offer_with_a_stale_version_is_refused()
    {
        var s = M3BPlay.Fresh(background: Background.IndustryInsider);
        Assert.Equal(DomainErrorCode.StaleVersion, M3BPlay.Engine.RespondOffer(s, M3BPlay.C, "trusted-tea:1", true, s.StateVersion + 1).Error.Code);
    }

    [Fact]
    public void Offer_roll_draws_nothing_without_an_eligible_template()
    {
        var s = M3BPlay.Fresh() with { Followers = 0 };
        var c = M3BPlay.C with { Sponsors = [.. M3BPlay.C.Sponsors.Where(t => t.Id != "chess-board")] };
        var rng = s.Rng;
        Assert.Equal(s, SponsorRules.AfterSettlement(s, c, 1, ref rng));
        Assert.Equal(s.Rng, rng);
    }

    [Fact]
    public void Side_business_keeps_adult_business_incompatible_templates_out()
    {
        var s = M3BPlay.Fresh() with { SideBusiness = new SideBusiness(52, Fixed.FromInt(10)) };
        Assert.DoesNotContain(SponsorRules.Eligible(s, M3BPlay.C), t => t.Id == "chess-board");
        Assert.Contains(SponsorRules.Eligible(M3BPlay.Fresh(), M3BPlay.C), t => t.Id == "chess-board");
    }

    [Fact]
    public void An_offer_opened_after_settlement_waits_two_weeks()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var (after, _) = M3BPlay.Week(M3BPlay.Fresh(seed) with { Followers = 20_000 }, M3BPlay.Plan(MainAction.NoAction));
            if (after.Offers is [var offer])
            {
                Assert.Equal((2, 3), (offer.OfferedWeek, offer.ExpiresAfterWeek));
                return;
            }
        }

        Assert.Fail("No seed in 1–200 opened an offer.");
    }

    [Fact]
    public void Sponsor_offer_effect_opens_the_named_template()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh(), M3BPlay.C, "test", [new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "hype-drink")]);
        Assert.Equal(("hype-drink", 400L), (s.Offers!.Value[0].TemplateId, s.Offers.Value[0].PaymentPln));
    }

    [Fact]
    public void Sponsor_offer_effect_skips_a_template_with_an_open_offer()
    {
        var s = Insider(week: 10);
        var after = EffectApplier.ToCarry(s, M3BPlay.C, "test", [new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "trusted-tea")]);
        Assert.Equal(s.Offers, after.Offers);
    }

    [Fact]
    public void M1_career_has_no_offers_to_answer()
        => Assert.Equal("unknown-offer", TestWorld.Engine.RespondOffer(TestCareers.Fresh(), TestWorld.Content, "x", true, 0).Error.Detail);

    private static CareerState Insider(int week)
        => M3BPlay.Fresh(background: Background.IndustryInsider) with
        {
            Week = new CareerWeek(week),
            Drama = Fixed.FromInt(15),
            Offers = [new SponsorOffer($"trusted-tea:{week}", "trusted-tea", week, week + 1, 400)],
        };

    private static SponsorContract Contract(string templateId) => new($"{templateId}:1", templateId, SponsorKind.Contract, 1, 4, 12, 300, 0);

    private static string Refusal(CareerState s) => M3BPlay.Engine.RespondOffer(s, M3BPlay.C, s.Offers!.Value[0].OfferId, true, s.StateVersion).Error.Detail;
}
