namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Creates career secrets and checks them against stored hashes.</summary>
public interface ISecretHasher
{
    string NewSecret();

    byte[] Hash(string secret);

    /// <summary>Compares in constant time.</summary>
    bool Matches(string secret, byte[] expectedHash);
}
