using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Application.Careers;

public static class Decimals
{
    /// <summary>Exact conversion: a <see cref="Fixed"/> has six decimal places, which <see cref="decimal"/> holds exactly.</summary>
    public static decimal Of(Fixed value) => (decimal)value.Raw / Fixed.Scale;
}
