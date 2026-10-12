using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Serialization;

/// <summary>Writes <see cref="Fixed"/> as its raw integer so no floating-point literal appears.</summary>
internal sealed class FixedJsonConverter : JsonConverter<Fixed>
{
    public override Fixed Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, Fixed value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Raw);
}

internal sealed class CareerWeekJsonConverter : JsonConverter<CareerWeek>
{
    public override CareerWeek Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, CareerWeek value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}

/// <summary>Writes category values as a plain array in <see cref="Category"/> order.</summary>
internal abstract class CategoryValuesJsonConverter<T> : JsonConverter<CategoryValues<T>>
{
    public override CategoryValues<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Expect(ref reader, JsonTokenType.StartArray);
        var values = ImmutableArray.CreateBuilder<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            values.Add(ReadElement(ref reader));
        }

        return new CategoryValues<T>(values.ToImmutable());
    }

    public override void Write(Utf8JsonWriter writer, CategoryValues<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var element in value.Values)
        {
            WriteElement(writer, element);
        }

        writer.WriteEndArray();
    }

    protected abstract T ReadElement(ref Utf8JsonReader reader);

    protected abstract void WriteElement(Utf8JsonWriter writer, T value);

    internal static void Expect(ref Utf8JsonReader reader, JsonTokenType token)
    {
        if (reader.TokenType != token)
        {
            throw new JsonException($"Expected {token} but found {reader.TokenType}.");
        }
    }
}

internal sealed class FixedCategoryValuesJsonConverter : CategoryValuesJsonConverter<Fixed>
{
    protected override Fixed ReadElement(ref Utf8JsonReader reader) => new(reader.GetInt64());

    protected override void WriteElement(Utf8JsonWriter writer, Fixed value) => writer.WriteNumberValue(value.Raw);
}

internal sealed class Int32CategoryValuesJsonConverter : CategoryValuesJsonConverter<int>
{
    protected override int ReadElement(ref Utf8JsonReader reader) => reader.GetInt32();

    protected override void WriteElement(Utf8JsonWriter writer, int value) => writer.WriteNumberValue(value);
}

/// <summary>Writes string-keyed dictionaries with keys in ordinal order, independent of hash order.</summary>
internal abstract class OrdinalDictionaryJsonConverter<TValue> : JsonConverter<ImmutableDictionary<string, TValue>>
{
    public override ImmutableDictionary<string, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        CategoryValuesJsonConverter<int>.Expect(ref reader, JsonTokenType.StartObject);
        var builder = ImmutableDictionary.CreateBuilder<string, TValue>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            builder.Add(key, ReadValue(ref reader));
        }

        return builder.ToImmutable();
    }

    public override void Write(Utf8JsonWriter writer, ImmutableDictionary<string, TValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(pair.Key);
            WriteValue(writer, pair.Value);
        }

        writer.WriteEndObject();
    }

    protected abstract TValue ReadValue(ref Utf8JsonReader reader);

    protected abstract void WriteValue(Utf8JsonWriter writer, TValue value);
}

internal sealed class Int64OrdinalDictionaryJsonConverter : OrdinalDictionaryJsonConverter<long>
{
    protected override long ReadValue(ref Utf8JsonReader reader) => reader.GetInt64();

    protected override void WriteValue(Utf8JsonWriter writer, long value) => writer.WriteNumberValue(value);
}

internal sealed class Int32OrdinalDictionaryJsonConverter : OrdinalDictionaryJsonConverter<int>
{
    protected override int ReadValue(ref Utf8JsonReader reader) => reader.GetInt32();

    protected override void WriteValue(Utf8JsonWriter writer, int value) => writer.WriteNumberValue(value);
}

/// <summary>Writes equipment tiers keyed by slot name in <see cref="EquipmentSlot"/> order.</summary>
internal sealed class EquipmentJsonConverter : JsonConverter<ImmutableDictionary<EquipmentSlot, int>>
{
    public override ImmutableDictionary<EquipmentSlot, int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        CategoryValuesJsonConverter<int>.Expect(ref reader, JsonTokenType.StartObject);
        var builder = ImmutableDictionary.CreateBuilder<EquipmentSlot, int>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var slot = Enum.Parse<EquipmentSlot>(reader.GetString()!);
            reader.Read();
            builder.Add(slot, reader.GetInt32());
        }

        return builder.ToImmutable();
    }

    public override void Write(Utf8JsonWriter writer, ImmutableDictionary<EquipmentSlot, int> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value.OrderBy(p => p.Key))
        {
            writer.WriteNumber(pair.Key.ToString(), pair.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>Writes a string set as an ordinally sorted array.</summary>
internal sealed class OrdinalSetJsonConverter : JsonConverter<ImmutableHashSet<string>>
{
    public override ImmutableHashSet<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        CategoryValuesJsonConverter<int>.Expect(ref reader, JsonTokenType.StartArray);
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            builder.Add(reader.GetString()!);
        }

        return builder.ToImmutable();
    }

    public override void Write(Utf8JsonWriter writer, ImmutableHashSet<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
