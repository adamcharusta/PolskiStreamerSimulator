namespace PolskiStreamerSimulator.Domain.Numerics;

/// <summary>
/// Deterministic fixed-point number: a raw <see cref="long"/> scaled by 1,000,000.
/// Arithmetic is checked; multiplication, division and rounding go half away from zero.
/// </summary>
public readonly record struct Fixed(long Raw) : IComparable<Fixed>
{
    public const long Scale = 1_000_000;

    public static Fixed Zero => new(0);

    public static Fixed One => new(Scale);

    public static Fixed Hundred => new(100 * Scale);

    public static Fixed FromInt(long value) => new(checked(value * Scale));

    public static Fixed FromRatio(long numerator, long denominator)
        => new(DivideRounded((Int128)numerator * Scale, denominator));

    public static Fixed operator +(Fixed a, Fixed b) => new(checked(a.Raw + b.Raw));

    public static Fixed operator -(Fixed a, Fixed b) => new(checked(a.Raw - b.Raw));

    public static Fixed operator -(Fixed a) => new(checked(-a.Raw));

    public static Fixed operator *(Fixed a, Fixed b) => new(DivideRounded((Int128)a.Raw * b.Raw, Scale));

    public static Fixed operator /(Fixed a, Fixed b) => new(DivideRounded((Int128)a.Raw * Scale, b.Raw));

    public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;

    public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;

    public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;

    public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

    public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);

    public Fixed Clamp(Fixed min, Fixed max)
    {
        if (this < min)
        {
            return min;
        }

        return this > max ? max : this;
    }

    public long RoundToLong() => DivideRounded(Raw, Scale);

    public override string ToString()
    {
        var magnitude = Raw < 0 ? (ulong)(-(Raw + 1)) + 1 : (ulong)Raw;
        var sign = Raw < 0 ? "-" : string.Empty;
        return $"{sign}{magnitude / Scale}.{magnitude % Scale:D6}";
    }

    internal static long DivideRounded(Int128 numerator, Int128 denominator)
    {
        var quotient = Int128.DivRem(numerator, denominator);
        var result = quotient.Quotient;
        if (Int128.Abs(quotient.Remainder) * 2 >= Int128.Abs(denominator))
        {
            result += (numerator < 0) == (denominator < 0) ? 1 : -1;
        }

        return checked((long)result);
    }
}
