using System.Numerics;

namespace PolskiStreamerSimulator.Domain.Numerics;

/// <summary>Integer-only square root and base-10 logarithm for <see cref="Fixed"/>.</summary>
public static class FixedMath
{
    private const int FractionBits = 40;
    private const int Log2Iterations = 30;
    private const long Log10Of2Scale = 1_000_000_000_000;
    private const long Log10Of2 = 301_029_995_664; // log10(2) at scale 10^12

    /// <summary>Floor of the exact square root at scale 10^6.</summary>
    public static Fixed Sqrt(Fixed x)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x.Raw);
        return new Fixed((long)IntegerSqrt((UInt128)x.Raw * Fixed.Scale));
    }

    /// <summary>Base-10 logarithm, accurate to about 1e-6, for x &gt; 0.</summary>
    public static Fixed Log10(Fixed x)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(x.Raw);

        // log2(raw) = k + log2(m) with raw = m * 2^k and m in [1, 2); m is held at scale 2^40.
        var k = BitOperations.Log2((ulong)x.Raw);
        var one = (UInt128)1 << FractionBits;
        var m = ((UInt128)(ulong)x.Raw << FractionBits) >> k;
        UInt128 fraction = 0;
        for (var i = 1; i <= Log2Iterations; i++)
        {
            m = (m * m) >> FractionBits;
            if (m >= 2 * one)
            {
                m >>= 1;
                fraction |= (UInt128)1 << (FractionBits - i);
            }
        }

        var log2Raw = ((Int128)k << FractionBits) + (Int128)fraction;

        // The base-10 logarithm of x is log2 of the raw value times log10 of 2, less 6 for the 10^6 scale.
        var log10Raw = Fixed.DivideRounded(log2Raw * Log10Of2, (Int128)(Log10Of2Scale / Fixed.Scale) << FractionBits);
        return new Fixed(log10Raw - (6 * Fixed.Scale));
    }

    public static Fixed Min(Fixed a, Fixed b) => a <= b ? a : b;

    public static Fixed Max(Fixed a, Fixed b) => a >= b ? a : b;

    private static UInt128 IntegerSqrt(UInt128 n)
    {
        if (n == 0)
        {
            return 0;
        }

        var x = (UInt128)1 << ((int)(UInt128.Log2(n) / 2) + 1);
        while (true)
        {
            var y = (x + (n / x)) / 2;
            if (y >= x)
            {
                return x;
            }

            x = y;
        }
    }
}
