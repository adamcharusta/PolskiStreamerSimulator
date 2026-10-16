namespace PolskiStreamerSimulator.Infrastructure.Content;

public sealed class ContentOptions
{
    public const string Section = "Content";

    /// <summary>Directory with <c>balance.json</c> and <c>events.json</c> for new careers.</summary>
    public string Directory { get; set; } = "content/m1";
}
