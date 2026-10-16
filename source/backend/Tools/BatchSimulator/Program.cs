using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.BatchSimulator;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, BuiltInPolicies.All);

    /// <summary>Exit code 0 on success, 1 when any career violated an invariant, 2 on invalid arguments or content.</summary>
    internal static int Run(string[] args, TextWriter output, IReadOnlyList<IChoicePolicy> availablePolicies)
    {
        var parsed = BatchOptions.Parse(args, availablePolicies);
        if (!parsed.IsSuccess)
        {
            output.WriteLine(parsed.Error.Detail);
            output.WriteLine(BatchOptions.Usage);
            return 2;
        }

        var options = parsed.Value;
        BatchResult result;
        try
        {
            result = BatchRunner.Run(options, availablePolicies);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            output.WriteLine($"Cannot load content from '{options.ContentDirectory}': {e.Message}");
            return 2;
        }

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
