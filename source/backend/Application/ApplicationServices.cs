using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application;

public static class ApplicationServices
{
    /// <summary>Registers the rules engine and the collaborators of the handlers; Wolverine discovers the handlers.</summary>
    public static IServiceCollection AddGameApplication(this IServiceCollection services)
    {
        services.AddSingleton(new WeekEngine(new RulesetRegistry([new M1Ruleset(), .. M3Ruleset.Versions])));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CareerViewBuilder>();
        services.AddSingleton<CareerAccess>();
        services.AddSingleton<RetentionSweep>();
        services.AddSingleton<LeaderboardPublication>();
        return services;
    }
}
