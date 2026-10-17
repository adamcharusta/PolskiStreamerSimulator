using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.BatchSimulator;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, null);

    /// <summary>
    /// Exit code 0 on success, 1 when any career violated an invariant, 2 on invalid arguments or content. Without
    /// <paramref name="availablePolicies"/> the policies are those of the content's ruleset (<see cref="BatchRunner.PoliciesFor"/>).
    /// </summary>
    internal static int Run(string[] args, TextWriter output, IReadOnlyList<IChoicePolicy>? availablePolicies)
    {
        var contentDirectory = BatchOptions.Parse(args, availablePolicies ?? BatchRunner.KnownPolicies) is { IsSuccess: true } probe ? probe.Value.ContentDirectory : null;
        try
        {
            availablePolicies ??= contentDirectory is null
                ? BatchRunner.KnownPolicies
                : BatchRunner.PoliciesFor(new JsonContentSource(contentDirectory).Load().RulesetId);
            return Run(output, availablePolicies, BatchOptions.Parse(args, availablePolicies));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            output.WriteLine($"Cannot load content from '{contentDirectory}': {e.Message}");
            return 2;
        }
    }

    private static int Run(TextWriter output, IReadOnlyList<IChoicePolicy> availablePolicies, Result<BatchOptions> parsed)
    {
        if (!parsed.IsSuccess)
        {
            output.WriteLine(parsed.Error.Detail);
            output.WriteLine(BatchOptions.Usage);
            return 2;
        }

        var options = parsed.Value;
        var result = BatchRunner.Run(options, availablePolicies);
        var markdown = ReportWriter.Markdown(result, options);
        output.Write(markdown);
        if (options.OutputDirectory is { } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "report.md"), markdown);
            File.WriteAllText(Path.Combine(directory, "report.json"), ReportWriter.Json(result, options));
        }

        return result.Careers.Any(c => c.Violations.Count > 0) ? 1 : 0;
    }
}
