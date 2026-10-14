using JasperFx;
using PolskiStreamerSimulator.Application;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Infrastructure;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.WebApp;
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(ApplicationServices).Assembly);
    options.UseFluentValidation();
});
builder.Services.AddWolverineHttp();
builder.Services.AddProblemDetails(ApiProblems.Configure);
builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
builder.Services.AddGameApplication();
builder.Services.AddGameInfrastructure(builder.Configuration);
builder.Services.PostConfigure<ContentOptions>(options => options.Directory = Path.GetFullPath(options.Directory, AppContext.BaseDirectory));
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.MapGet("/health", () => "ok");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapWolverineEndpoints(options => options.UseFluentValidationProblemDetailMiddleware());

return await app.RunJasperFxCommands(args);
