using System.Collections.Concurrent;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;
using PolskiStreamerSimulator.Application.Saves;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>
/// Handlers over in-memory stores (or the given ones), a fixed clock and the repository content. New careers pin
/// <see cref="Current"/>, which is stored before each creation, as the API stores it at startup.
/// </summary>
public sealed class AppTestKit
{
    public static readonly DateTimeOffset Start = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly ConcurrentDictionary<Guid, string> secrets = new();
    private InMemorySeasonStore? seasonStore;
    private InMemoryLeaderboardStore? entryStore;

    public AppTestKit(WeekEngine? engine = null)
        : this(engine ?? TestWorld.Engine, TestPaths.ContentM1, null, null)
    {
    }

    private AppTestKit(
        WeekEngine engine,
        string contentDirectory,
        ICareerStore? careers,
        IContentStore? contents,
        ISeasonStore? seasons = null,
        ILeaderboardStore? entries = null,
        ISaveSlotStore? slots = null)
    {
        Engine = engine;
        Current = new TestCurrentContent(contentDirectory);
        Careers = careers ?? Store;
        ContentStore = contents ?? Contents;
        Seasons = seasons ?? SeasonStore;
        Entries = entries ?? EntryStore;
        Slots = slots ?? new InMemorySaveSlotStore(Store);
    }

    public WeekEngine Engine { get; }

    public InMemoryCareerStore Store { get; } = new();

    public InMemoryContentStore Contents { get; } = new(TestContent.Valid());

    /// <summary>The career store the handlers use: <see cref="Store"/> unless another one was given.</summary>
    public ICareerStore Careers { get; }

    /// <summary>The content store the handlers use: <see cref="Contents"/> unless another one was given.</summary>
    public IContentStore ContentStore { get; }

    /// <summary>The audit trail of the in-memory season and leaderboard stores.</summary>
    public InMemoryAuditTrail AuditStore { get; } = new();

    public InMemorySeasonStore SeasonStore => seasonStore ??= new InMemorySeasonStore(AuditStore);

    public InMemoryLeaderboardStore EntryStore => entryStore ??= new InMemoryLeaderboardStore(AuditStore);

    /// <summary>The season store the handlers use: <see cref="SeasonStore"/> unless another one was given.</summary>
    public ISeasonStore Seasons { get; }

    /// <summary>The leaderboard store the handlers use: <see cref="EntryStore"/> unless another one was given.</summary>
    public ILeaderboardStore Entries { get; }

    /// <summary>The slot store the handlers use: in memory over <see cref="Store"/> unless another one was given.</summary>
    public ISaveSlotStore Slots { get; }

    public FixedTimeProvider Time { get; } = new(Start);

    public Sha256SecretHasher Secrets { get; } = new();

    public TestCurrentContent Current { get; }

    public CareerViewBuilder Views => new(Engine);

    public CareerAccess Access => new(Careers, Secrets);

    /// <summary>A kit with both rulesets whose new careers pin the content in <paramref name="directory"/>.</summary>
    public static AppTestKit WithContent(string directory) => new(M3Play.Engine, directory, null, null);

    /// <summary>A kit with both rulesets over the given stores, for example the SQL Server ones.</summary>
    public static AppTestKit On(ICareerStore careers, IContentStore contents, string directory) => new(M3Play.Engine, directory, careers, contents);

    /// <summary>A kit with both rulesets over the given career, content, season and leaderboard stores.</summary>
    public static AppTestKit On(ICareerStore careers, IContentStore contents, ISeasonStore seasons, ILeaderboardStore entries, string directory)
        => new(M3Play.Engine, directory, careers, contents, seasons, entries);

    /// <summary>A kit with both rulesets over the given career, content and slot stores.</summary>
    public static AppTestKit On(ICareerStore careers, IContentStore contents, ISaveSlotStore slots, string directory)
        => new(M3Play.Engine, directory, careers, contents, slots: slots);

    /// <summary>Later careers pin the content in <paramref name="directory"/>; existing careers keep theirs.</summary>
    public void SwitchCurrentContent(string directory) => Current.Switch(directory);

