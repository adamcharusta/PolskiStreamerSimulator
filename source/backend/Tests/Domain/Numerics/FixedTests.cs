using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Tests.Domain.Numerics;

public class FixedTests
{
    [Fact]
    public void Multiplication_rounds_half_away_from_zero()
        => Assert.Equal(new Fixed(1), new Fixed(1) * Fixed.FromRatio(1, 2)); // 0.0000005 -> 0.000001

    [Fact]
    public void Negative_multiplication_rounds_half_away_from_zero()
        => Assert.Equal(new Fixed(-1), new Fixed(-1) * Fixed.FromRatio(1, 2));

    [Fact]
    public void Multiplication_keeps_scale()
        => Assert.Equal(Fixed.FromRatio(15, 4), Fixed.FromRatio(3, 2) * Fixed.FromRatio(5, 2));

    [Fact]
    public void Division_rounds_half_away_from_zero()
    {
        Assert.Equal(new Fixed(333_333), Fixed.One / Fixed.FromInt(3));
        Assert.Equal(new Fixed(666_667), Fixed.FromInt(2) / Fixed.FromInt(3));
        Assert.Equal(new Fixed(-666_667), Fixed.FromInt(-2) / Fixed.FromInt(3));
    }

    [Fact]
    public void FromRatio_and_ToString() => Assert.Equal("0.333333", Fixed.FromRatio(1, 3).ToString());

    [Fact]
    public void ToString_formats_sign_and_six_decimals()
    {
        Assert.Equal("12.500000", Fixed.FromRatio(25, 2).ToString());
        Assert.Equal("-0.500000", Fixed.FromRatio(-1, 2).ToString());
        Assert.Equal("0.000000", Fixed.Zero.ToString());
    }

    [Fact]
    public void RoundToLong_half_away()
    {
        Assert.Equal(3, Fixed.FromRatio(5, 2).RoundToLong());
        Assert.Equal(-3, Fixed.FromRatio(-5, 2).RoundToLong());
        Assert.Equal(2, Fixed.FromRatio(249, 100).RoundToLong());
    }

    [Fact]
    public void Overflow_throws() => Assert.Throws<OverflowException>(() => new Fixed(long.MaxValue) + Fixed.One);

    [Fact]
    public void Multiplication_overflow_throws()
        => Assert.Throws<OverflowException>(() => Fixed.FromInt(10_000_000_000) * Fixed.FromInt(10_000_000_000));

    [Fact]
    public void Clamp_limits_to_range()
    {
        Assert.Equal(Fixed.Hundred, Fixed.FromInt(101).Clamp(Fixed.Zero, Fixed.Hundred));
        Assert.Equal(Fixed.Zero, Fixed.FromInt(-3).Clamp(Fixed.Zero, Fixed.Hundred));
        Assert.Equal(Fixed.FromInt(42), Fixed.FromInt(42).Clamp(Fixed.Zero, Fixed.Hundred));
    }

    [Fact]
    public void Comparison_operators_follow_raw_values()
    {
        Assert.True(Fixed.One < Fixed.Hundred);
        Assert.True(Fixed.Hundred > Fixed.One);
        Assert.True(Fixed.One <= Fixed.One);
        Assert.True(Fixed.One >= Fixed.One);
        Assert.Equal(-Fixed.One, Fixed.FromInt(-1));
    }
}
