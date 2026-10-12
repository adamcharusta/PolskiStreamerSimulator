using FsCheck;
using FsCheck.Xunit;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Tests.Domain.Numerics;

public class FixedMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 2_000_000)]
    [InlineData(2, 1_414_213)]
    [InlineData(1_000_000, 1_000_000_000)]
    public void Sqrt_floor(long input, long expectedRaw) => Assert.Equal(expectedRaw, FixedMath.Sqrt(Fixed.FromInt(input)).Raw);

    [Fact]
    public void Sqrt_of_fraction_and_of_huge_values()
    {
        Assert.Equal(500_000, FixedMath.Sqrt(Fixed.FromRatio(1, 4)).Raw);
        Assert.Equal(7_071_067_811, FixedMath.Sqrt(Fixed.FromInt(50_000_000)).Raw);
    }

    [Fact]
    public void Sqrt_of_negative_throws() => Assert.Throws<ArgumentOutOfRangeException>(() => FixedMath.Sqrt(Fixed.FromInt(-1)));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(10, 1_000_000)]
    [InlineData(2, 301_030)]
    [InlineData(1_000_001, 6_000_000)]
    [InlineData(10_000_001, 7_000_000)]
    public void Log10_within_1e5(long input, long expectedRaw)
        => Assert.InRange(FixedMath.Log10(Fixed.FromInt(input)).Raw, expectedRaw - 10, expectedRaw + 10);

    [Fact]
    public void Log10_of_fraction_is_negative()
        => Assert.InRange(FixedMath.Log10(Fixed.FromRatio(1, 100)).Raw, -2_000_010, -1_999_990);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Log10_of_non_positive_throws(long raw)
        => Assert.Throws<ArgumentOutOfRangeException>(() => FixedMath.Log10(new Fixed(raw)));

    [Property]
    public bool Log10_is_monotonic(PositiveInt a, PositiveInt b)
        => a.Get <= b.Get == (FixedMath.Log10(Fixed.FromInt(a.Get)) <= FixedMath.Log10(Fixed.FromInt(b.Get))) || a.Get > b.Get;

    [Fact]
    public void Min_and_max()
    {
        Assert.Equal(Fixed.One, FixedMath.Min(Fixed.One, Fixed.Hundred));
        Assert.Equal(Fixed.Hundred, FixedMath.Max(Fixed.One, Fixed.Hundred));
    }
}
