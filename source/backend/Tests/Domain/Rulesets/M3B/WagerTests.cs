using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets.M3B;

public class WagerTests
{
    private static readonly WeekPlan LowWager = M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low);

    [Theory]
    [InlineData(WagerTier.Low, 399, "needs-cash")]
    [InlineData(WagerTier.Low, 400, null)]
    [InlineData(WagerTier.Medium, 2_000, null)]
    [InlineData(WagerTier.High, 7_999, "needs-cash")]
    [InlineData(WagerTier.Low, 0, "needs-cash")]
    public void Stake_must_fit_a_quarter_of_positive_cash(WagerTier tier, long cash, string? reason)
        => Assert.Equal(reason, Reason(Cash(cash), M3BPlay.Plan(MainAction.NoAction, Category.Gambling, tier)));

    [Fact]
    public void Upgrade_in_the_same_plan_leaves_too_little_cash_for_the_stake()
    {
        var plan = new WeekPlan(MainAction.ImproveProduction, Category.Gambling, UpgradeSlot: EquipmentSlot.Microphone, Wager: WagerTier.Low);
        Assert.Equal("needs-cash", Reason(Cash(800), plan));   // 800 − 600 = 200 spendable < 400
        Assert.Null(Reason(Cash(1_000), plan));               // 400 spendable
    }

    [Fact]
    public void Negative_decision_carry_counts_against_the_stake()
        => Assert.Equal("needs-cash", Reason(Cash(400) with { Carry = DecisionCarry.Empty with { CashPln = -1 } }, LowWager));

    [Fact]
    public void Start_week_refuses_a_stake_the_state_no_longer_covers()
    {
        var result = M3BPlay.Engine.StartWeek(Cash(399), M3BPlay.C, LowWager, 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "needs-cash"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Terms_show_stake_chance_payout_and_loss()
        => Assert.Equal(new WagerTerms(WagerTier.Medium, 500, Fixed.FromRatio(45, 100), 1_000, 500), WagerRules.Terms(WagerTier.Medium, M3BPlay.C.Balance.Wagers!));

    [Fact]
    public void Won_wager_pays_the_net_win_and_reconciles()
    {
        var (before, after, report) = UntilWager(won: true);
        Assert.Contains(report.Income, l => l is { Code: "income:wager", AmountPln: 100 });
        Assert.Equal((true, 45, 100L), (report.Wager!.Won, report.Wager.Threshold, report.Wager.NetPln));
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Drama" && l.Detail == "wager" && l.Delta == Fixed.FromInt(2));
        Assert.Equal(after.CashPln - before.CashPln, report.CashDelta);
    }

    [Fact]
    public void Lost_wager_costs_the_stake_and_three_drama()
    {
        var (before, after, report) = UntilWager(won: false);
        Assert.Contains(report.Costs, l => l is { Code: "cost:wager", AmountPln: 100 });
        Assert.Equal(-100L, report.Wager!.NetPln);
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Drama" && l.Detail == "wager" && l.Delta == Fixed.FromInt(3));
        Assert.Equal(after.CashPln - before.CashPln, report.CashDelta);
    }

    [Theory]
    [InlineData(true, 115)]
    [InlineData(false, 105)]
    public void Wager_raises_the_audience_multiplier(bool won, int hundredths)
        => Assert.Equal(Fixed.FromRatio(hundredths, 100), UntilStarted(won).Started.Pending!.AudienceMultiplier);

    [Fact]
    public void Gambling_without_a_stake_draws_no_wager()
        => Assert.Null(M3BPlay.Start(Cash(4_000), M3BPlay.Plan(MainAction.NoAction, Category.Gambling)).Pending!.Wager);

    [Fact]
    public void Expected_value_of_a_stake_is_minus_ten_percent()
    {
        var w = M3BPlay.C.Balance.Wagers!;
        var terms = WagerRules.Terms(WagerTier.High, w);
        Assert.Equal(Fixed.FromInt(-200), (w.Chance * Fixed.FromInt(terms.PayoutPln - terms.StakePln)) - ((Fixed.One - w.Chance) * Fixed.FromInt(terms.LossPln)));
    }

    private static CareerState Cash(long pln) => M3BPlay.Fresh() with { CashPln = pln, Energy = Fixed.FromInt(60) };

    /// <summary>The first seed in 1–200 whose Low wager week starts with the wanted result, checked before any event resolves.</summary>
    private static (CareerState Before, CareerState Started) UntilStarted(bool won)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var before = Cash(4_000) with { Rng = Pcg32.Create(seed, CareerFactory.CareerStream) };
            var started = M3BPlay.Start(before, LowWager);
            if (started.Pending!.Wager!.Won == won)
            {
                return (before, started);
            }
        }

        throw new InvalidOperationException("No seed in 1–200 gave the wanted wager result.");
    }

    private static (CareerState Before, CareerState After, WeekReport Report) UntilWager(bool won)
    {
        var (before, started) = UntilStarted(won);
        var (after, report) = M3BPlay.SettleAll(started);
        return (before, after, report);
    }

    private static string? Reason(CareerState s, WeekPlan plan)
        => M3BPlay.Engine.LegalPlans(s, M3BPlay.C).Value.Single(p => p.Plan == plan).DisabledReason;
}
