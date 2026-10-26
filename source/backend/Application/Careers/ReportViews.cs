using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets.M3B;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Domain results as contract views.</summary>
public static class ReportViews
{
    public static PlanView Plan(WeekPlan plan) => new(plan.Action, plan.Category, plan.TrainTarget, plan.UpgradeSlot, plan.Wager, plan.TargetPlatform);

    public static ModifierView Modifier(ModifierLine line) => new(line.LabelKey, Decimals.Of(line.Value));

    public static RollView Roll(RollResult roll)
        => new(roll.EventId, roll.ChoiceId, roll.Roll, roll.Threshold, [.. roll.Modifiers.Select(Modifier)], roll.Outcome);

    public static ReportLineView Line(ReportLine line)
        => new(line.Code, line.AmountPln, line.Delta is { } delta ? Decimals.Of(delta) : null, line.Detail);

    /// <summary>The report with the roll texts of <paramref name="c"/> (M3-D).</summary>
    public static WeekReportView Report(WeekReport report, ContentSnapshot c) => Report(report) with
    {
        Rolls = [.. report.Rolls.Select(r => RollTexts.Of(r, c))],
        DecisionRolls = report.DecisionRolls.IsDefault ? [] : [.. report.DecisionRolls.Select(r => RollTexts.Of(r, c))],
        ActionRoll = report.ActionRoll is { } action ? RollTexts.Of(action, c) : null,
    };

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
        report.Ending,
        report.DecisionRolls.IsDefault ? [] : [.. report.DecisionRolls.Select(Roll)],
        report.ActionRoll is { } action ? Roll(action) : null,
        report.Viral is { } viral ? Viral(viral) : null,
        report.Strike is { } strike ? new StrikeRollView(strike.Platform, strike.Exposure, strike.Threshold, strike.Roll, strike.Strike, strike.Banned) : null,
        report.Wager is { } wager ? new WagerResultView(wager.Tier, wager.StakePln, wager.Roll, wager.Threshold, wager.Won, wager.NetPln) : null,
        report.Migration is { } moved ? new MigrationView(moved.From, moved.To, Decimals.Of(moved.Retention), moved.FollowersBefore, moved.FollowersAfter) : null);

    public static MigrationEstimateView Migration(MigrationEstimate m) => new(
        m.From,
        m.To,
        m.Executes,
        Decimals.Of(m.RetentionMin),
        Decimals.Of(m.RetentionMax),
        m.FollowersMin,
        m.FollowersMax,
        new PlatformProfileView(Decimals.Of(m.Target.Reach), Decimals.Of(m.Target.SubYield), Decimals.Of(m.Target.AdYield), Decimals.Of(m.Target.PolicyRisk)));

    public static ViralView Viral(ViralResult viral)
        => new(viral.Roll, viral.Threshold, Decimals.Of(viral.Multiplier), viral.FollowersBonus);
}
