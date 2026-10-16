using System.Security.Cryptography;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Serialization;

public static class StateHash
{
    /// <summary>Lowercase hex SHA-256 of the canonical JSON bytes.</summary>
    public static string Of(CareerState s) => Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Serialize(s)));
}
