# M1 Simulation Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver milestone M1: documentation traceability, a .NET 10 four-layer solution, and a deterministic C# simulation core that plays valid 1,040-week careers, plus a batch simulator with a baseline report and a content-capacity estimate.

**Architecture:** A dependency-free `Domain` project holds fixed-point numerics, PCG32, immutable career state, a versioned ruleset registry and a week state machine (`StartWeek → PresentEvent/ResolveEvent → SettleWeek`). Content and balance constants come from versioned JSON loaded by `Infrastructure` into an immutable `ContentSnapshot`. A console `BatchSimulator` drives many seeded careers through the same functions.

**Tech Stack:** .NET 10 (SDK pinned by `source/backend/global.json`), C# latest, xUnit v3, FsCheck (xUnit v3 integration), coverlet, System.Text.Json source generation, ASP.NET Core minimal host (`WebApp` only).

**Spec:** `docs/superpowers/specs/2026-10-12-m1-simulation-prototype-design.md`

## Global Constraints

- Target `net10.0`; `TreatWarningsAsErrors`, `Nullable`, `ImplicitUsings`, `Deterministic` from the existing `Directory.Build.props`; packages only via `Directory.Packages.props` (central versions, newest stable at implementation time, exact versions pinned).
- `Domain` has **no** `PackageReference` and **no** `ProjectReference`; it must not use `System.Random`, `DateTime`, `DateTimeOffset`, `float`, `double`, `decimal`, `Math.Sqrt`, `Math.Log*`.
- All money is `long` PLN. All fractional rule values are `Fixed` (raw `long`, scale `1_000_000`).
- Root namespace `PolskiStreamerSimulator.<Project>`; file-scoped namespaces; braces required (`.editorconfig`).
- Run every `dotnet` command from `source/backend` (`TD-55`).
- Documentation in English; player-facing working copy in Polish (`AGENTS.md`).
- Every plan-changing decision gets a register/GDD entry with date, source (`Owner decision` / `Agent proposal/default`), previous plan, new decision, reason, impact (`AGENTS.md`).
- Commits end with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01FpsFTH4LKNALDH7xUd9rhh
  ```
  and use the repository owner's git identity (`git -c user.name=adamcharusta -c user.email=charustaadam95@gmail.com commit ...`).
- Branches: see spec §10. Each task header names its branch; create it from the previous task's branch tip when it does not exist yet.
- Verification command used by "run all tests": `dotnet test PolskiStreamerSimulator.slnx` → `Failed: 0`.

## Review Focus

- **Insolvency on the exact boundary:** cash equal to −1,000 PLN after settlement ends the career; −999 does not. Pinned in Task 12.
- **Commands after a terminal state or with a stale version:** every command (`StartWeek`, `ResolveEvent`, `SettleWeek`, `Retire`) returns `CareerEnded`/`StaleVersion` and the RNG state is unchanged. Pinned in Tasks 8, 9 and 12.
- **Huge or zero audiences in fixed-point math:** `Followers = 0` and `Followers = 50_000_000` must not overflow `Fixed` or produce negative viewers. Pinned in Task 11.
- **Content with too few eligible events for some week:** the validator rejects it before any career starts, naming the week. Pinned in Task 7.
- **Unaffordable or negative-cash spend actions:** `ImproveProduction` with insufficient or negative cash returns `IllegalAction` with a reason and leaves state untouched. Pinned in Task 8.

---

### Task 1: README, traceability matrix and M1 decision records (#1)

Branch: `feature/m1-01-readme-traceability` (from `develop`)

**Files:**
- Modify: `README.md`
- Create: `docs/requirements-traceability.md`
- Modify: `docs/technical-decisions.md` (append TD-75…TD-81), `docs/game-design-document.md` (decision log row for the M1 mechanics scope), `docs/validation-and-delivery.md` (link the matrix in §2 and §5), `AGENTS.md` and `CLAUDE.md` (add the two new documents to the map tables)

**Interfaces:** Produces the document paths above, referenced by later tasks.

- [ ] **Step 1:** Rewrite `README.md`: one-paragraph purpose; status "M1 simulation prototype in progress"; repository layout (spec §2 tree, top two levels); links to every `docs/*.md` specification including GDD and validation plan; "Build and test" section with `cd source/backend`, `dotnet build PolskiStreamerSimulator.slnx`, `dotnet test PolskiStreamerSimulator.slnx`, `dotnet run --project Tools/BatchSimulator -- --careers 1000 --seed 42`.
- [ ] **Step 2:** Create `docs/requirements-traceability.md` with columns `ID | Requirement | Source | Acceptance criterion | Issue / milestone | Status`. One row per row of the validation §2 feature matrix (IDs `REQ-01`…`REQ-27` in matrix order) plus `REQ-28` determinism/replay and `REQ-29` old-ruleset continuation (technical-architecture). Issue column: M1 rows link `#3`–`#6`; others name their milestone (M2–M6 per validation §1). Status values: `Planned`, `In progress (M1)`.
- [ ] **Step 3:** Add a "Superseded decisions" table to the same file: `Superseded guidance | Replacement | Where recorded` covering every item in technical-decisions "Superseded Decisions" (offline play, TypeScript simulator, Node replay worker, signed offline starts, local ranked queue, three ranked starts/day, MUI admin, production Docker Compose, manual production approval, `master` branch, direct `develop`→production PR, CLI-only moderation).
- [ ] **Step 4:** Append to `docs/technical-decisions.md` Confirmed Decisions, dated 2026-10-12:
  - TD-75 Owner decision: M1 mechanics are the minimal core (spec §1 row 1); previous plan: validation §1 "one category"; impact: categories exist as data, deferred systems move to M3.
  - TD-76 Owner decision: content/balance seed as versioned JSON under `content/` → immutable `ContentSnapshot` with SHA-256 digest.
  - TD-77 Owner decision: fixed-point `Fixed` (scale 10^6), own `Sqrt`/`Log10`, PCG32; no floating point or `System.Random` in `Domain`.
  - TD-78 Owner decision: batch simulator is a sixth .NET project `source/backend/Tools/BatchSimulator` (outside the four layers; not deployed).
  - TD-79 Owner decision: one `feature/*` branch per M1 issue, stacked in dependency order.
  - TD-80 Agent proposal/default: Wolverine, EF Core, Mapster, FluentValidation and Identity packages are added in M2, not M1.
  - TD-81 Agent proposal/default: Lefthook and GitHub Actions CI are a separate task after M1.
  Add a GDD decision-log row for TD-75.
- [ ] **Step 5:** Verify links: `grep -o '](docs/[^)]*)' README.md | sed 's/](//;s/)//' | xargs -I{} test -f {} && echo OK` → prints `OK` for each link, no errors.
- [ ] **Step 6:** Commit `docs: add README, requirements traceability matrix and M1 decisions (closes #1)`.

---

### Task 2: .NET 10 solution scaffold and layer boundaries (#2)

Branch: `feature/m1-02-dotnet-scaffold` (from Task 1 branch)

**Files:**
- Create: `source/backend/PolskiStreamerSimulator.slnx`
- Create: `source/backend/Domain/Domain.csproj`, `Application/Application.csproj`, `Infrastructure/Infrastructure.csproj`, `WebApp/WebApp.csproj` + `WebApp/Program.cs`, `Tests/Tests.csproj`, `Tools/BatchSimulator/BatchSimulator.csproj` + `Program.cs` (prints usage, exit 0)
- Modify: `source/backend/Directory.Packages.props`
- Create: `source/backend/Tests/Architecture/LayerDependencyTests.cs`, `source/backend/Tests/WebApp/HealthEndpointTests.cs`
- Create: `source/backend/coverlet.runsettings`

**Interfaces:**
- Produces: assembly names `PolskiStreamerSimulator.Domain`, `.Application`, `.Infrastructure`, `.WebApp`, `.Tests`, `.BatchSimulator`; `WebApp` exposes `public partial class Program` and `GET /health` → `200 "ok"`.

- [ ] **Step 0:** Verify toolchain: `dotnet --version` prints `10.0.1xx`. If it fails, install the SDK with `dotnet-install.sh --channel 10.0` and stop to report if hosts are blocked.
- [ ] **Step 1: Write the failing tests**

```csharp
// Tests/Architecture/LayerDependencyTests.cs
[Fact] public void Domain_has_no_references_outside_the_BCL()
{
    var refs = typeof(PolskiStreamerSimulator.Domain.AssemblyMarker).Assembly
        .GetReferencedAssemblies().Select(a => a.Name!).ToArray();
    Assert.All(refs, r => Assert.True(r.StartsWith("System") || r == "netstandard" || r == "mscorlib", r));
}
[Fact] public void Application_does_not_reference_Infrastructure_or_WebApp() { /* referenced names exclude both */ }
[Fact] public void Domain_IL_does_not_use_forbidden_types()
{
    // Scan Domain types' fields, properties, method parameters/returns and method bodies' referenced
    // members (via MethodBody IL tokens resolved with Module.ResolveMember) for:
    // System.Random, System.DateTime, System.DateTimeOffset, System.Single, System.Double, System.Decimal,
    // System.Math.Sqrt, System.Math.Log, System.Math.Log10. Assert none found (list offenders).
}
// Tests/WebApp/HealthEndpointTests.cs  (Microsoft.AspNetCore.Mvc.Testing)
[Fact] public async Task Health_returns_ok()
{
    await using var app = new WebApplicationFactory<Program>();
    var res = await app.CreateClient().GetStringAsync("/health");
    Assert.Equal("ok", res);
}
```

- [ ] **Step 2:** Run `dotnet test PolskiStreamerSimulator.slnx` → fails to compile (no projects / `AssemblyMarker` missing).
- [ ] **Step 3:** Create projects. `Domain` has `internal`-free `public static class AssemblyMarker {}` and no references. `Application` → Domain. `Infrastructure` → Application, Domain. `WebApp` (`Microsoft.NET.Sdk.Web`) → all three; `Program.cs` maps `/health` and ends with `public partial class Program;`. `Tests` → all projects plus BatchSimulator; packages `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`, `FsCheck.Xunit.v3`, `coverlet.collector`, `Microsoft.AspNetCore.Mvc.Testing`. `BatchSimulator` (`Exe`) → Domain, Infrastructure. Add all six to the `.slnx`. Add `PackageVersion` entries to `Directory.Packages.props` with exact versions.
- [ ] **Step 4:** `coverlet.runsettings`: Cobertura format, exclude `[PolskiStreamerSimulator.Tests]*`, `[PolskiStreamerSimulator.BatchSimulator]*Program`, generated `*.g.cs`.
- [ ] **Step 5:** Run `dotnet test PolskiStreamerSimulator.slnx` → `Passed: 4, Failed: 0`; `dotnet format PolskiStreamerSimulator.slnx --verify-no-changes` → exit 0.
- [ ] **Step 6:** Commit `build: scaffold .NET 10 solution with four layers, tests and batch tool (closes #2)`.

---

### Task 3: Fixed-point numerics (#3)

Branch: `feature/m1-03-career-state` (from Task 2 branch)

**Files:**
- Create: `Domain/Numerics/Fixed.cs`, `Domain/Numerics/FixedMath.cs`
- Test: `Tests/Domain/Numerics/FixedTests.cs`, `Tests/Domain/Numerics/FixedMathTests.cs`

**Interfaces:**
- Produces: `public readonly record struct Fixed(long Raw) : IComparable<Fixed>` with `const long Scale = 1_000_000`; `static Fixed Zero, One, Hundred`; `static Fixed FromInt(long v)`; `static Fixed FromRatio(long numerator, long denominator)`; operators `+ - * /` (checked; `*` and `/` round half away from zero), `< > <= >=`, unary `-`; `Fixed Clamp(Fixed min, Fixed max)`; `long RoundToLong()` (half away from zero); `override string ToString()` → `"12.500000"`.
- Produces: `public static class FixedMath` with `Fixed Sqrt(Fixed x)` (x ≥ 0, floor of exact root at scale), `Fixed Log10(Fixed x)` (x > 0; `ArgumentOutOfRangeException` otherwise), `Fixed Min(Fixed a, Fixed b)`, `Fixed Max(Fixed a, Fixed b)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Multiplication_rounds_half_away_from_zero()
    => Assert.Equal(new Fixed(1), new Fixed(1) * Fixed.FromRatio(1, 2)); // 0.0000005 -> 0.000001
[Fact] public void FromRatio_and_ToString() => Assert.Equal("0.333333", Fixed.FromRatio(1, 3).ToString());
[Fact] public void RoundToLong_half_away() { Assert.Equal(3, Fixed.FromRatio(5, 2).RoundToLong()); Assert.Equal(-3, Fixed.FromRatio(-5, 2).RoundToLong()); }
[Fact] public void Overflow_throws() => Assert.Throws<OverflowException>(() => new Fixed(long.MaxValue) + Fixed.One);
[Theory, InlineData(0, 0), InlineData(4, 2_000_000), InlineData(2, 1_414_213), InlineData(1_000_000, 1_000_000_000)]
public void Sqrt_floor(long input, long expectedRaw) => Assert.Equal(expectedRaw, FixedMath.Sqrt(Fixed.FromInt(input)).Raw);
[Theory, InlineData(1, 0), InlineData(10, 1_000_000), InlineData(2, 301_030), InlineData(1_000_001, 6_000_000), InlineData(10_000_001, 7_000_000)]
public void Log10_within_1e5(long input, long expectedRaw) => Assert.InRange(FixedMath.Log10(Fixed.FromInt(input)).Raw, expectedRaw - 10, expectedRaw + 10);
[Property] public bool Log10_is_monotonic(PositiveInt a, PositiveInt b)
    => a.Get <= b.Get == (FixedMath.Log10(Fixed.FromInt(a.Get)) <= FixedMath.Log10(Fixed.FromInt(b.Get))) || a.Get > b.Get;
```

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Numerics"` → compile failure.
- [ ] **Step 3:** Implement `Fixed`. Multiplication via `Int128` intermediate then divide by `Scale` with half-away rounding; division `(Int128)a.Raw * Scale / b.Raw` with the same rounding; cast back with `checked`.
- [ ] **Step 4:** Implement `FixedMath.Sqrt` with integer Newton on `UInt128 n = (UInt128)raw * Scale` (floor isqrt). Implement `Log10`: normalize `x = m · 2^k` with `m` in [1, 2) using bit length of `Raw`; `log2(m)` by 30 iterations of the squaring method on `Int128` fixed values at scale 2^40; `log10(x) = (k + log2(m)) · log10(2)` with `log10(2) = 0.30102999566398` as a scale-10^12 constant; round to scale 10^6.
- [ ] **Step 5:** Run the filter → all pass.
- [ ] **Step 6:** Commit `feat(domain): add Fixed fixed-point type with deterministic sqrt and log10`.

---

### Task 4: PCG32 generator (#3)

Branch: `feature/m1-03-career-state`

**Files:**
- Create: `Domain/Numerics/Pcg32.cs`
- Test: `Tests/Domain/Numerics/Pcg32Tests.cs`

**Interfaces:**
- Produces: `public readonly record struct Pcg32(ulong State, ulong Increment)`; `static Pcg32 Create(ulong seed, ulong stream)` (reference PCG seeding: `inc = (stream << 1) | 1`, step, add seed, step); `(uint Value, Pcg32 Next) NextUInt()` (PCG-XSH-RR, multiplier `6364136223846793005`); `(int Value, Pcg32 Next) NextInt(int minInclusive, int maxInclusive)` (Lemire/rejection, no modulo bias; throws if min > max); `(Fixed Value, Pcg32 Next) NextFixed(Fixed minInclusive, Fixed maxInclusive)` (uniform over raw values).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Matches_pcg32_reference_vector()
{   // pcg32-demo: seed 42, stream 54
    var rng = Pcg32.Create(42, 54);
    uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
    foreach (var e in expected) { (var v, rng) = rng.NextUInt(); Assert.Equal(e, v); }
}
[Fact] public void NextInt_stays_in_range_and_hits_both_ends()
{   var rng = Pcg32.Create(1, 1); var seen = new HashSet<int>();
    for (var i = 0; i < 10_000; i++) { (var v, rng) = rng.NextInt(1, 100); Assert.InRange(v, 1, 100); seen.Add(v); }
    Assert.Equal(100, seen.Count); }
[Fact] public void Same_seed_same_sequence() { /* two generators, 1,000 draws equal */ }
```

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Pcg32"` → compile failure.
- [ ] **Step 3:** Implement `Pcg32` as specified.
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(domain): add PCG32 deterministic random generator`.

---

### Task 5: Career state, character, factory and invariants (#3)

Branch: `feature/m1-03-career-state`

**Files:**
- Create: `Domain/Careers/Enums.cs` (Background, Difficulty, PronounSet, Category, EquipmentSlot, CareerStatus, EndingReason), `Domain/Careers/CareerWeek.cs`, `Domain/Careers/CategoryValues.cs`, `Domain/Careers/CareerMetrics.cs`, `Domain/Careers/CareerState.cs`, `Domain/Careers/StartingProfile.cs`, `Domain/Careers/CareerFactory.cs`, `Domain/Careers/CareerInvariants.cs`
- Test: `Tests/Domain/Careers/CareerFactoryTests.cs`, `Tests/Domain/Careers/CareerInvariantsTests.cs`, `Tests/Domain/Careers/CareerWeekTests.cs`

**Interfaces:**
- Produces enums: `Background { DormRoomNewcomer, LocalMemeCreator, ChessClubRegular, IndustryInsider }`, `Difficulty { Easy, Normal }`, `PronounSet { Woman, Man, Nonbinary }`, `Category { JustChatting, Fps, Moba, Chess, Gambling }`, `EquipmentSlot { Microphone, Camera, Computer, Connection }`, `CareerStatus { Active, Ended }`, `EndingReason { Completed, Insolvency, EarlyRetirement }`.
- Produces: `readonly record struct CareerWeek(int Value)` (ctor throws outside 1–1040; `const int Last = 1040`; `bool IsLast`; `CareerWeek Next()`).
- Produces: `sealed record CategoryValues<T>(ImmutableArray<T> Values)` with indexer by `Category` and `With(Category, T)`; equality by sequence.
- Produces: `sealed record CareerMetrics(long Streams, long TotalLiveViews, ImmutableDictionary<string,long> IncomeBySource, ImmutableDictionary<string,long> CostsBySource, CategoryValues<int> WeeksPerCategory, int EventsResolved, int Criticals, int PeakFollowersMilestone)` with `static Empty`.
- Produces: `sealed record CareerState` with properties `string RulesetId, string ContentDigest, long StateVersion, Background Background, Difficulty Difficulty, PronounSet Pronouns, CareerWeek Week, Pcg32 Rng, long CashPln, long Followers, long LastLiveViewers, Fixed Drama, Fixed Credibility, Fixed Energy, Fixed Burnout, CategoryValues<Fixed> Skill, CategoryValues<Fixed> Boredom, CategoryValues<int> InactiveWeeks, ImmutableDictionary<EquipmentSlot,int> Equipment, ImmutableDictionary<string,int> EventCooldowns, ImmutableHashSet<string> Achievements, CareerMetrics Metrics, CareerStatus Status, EndingReason? Ending, ScoreBreakdown? FinalScore, PendingWeek? Pending`. (`ScoreBreakdown` and `PendingWeek` are defined in Tasks 12 and 8; create them now as empty `sealed record`s in their final namespaces `Domain.Scoring` and `Domain.Simulation` so the state compiles.)
- Produces: `sealed record StartingProfile(long CashPln, long Followers, Fixed Drama, Fixed Credibility, Fixed Energy, CategoryValues<Fixed> Skill, ImmutableDictionary<EquipmentSlot,int> Equipment, Fixed ScoreMultiplier)`.
- Produces: `static CareerState CareerFactory.Create(string rulesetId, string contentDigest, StartingProfile profile, Background background, Difficulty difficulty, PronounSet pronouns, ulong seed)` → week 1, `StateVersion 0`, burnout 0, boredom 0, `Rng = Pcg32.Create(seed, 0xDA3E39CB94B95BDB)`.
- Produces: `static IReadOnlyList<string> CareerInvariants.Validate(CareerState s)` returning human-readable violation codes (`"week-range"`, `"stat-range:Energy"`, `"followers-negative"`, `"viewers-negative"`, `"equipment-tier:Camera"`, `"ended-without-reason"`, `"ended-without-score"`, `"active-insolvent"`, `"pending-on-ended"`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Factory_creates_week_one_active_career() { var s = TestCareers.Dorm(seed: 7);
    Assert.Equal(1, s.Week.Value); Assert.Equal(CareerStatus.Active, s.Status); Assert.Equal(0, s.StateVersion);
    Assert.Empty(CareerInvariants.Validate(s)); }
[Theory, InlineData(0), InlineData(1041)] public void CareerWeek_rejects_out_of_range(int w) => Assert.Throws<ArgumentOutOfRangeException>(() => new CareerWeek(w));
[Fact] public void Invariants_report_each_violation()
{   var s = TestCareers.Dorm(1) with { Energy = Fixed.FromInt(101), Followers = -1, CashPln = -1_000 };
    Assert.Equal(["stat-range:Energy", "followers-negative", "active-insolvent"], CareerInvariants.Validate(s)); }
[Fact] public void Ended_career_requires_reason_and_score()
    => Assert.Contains("ended-without-reason", CareerInvariants.Validate(TestCareers.Dorm(1) with { Status = CareerStatus.Ended }));
```
Create `Tests/Support/TestCareers.cs` with `static CareerState Dorm(ulong seed)` using a literal `StartingProfile` (2,000 PLN, 0 followers, drama 0, credibility 50, energy 70, all skills 10 except FPS 18, equipment tier 0) and ruleset id `"m1-prototype.1"`, digest `"test"`.

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Careers"` → compile failure.
- [ ] **Step 3:** Implement the types and `Validate` (order of checks = order of codes listed above).
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(domain): model career state, factory and invariants`.

---

### Task 6: Ruleset registry and deterministic serialization (#3)

Branch: `feature/m1-03-career-state`

**Files:**
- Create: `Domain/Rulesets/IRuleset.cs`, `Domain/Rulesets/RulesetRegistry.cs`, `Domain/Simulation/DomainError.cs`, `Domain/Simulation/Result.cs`, `Domain/Serialization/CanonicalJson.cs`, `Domain/Serialization/DomainJsonContext.cs`, `Domain/Serialization/StateHash.cs`
- Test: `Tests/Domain/Rulesets/RulesetRegistryTests.cs`, `Tests/Domain/Serialization/CanonicalJsonTests.cs`

**Interfaces:**
- Produces: `public interface IRuleset { string Id { get; } }` (Task 8 extends it with week functions).
- Produces: `sealed class RulesetRegistry(IEnumerable<IRuleset> rulesets)` with `Result<IRuleset> Resolve(string rulesetId)`; duplicate IDs throw in the constructor.
- Produces: `enum DomainErrorCode { IllegalAction, StaleVersion, WeekAlreadySettled, NoPendingWeek, EventsUnresolved, CareerEnded, UnknownChoice, UnknownRuleset, ContentMismatch }`; `sealed record DomainError(DomainErrorCode Code, string Detail)`.
- Produces: `readonly record struct Result<T>` with `static Result<T> Ok(T value)`, `static Result<T> Fail(DomainError error)`, `bool IsSuccess`, `T Value` (throws on failure), `DomainError Error`.
- Produces: `static byte[] CanonicalJson.Serialize(CareerState s)`, `static CareerState CanonicalJson.Deserialize(ReadOnlySpan<byte> utf8)`; `static string StateHash.Of(CareerState s)` → lowercase hex SHA-256 of the canonical bytes.
- `Fixed` serializes as its `Raw` integer via a `JsonConverter<Fixed>`; dictionaries serialize with keys sorted ordinally; `Pcg32` as `{ "State": n, "Increment": n }`; enums as strings.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Resolve_returns_the_career_ruleset_not_the_latest()
{   var reg = new RulesetRegistry([new FakeRuleset("legacy.0"), new FakeRuleset("m1-prototype.1")]);
    Assert.Equal("legacy.0", reg.Resolve("legacy.0").Value.Id); }
[Fact] public void Unknown_ruleset_fails_without_fallback()
    => Assert.Equal(DomainErrorCode.UnknownRuleset, new RulesetRegistry([]).Resolve("x").Error.Code);
[Fact] public void Roundtrip_is_byte_identical()
{   var s = TestCareers.Dorm(9); var bytes = CanonicalJson.Serialize(s);
    Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes))); }
[Fact] public void Hash_ignores_nothing_and_is_stable()
{   Assert.Equal(StateHash.Of(TestCareers.Dorm(9)), StateHash.Of(TestCareers.Dorm(9)));
    Assert.NotEqual(StateHash.Of(TestCareers.Dorm(9)), StateHash.Of(TestCareers.Dorm(10))); }
[Fact] public void Json_contains_no_floating_point_literals()
    => Assert.DoesNotMatch(@"\d\.\d", Encoding.UTF8.GetString(CanonicalJson.Serialize(TestCareers.Dorm(3))));
```

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Rulesets|FullyQualifiedName~Serialization"` → compile failure.
- [ ] **Step 3:** Implement the types. Use a `JsonSerializerContext` (`DomainJsonContext`) with `WriteIndented = false`, `PropertyNamingPolicy = null`; sort dictionary keys by converting to `SortedDictionary` in custom converters; property order fixed with `[JsonPropertyOrder]` matching declaration order.
- [ ] **Step 4:** Run the filter → pass; run all tests → `Failed: 0`.
- [ ] **Step 5:** Commit `feat(domain): add ruleset registry, domain errors and canonical state hashing (closes #3)`.

---

### Task 7: Content snapshot, JSON source and validator (#4)

Branch: `feature/m1-04-week-engine` (from Task 6 branch)

**Files:**
- Create: `Domain/Content/BalanceConstants.cs`, `Domain/Content/EventDefinition.cs` (EventDefinition, Eligibility, ChoiceDefinition, ChanceModifier, OutcomeSet, Effect, EffectKind, StatKey), `Domain/Content/ContentSnapshot.cs`, `Domain/Content/ContentValidator.cs`
- Create: `Application/Content/IContentSource.cs`, `Infrastructure/Content/JsonContentSource.cs`
- Create: `content/m1/balance.json`, `content/m1/events.json` (repository root)
- Test: `Tests/Domain/Content/ContentValidatorTests.cs`, `Tests/Infrastructure/JsonContentSourceTests.cs`

**Interfaces:**
- Produces: `enum StatKey { Drama, Credibility, Energy, Burnout, PrimarySkill, PrimaryBoredom }`; `enum EffectKind { StatDelta, CashDelta, AudienceMultiplier, EventQuality }`; `sealed record Effect(EffectKind Kind, StatKey? Stat, Fixed Amount)`.
- Produces: `sealed record ChanceModifier(StatKey Stat, Fixed PerPoint, Fixed? Cap, string LabelKey)`; `sealed record OutcomeSet(ImmutableArray<Effect> Success, ImmutableArray<Effect> Failure, ImmutableArray<Effect>? CriticalSuccess, ImmutableArray<Effect>? CriticalFailure)`; `sealed record ChoiceDefinition(string Id, string LabelKey, string LabelPl, bool Guaranteed, Fixed BaseChance, ImmutableArray<ChanceModifier> Modifiers, bool AllowsCritical, OutcomeSet Outcomes)`; `sealed record Eligibility(int FromWeek, int ToWeek, ImmutableDictionary<StatKey, Fixed> Min, ImmutableDictionary<StatKey, Fixed> Max)`; `sealed record EventDefinition(string Id, string TitleKey, string TextPl, int Weight, int CooldownWeeks, Eligibility Eligibility, ImmutableArray<ChoiceDefinition> Choices)`.
- Produces: `sealed record BalanceConstants` — one property per key in `balance.json` below (names in PascalCase of the JSON key).
- Produces: `sealed record ContentSnapshot(string Version, string Digest, BalanceConstants Balance, ImmutableArray<EventDefinition> Events, ImmutableDictionary<Background, StartingProfile> Profiles)`.
- Produces: `static IReadOnlyList<string> ContentValidator.Validate(ContentSnapshot c)` (codes: `duplicate-event:<id>`, `duplicate-choice:<event>/<choice>`, `no-choices:<id>`, `chance-range:<event>/<choice>`, `weight-nonpositive:<id>`, `week-range:<id>`, `too-few-eligible:week-<n>`, `missing-profile:<Background>`).
- Produces: `interface IContentSource { ContentSnapshot Load(); }`; `sealed class JsonContentSource(string directory) : IContentSource` — reads `balance.json` + `events.json`, `Digest` = lowercase hex SHA-256 of `balance.json` bytes followed by `events.json` bytes; decimal JSON numbers parsed into `Fixed` via string (`"0.35"` → `Fixed.FromRatio(35,100)`), never via `double`; throws `InvalidDataException` listing validator codes when validation fails.

`content/m1/balance.json` keys and values (values marked † are Agent proposals to record in `gameplay-systems.md` in Task 13):
```
version "m1.1"; livingCostPln 100; easyUpkeepDiscount 0.15; equipmentUpkeepPerTierPln [10,20,35,55,80]†(index = tier);
equipmentPricePln [0,600,1500,3500,8000]† (price to reach tier i); streamEnergyCost 8; streamBurnoutGain 3;
restEnergy 25; restBurnout 12; noActionEnergy 5; trainEnergyCost 8; trainSkillMin 2; trainSkillMax 4;
manageCommunityCredibility 3†; manageCommunityDramaRelief 4†; manageCommunityBoredomRelief 5†;
qualityBase 25; qualitySkill 0.35; qualityEquipment 4; qualityBurnout 0.20; qualityEnergy 0.10;
viewersBase 50; viewersSkill 1.5; viewersSqrtFollowers 35; viewersLinearFollowers 0.025; viewersMin 5;
platformReach 1.0; platformSubYield 1.0; platformAdYield 1.0; noiseMin 0.85; noiseMax 1.15; boredomDivisor 250;
viralCapMultiplier 3; conversionRate 0.05†; churnRate 0.004†; churnBoredomFactor 0.0001†;
paidConversion 0.02†; subPricePln 10†; donationYield 0.15†; adYield 0.05†; adLoad 1.0;
skillDecayPerWeek 0.1; skillDecayAfterWeeks 13; boredomGainMin 2; boredomGainMax 4; boredomReliefMin 1; boredomReliefMax 2;
insolvencyThresholdPln -1000; scoreFollowersCap 1000000; scoreCashCap 10000000;
scoreWeights {followers 0.45, wealth 0.45, achievements 0.10}; achievementPointsCap 10;
difficultyMultiplier {Easy 1.00, Normal 1.10}; easyChanceBonus 0.10;
eventsPerWeekMin 2; eventsPerWeekMax 3;
profiles: DormRoomNewcomer {cash 2000, followers 0, drama 0, cred 50, energy 70, skill all 10 + Fps 18, equipment all 0, multiplier 1.00};
LocalMemeCreator {cash 1000†, followers 600, drama 20, cred 42, energy 70, skill all 10 + JustChatting 18, equipment all 1†, multiplier 0.96};
ChessClubRegular {cash 500, followers 0†, drama 0, cred 58, energy 70, skill all 10 + Chess 25, equipment all 1†, multiplier 0.98};
IndustryInsider {cash 5000, followers 1500, drama 15, cred 50, energy 70, skill all 10†, equipment all 2†, multiplier 0.90}
```
`content/m1/events.json`: five events, all weeks 1–1040, weight ≥ 1, Polish working text: `chat-raid` (choices `welcome` p 0.55 + Credibility 0.002/pt, `ignore` Guaranteed), `tech-failure` (`fix-live` p 0.45 + PrimarySkill 0.003/pt, criticals allowed; `restart` Guaranteed −Energy 3), `troll-wave` (`banter` p 0.50 − Drama 0.002/pt, criticals; `moderate` Guaranteed +Credibility 1), `donation-goal` (`push` p 0.40 + Credibility 0.003/pt; `skip` Guaranteed), `collab-ping` (`accept` p 0.50, criticals; `decline` Guaranteed). Outcome effects use AudienceMultiplier 0.9–1.3, EventQuality −5…+8, StatDelta ±1…5, CashDelta ≤ 200.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Repository_content_is_valid()
{   var c = new JsonContentSource(TestPaths.ContentM1).Load();
    Assert.Empty(ContentValidator.Validate(c)); Assert.Equal(64, c.Digest.Length); Assert.Equal(5, c.Events.Length); }
[Fact] public void Digest_changes_when_balance_changes() { /* copy dir to temp, edit livingCostPln, digests differ */ }
[Fact] public void Decimal_values_parse_exactly() => Assert.Equal(350_000, new JsonContentSource(TestPaths.ContentM1).Load().Balance.QualitySkill.Raw);
[Fact] public void Too_few_eligible_events_names_the_week()
{   var c = TestContent.Valid() with { Events = [.. TestContent.Valid().Events.Take(2)] };
    Assert.Contains("too-few-eligible:week-1", ContentValidator.Validate(c)); }
[Fact] public void Chance_outside_0_1_is_rejected() { /* baseChance 1.2 -> "chance-range:e/c" */ }
[Fact] public void Duplicate_ids_are_rejected() { /* duplicate-event:chat-raid */ }
```
`TestPaths.ContentM1` resolves `<repo>/content/m1` by walking up from `AppContext.BaseDirectory` to the folder containing `content`. `TestContent.Valid()` loads that snapshot.

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Content"` → compile failure.
- [ ] **Step 3:** Implement records, validator (eligibility check iterates weeks 1–1040 counting events whose `FromWeek ≤ w ≤ ToWeek`, ignoring stat bounds; requires ≥ `eventsPerWeekMax`), JSON source and both content files.
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(content): add versioned M1 content snapshot, JSON loader and validator`.

---

### Task 8: StartWeek and main actions (#4)

Branch: `feature/m1-04-week-engine`

**Files:**
- Create: `Domain/Simulation/WeekPlan.cs`, `Domain/Simulation/PendingWeek.cs` (replace placeholder), `Domain/Simulation/ReportLine.cs`, `Domain/Simulation/WeekEngine.cs`, `Domain/Rulesets/M1Ruleset.cs`, `Domain/Simulation/EventSelector.cs`
- Modify: `Domain/Rulesets/IRuleset.cs`
- Test: `Tests/Domain/Simulation/StartWeekTests.cs`

**Interfaces:**
- Consumes: Tasks 3–7.
- Produces: `enum MainAction { Rest, TrainCategory, ImproveProduction, ManageCommunity, NoAction }`; `sealed record WeekPlan(MainAction Action, Category Category, Category? TrainTarget = null, EquipmentSlot? UpgradeSlot = null)`.
- Produces: `sealed record ReportLine(string Code, long? AmountPln = null, Fixed? Delta = null, string? Detail = null)`.
- Produces: `sealed record PendingWeek(CareerWeek Week, WeekPlan Plan, ImmutableArray<string> EventIds, ImmutableArray<RollResult> Resolved, Fixed EventQuality, Fixed AudienceMultiplier, long CashDeltaFromEvents, ImmutableArray<ReportLine> Lines)`; `RollResult` is created as an empty placeholder record here and completed in Task 9.
- Produces on `IRuleset`: `Result<CareerState> StartWeek(CareerState s, ContentSnapshot c, WeekPlan plan, long expectedVersion)`; `IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> LegalPlans(CareerState s, ContentSnapshot c)`.
- Produces: `sealed class WeekEngine(RulesetRegistry registry)` delegating every call to the career's ruleset after checking `c.Digest == s.ContentDigest` (`ContentMismatch` otherwise).
- Produces: `static ImmutableArray<string> EventSelector.Select(CareerState s, ContentSnapshot c, ref Pcg32 rng)` — count = `NextInt(eventsPerWeekMin, eventsPerWeekMax)`; weighted draw without replacement among eligible events not in cooldown; ties broken by content order.

Guards in `StartWeek`, in order: `Status == Ended` → `CareerEnded`; `expectedVersion != StateVersion` → `StaleVersion`; `Pending != null` → `IllegalAction("week-in-progress")`; forced rest (`Energy == 0 || Burnout == 100`) and action ≠ Rest → `IllegalAction("forced-rest")`; `ImproveProduction` with `CashPln < 0` → `IllegalAction("negative-cash")`, with price > cash → `IllegalAction("unaffordable")`, at tier 4 → `IllegalAction("max-tier")`. On success: apply action, pay equipment price (line `cost:equipment`), select events, set `Pending`, `StateVersion + 1`. A failed command returns the unchanged input state's RNG.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Rest_restores_energy_and_lowers_burnout()
{   var s0 = TestCareers.Dorm(1) with { Energy = Fixed.FromInt(40), Burnout = Fixed.FromInt(20) };
    var s1 = Engine.StartWeek(s0, Content, new(MainAction.Rest, Category.Fps), 0).Value;
    Assert.Equal(Fixed.FromInt(65), s1.Energy); Assert.Equal(Fixed.FromInt(8), s1.Burnout);
    Assert.InRange(s1.Pending!.EventIds.Length, 2, 3); Assert.Equal(1, s1.StateVersion); }
[Fact] public void Unaffordable_upgrade_is_illegal_and_state_unchanged()
{   var s0 = TestCareers.Dorm(1) with { CashPln = 100 };
    var r = Engine.StartWeek(s0, Content, new(MainAction.ImproveProduction, Category.Fps, UpgradeSlot: EquipmentSlot.Camera), 0);
    Assert.Equal(DomainErrorCode.IllegalAction, r.Error.Code); Assert.Equal("unaffordable", r.Error.Detail); }
[Fact] public void Negative_cash_blocks_spend() { /* CashPln -1 -> "negative-cash" */ }
[Fact] public void Stale_version_is_rejected() { /* expectedVersion 5 -> StaleVersion */ }
[Fact] public void Second_start_in_same_week_is_rejected() { /* Pending set -> "week-in-progress" */ }
[Fact] public void Forced_rest_when_energy_zero() { /* Energy 0, Train -> "forced-rest"; Rest OK */ }
[Fact] public void Same_seed_selects_same_events() { /* two careers seed 5 -> equal EventIds */ }
[Fact] public void LegalPlans_lists_disabled_spend_with_reason() { /* CashPln 0: ImproveProduction entries have "unaffordable" */ }
```
`Engine` = `new WeekEngine(new RulesetRegistry([new M1Ruleset()]))`; `Content` = `TestContent.Valid()`; `TestCareers.Dorm` digest must equal `Content.Digest` — update `TestCareers` to read it from `TestContent.Valid()`.

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~StartWeek"` → compile failure.
- [ ] **Step 3:** Implement. Train: `gain = NextInt(trainSkillMin, trainSkillMax)` scaled by `max(0.2, (100 − skill)/100)`, energy −8. Manage community: credibility +3, drama −4, boredom of the planned category −5 (all clamped 0–100).
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(domain): start week with validated main action and event selection`.

---

### Task 9: Event odds, rolls and resolution (#4)

Branch: `feature/m1-04-week-engine`

**Files:**
- Create: `Domain/Simulation/RollResult.cs` (replace placeholder), `Domain/Simulation/EventPresentation.cs`, `Domain/Simulation/OddsCalculator.cs`
- Modify: `Domain/Rulesets/IRuleset.cs`, `Domain/Rulesets/M1Ruleset.cs`, `Domain/Simulation/WeekEngine.cs`
- Test: `Tests/Domain/Simulation/OddsCalculatorTests.cs`, `Tests/Domain/Simulation/ResolveEventTests.cs`

**Interfaces:**
- Produces: `sealed record ModifierLine(string LabelKey, Fixed Value)`; `sealed record ChoiceOdds(string ChoiceId, bool Guaranteed, Fixed Probability, int Threshold, ImmutableArray<ModifierLine> Modifiers, bool AllowsCritical)`; `sealed record EventPresentation(string EventId, ImmutableArray<ChoiceOdds> Choices)`.
- Produces: `enum RollOutcome { Success, Failure, CriticalSuccess, CriticalFailure, Guaranteed }`; `sealed record RollResult(string EventId, string ChoiceId, int? Roll, int Threshold, ImmutableArray<ModifierLine> Modifiers, RollOutcome Outcome)`.
- Produces: `static ChoiceOdds OddsCalculator.For(CareerState s, ChoiceDefinition choice, BalanceConstants b)` — `p = clamp(0.05, 0.95, base + Σ min(cap, stat × perPoint) + (Easy ? easyChanceBonus : 0))`, `Threshold = (p × 100).RoundToLong()`; Easy bonus listed as modifier `difficulty.easy`.
- Produces on `IRuleset`/`WeekEngine`: `Result<EventPresentation> PresentEvent(CareerState s, ContentSnapshot c)` (next unresolved event; `NoPendingWeek` / `IllegalAction("all-resolved")`); `Result<CareerState> ResolveEvent(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)` (`UnknownChoice` for a bad id).

Roll rule: guaranteed → no draw, `Outcome = Guaranteed`, apply `Success` effects. Otherwise `roll = NextInt(1,100)`; `AllowsCritical && roll == 1` → CriticalSuccess (apply `CriticalSuccess ?? Success`); `AllowsCritical && roll == 100` → CriticalFailure; else `roll ≤ Threshold` → Success else Failure. Effects: StatDelta → clamp 0–100 (PrimarySkill/PrimaryBoredom target plan category); CashDelta → `CashDeltaFromEvents` (report line `income:event` or `cost:event`); AudienceMultiplier multiplies `PendingWeek.AudienceMultiplier`; EventQuality adds to `PendingWeek.EventQuality`. Cooldown recorded in `EventCooldowns[eventId] = week + cooldownWeeks`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Probability_is_clamped_to_5_and_95() { /* base 1.2 -> 0.95/95; base -1 -> 0.05/5 */ }
[Fact] public void Easy_adds_ten_points_as_listed_modifier() { /* Normal 0.50 -> 50; Easy -> 60 with "difficulty.easy" */ }
[Fact] public void Presentation_draws_no_random() { /* Rng before == Rng after PresentEvent */ }
[Fact] public void Roll_equal_threshold_succeeds()
{   // find seed whose first roll after StartWeek equals the threshold via a helper that scans seeds 1..10_000
    var (s, roll) = TestScenarios.PendingWithFirstRoll(target: 55, choiceBase: 0.55m_as_Fixed);
    var r = Engine.ResolveEvent(s, Content, "welcome", s.StateVersion).Value;
    Assert.Equal(RollOutcome.Success, r.Pending!.Resolved[0].Outcome); }
[Fact] public void Roll_100_is_critical_failure_only_when_allowed() { /* two choices, one allows criticals */ }
[Fact] public void Resolution_records_roll_threshold_and_modifiers() { }
[Fact] public void Resolving_with_stale_version_draws_nothing() { /* StaleVersion; Rng unchanged */ }
[Fact] public void Unknown_choice_is_rejected() { }
```
Implement `TestScenarios` in `Tests/Support` as a helper building a `ContentSnapshot` with one custom event (via `with`) so the first roll can be targeted deterministically by scanning seeds.

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Odds|FullyQualifiedName~ResolveEvent"` → compile failure.
- [ ] **Step 3:** Implement as specified.
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(domain): present odds and resolve event rolls with explained results`.

---

### Task 10: SettleWeek core loop and week report (#4)

Branch: `feature/m1-04-week-engine`

**Files:**
- Create: `Domain/Simulation/WeekReport.cs`, `Domain/Simulation/Settlement/ProgressionRules.cs`
- Modify: `Domain/Rulesets/IRuleset.cs`, `Domain/Rulesets/M1Ruleset.cs`, `Domain/Simulation/WeekEngine.cs`
- Test: `Tests/Domain/Simulation/SettleWeekTests.cs`

**Interfaces:**
- Produces: `sealed record WeekReport(CareerWeek Week, WeekPlan Plan, ImmutableArray<RollResult> Rolls, long LiveViewers, long FollowersGained, long FollowersLost, long FollowersNet, ImmutableArray<ReportLine> Income, ImmutableArray<ReportLine> Costs, long CashDelta, ImmutableArray<ReportLine> StatChanges, EndingReason? Ending)`.
- Produces on `IRuleset`/`WeekEngine`: `Result<(CareerState State, WeekReport Report)> SettleWeek(CareerState s, ContentSnapshot c, long expectedVersion)`.
- Produces: `static class ProgressionRules` with `CareerState ApplyStreamFatigue(CareerState s, BalanceConstants b)` (−8 energy, +3 burnout), `CareerState ApplySkillAndBoredom(CareerState s, Category primary, ref Pcg32 rng, BalanceConstants b)` (gain `max(0.2,(100−skill)/100)` for the primary category; boredom +NextInt(2,4) primary, −NextInt(1,2) others; `InactiveWeeks` reset/increment; decay 0.1/week after 13 inactive weeks, floored at the starting profile skill).

Guards: Ended → `CareerEnded`; stale → `StaleVersion`; `Pending == null` → `NoPendingWeek` (this is the "settle twice" path: the second call returns `NoPendingWeek` and the test asserts `WeekAlreadySettled` is used when `expectedVersion` equals the post-settle version and `Pending` is null — implement: `Pending == null` → `WeekAlreadySettled` if the previous command was a settle (track `LastSettledWeek` on state, add property `int LastSettledWeek` defaulting 0), else `NoPendingWeek`); unresolved events → `EventsUnresolved`. In this task settlement does fatigue, skill/boredom, metrics (`Streams+1`, weeks per category), advances week (or keeps 1040 for Task 12), clears `Pending`, `StateVersion + 1`. Audience and money are zero placeholders replaced in Task 11.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Week_advances_once_and_clears_pending() { var (s1, rep) = Play.OneWeek(TestCareers.Dorm(2));
    Assert.Equal(2, s1.Week.Value); Assert.Null(s1.Pending); Assert.Equal(1, rep.Week.Value); }
[Fact] public void Settling_twice_is_rejected_without_random_draw()
{   var (s1, _) = Play.OneWeek(TestCareers.Dorm(2));
    var r = Engine.SettleWeek(s1, Content, s1.StateVersion);
    Assert.Equal(DomainErrorCode.WeekAlreadySettled, r.Error.Code); }
[Fact] public void Unresolved_events_block_settlement() { }
[Fact] public void Stream_costs_energy_and_adds_burnout() { /* NoAction: +5 then -8 => 67; burnout 3 */ }
[Fact] public void Skill_grows_with_diminishing_returns() { /* skill 90 gains less than skill 10 */ }
[Fact] public void Unused_category_decays_only_after_13_weeks_and_not_below_start() { }
[Fact] public void Same_seed_and_choices_produce_same_hash_after_52_weeks() { }
```
`Play.OneWeek(s, plan?)` (Tests/Support) runs StartWeek → resolve every event with its first choice → SettleWeek.

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~SettleWeek"` → compile failure.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the filter → pass; run all tests → `Failed: 0`.
- [ ] **Step 5:** Commit `feat(domain): settle weeks once with progression and weekly report (closes #4)`.

---

### Task 11: Audience and finances (#5)

Branch: `feature/m1-05-economy-score-endings` (from Task 10 branch)

**Files:**
- Create: `Domain/Simulation/Settlement/AudienceRules.cs`, `Domain/Simulation/Settlement/FinanceRules.cs`
- Modify: `Domain/Rulesets/M1Ruleset.cs`
- Test: `Tests/Domain/Simulation/AudienceRulesTests.cs`, `Tests/Domain/Simulation/FinanceRulesTests.cs`, `Tests/Domain/Simulation/ReconciliationProperties.cs`

**Interfaces:**
- Produces: `sealed record AudienceResult(Fixed Quality, long LiveViewers, long Gained, long Lost, long Net)`; `static AudienceResult AudienceRules.Compute(CareerState s, PendingWeek p, ref Pcg32 rng, BalanceConstants b)` implementing spec §6 formulas: `quality = clamp(0,100, qualityBase + qualitySkill·skill + qualityEquipment·equipmentAverage + eventQuality − qualityBurnout·burnout + qualityEnergy·energy)`; `baseline = max(viewersMin, (viewersBase + viewersSkill·skill + viewersSqrtFollowers·sqrt(followers) + viewersLinearFollowers·followers) · platformReach · (0.6 + quality/100))`; `live = round(min(viralCap·baseline, baseline · (1 − boredom/boredomDivisor) · audienceMultiplier · noise))`, `noise = NextFixed(noiseMin, noiseMax)`; `gained = round(live · conversionRate · (credibility/50))`; `lost = round(followers · (churnRate + churnBoredomFactor·boredom))`; `net = gained − lost`, followers floored at 0.
- Produces: `sealed record FinanceResult(ImmutableArray<ReportLine> Income, ImmutableArray<ReportLine> Costs, long CashDelta)`; `static FinanceResult FinanceRules.Compute(CareerState s, PendingWeek p, long liveViewers, BalanceConstants b)`: income lines `income:subscriptions = round(live·paidConversion·platformSubYield·subPricePln)`, `income:donations = round(live·donationYield)`, `income:ads = round(live·adYield·platformAdYield·adLoad)`, plus positive event cash; cost lines `cost:living` (Easy: `round(100·(1−0.15))`), `cost:equipment-upkeep` (sum of per-slot tier upkeep, Easy discounted), negative event cash, and the `cost:equipment` purchase carried from StartWeek. `CashDelta = Σincome − Σcosts`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Zero_followers_still_get_minimum_viewers() { /* followers 0 -> LiveViewers >= round(5*0.85) */ }
[Fact] public void Fifty_million_followers_do_not_overflow() { /* followers 50_000_000 computes, LiveViewers > 0 */ }
[Fact] public void Viral_cap_limits_viewers_to_three_times_baseline() { /* AudienceMultiplier 10 */ }
[Fact] public void Followers_and_viewers_are_reported_separately() { /* Gained, Lost, Net, LiveViewers distinct fields; Net == Gained - Lost */ }
[Fact] public void Easy_reduces_living_cost_by_15_percent() => /* cost:living 85 vs 100 */;
[Property(MaxTest = 200)] public bool Cash_delta_reconciles_with_report_lines(PositiveInt seed, NonNegativeInt weeks)
{   // play (weeks % 60) weeks with Balanced policy-like first-choice plans; for every report:
    // Σ Income.AmountPln − Σ Costs.AmountPln == report.CashDelta == stateAfter.CashPln − stateBefore.CashPln
}
```

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Audience|FullyQualifiedName~Finance|FullyQualifiedName~Reconciliation"` → compile failure.
- [ ] **Step 3:** Implement and wire into `SettleWeek` (order: fatigue → audience → finances → skill/boredom → metrics).
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(domain): compute audience, followers and reconciled weekly finances`.

---

### Task 12: Endings, score and achievements (#5)

Branch: `feature/m1-05-economy-score-endings`

**Files:**
- Create: `Domain/Scoring/ScoreBreakdown.cs` (replace placeholder), `Domain/Scoring/ScoreCalculator.cs`, `Domain/Scoring/Achievements.cs`
- Modify: `Domain/Rulesets/IRuleset.cs`, `Domain/Rulesets/M1Ruleset.cs`, `Domain/Simulation/WeekEngine.cs`
- Test: `Tests/Domain/Scoring/ScoreCalculatorTests.cs`, `Tests/Domain/Simulation/EndingTests.cs`

**Interfaces:**
- Produces: `sealed record ScoreBreakdown(Fixed F, Fixed W, Fixed A, long FollowersPoints, long WealthPoints, long AchievementPoints, long BaseScore, Fixed DifficultyMultiplier, Fixed BackgroundMultiplier, long FinalScore)`.
- Produces: `static ScoreBreakdown ScoreCalculator.Compute(long followers, long cashPln, int achievementPoints, Difficulty d, Fixed backgroundMultiplier, EndingReason ending, BalanceConstants b)` — Insolvency forces wealth input 0; `F`/`W` via `FixedMath.Log10`; points = `round(100,000 × weight × component)`; `BaseScore` = their sum; `FinalScore = round(BaseScore × difficulty × background)`.
- Produces: `static class Achievements` with ids `followers-1k` (1 pt), `followers-10k` (2 pts), `followers-100k` (3 pts), `career-completed` (4 pts); `ImmutableHashSet<string> Evaluate(CareerState s, bool completed)`; `int Points(ImmutableHashSet<string>)`.
- Produces on `IRuleset`/`WeekEngine`: `Result<CareerState> Retire(CareerState s, ContentSnapshot c, long expectedVersion)` (only when `Pending == null`; else `IllegalAction("week-in-progress")`).
- Settlement end check (after finances): `CashPln ≤ insolvencyThresholdPln` → Insolvency; else week 1040 settled → Completed; else advance week. Ending sets `Status = Ended`, `Ending`, `FinalScore`, `Pending = null`, and `WeekReport.Ending`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Score_of_zero_career_is_zero() { }
[Fact] public void Max_values_give_100000_before_multipliers()
    /* followers 1_000_000, cash 10_000_000, 10 points, Easy, 1.00 -> FinalScore 100_000 */;
[Fact] public void Normal_and_background_multipliers_apply() { /* base 50_000, Normal 1.10, 0.90 -> 49_500 */ }
[Fact] public void Insolvency_counts_wealth_as_zero() { }
[Property] public bool Score_is_monotonic_in_followers(PositiveInt a, PositiveInt b) { }
[Property] public bool Score_is_monotonic_in_cash(PositiveInt a, PositiveInt b) { }
[Fact] public void Cash_exactly_minus_1000_after_settlement_is_insolvency() { /* craft state with cost lines so cash lands on -1000 */ }
[Fact] public void Cash_minus_999_continues() { }
[Fact] public void Week_1040_completes_exactly_once()
{   var s = TestCareers.Dorm(4) with { Week = new CareerWeek(1040), LastSettledWeek = 1039 };
    var (end, rep) = Play.OneWeek(s);
    Assert.Equal(EndingReason.Completed, end.Ending); Assert.Equal(EndingReason.Completed, rep.Ending);
    Assert.Equal(DomainErrorCode.CareerEnded, Engine.StartWeek(end, Content, new(MainAction.Rest, Category.Fps), end.StateVersion).Error.Code); }
[Fact] public void Early_retirement_and_insolvency_have_distinct_records() { /* Ending and FinalScore differ */ }
[Fact] public void Every_command_on_ended_career_returns_CareerEnded_and_keeps_rng() { /* StartWeek, ResolveEvent, SettleWeek, Retire */ }
```

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Score|FullyQualifiedName~Ending"` → compile failure.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run the filter → pass.
- [ ] **Step 5:** Commit `feat(domain): add endings, achievements and 45/45/10 score breakdown`.

---

### Task 13: Policies, full-career golden runs and spec updates (#5)

Branch: `feature/m1-05-economy-score-endings`

**Files:**
- Create: `Domain/Simulation/Policies/IChoicePolicy.cs`, `Domain/Simulation/Policies/BuiltInPolicies.cs`, `Domain/Simulation/CareerRunner.cs`
- Test: `Tests/Domain/Simulation/FullCareerTests.cs`, `Tests/Domain/Simulation/InvariantProperties.cs`, `Tests/Domain/Simulation/PronounNeutralityTests.cs`, `Tests/Golden/full-career-seed-42.txt`
- Modify: `docs/gameplay-systems.md` (add "M1 prototype constants" table with every † value from Task 7 marked `Agent proposal`, M1 achievement list, forced-rest simplification), `docs/validation-and-delivery.md` (§1 milestone 1 wording → minimal core per TD-75), `docs/technical-decisions.md` (TD-82 Agent proposal: M1 constants and forced-rest simplification), `docs/requirements-traceability.md` (M1 rows → `Implemented (M1)` where tests exist)

**Interfaces:**
- Produces: `interface IChoicePolicy { string Name { get; } WeekPlan ChoosePlan(CareerState s, ContentSnapshot c, IReadOnlyList<(WeekPlan Plan, string? DisabledReason)> legal); string ChooseEventChoice(CareerState s, EventPresentation e); bool ShouldRetire(CareerState s); }`.
- Produces: `static class BuiltInPolicies { static IChoicePolicy Balanced, Grinder, RestHeavy, Specialist, Rotator; static IReadOnlyList<IChoicePolicy> All; }` — Balanced: rest when energy < 30 or burnout > 70, upgrade cheapest slot when cash > 3× price, train primary every 4th week, else ManageCommunity when drama > 40, else NoAction; category = background's bonus category; event choice = highest probability non-guaranteed when p ≥ 0.5 else guaranteed. Grinder: never rests unless forced, always Train. RestHeavy: Rest when energy < 60. Specialist: like Balanced, one category forever. Rotator: like Balanced, category = `(Category)(week % 5)`. None retires.
- Produces: `sealed record CareerRunResult(CareerState Final, int WeeksPlayed, IReadOnlyList<string> InvariantViolations, string FinalHash)`; `static CareerRunResult CareerRunner.Run(WeekEngine engine, ContentSnapshot c, CareerState start, IChoicePolicy policy, Action<CareerState, WeekReport>? onWeek = null)` — loops StartWeek → present/resolve all → SettleWeek until Ended; validates invariants after every step and stops at the first violation (recording it).

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory, MemberData(nameof(PoliciesAndBackgrounds))]
public void Every_policy_and_background_reaches_a_valid_ending(string policy, Background bg)
{   var r = CareerRunner.Run(Engine, Content, TestCareers.For(bg, Difficulty.Normal, seed: 42), BuiltInPolicies.ByName(policy));
    Assert.Empty(r.InvariantViolations); Assert.Equal(CareerStatus.Ended, r.Final.Status); Assert.NotNull(r.Final.FinalScore); }
