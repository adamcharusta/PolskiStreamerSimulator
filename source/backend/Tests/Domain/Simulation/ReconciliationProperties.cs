using FsCheck;
using FsCheck.Xunit;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class ReconciliationProperties
{
    /// <summary>Default plan, but every third week buys the cheapest legal equipment upgrade when one exists.</summary>
    private static WeekPlan PlanFor(CareerState s)
    {
        if (s.Week.Value % 3 == 0)
        {
            var upgrade = TestWorld.Engine.LegalPlans(s, TestWorld.Content).Value
                .FirstOrDefault(l => l.Plan.Action == MainAction.ImproveProduction && l.DisabledReason is null);
            if (upgrade.Plan is not null)
            {
                return upgrade.Plan;
            }
        }

        return Play.DefaultPlan(s);
    }

    [Property(MaxTest = 200)]
    public bool Cash_delta_reconciles_with_report_lines(PositiveInt seed, NonNegativeInt weeks)
    {
        var s = TestCareers.Dorm((ulong)seed.Get);
        for (var i = 0; i < weeks.Get % 60 && s.Status == CareerStatus.Active; i++)
        {
            var (next, report) = Play.OneWeek(s, PlanFor(s));
            var lines = report.Income.Sum(l => l.AmountPln!.Value) - report.Costs.Sum(l => l.AmountPln!.Value);
            if (lines != report.CashDelta || report.CashDelta != next.CashPln - s.CashPln)
            {
                return false;
            }

            s = next;
        }

        return true;
    }

    [Fact]
    public void Settlement_applies_audience_and_money_to_the_career()
    {
        var before = TestCareers.Dorm(21) with { Followers = 5_000 };
        var (after, report) = Play.OneWeek(before);
        Assert.Equal(before.Followers + report.FollowersNet, after.Followers);
        Assert.Equal(report.FollowersGained - report.FollowersLost, report.FollowersNet);
        Assert.Equal(report.LiveViewers, after.LastLiveViewers);
        Assert.True(report.LiveViewers > 0);
        Assert.Equal(report.LiveViewers, after.Metrics.TotalLiveViews);
        Assert.Equal(before.CashPln + report.CashDelta, after.CashPln);
        Assert.Equal(report.Income.Sum(l => l.AmountPln!.Value), after.Metrics.IncomeBySource.Values.Sum());
        Assert.Equal(report.Costs.Sum(l => l.AmountPln!.Value), after.Metrics.CostsBySource.Values.Sum());
    }

    [Fact]
    public void Purchase_paid_at_start_is_counted_once()
    {
        var before = TestCareers.Dorm(22);
        var (after, report) = Play.OneWeek(before, new WeekPlan(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Microphone));
        Assert.Contains(report.Costs, l => l.Code == "cost:equipment" && l.AmountPln == 600);
        Assert.Equal(before.CashPln + report.CashDelta, after.CashPln);
    }
}
