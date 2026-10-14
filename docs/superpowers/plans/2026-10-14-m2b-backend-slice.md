# M2-B Backend Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist careers in SQL Server, expose career creation, reading, manual actions and a 13-week batch over Wolverine HTTP with repeat-safe, version-checked commands, and publish the OpenAPI document sub-project C builds on.

**Architecture:** `Application` holds the use cases as Wolverine handlers behind ports (`ICareerStore`, `IContentStore`, `ICurrentContent`, `ISecretHasher`, `TimeProvider`); `Infrastructure` implements the ports with EF Core on SQL Server as a state snapshot per career plus an append-only command log; `WebApp` maps HTTP requests to handler messages and results to JSON or Problem Details. The M1 `Domain` is reused unchanged except for policy reasons and a pause query.

**Tech Stack:** .NET 10, WolverineFx.Http 6.49.0, FluentValidation (via WolverineFx.Http.FluentValidation), EF Core 10 SQL Server provider, Mapster, Microsoft.AspNetCore.OpenApi, xUnit v3 on Microsoft.Testing.Platform, Testcontainers for SQL Server.

**Spec:** [`docs/superpowers/specs/2026-10-14-m2b-backend-slice-design.md`](../specs/2026-10-14-m2b-backend-slice-design.md)

## Global Constraints

- Run every `dotnet` command from `source/backend` ([AGENTS.md](../../../AGENTS.md)).
- Builds treat warnings as errors, with nullable reference types, enforced code style (braces always, no unused `using`) and central package versions in `source/backend/Directory.Packages.props`.
- Exact package versions: `WolverineFx.Http` 6.49.0, `WolverineFx.Http.FluentValidation` 6.49.0, `Microsoft.EntityFrameworkCore.SqlServer` 10.0.13, `Microsoft.EntityFrameworkCore.Design` 10.0.13, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.13, `Mapster` 10.0.13, `Microsoft.AspNetCore.OpenApi` 10.0.13, `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 (bumped from 10.0.12), `Testcontainers.MsSql` 4.16.0; local tool `dotnet-ef` 10.0.13.
- SQL Server test image: `mcr.microsoft.com/mssql/server:2025-RTM-ubuntu-22.04`.
- `Domain` references only the BCL and never uses `decimal`, `double`, `float`, `DateTime`, `DateTimeOffset` or `System.Random` (`Tests/Architecture/LayerDependencyTests.cs`). `Application` never references `Infrastructure` or `WebApp`.
- M1 rules and results do not change: every existing test, the golden files and the deterministic section of `docs/reports/m1-baseline-simulation.md` stay identical.
- Integration tests carry `[Trait("Category", "Integration")]` and `[Collection(SqlServerCollection.Name)]`; everything else runs without Docker.
- Routes: `POST /v1/careers`, `GET /v1/careers/{id}`, `POST /v1/careers/{id}/actions`, `POST /v1/careers/{id}/batches`.
- Error codes, exactly: `validation-failed` (400), `missing-secret` (401), `career-not-found` (404), `stale-version` (409), `career-ended` (409), `illegal-action`, `unknown-choice`, `events-unresolved`, `no-pending-week`, `week-in-progress`, `command-id-reused` (422), `internal-error` (500).
- JSON: camelCase properties; enums as camelCase strings (`dormRoomNewcomer`, `trainCategory`); `Fixed` values as JSON numbers through an exact `decimal` conversion.
- Career secret: 32 random bytes, base64url (43 characters), sent as `Authorization: Bearer <secret>`; only its SHA-256 is stored and compared in constant time.
- Character nickname: 3–20 characters after trimming; letters (any Unicode letter, including Polish), digits, space, `_`, `-`, `.`; stored trimmed.
- Every M2 career is unranked (`ranked = false`). New careers use ruleset `m1-prototype.1` and the content in `content/m1`.
- Test-driven development ([TD-58](../../technical-decisions.md)): each production change starts with a failing test.
- Documentation and decision records are in English. Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Pushes, PRs, live ruleset changes and merges each need the owner's explicit yes immediately before.

## Review Focus

- **Content changed after careers exist.** A new `content/m1` digest must not orphan careers pinned to the old one; the old snapshot keeps loading (Task 4, `An_older_snapshot_is_still_found_after_a_newer_one_is_stored`).
- **Same command ID, different body.** A client that refreshes and reuses a command ID with a newer `expectedVersion` gets `422 command-id-reused`, never a replay of the old result or a new execution (Task 8, `Reusing_a_command_id_with_another_request_is_rejected`).
- **Repeating a batch after the career moved on.** Replaying an old batch command ID returns the stored weeks and never plays further weeks (Task 14, `Repeating_a_batch_after_the_career_moved_on_plays_no_more_weeks`).
- **Malformed requests.** Broken JSON, an unknown enum string or a missing body return `400 validation-failed`, not `500` (Task 9, `Malformed_json_and_unknown_enums_return_validation_failed`).
- **Nickname edge cases.** Surrounding spaces and Polish letters are accepted and stored trimmed; emoji, two characters or 21 characters are rejected (Task 7, `The_nickname_is_stored_trimmed`, `An_invalid_nickname_is_rejected`).

## Delivery

Three PRs into `develop`, each merged before the next branch is cut from the updated `develop`:

| PR | Branch | Tasks | Closes |
| --- | --- | --- | --- |
| 1 | `feature/m2b-09-persistence` (already holds the spec) | 1–5 | #9 |
| 2 | `feature/m2b-10-career-api` | 6–11 | #10 |
| 3 | `feature/m2b-13-batch` | 12–15 | refers to #13 (stays open for C) |

Common commands (from `source/backend`):

- Unit run with the gate: `dotnet test PolskiStreamerSimulator.slnx -c Release --filter-not-trait "Category=Integration" --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total`
- Integration run (Docker Desktop running): `dotnet test PolskiStreamerSimulator.slnx -c Release --filter-trait "Category=Integration"`
- One test class: append `--filter-class "*<ClassName>"`.

---

# PR 1 — Persistence (#9)

### Task 1: Parse content snapshots from file bytes

Stored snapshots must reproduce the digest, which is the SHA-256 of the two files' bytes. Expose parsing from bytes.

**Files:**
- Modify: `source/backend/Infrastructure/Content/JsonContentSource.cs` (the `Load` method)
- Test: `source/backend/Tests/Infrastructure/JsonContentSourceTests.cs`
- Create: `source/backend/Tests/Support/TestContentFiles.cs`

**Interfaces:**
- Produces: `public static ContentSnapshot JsonContentSource.Parse(byte[] balanceBytes, byte[] eventsBytes, string source)`; `TestContentFiles.Balance`, `TestContentFiles.Events` (`byte[]` of the repository files).

- [ ] **Step 1: Write the failing tests**

Create `Tests/Support/TestContentFiles.cs`:

```csharp
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The bytes of the repository's M1 content files.</summary>
public static class TestContentFiles
{
    public static byte[] Balance { get; } = File.ReadAllBytes(Path.Combine(TestPaths.ContentM1, JsonContentSource.BalanceFile));

