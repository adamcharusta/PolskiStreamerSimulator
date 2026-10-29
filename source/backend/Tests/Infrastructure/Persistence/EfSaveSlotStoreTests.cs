using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Application.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfSaveSlotStoreTests(SqlServerFixture sql) : SaveStoreContract
{
    protected override async Task<AppTestKit> CreateKitAsync()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        return AppTestKit.On(new EfCareerStore(factory), new EfContentStore(factory, TimeProvider.System), new EfSaveSlotStore(factory), TestPaths.ContentM1);
    }
}