    public CreateCareerHandler CreateHandler() => new(Careers, Current, ContentStore, Seasons, Secrets, Views, Time);

    public LeaderboardPublication Publication => new(Seasons, Entries, Time);

    public PublishResultHandler PublishHandler() => new(Access, Publication, Entries, Time);

    public GetEntryStatusHandler EntryStatusHandler() => new(Access, Publication);

    public GetLeaderboardHandler LeaderboardHandler() => new(Seasons, Entries);

    public ListSeasonsHandler SeasonsHandler() => new(Seasons);

    public ExtendSeasonHandler ExtendHandler() => new(Seasons, Time);

    public ListModeratedEntriesHandler ModeratedHandler() => new(Seasons, EntryStore);

    public ModerateEntryHandler ModerateHandler() => new(EntryStore, Entries, Time);

    public ListAuditHandler AuditHandler() => new(AuditStore);

    public GetCareerHandler GetHandler() => new(Access, Careers, ContentStore, Views);

    public ListSaveSlotsHandler SlotsHandler() => new(Access, Slots);

    public SaveToSlotHandler SaveHandler() => new(Access, Slots, Time);

    public RestoreSlotHandler RestoreHandler() => new(Access, Careers, Slots, ContentStore, Views, Time);

    public ExportCareerHandler ExportHandler() => new(Access, Time);

    public ImportCareerHandler ImportHandler() => new(Careers, ContentStore, Engine, Secrets, Views, Time);

    public RecoverCareerHandler RecoverHandler() => new(Careers, ContentStore, Secrets, Views, Time);

    public IssueRecoveryKeyHandler IssueKeyHandler() => new(Access, Careers, Secrets, Time);

    public RetentionSweep Sweep() => new(Careers, Time);

    public SubmitActionHandler SubmitHandler(ICareerStore? store = null)
    {
        var s = store ?? Careers;
        return new SubmitActionHandler(new CareerAccess(s, Secrets), s, ContentStore, Engine, Views, Time);
    }

    public GetHistoryHandler HistoryHandler() => new(Access, Careers);

    public GetHistoryWeekHandler WeekHandler() => new(Access, Careers);

    public RunBatchHandler BatchHandler(ICareerStore? store = null)
    {
        var s = store ?? Careers;
        return new RunBatchHandler(new CareerAccess(s, Secrets), s, ContentStore, Engine, Views, Time);
    }

    public async Task<CreatedCareer> NewCareerAsync(string nickname = "Streamer", Background background = Background.DormRoomNewcomer, bool ranked = false)
    {
        await StartAsync();
        var created = (await CreateHandler().Handle(new CreateCareer(nickname, background, Difficulty.Normal, PronounSet.Woman, Ranked: ranked), Ct)).Value;
        secrets[created.CareerId] = created.Secret;
        return created;
    }

    /// <summary>What the API does at startup: stores the current content and makes the open season match it.</summary>
    public async Task StartAsync()
    {
        await ContentStore.EnsureStoredAsync(Current.Files, Ct);
        await Seasons.EnsureCurrentAsync(Current.Snapshot.RulesetId, Current.Snapshot.Digest, Time.GetUtcNow(), Ct);
    }

    /// <summary>Retires the career at once; a retired career has a final score and can publish it.</summary>
    public async Task<CareerView> RetireAsync(CareerView career)
        => (await SubmitAsync(career.CareerId, Guid.NewGuid(), new ActionInput(career.Version, ActionType.Retire, null, null, null, null, null))).Value.Career;

    public Task<AppResult<EntryStatusView>> PublishAsync(Guid careerId, string? nickname)
        => PublishHandler().Handle(new PublishResult(careerId, secrets[careerId], nickname), Ct);

    public async Task<EntryStatusView> EntryStatusAsync(Guid careerId)
        => (await EntryStatusHandler().Handle(new GetEntryStatus(careerId, secrets[careerId]), Ct)).Value;

    public async Task<HistoryView> HistoryAsync(Guid careerId)
        => (await HistoryHandler().Handle(new GetHistory(careerId, secrets[careerId]), Ct)).Value;

