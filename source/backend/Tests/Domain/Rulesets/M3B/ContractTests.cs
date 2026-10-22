using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class ContractTests
{
    [Fact]
    public void Pitch_in_a_due_week_fulfils_and_pays_the_installment()
    {
        var (after, report) = M3BPlay.Week(Due(), M3BPlay.Plan(MainAction.PitchFulfillSponsor));
        Assert.Contains(report.Income, l => l is { Code: "income:sponsor", AmountPln: 400, Detail: "trusted-tea" });
        Assert.Equal(17, after.Contracts!.Value[0].NextDueWeek);
        Assert.Null(report.ActionRoll);
    }

    [Fact]
    public void Last_installment_ends_the_contract()
    {
        var (after, report) = M3BPlay.Week(Due(week: 21, nextDue: 21), M3BPlay.Plan(MainAction.PitchFulfillSponsor));
        Assert.Contains(report.Income, l => l.Code == "income:sponsor");
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Missed_installment_costs_the_capped_penalty_and_credibility()
    {
        var (after, report) = M3BPlay.Week(Due(), M3BPlay.Plan(MainAction.NoAction));
        Assert.Contains(report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 200, Detail: "trusted-tea" });
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Credibility" && l.Detail == "sponsor" && l.Delta == Fixed.FromInt(-3));
        Assert.Equal((1, 17), (after.Contracts!.Value[0].Missed, after.Contracts.Value[0].NextDueWeek));
    }

    [Fact]
    public void Penalty_never_exceeds_the_installment()
    {
        var s = Due() with { Contracts = [new SponsorContract("trusted-tea:10", "trusted-tea", SponsorKind.Contract, 10, 13, 21, 150, 0)] };
        Assert.Contains(M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction)).Report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 150 });
    }

    [Fact]
    public void Second_miss_terminates_with_five_more_credibility()
    {
        var (after, report) = M3BPlay.Week(Due(missed: 1), M3BPlay.Plan(MainAction.NoAction));
        Assert.Empty(after.Contracts!.Value);
        var lines = report.StatChanges.Where(l => l.Code == "stat:Credibility" && l.Detail == "sponsor").Select(l => l.Delta).ToArray();
        Assert.Equal([Fixed.FromInt(-3), Fixed.FromInt(-5)], lines);   // breach credibility, then the termination line
    }

    [Fact]
    public void Gambling_stream_breaches_a_gambling_incompatible_contract_once()
    {
        var (after, report) = M3BPlay.Week(Due(week: 15, nextDue: 17), M3BPlay.Plan(MainAction.NoAction, Category.Gambling));
        Assert.Single(report.Costs, l => l.Code == "cost:sponsor-penalty");
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Week_off_postpones_a_due_installment_without_penalty()
    {
        var (after, report) = M3BPlay.Week(CareerFlags.Set(Due(), "sys:rest-only", 1), M3BPlay.Plan(MainAction.Rest));
        Assert.DoesNotContain(report.Costs, l => l.Code == "cost:sponsor-penalty");
        var contract = after.Contracts!.Value[0];
        Assert.Equal((14, 22, 0), (contract.NextDueWeek, contract.TermEndWeek, contract.Missed));
    }

    [Fact]
    public void One_off_pays_in_the_next_stream_when_the_category_matches()
    {
        var (after, report) = M3BPlay.Week(ChessDeal(), M3BPlay.Plan(MainAction.NoAction, Category.Chess));
        Assert.Contains(report.Income, l => l is { Code: "income:sponsor", AmountPln: 250 });
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void One_off_in_the_wrong_category_is_a_breach()
    {
        var (after, report) = M3BPlay.Week(ChessDeal(), M3BPlay.Plan(MainAction.NoAction, Category.Fps));
        Assert.Contains(report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 100 });
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Plan_preview_shows_the_penalty_of_a_missed_installment()
    {
        var consequence = Assert.Single(SponsorRules.Consequences(Due(), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction)));
        Assert.Equal((SponsorConsequenceKind.MissedInstallment, 200L, Fixed.FromInt(-3), false), (consequence.Kind, consequence.PenaltyPln, consequence.Credibility, consequence.Terminates));
        Assert.Empty(SponsorRules.Consequences(Due(), M3BPlay.C, M3BPlay.Plan(MainAction.PitchFulfillSponsor)));
    }

    [Fact]
    public void Plan_preview_shows_a_terminating_breach()
    {
        var consequence = Assert.Single(SponsorRules.Consequences(Due(week: 15, nextDue: 17), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling)));
        Assert.Equal((SponsorConsequenceKind.Breach, 200L, true), (consequence.Kind, consequence.PenaltyPln, consequence.Terminates));
    }

    [Fact]
    public void Due_this_week_lists_contracts_only()
    {
        var s = Due() with { Contracts = [.. Due().Contracts!.Value, new SponsorContract("chess-board:13", "chess-board", SponsorKind.OneOff, 13, 13, 13, 250, 0)] };
        Assert.Equal(["trusted-tea:10"], SponsorRules.DueThisWeek(s).Select(d => d.ContractId));
    }

    [Fact]
    public void Pitch_without_a_due_task_rolls_with_visible_odds()
    {
        var s = Due(week: 15, nextDue: 17) with { Followers = 2_000 };
        Assert.Equal(35, M3BPlay.Engine.PresentAction(s, M3BPlay.C, M3BPlay.Plan(MainAction.PitchFulfillSponsor)).Value!.Threshold);
        var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.PitchFulfillSponsor));
        Assert.Equal(("pitch", 35), (started.Pending!.ActionRoll!.EventId, started.Pending.ActionRoll.Threshold));
    }

    [Fact]
    public void Pitch_in_a_due_week_shows_no_odds()
        => Assert.Null(M3BPlay.Engine.PresentAction(Due(), M3BPlay.C, M3BPlay.Plan(MainAction.PitchFulfillSponsor)).Value);

    [Fact]
    public void Successful_pitch_opens_an_offer_at_once()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var s = Due(week: 15, nextDue: 17) with { Followers = 2_000, Rng = Pcg32.Create(seed, CareerFactory.CareerStream) };
            var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.PitchFulfillSponsor));
            if (started.Pending!.ActionRoll!.Outcome == RollOutcome.Success)
            {
                Assert.Equal(16, Assert.Single(started.Offers!.Value).ExpiresAfterWeek);
                return;
            }
        }

        Assert.Fail("No seed in 1–200 gave a successful pitch.");
    }

    [Fact]
    public void Pitch_with_nothing_due_and_no_fitting_sponsor_is_disabled()
    {
        var c = M3BPlay.C with { Sponsors = [.. M3BPlay.C.Sponsors.Where(t => t.Id != "chess-board")] };
        var s = CareerFactory.Create(c, Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 3) with { Followers = 0, Energy = Fixed.FromInt(60) };
        Assert.Equal("no-sponsor-fits", M3BPlay.Engine.LegalPlans(s, c).Value.Single(p => p.Plan == M3BPlay.Plan(MainAction.PitchFulfillSponsor)).DisabledReason);
    }

    // trusted-tea accepted in week 10: due 13, 17, 21; payment 400; breach 200 PLN, −3.
    private static CareerState Due(int week = 13, int missed = 0, int nextDue = 13)
        => M3BPlay.Fresh() with
        {
            Week = new CareerWeek(week),
            LastSettledWeek = week - 1,
            Energy = Fixed.FromInt(60),
            Credibility = Fixed.FromInt(50),
            Contracts = [new SponsorContract("trusted-tea:10", "trusted-tea", SponsorKind.Contract, 10, nextDue, 21, 400, missed)],
        };

    private static CareerState ChessDeal()
        => M3BPlay.Fresh() with { Week = new CareerWeek(5), LastSettledWeek = 4, Contracts = [new SponsorContract("chess-board:5", "chess-board", SponsorKind.OneOff, 5, 5, 5, 250, 0)] };
}
