using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Handlers over in-memory stores, a fixed clock and the repository content.</summary>
public sealed class AppTestKit(WeekEngine? engine = null)
{
    public static readonly DateTimeOffset Start = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public WeekEngine Engine { get; } = engine ?? TestWorld.Engine;

    public InMemoryCareerStore Store { get; } = new();

    public InMemoryContentStore Contents { get; } = new(TestContent.Valid());

    public FixedTimeProvider Time { get; } = new(Start);

    public Sha256SecretHasher Secrets { get; } = new();

    public ICurrentContent Current { get; } = new TestCurrentContent();

    public CareerViewBuilder Views => new(Engine);

    public CareerAccess Access => new(Store, Secrets);

    public CreateCareerHandler CreateHandler() => new(Store, Current, Secrets, Views, Time);

    public GetCareerHandler GetHandler() => new(Access, Store, Contents, Views);

    public SubmitActionHandler SubmitHandler(ICareerStore? store = null)
    {
        var s = store ?? Store;
        return new SubmitActionHandler(new CareerAccess(s, Secrets), s, Contents, Engine, Views, Time);
    }

    public RunBatchHandler BatchHandler(ICareerStore? store = null)
    {
        var s = store ?? Store;
        return new RunBatchHandler(new CareerAccess(s, Secrets), s, Contents, Engine, Views, Time);
    }

    public async Task<CreatedCareer> NewCareerAsync(string nickname = "Streamer", Background background = Background.DormRoomNewcomer)
        => (await CreateHandler().Handle(
            new CreateCareer(nickname, background, Difficulty.Normal, PronounSet.Woman),
            TestContext.Current.CancellationToken)).Value;
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class TestCurrentContent : ICurrentContent
{
    public ContentFiles Files { get; } = new(TestContentFiles.Balance, TestContentFiles.Events);

    public ContentSnapshot Snapshot => TestContent.Valid();
}
