namespace PolskiStreamerSimulator.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Store the current content snapshot at startup (and migrate when <see cref="MigrateOnStartup"/>).</summary>
    public bool InitializeOnStartup { get; set; } = true;

    /// <summary>Apply EF Core migrations at startup; on in Development and tests only.</summary>
    public bool MigrateOnStartup { get; set; }
}
