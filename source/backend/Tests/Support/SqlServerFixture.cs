using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>One SQL Server container for the integration tests; each test class creates its own databases.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2025-RTM-ubuntu-22.04";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    /// <summary>A connection string naming a database that does not exist yet.</summary>
    public string NewDatabase()
        => new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            // Docker Desktop on Windows may forward published ports only over IPv6 loopback; "localhost" reaches both.
            DataSource = $"localhost,{container.GetMappedPublicPort(MsSqlBuilder.MsSqlPort)}",
            InitialCatalog = $"pss_{Guid.NewGuid():N}",
        }.ConnectionString;

    public static DbContextOptions<GameDbContext> Options(string connectionString, params IInterceptor[] interceptors)
        => new DbContextOptionsBuilder<GameDbContext>().UseSqlServer(connectionString).AddInterceptors(interceptors).Options;

    /// <summary>Creates and migrates a new database and returns a context factory for it.</summary>
    public async Task<TestDbContextFactory> MigratedDatabaseAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var factory = new TestDbContextFactory(Options(NewDatabase(), interceptors));
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync(cancellationToken);
        return factory;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

/// <summary>Context factory over fixed options, as <c>AddDbContextFactory</c> registers in the application.</summary>
public sealed class TestDbContextFactory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
{
    public DbContextOptions<GameDbContext> Options => options;

    public GameDbContext CreateDbContext() => new(options);
}