    public static byte[] Events { get; } = File.ReadAllBytes(Path.Combine(TestPaths.ContentM1, JsonContentSource.EventsFile));
}
```

Add to `JsonContentSourceTests` (add `using PolskiStreamerSimulator.Tests.Support;` if the file lacks it):

```csharp
    [Fact]
    public void Parsing_the_file_bytes_gives_the_loaded_snapshot()
    {
        var parsed = JsonContentSource.Parse(TestContentFiles.Balance, TestContentFiles.Events, "bytes");
        var loaded = new JsonContentSource(TestPaths.ContentM1).Load();

        Assert.Equal(loaded.Digest, parsed.Digest);
        Assert.Equal(loaded.Events.Select(e => e.Id), parsed.Events.Select(e => e.Id));
    }

    [Fact]
    public void Any_byte_change_changes_the_digest()
    {
        var padded = TestContentFiles.Balance.Concat("\n"u8.ToArray()).ToArray();

        Assert.NotEqual(
            JsonContentSource.Parse(TestContentFiles.Balance, TestContentFiles.Events, "a").Digest,
            JsonContentSource.Parse(padded, TestContentFiles.Events, "b").Digest);
    }

    [Fact]
    public void Invalid_bytes_name_their_source()
    {
        var error = Assert.Throws<InvalidDataException>(
            () => JsonContentSource.Parse(TestContentFiles.Balance, """{"events":[]}"""u8.ToArray(), "stored snapshot x"));

        Assert.Contains("'stored snapshot x'", error.Message, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*JsonContentSourceTests"`
Expected: build error `'JsonContentSource' does not contain a definition for 'Parse'`.

- [ ] **Step 3: Implement**

In `JsonContentSource.cs`, replace the body of `Load()` so that it reads both files and delegates; move the existing body into `Parse`, changing only the variable names and the error message source:

```csharp
    public ContentSnapshot Load()
        => Parse(
            File.ReadAllBytes(Path.Combine(directory, BalanceFile)),
            File.ReadAllBytes(Path.Combine(directory, EventsFile)),
            directory);

    /// <summary>
    /// Parses and validates content from the bytes of <c>balance.json</c> and <c>events.json</c>. The digest is the
    /// SHA-256 of exactly these bytes, so a stored copy reproduces it; <paramref name="source"/> names the origin in errors.
    /// </summary>
    public static ContentSnapshot Parse(byte[] balanceBytes, byte[] eventsBytes, string source)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(balanceBytes);
        hash.AppendData(eventsBytes);
        var digest = Convert.ToHexStringLower(hash.GetHashAndReset());

        using var balanceDocument = JsonDocument.Parse(balanceBytes);
        using var eventsDocument = JsonDocument.Parse(eventsBytes);
        var balanceRoot = balanceDocument.RootElement;
        var balance = ReadBalance(balanceRoot);
        var profiles = ReadProfiles(Property(balanceRoot, "profiles", BalanceFile));
        var events = ReadArray(Property(eventsDocument.RootElement, "events", EventsFile), "events", ReadEvent);
        var snapshot = new ContentSnapshot(balance.Version, digest, balance, events, profiles);

        var violations = ContentValidator.Validate(snapshot);
        if (violations.Count > 0)
        {
            throw new InvalidDataException($"Content in '{source}' is invalid: {string.Join(", ", violations)}");
        }

        return snapshot;
    }
```

Keep the existing private static helpers unchanged.

- [ ] **Step 4: Run the class and the full unit suite**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*JsonContentSourceTests"` → PASS. Then the unit run with the gate (see Delivery) → PASS, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add source/backend/Infrastructure/Content/JsonContentSource.cs source/backend/Tests/Infrastructure/JsonContentSourceTests.cs source/backend/Tests/Support/TestContentFiles.cs
git commit -m "refactor(infrastructure): parse content snapshots from file bytes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Persistence ports and the store contract

**Files:**
- Create: `source/backend/Application/Careers/Persistence/CareerRecords.cs`
- Create: `source/backend/Application/Careers/Persistence/ICareerStore.cs`
- Create: `source/backend/Application/Content/IContentStore.cs`
- Create: `source/backend/Tests/Support/InMemoryCareerStore.cs`
- Create: `source/backend/Tests/Support/InMemoryContentStore.cs`
- Create: `source/backend/Tests/Application/Persistence/CareerStoreContract.cs`
- Create: `source/backend/Tests/Application/Persistence/InMemoryCareerStoreTests.cs`

**Interfaces:**
- Produces (namespace `PolskiStreamerSimulator.Application.Careers.Persistence`): `StoredCareer(Guid Id, string Nickname, CareerState State, DateTimeOffset CreatedAt, DateTimeOffset LastActionAt)`, `NewCareer(Guid Id, string Nickname, CareerState State, byte[] SecretHash, DateTimeOffset CreatedAt)`, `enum ActionKind { StartWeek, ResolveEvent, SettleWeek, AutoWeek }`, `NewAction(Guid CommandId, int Step, ActionKind Kind, string RequestJson, string ResponseJson, string? ReportJson)`, `StoredAction(int Sequence, Guid CommandId, int Step, ActionKind Kind, string RequestJson, string ResponseJson, string? ReportJson, long StateVersionAfter, string StateHashAfter, DateTimeOffset CreatedAt)`, `enum AppendOutcome { Appended, VersionConflict, DuplicateCommand }`, `ICareerStore`.
- Produces (namespace `PolskiStreamerSimulator.Application.Content`): `ContentFiles(byte[] Balance, byte[] Events)`, `IContentStore`.
- Produces (tests): `InMemoryCareerStore`, `InMemoryContentStore(params ContentSnapshot[] initial)`, abstract `CareerStoreContract` with `protected abstract Task<ICareerStore> CreateStoreAsync()`.

- [ ] **Step 1: Write the ports**

`Application/Careers/Persistence/CareerRecords.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>A career as stored: its current state and display metadata.</summary>
public sealed record StoredCareer(Guid Id, string Nickname, CareerState State, DateTimeOffset CreatedAt, DateTimeOffset LastActionAt);

/// <summary>A career to create with the hash of its access secret; the raw secret is never stored.</summary>
public sealed record NewCareer(Guid Id, string Nickname, CareerState State, byte[] SecretHash, DateTimeOffset CreatedAt);

/// <summary>What a logged command did.</summary>
public enum ActionKind
{
    StartWeek,
    ResolveEvent,
    SettleWeek,
    AutoWeek,
}

/// <summary>A log entry to append; the store assigns its sequence.</summary>
public sealed record NewAction(Guid CommandId, int Step, ActionKind Kind, string RequestJson, string ResponseJson, string? ReportJson);

/// <summary>A logged command step.</summary>
public sealed record StoredAction(
    int Sequence,
    Guid CommandId,
    int Step,
    ActionKind Kind,
    string RequestJson,
    string ResponseJson,
    string? ReportJson,
    long StateVersionAfter,
    string StateHashAfter,
    DateTimeOffset CreatedAt);

/// <summary>Result of <see cref="ICareerStore.AppendAsync"/>.</summary>
public enum AppendOutcome
{
    Appended,
    VersionConflict,
    DuplicateCommand,
}
```

`Application/Careers/Persistence/ICareerStore.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers.Persistence;

/// <summary>Persistence port for careers and their append-only command log.</summary>
public interface ICareerStore
{
    Task CreateAsync(NewCareer career, CancellationToken cancellationToken);

    Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken);

    Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>Every logged step of one command, ordered by step; empty when the command was never stored.</summary>
    Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken);

    /// <summary>The newest logged entry that carries a week report, or <c>null</c>.</summary>
    Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction, replaces the state and appends <paramref name="action"/> when the stored version equals
    /// <paramref name="expectedVersion"/>; otherwise changes nothing.
    /// </summary>
    Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
```

`Application/Content/IContentStore.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>The bytes of <c>balance.json</c> and <c>events.json</c>; their SHA-256 is the snapshot digest.</summary>
public sealed record ContentFiles(byte[] Balance, byte[] Events);

/// <summary>Port for content snapshots, kept so every career can load the content it was created with.</summary>
public interface IContentStore
{
    /// <summary>Stores the files under their digest unless already stored; returns the parsed snapshot.</summary>
    Task<ContentSnapshot> EnsureStoredAsync(ContentFiles files, CancellationToken cancellationToken);

    /// <summary>The snapshot with <paramref name="digest"/>, or <c>null</c> when it was never stored.</summary>
    Task<ContentSnapshot?> FindAsync(string digest, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Write the contract and the in-memory test run**

`Tests/Application/Persistence/CareerStoreContract.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Persistence;

/// <summary>Behaviour every <see cref="ICareerStore"/> shows; run against the in-memory and the SQL Server store.</summary>
public abstract class CareerStoreContract
{
    private static readonly DateTimeOffset Created = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddMinutes(5);

    protected abstract Task<ICareerStore> CreateStoreAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static NewCareer SampleCareer(ulong seed = 1)
        => new(Guid.CreateVersion7(Created), "Streamer", TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, seed), [1, 2, 3], Created);

    private static CareerState Advanced(CareerState s) => s with { StateVersion = s.StateVersion + 1 };

    private static NewAction Entry(Guid commandId, int step = 0, string? report = null)
        => new(commandId, step, step == 0 ? ActionKind.SettleWeek : ActionKind.AutoWeek, """{"request":1}""", """{"response":1}""", report);

    private async Task<(ICareerStore Store, NewCareer Career)> StoreWithCareerAsync()
    {
        var store = await CreateStoreAsync();
        var career = SampleCareer();
        await store.CreateAsync(career, Ct);
        return (store, career);
    }

    [Fact]
    public async Task A_created_career_is_found_with_its_state_and_secret_hash()
    {
        var (store, career) = await StoreWithCareerAsync();

        var found = await store.FindAsync(career.Id, Ct);

        Assert.NotNull(found);
        Assert.Equal("Streamer", found.Nickname);
        Assert.Equal(StateHash.Of(career.State), StateHash.Of(found.State));
        Assert.Equal(Created, found.CreatedAt);
        Assert.Equal(Created, found.LastActionAt);
        Assert.Equal(career.SecretHash, await store.FindSecretHashAsync(career.Id, Ct));
    }

    [Fact]
    public async Task An_unknown_career_is_not_found()
    {
        var store = await CreateStoreAsync();

        Assert.Null(await store.FindAsync(Guid.NewGuid(), Ct));
        Assert.Null(await store.FindSecretHashAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Append_replaces_the_state_and_logs_the_command()
    {
        var (store, career) = await StoreWithCareerAsync();
        var next = Advanced(career.State);
        var command = Guid.NewGuid();

        Assert.Equal(AppendOutcome.Appended, await store.AppendAsync(career.Id, career.State.StateVersion, next, Entry(command), Later, Ct));

        var found = await store.FindAsync(career.Id, Ct);
        Assert.Equal(next.StateVersion, found!.State.StateVersion);
        Assert.Equal(Later, found.LastActionAt);
        var logged = Assert.Single(await store.FindCommandAsync(career.Id, command, Ct));
        Assert.Equal(1, logged.Sequence);
        Assert.Equal(ActionKind.SettleWeek, logged.Kind);
        Assert.Equal(next.StateVersion, logged.StateVersionAfter);
        Assert.Equal(StateHash.Of(next), logged.StateHashAfter);
        Assert.Equal("""{"request":1}""", logged.RequestJson);
        Assert.Equal("""{"response":1}""", logged.ResponseJson);
        Assert.Equal(Later, logged.CreatedAt);
    }

    [Fact]
    public async Task A_stale_version_changes_nothing()
    {
        var (store, career) = await StoreWithCareerAsync();
        var command = Guid.NewGuid();

        var outcome = await store.AppendAsync(career.Id, career.State.StateVersion + 1, Advanced(Advanced(career.State)), Entry(command), Later, Ct);

        Assert.Equal(AppendOutcome.VersionConflict, outcome);
        Assert.Equal(career.State.StateVersion, (await store.FindAsync(career.Id, Ct))!.State.StateVersion);
        Assert.Empty(await store.FindCommandAsync(career.Id, command, Ct));
    }

    [Fact]
    public async Task A_repeated_command_step_is_rejected_and_changes_nothing()
    {
        var (store, career) = await StoreWithCareerAsync();
        var command = Guid.NewGuid();
        var v1 = Advanced(career.State);
        Assert.Equal(AppendOutcome.Appended, await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(command), Later, Ct));

        var outcome = await store.AppendAsync(career.Id, v1.StateVersion, Advanced(v1), Entry(command), Later, Ct);

        Assert.Equal(AppendOutcome.DuplicateCommand, outcome);
        Assert.Single(await store.FindCommandAsync(career.Id, command, Ct));
        Assert.Equal(v1.StateVersion, (await store.FindAsync(career.Id, Ct))!.State.StateVersion);
    }

    [Fact]
    public async Task Command_steps_come_back_in_order_with_consecutive_sequences()
    {
        var (store, career) = await StoreWithCareerAsync();
        var batch = Guid.NewGuid();
        var v1 = Advanced(career.State);
        var v2 = Advanced(v1);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(batch, 1), Later, Ct);
        await store.AppendAsync(career.Id, v1.StateVersion, v2, Entry(batch, 2), Later, Ct);

        var steps = await store.FindCommandAsync(career.Id, batch, Ct);

        Assert.Equal([1, 2], steps.Select(s => s.Step));
        Assert.Equal([1, 2], steps.Select(s => s.Sequence));
        Assert.All(steps, s => Assert.Equal(ActionKind.AutoWeek, s.Kind));
    }

    [Fact]
    public async Task The_latest_report_is_the_newest_entry_that_has_one()
    {
        var (store, career) = await StoreWithCareerAsync();
        Assert.Null(await store.FindLatestReportAsync(career.Id, Ct));
        var v1 = Advanced(career.State);
        var v2 = Advanced(v1);
        await store.AppendAsync(career.Id, career.State.StateVersion, v1, Entry(Guid.NewGuid(), report: """{"week":1}"""), Later, Ct);
        await store.AppendAsync(career.Id, v1.StateVersion, v2, Entry(Guid.NewGuid()), Later, Ct);

        var latest = await store.FindLatestReportAsync(career.Id, Ct);

        Assert.Equal("""{"week":1}""", latest!.ReportJson);
        Assert.Equal(1, latest.Sequence);
    }
}
```

`Tests/Application/Persistence/InMemoryCareerStoreTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Persistence;

public sealed class InMemoryCareerStoreTests : CareerStoreContract
{
    protected override Task<ICareerStore> CreateStoreAsync() => Task.FromResult<ICareerStore>(new InMemoryCareerStore());
}
```

- [ ] **Step 3: Run to see it fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*InMemoryCareerStoreTests"`
Expected: build error, `InMemoryCareerStore` does not exist.

- [ ] **Step 4: Implement the in-memory doubles**

`Tests/Support/InMemoryCareerStore.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Thread-safe <see cref="ICareerStore"/> with the same rules as the SQL Server store.</summary>
public sealed class InMemoryCareerStore : ICareerStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, StoredCareer> careers = [];
    private readonly Dictionary<Guid, byte[]> secrets = [];
    private readonly Dictionary<Guid, List<StoredAction>> logs = [];

    public Task CreateAsync(NewCareer career, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            careers.Add(career.Id, new StoredCareer(career.Id, career.Nickname, career.State, career.CreatedAt, career.CreatedAt));
            secrets.Add(career.Id, career.SecretHash);
            logs.Add(career.Id, []);
        }

        return Task.CompletedTask;
    }

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(careers.GetValueOrDefault(careerId));
        }
    }

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(secrets.GetValueOrDefault(careerId));
        }
    }

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            IReadOnlyList<StoredAction> steps = logs.TryGetValue(careerId, out var log)
                ? [.. log.Where(a => a.CommandId == commandId).OrderBy(a => a.Step)]
                : [];
            return Task.FromResult(steps);
        }
    }

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(logs.TryGetValue(careerId, out var log) ? log.LastOrDefault(a => a.ReportJson is not null) : null);
        }
    }

    public Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var career = careers[careerId];
            if (career.State.StateVersion != expectedVersion)
            {
                return Task.FromResult(AppendOutcome.VersionConflict);
            }

            var log = logs[careerId];
            if (log.Any(a => a.CommandId == action.CommandId && a.Step == action.Step))
            {
                return Task.FromResult(AppendOutcome.DuplicateCommand);
            }

            log.Add(new StoredAction(
                log.Count + 1,
                action.CommandId,
                action.Step,
                action.Kind,
                action.RequestJson,
                action.ResponseJson,
                action.ReportJson,
                newState.StateVersion,
                StateHash.Of(newState),
                at));
            careers[careerId] = career with { State = newState, LastActionAt = at };
            return Task.FromResult(AppendOutcome.Appended);
        }
    }

    /// <summary>Number of logged steps for <paramref name="careerId"/>.</summary>
    public int LogCount(Guid careerId)
    {
        lock (gate)
        {
            return logs.TryGetValue(careerId, out var log) ? log.Count : 0;
        }
    }
}
```

`Tests/Support/InMemoryContentStore.cs`:

```csharp
using System.Collections.Concurrent;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Support;

public sealed class InMemoryContentStore : IContentStore
{
    private readonly ConcurrentDictionary<string, ContentSnapshot> snapshots = new(StringComparer.Ordinal);

    public InMemoryContentStore(params ContentSnapshot[] initial)
    {
        foreach (var snapshot in initial)
        {
            snapshots[snapshot.Digest] = snapshot;
        }
    }

    public Task<ContentSnapshot> EnsureStoredAsync(ContentFiles files, CancellationToken cancellationToken)
    {
        var snapshot = JsonContentSource.Parse(files.Balance, files.Events, "memory");
        return Task.FromResult(snapshots.GetOrAdd(snapshot.Digest, snapshot));
    }

    public Task<ContentSnapshot?> FindAsync(string digest, CancellationToken cancellationToken)
        => Task.FromResult(snapshots.TryGetValue(digest, out var snapshot) ? snapshot : null);
}
```

- [ ] **Step 5: Run and commit**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*InMemoryCareerStoreTests"` → 7 passed. Unit run with the gate → PASS.

```bash
git add source/backend/Application source/backend/Tests/Support/InMemoryCareerStore.cs source/backend/Tests/Support/InMemoryContentStore.cs source/backend/Tests/Application
git commit -m "feat(application): add the career and content persistence ports" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: EF Core model, initial migration and the SQL Server fixture

**Files:**
- Modify: `source/backend/Directory.Packages.props`, `source/backend/Infrastructure/Infrastructure.csproj`, `source/backend/Tests/Tests.csproj`, `source/backend/.config/dotnet-tools.json` (via `dotnet tool install`), `source/backend/Tests/testconfig.json`
- Create: `source/backend/Infrastructure/Persistence/Rows.cs`, `source/backend/Infrastructure/Persistence/GameDbContext.cs`, `source/backend/Infrastructure/Persistence/GameDbContextDesignTimeFactory.cs`, `source/backend/Infrastructure/Persistence/Migrations/*` (generated)
- Create: `source/backend/Tests/Support/SqlServerFixture.cs`
- Test: `source/backend/Tests/Infrastructure/Persistence/MigrationTests.cs`

**Interfaces:**
- Produces: `GameDbContext` (schema `game`; `DbSet<CareerRow> Careers`, `CareerSecrets`, `CareerActions`, `ContentSnapshots`); row classes `CareerRow`, `CareerSecretRow`, `CareerActionRow`, `ContentSnapshotRow`; `SqlServerFixture` (`NewDatabase()`, `static Options(string, params IInterceptor[])`, `MigratedDatabaseAsync(CancellationToken, params IInterceptor[])`), `SqlServerCollection.Name`, `TestDbContextFactory`.

- [ ] **Step 1: Add packages and the EF tool**

In `Directory.Packages.props`, inside the existing `<ItemGroup>` (keep alphabetical order), add these entries and change `Microsoft.AspNetCore.Mvc.Testing` to `10.0.13`:

```xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.13" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.13" />
    <PackageVersion Include="Testcontainers.MsSql" Version="4.16.0" />
```

In `Infrastructure.csproj` add:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
```

In `Tests.csproj` add `<PackageReference Include="Testcontainers.MsSql" />` to the package `ItemGroup`.

Run: `dotnet tool install dotnet-ef --version 10.0.13` (adds the tool to `.config/dotnet-tools.json`), then `dotnet build PolskiStreamerSimulator.slnx` → 0 warnings.

- [ ] **Step 2: Write the failing migration test and the fixture**

`Tests/Support/SqlServerFixture.cs`:

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>One SQL Server container for the integration tests; each test class creates its own databases.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2025-RTM-ubuntu-22.04";

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    /// <summary>A connection string naming a database that does not exist yet.</summary>
    public string NewDatabase()
        => new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = $"pss_{Guid.NewGuid():N}" }.ConnectionString;

    public static DbContextOptions<GameDbContext> Options(string connectionString, params IInterceptor[] interceptors)
        => new DbContextOptionsBuilder<GameDbContext>().UseSqlServer(connectionString).AddInterceptors(interceptors).Options;

    /// <summary>Creates and migrates a new database and returns a context factory for it.</summary>
    public async Task<TestDbContextFactory> MigratedDatabaseAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var factory = new TestDbContextFactory(Options(NewDatabase(), interceptors));
        await using var db = factory.CreateDbContext();
        await db.Database.MigrateAsync(cancellationToken);
        return factory;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

/// <summary>Context factory over fixed options, as <c>AddDbContextFactory</c> registers in the application.</summary>
public sealed class TestDbContextFactory(DbContextOptions<GameDbContext> options) : IDbContextFactory<GameDbContext>
{
    public DbContextOptions<GameDbContext> Options => options;

    public GameDbContext CreateDbContext() => new(options);
}
```

If `new MsSqlBuilder(Image)` reports an obsolete or missing constructor, use `new MsSqlBuilder().WithImage(Image)` instead.

`Tests/Infrastructure/Persistence/MigrationTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Migrations_build_the_schema_on_an_empty_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = await sql.MigratedDatabaseAsync(ct);
        await using var db = factory.CreateDbContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(0, await db.Careers.CountAsync(ct));
        Assert.Equal(0, await db.CareerActions.CountAsync(ct));
        Assert.Equal(0, await db.ContentSnapshots.CountAsync(ct));
    }
}
```

- [ ] **Step 3: Run to see it fail**

Run (Docker running): `dotnet test PolskiStreamerSimulator.slnx --filter-class "*MigrationTests"`
Expected: build error, `GameDbContext` does not exist.

- [ ] **Step 4: Implement the model**

`Infrastructure/Persistence/Rows.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>Row of <c>game.careers</c>: the current state snapshot and display metadata.</summary>
public sealed class CareerRow
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = "";

    public string RulesetId { get; set; } = "";

    public string ContentDigest { get; set; } = "";

    public bool Ranked { get; set; }

    public CareerStatus Status { get; set; }

    public long StateVersion { get; set; }

    public string StateJson { get; set; } = "";

    public string StateHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastActionAt { get; set; }
}

/// <summary>Row of <c>game.career_secrets</c>; holds only the SHA-256 of the access secret.</summary>
public sealed class CareerSecretRow
{
    public Guid CareerId { get; set; }

    public byte[] SecretHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Row of the append-only <c>game.career_actions</c> log.</summary>
public sealed class CareerActionRow
{
    public Guid CareerId { get; set; }

    public int Sequence { get; set; }

    public Guid CommandId { get; set; }

    public int Step { get; set; }

    public ActionKind Kind { get; set; }

    public string RequestJson { get; set; } = "";

    public string ResponseJson { get; set; } = "";

    public string? ReportJson { get; set; }

    public long StateVersionAfter { get; set; }

    public string StateHashAfter { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Row of <c>game.content_snapshots</c>: the two content files byte-for-byte under their digest.</summary>
public sealed class ContentSnapshotRow
{
    public string Digest { get; set; } = "";

    public byte[] BalanceJson { get; set; } = [];

    public byte[] EventsJson { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}
```

`Infrastructure/Persistence/GameDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>EF Core context for the game tables in the <c>game</c> schema.</summary>
public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public const string Schema = "game";

    public DbSet<CareerRow> Careers => Set<CareerRow>();

    public DbSet<CareerSecretRow> CareerSecrets => Set<CareerSecretRow>();

    public DbSet<CareerActionRow> CareerActions => Set<CareerActionRow>();

    public DbSet<ContentSnapshotRow> ContentSnapshots => Set<ContentSnapshotRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ContentSnapshotRow>(e =>
        {
            e.ToTable("content_snapshots");
            e.HasKey(x => x.Digest);
            e.Property(x => x.Digest).HasColumnName("digest").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.BalanceJson).HasColumnName("balance_json");
            e.Property(x => x.EventsJson).HasColumnName("events_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<CareerRow>(e =>
        {
            e.ToTable("careers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Nickname).HasColumnName("nickname").HasMaxLength(20);
            e.Property(x => x.RulesetId).HasColumnName("ruleset_id").HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ContentDigest).HasColumnName("content_digest").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.Ranked).HasColumnName("ranked");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsUnicode(false);
            e.Property(x => x.StateVersion).HasColumnName("state_version").IsConcurrencyToken();
            e.Property(x => x.StateJson).HasColumnName("state_json");
            e.Property(x => x.StateHash).HasColumnName("state_hash").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.LastActionAt).HasColumnName("last_action_at");
            e.HasOne<ContentSnapshotRow>().WithMany().HasForeignKey(x => x.ContentDigest).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CareerSecretRow>(e =>
        {
            e.ToTable("career_secrets");
            e.HasKey(x => x.CareerId);
            e.Property(x => x.CareerId).HasColumnName("career_id");
            e.Property(x => x.SecretHash).HasColumnName("secret_hash").HasMaxLength(32).IsFixedLength();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne<CareerRow>().WithOne().HasForeignKey<CareerSecretRow>(x => x.CareerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CareerActionRow>(e =>
        {
            e.ToTable("career_actions");
            e.HasKey(x => new { x.CareerId, x.Sequence });
            e.Property(x => x.CareerId).HasColumnName("career_id");
            e.Property(x => x.Sequence).HasColumnName("sequence").ValueGeneratedNever();
            e.Property(x => x.CommandId).HasColumnName("command_id");
            e.Property(x => x.Step).HasColumnName("step");
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16).IsUnicode(false);
            e.Property(x => x.RequestJson).HasColumnName("request_json");
            e.Property(x => x.ResponseJson).HasColumnName("response_json");
            e.Property(x => x.ReportJson).HasColumnName("report_json");
            e.Property(x => x.StateVersionAfter).HasColumnName("state_version_after");
            e.Property(x => x.StateHashAfter).HasColumnName("state_hash_after").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.CareerId, x.CommandId, x.Step }).IsUnique().HasDatabaseName("ux_career_actions_command");
            e.HasOne<CareerRow>().WithMany().HasForeignKey(x => x.CareerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

`Infrastructure/Persistence/GameDbContextDesignTimeFactory.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the model without the web host; it never opens a connection.</summary>
[ExcludeFromCodeCoverage]
public sealed class GameDbContextDesignTimeFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlServer("Server=localhost;Database=pss_design;TrustServerCertificate=True")
            .Options);
}
```

Generate the migration:

Run: `dotnet ef migrations add InitialGameSchema --project Infrastructure --startup-project Infrastructure --output-dir Persistence/Migrations`
Expected: `Infrastructure/Persistence/Migrations/<timestamp>_InitialGameSchema.cs`, its `.Designer.cs` and `GameDbContextModelSnapshot.cs`. Open the migration and check: schema `game`; tables `content_snapshots`, `careers`, `career_secrets`, `career_actions`; unique index `ux_career_actions_command`; `secret_hash` `binary(32)`; digests `char(64)`.

Exclude the generated migrations from coverage: in `Tests/testconfig.json` change `"excludeByFile": "**/*.g.cs"` to `"excludeByFile": "**/*.g.cs,**/Persistence/Migrations/*.cs"`.

- [ ] **Step 5: Run the migration test and the unit suite**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*MigrationTests"` → 1 passed (the first run pulls the SQL Server image). Unit run with the gate → PASS, 0 warnings, and the migration test does not run there.

- [ ] **Step 6: Commit**

```bash
git add source/backend/Directory.Packages.props source/backend/.config/dotnet-tools.json source/backend/Infrastructure source/backend/Tests
git commit -m "feat(infrastructure): add the game schema and its first migration" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4: Career and content stores on SQL Server

**Files:**
- Create: `source/backend/Infrastructure/Persistence/SqlErrors.cs`, `source/backend/Infrastructure/Persistence/EfCareerStore.cs`, `source/backend/Infrastructure/Persistence/EfContentStore.cs`
- Create: `source/backend/Tests/Support/FailingCommandInterceptor.cs`
- Test: `source/backend/Tests/Infrastructure/Persistence/EfCareerStoreTests.cs`, `source/backend/Tests/Infrastructure/Persistence/EfContentStoreTests.cs`

**Interfaces:**
- Consumes: `ICareerStore`, `IContentStore`, `ContentFiles` (Task 2); `GameDbContext`, rows, `SqlServerFixture` (Task 3); `JsonContentSource.Parse` (Task 1).
- Produces: `EfCareerStore(IDbContextFactory<GameDbContext> contexts) : ICareerStore`; `EfContentStore(IDbContextFactory<GameDbContext> contexts, TimeProvider time) : IContentStore`; `FailingCommandInterceptor(string marker)` with `bool Armed`.

- [ ] **Step 1: Write the failing tests**

`Tests/Support/FailingCommandInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Throws before executing any command whose SQL contains <paramref name="marker"/> while armed.</summary>
public sealed class FailingCommandInterceptor(string marker) : DbCommandInterceptor
{
    public bool Armed { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Fail(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Fail(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Fail(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Fail(DbCommand command)
    {
        if (Armed && command.CommandText.Contains(marker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Injected failure before: {marker}");
        }
    }
}
```

`Tests/Infrastructure/Persistence/EfCareerStoreTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Serialization;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Application.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfCareerStoreTests(SqlServerFixture sql) : CareerStoreContract
{
    private static readonly ContentFiles Files = new(TestContentFiles.Balance, TestContentFiles.Events);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected override async Task<ICareerStore> CreateStoreAsync() => new EfCareerStore(await ContentReadyAsync());

    private async Task<TestDbContextFactory> ContentReadyAsync(params FailingCommandInterceptor[] interceptors)
    {
        var factory = await sql.MigratedDatabaseAsync(Ct, interceptors);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);
        return factory;
    }

    [Fact]
    public async Task A_failed_log_insert_leaves_the_career_unchanged()
    {
        var interceptor = new FailingCommandInterceptor("INSERT INTO [game].[career_actions]");
        var factory = await ContentReadyAsync(interceptor);
        var store = new EfCareerStore(factory);
        var career = SampleCareer();
        await store.CreateAsync(career, Ct);
        var next = career.State with { StateVersion = career.State.StateVersion + 1 };
        var entry = new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null);

        interceptor.Armed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AppendAsync(career.Id, career.State.StateVersion, next, entry, DateTimeOffset.UnixEpoch, Ct));
        interceptor.Armed = false;

        Assert.Equal(career.State.StateVersion, (await store.FindAsync(career.Id, Ct))!.State.StateVersion);
        await using var db = factory.CreateDbContext();
        Assert.Equal(0, await db.CareerActions.CountAsync(Ct));
    }

    [Fact]
    public async Task A_career_survives_a_new_store_on_the_same_database()
    {
        var factory = await ContentReadyAsync();
        var career = SampleCareer();
        await new EfCareerStore(factory).CreateAsync(career, Ct);

        var reopened = await new EfCareerStore(new TestDbContextFactory(factory.Options)).FindAsync(career.Id, Ct);

        Assert.Equal(StateHash.Of(career.State), StateHash.Of(reopened!.State));
    }
}
```

`Tests/Infrastructure/Persistence/EfContentStoreTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Infrastructure.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EfContentStoreTests(SqlServerFixture sql)
{
    private static readonly ContentFiles Files = new(TestContentFiles.Balance, TestContentFiles.Events);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stored_files_load_back_as_the_same_snapshot()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        var stored = await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);

        var loaded = await new EfContentStore(factory, TimeProvider.System).FindAsync(stored.Digest, Ct);

        Assert.Equal(TestContent.Valid().Digest, stored.Digest);
        Assert.Equal(stored.Digest, loaded!.Digest);
        Assert.Equal(stored.Events.Select(e => e.Id), loaded.Events.Select(e => e.Id));
    }

    [Fact]
    public async Task Storing_the_same_files_twice_keeps_one_row()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);
        await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);

        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.ContentSnapshots.CountAsync(Ct));
    }

    [Fact]
    public async Task An_older_snapshot_is_still_found_after_a_newer_one_is_stored()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);
        var old = await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(Files, Ct);
        var newer = new ContentFiles([.. TestContentFiles.Balance, .. "\n"u8.ToArray()], TestContentFiles.Events);
        var current = await new EfContentStore(factory, TimeProvider.System).EnsureStoredAsync(newer, Ct);

        var reloaded = await new EfContentStore(factory, TimeProvider.System).FindAsync(old.Digest, Ct);

        Assert.NotEqual(old.Digest, current.Digest);
        Assert.Equal(old.Digest, reloaded!.Digest);
    }

    [Fact]
    public async Task An_unknown_digest_is_not_found()
    {
        var factory = await sql.MigratedDatabaseAsync(Ct);

        Assert.Null(await new EfContentStore(factory, TimeProvider.System).FindAsync(new string('0', 64), Ct));
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*Ef*StoreTests"`
Expected: build error, `EfCareerStore` and `EfContentStore` do not exist.

- [ ] **Step 3: Implement the stores**

`Infrastructure/Persistence/SqlErrors.cs`:

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

internal static class SqlErrors
{
    /// <summary>SQL Server 2601/2627: a unique index or primary key rejected the insert.</summary>
    public static bool IsDuplicateKey(DbUpdateException error) => error.InnerException is SqlException { Number: 2601 or 2627 };
}
```

`Infrastructure/Persistence/EfCareerStore.cs`:

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Serialization;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="ICareerStore"/>: a state snapshot per career and an append-only command log.</summary>
public sealed class EfCareerStore(IDbContextFactory<GameDbContext> contexts) : ICareerStore
{
    public async Task CreateAsync(NewCareer career, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        db.Careers.Add(new CareerRow
        {
            Id = career.Id,
            Nickname = career.Nickname,
            RulesetId = career.State.RulesetId,
            ContentDigest = career.State.ContentDigest,
            Ranked = false,
            Status = career.State.Status,
            StateVersion = career.State.StateVersion,
            StateJson = JsonOf(career.State),
            StateHash = StateHash.Of(career.State),
            CreatedAt = career.CreatedAt,
            LastActionAt = career.CreatedAt,
        });
        db.CareerSecrets.Add(new CareerSecretRow { CareerId = career.Id, SecretHash = career.SecretHash, CreatedAt = career.CreatedAt });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == careerId, cancellationToken);
        return row is null
            ? null
            : new StoredCareer(row.Id, row.Nickname, CanonicalJson.Deserialize(Encoding.UTF8.GetBytes(row.StateJson)), row.CreatedAt, row.LastActionAt);
    }

    public async Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.CareerSecrets.AsNoTracking()
            .Where(s => s.CareerId == careerId)
            .Select(s => s.SecretHash)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await db.CareerActions.AsNoTracking()
            .Where(a => a.CareerId == careerId && a.CommandId == commandId)
            .OrderBy(a => a.Step)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(ToStored)];
    }

    public async Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.CareerActions.AsNoTracking()
            .Where(a => a.CareerId == careerId && a.ReportJson != null)
            .OrderByDescending(a => a.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToStored(row);
    }

    public async Task<AppendOutcome> AppendAsync(
        Guid careerId,
        long expectedVersion,
        CareerState newState,
        NewAction action,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var json = JsonOf(newState);
        var hash = StateHash.Of(newState);

        // The version condition both detects a concurrent advance and locks the row until commit.
        var updated = await db.Careers
            .Where(c => c.Id == careerId && c.StateVersion == expectedVersion)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.StateVersion, newState.StateVersion)
                    .SetProperty(c => c.StateJson, json)
                    .SetProperty(c => c.StateHash, hash)
                    .SetProperty(c => c.Status, newState.Status)
                    .SetProperty(c => c.LastActionAt, at),
                cancellationToken);
        if (updated == 0)
        {
            return AppendOutcome.VersionConflict;
        }

        var last = await db.CareerActions.Where(a => a.CareerId == careerId).MaxAsync(a => (int?)a.Sequence, cancellationToken) ?? 0;
        db.CareerActions.Add(new CareerActionRow
        {
            CareerId = careerId,
            Sequence = last + 1,
            CommandId = action.CommandId,
            Step = action.Step,
            Kind = action.Kind,
            RequestJson = action.RequestJson,
            ResponseJson = action.ResponseJson,
            ReportJson = action.ReportJson,
            StateVersionAfter = newState.StateVersion,
            StateHashAfter = hash,
            CreatedAt = at,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
        {
            return AppendOutcome.DuplicateCommand;
        }

        await transaction.CommitAsync(cancellationToken);
        return AppendOutcome.Appended;
    }

    private static string JsonOf(CareerState state) => Encoding.UTF8.GetString(CanonicalJson.Serialize(state));

    private static StoredAction ToStored(CareerActionRow row)
        => new(row.Sequence, row.CommandId, row.Step, row.Kind, row.RequestJson, row.ResponseJson, row.ReportJson, row.StateVersionAfter, row.StateHashAfter, row.CreatedAt);
}
```

Returning before `CommitAsync` disposes the transaction, which rolls it back.

`Infrastructure/Persistence/EfContentStore.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>SQL Server <see cref="IContentStore"/>; parsed snapshots are cached by digest.</summary>
public sealed class EfContentStore(IDbContextFactory<GameDbContext> contexts, TimeProvider time) : IContentStore
{
    private readonly ConcurrentDictionary<string, ContentSnapshot> cache = new(StringComparer.Ordinal);

    public async Task<ContentSnapshot> EnsureStoredAsync(ContentFiles files, CancellationToken cancellationToken)
    {
        var snapshot = JsonContentSource.Parse(files.Balance, files.Events, "content files");
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        if (!await db.ContentSnapshots.AnyAsync(s => s.Digest == snapshot.Digest, cancellationToken))
        {
            db.ContentSnapshots.Add(new ContentSnapshotRow
            {
                Digest = snapshot.Digest,
                BalanceJson = files.Balance,
                EventsJson = files.Events,
                CreatedAt = time.GetUtcNow(),
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException error) when (SqlErrors.IsDuplicateKey(error))
            {
                // Another API instance stored the same digest first; the stored bytes are identical.
            }
        }

        return cache.GetOrAdd(snapshot.Digest, snapshot);
    }

    public async Task<ContentSnapshot?> FindAsync(string digest, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(digest, out var cached))
        {
            return cached;
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await db.ContentSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.Digest == digest, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var snapshot = JsonContentSource.Parse(row.BalanceJson, row.EventsJson, $"stored snapshot {digest}");
        if (!string.Equals(snapshot.Digest, digest, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Stored content snapshot {digest} hashes to {snapshot.Digest}.");
        }

        return cache.GetOrAdd(digest, snapshot);
    }
}
```

- [ ] **Step 4: Run the store tests and the unit suite**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-trait "Category=Integration"` → all integration tests pass (contract 7 + 2, content 4, migration 1). Unit run with the gate → PASS, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add source/backend/Infrastructure source/backend/Tests
git commit -m "feat(infrastructure): store careers, their command log and content snapshots in SQL Server" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5: CI integration job, decisions and PR 1

**Files:**
- Modify: `.github/workflows/ci.yml`, `.github/rulesets/main.json`, `.github/rulesets/develop.json`
- Modify: `docs/technical-decisions.md`, `docs/development-and-delivery.md`, `docs/repository-standards.md`, `docs/requirements-traceability.md`, `README.md`
- Add: this plan file

- [ ] **Step 1: Change the workflow**

In `.github/workflows/ci.yml`:

1. In job `backend`, step `Test with coverage gate`, add `--filter-not-trait "Category=Integration"` after `--no-build` so the line reads:

```yaml
        run: >-
          dotnet test PolskiStreamerSimulator.slnx -c Release --no-build
          --filter-not-trait "Category=Integration"
          --results-directory "$RUNNER_TEMP/test-results"
          --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total
```

2. Add this job after `backend` (same pinned action SHAs as `backend`):

```yaml
  backend-integration:
    name: backend-integration
    runs-on: ubuntu-latest
    timeout-minutes: 20
    defaults:
      run:
        working-directory: source/backend
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: source/backend/global.json
      - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('source/backend/Directory.Packages.props', 'source/backend/**/*.csproj', 'source/backend/.config/dotnet-tools.json') }}
          restore-keys: nuget-${{ runner.os }}-
      - name: Restore
        run: dotnet restore PolskiStreamerSimulator.slnx
      - name: Build
        run: dotnet build PolskiStreamerSimulator.slnx -c Release --no-restore
      # SQL Server runs in Testcontainers; ubuntu-latest has Docker.
      - name: Integration tests
        run: dotnet test PolskiStreamerSimulator.slnx -c Release --no-build --filter-trait "Category=Integration"
```

3. In job `sonar`, step `Begin analysis`, change the two exclusion lines to:

```yaml
          /d:sonar.coverage.exclusions="**/Tools/BatchSimulator/Program.cs,**/*.g.cs,**/Persistence/Migrations/**"
          /d:sonar.exclusions="**/Tests/Golden/**,**/Persistence/Migrations/**"
```

Run: `'/c/Users/charu/AppData/Local/Microsoft/WinGet/Packages/rhysd.actionlint_Microsoft.Winget.Source_8wekyb3d8bbwe/actionlint.exe' .github/workflows/ci.yml` (from the repository root) → exit 0.

- [ ] **Step 2: Require the new check in the ruleset files**

In both `.github/rulesets/main.json` and `.github/rulesets/develop.json`, the `required_status_checks` list becomes:

```json
        "required_status_checks": [
          { "context": "backend", "integration_id": 15368 },
          { "context": "backend-integration", "integration_id": 15368 },
          { "context": "sonar", "integration_id": 15368 },
          { "context": "pr-policy", "integration_id": 15368 }
        ]
```

Check: `python -c "import json; [json.load(open(f, encoding='utf-8')) for f in ['.github/rulesets/main.json', '.github/rulesets/develop.json']]; print('json ok')"` from the repository root.

- [ ] **Step 3: Record the decisions**

Append four rows after TD-89 in `docs/technical-decisions.md` (Confirmed Decisions table):

```markdown
| TD-90 | Scope M2-B to the M1 ruleset `m1-prototype.1` and `content/m1`. Every M2 career is unranked. The 13-week batch plays the fixed `Balanced` policy and records a reason for each plan and event choice. The batch pause before a mandatory decision is built as a ruleset query that the M1 ruleset never triggers. `/admin`, seasons, leaderboard, save slots, recovery keys and 4/52-week batches stay out of M2. | Owner decision, 2026-10-14 (M2-B brainstorming). Previous plan: the architecture lists ranked/unranked creation, saved player strategies and mandatory crises without a milestone. Reason: M1 has no mandatory decisions or seasons, and the slice needs only one playable path. Impact: real mandatory events and player-configured strategies arrive with M3 content; the ranked choice arrives with seasons. A later nickname generator inspired by real streamers is an owner idea outside M2. |
| TD-91 | Persist each career as a state snapshot plus an append-only command log in SQL Server schema `game`: `careers` (canonical state JSON, `state_version` as the concurrency check, state hash, `ranked`, `last_action_at`), `career_secrets` (SHA-256 only), `career_actions` (unique `(career_id, command_id, step)`, stored request and response for repeats, report JSON) and `content_snapshots` (both content files byte-for-byte under their digest). | Owner decision, 2026-10-14 (approach A of: snapshot plus log; log only; relational state). Previous plan: the architecture proposed these tables without a storage model. Reason: one-row reads, exact repeats and full audit replay without migrations for rule changes. Impact: each command is one transaction; a lost update or a duplicate command is detected by the version condition and the unique index. |
| TD-92 | Expose `POST /v1/careers`, `GET /v1/careers/{id}`, `POST /v1/careers/{id}/actions` and `POST /v1/careers/{id}/batches`. The career view carries the next step, so the proposed `GET …/choices` is merged into `GET /v1/careers/{id}`. The career secret is 32 random bytes sent as `Authorization: Bearer`; a wrong secret and an unknown career both return `404 career-not-found`. Errors are Problem Details with a stable `code`. Career creation has no repeat key. The OpenAPI document is committed at `source/backend/WebApp/openapi/v1.json` and checked for drift in CI. The character nickname has 3–20 characters: letters, digits, space, `_`, `-`, `.`. Mapster maps HTTP requests to application messages; career views are built explicitly because they query the rules. | Agent proposal accepted by the owner, 2026-10-14. Previous plan: the technical architecture's proposed API table and an unspecified token mechanism. Reason: one consistent read per step, no CSRF surface, no probing for career IDs, and a generated client in C. Impact: C reads the committed OpenAPI file; an abandoned creation retry leaves an orphan career for the retention sweep. |
| TD-93 | Add the required CI job `backend-integration`, which runs the `Category=Integration` tests against SQL Server in Testcontainers; `backend` excludes them and keeps the 80% gate; `sonar` runs every test. Exclude generated EF Core migrations from coverage and SonarQube Cloud analysis. Deliver M2-B as three PRs (#9, #10, #13 server side). | Agent proposal accepted by the owner, 2026-10-14. Previous plan: TD-86 foresaw `backend-integration` without details. Reason: integration tests need Docker and minutes; unit feedback stays fast. Impact: the `main` and `develop` rulesets require `backend-integration`; developers need Docker only for integration tests. |
```

Append to TD-86's last cell: ` B adds \`backend-integration\` (TD-93).`

In `docs/development-and-delivery.md`, in the paragraph starting `**Current state (M2-A, TD-86).**`, replace `with the required jobs \`backend\`, \`sonar\` and \`pr-policy\`` with `with the required jobs \`backend\`, \`backend-integration\` (TD-93), \`sonar\` and \`pr-policy\``, and replace `Sub-project B adds a SQL Server integration job and sub-project C adds the frontend job.` with `\`backend-integration\` runs the SQL Server tests in Testcontainers, so running them locally needs Docker Desktop; the pre-commit hook and \`backend\` skip them. Sub-project C adds the frontend job.`

In `docs/repository-standards.md`: in the `testconfig.json` row, mention that generated EF Core migrations (`**/Persistence/Migrations/*.cs`) are excluded; in the `.config/dotnet-tools.json` row, add `dotnet-ef` 10.0.13 next to SonarScanner.

In `docs/requirements-traceability.md`, REQ-27: change `Testcontainers and Playwright follow in M2-B/C` to `Testcontainers delivered in M2-B (\`backend-integration\`, TD-93); Playwright follows in M2-C`.

In `README.md`, change `CI (backend, SonarQube Cloud, PR policy)` to `CI (backend, backend integration, SonarQube Cloud, PR policy)`.

Check links: `bash .superpowers/sdd/2026-10-13-m2a-delivery-foundation/check-links.sh` → `links OK` (if the helper is gone, check every new relative link by opening its target).

- [ ] **Step 4: Verify everything**

From `source/backend`: unit run with the gate → PASS with total line coverage ≥ 80%; integration run → PASS; `dotnet format PolskiStreamerSimulator.slnx --verify-no-changes` → exit 0.

- [ ] **Step 5: Commit**

```bash
git add .github docs README.md
git commit -m "ci: run SQL Server integration tests as a required check" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: [external, owner yes] Push, open PR 1, require the check, merge**

1. `git push -u origin feature/m2b-09-persistence`
2. `gh pr create --base develop --title "M2-B persistence: careers, command log and content snapshots in SQL Server (#9)" --body "<summary, test plan, Closes #9, 🤖 line>"`
3. Wait until `backend`, `backend-integration`, `sonar` and `pr-policy` are green.
4. With the owner's yes, apply the ruleset files: `gh api --method PUT repos/adamcharusta/PolskiStreamerSimulator/rulesets/24968586 --input .github/rulesets/main.json` and `gh api --method PUT repos/adamcharusta/PolskiStreamerSimulator/rulesets/24968589 --input .github/rulesets/develop.json`; confirm with `gh api repos/adamcharusta/PolskiStreamerSimulator/rules/branches/develop --jq '.[] | select(.type=="required_status_checks") | [.parameters.required_status_checks[].context]'`.
5. Confirm `gh pr view --json mergeStateStatus` is `CLEAN`, then with the owner's yes `gh pr merge --merge`; `git checkout develop && git pull --ff-only`; watch the `develop` push run until `backend`, `backend-integration` and `sonar` succeed.

---

# PR 2 — Career API (#10)

Start from the updated `develop`: `git checkout develop && git pull --ff-only && git checkout -b feature/m2b-10-career-api`.

### Task 6: Wolverine host, infrastructure wiring and the test hosts

**Files:**
- Modify: `source/backend/Directory.Packages.props`, `source/backend/WebApp/WebApp.csproj`, `source/backend/Application/Application.csproj`, `source/backend/Infrastructure/Infrastructure.csproj`, `source/backend/WebApp/Program.cs`, `source/backend/Tests/WebApp/HealthEndpointTests.cs`
- Create: `source/backend/WebApp/appsettings.json`, `source/backend/WebApp/appsettings.Development.json`
- Create: `source/backend/Application/ApplicationServices.cs`, `source/backend/Application/Content/ICurrentContent.cs`
- Create: `source/backend/Infrastructure/InfrastructureServices.cs`, `source/backend/Infrastructure/Content/ContentOptions.cs`, `source/backend/Infrastructure/Content/DirectoryCurrentContent.cs`, `source/backend/Infrastructure/Persistence/DatabaseOptions.cs`, `source/backend/Infrastructure/Persistence/DatabaseInitializer.cs`
- Create: `source/backend/Tests/Support/ApiFactory.cs`, `source/backend/Tests/Support/SqlApiFactory.cs`
- Test: `source/backend/Tests/WebApp/DatabaseInitializerTests.cs`

**Interfaces:**
- Produces: `ApplicationServices.AddGameApplication(this IServiceCollection)` (registers `WeekEngine` with `M1Ruleset` and `TimeProvider.System`); `ICurrentContent { ContentFiles Files; ContentSnapshot Snapshot; }`; `InfrastructureServices.AddGameInfrastructure(this IServiceCollection, IConfiguration)`; `ContentOptions { Directory }` (section `Content`); `DatabaseOptions { InitializeOnStartup = true; MigrateOnStartup }` (section `Database`); `ApiFactory` (in-memory stores, `Store` property); `SqlApiFactory(string connectionString)`.

- [ ] **Step 1: Add packages**

`Directory.Packages.props` (alphabetical):

```xml
    <PackageVersion Include="Mapster" Version="10.0.13" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.13" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.13" />
    <PackageVersion Include="WolverineFx.Http" Version="6.49.0" />
    <PackageVersion Include="WolverineFx.Http.FluentValidation" Version="6.49.0" />
```

`WebApp.csproj` — add the packages and ship the M1 content next to the binaries:

```xml
  <ItemGroup>
    <PackageReference Include="Mapster" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="WolverineFx.Http" />
    <PackageReference Include="WolverineFx.Http.FluentValidation" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\..\content\m1\*.json" LinkBase="content\m1" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

`Application.csproj`: `<ItemGroup><PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" /></ItemGroup>`.

`Infrastructure.csproj`: `<ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>` (options binding and `IHostedService`).

Run: `dotnet build PolskiStreamerSimulator.slnx` → 0 warnings.

- [ ] **Step 2: Write the failing tests**

`Tests/Support/ApiFactory.cs`:

```csharp
using JasperFx;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The API over in-memory stores: no database and no Docker.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Program ends with RunJasperFxCommands; the test host must still start.
    static ApiFactory() => JasperFxEnvironment.AutoStartHost = true;

    public InMemoryCareerStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", "Server=unused;Database=unused");
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.UseSetting("Content:Directory", TestPaths.ContentM1);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICareerStore>();
            services.AddSingleton<ICareerStore>(Store);
            services.RemoveAll<IContentStore>();
            services.AddSingleton<IContentStore>(new InMemoryContentStore(TestContent.Valid()));
        });
    }
}
```

`Tests/Support/SqlApiFactory.cs`:

```csharp
using JasperFx;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>The API on a real SQL Server database; startup migrates it and stores the current content.</summary>
public sealed class SqlApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    static SqlApiFactory() => JasperFxEnvironment.AutoStartHost = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Content:Directory", TestPaths.ContentM1);
    }
}
```

Replace `Tests/WebApp/HealthEndpointTests.cs` with:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Tests.Support;
using Wolverine;

namespace PolskiStreamerSimulator.Tests.WebApp;

public class HealthEndpointTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await api.CreateClient().GetStringAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal("ok", res);
    }

    [Fact]
    public void The_host_runs_wolverine_over_the_test_stores()
    {
        Assert.NotNull(api.Services.GetRequiredService<IMessageBus>());
        Assert.IsType<InMemoryCareerStore>(api.Services.GetRequiredService<ICareerStore>());
    }
}
```

`Tests/WebApp/DatabaseInitializerTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DatabaseInitializerTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Startup_migrates_the_database_and_stores_the_current_content()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = sql.NewDatabase();
        await using (var api = new SqlApiFactory(connection))
        {
            Assert.Equal("ok", await api.CreateClient().GetStringAsync("/health", ct));
        }

        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        Assert.Equal([TestContent.Valid().Digest], await db.ContentSnapshots.Select(s => s.Digest).ToListAsync(ct));
    }
}
```

- [ ] **Step 3: Run to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*HealthEndpointTests"`
Expected: build errors (`JasperFx`, `InMemoryCareerStore` registration targets, `ICurrentContent` missing).

