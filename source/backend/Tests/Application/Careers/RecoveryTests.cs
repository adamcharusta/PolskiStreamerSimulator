using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>Spec M4-A §3: a career's own key opens only that career and rotates its browser secret.</summary>
public sealed class RecoveryTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<AppResult<RecoveredCareer>> RecoverAsync(string? key) => kit.RecoverHandler().Handle(new RecoverCareer(key), Ct);

    [Fact]
    public async Task Recovery_opens_the_career_with_a_new_secret_and_the_old_secret_stops_working()
    {
        var created = await kit.NewCareerAsync();
        kit.Time.Now = AppTestKit.Start.AddDays(10);

        var recovered = (await RecoverAsync(created.RecoveryKey)).Value;

        Assert.Equal(created.CareerId, recovered.CareerId);
        Assert.Equal(created.Career.Version, recovered.Career.Version);
        Assert.NotEqual(created.Secret, recovered.Secret);
        Assert.True((await kit.Access.OpenAsync(created.CareerId, recovered.Secret, Ct)).IsSuccess);
        Assert.Equal(ErrorCodes.CareerNotFound, (await kit.Access.OpenAsync(created.CareerId, created.Secret, Ct)).Error.Code);
    }

    [Fact]
    public async Task Recovery_keeps_the_key_valid_and_does_not_move_the_last_action_or_the_expiry()
    {
        var created = await kit.NewCareerAsync();
        kit.Time.Now = AppTestKit.Start.AddDays(10);

        await RecoverAsync(created.RecoveryKey);
        var again = await RecoverAsync(created.RecoveryKey);

        Assert.True(again.IsSuccess);
        var stored = await kit.Store.FindAsync(created.CareerId, Ct);
        Assert.Equal(AppTestKit.Start, stored!.LastActionAt);
        Assert.Equal(AppTestKit.Start.AddMonths(6), stored.ExpiresAt);
        Assert.Equal(AppTestKit.Start.AddMonths(6), again.Value.Career.ExpiresAt);
    }

    [Fact]
    public async Task One_careers_key_never_opens_another_career()
    {
        var first = await kit.NewCareerAsync();
        var second = await kit.NewCareerAsync();
        var random = first.RecoveryKey[^43..];

        var swapped = await RecoverAsync(RecoveryKeys.Format(second.CareerId, random));

        Assert.Equal(ErrorCodes.RecoveryFailed, swapped.Error.Code);
        Assert.True((await kit.Access.OpenAsync(second.CareerId, second.Secret, Ct)).IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a key")]
    public async Task A_malformed_key_fails_like_a_wrong_one(string? key)
        => Assert.Equal(ErrorCodes.RecoveryFailed, (await RecoverAsync(key)).Error.Code);

    [Fact]
    public async Task A_key_of_an_unknown_career_fails_like_a_wrong_one()
        => Assert.Equal(ErrorCodes.RecoveryFailed, (await RecoverAsync(RecoveryKeys.Format(Guid.NewGuid(), kit.Secrets.NewSecret()))).Error.Code);

    [Fact]
    public async Task A_deleted_career_cannot_be_recovered()
    {
        var created = await kit.NewCareerAsync();
        kit.Time.Now = AppTestKit.Start.AddMonths(6);
        await kit.Sweep().RunAsync(Ct);

        Assert.Equal(ErrorCodes.RecoveryFailed, (await RecoverAsync(created.RecoveryKey)).Error.Code);
    }

    [Fact]
    public async Task Issuing_a_key_replaces_the_old_one_and_keeps_the_secret()
    {
        var created = await kit.NewCareerAsync();

        var issued = (await kit.IssueKeyHandler().Handle(new IssueRecoveryKey(created.CareerId, created.Secret), Ct)).Value;

        Assert.NotEqual(created.RecoveryKey, issued.RecoveryKey);
        Assert.Equal(ErrorCodes.RecoveryFailed, (await RecoverAsync(created.RecoveryKey)).Error.Code);
        Assert.True((await kit.Access.OpenAsync(created.CareerId, created.Secret, Ct)).IsSuccess);
        Assert.True((await RecoverAsync(issued.RecoveryKey)).IsSuccess);
        Assert.Equal(AppTestKit.Start, (await kit.Store.FindAsync(created.CareerId, Ct))!.LastActionAt);
    }

    [Fact]
    public async Task Issuing_a_key_needs_the_careers_secret()
    {
        var created = await kit.NewCareerAsync();
        var handler = kit.IssueKeyHandler();

        Assert.Equal(ErrorCodes.MissingSecret, (await handler.Handle(new IssueRecoveryKey(created.CareerId, null), Ct)).Error.Code);
        Assert.Equal(ErrorCodes.CareerNotFound, (await handler.Handle(new IssueRecoveryKey(created.CareerId, "wrong"), Ct)).Error.Code);
    }

    [Fact]
    public async Task A_career_without_a_key_cannot_be_recovered_until_it_gets_one()
    {
        var created = await kit.NewCareerAsync();
        kit.Store.ClearRecovery(created.CareerId);

        Assert.Equal(ErrorCodes.RecoveryFailed, (await RecoverAsync(created.RecoveryKey)).Error.Code);

        var issued = (await kit.IssueKeyHandler().Handle(new IssueRecoveryKey(created.CareerId, created.Secret), Ct)).Value;
        Assert.True((await RecoverAsync(issued.RecoveryKey)).IsSuccess);
    }

    [Fact]
    public async Task A_recovered_career_can_be_played_with_the_new_secret()
    {
        var created = await kit.NewCareerAsync();
        var recovered = (await RecoverAsync(created.RecoveryKey)).Value;
        kit.UseSecret(created.CareerId, recovered.Secret);

        var played = await kit.PlayManualWeekAsync(recovered.Career);

        Assert.Equal(1, played.LastSettledWeek);
    }
}
