using PolskiStreamerSimulator.Application.Leaderboard;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Thread-safe <see cref="ISeasonStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemorySeasonStore : ISeasonStore
{
    private readonly Lock gate = new();
    private readonly List<Season> seasons = [];
    private readonly List<(int Season, DateTimeOffset Deadline, string Reason, string Actor, DateTimeOffset At)> extensions = [];

    public IReadOnlyList<(int Season, DateTimeOffset Deadline, string Reason, string Actor, DateTimeOffset At)> Extensions
    {
        get
        {
            lock (gate)
            {
                return [.. extensions];
            }
        }
    }

    public Task<Season> EnsureCurrentAsync(string rulesetId, string contentDigest, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var open = seasons.FindIndex(s => s.ClosedAt is null);
            if (open >= 0 && seasons[open].Pins(rulesetId, contentDigest))
            {
                return Task.FromResult(WithExtension(seasons[open]));
            }

            if (open >= 0)
            {
                seasons[open] = seasons[open] with { ClosedAt = now };
            }

            var next = new Season(seasons.Count + 1, rulesetId, contentDigest, now, null);
            seasons.Add(next);
            return Task.FromResult(next);
        }
    }

    public Task<Season?> CurrentAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(seasons.Where(s => s.ClosedAt is null).Select(WithExtension).SingleOrDefault());
        }
    }

    public Task<Season?> FindAsync(int number, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(seasons.Where(s => s.Number == number).Select(WithExtension).SingleOrDefault());
        }
    }

    public Task<IReadOnlyList<Season>> ListAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<Season>>([.. seasons.Select(WithExtension).OrderByDescending(s => s.Number)]);
        }
    }

    public Task AddExtensionAsync(int number, DateTimeOffset deadline, string reason, string actor, DateTimeOffset at, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            extensions.Add((number, deadline, reason, actor, at));
        }

        return Task.CompletedTask;
    }

    /// <summary>Closes the open season at <paramref name="at"/> without opening another, as a test shortcut.</summary>
    public void CloseCurrent(DateTimeOffset at)
    {
        lock (gate)
        {
            var open = seasons.FindIndex(s => s.ClosedAt is null);
            seasons[open] = seasons[open] with { ClosedAt = at };
        }
    }

    private Season WithExtension(Season s)
    {
        var mine = extensions.Where(x => x.Season == s.Number).Select(x => (DateTimeOffset?)x.Deadline).Max();
        return s with { ExtendedUntil = mine };
    }
}

/// <summary>Thread-safe <see cref="ILeaderboardStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemoryLeaderboardStore : ILeaderboardStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, LeaderboardEntry> byCareer = [];

    public int Count
    {
        get
        {
            lock (gate)
            {
                return byCareer.Count;
            }
        }
    }

    public Task<LeaderboardEntry?> FindByCareerAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(byCareer.GetValueOrDefault(careerId));
        }
    }

    public Task<bool> AddAsync(LeaderboardEntry entry, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(byCareer.TryAdd(entry.CareerId, entry));
        }
    }

    public Task<int> RankAsync(LeaderboardEntry entry, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(1 + Visible(entry.Season).Count(e => LeaderboardOrder.Instance.Compare(e, entry) < 0));
        }
    }

    public Task<IReadOnlyList<LeaderboardEntry>> TopAsync(int season, int limit, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<LeaderboardEntry>>([.. Visible(season).Order(LeaderboardOrder.Instance).Take(limit)]);
        }
    }

    /// <summary>Hides an entry as M4-D moderation will.</summary>
    public void Hide(Guid careerId, DateTimeOffset at)
    {
        lock (gate)
        {
            byCareer[careerId] = byCareer[careerId] with { HiddenAt = at };
        }
    }

    private IEnumerable<LeaderboardEntry> Visible(int season) => byCareer.Values.Where(e => e.Season == season && e.HiddenAt is null);
}