- [ ] **Step 4: Implement the wiring**

`Application/Content/ICurrentContent.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Content;

/// <summary>The content new careers pin: its files and the parsed snapshot.</summary>
public interface ICurrentContent
{
    ContentFiles Files { get; }

    ContentSnapshot Snapshot { get; }
}
```

`Application/ApplicationServices.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application;

public static class ApplicationServices
{
    /// <summary>Registers the rules engine and the collaborators of the handlers; Wolverine discovers the handlers.</summary>
    public static IServiceCollection AddGameApplication(this IServiceCollection services)
    {
        services.AddSingleton(new WeekEngine(new RulesetRegistry([new M1Ruleset()])));
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
```

`Infrastructure/Content/ContentOptions.cs`:

```csharp
namespace PolskiStreamerSimulator.Infrastructure.Content;

public sealed class ContentOptions
{
    public const string Section = "Content";

    /// <summary>Directory with <c>balance.json</c> and <c>events.json</c> for new careers.</summary>
    public string Directory { get; set; } = "content/m1";
}
```

`Infrastructure/Content/DirectoryCurrentContent.cs`:

```csharp
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>Reads the content files once, on first use.</summary>
public sealed class DirectoryCurrentContent(IOptions<ContentOptions> options) : ICurrentContent
{
    private readonly Lazy<(ContentFiles Files, ContentSnapshot Snapshot)> loaded = new(() => Load(options.Value.Directory));

    public ContentFiles Files => loaded.Value.Files;

    public ContentSnapshot Snapshot => loaded.Value.Snapshot;

    private static (ContentFiles Files, ContentSnapshot Snapshot) Load(string directory)
    {
        var files = new ContentFiles(
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.BalanceFile)),
            File.ReadAllBytes(Path.Combine(directory, JsonContentSource.EventsFile)));
        return (files, JsonContentSource.Parse(files.Balance, files.Events, directory));
    }
}
```

`Infrastructure/Persistence/DatabaseOptions.cs`:

```csharp
namespace PolskiStreamerSimulator.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Store the current content snapshot at startup (and migrate when <see cref="MigrateOnStartup"/>).</summary>
    public bool InitializeOnStartup { get; set; } = true;

    /// <summary>Apply EF Core migrations at startup; on in Development and tests only.</summary>
    public bool MigrateOnStartup { get; set; }
}
```

`Infrastructure/Persistence/DatabaseInitializer.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Infrastructure.Persistence;

/// <summary>Before the API serves requests: optionally migrates, then stores the content new careers pin.</summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<GameDbContext> contexts,
    IContentStore contents,
    ICurrentContent current,
    IOptions<DatabaseOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.InitializeOnStartup)
        {
            return;
        }

        if (options.Value.MigrateOnStartup)
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }

        await contents.EnsureStoredAsync(current.Files, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

`Infrastructure/InfrastructureServices.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Infrastructure.Persistence;

namespace PolskiStreamerSimulator.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddGameInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily: commands such as `openapi` build the host without a database.
        services.AddDbContextFactory<GameDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Game")
            ?? throw new InvalidOperationException("Connection string 'Game' is not set; set ConnectionStrings__Game.")));
        services.AddOptions<ContentOptions>().Bind(configuration.GetSection(ContentOptions.Section));
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.Section));
        services.AddSingleton<ICareerStore, EfCareerStore>();
        services.AddSingleton<IContentStore, EfContentStore>();
        services.AddSingleton<ICurrentContent, DirectoryCurrentContent>();
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
```

`WebApp/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Content": {
    "Directory": "content/m1"
  }
}
```

`WebApp/appsettings.Development.json`:

```json
{
  "Database": {
    "MigrateOnStartup": true
  }
}
```

Replace `WebApp/Program.cs`:

```csharp
using JasperFx;
using PolskiStreamerSimulator.Application;
using PolskiStreamerSimulator.Infrastructure;
using PolskiStreamerSimulator.Infrastructure.Content;
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(ApplicationServices).Assembly);
    options.UseFluentValidation();
});
builder.Services.AddWolverineHttp();
builder.Services.AddGameApplication();
builder.Services.AddGameInfrastructure(builder.Configuration);
builder.Services.PostConfigure<ContentOptions>(options => options.Directory = Path.GetFullPath(options.Directory, AppContext.BaseDirectory));
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/health", () => "ok");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapWolverineEndpoints(options => options.UseFluentValidationProblemDetailMiddleware());

return await app.RunJasperFxCommands(args);

/// <summary>Entry point type; public so tests can host the API.</summary>
public partial class Program;
```

If a namespace does not resolve, find it in the package (`JasperFxEnvironment` and `RunJasperFxCommands` live in the `JasperFx` package that WolverineFx depends on) rather than changing the design.

- [ ] **Step 5: Run the tests**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*HealthEndpointTests"` → 2 passed. Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*DatabaseInitializerTests"` → 1 passed. Unit run with the gate → PASS.

- [ ] **Step 6: Commit**

