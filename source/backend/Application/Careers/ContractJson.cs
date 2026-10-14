using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>The one JSON shape of the contract, for HTTP bodies and for responses stored in the command log.</summary>
public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions options)
        => options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"Stored {typeof(T).Name} JSON was null.");

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
