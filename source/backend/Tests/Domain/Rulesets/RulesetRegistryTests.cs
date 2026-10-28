using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class RulesetRegistryTests
{
    [Fact]
    public void M3_ruleset_is_registered_for_both_ids()
    {
        var registry = new RulesetRegistry([new M1Ruleset(), .. M3Ruleset.Versions]);
        Assert.Equal("m3-systems.1", registry.Resolve("m3-systems.1").Value.Id);
        Assert.Equal("m3-systems.2", registry.Resolve("m3-systems.2").Value.Id);
    }

    [Fact]
    public void M3_ruleset_refuses_an_unknown_id()
        => Assert.Throws<ArgumentException>(() => new M3Ruleset("m3-systems.9"));

    [Fact]
    public void Stored_m3_systems_1_career_resolves_to_its_own_ruleset()
    {
        var c = M3GoldenTests.Content;
        var s = CareerFactory.Create(c.RulesetId, c.Digest, c.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 5);
        var restored = CanonicalJson.Deserialize(CanonicalJson.Serialize(s));
        var week = M3Play.Engine.StartWeek(restored, c, new WeekPlan(MainAction.NoAction, Category.Fps), restored.StateVersion);
        Assert.True(week.IsSuccess);
        Assert.Equal("m3-systems.1", week.Value.RulesetId);
    }
    [Fact]
    public void Resolve_returns_the_career_ruleset_not_the_latest()
    {
        var reg = new RulesetRegistry([new LegacyRuleset("legacy.0"), new LegacyRuleset("m1-prototype.1")]);
        Assert.Equal("legacy.0", reg.Resolve("legacy.0").Value.Id);
        Assert.Equal("m1-prototype.1", reg.Resolve("m1-prototype.1").Value.Id);
    }

    [Fact]
    public void Unknown_ruleset_fails_without_fallback()
    {
        var result = new RulesetRegistry([new LegacyRuleset("m1-prototype.1")]).Resolve("x");
        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.UnknownRuleset, result.Error.Code);
        Assert.Equal("x", result.Error.Detail);
    }

    [Fact]
    public void Duplicate_ruleset_ids_are_rejected()
        => Assert.Throws<ArgumentException>(() => new RulesetRegistry([new LegacyRuleset("a"), new LegacyRuleset("a")]));

    [Fact]
    public void Failed_result_has_no_value()
        => Assert.Throws<InvalidOperationException>(() => new RulesetRegistry([]).Resolve("x").Value);
}
