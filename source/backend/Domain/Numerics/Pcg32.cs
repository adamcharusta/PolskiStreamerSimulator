namespace PolskiStreamerSimulator.Domain.Numerics;

/// <summary>
/// Immutable PCG-XSH-RR 64/32 generator. Every draw returns the value and the next generator state,
/// so the state can live inside <c>CareerState</c> and be persisted or replayed.
/// </summary>
public readonly record struct Pcg32(ulong State, ulong Increment)
{
    private const ulong Multiplier = 6364136223846793005;

    /// <summary>Reference PCG seeding (<c>pcg32_srandom_r</c>).</summary>
    public static Pcg32 Create(ulong seed, ulong stream)
    {
        var rng = new Pcg32(0, (stream << 1) | 1);
        rng = rng.Step();
        rng = rng with { State = unchecked(rng.State + seed) };
        return rng.Step();
    }

    public (uint Value, Pcg32 Next) NextUInt()
    {
        var old = State;
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rotation = (int)(old >> 59);
        var value = (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        return (value, Step());
    }

    /// <summary>Uniform integer in [<paramref name="minInclusive"/>, <paramref name="maxInclusive"/>] without modulo bias.</summary>
    public (int Value, Pcg32 Next) NextInt(int minInclusive, int maxInclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minInclusive, maxInclusive);
        var span = (ulong)((long)maxInclusive - minInclusive) + 1;
        var (offset, next) = NextBelow(span);
        return ((int)(minInclusive + (long)offset), next);
    }

    /// <summary>Uniform over the raw values in [<paramref name="minInclusive"/>, <paramref name="maxInclusive"/>].</summary>
    public (Fixed Value, Pcg32 Next) NextFixed(Fixed minInclusive, Fixed maxInclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minInclusive.Raw, maxInclusive.Raw);
        var span = (Int128)maxInclusive.Raw - minInclusive.Raw + 1;
        if (span > ulong.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInclusive), "The range must span at most 2^64 values.");
        }

        var (offset, next) = NextBelow((ulong)span);
        return (new Fixed((long)((Int128)minInclusive.Raw + offset)), next);
    }

    private Pcg32 Step() => this with { State = unchecked((State * Multiplier) + Increment) };

    private (ulong Value, Pcg32 Next) NextBelow(ulong bound)
    {
        if (bound <= uint.MaxValue)
        {
            // pcg32_boundedrand_r: reject the low values that would bias r % bound.
            var bound32 = (uint)bound;
            var threshold = unchecked(0u - bound32) % bound32;
            var rng = this;
            while (true)
            {
                (var r, rng) = rng.NextUInt();
                if (r >= threshold)
                {
                    return (r % bound32, rng);
                }
            }
        }

        var rng64 = this;
        if (bound == (ulong)uint.MaxValue + 1)
        {
            (var r, rng64) = rng64.NextUInt();
            return (r, rng64);
        }

        var threshold64 = unchecked(0UL - bound) % bound;
        while (true)
        {
            (var high, rng64) = rng64.NextUInt();
            (var low, rng64) = rng64.NextUInt();
            var r = ((ulong)high << 32) | low;
            if (r >= threshold64)
            {
                return (r % bound, rng64);
            }
        }
    }
}
