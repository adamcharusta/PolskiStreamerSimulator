using System.Text.Json.Serialization;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Serialization;

/// <summary>
/// Source-generated serializer for canonical career JSON: compact, PascalCase properties in declaration order,
/// enums as names, and the custom converters that keep output independent of hash order and floating point.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    UseStringEnumConverter = true,
    Converters = new[]
    {
        typeof(FixedJsonConverter),
        typeof(CareerWeekJsonConverter),
        typeof(FixedCategoryValuesJsonConverter),
        typeof(Int32CategoryValuesJsonConverter),
        typeof(Int64OrdinalDictionaryJsonConverter),
        typeof(Int32OrdinalDictionaryJsonConverter),
        typeof(EquipmentJsonConverter),
        typeof(OrdinalSetJsonConverter),
    })]
[JsonSerializable(typeof(CareerState))]
internal sealed partial class DomainJsonContext : JsonSerializerContext;
