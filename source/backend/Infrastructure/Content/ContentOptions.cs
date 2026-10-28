namespace PolskiStreamerSimulator.Infrastructure.Content;

public sealed class ContentOptions
{
    public const string Section = "Content";

    /// <summary>
    /// Directory with <c>balance.json</c> and <c>events.json</c> for new careers; existing careers load the snapshot
    /// they were created with from the content store.
    /// </summary>
    public string Directory { get; set; } = "content/m3";
}