    public Task<AppResult<SaveSlotsView>> SlotsAsync(Guid careerId)
        => SlotsHandler().Handle(new ListSaveSlots(careerId, secrets[careerId]), Ct);

    public Task<AppResult<SaveSlotsView>> SaveSlotAsync(Guid careerId, int slot, long expectedVersion)
        => SaveHandler().Handle(new SaveToSlot(careerId, secrets[careerId], slot, expectedVersion), Ct);

    public Task<AppResult<ActionOutcome>> RestoreAsync(Guid careerId, int slot, long expectedVersion, Guid? commandId = null)
        => RestoreHandler().Handle(new RestoreSlot(careerId, secrets[careerId], slot, commandId ?? Guid.NewGuid(), expectedVersion), Ct);

    public Task<AppResult<SaveFile>> ExportAsync(Guid careerId)
        => ExportHandler().Handle(new ExportCareer(careerId, secrets[careerId]), Ct);

    /// <summary>Imports <paramref name="save"/>; a created career's secret is kept for later calls.</summary>
    public async Task<AppResult<CreatedCareer>> ImportAsync(System.Text.Json.JsonElement save)
    {
        var imported = await ImportHandler().Handle(new ImportCareer(save), Ct);
        if (imported.IsSuccess)
        {
            secrets[imported.Value.CareerId] = imported.Value.Secret;
        }

        return imported;
    }

    /// <summary>Later calls of this kit use <paramref name="secret"/> for the career, as a browser after a recovery would.</summary>
    public void UseSecret(Guid careerId, string secret) => secrets[careerId] = secret;

    public Task<AppResult<ActionOutcome>> SubmitAsync(Guid careerId, Guid commandId, ActionInput input)
        => SubmitHandler().Handle(new SubmitAction(careerId, secrets[careerId], commandId, input), Ct);

    public Task<AppResult<BatchOutcome>> RunBatchAsync(Guid careerId, long expectedVersion)
        => BatchHandler().Handle(new RunBatch(careerId, secrets[careerId], Guid.NewGuid(), new BatchInput(expectedVersion, RunBatchHandler.Weeks)), Ct);

    public async Task<CareerView> GetAsync(Guid careerId)
        => (await GetHandler().Handle(new GetCareer(careerId, secrets[careerId]), Ct)).Value;

    public Task<CareerView> PlayManualWeekAsync(CreatedCareer career) => PlayManualWeekAsync(career.Career);

    /// <summary>
    /// Answers every queued decision, takes <paramref name="action"/> (rest by default) in Fps, resolves every event and
    /// settles; each choice is the first one the career can take.
    /// </summary>
    public async Task<CareerView> PlayManualWeekAsync(CareerView career, MainAction action = MainAction.Rest)
    {
        var view = career;
        while (view.Next.Kind == NextStepKind.ResolveDecision)
        {
            view = await StepAsync(view, ActionType.ResolveDecision, FirstAvailable(view));
        }

        view = (await SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.StartWeek, action, Category.Fps, null, null, null))).Value.Career;
        while (view.Next.Kind == NextStepKind.ResolveEvent)
        {
            view = await StepAsync(view, ActionType.ResolveEvent, FirstAvailable(view));
        }

        return await StepAsync(view, ActionType.SettleWeek, null);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string FirstAvailable(CareerView view) => view.Next.Event!.Choices.First(c => c.UnavailableReason is null).ChoiceId;

    private async Task<CareerView> StepAsync(CareerView view, ActionType type, string? choiceId)
        => (await SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, type, null, null, null, null, choiceId))).Value.Career;
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The content files of one repository directory and their snapshot; <see cref="Switch"/> replaces them.</summary>
public sealed class TestCurrentContent : ICurrentContent
{
    public TestCurrentContent()
        : this(TestPaths.ContentM1)
    {
    }

    public TestCurrentContent(string directory) => Switch(directory);

    public ContentFiles Files { get; private set; } = null!;

    public ContentSnapshot Snapshot { get; private set; } = null!;

    public void Switch(string directory)
    {
        Files = new ContentFiles(
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.BalanceFile)),
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.EventsFile)));
        Snapshot = TestContent.Of(directory);
    }
}
