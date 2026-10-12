using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Careers;

/// <summary>One value per <see cref="Category"/>, indexed by the enum; equality compares the sequence.</summary>
public sealed record CategoryValues<T>
{
    public static readonly int Count = Enum.GetValues<Category>().Length;

    public CategoryValues(ImmutableArray<T> values)
    {
        if (values.IsDefault || values.Length != Count)
        {
            throw new ArgumentException($"Expected {Count} category values.", nameof(values));
        }

        Values = values;
    }

    public ImmutableArray<T> Values { get; }

    public T this[Category category] => Values[(int)category];

    public static CategoryValues<T> Uniform(T value) => new([.. Enumerable.Repeat(value, Count)]);

    public CategoryValues<T> With(Category category, T value) => new(Values.SetItem((int)category, value));

    public bool Equals(CategoryValues<T>? other) => other is not null && Values.SequenceEqual(other.Values);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
