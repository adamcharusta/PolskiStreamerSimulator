namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// Validates the ICU MessageFormat subset used by event text: literal text with <c>''</c> escapes and
/// <c>'…'</c> quoting, and <c>{gender, select, female {…} male {…} other {…}}</c> whose branches may nest the
/// same subset. Every other argument form is rejected so the browser's <c>intl-messageformat</c> renders exactly
/// what was validated.
/// </summary>
public static class IcuMessage
{
    private static readonly string[] Branches = ["female", "male", "other"];

    public static string? Validate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "empty";
        }

        var position = 0;
        var problem = ParseMessage(message, ref position, nested: false);
        return problem ?? (position == message.Length ? null : "unbalanced");
    }

    /// <summary>Reads literal text and arguments until the end, or until a closing brace when nested.</summary>
    private static string? ParseMessage(string m, ref int i, bool nested)
    {
        while (i < m.Length)
        {
            var ch = m[i];
            if (ch == '}')
            {
                return nested ? null : "unbalanced";
            }

            var problem = ch switch
            {
                '\'' => SkipQuote(m, ref i),
                '{' => ParseArgument(m, ref i),
                _ => Advance(ref i),
            };
            if (problem is not null)
            {
                return problem;
            }
        }

        return nested ? "unbalanced" : null;
    }

    private static string? Advance(ref int i)
    {
        i++;
        return null;
    }

    /// <summary>
    /// ICU quoting: <c>''</c> is one apostrophe; an apostrophe before a brace starts a quoted literal that ends at the
    /// next single apostrophe; any other apostrophe is literal.
    /// </summary>
    private static string? SkipQuote(string m, ref int i)
    {
        if (i + 1 < m.Length && m[i + 1] == '\'')
        {
            i += 2;
            return null;
        }

        if (i + 1 < m.Length && (m[i + 1] == '{' || m[i + 1] == '}'))
        {
            var end = m.IndexOf('\'', i + 1);
            if (end < 0)
            {
                return "unterminated-quote";
            }

            i = end + 1;
            return null;
        }

        i++;
        return null;
    }

    private static string? ParseArgument(string m, ref int i)
    {
        var close = m.IndexOfAny([',', '}'], i + 1);
        if (close < 0)
        {
            return "unbalanced";
        }

        var name = m[(i + 1)..close].Trim();
        if (m[close] == '}')
        {
            // A simple {name} argument; the content uses none, but ICU allows it.
            i = close + 1;
            return name.Length == 0 ? "unsupported-argument" : null;
        }

        var typeEnd = m.IndexOf(',', close + 1);
        if (typeEnd < 0 || m[(close + 1)..typeEnd].Trim() != "select")
        {
            return "unsupported-argument";
        }

        if (name != "gender")
        {
            return "select-not-gender";
        }

        i = typeEnd + 1;
        return ParseBranches(m, ref i);
    }

    private static string? ParseBranches(string m, ref int i)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            SkipWhitespace(m, ref i);
            if (i >= m.Length)
            {
                return "unbalanced";
            }

            if (m[i] == '}')
            {
                i++;
                break;
            }

            if (ParseBranch(m, ref i, seen) is { } problem)
            {
                return problem;
            }
        }

        return Branches.FirstOrDefault(b => !seen.Contains(b)) is { } missing ? $"missing-branch:{missing}" : null;
    }

    private static string? ParseBranch(string m, ref int i, HashSet<string> seen)
    {
        var keyStart = i;
        while (i < m.Length && !char.IsWhiteSpace(m[i]) && m[i] != '{' && m[i] != '}')
        {
            i++;
        }

        var key = m[keyStart..i];
        if (!Branches.Contains(key))
        {
            return $"unknown-branch:{key}";
        }

        seen.Add(key);
        SkipWhitespace(m, ref i);
        if (i >= m.Length || m[i] != '{')
        {
            return "unbalanced";
        }

        i++;
        var problem = ParseMessage(m, ref i, nested: true);
        i++; // the branch's closing brace
        return problem;
    }

    private static void SkipWhitespace(string m, ref int i)
    {
        while (i < m.Length && char.IsWhiteSpace(m[i]))
        {
            i++;
        }
    }
}
