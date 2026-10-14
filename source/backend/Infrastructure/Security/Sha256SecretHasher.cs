using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.Infrastructure.Security;

/// <summary>32 random bytes as base64url; SHA-256 is enough for 256 bits of randomness, no slow hash needed.</summary>
public sealed class Sha256SecretHasher : ISecretHasher
{
    public string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public bool Matches(string secret, byte[] expectedHash) => CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);
}
