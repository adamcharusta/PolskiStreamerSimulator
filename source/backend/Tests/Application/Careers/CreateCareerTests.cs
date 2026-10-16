using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class CreateCareerTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_career_stores_it_and_returns_a_secret_and_the_first_plan()
    {
        var created = await kit.NewCareerAsync();

        Assert.Equal(43, created.Secret.Length);
        var stored = await kit.Store.FindAsync(created.CareerId, Ct);
        Assert.NotNull(stored);
        Assert.Equal(M1Ruleset.RulesetId, stored.State.RulesetId);
        Assert.Equal(TestContent.Valid().Digest, stored.State.ContentDigest);
        Assert.Equal(AppTestKit.Start, stored.CreatedAt);
        Assert.True(kit.Secrets.Matches(created.Secret, (await kit.Store.FindSecretHashAsync(created.CareerId, Ct))!));
        Assert.Equal(NextStepKind.PlanWeek, created.Career.Next.Kind);
        Assert.NotEmpty(created.Career.Next.Plans);
        Assert.Equal(1, created.Career.Week);
        Assert.Equal(stored.State.StateVersion, created.Career.Version);
        Assert.Equal(TestContent.Valid().Profiles[Background.DormRoomNewcomer].CashPln, created.Career.Stats.CashPln);
        Assert.Null(created.Career.LastReport);
    }

    [Fact]
    public async Task The_nickname_is_stored_trimmed()
    {
        var created = await kit.NewCareerAsync("  Zażółć_1.  ");

        Assert.Equal("Zażółć_1.", created.Career.Nickname);
        Assert.Equal("Zażółć_1.", (await kit.Store.FindAsync(created.CareerId, Ct))!.Nickname);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("😀😀😀")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("   ")]
    [InlineData("name!")]
    public async Task An_invalid_nickname_is_rejected(string nickname)
    {
        var result = await kit.CreateHandler().Handle(new CreateCareer(nickname, Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task An_undefined_background_is_rejected()
    {
        var result = await kit.CreateHandler().Handle(new CreateCareer("Streamer", (Background)99, Difficulty.Normal, PronounSet.Woman), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task Two_careers_get_different_ids_and_secrets()
    {
        var first = await kit.NewCareerAsync();
        var second = await kit.NewCareerAsync();

        Assert.NotEqual(first.CareerId, second.CareerId);
        Assert.NotEqual(first.Secret, second.Secret);
    }
}
