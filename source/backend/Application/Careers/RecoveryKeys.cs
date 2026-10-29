namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// The recovery key <c>pss1.&lt;career ID as 32 hex digits&gt;.&lt;43 base64url characters&gt;</c> (spec M4-A §3). The
/// career ID lets the server find the career; only the random part proves possession.
/// </summary>
public static class RecoveryKeys
{
    private const string Prefix = "pss1";
    private const int RandomLength = 43;

    /// <summary>The key of <paramref name="careerId"/> with the random part <paramref name="random"/>, a fresh secret.</summary>
    public static string Format(Guid careerId, string random) => $"{Prefix}.{careerId:N}.{random}";

    /// <summary>
    /// Reads a pasted key into its career ID and its canonical form, the key as <see cref="Format"/> writes it, whose
    /// hash is what the server stores; surrounding whitespace and upper-case hex digits are accepted.
    /// </summary>
    public static bool TryParse(string? key, out Guid careerId, out string canonical)
    {
        careerId = Guid.Empty;
        canonical = "";
        var parts = (key ?? "").Trim().Split('.');
        if (parts is not [Prefix, var id, var random]
            || id.Length != 32
            || !Guid.TryParseExact(id, "N", out careerId)
            || random.Length != RandomLength
            || !random.All(IsBase64UrlChar))
        {
            careerId = Guid.Empty;
            return false;
        }

        canonical = Format(careerId, random);
        return true;
    }

    private static bool IsBase64UrlChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';
}
