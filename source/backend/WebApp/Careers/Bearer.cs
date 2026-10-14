namespace PolskiStreamerSimulator.WebApp.Careers;

internal static class Bearer
{
    private const string Scheme = "Bearer ";

    /// <summary>The career secret from an <c>Authorization: Bearer</c> header, or <c>null</c>.</summary>
    public static string? Secret(string? authorization)
        => authorization is not null && authorization.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? authorization[Scheme.Length..].Trim()
            : null;
}
