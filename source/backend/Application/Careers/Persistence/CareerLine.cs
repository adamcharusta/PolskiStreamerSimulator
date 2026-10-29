namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>
/// The line of play in an append-only log after unranked restores (spec M4-C §4): walking back from the end, each
/// <see cref="ActionKind.Restore"/> jumps to the sequence its slot was saved at, so abandoned entries are skipped.
/// </summary>
public static class CareerLine
{
    /// <summary>The entries of the line that ends at <paramref name="atOrBefore"/>, without restores, in log order.</summary>
    public static IReadOnlyList<StoredAction> Of(IEnumerable<StoredAction> log, int atOrBefore = int.MaxValue)
    {
        var line = new List<StoredAction>();
        var end = atOrBefore;
        foreach (var entry in log.OrderByDescending(a => a.Sequence))
        {
            if (entry.Sequence > end)
            {
                continue;
            }

            if (entry.Kind == ActionKind.Restore)
            {
                end = entry.RestoredFrom ?? 0;
                continue;
            }

            line.Add(entry);
        }

        line.Reverse();
        return line;
    }
}
