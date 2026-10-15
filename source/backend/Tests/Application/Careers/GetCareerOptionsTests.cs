using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class GetCareerOptionsTests
{
    private static CareerOptionsView Options() => new GetCareerOptionsHandler(new AppTestKit().Current).Handle(new GetCareerOptions());

    [Fact]
    public void Every_background_difficulty_and_pronoun_set_is_offered_in_enum_order()
    {
        var options = Options();

        Assert.Equal(Enum.GetValues<Background>(), options.Backgrounds.Select(b => b.Background));
        Assert.Equal(Enum.GetValues<Difficulty>(), options.Difficulties.Select(d => d.Difficulty));
        Assert.Equal(Enum.GetValues<PronounSet>(), options.Pronouns);
    }

    [Fact]
    public void A_background_shows_its_starting_profile_from_the_current_content()
    {
        var dorm = Options().Backgrounds.Single(b => b.Background == Background.DormRoomNewcomer);

        Assert.Equal((2_000L, 0L, 18m, 0, 1.0m), (dorm.CashPln, dorm.Followers, dorm.Skill.Fps, dorm.Equipment.Microphone, dorm.ScoreMultiplier));
    }

    [Fact]
    public void A_difficulty_shows_its_score_multiplier()
        => Assert.Equal([1.0m, 1.1m], Options().Difficulties.Select(d => d.ScoreMultiplier));
}
