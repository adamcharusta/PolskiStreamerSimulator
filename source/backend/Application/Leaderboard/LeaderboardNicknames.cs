using System.Globalization;
using System.Text;

namespace PolskiStreamerSimulator.Application.Leaderboard;

/// <summary>The 1–5-character arcade nickname of a leaderboard entry (spec M4-B §5); not the career nickname.</summary>
public static class LeaderboardNicknames
{
    public const int MaxLength = 5;
    public const string Rule = "Leaderboard nickname must have 1-5 characters: A-Z, Polish capital letters or digits.";

    private const string PolishCapitals = "ĄĆĘŁŃÓŚŹŻ";

    /// <summary>NFC, trimmed and uppercased; the stored and shown form.</summary>
    public static string Normalize(string nickname)
        => nickname.Normalize(NormalizationForm.FormC).Trim().ToUpper(CultureInfo.InvariantCulture);

    public static bool IsValid(string? nickname)
    {
        if (nickname is null)
        {
            return false;
        }

        var normalized = Normalize(nickname);
        return normalized.Length is >= 1 and <= MaxLength
            && normalized.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') || PolishCapitals.Contains(c));
    }
}
