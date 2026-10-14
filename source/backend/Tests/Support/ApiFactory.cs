using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The API over in-memory stores: no database and no Docker.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Program ends with RunJasperFxCommands; the test host must still start.
    static ApiFactory() => JasperFxEnvironment.AutoStartHost = true;

    public InMemoryCareerStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", "Server=unused;Database=unused");
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.UseSetting("Content:Directory", TestPaths.ContentM1);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICareerStore>();
            services.AddSingleton<ICareerStore>(Store);
            services.RemoveAll<IContentStore>();
            services.AddSingleton<IContentStore>(new InMemoryContentStore(TestContent.Valid()));
        });
    }
}