```bash
git add source/backend
git commit -m "feat(webapp): host Wolverine HTTP over the SQL Server stores" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 7: Creating and reading a career

**Files:**
- Create in `source/backend/Application/Careers/`: `AppResult.cs`, `Contracts.cs`, `ContractJson.cs`, `Decimals.cs`, `ReportViews.cs`, `CareerViewBuilder.cs`, `ISecretHasher.cs`, `CareerAccess.cs`, `CareerReads.cs`, `Nicknames.cs`, `CreateCareer.cs`, `GetCareer.cs`
- Create: `source/backend/Infrastructure/Security/Sha256SecretHasher.cs`
- Modify: `source/backend/Application/ApplicationServices.cs`, `source/backend/Infrastructure/InfrastructureServices.cs`
- Create: `source/backend/Tests/Support/AppTestKit.cs`
- Test: `source/backend/Tests/Application/Careers/CreateCareerTests.cs`, `GetCareerTests.cs`, `CareerViewBuilderTests.cs`; `source/backend/Tests/Infrastructure/Sha256SecretHasherTests.cs`

**Interfaces:**
- Produces (namespace `PolskiStreamerSimulator.Application.Careers`): `ErrorCodes`, `AppError(string Code, string? Detail = null)`, `AppResult<T>` (`IsSuccess`, `Value`, `Error`, `Ok`, `Fail`); the view records in `Contracts.cs`; `ContractJson` (`Options`, `Configure(JsonSerializerOptions)`, `Serialize<T>`, `Deserialize<T>`); `Decimals.Of(Fixed)`; `ReportViews` (`Plan`, `Modifier`, `Roll`, `Line`, `Report`); `CareerViewBuilder(WeekEngine)` with `CareerView Build(Guid, string, CareerState, ContentSnapshot, WeekReportView?)` and `static CareerStatsView Stats(CareerState)`; `ISecretHasher` (`NewSecret`, `Hash`, `Matches`); `CareerAccess(ICareerStore, ISecretHasher).OpenAsync(Guid, string?, CancellationToken)`; `CareerReads.RequireAsync(IContentStore, string, CancellationToken)`, `CareerReads.LastReportAsync(ICareerStore, Guid, CancellationToken)`; `Nicknames` (`IsValid`, `Normalize`, `Rule`); `CreateCareer`, `CreatedCareer`, `CreateCareerHandler`; `GetCareer`, `GetCareerHandler`.
- Produces (tests): `AppTestKit(WeekEngine? engine = null)` with `Store`, `Contents`, `Time`, `Secrets`, `Views`, `Current`, `Engine`, `CreateHandler()`, `GetHandler()`, `NewCareerAsync(...)`; `FixedTimeProvider`; `TestCurrentContent`.

- [ ] **Step 1: Write the failing tests**

`Tests/Support/AppTestKit.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Handlers over in-memory stores, a fixed clock and the repository content.</summary>
public sealed class AppTestKit(WeekEngine? engine = null)
{
    public static readonly DateTimeOffset Start = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public WeekEngine Engine { get; } = engine ?? TestWorld.Engine;

    public InMemoryCareerStore Store { get; } = new();

    public InMemoryContentStore Contents { get; } = new(TestContent.Valid());

    public FixedTimeProvider Time { get; } = new(Start);

    public Sha256SecretHasher Secrets { get; } = new();

    public ICurrentContent Current { get; } = new TestCurrentContent();

    public CareerViewBuilder Views => new(Engine);

    public CareerAccess Access => new(Store, Secrets);

    public CreateCareerHandler CreateHandler() => new(Store, Current, Secrets, Views, Time);

    public GetCareerHandler GetHandler() => new(Access, Store, Contents, Views);

    public async Task<CreatedCareer> NewCareerAsync(string nickname = "Streamer", Background background = Background.DormRoomNewcomer)
        => (await CreateHandler().Handle(
            new CreateCareer(nickname, background, Difficulty.Normal, PronounSet.Woman),
            TestContext.Current.CancellationToken)).Value;
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class TestCurrentContent : ICurrentContent
{
    public ContentFiles Files { get; } = new(TestContentFiles.Balance, TestContentFiles.Events);

    public ContentSnapshot Snapshot => TestContent.Valid();
}
```

`Tests/Application/Careers/CreateCareerTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class CreateCareerTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_career_stores_it_and_returns_a_secret_and_the_first_plan()
    {
        var created = await kit.NewCareerAsync();

        Assert.Equal(43, created.Secret.Length);
        var stored = await kit.Store.FindAsync(created.CareerId, Ct);
        Assert.NotNull(stored);
        Assert.Equal(M1Ruleset.RulesetId, stored.State.RulesetId);
        Assert.Equal(TestContent.Valid().Digest, stored.State.ContentDigest);
        Assert.Equal(AppTestKit.Start, stored.CreatedAt);
        Assert.True(kit.Secrets.Matches(created.Secret, (await kit.Store.FindSecretHashAsync(created.CareerId, Ct))!));
        Assert.Equal(NextStepKind.PlanWeek, created.Career.Next.Kind);
        Assert.NotEmpty(created.Career.Next.Plans);
        Assert.Equal(1, created.Career.Week);
        Assert.Equal(stored.State.StateVersion, created.Career.Version);
        Assert.Equal(TestContent.Valid().Profiles[Background.DormRoomNewcomer].CashPln, created.Career.Stats.CashPln);
        Assert.Null(created.Career.LastReport);
    }

