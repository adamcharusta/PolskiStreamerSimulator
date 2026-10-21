using System.Globalization;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.BatchSimulator;

/// <summary>Command-line options of the batch simulator.</summary>
public sealed record BatchOptions(
    int Careers,
    ulong Seed,
    IReadOnlyList<string> Policies,
    string ContentDirectory,
    string? OutputDirectory,
    int? MaxDegreeOfParallelism)
{
    public const string Usage =
        "Usage: BatchSimulator [--careers <n>] [--seed <n>] [--policies all|a,b] [--content <dir>] [--out <dir>] [--parallel <n>]";

    /// <summary>
    /// Parses <c>--careers</c> (default 1000), <c>--seed</c> (42), <c>--policies</c> (all of the M1 list here; the program
    /// passes the list of the content's ruleset), <c>--content</c>, <c>--out</c> and <c>--parallel</c>.
    /// </summary>
    public static Result<BatchOptions> Parse(string[] args) => Parse(args, BuiltInPolicies.All);

    internal static Result<BatchOptions> Parse(string[] args, IReadOnlyList<IChoicePolicy> availablePolicies)
    {
        var allNames = availablePolicies.Select(p => p.Name).ToArray();
        var options = new BatchOptions(1000, 42, allNames, DefaultContentDirectory(), null, null);
        for (var i = 0; i < args.Length; i += 2)
        {
            var flag = args[i];
            if (i + 1 >= args.Length)
            {
                return Fail($"Missing value for {flag}.");
            }

            var value = args[i + 1];
            switch (flag)
            {
                case "--careers" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var careers) && careers > 0:
                    options = options with { Careers = careers };
                    break;
                case "--seed" when ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed):
                    options = options with { Seed = seed };
                    break;
                case "--policies":
                    var names = value == "all" ? allNames : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (names.Length == 0 || names.Any(n => !allNames.Contains(n, StringComparer.Ordinal)))
                    {
                        return Fail($"Unknown policy in '{value}'. Available: {string.Join(", ", allNames)}.");
                    }

                    options = options with { Policies = names };
                    break;
                case "--content":
                    options = options with { ContentDirectory = value };
                    break;
                case "--out":
                    options = options with { OutputDirectory = value };
                    break;
                case "--parallel" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parallel) && parallel > 0:
                    options = options with { MaxDegreeOfParallelism = parallel };
                    break;
                case "--careers" or "--seed" or "--parallel":
                    return Fail($"Invalid value '{value}' for {flag}; expected a positive integer.");
                default:
                    return Fail($"Unknown option '{flag}'.");
            }
        }

        return Result<BatchOptions>.Ok(options);
    }

    private static Result<BatchOptions> Fail(string message) => Result<BatchOptions>.Fail(DomainErrorCode.IllegalAction, message);

    /// <summary>The nearest <c>content/m1</c> above the working directory or the executable, else the relative path.</summary>
    private static string DefaultContentDirectory()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "content", "m1");
                if (File.Exists(Path.Combine(candidate, "balance.json")))
                {
                    return candidate;
                }
            }
        }

        return Path.Combine("content", "m1");
    }
}