[Fact] public void Seed_42_balanced_dorm_matches_golden_hash()
    => Assert.Equal(File.ReadAllText(TestPaths.Golden("full-career-seed-42.txt")).Trim(), CareerRunner.Run(/*Dorm, Normal, 42, Balanced*/).FinalHash);
[Property(MaxTest = 50)] public bool Random_legal_choices_never_break_invariants(PositiveInt seed) { /* RandomLegalPolicy(seed) */ }
[Fact] public void Pronouns_do_not_change_outcomes()
{   var hashes = Enum.GetValues<PronounSet>().Select(p => CareerRunner.Run(/*same seed, pronoun p*/).Final with { Pronouns = PronounSet.Woman }).Select(StateHash.Of).Distinct();
    Assert.Single(hashes); }
```
Add `TestCareers.For(Background, Difficulty, ulong seed, PronounSet pronouns = Woman)` using `Content.Profiles`. The golden file is created by running the test once, inspecting that the career completed or ended validly, and committing the printed hash (the test prints it on mismatch).

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~FullCareer|FullyQualifiedName~InvariantProperties|FullyQualifiedName~Pronoun"` → compile failure.
- [ ] **Step 3:** Implement policies and runner; write the golden file.
- [ ] **Step 4:** Run all tests → `Failed: 0`. Run coverage: `dotnet test PolskiStreamerSimulator.slnx --settings coverlet.runsettings --collect "XPlat Code Coverage"` and compute line rate from the Cobertura file for `PolskiStreamerSimulator.Domain` → ≥ 0.80.
- [ ] **Step 5:** Update the four documents listed above.
- [ ] **Step 6:** Commit `feat(domain): add automation policies and full-career golden runs (closes #5)`.

