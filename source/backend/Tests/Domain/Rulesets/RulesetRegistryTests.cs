using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class RulesetRegistryTests
{
    private sealed record FakeRuleset(string Id) : IRuleset;

    [Fact]
    public void Resolve_returns_the_career_ruleset_not_the_latest()
    {
        var reg = new RulesetRegistry([new FakeRuleset("legacy.0"), new FakeRuleset("m1-prototype.1")]);
        Assert.Equal("legacy.0", reg.Resolve("legacy.0").Value.Id);
        Assert.Equal("m1-prototype.1", reg.Resolve("m1-prototype.1").Value.Id);
    }

    [Fact]
    public void Unknown_ruleset_fails_without_fallback()
    {
        var result = new RulesetRegistry([new FakeRuleset("m1-prototype.1")]).Resolve("x");
        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.UnknownRuleset, result.Error.Code);
        Assert.Equal("x", result.Error.Detail);
    }

    [Fact]
    public void Duplicate_ruleset_ids_are_rejected()
        => Assert.Throws<ArgumentException>(() => new RulesetRegistry([new FakeRuleset("a"), new FakeRuleset("a")]));

    [Fact]
    public void Failed_result_has_no_value()
        => Assert.Throws<InvalidOperationException>(() => new RulesetRegistry([]).Resolve("x").Value);
}
