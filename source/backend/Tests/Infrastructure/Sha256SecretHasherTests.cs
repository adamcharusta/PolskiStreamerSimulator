using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.Tests.Infrastructure;

public sealed class Sha256SecretHasherTests
{
    private readonly Sha256SecretHasher hasher = new();

    [Fact]
    public void A_new_secret_is_43_base64url_characters_and_unique()
    {
        var first = hasher.NewSecret();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
        Assert.NotEqual(first, hasher.NewSecret());
    }

    [Fact]
    public void Only_the_hashed_secret_matches()
    {
        var secret = hasher.NewSecret();
        var hash = hasher.Hash(secret);

        Assert.Equal(32, hash.Length);
        Assert.True(hasher.Matches(secret, hash));
        Assert.False(hasher.Matches(hasher.NewSecret(), hash));
    }
}
