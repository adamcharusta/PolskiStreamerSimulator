using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class RecoveryKeysTests
{
    private const string Random = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6";

    [Fact]
    public void A_formatted_key_parses_back_to_its_career()
    {
        var id = Guid.NewGuid();
        var key = RecoveryKeys.Format(id, Random);

        Assert.True(RecoveryKeys.TryParse(key, out var parsed, out var canonical));
        Assert.Equal(id, parsed);
        Assert.Equal(key, canonical);
        Assert.StartsWith($"pss1.{id:N}.", key, StringComparison.Ordinal);
    }

    [Fact]
    public void Whitespace_and_upper_case_hex_read_as_the_canonical_key()
    {
        var id = Guid.NewGuid();
        var key = RecoveryKeys.Format(id, Random);

        Assert.True(RecoveryKeys.TryParse($"  pss1.{id.ToString("N").ToUpperInvariant()}.{Random}\n", out _, out var canonical));
        Assert.Equal(key, canonical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pss2.0123456789abcdef0123456789abcdef.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6")]
    [InlineData("pss1.0123456789abcdef0123456789abcdeg.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6")]
    [InlineData("pss1.0123456789abcdef0123456789abcde.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6")]
    [InlineData("pss1.0123456789abcdef0123456789abcdef.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_")]
    [InlineData("pss1.0123456789abcdef0123456789abcdef.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_=")]
    [InlineData("pss1.0123456789abcdef0123456789abcdef.abcdefghijklmnopqrstuvwxyz ABCDEFGHIJ0123-_")]
    [InlineData("pss1.0123456789abcdef0123456789abcdef.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6.x")]
    public void A_malformed_key_does_not_parse(string? key)
    {
        Assert.False(RecoveryKeys.TryParse(key, out var careerId, out var canonical));
        Assert.Equal(Guid.Empty, careerId);
        Assert.Equal("", canonical);
    }
}
