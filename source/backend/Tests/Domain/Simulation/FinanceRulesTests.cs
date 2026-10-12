using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class FinanceRulesTests
{
    private static readonly BalanceConstants Balance = TestWorld.Content.Balance;

    private static PendingWeek Pending(params ReportLine[] lines)
        => new(new CareerWeek(1), new WeekPlan(MainAction.NoAction, Category.Fps), [], [], Fixed.Zero, Fixed.One, 0, [.. lines]);

    private static long Amount(IEnumerable<ReportLine> lines, string code) => lines.Single(l => l.Code == code).AmountPln!.Value;

    [Fact]
    public void Income_and_costs_match_a_hand_computed_week()
    {
        // subs = round(3727 × 0.02 × 1 × 10) = 745; donations = round(3727 × 0.15) = 559; ads = round(3727 × 0.05 × 1 × 1) = 186
        // living 100; upkeep 4 slots × 10 at tier 0 = 40
        var r = FinanceRules.Compute(TestCareers.Dorm(1), Pending(), 3_727, Balance);
        Assert.Equal(745, Amount(r.Income, "income:subscriptions"));
        Assert.Equal(559, Amount(r.Income, "income:donations"));
        Assert.Equal(186, Amount(r.Income, "income:ads"));
        Assert.Equal(100, Amount(r.Costs, "cost:living"));
        Assert.Equal(40, Amount(r.Costs, "cost:equipment-upkeep"));
        Assert.Equal(745 + 559 + 186 - 100 - 40, r.CashDelta);
    }

    [Fact]
    public void Easy_reduces_living_cost_by_15_percent()
    {
        var easy = FinanceRules.Compute(TestCareers.Dorm(1) with { Difficulty = Difficulty.Easy }, Pending(), 0, Balance);
        var normal = FinanceRules.Compute(TestCareers.Dorm(1), Pending(), 0, Balance);
        Assert.Equal(85, Amount(easy.Costs, "cost:living"));
        Assert.Equal(34, Amount(easy.Costs, "cost:equipment-upkeep"));
        Assert.Equal(100, Amount(normal.Costs, "cost:living"));
    }

    [Fact]
    public void Upkeep_follows_each_slot_tier()
    {
        var s = TestCareers.Dorm(1);
        s = s with { Equipment = s.Equipment.SetItem(EquipmentSlot.Camera, 2).SetItem(EquipmentSlot.Computer, 4) };
        // 10 + 35 + 80 + 10
        Assert.Equal(135, Amount(FinanceRules.Compute(s, Pending(), 0, Balance).Costs, "cost:equipment-upkeep"));
    }

    [Fact]
    public void Event_cash_and_equipment_purchases_are_listed()
    {
        var r = FinanceRules.Compute(
            TestCareers.Dorm(1),
            Pending(
                new ReportLine("cost:equipment", AmountPln: 600, Detail: "Camera"),
                new ReportLine("income:event", AmountPln: 200, Detail: "donation-goal"),
                new ReportLine("cost:event", AmountPln: 150, Detail: "tech-failure"),
                new ReportLine("stat:Energy", Delta: Fixed.FromInt(5), Detail: "action")),
            0,
            Balance);
        Assert.Contains(new ReportLine("income:event", AmountPln: 200, Detail: "donation-goal"), r.Income);
        Assert.Contains(new ReportLine("cost:event", AmountPln: 150, Detail: "tech-failure"), r.Costs);
        Assert.Contains(new ReportLine("cost:equipment", AmountPln: 600, Detail: "Camera"), r.Costs);
        Assert.DoesNotContain(r.Income.Concat(r.Costs), l => l.Code.StartsWith("stat:", StringComparison.Ordinal));
        Assert.Equal(200 - 150 - 600 - 100 - 40, r.CashDelta);
    }
}