---

### Task 14: Batch simulator and baseline report (#6)

Branch: `feature/m1-06-batch-simulator` (from Task 13 branch)

**Files:**
- Create: `Tools/BatchSimulator/BatchOptions.cs`, `Tools/BatchSimulator/BatchRunner.cs`, `Tools/BatchSimulator/BatchStatistics.cs`, `Tools/BatchSimulator/ReportWriter.cs`
- Modify: `Tools/BatchSimulator/Program.cs`
- Test: `Tests/BatchSimulator/BatchOptionsTests.cs`, `Tests/BatchSimulator/BatchRunnerTests.cs`
- Create: `docs/reports/m1-baseline-simulation.md` (generated output, committed)

**Interfaces:**
- Produces: `sealed record BatchOptions(int Careers, ulong Seed, IReadOnlyList<string> Policies, string ContentDirectory, string? OutputDirectory, int? MaxDegreeOfParallelism)`; `static Result<BatchOptions> BatchOptions.Parse(string[] args)` (`--careers` default 1000, `--seed` 42, `--policies all|a,b`, `--content` default `<repo>/content/m1`, `--out`, `--parallel`); unknown flag → failure with message.
- Produces: `sealed record CareerSummary(int Index, ulong Seed, string Policy, Background Background, Difficulty Difficulty, EndingReason Ending, int Weeks, long Followers, long Cash, long Score, IReadOnlyList<string> Violations, string FinalHash)`; career `i` uses seed `SplitMix64(masterSeed + i)`, background `(Background)(i % 4)`, difficulty `i % 2 == 0 ? Normal : Easy`, policy `policies[i % policies.Count]`.
- Produces: `sealed record BatchResult(IReadOnlyList<CareerSummary> Careers, TimeSpan Elapsed, long AllocatedBytes, long PeakWorkingSetBytes)`; `static BatchResult BatchRunner.Run(BatchOptions o)` (`Parallel.For`, results stored by index so order is deterministic).
- Produces: `static string ReportWriter.Markdown(BatchResult r, BatchOptions o)` and `static string ReportWriter.Json(BatchResult r, BatchOptions o)`; sections: `## Deterministic results` (completion rate, ending distribution, p5/p50/p95/max of followers, cash and score per policy, per background, per difficulty; top 5 and bottom 5 careers by score with index, seed, policy, background, ending, weeks, followers, cash; invariant failures), then `## Performance` (elapsed, careers/s, ns per simulated week, allocated bytes, peak working set, machine `Environment.ProcessorCount`, `RuntimeInformation.FrameworkDescription`).
- `Program.Main` returns 0 on success, 1 on any invariant violation, 2 on argument error; writes `report.md` and `report.json` into `--out` when given and prints Markdown to stdout.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Parse_defaults() { var o = BatchOptions.Parse([]).Value; Assert.Equal(1000, o.Careers); Assert.Equal(42UL, o.Seed); }
[Fact] public void Parse_rejects_unknown_flag() => Assert.False(BatchOptions.Parse(["--nope"]).IsSuccess);
[Fact] public void Deterministic_section_is_identical_across_runs()
{   var o = BatchOptions.Parse(["--careers", "40", "--seed", "7"]).Value;
    string Det(string md) => md[..md.IndexOf("## Performance", StringComparison.Ordinal)];
    Assert.Equal(Det(ReportWriter.Markdown(BatchRunner.Run(o), o)), Det(ReportWriter.Markdown(BatchRunner.Run(o with { MaxDegreeOfParallelism = 1 }), o))); }
