using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the model without the web host; it never opens a connection.</summary>
[ExcludeFromCodeCoverage]
public sealed class GameDbContextDesignTimeFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlServer("Server=localhost;Database=pss_design;TrustServerCertificate=True")
            .Options);
}
