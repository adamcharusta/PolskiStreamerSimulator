using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

/// <summary>Spec M4-A §4: a creation repeated with its creation ID returns the same career with new credentials.</summary>
public sealed class RepeatCreationTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CreateCareer Request(Guid creationId, string nickname = "Streamer")
        => new(nickname, Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, creationId);

    private async Task<AppResult<CreatedCareer>> CreateAsync(CreateCareer command)
    {
        await kit.ContentStore.EnsureStoredAsync(kit.Current.Files, Ct);
        return await kit.CreateHandler().Handle(command, Ct);
    }

    [Fact]
    public async Task A_new_career_returns_a_recovery_key_of_its_own()
    {
        var created = (await CreateAsync(Request(Guid.NewGuid()))).Value;

        Assert.True(RecoveryKeys.TryParse(created.RecoveryKey, out var careerId, out _));
        Assert.Equal(created.CareerId, careerId);
        Assert.NotEqual(created.Secret, created.RecoveryKey[^43..]);
        var credentials = await kit.Store.FindCredentialsAsync(created.CareerId, Ct);
        Assert.True(kit.Secrets.Matches(created.RecoveryKey, credentials!.RecoveryHash!));
    }

    [Fact]
    public async Task A_new_career_shows_its_expiry_six_months_ahead()
    {
        var created = (await CreateAsync(Request(Guid.NewGuid()))).Value;

        Assert.Equal(AppTestKit.Start.AddMonths(6), created.Career.ExpiresAt);
    }

    [Fact]
    public async Task A_repeat_returns_the_same_career_with_new_working_credentials()
    {
        var creationId = Guid.NewGuid();
        var first = (await CreateAsync(Request(creationId))).Value;
        kit.Time.Now = AppTestKit.Start.AddHours(23);

        var repeat = (await CreateAsync(Request(creationId))).Value;

        Assert.Equal(first.CareerId, repeat.CareerId);
        Assert.NotEqual(first.Secret, repeat.Secret);
        Assert.NotEqual(first.RecoveryKey, repeat.RecoveryKey);
        Assert.Equal(first.Career.Version, repeat.Career.Version);
        Assert.Equal(1, kit.Store.Count);
        Assert.True((await kit.Access.OpenAsync(first.CareerId, repeat.Secret, Ct)).IsSuccess);
        Assert.Equal(ErrorCodes.CareerNotFound, (await kit.Access.OpenAsync(first.CareerId, first.Secret, Ct)).Error.Code);
    }

    [Fact]
    public async Task A_repeat_with_another_request_is_refused()
    {
        var creationId = Guid.NewGuid();
        var first = (await CreateAsync(Request(creationId))).Value;

        var repeat = await CreateAsync(Request(creationId, "Other"));

        Assert.Equal(ErrorCodes.CreationIdReused, repeat.Error.Code);
        Assert.True((await kit.Access.OpenAsync(first.CareerId, first.Secret, Ct)).IsSuccess);
    }

    [Fact]
    public async Task A_repeat_after_24_hours_is_closed_and_keeps_the_credentials()
    {
        var creationId = Guid.NewGuid();
        var first = (await CreateAsync(Request(creationId))).Value;
        kit.Time.Now = AppTestKit.Start + CreateCareerHandler.RepeatWindow + TimeSpan.FromSeconds(1);

        var repeat = await CreateAsync(Request(creationId));

        Assert.Equal(ErrorCodes.CreationClosed, repeat.Error.Code);
        Assert.True((await kit.Access.OpenAsync(first.CareerId, first.Secret, Ct)).IsSuccess);
    }

    [Fact]
    public async Task A_repeat_after_the_first_action_is_closed_and_keeps_the_credentials()
    {
        var creationId = Guid.NewGuid();
        var first = (await CreateAsync(Request(creationId))).Value;
        kit.UseSecret(first.CareerId, first.Secret);
        await kit.PlayManualWeekAsync(first);

        var repeat = await CreateAsync(Request(creationId));

        Assert.Equal(ErrorCodes.CreationClosed, repeat.Error.Code);
        Assert.True((await kit.Access.OpenAsync(first.CareerId, first.Secret, Ct)).IsSuccess);
    }

    [Fact]
    public async Task A_creation_without_an_id_is_never_a_repeat()
    {
        await CreateAsync(new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman));
        await CreateAsync(new CreateCareer("Streamer", Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman));

        Assert.Equal(2, kit.Store.Count);
    }

    [Fact]
    public async Task A_concurrent_repeat_that_loses_the_insert_answers_as_a_repeat()
    {
        var creationId = Guid.NewGuid();
        var store = new CreationRaceStore(kit.Store);
        await kit.ContentStore.EnsureStoredAsync(kit.Current.Files, Ct);
        var handler = new CreateCareerHandler(store, kit.Current, kit.ContentStore, kit.Seasons, kit.Secrets, kit.Views, kit.Time);
        store.Race = () => handler.Handle(Request(creationId), Ct);

        var loser = (await handler.Handle(Request(creationId), Ct)).Value;

        Assert.Equal(1, kit.Store.Count);
        Assert.True((await kit.Access.OpenAsync(loser.CareerId, loser.Secret, Ct)).IsSuccess);
    }

    /// <summary>The first lookup finds nothing, then a concurrent creation under the same ID stores its career first.</summary>
    private sealed class CreationRaceStore(InMemoryCareerStore inner) : ForwardingCareerStore(inner)
    {
        private bool raced;

        public Func<Task>? Race { get; set; }

        public override async Task<Guid?> FindByCreationAsync(byte[] creationHash, CancellationToken cancellationToken)
        {
            if (!raced)
            {
                raced = true;
                await Race!();
                return null;
            }

            return await base.FindByCreationAsync(creationHash, cancellationToken);
        }
    }
}
