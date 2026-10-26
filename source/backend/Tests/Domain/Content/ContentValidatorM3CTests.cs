using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Tests.Domain.Scoring;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

/// <summary>M3-C validator rules for the <c>balance.achievements</c> section.</summary>
public class ContentValidatorM3CTests
{
    private static ContentSnapshot With(ImmutableArray<AchievementDefinition> defs)
        => M3BTestContent.Valid() with { Balance = M3BTestContent.Valid().Balance with { Achievements = defs } };

    private static AchievementDefinition First => AchievementRulesTests.Defs[0];

    [Fact]
    public void Valid_achievements_pass() => Assert.Empty(ContentValidator.Validate(With(AchievementRulesTests.Defs)));

    [Fact]
    public void Duplicate_ids_are_rejected()
        => Assert.Contains("achievement:duplicate:fans", ContentValidator.Validate(With([First, First])));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Points_are_one_to_five(int points)
        => Assert.Contains("achievement:points:fans", ContentValidator.Validate(With([First with { Points = points }])));

    [Fact]
    public void A_threshold_kind_needs_a_positive_threshold()
    {
        Assert.Contains("achievement:threshold:fans", ContentValidator.Validate(With([First with { Threshold = null }])));
        Assert.Contains("achievement:threshold:fans", ContentValidator.Validate(With([First with { Threshold = Fixed.Zero }])));
        Assert.Contains("achievement:threshold:done", ContentValidator.Validate(With([First with { Id = "done", Kind = AchievementKind.Completed, Threshold = Fixed.One }])));
    }

    [Fact]
    public void Names_and_descriptions_are_valid_icu_text()
    {
        Assert.Contains("achievement:text:fans", ContentValidator.Validate(With([First with { Name = "" }])));
        Assert.Contains("achievement:text:fans", ContentValidator.Validate(With([First with { Description = "{name}" }])));
    }

    [Fact]
    public void An_empty_section_is_rejected()
        => Assert.Contains("achievement:empty", ContentValidator.Validate(With([])));
}
