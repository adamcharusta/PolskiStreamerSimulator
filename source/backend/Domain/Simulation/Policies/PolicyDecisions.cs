using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>Why a policy decided: a stable kebab-case code and named values. Clients localize it; Domain holds no copy.</summary>
public sealed record PolicyReason(string Code, ImmutableSortedDictionary<string, string> Args)
{
    public static PolicyReason Of(string code, params (string Name, string Value)[] args)
        => new(code, args.ToImmutableSortedDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    /// <summary>An enum value in the API's camelCase form.</summary>
    public static string Name<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}

public sealed record PlanDecision(WeekPlan Plan, PolicyReason Reason);

public sealed record ChoiceDecision(string ChoiceId, PolicyReason Reason);
