using FsCheck;
using FsCheck.Xunit;
using PolskiStreamerSimulator.Domain.Careers;
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
}
