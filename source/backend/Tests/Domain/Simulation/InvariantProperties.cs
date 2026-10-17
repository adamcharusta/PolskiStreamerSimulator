using FsCheck;
using FsCheck.Xunit;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class InvariantProperties
{
    [Property(MaxTest = 50)]
    public bool Random_legal_choices_never_break_invariants(PositiveInt seed)
    {
        var background = (Background)(seed.Get % 4);
        var difficulty = seed.Get % 2 == 0 ? Difficulty.Normal : Difficulty.Easy;
        var r = CareerRunner.Run(TestWorld.Engine, TestWorld.Content, TestCareers.For(background, difficulty, (ulong)seed.Get), new RandomLegalPolicy((ulong)seed.Get));
        return r.InvariantViolations.Count == 0 && r.Final.Status == CareerStatus.Ended && r.Final.FinalScore is not null;
    }

    [Property(MaxTest = 20)]
    public bool M3_random_legal_choices_keep_stats_bounded_and_reports_reconciled(PositiveInt seed)
    {
        var start = M3Play.Fresh((ulong)seed.Get);
        var previousCash = start.CashPln;
        var bounded = true;
        var reconciled = true;
        var weeks = 0;
        var r = CareerRunner.Run(M3Play.Engine, M3Play.C, start, new RandomLegalPolicy((ulong)seed.Get, retireFromWeek: 200), (s, report) =>
        {
            weeks++;
            bounded &= new[] { s.Energy, s.Burnout, s.Drama, s.Credibility }.All(v => v >= Fixed.Zero && v <= Fixed.Hundred);
            reconciled &= report.Income.Sum(l => l.AmountPln!.Value) - report.Costs.Sum(l => l.AmountPln!.Value) == report.CashDelta
                && s.CashPln - previousCash == report.CashDelta;
            previousCash = s.CashPln;
        });
        return r.InvariantViolations.Count == 0 && bounded && reconciled && weeks > 0;
    }
}
