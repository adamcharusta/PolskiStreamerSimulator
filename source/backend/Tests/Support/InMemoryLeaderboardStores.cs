using PolskiStreamerSimulator.Application.Leaderboard;
using PolskiStreamerSimulator.Application.Moderation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The audit records of the in-memory stores, newest last.</summary>
public sealed class InMemoryAuditTrail : IAuditTrail
{
    private readonly Lock gate = new();
    private readonly List<AuditRecord> records = [];

    public IReadOnlyList<AuditRecord> All
    {
        get
        {
            lock (gate)
            {
                return [.. records];
            }
        }
    }

    public void Add(AuditEntry entry)
    {
        lock (gate)
        {
            records.Add(new AuditRecord(records.Count + 1, entry));
        }
    }

    public Task<IReadOnlyList<AuditRecord>> RecentAsync(int limit, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<AuditRecord>>([.. records.AsEnumerable().Reverse().Take(limit)]);
        }
    }
}

/// <summary>Thread-safe <see cref="ISeasonStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemorySeasonStore(InMemoryAuditTrail? audit = null) : ISeasonStore
{
    /// <summary>Where extensions record their audit entries.</summary>
    public InMemoryAuditTrail Audit { get; } = audit ?? new InMemoryAuditTrail();

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

    public Task AddExtensionAsync(int number, DateTimeOffset deadline, AuditEntry audit, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            extensions.Add((number, deadline, audit.Reason, audit.Actor, audit.At));
            Audit.Add(audit);
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

/// <summary>Thread-safe <see cref="ILeaderboardStore"/> and <see cref="IModerationStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemoryLeaderboardStore(InMemoryAuditTrail? audit = null) : ILeaderboardStore, IModerationStore
{
    /// <summary>Where moderation records its audit entries.</summary>
    public InMemoryAuditTrail Audit { get; } = audit ?? new InMemoryAuditTrail();

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

    /// <summary>Hides an entry directly, without an audit record, as a test shortcut.</summary>
    public void Hide(Guid careerId, DateTimeOffset at)
    {
        lock (gate)
        {
            byCareer[careerId] = byCareer[careerId] with { HiddenAt = at, HiddenReason = "test" };
        }
    }

    public Task<IReadOnlyList<LeaderboardEntry>> ListAsync(int season, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<LeaderboardEntry>>([.. byCareer.Values.Where(e => e.Season == season).Order(LeaderboardOrder.Instance)]);
        }
    }

    public Task<(ModerationOutcome Outcome, LeaderboardEntry? Entry)> SetHiddenAsync(Guid entryId, bool hidden, AuditEntry audit, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var entry = byCareer.Values.SingleOrDefault(e => e.Id == entryId);
            if (entry is null)
            {
                return Task.FromResult<(ModerationOutcome, LeaderboardEntry?)>((ModerationOutcome.NotFound, null));
            }

            if ((entry.HiddenAt is not null) == hidden)
            {
                return Task.FromResult<(ModerationOutcome, LeaderboardEntry?)>((ModerationOutcome.Unchanged, entry));
            }

            var changed = entry with { HiddenAt = hidden ? audit.At : null, HiddenReason = hidden ? audit.Reason : null };
            byCareer[entry.CareerId] = changed;
            Audit.Add(audit with
            {
                OldJson = HiddenState.Json(entry.HiddenAt, entry.HiddenReason),
                NewJson = HiddenState.Json(changed.HiddenAt, changed.HiddenReason),
            });
            return Task.FromResult<(ModerationOutcome, LeaderboardEntry?)>((ModerationOutcome.Changed, changed));
        }
    }

    private IEnumerable<LeaderboardEntry> Visible(int season) => byCareer.Values.Where(e => e.Season == season && e.HiddenAt is null);
}
