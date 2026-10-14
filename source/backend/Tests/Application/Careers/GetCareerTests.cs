using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class GetCareerTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_careers_secret_opens_it()
    {
        var created = await kit.NewCareerAsync();

        var view = (await kit.GetHandler().Handle(new GetCareer(created.CareerId, created.Secret), Ct)).Value;

        Assert.Equal(created.Career.Version, view.Version);
        Assert.Equal("Streamer", view.Nickname);
        Assert.Null(view.LastReport);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_secret_is_rejected(string? secret)
    {
        var created = await kit.NewCareerAsync();

        var result = await kit.GetHandler().Handle(new GetCareer(created.CareerId, secret), Ct);

        Assert.Equal(ErrorCodes.MissingSecret, result.Error.Code);
    }

    [Fact]
    public async Task Another_careers_secret_does_not_open_it()
    {
        var mine = await kit.NewCareerAsync();
        var theirs = await kit.NewCareerAsync();

        var result = await kit.GetHandler().Handle(new GetCareer(mine.CareerId, theirs.Secret), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }

    [Fact]
    public async Task An_unknown_career_is_not_found()
    {
        var created = await kit.NewCareerAsync();

        var result = await kit.GetHandler().Handle(new GetCareer(Guid.NewGuid(), created.Secret), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }
}
