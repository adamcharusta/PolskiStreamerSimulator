namespace PolskiStreamerSimulator.Tests.Support;

public static class TestPaths
{
    /// <summary>Repository root: the nearest ancestor of the test binaries that contains <c>content</c>.</summary>
    public static string RepositoryRoot { get; } = FindRoot();

    public static string ContentM1 => Path.Combine(RepositoryRoot, "content", "m1");

    public static string Golden(string fileName)
        => Path.Combine(RepositoryRoot, "source", "backend", "Tests", "Golden", fileName);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "content")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("No ancestor directory contains 'content'.");
    }
}
