using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Domain results as contract views.</summary>
public static class ReportViews
{
    public static PlanView Plan(WeekPlan plan) => new(plan.Action, plan.Category, plan.TrainTarget, plan.UpgradeSlot);

    public static ModifierView Modifier(ModifierLine line) => new(line.LabelKey, Decimals.Of(line.Value));

    public static RollView Roll(RollResult roll)
        => new(roll.EventId, roll.ChoiceId, roll.Roll, roll.Threshold, [.. roll.Modifiers.Select(Modifier)], roll.Outcome);

    public static ReportLineView Line(ReportLine line)
        => new(line.Code, line.AmountPln, line.Delta is { } delta ? Decimals.Of(delta) : null, line.Detail);

    public static WeekReportView Report(WeekReport report) => new(
        report.Week.Value,
        Plan(report.Plan),
        [.. report.Rolls.Select(Roll)],
        report.LiveViewers,
        report.FollowersGained,
        report.FollowersLost,
        report.FollowersNet,
        [.. report.Income.Select(Line)],
        [.. report.Costs.Select(Line)],
        report.CashDelta,
        [.. report.StatChanges.Select(Line)],
        report.Ending);
}
