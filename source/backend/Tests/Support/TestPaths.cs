namespace PolskiStreamerSimulator.Tests.Support;

public static class TestPaths
{
    /// <summary>Repository root: the nearest ancestor of the test binaries that holds the backend solution.</summary>
    public static string RepositoryRoot { get; } = FindRoot();

    public static string ContentM1 => Path.Combine(RepositoryRoot, "content", "m1");

    public static string ContentM3 => Path.Combine(RepositoryRoot, "content", "m3");

    public static string Golden(string fileName)
        => Path.Combine(RepositoryRoot, "source", "backend", "Tests", "Golden", fileName);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            // Not "content": WebApp copies content/m1 next to its binaries, and so into the test output.
            if (File.Exists(Path.Combine(dir.FullName, "source", "backend", "PolskiStreamerSimulator.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("No ancestor directory holds source/backend/PolskiStreamerSimulator.slnx.");
    }
}
