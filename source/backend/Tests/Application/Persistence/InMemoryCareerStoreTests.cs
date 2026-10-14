using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Persistence;

public sealed class InMemoryCareerStoreTests : CareerStoreContract
{
    protected override Task<ICareerStore> CreateStoreAsync() => Task.FromResult<ICareerStore>(new InMemoryCareerStore());
}
