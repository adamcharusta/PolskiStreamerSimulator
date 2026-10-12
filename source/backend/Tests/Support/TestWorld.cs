using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The M1 engine and repository content shared by simulation tests.</summary>
public static class TestWorld
{
    public static WeekEngine Engine { get; } = new(new RulesetRegistry([new M1Ruleset()]));

    public static ContentSnapshot Content => TestContent.Valid();
}
