using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Week report views for tests of code that reads reports.</summary>
public static class TestReports
{
    public static RollView Roll() => new("x", "y", 10, 50, [], RollOutcome.Success);

    public static WeekReportView Week(int week) => new(
        week,
        new PlanView(MainAction.NoAction, Category.Fps, null, null, null, null),
        [],
        100,
        5,
        1,
        4,
        [],
        [],
        0,
        [],
        null,
        [],
        null,
        null,
        null,
        null,
        null);
}
