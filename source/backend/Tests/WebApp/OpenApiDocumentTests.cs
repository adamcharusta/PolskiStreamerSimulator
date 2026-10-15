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
        Assert.True(paths.TryGetProperty("/v1/careers/{id}/batches", out _));
        Assert.True(paths.TryGetProperty("/v1/career-options", out _));
        Assert.Contains("dormRoomNewcomer", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Numbers_are_plain_json_numbers_for_the_generated_client()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(DocumentPath));
        var careerView = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CareerView").GetProperty("properties");

        Assert.Equal("integer", careerView.GetProperty("version").GetProperty("type").GetString());
        Assert.Equal("integer", careerView.GetProperty("week").GetProperty("type").GetString());
    }

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(DocumentPath));

    [Fact]
    public void Career_operations_require_the_bearer_secret_and_every_operation_lists_a_500()
    {
        using var document = Load();
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("careerSecret").GetProperty("scheme").GetString());
        Assert.True(paths.GetProperty("/v1/careers/{id}").GetProperty("get").GetProperty("security")[0].TryGetProperty("careerSecret", out _));
        Assert.False(paths.GetProperty("/v1/careers").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(paths.TryGetProperty("/health", out _));
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var op = operation.Value;
                Assert.True(op.GetProperty("responses").TryGetProperty("500", out _), $"{operation.Name} {path.Name} has no 500");
                Assert.Equal("careers", op.GetProperty("tags")[0].GetString());
                Assert.DoesNotContain('_', op.GetProperty("summary").GetString() ?? "_");
            }
        }
    }

    [Fact]
    public void Enums_are_strings_without_null_and_problems_carry_a_code()
    {
        using var document = Load();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var ending = schemas.GetProperty("EndingReason");
        Assert.Equal("string", ending.GetProperty("type").GetString());
        Assert.DoesNotContain(ending.GetProperty("enum").EnumerateArray(), v => v.ValueKind == JsonValueKind.Null);
        var problem = schemas.GetProperty("ProblemDetails");
        Assert.True(problem.GetProperty("properties").TryGetProperty("code", out _));
        Assert.True(problem.GetProperty("properties").TryGetProperty("errors", out _));
        Assert.Contains("code", problem.GetProperty("required").EnumerateArray().Select(v => v.GetString()));
    }
}
