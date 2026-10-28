using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

/// <summary>A short stream after the recovery crisis has its own report line (M3-D, #82).</summary>
public class ShortStreamLineTests
{
    [Fact]
    public void A_short_stream_week_reports_it()
    {
        var s = CareerFlags.Set(M3Play.Fresh(), "sys:short-stream", 1);
        var (_, report) = M3Play.SettleAll(M3Play.Start(s, MainAction.Rest));
        Assert.Contains(report.StatChanges, l => l.Code == "week:short-stream");
    }

    [Fact]
    public void A_regular_week_does_not()
    {
        var (_, report) = M3Play.SettleAll(M3Play.Start(M3Play.Fresh(), MainAction.Rest));
        Assert.DoesNotContain(report.StatChanges, l => l.Code == "week:short-stream");
    }
}
