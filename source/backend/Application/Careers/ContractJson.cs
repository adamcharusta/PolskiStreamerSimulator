using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>The one JSON shape of the contract, for HTTP bodies and for responses stored in the command log.</summary>
public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>
    /// Enums only as camelCase names, numbers only as JSON numbers, and every constructor parameter without a default
    /// required, so a typo or an omitted field is a 400 instead of a silently defaulted value.
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.NumberHandling = JsonNumberHandling.Strict;
        options.RespectRequiredConstructorParameters = true;
    }

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
