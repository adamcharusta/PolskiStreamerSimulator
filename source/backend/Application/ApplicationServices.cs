using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application;

public static class ApplicationServices
{
    /// <summary>Registers the rules engine and the collaborators of the handlers; Wolverine discovers the handlers.</summary>
    public static IServiceCollection AddGameApplication(this IServiceCollection services)
    {
        services.AddSingleton(new WeekEngine(new RulesetRegistry([new M1Ruleset()])));
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
