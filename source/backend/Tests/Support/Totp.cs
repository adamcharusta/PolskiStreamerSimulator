using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>RFC 6238 codes as an authenticator app computes them from the Base32 key Identity hands out.</summary>
public static class Totp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Code(string base32Key, DateTimeOffset at)
    {
        var step = at.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(Decode(base32Key), counter);
        var offset = hash[^1] & 0x0F;
        var value = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7FFFFFFF) % 1_000_000;
        return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Decode(string base32)
    {
        var bits = 0;
        var buffer = 0;
        var bytes = new List<byte>();
        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }

        return [.. bytes];
    }
}
