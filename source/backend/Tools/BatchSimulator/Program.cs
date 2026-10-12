namespace PolskiStreamerSimulator.BatchSimulator;

public static class Program
{
    public static int Main(string[] args)
    {
        _ = args;
        Console.WriteLine("Usage: BatchSimulator --careers <n> --seed <n> [--policies all|a,b] [--content <dir>] [--out <dir>] [--parallel <n>]");
        return 0;
    }
}
