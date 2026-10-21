using System.Collections.Concurrent;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
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

    public AppTestKit(WeekEngine? engine = null)
        : this(engine ?? TestWorld.Engine, TestPaths.ContentM1, null, null)
    {
    }

    private AppTestKit(WeekEngine engine, string contentDirectory, ICareerStore? careers, IContentStore? contents)
    {
        Engine = engine;
        Current = new TestCurrentContent(contentDirectory);
        Careers = careers ?? Store;
        ContentStore = contents ?? Contents;
    }

    public WeekEngine Engine { get; }

    public InMemoryCareerStore Store { get; } = new();

    public InMemoryContentStore Contents { get; } = new(TestContent.Valid());

    /// <summary>The career store the handlers use: <see cref="Store"/> unless another one was given.</summary>
    public ICareerStore Careers { get; }

    /// <summary>The content store the handlers use: <see cref="Contents"/> unless another one was given.</summary>
    public IContentStore ContentStore { get; }

    public FixedTimeProvider Time { get; } = new(Start);

    public Sha256SecretHasher Secrets { get; } = new();

    public TestCurrentContent Current { get; }

    public CareerViewBuilder Views => new(Engine);

    public CareerAccess Access => new(Careers, Secrets);

    /// <summary>A kit with both rulesets whose new careers pin the content in <paramref name="directory"/>.</summary>
    public static AppTestKit WithContent(string directory) => new(M3Play.Engine, directory, null, null);

    /// <summary>A kit with both rulesets over the given stores, for example the SQL Server ones.</summary>
    public static AppTestKit On(ICareerStore careers, IContentStore contents, string directory) => new(M3Play.Engine, directory, careers, contents);

    /// <summary>Later careers pin the content in <paramref name="directory"/>; existing careers keep theirs.</summary>
    public void SwitchCurrentContent(string directory) => Current.Switch(directory);

    public CreateCareerHandler CreateHandler() => new(Careers, Current, Secrets, Views, Time);

    public GetCareerHandler GetHandler() => new(Access, Careers, ContentStore, Views);

    public SubmitActionHandler SubmitHandler(ICareerStore? store = null)
    {
        var s = store ?? Careers;
        return new SubmitActionHandler(new CareerAccess(s, Secrets), s, ContentStore, Engine, Views, Time);
    }

    public RunBatchHandler BatchHandler(ICareerStore? store = null)
    {
        var s = store ?? Careers;
        return new RunBatchHandler(new CareerAccess(s, Secrets), s, ContentStore, Engine, Views, Time);
    }

    public async Task<CreatedCareer> NewCareerAsync(string nickname = "Streamer", Background background = Background.DormRoomNewcomer)
    {
        await ContentStore.EnsureStoredAsync(Current.Files, Ct);
        var created = (await CreateHandler().Handle(new CreateCareer(nickname, background, Difficulty.Normal, PronounSet.Woman), Ct)).Value;
        secrets[created.CareerId] = created.Secret;
        return created;
    }

    public Task<AppResult<ActionOutcome>> SubmitAsync(Guid careerId, Guid commandId, ActionInput input)
        => SubmitHandler().Handle(new SubmitAction(careerId, secrets[careerId], commandId, input), Ct);

    public Task<AppResult<BatchOutcome>> RunBatchAsync(Guid careerId, long expectedVersion)
        => BatchHandler().Handle(new RunBatch(careerId, secrets[careerId], Guid.NewGuid(), new BatchInput(expectedVersion, RunBatchHandler.Weeks)), Ct);

    public async Task<CareerView> GetAsync(Guid careerId)
        => (await GetHandler().Handle(new GetCareer(careerId, secrets[careerId]), Ct)).Value;

    public Task<CareerView> PlayManualWeekAsync(CreatedCareer career) => PlayManualWeekAsync(career.Career);

    /// <summary>
    /// Answers every queued decision, rests in Fps, resolves every event and settles; each choice is the first one
    /// the career can take.
    /// </summary>
    public async Task<CareerView> PlayManualWeekAsync(CareerView career)
    {
        var view = career;
        while (view.Next.Kind == NextStepKind.ResolveDecision)
        {
            view = await StepAsync(view, ActionType.ResolveDecision, FirstAvailable(view));
        }

        view = (await SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null))).Value.Career;
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
