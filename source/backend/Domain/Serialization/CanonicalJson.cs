using System.Text.Json;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Serialization;

/// <summary>Deterministic UTF-8 JSON for career state: equal states always produce identical bytes.</summary>
public static class CanonicalJson
{
    public static byte[] Serialize(CareerState s) => JsonSerializer.SerializeToUtf8Bytes(s, DomainJsonContext.Default.CareerState);

    public static CareerState Deserialize(ReadOnlySpan<byte> utf8)
        => JsonSerializer.Deserialize(utf8, DomainJsonContext.Default.CareerState)
            ?? throw new JsonException("Career state JSON was null.");
}