[Fact] public void Two_hundred_careers_have_no_invariant_failures()
    => Assert.All(BatchRunner.Run(BatchOptions.Parse(["--careers", "200"]).Value).Careers, c => Assert.Empty(c.Violations));
[Fact] public void Report_lists_extremes_and_cost_of_1040_weeks()
{   var o = BatchOptions.Parse(["--careers", "20"]).Value; var md = ReportWriter.Markdown(BatchRunner.Run(o), o);
    Assert.Contains("Top 5", md); Assert.Contains("Bottom 5", md); Assert.Contains("ns per simulated week", md); }
[Fact] public void Invariant_failure_gives_exit_code_1() { /* Program.Run with an injected failing policy via internal overload -> 1 */ }
```

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~BatchSimulator"` → compile failure.
- [ ] **Step 3:** Implement. Memory: `GC.GetTotalAllocatedBytes(true)` delta and `Process.GetCurrentProcess().PeakWorkingSet64`.
- [ ] **Step 4:** Run the filter → pass; run all tests → `Failed: 0`.
- [ ] **Step 5:** Generate the baseline: `dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --out ../../docs/reports/m1-baseline` → exit 0. Copy `report.md` to `docs/reports/m1-baseline-simulation.md`, prepend a dated summary paragraph with notable findings (completion rate, any policy that always wins or always goes insolvent, time per full career) and list them as balance follow-ups. Delete the `m1-baseline` folder.
- [ ] **Step 6:** Update `docs/requirements-traceability.md` and `README.md` status line ("M1 prototype complete pending #7").
- [ ] **Step 7:** Commit `feat(tools): add batch simulator with reproducible outcome and performance report (closes #6)`.

