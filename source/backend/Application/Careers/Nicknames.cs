namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Character nickname rule (TD-92); not the 1–5-character leaderboard nickname.</summary>
public static class Nicknames
{
    public const int MinLength = 3;
    public const int MaxLength = 20;
    public const string Rule = "Nickname must have 3-20 characters: letters, digits, spaces, '_', '-' or '.'.";

    public static string Normalize(string nickname) => nickname.Trim();

    public static bool IsValid(string? nickname)
    {
        if (nickname is null)
        {
            return false;
        }

        var trimmed = nickname.Trim();
        return trimmed.Length is >= MinLength and <= MaxLength
            && trimmed.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.');
    }
}
