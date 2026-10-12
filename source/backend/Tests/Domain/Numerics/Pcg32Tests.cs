using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Tests.Domain.Numerics;

public class Pcg32Tests
{
    [Fact]
    public void Matches_pcg32_reference_vector()
    {
        // pcg32-demo: seed 42, stream 54
        var rng = Pcg32.Create(42, 54);
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
        foreach (var e in expected)
        {
            (var v, rng) = rng.NextUInt();
            Assert.Equal(e, v);
        }
    }

    [Fact]
    public void NextInt_stays_in_range_and_hits_both_ends()
    {
        var rng = Pcg32.Create(1, 1);
        var seen = new HashSet<int>();
        for (var i = 0; i < 10_000; i++)
        {
            (var v, rng) = rng.NextInt(1, 100);
            Assert.InRange(v, 1, 100);
            seen.Add(v);
        }

        Assert.Equal(100, seen.Count);
    }

    [Fact]
    public void NextInt_with_equal_bounds_returns_the_bound()
    {
        var (v, _) = Pcg32.Create(3, 3).NextInt(7, 7);
        Assert.Equal(7, v);
    }

    [Fact]
    public void NextInt_rejects_inverted_bounds()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.Create(1, 1).NextInt(5, 4));

    [Fact]
    public void Same_seed_same_sequence()
    {
        var a = Pcg32.Create(123, 9);
        var b = Pcg32.Create(123, 9);
        for (var i = 0; i < 1_000; i++)
        {
            (var x, a) = a.NextInt(int.MinValue, int.MaxValue);
            (var y, b) = b.NextInt(int.MinValue, int.MaxValue);
            Assert.Equal(x, y);
        }

        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_streams_give_different_sequences()
    {
        var (a, _) = Pcg32.Create(42, 1).NextUInt();
        var (b, _) = Pcg32.Create(42, 2).NextUInt();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Drawing_does_not_mutate_the_original_generator()
    {
        var rng = Pcg32.Create(5, 5);
        var (first, _) = rng.NextUInt();
        var (again, _) = rng.NextUInt();
        Assert.Equal(first, again);
    }

    [Fact]
    public void NextFixed_stays_in_range_and_hits_both_ends()
    {
        var min = new Fixed(850_000);
        var max = new Fixed(850_003);
        var rng = Pcg32.Create(8, 8);
        var seen = new HashSet<long>();
        for (var i = 0; i < 1_000; i++)
        {
            (var v, rng) = rng.NextFixed(min, max);
            Assert.InRange(v.Raw, min.Raw, max.Raw);
            seen.Add(v.Raw);
        }

        Assert.Equal(4, seen.Count);
    }

    [Fact]
    public void NextFixed_supports_spans_wider_than_32_bits()
    {
        var min = Fixed.FromInt(-1_000_000);
        var max = Fixed.FromInt(1_000_000);
        var rng = Pcg32.Create(11, 11);
        for (var i = 0; i < 1_000; i++)
        {
            (var v, rng) = rng.NextFixed(min, max);
            Assert.InRange(v.Raw, min.Raw, max.Raw);
        }
    }
}
