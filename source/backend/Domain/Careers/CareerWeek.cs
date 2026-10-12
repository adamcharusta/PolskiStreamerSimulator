namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>Week of a 20-year career, 1 to 1,040.</summary>
public readonly record struct CareerWeek
{
    public const int First = 1;
    public const int Last = 1040;

    public CareerWeek(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, First);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, Last);
        Value = value;
    }

    public int Value { get; }

    public bool IsLast => Value == Last;

    public CareerWeek Next() => new(Value + 1);
}
