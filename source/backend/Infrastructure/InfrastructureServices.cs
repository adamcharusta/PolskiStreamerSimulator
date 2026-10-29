using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddGameInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily: commands such as `openapi` build the host without a database.
        services.AddDbContextFactory<GameDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Game")
            ?? throw new InvalidOperationException("Connection string 'Game' is not set; set ConnectionStrings__Game.")));
        services.AddOptions<ContentOptions>().Bind(configuration.GetSection(ContentOptions.Section));
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.Section));
        services.AddSingleton<ICareerStore, EfCareerStore>();
        services.AddSingleton<IContentStore, EfContentStore>();
        services.AddSingleton<ISaveSlotStore, EfSaveSlotStore>();
        services.AddSingleton<ISeasonStore, EfSeasonStore>();
        services.AddSingleton<ILeaderboardStore, EfLeaderboardStore>();
        services.AddSingleton<ICurrentContent, DirectoryCurrentContent>();
        services.AddSingleton<ISecretHasher, Sha256SecretHasher>();
        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection(RetentionOptions.Section));
        services.AddHostedService<DatabaseInitializer>();

        // After the initializer, which migrates the schema the sweep deletes from.
        services.AddHostedService<RetentionSweeper>();
        return services;
    }
}
