using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class OpenApiDocumentTests
{
    private static readonly string DocumentPath = Path.Combine(TestPaths.RepositoryRoot, "source", "backend", "WebApp", "openapi", "v1.json");

    [Fact]
    public void The_committed_document_describes_the_career_api()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(DocumentPath));
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/v1/careers", out _));
        Assert.True(paths.TryGetProperty("/v1/careers/{id}", out _));
        Assert.True(paths.TryGetProperty("/v1/careers/{id}/actions", out _));
        Assert.Contains("dormRoomNewcomer", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
