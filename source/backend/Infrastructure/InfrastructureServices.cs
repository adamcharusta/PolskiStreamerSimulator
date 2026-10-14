using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Infrastructure.Persistence;

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
        services.AddSingleton<ICurrentContent, DirectoryCurrentContent>();
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