    [Fact]
    public async Task The_nickname_is_stored_trimmed()
    {
        var created = await kit.NewCareerAsync("  Zażółć_1.  ");

        Assert.Equal("Zażółć_1.", created.Career.Nickname);
        Assert.Equal("Zażółć_1.", (await kit.Store.FindAsync(created.CareerId, Ct))!.Nickname);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("😀😀😀")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("   ")]
    [InlineData("name!")]
    public async Task An_invalid_nickname_is_rejected(string nickname)
    {
        var result = await kit.CreateHandler().Handle(new CreateCareer(nickname, Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task An_undefined_background_is_rejected()
    {
        var result = await kit.CreateHandler().Handle(new CreateCareer("Streamer", (Background)99, Difficulty.Normal, PronounSet.Woman), Ct);

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task Two_careers_get_different_ids_and_secrets()
    {
        var first = await kit.NewCareerAsync();
        var second = await kit.NewCareerAsync();

        Assert.NotEqual(first.CareerId, second.CareerId);
        Assert.NotEqual(first.Secret, second.Secret);
    }
}
```

`Tests/Application/Careers/GetCareerTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class GetCareerTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_careers_secret_opens_it()
    {
        var created = await kit.NewCareerAsync();

        var view = (await kit.GetHandler().Handle(new GetCareer(created.CareerId, created.Secret), Ct)).Value;

        Assert.Equal(created.Career.Version, view.Version);
        Assert.Equal("Streamer", view.Nickname);
        Assert.Null(view.LastReport);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_missing_secret_is_rejected(string? secret)
    {
        var created = await kit.NewCareerAsync();

        var result = await kit.GetHandler().Handle(new GetCareer(created.CareerId, secret), Ct);

        Assert.Equal(ErrorCodes.MissingSecret, result.Error.Code);
    }

    [Fact]
    public async Task Another_careers_secret_does_not_open_it()
    {
        var mine = await kit.NewCareerAsync();
        var theirs = await kit.NewCareerAsync();

        var result = await kit.GetHandler().Handle(new GetCareer(mine.CareerId, theirs.Secret), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }

    [Fact]
    public async Task An_unknown_career_is_not_found()
    {
        var created = await kit.NewCareerAsync();

        var result = await kit.GetHandler().Handle(new GetCareer(Guid.NewGuid(), created.Secret), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }
}
```

`Tests/Application/Careers/CareerViewBuilderTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class CareerViewBuilderTests
{
    private static readonly ContentSnapshot Content = TestWorld.Content;
    private readonly CareerViewBuilder views = new(TestWorld.Engine);

    private static CareerState Fresh() => TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 1);

    private static CareerState Started(CareerState s)
        => TestWorld.Engine.StartWeek(s, Content, new WeekPlan(MainAction.Rest, Category.Fps), s.StateVersion).Value;

    private static CareerState AllResolved(CareerState s)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var presented = TestWorld.Engine.PresentEvent(s, Content).Value;
            s = TestWorld.Engine.ResolveEvent(s, Content, presented.Choices[0].ChoiceId, s.StateVersion).Value;
        }

        return s;
    }

    [Fact]
    public void A_new_week_offers_every_plan_with_its_disabled_reason()
    {
        var s = Fresh();

        var view = views.Build(Guid.NewGuid(), "x", s, Content, null);

        Assert.Equal(NextStepKind.PlanWeek, view.Next.Kind);
        Assert.Equal(TestWorld.Engine.LegalPlans(s, Content).Value.Count, view.Next.Plans.Count);
        Assert.Contains(view.Next.Plans, p => p.DisabledReason is null);
        Assert.Null(view.Next.Event);
    }

    [Fact]
    public void A_started_week_shows_the_next_event_with_polish_text_and_odds()
    {
        var view = views.Build(Guid.NewGuid(), "x", Started(Fresh()), Content, null);

        Assert.Equal(NextStepKind.ResolveEvent, view.Next.Kind);
        var shown = view.Next.Event!;
        var definition = Content.Events.Single(e => e.Id == shown.EventId);
        Assert.Equal(definition.TextPl, shown.TextPl);
        Assert.Equal(definition.Choices.Select(c => c.LabelPl), shown.Choices.Select(c => c.LabelPl));
        Assert.All(shown.Choices, c => Assert.InRange(c.Probability, 0m, 1m));
        Assert.Empty(view.Next.Plans);
    }

    [Fact]
    public void A_week_with_every_event_resolved_asks_to_settle()
        => Assert.Equal(NextStepKind.SettleWeek, views.Build(Guid.NewGuid(), "x", AllResolved(Started(Fresh())), Content, null).Next.Kind);

    [Fact]
    public void An_ended_career_has_no_next_step()
    {
        var ended = Fresh() with { Status = CareerStatus.Ended, Ending = EndingReason.Insolvency };

        var view = views.Build(Guid.NewGuid(), "x", ended, Content, null);

        Assert.Equal(NextStepKind.Ended, view.Next.Kind);
        Assert.Equal(EndingReason.Insolvency, view.Ending);
    }

    [Fact]
    public void Fixed_values_convert_exactly()
    {
        Assert.Equal(0.333333m, Decimals.Of(Fixed.FromRatio(1, 3)));
        Assert.Equal(-0.000001m, Decimals.Of(new Fixed(-1)));
        Assert.Equal(100m, Decimals.Of(Fixed.Hundred));
    }
}
```

`Tests/Infrastructure/Sha256SecretHasherTests.cs`:

```csharp
using PolskiStreamerSimulator.Infrastructure.Security;

namespace PolskiStreamerSimulator.Tests.Infrastructure;

public sealed class Sha256SecretHasherTests
{
    private readonly Sha256SecretHasher hasher = new();

    [Fact]
    public void A_new_secret_is_43_base64url_characters_and_unique()
    {
        var first = hasher.NewSecret();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
        Assert.NotEqual(first, hasher.NewSecret());
    }

    [Fact]
    public void Only_the_hashed_secret_matches()
    {
        var secret = hasher.NewSecret();
        var hash = hasher.Hash(secret);

        Assert.Equal(32, hash.Length);
        Assert.True(hasher.Matches(secret, hash));
        Assert.False(hasher.Matches(hasher.NewSecret(), hash));
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*CareerTests"`
Expected: build errors, the application types do not exist.

- [ ] **Step 3: Implement**

`Application/Careers/AppResult.cs`:

```csharp
namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Stable error codes; WebApp maps each to an HTTP status.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation-failed";
    public const string MissingSecret = "missing-secret";
    public const string CareerNotFound = "career-not-found";
    public const string StaleVersion = "stale-version";
    public const string CareerEnded = "career-ended";
    public const string IllegalAction = "illegal-action";
    public const string UnknownChoice = "unknown-choice";
    public const string EventsUnresolved = "events-unresolved";
    public const string NoPendingWeek = "no-pending-week";
    public const string WeekInProgress = "week-in-progress";
    public const string CommandIdReused = "command-id-reused";
    public const string Internal = "internal-error";
}

/// <summary>A rejected request: a stable <see cref="Code"/> and an optional machine-readable detail.</summary>
public sealed record AppError(string Code, string? Detail = null);

/// <summary>Either a value or an <see cref="AppError"/>; handlers return it instead of throwing for rejected requests.</summary>
public sealed class AppResult<T>
{
    private readonly T? value;
    private readonly AppError? error;

    private AppResult(T? value, AppError? error)
    {
        this.value = value;
        this.error = error;
    }

    public bool IsSuccess => error is null;

    public T Value => IsSuccess ? value! : throw new InvalidOperationException($"Result failed with {error!.Code}.");

    public AppError Error => error ?? throw new InvalidOperationException("Result succeeded; there is no error.");

    public static AppResult<T> Ok(T value) => new(value, null);

    public static AppResult<T> Fail(AppError error) => new(default, error);

    public static AppResult<T> Fail(string code, string? detail = null) => new(default, new AppError(code, detail));
}
```

`Application/Careers/Contracts.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>What the player can do next.</summary>
public enum NextStepKind
{
    PlanWeek,
    ResolveEvent,
    SettleWeek,
    Ended,
}

/// <summary>A career as the client sees it at one version; also the stored response of each step.</summary>
public sealed record CareerView(
    Guid CareerId,
    string Nickname,
    long Version,
    CareerStatus Status,
    EndingReason? Ending,
    Background Background,
    Difficulty Difficulty,
    PronounSet Pronouns,
    int Week,
    int LastSettledWeek,
    CareerStatsView Stats,
    NextStepView Next,
    WeekReportView? LastReport);

public sealed record CareerStatsView(
    long CashPln,
    long Followers,
    long LastLiveViewers,
    decimal Drama,
    decimal Credibility,
    decimal Energy,
    decimal Burnout,
    CategoryValuesView Skill,
    CategoryValuesView Boredom,
    EquipmentView Equipment);

public sealed record CategoryValuesView(decimal JustChatting, decimal Fps, decimal Moba, decimal Chess, decimal Gambling);

public sealed record EquipmentView(int Microphone, int Camera, int Computer, int Connection);

/// <summary><see cref="Plans"/> is filled for <see cref="NextStepKind.PlanWeek"/>, <see cref="Event"/> for <see cref="NextStepKind.ResolveEvent"/>.</summary>
public sealed record NextStepView(NextStepKind Kind, IReadOnlyList<PlanOptionView> Plans, EventView? Event);

public sealed record PlanView(MainAction MainAction, Category Category, Category? TrainTarget, EquipmentSlot? UpgradeSlot);

public sealed record PlanOptionView(PlanView Plan, string? DisabledReason);

public sealed record EventView(string EventId, string TitleKey, string TextPl, IReadOnlyList<ChoiceView> Choices);

public sealed record ChoiceView(
    string ChoiceId,
    string LabelKey,
    string LabelPl,
    bool Guaranteed,
    decimal Probability,
    int Threshold,
    IReadOnlyList<ModifierView> Modifiers,
    bool AllowsCritical);

public sealed record ModifierView(string LabelKey, decimal Value);

public sealed record RollView(string EventId, string ChoiceId, int? Roll, int Threshold, IReadOnlyList<ModifierView> Modifiers, RollOutcome Outcome);

public sealed record ReportLineView(string Code, long? AmountPln, decimal? Delta, string? Detail);

public sealed record WeekReportView(
    int Week,
    PlanView Plan,
    IReadOnlyList<RollView> Rolls,
    long LiveViewers,
    long FollowersGained,
    long FollowersLost,
    long FollowersNet,
    IReadOnlyList<ReportLineView> Income,
    IReadOnlyList<ReportLineView> Costs,
    long CashDelta,
    IReadOnlyList<ReportLineView> StatChanges,
    EndingReason? Ending);
```

`Application/Careers/ContractJson.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>The one JSON shape of the contract, for HTTP bodies and for responses stored in the command log.</summary>
public static class ContractJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions options)
        => options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"Stored {typeof(T).Name} JSON was null.");

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
```

`Application/Careers/Decimals.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Application.Careers;

public static class Decimals
{
    /// <summary>Exact conversion: a <see cref="Fixed"/> has six decimal places, which <see cref="decimal"/> holds exactly.</summary>
    public static decimal Of(Fixed value) => (decimal)value.Raw / Fixed.Scale;
}
```

`Application/Careers/ReportViews.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Domain results as contract views.</summary>
public static class ReportViews
{
    public static PlanView Plan(WeekPlan plan) => new(plan.Action, plan.Category, plan.TrainTarget, plan.UpgradeSlot);

    public static ModifierView Modifier(ModifierLine line) => new(line.LabelKey, Decimals.Of(line.Value));

    public static RollView Roll(RollResult roll)
        => new(roll.EventId, roll.ChoiceId, roll.Roll, roll.Threshold, [.. roll.Modifiers.Select(Modifier)], roll.Outcome);

    public static ReportLineView Line(ReportLine line)
        => new(line.Code, line.AmountPln, line.Delta is { } delta ? Decimals.Of(delta) : null, line.Detail);

    public static WeekReportView Report(WeekReport report) => new(
        report.Week.Value,
        Plan(report.Plan),
        [.. report.Rolls.Select(Roll)],
        report.LiveViewers,
        report.FollowersGained,
        report.FollowersLost,
        report.FollowersNet,
        [.. report.Income.Select(Line)],
        [.. report.Costs.Select(Line)],
        report.CashDelta,
        [.. report.StatChanges.Select(Line)],
        report.Ending);
}
```

`Application/Careers/CareerViewBuilder.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>
/// Builds the career view from the state; it queries the career's own ruleset for plans and odds, so it is written
/// out instead of being mapped.
/// </summary>
public sealed class CareerViewBuilder(WeekEngine engine)
{
    public CareerView Build(Guid careerId, string nickname, CareerState s, ContentSnapshot c, WeekReportView? lastReport)
        => new(
            careerId,
            nickname,
            s.StateVersion,
            s.Status,
            s.Ending,
            s.Background,
            s.Difficulty,
            s.Pronouns,
            s.Week.Value,
            s.LastSettledWeek,
            Stats(s),
            Next(s, c),
            lastReport);

    public static CareerStatsView Stats(CareerState s) => new(
        s.CashPln,
        s.Followers,
        s.LastLiveViewers,
        Decimals.Of(s.Drama),
        Decimals.Of(s.Credibility),
        Decimals.Of(s.Energy),
        Decimals.Of(s.Burnout),
        Categories(s.Skill),
        Categories(s.Boredom),
        new EquipmentView(
            s.Equipment[EquipmentSlot.Microphone],
            s.Equipment[EquipmentSlot.Camera],
            s.Equipment[EquipmentSlot.Computer],
            s.Equipment[EquipmentSlot.Connection]));

    private static CategoryValuesView Categories(CategoryValues<Fixed> v) => new(
        Decimals.Of(v[Category.JustChatting]),
        Decimals.Of(v[Category.Fps]),
        Decimals.Of(v[Category.Moba]),
        Decimals.Of(v[Category.Chess]),
        Decimals.Of(v[Category.Gambling]));

    private NextStepView Next(CareerState s, ContentSnapshot c)
    {
        if (s.Status == CareerStatus.Ended)
        {
            return new NextStepView(NextStepKind.Ended, [], null);
        }

        if (s.Pending is null)
        {
            var plans = Expect(engine.LegalPlans(s, c)).Select(l => new PlanOptionView(ReportViews.Plan(l.Plan), l.DisabledReason));
            return new NextStepView(NextStepKind.PlanWeek, [.. plans], null);
        }

        return s.Pending.Resolved.Length < s.Pending.EventIds.Length
            ? new NextStepView(NextStepKind.ResolveEvent, [], Event(Expect(engine.PresentEvent(s, c)), c))
            : new NextStepView(NextStepKind.SettleWeek, [], null);
    }

    private static EventView Event(EventPresentation presented, ContentSnapshot c)
    {
        var definition = c.Events.First(e => string.Equals(e.Id, presented.EventId, StringComparison.Ordinal));
        return new EventView(presented.EventId, definition.TitleKey, definition.TextPl, [.. presented.Choices.Select(o => Choice(o, definition))]);
    }

    private static ChoiceView Choice(ChoiceOdds odds, EventDefinition definition)
    {
        var choice = definition.Choices.First(x => string.Equals(x.Id, odds.ChoiceId, StringComparison.Ordinal));
        return new ChoiceView(
            odds.ChoiceId,
            choice.LabelKey,
            choice.LabelPl,
            odds.Guaranteed,
            Decimals.Of(odds.Probability),
            odds.Threshold,
            [.. odds.Modifiers.Select(ReportViews.Modifier)],
            odds.AllowsCritical);
    }

    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Career view failed: {result.Error.Code} ({result.Error.Detail}).");
}
```

`Application/Careers/ISecretHasher.cs`:

```csharp
namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Creates career secrets and checks them against stored hashes.</summary>
public interface ISecretHasher
{
    string NewSecret();

    byte[] Hash(string secret);

    /// <summary>Compares in constant time.</summary>
    bool Matches(string secret, byte[] expectedHash);
}
```

`Infrastructure/Security/Sha256SecretHasher.cs`:

```csharp
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.Infrastructure.Security;

/// <summary>32 random bytes as base64url; SHA-256 is enough for 256 bits of randomness, no slow hash needed.</summary>
public sealed class Sha256SecretHasher : ISecretHasher
{
    public string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public byte[] Hash(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    public bool Matches(string secret, byte[] expectedHash) => CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);
}
```

`Application/Careers/CareerAccess.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;

namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Opens a career only with its own secret; an unknown career and a wrong secret look the same.</summary>
public sealed class CareerAccess(ICareerStore store, ISecretHasher secrets)
{
    public async Task<AppResult<StoredCareer>> OpenAsync(Guid careerId, string? secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return AppResult<StoredCareer>.Fail(ErrorCodes.MissingSecret);
        }

        var hash = await store.FindSecretHashAsync(careerId, cancellationToken);
        if (hash is null || !secrets.Matches(secret, hash))
        {
            return AppResult<StoredCareer>.Fail(ErrorCodes.CareerNotFound);
        }

        var career = await store.FindAsync(careerId, cancellationToken);
        return career is null ? AppResult<StoredCareer>.Fail(ErrorCodes.CareerNotFound) : AppResult<StoredCareer>.Ok(career);
    }
}
```

`Application/Careers/CareerReads.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Application.Careers;

public static class CareerReads
{
    /// <summary>The career's pinned content; missing content is a server fault.</summary>
    public static async Task<ContentSnapshot> RequireAsync(this IContentStore contents, string digest, CancellationToken cancellationToken)
        => await contents.FindAsync(digest, cancellationToken)
            ?? throw new InvalidOperationException($"Content snapshot {digest} is not stored.");

    /// <summary>The newest stored week report, as the client last saw it.</summary>
    public static async Task<WeekReportView?> LastReportAsync(this ICareerStore store, Guid careerId, CancellationToken cancellationToken)
        => await store.FindLatestReportAsync(careerId, cancellationToken) is { ReportJson: { } json }
            ? ContractJson.Deserialize<WeekReportView>(json)
            : null;
}
```

`Application/Careers/Nicknames.cs`:

```csharp
namespace PolskiStreamerSimulator.Application.Careers;

/// <summary>Character nickname rule (TD-92); not the 1–5-character leaderboard nickname.</summary>
public static class Nicknames
{
    public const int MinLength = 3;
    public const int MaxLength = 20;
    public const string Rule = "Nickname must have 3-20 characters: letters, digits, spaces, '_', '-' or '.'.";

    public static string Normalize(string nickname) => nickname.Trim();

    public static bool IsValid(string? nickname)
    {
        if (nickname is null)
        {
            return false;
        }

        var trimmed = nickname.Trim();
        return trimmed.Length is >= MinLength and <= MaxLength
            && trimmed.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.');
    }
}
```

`Application/Careers/CreateCareer.cs`:

```csharp
using System.Security.Cryptography;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record CreateCareer(string Nickname, Background Background, Difficulty Difficulty, PronounSet Pronouns);

/// <summary>The new career; <see cref="Secret"/> is returned only here and never stored.</summary>
public sealed record CreatedCareer(Guid CareerId, string Secret, CareerView Career);

public sealed class CreateCareerHandler(ICareerStore store, ICurrentContent content, ISecretHasher secrets, CareerViewBuilder views, TimeProvider time)
{
    public async Task<AppResult<CreatedCareer>> Handle(CreateCareer command, CancellationToken cancellationToken)
    {
        if (!Nicknames.IsValid(command.Nickname))
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.ValidationFailed, Nicknames.Rule);
        }

        if (!Enum.IsDefined(command.Background) || !Enum.IsDefined(command.Difficulty) || !Enum.IsDefined(command.Pronouns))
        {
            return AppResult<CreatedCareer>.Fail(ErrorCodes.ValidationFailed, "background, difficulty or pronouns is not defined");
        }

        var snapshot = content.Snapshot;
        var seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        var state = CareerFactory.Create(
            M1Ruleset.RulesetId,
            snapshot.Digest,
            snapshot.Profiles[command.Background],
            command.Background,
            command.Difficulty,
            command.Pronouns,
            seed);
        var now = time.GetUtcNow();
        var id = Guid.CreateVersion7(now);
        var secret = secrets.NewSecret();
        var nickname = Nicknames.Normalize(command.Nickname);
        await store.CreateAsync(new NewCareer(id, nickname, state, secrets.Hash(secret), now), cancellationToken);
        return AppResult<CreatedCareer>.Ok(new CreatedCareer(id, secret, views.Build(id, nickname, state, snapshot, null)));
    }
}
```

`Application/Careers/GetCareer.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record GetCareer(Guid CareerId, string? Secret);

public sealed class GetCareerHandler(CareerAccess access, ICareerStore store, IContentStore contents, CareerViewBuilder views)
{
    public async Task<AppResult<CareerView>> Handle(GetCareer query, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(query.CareerId, query.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<CareerView>.Fail(opened.Error);
        }

        var career = opened.Value;
        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        var lastReport = await store.LastReportAsync(career.Id, cancellationToken);
        return AppResult<CareerView>.Ok(views.Build(career.Id, career.Nickname, career.State, content, lastReport));
    }
}
```

Register the collaborators. In `ApplicationServices.AddGameApplication` add before `return`:

```csharp
        services.AddSingleton<CareerViewBuilder>();
        services.AddSingleton<CareerAccess>();
```

(with `using PolskiStreamerSimulator.Application.Careers;`). In `InfrastructureServices.AddGameInfrastructure` add `services.AddSingleton<ISecretHasher, Sha256SecretHasher>();` (with the `Application.Careers` and `Infrastructure.Security` usings).

- [ ] **Step 4: Run and commit**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*CareerTests"`, `--filter-class "*CareerViewBuilderTests"`, `--filter-class "*Sha256SecretHasherTests"` → all pass. Unit run with the gate → PASS.

```bash
git add source/backend
git commit -m "feat(application): create and read careers with a per-career secret" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 8: Actions with repeats and version checks

**Files:**
- Create: `source/backend/Application/Careers/DomainErrors.cs`, `source/backend/Application/Careers/SubmitAction.cs`
- Modify: `source/backend/Tests/Support/AppTestKit.cs`
- Create: `source/backend/Tests/Support/RacingCareerStore.cs`
- Test: `source/backend/Tests/Application/Careers/SubmitActionTests.cs`

**Interfaces:**
- Consumes: Task 7 types; `ICareerStore` (Task 2).
- Produces: `enum ActionType { StartWeek, ResolveEvent, SettleWeek }`; `ActionInput(long ExpectedVersion, ActionType Type, MainAction? MainAction, Category? Category, Category? TrainTarget, EquipmentSlot? UpgradeSlot, string? ChoiceId)`; `ActionOutcome(CareerView Career, RollView? Roll, WeekReportView? Report)`; `SubmitAction(Guid CareerId, string? Secret, Guid CommandId, ActionInput Input)`; `SubmitActionHandler`; `DomainErrors.ToAppError(DomainError)`; test `AppTestKit.SubmitHandler(ICareerStore? store = null)`, `RacingCareerStore(ICareerStore inner, Func<Task> race)`.

- [ ] **Step 1: Write the failing tests**

Add to `AppTestKit`:

```csharp
    public SubmitActionHandler SubmitHandler(ICareerStore? store = null)
    {
        var s = store ?? Store;
        return new SubmitActionHandler(new CareerAccess(s, Secrets), s, Contents, Engine, Views, Time);
    }
```

(with `using PolskiStreamerSimulator.Application.Careers.Persistence;`).

`Tests/Support/RacingCareerStore.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Runs <paramref name="race"/> once, right before the first append, as a second tab or a retry would.</summary>
public sealed class RacingCareerStore(ICareerStore inner, Func<Task> race) : ICareerStore
{
    private bool raced;

    public Task CreateAsync(NewCareer career, CancellationToken cancellationToken) => inner.CreateAsync(career, cancellationToken);

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindAsync(careerId, cancellationToken);

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindSecretHashAsync(careerId, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
        => inner.FindCommandAsync(careerId, commandId, cancellationToken);

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.FindLatestReportAsync(careerId, cancellationToken);

    public async Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (!raced)
        {
            raced = true;
            await race();
        }

        return await inner.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
    }
}
```

`Tests/Application/Careers/SubmitActionTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class SubmitActionTests
{
    private readonly AppTestKit kit = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionInput Start(long version, MainAction action = MainAction.Rest, EquipmentSlot? slot = null)
        => new(version, ActionType.StartWeek, action, Category.Fps, null, slot, null);

    private static ActionInput Resolve(long version, string choiceId) => new(version, ActionType.ResolveEvent, null, null, null, null, choiceId);

    private static ActionInput Settle(long version) => new(version, ActionType.SettleWeek, null, null, null, null, null);

    private Task<AppResult<ActionOutcome>> SubmitAsync(CreatedCareer career, ActionInput input, Guid? commandId = null, ICareerStore? store = null)
        => kit.SubmitHandler(store).Handle(new SubmitAction(career.CareerId, career.Secret, commandId ?? Guid.NewGuid(), input), Ct);

    private async Task<ActionOutcome> PlayWeekAsync(CreatedCareer career)
    {
        var outcome = (await SubmitAsync(career, Start(career.Career.Version))).Value;
        while (outcome.Career.Next.Kind == NextStepKind.ResolveEvent)
        {
            outcome = (await SubmitAsync(career, Resolve(outcome.Career.Version, outcome.Career.Next.Event!.Choices[0].ChoiceId))).Value;
        }

        return (await SubmitAsync(career, Settle(outcome.Career.Version))).Value;
    }

    [Fact]
    public async Task Starting_a_week_logs_the_command_and_shows_the_first_event()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var outcome = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;

        Assert.Equal(career.Career.Version + 1, outcome.Career.Version);
        Assert.Equal(NextStepKind.ResolveEvent, outcome.Career.Next.Kind);
        Assert.Null(outcome.Roll);
        Assert.Null(outcome.Report);
        var logged = Assert.Single(await kit.Store.FindCommandAsync(career.CareerId, command, Ct));
        Assert.Equal(ActionKind.StartWeek, logged.Kind);
    }

    [Fact]
    public async Task A_full_week_returns_rolls_and_a_report_that_later_reads_show()
    {
        var career = await kit.NewCareerAsync();

        var settled = await PlayWeekAsync(career);

        Assert.Equal(1, settled.Report!.Week);
        Assert.Equal(NextStepKind.PlanWeek, settled.Career.Next.Kind);
        Assert.Equal(1, settled.Career.LastSettledWeek);
        var reread = (await kit.GetHandler().Handle(new GetCareer(career.CareerId, career.Secret), Ct)).Value;
        Assert.Equal(ContractJson.Serialize(settled.Report), ContractJson.Serialize(reread.LastReport));
    }

    [Fact]
    public async Task Resolving_an_event_returns_its_roll()
    {
        var career = await kit.NewCareerAsync();
        var started = (await SubmitAsync(career, Start(career.Career.Version))).Value;
        var shown = started.Career.Next.Event!;

        var resolved = (await SubmitAsync(career, Resolve(started.Career.Version, shown.Choices[0].ChoiceId))).Value;

        Assert.Equal(shown.EventId, resolved.Roll!.EventId);
        Assert.Equal(shown.Choices[0].ChoiceId, resolved.Roll.ChoiceId);
    }

    [Fact]
    public async Task Repeating_a_command_returns_the_stored_result_without_a_second_entry()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var first = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;
        kit.Time.Now = AppTestKit.Start.AddHours(1);

        var repeat = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(repeat));
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
        Assert.Equal(AppTestKit.Start, (await kit.Store.FindAsync(career.CareerId, Ct))!.LastActionAt);
    }

    [Fact]
    public async Task Reusing_a_command_id_with_another_request_is_rejected()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var started = (await SubmitAsync(career, Start(career.Career.Version), command)).Value;

        var reused = await SubmitAsync(career, Resolve(started.Career.Version, started.Career.Next.Event!.Choices[0].ChoiceId), command);

        Assert.Equal(ErrorCodes.CommandIdReused, reused.Error.Code);
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_stale_version_is_rejected_and_nothing_is_logged()
    {
        var career = await kit.NewCareerAsync();

        var result = await SubmitAsync(career, Start(career.Career.Version + 1));

        Assert.Equal(ErrorCodes.StaleVersion, result.Error.Code);
        Assert.Equal(0, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task Rule_rejections_have_stable_codes()
    {
        var career = await kit.NewCareerAsync();
        var v = career.Career.Version;

        Assert.Equal(ErrorCodes.NoPendingWeek, (await SubmitAsync(career, Resolve(v, "anything"))).Error.Code);
        Assert.Equal(ErrorCodes.IllegalAction, (await SubmitAsync(career, Start(v, MainAction.ImproveProduction))).Error.Code);
        var started = (await SubmitAsync(career, Start(v))).Value;
        Assert.Equal(ErrorCodes.UnknownChoice, (await SubmitAsync(career, Resolve(started.Career.Version, "no-such-choice"))).Error.Code);
        Assert.Equal(ErrorCodes.EventsUnresolved, (await SubmitAsync(career, Settle(started.Career.Version))).Error.Code);
    }

    [Fact]
    public async Task A_start_without_a_category_is_rejected()
    {
        var career = await kit.NewCareerAsync();

        var result = await SubmitAsync(career, new ActionInput(career.Career.Version, ActionType.StartWeek, MainAction.Rest, null, null, null, null));

        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public async Task An_ended_career_rejects_actions()
    {
        var career = await kit.NewCareerAsync();
        var state = (await kit.Store.FindAsync(career.CareerId, Ct))!.State;
        var ended = state with { StateVersion = state.StateVersion + 1, Status = CareerStatus.Ended, Ending = EndingReason.EarlyRetirement };
        await kit.Store.AppendAsync(career.CareerId, state.StateVersion, ended, new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null), AppTestKit.Start, Ct);

        var result = await SubmitAsync(career, Start(ended.StateVersion));

        Assert.Equal(ErrorCodes.CareerEnded, result.Error.Code);
    }

    [Fact]
    public async Task A_second_tab_that_loses_the_race_gets_stale_version()
    {
        var career = await kit.NewCareerAsync();
        var racing = new RacingCareerStore(kit.Store, () => SubmitAsync(career, Start(career.Career.Version)));

        var loser = await SubmitAsync(career, Start(career.Career.Version), store: racing);

        Assert.Equal(ErrorCodes.StaleVersion, loser.Error.Code);
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_duplicate_that_loses_the_race_gets_the_winners_result()
    {
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        AppResult<ActionOutcome>? winner = null;
        var racing = new RacingCareerStore(kit.Store, async () => winner = await SubmitAsync(career, Start(career.Career.Version), command));

        var loser = await SubmitAsync(career, Start(career.Career.Version), command, racing);

        Assert.Equal(ContractJson.Serialize(winner!.Value), ContractJson.Serialize(loser.Value));
        Assert.Equal(1, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_wrong_secret_opens_nothing()
    {
        var mine = await kit.NewCareerAsync();
        var theirs = await kit.NewCareerAsync();

        var result = await kit.SubmitHandler().Handle(new SubmitAction(mine.CareerId, theirs.Secret, Guid.NewGuid(), Start(mine.Career.Version)), Ct);

        Assert.Equal(ErrorCodes.CareerNotFound, result.Error.Code);
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*SubmitActionTests"` → build errors (`SubmitActionHandler` missing).

- [ ] **Step 3: Implement**

`Application/Careers/DomainErrors.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

public static class DomainErrors
{
    /// <summary>Maps a rule rejection to its stable code; an unknown ruleset or mismatched content is a server fault.</summary>
    public static AppError ToAppError(DomainError error) => error.Code switch
    {
        DomainErrorCode.IllegalAction => new AppError(ErrorCodes.IllegalAction, error.Detail),
        DomainErrorCode.StaleVersion or DomainErrorCode.WeekAlreadySettled => new AppError(ErrorCodes.StaleVersion, error.Detail),
        DomainErrorCode.NoPendingWeek => new AppError(ErrorCodes.NoPendingWeek, error.Detail),
        DomainErrorCode.EventsUnresolved => new AppError(ErrorCodes.EventsUnresolved, error.Detail),
        DomainErrorCode.CareerEnded => new AppError(ErrorCodes.CareerEnded, error.Detail),
        DomainErrorCode.UnknownChoice => new AppError(ErrorCodes.UnknownChoice, error.Detail),
        _ => throw new InvalidOperationException($"Server fault: {error.Code} ({error.Detail})."),
    };
}
```

`Application/Careers/SubmitAction.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Application.Careers;

public enum ActionType
{
    StartWeek,
    ResolveEvent,
    SettleWeek,
}

/// <summary>One manual step; stored as the request of the command, so a repeat must send exactly the same values.</summary>
public sealed record ActionInput(
    long ExpectedVersion,
    ActionType Type,
    MainAction? MainAction,
    Category? Category,
    Category? TrainTarget,
    EquipmentSlot? UpgradeSlot,
    string? ChoiceId);

public sealed record ActionOutcome(CareerView Career, RollView? Roll, WeekReportView? Report);

public sealed record SubmitAction(Guid CareerId, string? Secret, Guid CommandId, ActionInput Input);

public sealed class SubmitActionHandler(
    CareerAccess access,
    ICareerStore store,
    IContentStore contents,
    WeekEngine engine,
    CareerViewBuilder views,
    TimeProvider time)
{
    public async Task<AppResult<ActionOutcome>> Handle(SubmitAction command, CancellationToken cancellationToken)
    {
        var opened = await access.OpenAsync(command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<ActionOutcome>.Fail(opened.Error);
        }

        var requestJson = ContractJson.Serialize(command.Input);
        if (await RepeatAsync(command, requestJson, cancellationToken) is { } repeat)
        {
            return repeat;
        }

        var career = opened.Value;
        if (career.State.StateVersion != command.Input.ExpectedVersion)
        {
            return AppResult<ActionOutcome>.Fail(ErrorCodes.StaleVersion, $"current-{career.State.StateVersion}");
        }

        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        var step = Execute(career.State, content, command.Input);
        if (!step.IsSuccess)
        {
            return AppResult<ActionOutcome>.Fail(step.Error);
        }

        var (next, roll, report) = step.Value;
        var lastReport = report ?? await store.LastReportAsync(career.Id, cancellationToken);
        var outcome = new ActionOutcome(views.Build(career.Id, career.Nickname, next, content, lastReport), roll, report);
        var entry = new NewAction(
            command.CommandId,
            0,
            KindOf(command.Input.Type),
            requestJson,
            ContractJson.Serialize(outcome),
            report is null ? null : ContractJson.Serialize(report));

        var appended = await store.AppendAsync(career.Id, command.Input.ExpectedVersion, next, entry, time.GetUtcNow(), cancellationToken);
        if (appended == AppendOutcome.Appended)
        {
            return AppResult<ActionOutcome>.Ok(outcome);
        }

        // A concurrent request won: the same command (answer with its stored result) or another one (a stale version).
        return await RepeatAsync(command, requestJson, cancellationToken)
            ?? AppResult<ActionOutcome>.Fail(ErrorCodes.StaleVersion);
    }

    private async Task<AppResult<ActionOutcome>?> RepeatAsync(SubmitAction command, string requestJson, CancellationToken cancellationToken)
    {
        var stored = await store.FindCommandAsync(command.CareerId, command.CommandId, cancellationToken);
        if (stored.Count == 0)
        {
            return null;
        }

        return stored is [{ Step: 0 } only] && string.Equals(only.RequestJson, requestJson, StringComparison.Ordinal)
            ? AppResult<ActionOutcome>.Ok(ContractJson.Deserialize<ActionOutcome>(only.ResponseJson))
            : AppResult<ActionOutcome>.Fail(ErrorCodes.CommandIdReused);
    }

    private AppResult<(CareerState State, RollView? Roll, WeekReportView? Report)> Execute(CareerState s, ContentSnapshot c, ActionInput input)
    {
        switch (input.Type)
        {
            case ActionType.StartWeek:
                if (input.MainAction is not { } action || input.Category is not { } category)
                {
                    return Rejected(new AppError(ErrorCodes.ValidationFailed, "startWeek needs mainAction and category"));
                }

                return Map(engine.StartWeek(s, c, new WeekPlan(action, category, input.TrainTarget, input.UpgradeSlot), input.ExpectedVersion), next => (next, null, null));

            case ActionType.ResolveEvent:
                if (string.IsNullOrEmpty(input.ChoiceId))
                {
                    return Rejected(new AppError(ErrorCodes.ValidationFailed, "resolveEvent needs choiceId"));
                }

                return Map(engine.ResolveEvent(s, c, input.ChoiceId, input.ExpectedVersion), next => (next, ReportViews.Roll(next.Pending!.Resolved[^1]), null));

            case ActionType.SettleWeek:
                return Map(engine.SettleWeek(s, c, input.ExpectedVersion), settled => (settled.State, null, ReportViews.Report(settled.Report)));

            default:
                return Rejected(new AppError(ErrorCodes.ValidationFailed, "unknown action type"));
        }
    }

    private static AppResult<(CareerState State, RollView? Roll, WeekReportView? Report)> Map<T>(
        Result<T> result, Func<T, (CareerState State, RollView? Roll, WeekReportView? Report)> map)
        => result.IsSuccess
            ? AppResult<(CareerState, RollView?, WeekReportView?)>.Ok(map(result.Value))
            : Rejected(DomainErrors.ToAppError(result.Error));

    private static AppResult<(CareerState State, RollView? Roll, WeekReportView? Report)> Rejected(AppError error)
        => AppResult<(CareerState, RollView?, WeekReportView?)>.Fail(error);

    private static ActionKind KindOf(ActionType type) => type switch
    {
        ActionType.StartWeek => ActionKind.StartWeek,
        ActionType.ResolveEvent => ActionKind.ResolveEvent,
        _ => ActionKind.SettleWeek,
    };
}
```

- [ ] **Step 4: Run and commit**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*SubmitActionTests"` → 12 passed. Unit run with the gate → PASS.

```bash
git add source/backend
git commit -m "feat(application): settle manual steps once with repeat-safe commands" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 9: HTTP endpoints, validation and Problem Details

**Files:**
- Create: `source/backend/WebApp/ApiProblems.cs`, `source/backend/WebApp/Careers/Bearer.cs`, `source/backend/WebApp/Careers/Requests.cs`, `source/backend/WebApp/Careers/CareerEndpoints.cs`
- Modify: `source/backend/WebApp/Program.cs`
- Create: `source/backend/Tests/Support/ApiCalls.cs`
- Test: `source/backend/Tests/WebApp/CareerApiTests.cs`

**Interfaces:**
- Consumes: Tasks 6–8.
- Produces: routes `POST /v1/careers` (`createCareer`), `GET /v1/careers/{id}` (`getCareer`), `POST /v1/careers/{id}/actions` (`submitAction`); `CreateCareerRequest`, `ActionRequest` with validators; `ApiProblems.From(AppError)`, `ApiProblems.Configure(ProblemDetailsOptions)`; test helpers `ApiCalls.CreateCareerAsync`, `GetCareerAsync`, `PostActionAsync`, `ActAsync`, `PlayWeekAsync`, `CodeAsync`.

- [ ] **Step 1: Write the failing tests**

`Tests/Support/ApiCalls.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>HTTP helpers for the career API.</summary>
public static class ApiCalls
{
    public static async Task<(Guid Id, string Secret, JsonElement Career)> CreateCareerAsync(this HttpClient client, CancellationToken ct, string nickname = "Streamer")
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname, background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return (body.GetProperty("careerId").GetGuid(), body.GetProperty("secret").GetString()!, body.GetProperty("career"));
    }

    public static Task<HttpResponseMessage> GetCareerAsync(this HttpClient client, Guid id, string? secret, CancellationToken ct)
        => client.SendAsync(Request(HttpMethod.Get, $"/v1/careers/{id}", secret, null), ct);

    public static Task<HttpResponseMessage> PostActionAsync(this HttpClient client, Guid id, string? secret, object body, CancellationToken ct)
        => client.SendAsync(Request(HttpMethod.Post, $"/v1/careers/{id}/actions", secret, body), ct);

    public static async Task<JsonElement> ActAsync(this HttpClient client, Guid id, string secret, object body, CancellationToken ct)
    {
        var response = await client.PostActionAsync(id, secret, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    /// <summary>Plays one manual week: the first enabled plan, the first choice of every event, then settlement.</summary>
    public static async Task<JsonElement> PlayWeekAsync(this HttpClient client, Guid id, string secret, JsonElement career, CancellationToken ct)
    {
        var plan = career.GetProperty("next").GetProperty("plans").EnumerateArray()
            .First(p => p.GetProperty("disabledReason").ValueKind == JsonValueKind.Null)
            .GetProperty("plan");
        var outcome = await client.ActAsync(id, secret, new
        {
            commandId = Guid.NewGuid(),
            expectedVersion = career.GetProperty("version").GetInt64(),
            type = "startWeek",
            mainAction = plan.GetProperty("mainAction").GetString(),
            category = plan.GetProperty("category").GetString(),
            trainTarget = Text(plan, "trainTarget"),
            upgradeSlot = Text(plan, "upgradeSlot"),
        }, ct);
        var view = outcome.GetProperty("career");
        while (view.GetProperty("next").GetProperty("kind").GetString() == "resolveEvent")
        {
            var choiceId = view.GetProperty("next").GetProperty("event").GetProperty("choices")[0].GetProperty("choiceId").GetString();
            outcome = await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = view.GetProperty("version").GetInt64(), type = "resolveEvent", choiceId }, ct);
            view = outcome.GetProperty("career");
        }

        return await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = view.GetProperty("version").GetInt64(), type = "settleWeek" }, ct);
    }

    public static async Task<string?> CodeAsync(this HttpResponseMessage response, CancellationToken ct)
        => (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("code").GetString();

    private static string? Text(JsonElement element, string name)
        => element.GetProperty(name).ValueKind == JsonValueKind.Null ? null : element.GetProperty(name).GetString();

    private static HttpRequestMessage Request(HttpMethod method, string uri, string? secret, object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (secret is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
```

`Tests/WebApp/CareerApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class CareerApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Creating_a_career_returns_201_with_its_location_secret_and_first_plan()
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname = "Streamer", background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("careerId").GetGuid();
        Assert.Equal($"/v1/careers/{id}", response.Headers.Location!.OriginalString);
        Assert.Equal(43, body.GetProperty("secret").GetString()!.Length);
        Assert.Equal("planWeek", body.GetProperty("career").GetProperty("next").GetProperty("kind").GetString());
        Assert.Equal("dormRoomNewcomer", body.GetProperty("career").GetProperty("background").GetString());
    }

    [Fact]
    public async Task An_invalid_nickname_returns_400_validation_failed_with_field_errors()
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname = "x", background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"nickname":"Streamer","background":"wizard","difficulty":"normal","pronouns":"woman"}""")]
    [InlineData("")]
    public async Task Malformed_json_and_unknown_enums_return_validation_failed(string json)
    {
        var response = await client.PostAsync("/v1/careers", new StringContent(json, Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task Reading_without_a_secret_returns_401_missing_secret()
    {
        var (id, _, _) = await client.CreateCareerAsync(Ct);

        var response = await client.GetCareerAsync(id, null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("missing-secret", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task Another_careers_secret_and_an_unknown_id_both_return_404()
    {
        var mine = await client.CreateCareerAsync(Ct);
        var theirs = await client.CreateCareerAsync(Ct);

        var wrongSecret = await client.GetCareerAsync(mine.Id, theirs.Secret, Ct);
        var unknown = await client.GetCareerAsync(Guid.NewGuid(), theirs.Secret, Ct);

        Assert.Equal(HttpStatusCode.NotFound, wrongSecret.StatusCode);
        Assert.Equal("career-not-found", await wrongSecret.CodeAsync(Ct));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("career-not-found", await unknown.CodeAsync(Ct));
    }

    [Fact]
    public async Task The_bearer_scheme_is_case_insensitive()
    {
        var (id, secret, _) = await client.CreateCareerAsync(Ct);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/careers/{id}");
        request.Headers.TryAddWithoutValidation("Authorization", $"bearer {secret}");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_week_can_be_played_and_read_back()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);

        var settled = await client.PlayWeekAsync(id, secret, career, Ct);
        var reread = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(1, settled.GetProperty("report").GetProperty("week").GetInt32());
        Assert.Equal(1, reread.GetProperty("lastReport").GetProperty("week").GetInt32());
        Assert.Equal(settled.GetProperty("career").GetProperty("version").GetInt64(), reread.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Rule_rejections_map_to_their_status_codes()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();

        var stale = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version + 5, type = "settleWeek" }, Ct);
        var noWeek = await client.PostActionAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "resolveEvent", choiceId = "x" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("stale-version", await stale.CodeAsync(Ct));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noWeek.StatusCode);
        Assert.Equal("no-pending-week", await noWeek.CodeAsync(Ct));
    }

    [Fact]
    public async Task A_repeated_request_returns_the_same_body()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var body = new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "startWeek", mainAction = "rest", category = "fps" };

        var first = await (await client.PostActionAsync(id, secret, body, Ct)).Content.ReadAsStringAsync(Ct);
        var second = await (await client.PostActionAsync(id, secret, body, Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Equal(first, second);
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*CareerApiTests"` → the requests return 404 (no endpoints yet).

- [ ] **Step 3: Implement**

`WebApp/ApiProblems.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>Problem Details with a stable <c>code</c> for every API failure.</summary>
public static class ApiProblems
{
    private static readonly Dictionary<string, int> Statuses = new(StringComparer.Ordinal)
    {
        [ErrorCodes.ValidationFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MissingSecret] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.CareerNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.StaleVersion] = StatusCodes.Status409Conflict,
        [ErrorCodes.CareerEnded] = StatusCodes.Status409Conflict,
        [ErrorCodes.IllegalAction] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UnknownChoice] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.EventsUnresolved] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.NoPendingWeek] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.WeekInProgress] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CommandIdReused] = StatusCodes.Status422UnprocessableEntity,
    };

    public static IResult From(AppError error)
    {
        var problem = new ProblemDetails
        {
            Status = Statuses.GetValueOrDefault(error.Code, StatusCodes.Status500InternalServerError),
            Title = error.Code,
            Detail = error.Detail,
        };
        problem.Extensions["code"] = error.Code;
        return TypedResults.Problem(problem);
    }

    /// <summary>Adds the code to Problem Details produced by the framework: validation (400) and unhandled errors (500).</summary>
    public static void Configure(ProblemDetailsOptions options)
        => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            if (problem.Extensions.ContainsKey("code"))
            {
                return;
            }

            if (problem.Status == StatusCodes.Status400BadRequest)
            {
                problem.Extensions["code"] = ErrorCodes.ValidationFailed;
            }
            else if (problem.Status >= StatusCodes.Status500InternalServerError)
            {
                problem.Extensions["code"] = ErrorCodes.Internal;
                problem.Detail = null;
            }
        };
}
```

`WebApp/Careers/Bearer.cs`:

```csharp
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
```

`WebApp/Careers/Requests.cs`:

```csharp
using FluentValidation;
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.WebApp.Careers;

public sealed record CreateCareerRequest(string Nickname, Background Background, Difficulty Difficulty, PronounSet Pronouns);

public sealed class CreateCareerRequestValidator : AbstractValidator<CreateCareerRequest>
{
    public CreateCareerRequestValidator()
    {
        RuleFor(r => r.Nickname).Must(Nicknames.IsValid).WithMessage(Nicknames.Rule);
        RuleFor(r => r.Background).IsInEnum();
        RuleFor(r => r.Difficulty).IsInEnum();
        RuleFor(r => r.Pronouns).IsInEnum();
    }
}

public sealed record ActionRequest(
    Guid CommandId,
    long ExpectedVersion,
    ActionType Type,
    MainAction? MainAction,
    Category? Category,
    Category? TrainTarget,
    EquipmentSlot? UpgradeSlot,
    string? ChoiceId);

public sealed class ActionRequestValidator : AbstractValidator<ActionRequest>
{
    public ActionRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEmpty();
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Type).IsInEnum();
        When(r => r.Type == ActionType.StartWeek, () =>
        {
            RuleFor(r => r.MainAction).NotNull().IsInEnum();
            RuleFor(r => r.Category).NotNull().IsInEnum();
        });
        When(r => r.Type == ActionType.ResolveEvent, () => RuleFor(r => r.ChoiceId).NotEmpty().MaximumLength(64));
    }
}
```

`WebApp/Careers/CareerEndpoints.cs`:

```csharp
using Mapster;
using Microsoft.AspNetCore.Mvc;
using PolskiStreamerSimulator.Application.Careers;
using Wolverine;
using Wolverine.Http;

namespace PolskiStreamerSimulator.WebApp.Careers;

/// <summary>Career endpoints: each maps a request to a handler message and the result to JSON or Problem Details.</summary>
public static class CareerEndpoints
{
    private const string ProblemJson = "application/problem+json";

    [WolverinePost("/v1/careers", OperationId = "createCareer")]
    [ProducesResponseType<CreatedCareer>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public static async Task<IResult> PostCareer(CreateCareerRequest request, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<CreatedCareer>>(request.Adapt<CreateCareer>(), cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/v1/careers/{result.Value.CareerId}", result.Value)
            : ApiProblems.From(result.Error);
    }

    [WolverineGet("/v1/careers/{id}", OperationId = "getCareer")]
    [ProducesResponseType<CareerView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public static async Task<IResult> GetCareerById(
        Guid id, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<AppResult<CareerView>>(new GetCareer(id, Bearer.Secret(authorization)), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }

    [WolverinePost("/v1/careers/{id}/actions", OperationId = "submitAction")]
    [ProducesResponseType<ActionOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static async Task<IResult> PostAction(
        Guid id, ActionRequest request, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = new SubmitAction(id, Bearer.Secret(authorization), request.CommandId, request.Adapt<ActionInput>());
        var result = await bus.InvokeAsync<AppResult<ActionOutcome>>(message, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }
}
```

In `Program.cs` add after `AddWolverineHttp()`:

```csharp
builder.Services.AddProblemDetails(ApiProblems.Configure);
builder.Services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
```

and after `var app = builder.Build();`:

```csharp
app.UseExceptionHandler();
```

with `using PolskiStreamerSimulator.Application.Careers;` and `using PolskiStreamerSimulator.WebApp;`.

If camelCase enum strings are rejected in request bodies (Wolverine not reading the minimal-API JSON options), configure Wolverine's serializer explicitly: `app.MapWolverineEndpoints(options => { options.UseFluentValidationProblemDetailMiddleware(); options.UseSystemTextJsonForSerialization(json => ContractJson.Configure(json)); });`. If a malformed body yields a `500` instead of `400`, the `Malformed_json…` test shows it; map `BadHttpRequestException` to 400 in `UseExceptionHandler` via `builder.Services.AddExceptionHandler<…>()` rather than weakening the test.

- [ ] **Step 4: Run and commit**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*CareerApiTests"` → 11 passed (the theory counts three). Unit run with the gate → PASS.

```bash
git add source/backend
git commit -m "feat(webapp): expose career creation, reading and actions over HTTP" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 10: OpenAPI document and drift check

**Files:**
- Create: `source/backend/WebApp/openapi/v1.json` (generated)
- Modify: `.github/workflows/ci.yml`
- Test: `source/backend/Tests/WebApp/OpenApiDocumentTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class OpenApiDocumentTests
{
    private static readonly string DocumentPath = Path.Combine(TestPaths.RepositoryRoot, "source", "backend", "WebApp", "openapi", "v1.json");

    [Fact]
    public void The_committed_document_describes_the_career_api()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(DocumentPath));
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/v1/careers", out _));
        Assert.True(paths.TryGetProperty("/v1/careers/{id}", out _));
        Assert.True(paths.TryGetProperty("/v1/careers/{id}/actions", out _));
        Assert.Contains("dormRoomNewcomer", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
```

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*OpenApiDocumentTests"` → FAIL (file not found).

- [ ] **Step 2: Generate the document**

Run: `dotnet build PolskiStreamerSimulator.slnx -c Release` then `dotnet run --project WebApp -c Release --no-build -- openapi --document v1 --output WebApp/openapi/v1.json`.
Expected: the file exists and lists the three paths and camelCase enum values. If the `openapi` command is not available, add `Microsoft.Extensions.ApiDescription.Server` 10.0.13 to WebApp with `<OpenApiDocumentsDirectory>openapi</OpenApiDocumentsDirectory>` and `<OpenApiGenerateDocumentsOptions>--file-name v1</OpenApiGenerateDocumentsOptions>`, and record the change in TD-92 and the spec.

Run the test again → PASS.

- [ ] **Step 3: Add the drift check to CI**

In `.github/workflows/ci.yml`, job `backend`, after the `Build` step:

```yaml
      # C generates its client from this file; it must match the code.
      - name: Verify OpenAPI document
        run: |
          dotnet run --project WebApp -c Release --no-build -- openapi --document v1 --output WebApp/openapi/v1.json
          git diff --exit-code -- WebApp/openapi/v1.json
```

Run actionlint (as in Task 5) → exit 0. Locally re-run the two commands from `source/backend` → `git diff` shows no change.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml source/backend/WebApp/openapi/v1.json source/backend/Tests/WebApp/OpenApiDocumentTests.cs
git commit -m "build: commit the OpenAPI document and check it for drift" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 11: HTTP integration tests, documentation and PR 2

**Files:**
- Test: `source/backend/Tests/WebApp/CareerApiSqlTests.cs`
- Modify: `docs/technical-architecture.md`, `docs/technology-stack.md`, `docs/repository-standards.md`, `docs/content-and-experience.md`, `docs/development-and-delivery.md`, `docs/requirements-traceability.md`

- [ ] **Step 1: Write the integration tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CareerApiSqlTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<int> LogCountAsync(string connection, Guid careerId)
    {
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        return await db.CareerActions.CountAsync(a => a.CareerId == careerId, Ct);
    }

    [Fact]
    public async Task A_played_week_survives_an_api_restart()
    {
        var connection = sql.NewDatabase();
        Guid id;
        string secret;
        string before;
        await using (var first = new SqlApiFactory(connection))
        {
            var client = first.CreateClient();
            var created = await client.CreateCareerAsync(Ct);
            (id, secret) = (created.Id, created.Secret);
            await client.PlayWeekAsync(id, secret, created.Career, Ct);
            before = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadAsStringAsync(Ct);
        }

        await using var second = new SqlApiFactory(connection);
        var after = await (await second.CreateClient().GetCareerAsync(id, secret, Ct)).Content.ReadAsStringAsync(Ct);

        Assert.Equal(before, after);
        Assert.Contains("\"lastReport\":{", after, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parallel_repeats_of_one_command_store_one_entry_and_answer_alike()
    {
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var body = new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "startWeek", mainAction = "rest", category = "fps" };

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostActionAsync(id, secret, body, Ct)));
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(Ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(bodies.Distinct(StringComparer.Ordinal));
        Assert.Equal(1, await LogCountAsync(connection, id));
    }

    [Fact]
    public async Task Parallel_commands_at_one_version_let_exactly_one_win()
    {
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var version = career.GetProperty("version").GetInt64();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostActionAsync(
            id, secret, new { commandId = Guid.NewGuid(), expectedVersion = version, type = "startWeek", mainAction = "rest", category = "fps" }, Ct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            Assert.Equal("stale-version", await loser.CodeAsync(Ct));
        }

        Assert.Equal(1, await LogCountAsync(connection, id));
    }

    [Fact]
    public async Task A_secret_opens_only_its_own_career()
    {
        await using var api = new SqlApiFactory(sql.NewDatabase());
        var client = api.CreateClient();
        var mine = await client.CreateCareerAsync(Ct);
        var theirs = await client.CreateCareerAsync(Ct);

        var response = await client.GetCareerAsync(mine.Id, theirs.Secret, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("career-not-found", body.GetProperty("code").GetString());
    }
}
```

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*CareerApiSqlTests"` → 4 passed. (These pass once written because Tasks 6–9 built the behaviour; if one fails, fix the production code, not the test.)

- [ ] **Step 2: Update the documentation**

- `docs/technical-architecture.md` §3 *Proposed public API*: mark `POST /v1/careers`, `GET /v1/careers/{id}` and `POST /v1/careers/{id}/actions` as implemented in M2 (TD-92); replace the `GET /v1/careers/{id}/choices` row with a note that the career view carries the next step and its odds (TD-92); in §2 state that the career secret is a bearer token whose SHA-256 alone is stored (TD-92); in §4 add `career_actions.step` and `report_json` and the `content_snapshots` byte-for-byte storage (TD-91).
- `docs/technology-stack.md` *OpenAPI and client generation*: the document is generated with `dotnet run --project WebApp -- openapi --document v1 --output WebApp/openapi/v1.json` and committed at `source/backend/WebApp/openapi/v1.json`; CI fails on drift (TD-92).
- `docs/repository-standards.md`: add a row for `source/backend/WebApp/openapi/v1.json` (generated, committed, never edited by hand) and for `WebApp/appsettings*.json` (no secrets; the connection string comes from `ConnectionStrings__Game`).
- `docs/content-and-experience.md` §3 *Character creation*: the nickname has 3–20 characters (letters, digits, space, `_`, `-`, `.`), stored trimmed; distinct from the 1–5-character leaderboard nickname (TD-92).
- `docs/development-and-delivery.md` *Local*: run the API with `ConnectionStrings__Game` pointing at a SQL Server instance; Development applies migrations at startup; C adds the one-command Docker Compose setup.
- `docs/requirements-traceability.md`: REQ-02, REQ-12, REQ-13, REQ-16, REQ-28 and REQ-29 name what M2-B delivers on the server (autosave of every step, server-only outcomes, per-career secret without recovery, the command log for replay, careers pinned to their ruleset and content snapshot) and set `In progress (M2)` where a later milestone still owns part of the row.

Check the links (Task 5 step 3).

- [ ] **Step 3: Verify and commit**

From `source/backend`: unit run with the gate → PASS; integration run → PASS; `dotnet format PolskiStreamerSimulator.slnx --verify-no-changes` → exit 0; the OpenAPI drift commands → no diff.

```bash
git add docs source/backend/Tests/WebApp/CareerApiSqlTests.cs
git commit -m "docs: record the M2-B career API" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: [external, owner yes] Push, open PR 2, merge**

`git push -u origin feature/m2b-10-career-api`; `gh pr create --base develop --title "M2-B career API: create, read and act on careers (#10)" --body "<summary, test plan, Closes #10, 🤖 line>"`; wait for all four checks; with the owner's yes `gh pr merge --merge`; sync `develop` and watch its push run.

---

# PR 3 — Thirteen-week batch (#13 server side)

Start from the updated `develop`: `git checkout develop && git pull --ff-only && git checkout -b feature/m2b-13-batch`.

### Task 12: Policy reasons

The built-in policies return a reason with every decision. Their decisions must not change.

**Files:**
- Create: `source/backend/Domain/Simulation/Policies/PolicyDecisions.cs`
- Modify: `source/backend/Domain/Simulation/Policies/IChoicePolicy.cs`, `source/backend/Domain/Simulation/Policies/BuiltInPolicies.cs`, `source/backend/Domain/Simulation/CareerRunner.cs`
- Modify: `source/backend/Tests/Support/RandomLegalPolicy.cs`, `source/backend/Tests/BatchSimulator/BatchRunnerTests.cs` (`BrokenPolicy`), `source/backend/Tests/Domain/Simulation/PolicyTests.cs`
- Test: `source/backend/Tests/Domain/Simulation/PolicyReasonTests.cs`

**Interfaces:**
- Produces (namespace `PolskiStreamerSimulator.Domain.Simulation.Policies`): `PolicyReason(string Code, ImmutableSortedDictionary<string, string> Args)` with `static PolicyReason Of(string code, params (string Name, string Value)[] args)` and `static string Name<TEnum>(TEnum value)`; `PlanDecision(WeekPlan Plan, PolicyReason Reason)`; `ChoiceDecision(string ChoiceId, PolicyReason Reason)`; `IChoicePolicy.ChoosePlan(...) : PlanDecision`, `IChoicePolicy.ChooseEventChoice(...) : ChoiceDecision`.
- Reason codes (plans): `forced-rest`, `train-every-week`, `rest-low-energy` (`energy`, `threshold`), `rest-high-burnout` (`burnout`, `limit`), `upgrade-equipment` (`slot`, `pricePln`, `cashPln`), `train-scheduled` (`everyWeeks`), `manage-community-drama` (`drama`, `limit`), `no-action-default`, `fallback-rest` (`preferred`); every plan reason also carries `category` and `categoryRule` (`bonus`, `least-bored`, `rotation`). Reason codes (events): `gamble-best-odds` (`probability`, `threshold`), `take-guaranteed` (optional `bestProbability`), `only-gambles` (`probability`). `Fixed` values use `Fixed.ToString()` (`20.000000`); integers use the invariant culture; enum values are camelCase.

- [ ] **Step 1: Write the failing tests**

`Tests/Domain/Simulation/PolicyReasonTests.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public sealed class PolicyReasonTests
{
    private static readonly ContentSnapshot Content = TestWorld.Content;

    private static CareerState Career() => TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 7);

    private static PlanDecision Plan(IChoicePolicy policy, CareerState s)
        => policy.ChoosePlan(s, Content, TestWorld.Engine.LegalPlans(s, Content).Value);

    [Fact]
    public void Low_energy_rest_names_the_energy_and_threshold()
    {
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(20) });

        Assert.Equal(MainAction.Rest, decision.Plan.Action);
        Assert.Equal("rest-low-energy", decision.Reason.Code);
        Assert.Equal("20.000000", decision.Reason.Args["energy"]);
        Assert.Equal("30", decision.Reason.Args["threshold"]);
        Assert.Equal("bonus", decision.Reason.Args["categoryRule"]);
        Assert.Equal(PolicyReason.Name(decision.Plan.Category), decision.Reason.Args["category"]);
    }

    [Fact]
    public void Exhaustion_is_forced_rest()
        => Assert.Equal("forced-rest", Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.Zero }).Reason.Code);

    [Fact]
    public void High_burnout_rest_names_the_limit()
    {
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(80), Burnout = Fixed.FromInt(75) });

        Assert.Equal("rest-high-burnout", decision.Reason.Code);
        Assert.Equal("70.000000", decision.Reason.Args["limit"]);
    }

    [Fact]
    public void A_rich_career_upgrades_its_cheapest_slot()
    {
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(80), CashPln = 1_000_000 });

        Assert.Equal(MainAction.ImproveProduction, decision.Plan.Action);
        Assert.Equal("upgrade-equipment", decision.Reason.Code);
        Assert.Equal(PolicyReason.Name(decision.Plan.UpgradeSlot!.Value), decision.Reason.Args["slot"]);
        Assert.Equal("1000000", decision.Reason.Args["cashPln"]);
    }

    [Fact]
    public void Every_fourth_week_trains()
    {
        // Enough cash to train, too little to reach the upgrade reserve.
        var decision = Plan(BuiltInPolicies.Balanced, Career() with { Energy = Fixed.FromInt(80), CashPln = 500, Week = new CareerWeek(4) });

        Assert.Equal(MainAction.TrainCategory, decision.Plan.Action);
        Assert.Equal("train-scheduled", decision.Reason.Code);
        Assert.Equal("4", decision.Reason.Args["everyWeeks"]);
    }

    [Fact]
    public void A_bored_bonus_category_is_switched_and_says_so()
    {
        var s = Career() with { Energy = Fixed.FromInt(80), Boredom = CategoryValues<Fixed>.Uniform(Fixed.FromInt(90)).With(Category.Chess, Fixed.Zero) };

        var decision = Plan(BuiltInPolicies.Balanced, s);

        Assert.Equal(Category.Chess, decision.Plan.Category);
        Assert.Equal("least-bored", decision.Reason.Args["categoryRule"]);
    }

    [Fact]
    public void The_rotator_names_its_rotation()
        => Assert.Equal("rotation", Plan(BuiltInPolicies.Rotator, Career() with { Energy = Fixed.FromInt(80) }).Reason.Args["categoryRule"]);

    [Fact]
    public void Event_choices_name_the_odds_they_weighed()
    {
        var even = new EventPresentation("e", [new ChoiceOdds("bold", false, Fixed.FromRatio(3, 5), 60, [], false), new ChoiceOdds("safe", true, Fixed.One, 0, [], false)]);
        var unlikely = new EventPresentation("e", [new ChoiceOdds("bold", false, Fixed.FromRatio(2, 5), 40, [], false), new ChoiceOdds("safe", true, Fixed.One, 0, [], false)]);
        var gamblesOnly = new EventPresentation("e", [new ChoiceOdds("bold", false, Fixed.FromRatio(2, 5), 40, [], false)]);

        var gamble = BuiltInPolicies.Balanced.ChooseEventChoice(Career(), even);
        var guaranteed = BuiltInPolicies.Balanced.ChooseEventChoice(Career(), unlikely);
        var forced = BuiltInPolicies.Balanced.ChooseEventChoice(Career(), gamblesOnly);

        Assert.Equal(("bold", "gamble-best-odds", "0.600000"), (gamble.ChoiceId, gamble.Reason.Code, gamble.Reason.Args["probability"]));
        Assert.Equal(("safe", "take-guaranteed", "0.400000"), (guaranteed.ChoiceId, guaranteed.Reason.Code, guaranteed.Reason.Args["bestProbability"]));
        Assert.Equal(("bold", "only-gambles"), (forced.ChoiceId, forced.Reason.Code));
    }
}
```

If the Dorm profile's bonus category makes `A_bored_bonus_category_is_switched_and_says_so` pick another category, set the zero-boredom category to one that is not the bonus category and assert that one.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*PolicyReasonTests"` → build errors (`PlanDecision` missing).

- [ ] **Step 3: Implement**

`Domain/Simulation/Policies/PolicyDecisions.cs`:

```csharp
using System.Collections.Immutable;

namespace PolskiStreamerSimulator.Domain.Simulation.Policies;

/// <summary>Why a policy decided: a stable kebab-case code and named values. Clients localize it; Domain holds no copy.</summary>
public sealed record PolicyReason(string Code, ImmutableSortedDictionary<string, string> Args)
{
    public static PolicyReason Of(string code, params (string Name, string Value)[] args)
        => new(code, args.ToImmutableSortedDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    /// <summary>An enum value in the API's camelCase form.</summary>
    public static string Name<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}

public sealed record PlanDecision(WeekPlan Plan, PolicyReason Reason);

public sealed record ChoiceDecision(string ChoiceId, PolicyReason Reason);
```

`IChoicePolicy.cs` — change the two method signatures:

```csharp
    PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal);

    ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e);
```

`BuiltInPolicies.cs` — replace the body of `RulePolicy` from `public string Name => name;` to the end of `ChooseCategory` with (constants and the enum stay):

```csharp
        public string Name => name;

        public PlanDecision ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal)
        {
            var (category, categoryRule) = ChooseCategory(s, c);
            var (preferred, code, args) = Prefer(s, c, category);
            (string Name, string Value)[] common = [("category", PolicyReason.Name(category)), ("categoryRule", categoryRule)];
            return legal.Any(l => l.Plan == preferred && l.DisabledReason is null)
                ? new PlanDecision(preferred, PolicyReason.Of(code, [.. args, .. common]))
                : new PlanDecision(
                    new WeekPlan(MainAction.Rest, category),
                    PolicyReason.Of("fallback-rest", [("preferred", PolicyReason.Name(preferred.Action)), .. common]));
        }

        /// <summary>Highest-probability gamble when it is at least even odds, otherwise the guaranteed response.</summary>
        public ChoiceDecision ChooseEventChoice(CareerState s, EventPresentation e)
        {
            var bestGamble = e.Choices.Where(o => !o.Guaranteed).OrderByDescending(o => o.Probability).FirstOrDefault();
            if (bestGamble is not null && bestGamble.Probability >= GambleThreshold)
            {
                return new ChoiceDecision(
                    bestGamble.ChoiceId,
                    PolicyReason.Of("gamble-best-odds", ("probability", bestGamble.Probability.ToString()), ("threshold", GambleThreshold.ToString())));
            }

            var guaranteed = e.Choices.FirstOrDefault(o => o.Guaranteed);
            if (guaranteed is not null)
            {
                return new ChoiceDecision(
                    guaranteed.ChoiceId,
                    bestGamble is null
                        ? PolicyReason.Of("take-guaranteed")
                        : PolicyReason.Of("take-guaranteed", ("bestProbability", bestGamble.Probability.ToString())));
            }

            var only = bestGamble ?? e.Choices[0];
            return new ChoiceDecision(only.ChoiceId, PolicyReason.Of("only-gambles", ("probability", only.Probability.ToString())));
        }

        public bool ShouldRetire(CareerState s) => false;

        private (WeekPlan Plan, string Code, (string Name, string Value)[] Args) Prefer(CareerState s, ContentSnapshot c, Category category)
        {
            var rest = new WeekPlan(MainAction.Rest, category);
            var forcedRest = s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred;
            (string Name, string Value)[] tiredness = [("energy", s.Energy.ToString()), ("burnout", s.Burnout.ToString())];
            if (grind)
            {
                return forcedRest
                    ? (rest, "forced-rest", tiredness)
                    : (new WeekPlan(MainAction.TrainCategory, category), "train-every-week", []);
            }

            if (forcedRest)
            {
                return (rest, "forced-rest", tiredness);
            }

            if (s.Energy < Fixed.FromInt(restBelowEnergy))
            {
                return (rest, "rest-low-energy", [("energy", s.Energy.ToString()), ("threshold", restBelowEnergy.ToString(CultureInfo.InvariantCulture))]);
            }

            if (s.Burnout > BurnoutLimit)
            {
                return (rest, "rest-high-burnout", [("burnout", s.Burnout.ToString()), ("limit", BurnoutLimit.ToString())]);
            }

            var upgradable = Enum.GetValues<EquipmentSlot>().Where(slot => s.Equipment[slot] < CareerInvariants.MaxEquipmentTier).ToArray();
            if (upgradable.Length > 0)
            {
                var cheapest = upgradable.MinBy(slot => s.Equipment[slot]);
                var price = c.Balance.EquipmentPricePln[s.Equipment[cheapest] + 1];
                if (s.CashPln > UpgradeReserveMultiple * price)
                {
                    return (
                        new WeekPlan(MainAction.ImproveProduction, category, UpgradeSlot: cheapest),
                        "upgrade-equipment",
                        [("slot", PolicyReason.Name(cheapest)), ("pricePln", price.ToString(CultureInfo.InvariantCulture)), ("cashPln", s.CashPln.ToString(CultureInfo.InvariantCulture))]);
                }
            }

            if (s.Week.Value % TrainEveryWeeks == 0)
            {
                return (new WeekPlan(MainAction.TrainCategory, category), "train-scheduled", [("everyWeeks", TrainEveryWeeks.ToString(CultureInfo.InvariantCulture))]);
            }

            return s.Drama > DramaLimit
                ? (new WeekPlan(MainAction.ManageCommunity, category), "manage-community-drama", [("drama", s.Drama.ToString()), ("limit", DramaLimit.ToString())])
                : (new WeekPlan(MainAction.NoAction, category), "no-action-default", []);
        }

        private (Category Category, string Rule) ChooseCategory(CareerState s, ContentSnapshot c)
        {
            if (categoryMode == CategoryMode.Rotate)
            {
                return ((Category)(s.Week.Value % CategoryValues<int>.Count), "rotation");
            }

            var startingSkill = c.Profiles[s.Background].Skill;
            var bonus = Enum.GetValues<Category>().MaxBy(category => startingSkill[category]);
            if (categoryMode == CategoryMode.BonusUnlessBored && s.Boredom[bonus] >= BoredomLimit)
            {
                return (Enum.GetValues<Category>().MinBy(category => s.Boredom[category]), "least-bored");
            }

            return (bonus, "bonus");
        }
```

Add `using System.Globalization;` to `BuiltInPolicies.cs`. The order of checks reproduces the old `if (forcedRest || energy < threshold || burnout > limit)` exactly.

`CareerRunner.cs`: use `policy.ChoosePlan(s, c, legal.Value).Plan` (line 48) and `policy.ChooseEventChoice(s, presentation.Value).ChoiceId` (line 87).

Test doubles: in `Tests/Support/RandomLegalPolicy.cs` return `new PlanDecision(<existing plan>, PolicyReason.Of("random"))` and `new ChoiceDecision(<existing id>, PolicyReason.Of("random"))`; in `BatchRunnerTests.BrokenPolicy` return `new PlanDecision(new WeekPlan(MainAction.ImproveProduction, Category.Fps), PolicyReason.Of("broken"))` and `new ChoiceDecision(e.Choices[0].ChoiceId, PolicyReason.Of("broken"))`. In `PolicyTests.cs`, `Choose` returns `policy.ChoosePlan(...).Plan`, and the three `ChooseEventChoice` asserts compare `.ChoiceId`.

- [ ] **Step 4: Prove the decisions did not change**

Run: unit run with the gate → PASS (golden tests included).

Then reproduce the baseline report's deterministic section (from `source/backend`):

```bash
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --out "$TEMP/m1-baseline-check"
python - "$TEMP/m1-baseline-check/report.md" ../../docs/reports/m1-baseline-simulation.md <<'EOF'
import sys
def deterministic(path):
    text = open(path, encoding="utf-8").read()
    return text[text.index("## Summary"):text.index("## Performance")]
assert deterministic(sys.argv[1]) == deterministic(sys.argv[2]), "the deterministic section changed"
print("baseline unchanged")
EOF
```

Expected: `baseline unchanged`. If the section headers differ, compare the equivalent span by hand; any difference in numbers means a decision changed and must be fixed.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): give every policy decision a reason" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 13: Pause query on rulesets

**Files:**
- Modify: `source/backend/Domain/Rulesets/IRuleset.cs`, `source/backend/Domain/Rulesets/M1Ruleset.cs`, `source/backend/Domain/Simulation/WeekEngine.cs`, `source/backend/Tests/Support/LegacyRuleset.cs`
- Create: `source/backend/Tests/Support/PausingRuleset.cs`
- Test: `source/backend/Tests/Domain/Rulesets/PlayerDecisionTests.cs`

**Interfaces:**
- Produces: `bool IRuleset.RequiresPlayerDecision(CareerState s, ContentSnapshot c)`; `Result<bool> WeekEngine.RequiresPlayerDecision(CareerState s, ContentSnapshot c)`; test `PausingRuleset(int pauseBeforeWeek)` (ID `m1-prototype.1`, M1 rules, pauses before that week).

- [ ] **Step 1: Write the failing tests**

`Tests/Support/PausingRuleset.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>M1 rules that demand a player decision before one week; stands in for the mandatory events of M3.</summary>
public sealed class PausingRuleset(int pauseBeforeWeek) : IRuleset
{
    private readonly M1Ruleset inner = new();

    public string Id => inner.Id;

    public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c) => s.Pending is null && s.Week.Value == pauseBeforeWeek;

    public Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion) => inner.StartWeek(s, c, plan, expectedVersion);

    public IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c) => inner.LegalPlans(s, c);

    public Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c) => inner.PresentEvent(s, c);

    public Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion) => inner.ResolveEvent(s, c, choiceId, expectedVersion);

    public Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion) => inner.SettleWeek(s, c, expectedVersion);

    public Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion) => inner.Retire(s, c, expectedVersion);

    public static WeekEngine Engine(int pauseBeforeWeek) => new(new RulesetRegistry([new PausingRuleset(pauseBeforeWeek)]));
}
```

`Tests/Domain/Rulesets/PlayerDecisionTests.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public sealed class PlayerDecisionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(1040)]
    public void M1_never_requires_a_player_decision(int week)
    {
        var s = TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 3) with { Week = new CareerWeek(week) };

        Assert.False(new M1Ruleset().RequiresPlayerDecision(s, TestWorld.Content));
        Assert.False(TestWorld.Engine.RequiresPlayerDecision(s, TestWorld.Content).Value);
    }

    [Fact]
    public void The_engine_asks_the_careers_own_ruleset()
    {
        var s = TestCareers.For(Background.DormRoomNewcomer, Difficulty.Normal, 3) with { Week = new CareerWeek(5) };

        Assert.True(PausingRuleset.Engine(5).RequiresPlayerDecision(s, TestWorld.Content).Value);
        Assert.False(PausingRuleset.Engine(6).RequiresPlayerDecision(s, TestWorld.Content).Value);
    }
}
```

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*PlayerDecisionTests"` → build errors.

- [ ] **Step 2: Implement**

`IRuleset.cs` — add:

```csharp
    /// <summary>
    /// Whether the next week needs a player decision before it may start; an automated batch stops here without
    /// starting the week.
    /// </summary>
    bool RequiresPlayerDecision(CareerState s, ContentSnapshot c);
```

`M1Ruleset.cs` — add:

```csharp
    /// <summary>The M1 rules have no mandatory decisions (TD-90); forced rest leaves only one legal plan.</summary>
    public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c) => false;
```

`WeekEngine.cs` — add:

```csharp
    public Result<bool> RequiresPlayerDecision(CareerState s, ContentSnapshot c)
    {
        var ruleset = Resolve(s, c);
        return ruleset.IsSuccess ? Result<bool>.Ok(ruleset.Value.RequiresPlayerDecision(s, c)) : Result<bool>.Fail(ruleset.Error);
    }
```

`Tests/Support/LegacyRuleset.cs` — add `public bool RequiresPlayerDecision(CareerState s, ContentSnapshot c) => false;`.

- [ ] **Step 3: Run and commit**

Run: `--filter-class "*PlayerDecisionTests"` → 4 passed; unit run with the gate → PASS.

```bash
git add source/backend
git commit -m "feat(domain): let a ruleset require a player decision before a week" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 14: Batch handler

**Files:**
- Create: `source/backend/Application/Careers/RunBatch.cs`
- Modify: `source/backend/Tests/Support/AppTestKit.cs`
- Create: `source/backend/Tests/Support/FailingAppendStore.cs`
- Test: `source/backend/Tests/Application/Careers/RunBatchTests.cs`

**Interfaces:**
- Consumes: Tasks 7, 8, 12, 13.
- Produces: `enum BatchStopReason { Completed, PausedForDecision, CareerEnded, Interrupted }`; `BatchInput(long ExpectedVersion, int Weeks)`; `ReasonView(string Code, IReadOnlyDictionary<string, string> Args)`; `AutoChoiceView(string EventId, string ChoiceId, ReasonView Reason, RollView Roll)`; `AutoWeekView(int Week, PlanView Plan, ReasonView PlanReason, IReadOnlyList<AutoChoiceView> Choices, WeekReportView Report)`; `BatchSummaryView(int WeeksCommitted, long FollowersNet, long CashDelta, long IncomePln, long CostsPln, CareerStatsView Start, CareerStatsView End)`; `BatchOutcome(int WeeksCommitted, BatchStopReason StopReason, IReadOnlyList<AutoWeekView> Weeks, BatchSummaryView Summary, CareerView Career)`; `AutoWeekRecord(CareerStatsView Before, CareerStatsView After, AutoWeekView Week)`; `RunBatch(Guid CareerId, string? Secret, Guid CommandId, BatchInput Input)`; `RunBatchHandler` with `const int Weeks = 13`; test `AppTestKit.BatchHandler(ICareerStore? store = null)`, `FailingAppendStore(ICareerStore inner, int failOnAppend)`.

- [ ] **Step 1: Write the failing tests**

Add to `AppTestKit`:

```csharp
    public RunBatchHandler BatchHandler(ICareerStore? store = null)
    {
        var s = store ?? Store;
        return new RunBatchHandler(new CareerAccess(s, Secrets), s, Contents, Engine, Views, Time);
    }
```

`Tests/Support/FailingAppendStore.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Throws on the given append (1-based), as a crash in the middle of a batch would.</summary>
public sealed class FailingAppendStore(ICareerStore inner, int failOnAppend) : ICareerStore
{
    private int appends;

    public Task CreateAsync(NewCareer career, CancellationToken cancellationToken) => inner.CreateAsync(career, cancellationToken);

    public Task<StoredCareer?> FindAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindAsync(careerId, cancellationToken);

    public Task<byte[]?> FindSecretHashAsync(Guid careerId, CancellationToken cancellationToken) => inner.FindSecretHashAsync(careerId, cancellationToken);

    public Task<IReadOnlyList<StoredAction>> FindCommandAsync(Guid careerId, Guid commandId, CancellationToken cancellationToken)
        => inner.FindCommandAsync(careerId, commandId, cancellationToken);

    public Task<StoredAction?> FindLatestReportAsync(Guid careerId, CancellationToken cancellationToken)
        => inner.FindLatestReportAsync(careerId, cancellationToken);

    public Task<AppendOutcome> AppendAsync(
        Guid careerId, long expectedVersion, CareerState newState, NewAction action, DateTimeOffset at, CancellationToken cancellationToken)
        => ++appends == failOnAppend
            ? throw new InvalidOperationException("Simulated crash.")
            : inner.AppendAsync(careerId, expectedVersion, newState, action, at, cancellationToken);
}
```

`Tests/Application/Careers/RunBatchTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class RunBatchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<AppResult<BatchOutcome>> RunAsync(AppTestKit kit, CreatedCareer career, long version, Guid? commandId = null, int weeks = 13, ICareerStore? store = null)
        => kit.BatchHandler(store).Handle(new RunBatch(career.CareerId, career.Secret, commandId ?? Guid.NewGuid(), new BatchInput(version, weeks)), Ct);

    private static async Task<long> VersionAsync(AppTestKit kit, CreatedCareer career) => (await kit.Store.FindAsync(career.CareerId, Ct))!.State.StateVersion;

    [Fact]
    public async Task A_batch_commits_thirteen_weeks_with_their_reasons()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var outcome = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal(13, outcome.WeeksCommitted);
        Assert.Equal(BatchStopReason.Completed, outcome.StopReason);
        Assert.Equal(Enumerable.Range(1, 13), outcome.Weeks.Select(w => w.Week));
        Assert.All(outcome.Weeks, w =>
        {
            Assert.False(string.IsNullOrEmpty(w.PlanReason.Code));
            Assert.All(w.Choices, c => Assert.False(string.IsNullOrEmpty(c.Reason.Code)));
        });
        var log = await kit.Store.FindCommandAsync(career.CareerId, command, Ct);
        Assert.Equal(Enumerable.Range(1, 13), log.Select(a => a.Step));
        Assert.Equal(Enumerable.Range(1, 13), log.Select(a => a.Sequence));
        Assert.All(log, a => Assert.Equal(ActionKind.AutoWeek, a.Kind));
        Assert.True(log.Zip(log.Skip(1)).All(p => p.First.StateVersionAfter < p.Second.StateVersionAfter));
        Assert.Equal(13, outcome.Career.LastSettledWeek);
        Assert.Equal(log[^1].StateVersionAfter, outcome.Career.Version);
        Assert.Equal(13, outcome.Career.LastReport!.Week);
        Assert.Equal(outcome.Weeks.Sum(w => w.Report.FollowersNet), outcome.Summary.FollowersNet);
    }

    [Fact]
    public async Task Pausing_before_a_mandatory_week_does_not_consume_it()
    {
        var kit = new AppTestKit(PausingRuleset.Engine(5));
        var career = await kit.NewCareerAsync();

        var outcome = (await RunAsync(kit, career, career.Career.Version)).Value;

        Assert.Equal(4, outcome.WeeksCommitted);
        Assert.Equal(BatchStopReason.PausedForDecision, outcome.StopReason);
        Assert.Equal(5, outcome.Career.Week);
        Assert.Equal(4, outcome.Career.LastSettledWeek);
        Assert.Equal(NextStepKind.PlanWeek, outcome.Career.Next.Kind);
        Assert.Equal(4, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_pause_before_the_first_week_changes_nothing_and_repeats_alike()
    {
        var kit = new AppTestKit(PausingRuleset.Engine(1));
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var first = (await RunAsync(kit, career, career.Career.Version, command)).Value;
        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((0, BatchStopReason.PausedForDecision), (first.WeeksCommitted, first.StopReason));
        Assert.Equal(career.Career.Version, await VersionAsync(kit, career));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(repeat));
        Assert.Equal(0, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task A_batch_stops_when_the_career_ends()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var state = (await kit.Store.FindAsync(career.CareerId, Ct))!.State;
        var late = state with { StateVersion = state.StateVersion + 1, Week = new CareerWeek(1039), LastSettledWeek = 1038 };
        await kit.Store.AppendAsync(career.CareerId, state.StateVersion, late, new NewAction(Guid.NewGuid(), 0, ActionKind.SettleWeek, "{}", "{}", null), AppTestKit.Start, Ct);

        var outcome = (await RunAsync(kit, career, late.StateVersion)).Value;

        Assert.Equal(2, outcome.WeeksCommitted);
        Assert.Equal(BatchStopReason.CareerEnded, outcome.StopReason);
        Assert.Equal(NextStepKind.Ended, outcome.Career.Next.Kind);
    }

    [Fact]
    public async Task A_batch_needs_a_week_boundary_the_current_version_and_an_active_career()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var v = career.Career.Version;

        Assert.Equal(ErrorCodes.StaleVersion, (await RunAsync(kit, career, v + 1)).Error.Code);
        Assert.Equal(ErrorCodes.ValidationFailed, (await RunAsync(kit, career, v, weeks: 4)).Error.Code);
        await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(v, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct);
        Assert.Equal(ErrorCodes.WeekInProgress, (await RunAsync(kit, career, v + 1)).Error.Code);
    }

    [Fact]
    public async Task Repeating_a_finished_batch_returns_the_same_weeks()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();

        var first = (await RunAsync(kit, career, career.Career.Version, command)).Value;
        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(repeat));
        Assert.Equal(13, kit.Store.LogCount(career.CareerId));
    }

    [Fact]
    public async Task Repeating_a_batch_after_the_career_moved_on_plays_no_more_weeks()
    {
        var kit = new AppTestKit(PausingRuleset.Engine(5));
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        await RunAsync(kit, career, career.Career.Version, command);
        var submit = kit.SubmitHandler();
        var view = (await kit.GetHandler().Handle(new GetCareer(career.CareerId, career.Secret), Ct)).Value;
        var step = (await submit.Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(view.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct)).Value;
        while (step.Career.Next.Kind == NextStepKind.ResolveEvent)
        {
            step = (await submit.Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(step.Career.Version, ActionType.ResolveEvent, null, null, null, null, step.Career.Next.Event!.Choices[0].ChoiceId)), Ct)).Value;
        }

        await submit.Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(step.Career.Version, ActionType.SettleWeek, null, null, null, null, null)), Ct);

        var repeat = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((4, BatchStopReason.Interrupted), (repeat.WeeksCommitted, repeat.StopReason));
        Assert.Equal(4, (await kit.Store.FindCommandAsync(career.CareerId, command, Ct)).Count);
        Assert.Equal(5, repeat.Career.LastSettledWeek);
    }

    [Fact]
    public async Task A_crashed_batch_resumes_where_it_stopped()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(kit, career, career.Career.Version, command, store: new FailingAppendStore(kit.Store, 4)));
        Assert.Equal(3, kit.Store.LogCount(career.CareerId));

        var resumed = (await RunAsync(kit, career, career.Career.Version, command)).Value;

        Assert.Equal((13, BatchStopReason.Completed), (resumed.WeeksCommitted, resumed.StopReason));
        Assert.Equal(Enumerable.Range(1, 13), (await kit.Store.FindCommandAsync(career.CareerId, command, Ct)).Select(a => a.Step));
    }

    [Fact]
    public async Task Another_request_in_the_middle_interrupts_the_batch()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var racing = new RacingCareerStore(kit.Store, async () =>
        {
            var current = (await kit.Store.FindAsync(career.CareerId, Ct))!.State;
            await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, Guid.NewGuid(), new ActionInput(current.StateVersion, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct);
        });

        var outcome = (await RunAsync(kit, career, career.Career.Version, store: racing)).Value;

        Assert.Equal((0, BatchStopReason.Interrupted), (outcome.WeeksCommitted, outcome.StopReason));
        Assert.Equal(NextStepKind.ResolveEvent, outcome.Career.Next.Kind);
    }

    [Fact]
    public async Task A_manual_command_id_cannot_be_reused_for_a_batch()
    {
        var kit = new AppTestKit();
        var career = await kit.NewCareerAsync();
        var command = Guid.NewGuid();
        var started = (await kit.SubmitHandler().Handle(new SubmitAction(career.CareerId, career.Secret, command, new ActionInput(career.Career.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null)), Ct)).Value;

        Assert.Equal(ErrorCodes.CommandIdReused, (await RunAsync(kit, career, started.Career.Version, command)).Error.Code);
    }
}
```

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*RunBatchTests"` → build errors.

- [ ] **Step 2: Implement**

`Application/Careers/RunBatch.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;

namespace PolskiStreamerSimulator.Application.Careers;

public enum BatchStopReason
{
    Completed,
    PausedForDecision,
    CareerEnded,
    Interrupted,
}

public sealed record BatchInput(long ExpectedVersion, int Weeks);

public sealed record ReasonView(string Code, IReadOnlyDictionary<string, string> Args);

public sealed record AutoChoiceView(string EventId, string ChoiceId, ReasonView Reason, RollView Roll);

public sealed record AutoWeekView(int Week, PlanView Plan, ReasonView PlanReason, IReadOnlyList<AutoChoiceView> Choices, WeekReportView Report);

public sealed record BatchSummaryView(int WeeksCommitted, long FollowersNet, long CashDelta, long IncomePln, long CostsPln, CareerStatsView Start, CareerStatsView End);

public sealed record BatchOutcome(int WeeksCommitted, BatchStopReason StopReason, IReadOnlyList<AutoWeekView> Weeks, BatchSummaryView Summary, CareerView Career);

/// <summary>What one <c>autoWeek</c> log entry stores, so a repeat can rebuild the batch result.</summary>
public sealed record AutoWeekRecord(CareerStatsView Before, CareerStatsView After, AutoWeekView Week);

public sealed record RunBatch(Guid CareerId, string? Secret, Guid CommandId, BatchInput Input);

/// <summary>
/// Plays up to 13 weeks with the fixed <c>Balanced</c> policy (TD-90), committing each week on its own; stops at an
/// ending, before a week that needs the player, or when another request changes the career.
/// </summary>
public sealed class RunBatchHandler(
    CareerAccess access,
    ICareerStore store,
    IContentStore contents,
    WeekEngine engine,
    CareerViewBuilder views,
    TimeProvider time)
{
    public const int Weeks = 13;

    private static readonly IChoicePolicy Policy = BuiltInPolicies.Balanced;

    public async Task<AppResult<BatchOutcome>> Handle(RunBatch command, CancellationToken cancellationToken)
    {
        if (command.Input.Weeks != Weeks)
        {
            return AppResult<BatchOutcome>.Fail(ErrorCodes.ValidationFailed, "weeks must be 13");
        }

        var opened = await access.OpenAsync(command.CareerId, command.Secret, cancellationToken);
        if (!opened.IsSuccess)
        {
            return AppResult<BatchOutcome>.Fail(opened.Error);
        }

        var career = opened.Value;
        var requestJson = ContractJson.Serialize(command.Input);
        var stored = await store.FindCommandAsync(career.Id, command.CommandId, cancellationToken);
        if (stored.Count > 0 && (stored.Any(a => a.Kind != ActionKind.AutoWeek) || !string.Equals(stored[0].RequestJson, requestJson, StringComparison.Ordinal)))
        {
            return AppResult<BatchOutcome>.Fail(ErrorCodes.CommandIdReused);
        }

        var records = stored.Select(a => ContractJson.Deserialize<AutoWeekRecord>(a.ResponseJson)).ToList();
        var content = await contents.RequireAsync(career.State.ContentDigest, cancellationToken);
        BatchStopReason stop;
        if (stored.Count == 0)
        {
            if (career.State.StateVersion != command.Input.ExpectedVersion)
            {
                return AppResult<BatchOutcome>.Fail(ErrorCodes.StaleVersion, $"current-{career.State.StateVersion}");
            }

            if (career.State.Status == CareerStatus.Ended)
            {
                return AppResult<BatchOutcome>.Fail(ErrorCodes.CareerEnded);
            }

            if (career.State.Pending is not null)
            {
                return AppResult<BatchOutcome>.Fail(ErrorCodes.WeekInProgress);
            }

            stop = await PlayAsync(career, content, command.CommandId, requestJson, records, cancellationToken);
        }
        else if (career.State.StateVersion == stored[^1].StateVersionAfter)
        {
            // Nothing happened since this batch's last week: finish it (after a crash) or report the same stop.
            stop = await PlayAsync(career, content, command.CommandId, requestJson, records, cancellationToken);
        }
        else
        {
            // The career moved on; report what the batch committed and never play further weeks.
            stop = records.Count == Weeks ? BatchStopReason.Completed
                : records[^1].Week.Report.Ending is not null ? BatchStopReason.CareerEnded
                : BatchStopReason.Interrupted;
        }

        var current = await store.FindAsync(career.Id, cancellationToken) ?? throw new InvalidOperationException("The career disappeared.");
        var lastReport = await store.LastReportAsync(career.Id, cancellationToken);
        return AppResult<BatchOutcome>.Ok(Outcome(current, content, records, stop, lastReport));
    }

    private async Task<BatchStopReason> PlayAsync(
        StoredCareer career, ContentSnapshot c, Guid commandId, string requestJson, List<AutoWeekRecord> records, CancellationToken cancellationToken)
    {
        var s = career.State;
        while (true)
        {
            if (s.Status == CareerStatus.Ended)
            {
                return BatchStopReason.CareerEnded;
            }

            if (records.Count == Weeks)
            {
                return BatchStopReason.Completed;
            }

            if (Expect(engine.RequiresPlayerDecision(s, c)))
            {
                return BatchStopReason.PausedForDecision;
            }

            var (next, week) = PlayWeek(s, c);
            var record = new AutoWeekRecord(CareerViewBuilder.Stats(s), CareerViewBuilder.Stats(next), week);
            var entry = new NewAction(commandId, records.Count + 1, ActionKind.AutoWeek, requestJson, ContractJson.Serialize(record), ContractJson.Serialize(week.Report));
            if (await store.AppendAsync(career.Id, s.StateVersion, next, entry, time.GetUtcNow(), cancellationToken) != AppendOutcome.Appended)
            {
                return BatchStopReason.Interrupted;
            }

            records.Add(record);
            s = next;
        }
    }

    private (CareerState State, AutoWeekView Week) PlayWeek(CareerState s, ContentSnapshot c)
    {
        var plan = Policy.ChoosePlan(s, c, Expect(engine.LegalPlans(s, c)));
        s = Expect(engine.StartWeek(s, c, plan.Plan, s.StateVersion));
        var choices = new List<AutoChoiceView>();
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var presented = Expect(engine.PresentEvent(s, c));
            var choice = Policy.ChooseEventChoice(s, presented);
            s = Expect(engine.ResolveEvent(s, c, choice.ChoiceId, s.StateVersion));
            choices.Add(new AutoChoiceView(presented.EventId, choice.ChoiceId, Reason(choice.Reason), ReportViews.Roll(s.Pending!.Resolved[^1])));
        }

        var settled = Expect(engine.SettleWeek(s, c, s.StateVersion));
        var report = ReportViews.Report(settled.Report);
        return (settled.State, new AutoWeekView(report.Week, ReportViews.Plan(plan.Plan), Reason(plan.Reason), choices, report));
    }

    private BatchOutcome Outcome(StoredCareer current, ContentSnapshot c, List<AutoWeekRecord> records, BatchStopReason stop, WeekReportView? lastReport)
    {
        var now = CareerViewBuilder.Stats(current.State);
        var summary = new BatchSummaryView(
            records.Count,
            records.Sum(r => r.Week.Report.FollowersNet),
            records.Sum(r => r.Week.Report.CashDelta),
            records.Sum(r => r.Week.Report.Income.Sum(l => l.AmountPln ?? 0)),
            records.Sum(r => r.Week.Report.Costs.Sum(l => l.AmountPln ?? 0)),
            records.Count > 0 ? records[0].Before : now,
            records.Count > 0 ? records[^1].After : now);
        return new BatchOutcome(records.Count, stop, [.. records.Select(r => r.Week)], summary, views.Build(current.Id, current.Nickname, current.State, c, lastReport));
    }

    private static ReasonView Reason(PolicyReason reason) => new(reason.Code, reason.Args);

    // The policy only picks what the engine offers, so a rejection here is a server fault, not a player error.
    private static T Expect<T>(Result<T> result)
        => result.IsSuccess ? result.Value : throw new InvalidOperationException($"Batch step failed: {result.Error.Code} ({result.Error.Detail}).");
}
```

- [ ] **Step 3: Run and commit**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*RunBatchTests"` → 10 passed. Unit run with the gate → PASS.

```bash
git add source/backend
git commit -m "feat(application): play a 13-week batch that commits each week once" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 15: Batch endpoint, integration test, documentation and PR 3

**Files:**
- Modify: `source/backend/WebApp/Careers/Requests.cs`, `source/backend/WebApp/Careers/CareerEndpoints.cs`, `source/backend/WebApp/openapi/v1.json` (regenerated), `source/backend/Tests/Support/ApiCalls.cs`
- Test: `source/backend/Tests/WebApp/BatchApiTests.cs`, `source/backend/Tests/WebApp/BatchApiSqlTests.cs`, `source/backend/Tests/WebApp/OpenApiDocumentTests.cs`
- Modify: `docs/game-design-document.md`, `docs/gameplay-systems.md`, `docs/technical-architecture.md`, `docs/requirements-traceability.md`

**Interfaces:**
- Produces: `POST /v1/careers/{id}/batches` (`runBatch`); `BatchRequest(Guid CommandId, long ExpectedVersion, int Weeks)` with validator; test helper `ApiCalls.PostBatchAsync(this HttpClient, Guid, string, object, CancellationToken)`.

- [ ] **Step 1: Write the failing tests**

Add to `ApiCalls`:

```csharp
    public static Task<HttpResponseMessage> PostBatchAsync(this HttpClient client, Guid id, string secret, object body, CancellationToken ct)
        => client.SendAsync(Request(HttpMethod.Post, $"/v1/careers/{id}/batches", secret, body), ct);
```

`Tests/WebApp/BatchApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

public sealed class BatchApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient client = api.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_batch_plays_thirteen_weeks_and_the_career_reads_back()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 13 }, Ct);
        var batch = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var reread = await (await client.GetCareerAsync(id, secret, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(13, batch.GetProperty("weeksCommitted").GetInt32());
        Assert.Equal("completed", batch.GetProperty("stopReason").GetString());
        Assert.False(string.IsNullOrEmpty(batch.GetProperty("weeks")[0].GetProperty("planReason").GetProperty("code").GetString()));
        Assert.Equal(14, reread.GetProperty("week").GetInt32());
        Assert.Equal(13, reread.GetProperty("lastReport").GetProperty("week").GetInt32());
    }

    [Fact]
    public async Task Only_thirteen_weeks_are_accepted()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 52 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await response.CodeAsync(Ct));
    }

    [Fact]
    public async Task A_started_week_blocks_a_batch()
    {
        var (id, secret, career) = await client.CreateCareerAsync(Ct);
        var started = await client.ActAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), type = "startWeek", mainAction = "rest", category = "fps" }, Ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = started.GetProperty("career").GetProperty("version").GetInt64(), weeks = 13 }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("week-in-progress", await response.CodeAsync(Ct));
    }
}
```

`Tests/WebApp/BatchApiSqlTests.cs`:

```csharp
using System.Net;
using Microsoft.EntityFrameworkCore;
using PolskiStreamerSimulator.Application.Careers.Persistence;
using PolskiStreamerSimulator.Infrastructure.Persistence;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.WebApp;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BatchApiSqlTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_batch_stores_thirteen_consecutive_weeks()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = sql.NewDatabase();
        await using var api = new SqlApiFactory(connection);
        var client = api.CreateClient();
        var (id, secret, career) = await client.CreateCareerAsync(ct);

        var response = await client.PostBatchAsync(id, secret, new { commandId = Guid.NewGuid(), expectedVersion = career.GetProperty("version").GetInt64(), weeks = 13 }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = new GameDbContext(SqlServerFixture.Options(connection));
        var log = await db.CareerActions.Where(a => a.CareerId == id).OrderBy(a => a.Sequence).ToListAsync(ct);
        Assert.Equal(Enumerable.Range(1, 13), log.Select(a => a.Sequence));
        Assert.All(log, a => Assert.Equal(ActionKind.AutoWeek, a.Kind));
        Assert.True(log.Zip(log.Skip(1)).All(p => p.First.StateVersionAfter < p.Second.StateVersionAfter));
        var stored = await db.Careers.SingleAsync(c => c.Id == id, ct);
        Assert.Equal(log[^1].StateVersionAfter, stored.StateVersion);
        Assert.Equal(log[^1].StateHashAfter, stored.StateHash);
    }
}
```

In `OpenApiDocumentTests`, add `Assert.True(paths.TryGetProperty("/v1/careers/{id}/batches", out _));`.

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*BatchApiTests"` → 404s (no endpoint).

- [ ] **Step 2: Implement the endpoint**

In `Requests.cs`:

```csharp
public sealed record BatchRequest(Guid CommandId, long ExpectedVersion, int Weeks);

public sealed class BatchRequestValidator : AbstractValidator<BatchRequest>
{
    public BatchRequestValidator()
    {
        RuleFor(r => r.CommandId).NotEmpty();
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Weeks).Equal(RunBatchHandler.Weeks);
    }
}
```

In `CareerEndpoints.cs`:

```csharp
    [WolverinePost("/v1/careers/{id}/batches", OperationId = "runBatch")]
    [ProducesResponseType<BatchOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public static async Task<IResult> PostBatch(
        Guid id, BatchRequest request, [FromHeader(Name = "Authorization")] string? authorization, IMessageBus bus, IHostApplicationLifetime lifetime)
    {
        // A batch keeps running when the client disconnects; only shutdown stops it, and each week is already committed.
        var message = new RunBatch(id, Bearer.Secret(authorization), request.CommandId, request.Adapt<BatchInput>());
        var result = await bus.InvokeAsync<AppResult<BatchOutcome>>(message, lifetime.ApplicationStopping, TimeSpan.FromMinutes(1));
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);
    }
```

Regenerate the document (Task 10, step 2 commands) and confirm the new path is in it.

Run: `--filter-class "*BatchApiTests"` → 3 passed; `--filter-class "*OpenApiDocumentTests"` → PASS; `--filter-class "*BatchApiSqlTests"` → PASS; unit run with the gate → PASS; integration run → PASS.

- [ ] **Step 3: Update the documentation**

- `docs/game-design-document.md` decision log: add a 2026-10-14 row — "The M2 slice creates only unranked careers; its 13-week batch plays one fixed strategy (`Balanced`) and shows the reason for each decision; player-configured strategies, mandatory crises and the ranked choice arrive later" — source "Owner, M2-B brainstorming (TD-90)".
- `docs/gameplay-systems.md` automation section: add an M2 note — the slice offers only the 13-week batch with the fixed `Balanced` policy, records a reason code per decision, and stops before a week that needs the player (the M1 rules have no such week) (TD-90).
- `docs/technical-architecture.md` §2: `POST /v1/careers/{id}/batches` implemented for 13 weeks in M2; each week commits on its own; a repeat with the same command ID returns the stored weeks or finishes a crashed batch, and never plays weeks after the career moved on.
- `docs/requirements-traceability.md` REQ-04: `In progress (M2)`; M2 delivers the 13-week batch with recorded reasons and the pause mechanism; 4/52-week batches, the saved player strategy and real mandatory decisions follow.

Check the links.

- [ ] **Step 4: Commit**

```bash
git add source/backend docs
git commit -m "feat(webapp): run a 13-week batch over HTTP" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: [external, owner yes] Push, open PR 3, update #13, merge**

1. `git push -u origin feature/m2b-13-batch`; `gh pr create --base develop --title "M2-B batch: 13 automated weeks with reasons and a pause hook (#13 server side)" --body "<summary, test plan, Refs #13 (UI stays in C), 🤖 line>"`.
2. With the owner's yes, append to issue #13 a `## Delivery split (TD-85)` section: server side (batch endpoint, `Balanced` reasons, pause mechanism) delivered in M2-B; the batch UI, the cumulative report screen and the pause prompt stay in M2-C.
3. Wait for all four checks; with the owner's yes `gh pr merge --merge`; sync `develop`; watch its push run until `backend`, `backend-integration` and `sonar` succeed.

---

## Final review

Before each PR's merge, one fresh reviewer (most capable model) reviews that branch against the spec and this plan; Important findings are fixed on the branch before the merge, and every ruling is reported to the owner.
