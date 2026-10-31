using System.Text.Json;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary><see cref="IContentCompiler"/> over <see cref="JsonContentSource"/>, the parser the server loads content with.</summary>
public sealed class JsonContentCompiler : IContentCompiler
{
    private const string ViolationsPrefix = "is invalid: ";

    public (ContentSnapshot? Snapshot, IReadOnlyList<string> Problems) Compile(ContentFiles files)
    {
        try
        {
            return (JsonContentSource.Parse(files.Balance, files.Events, "draft"), []);
        }
        catch (InvalidDataException error)
        {
            // The validator's violations are joined after the prefix; a parser error is one message.
            var at = error.Message.IndexOf(ViolationsPrefix, StringComparison.Ordinal);
            return at < 0
                ? (null, [error.Message])
                : (null, [.. error.Message[(at + ViolationsPrefix.Length)..].Split(", ", StringSplitOptions.RemoveEmptyEntries)]);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException or InvalidOperationException or OverflowException or ArgumentException)
        {
            return (null, [$"{error.GetType().Name}: {error.Message}"]);
        }
    }
}