---

### Task 15: Content production capacity estimate (#7)

Branch: `feature/m1-07-content-capacity` (from `develop`)

**Files:**
- Create: `docs/content-capacity-estimate.md`, `docs/templates/content-authoring-log.csv`
- Modify: `docs/content-and-experience.md` (link the estimate from the content-volume paragraph; mark launch floor "proposed, pending owner measurement"), `docs/validation-and-delivery.md` §4 "Content volume" row, `docs/technical-decisions.md` or GDD log (Agent proposal entry), `AGENTS.md`/`CLAUDE.md` maps

**Interfaces:** none (documentation).

- [ ] **Step 1:** Read `docs/content-and-experience.md` template counts and event structure; write `content-capacity-estimate.md` with: scope (what one "template" is: event + choices + outcomes + three pronoun-form variants + QA); measurement method (author 5 representative templates per category across early/mid/late phase, timing writing, pronoun variants and QA separately in the CSV); effort model `hours = n × (write + variants + qa) × (1 + revision factor)`; agent estimate table using stated assumptions (clearly labelled as assumptions, e.g. write 40 min, variants 15 min, QA 15 min, revision 25%); a proposed launch floor by phase (early/mid/late) × category (five) derived from avoiding repetition in a 1,040-week run with 2–3 events/week and cooldowns, showing the arithmetic; and an "Owner sign-off" section with empty fields for measured times and approved floor.
- [ ] **Step 2:** Create the CSV header: `date,author,template_id,category,phase,write_minutes,pronoun_variant_minutes,qa_minutes,revision_minutes,notes`.
- [ ] **Step 3:** Update the linked documents and decision log (Agent proposal, date 2026-10-12, pending owner measurement).
- [ ] **Step 4:** Verify links as in Task 1 Step 5.
- [ ] **Step 5:** Commit `docs: add content production capacity estimate and measurement template (refs #7)` — do **not** close #7 (owner measurement and approval outstanding).

---

## Self-review notes

- Spec coverage: §2 → T2; §3 → T3–T4; §4 → T5–T6; §5 → T7–T10, T13 (policies); §6 → T11–T12; §7 → T14; §8 → T1, T15; §9 → T2 (format, coverage settings), T13 (coverage check); §10 → task branch headers; decision records → T1, T13, T15.
- `LastSettledWeek` is introduced in Task 10 and used in Task 12 tests; `TestCareers.Dorm` gains the content digest in Task 8.
