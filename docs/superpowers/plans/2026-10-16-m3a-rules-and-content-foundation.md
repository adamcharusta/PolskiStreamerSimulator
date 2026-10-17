# M3-A Rules and Content Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ruleset `m3-systems.1` with schema-v2 declarative events, mandatory decisions, the energy/burnout loop with `NetworkCollaborate`, five distinct categories with mastery and viral clips, and make new careers play it in the browser while M1 careers stay byte-identical.

**Architecture:** M1 behaviour is frozen by a golden state-hash test before anything moves. Shared rule code (rolling, effects, command checks, endings) moves out of `M1Ruleset` into `Rulesets/Shared`; per-category, mastery, burnout and viral numbers become balance data whose M1 defaults are neutral, so one set of formulas serves both rulesets. `M3Ruleset` adds flags, follow-ups, a decision queue and a decision carry. New `CareerState` and `PendingWeek` fields are nullable and omitted from canonical JSON when null, so M1 state hashes never change.

**Tech Stack:** .NET 10 (`Domain` without packages, xUnit v3, FsCheck), EF Core SQL Server with Testcontainers, Wolverine HTTP, React 19 + TanStack Query + i18next, `intl-messageformat`, Orval, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md`

## Global Constraints

- `Domain` references only the BCL; no `System.Random`, `DateTime`, `double`, `float`, `decimal` or `Math.Sqrt/Log` (enforced by `Tests/Architecture/LayerDependencyTests.cs`).
- Every random draw comes from the career's `Pcg32` in `CareerState.Rng`; a skipped draw must be skipped identically on replay.
- `m1-prototype.1` results must not change: the golden test from Task 1 stays green after every task.
- New nullable state fields carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`.
- Player-facing text is Polish; documentation, identifiers and decision records are English.
- Event text is ICU MessageFormat: `{gender, select, female {…} male {…} other {…}}`; `other` is the gender-neutral form (`zrobiłoś`); an ASCII apostrophe is written `''`.
- TDD: each behaviour starts with a failing test. Backend and frontend line coverage ≥ 80 %. SonarQube Cloud zero open issues on every PR (TD-96).
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Backend commands run from `source/backend`; frontend commands from `source/frontend`.

## Review Focus

1. **A stored M2 career after the deploy** — an M1 state JSON written before this change must deserialize, keep its hash and keep playing. Pinned by Task 1 (golden) and Task 2 (`Stored_m1_state_round_trips_without_new_fields`).
2. **A decision queued while a batch runs** — the batch must stop before the week and the player must be able to resolve it, then plan normally. Pinned by Task 15 (`Batch_stops_before_a_queued_decision_and_resolving_it_allows_planning`).
3. **Repeating `resolveDecision` with the same command ID** — the second call returns the stored outcome and draws nothing. Pinned by Task 15 (`Repeated_resolve_decision_returns_the_stored_outcome`).
4. **ICU text that `intl-messageformat` rejects but our validator accepts (or the reverse)** — pinned by the frontend content test in Task 17 that formats every `content/m3` message for all three genders.
5. **Recovery crisis loop** — a player who keeps choosing *push through* at burnout 100 must never be blocked from playing. Pinned by Task 10 (`Push_through_failure_still_allows_any_plan`).

## File structure

**Domain**
- `Domain/Rulesets/Shared/RuleSteps.cs` (new): command rejection, roll, next event lookup, ending, accumulation — moved from `M1Ruleset`.
- `Domain/Rulesets/Shared/EffectApplier.cs` (new): applies every `EffectKind` to a state and a pending week or decision carry.
- `Domain/Rulesets/M3Ruleset.cs` (new): the M3 rules.
- `Domain/Rulesets/M3/DecisionQueue.cs` (new): which decisions open the next week.
- `Domain/Rulesets/M3/Flags.cs` (new): flag set/clear/expiry helpers.
- `Domain/Content/IcuMessage.cs` (new): ICU subset validator.
- `Domain/Content/CategoryProfile.cs` (new): per-category balance numbers.
- `Domain/Content/EventDefinition.cs`, `ContentSnapshot.cs`, `ContentValidator.cs`, `BalanceConstants.cs` (modify).
- `Domain/Careers/CareerState.cs`, `CareerInvariants.cs`, `Enums.cs` (modify).
- `Domain/Simulation/PendingWeek.cs`, `WeekPlan.cs`, `WeekReport.cs`, `DomainError.cs`, `WeekEngine.cs`, `OddsCalculator.cs`, `EventSelector.cs`, `Stats.cs`, `CareerRunner.cs`, `Settlement/*.cs`, `Policies/BuiltInPolicies.cs` (modify).
- `Domain/Simulation/DecisionCarry.cs`, `Domain/Simulation/ScheduledEvent.cs` (new).

**Application / Infrastructure / WebApp**
- `Infrastructure/Content/JsonContentSource.cs` (modify: schema 2, optional balance sections).
- `Infrastructure/Content/EventVersions.cs` (new): `(id, version)` conflict check.
- `Infrastructure/Persistence/EfContentStore.cs`, `Tests/Support/InMemoryContentStore.cs` (modify).
- `Application/Careers/Contracts.cs`, `CareerViewBuilder.cs`, `SubmitAction.cs`, `CreateCareer.cs`, `ReportViews.cs`, `Persistence/CareerRecords.cs` (modify).
- `WebApp/Careers/Requests.cs`, `WebApp/openapi/*.json` (modify, regenerated).

**Content and tools**
- `content/m3/balance.json`, `content/m3/events.json` (new).
- `Tools/BatchSimulator/*` (modify: ruleset from content, new policies).
- `docs/reports/m3a-baseline-simulation.md` (new, generated).

**Frontend**
- `src/content/icu.ts` (new): `formatContent(text, pronouns)`.
- `src/content/icu.test.ts`, `src/content/m3Content.test.ts` (new).
- `src/features/career/stage/LiveEvent.tsx` (modify: ICU, decision mode), `PlanWeek.tsx` (only offered actions, network odds), `WeekReport.tsx` (decision and action rolls, viral line), `Stage.tsx` (decision step), `plans.ts`, `src/locales/pl/translation.json`.
- `e2e/journey.spec.ts` (modify: week-2 milestone).

## Delivery

Three PRs into `develop`, each from its own branch off the latest `develop`, merged before the next starts. This moves the representative content from PR 3 to PR 2 because the simulation in PR 2 needs it; Task 0 records that change in the spec.

| PR | Branch | Tasks |
| --- | --- | --- |
| 1 Foundation | `feature/m3a-1-foundation` | 0–7 |
| 2 Rules and content | `feature/m3a-2-rules` | 8–14 |
| 3 API and play | `feature/m3a-3-play` | 15–19 |

Before each PR: `dotnet test` (all), `npm run lint && npm run typecheck && npm test -- --coverage` when the frontend changed, then open the PR, wait for CI, check SonarQube Cloud with `https://sonarcloud.io/api/issues/search?componentKeys=adamcharusta_PolskiStreamerSimulator&pullRequest=<n>&resolved=false` (must be `"total":0`) and ask the owner to merge. After the merge, check `&branch=develop`.

---

# PR 1 — Foundation

### Task 0: Record plan-time adjustments in the spec

**Files:**
- Modify: `docs/superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md` (§4.3, §6, §9.3)
- Modify: `docs/technical-decisions.md` (TD-111)

- [ ] **Step 1: Edit the spec**

In §4.3 replace the paragraph starting "ICU syntax is validated in `Application`…" with:

```markdown
ICU syntax is validated by `IcuMessage` in `Domain`, a dependency-free validator for the subset the content uses: plain text, `''` escapes, `{name}` arguments and `{gender, select, …}` with `female`, `male` and `other` (each branch may nest the same subset). Any other ICU construct is rejected. A frontend test formats every `content/m3` message with `intl-messageformat` for all three genders, so the two parsers cannot drift.
```

In §6 replace the bullet starting "New command `ResolveDecision(choiceId)`" with:

```markdown
- New command `ResolveDecision(choiceId)`: presents the same odds as a live event and rolls. Stat effects apply at once. Cash and follower effects, the roll and its stat lines go into a **decision carry** that the next `StartWeek` moves into the week, so the week's report lists every cash movement and reconciles exactly. Audience, quality and viral effects are not allowed in `milestone`/`crisis` events (validator).
- One-time milestones open as soon as they are eligible; repeatable milestones and crises open after a seeded `decisionChancePerWeek` roll (balance, proposed 6 %).
```

In §9.3 replace the three numbered PR items with:

```markdown
1. **Foundation:** M1 golden test, hash-neutral state fields, shared rule steps, schema v2 with loader and ICU validator, immutability, `M3Ruleset` with M1-equivalent rules, flags, follow-ups and the decision mechanism.
2. **Rules and content:** category profiles, mastery, virals, burnout penalty, `NetworkCollaborate`, recovery crisis, decision opening rules, the ~40 Polish events in `content/m3`, simulator support and the baseline report. (Content moved here from PR 3 because the simulation needs it.)
3. **API and play:** API and OpenAPI changes, frontend ICU rendering and decision step, new careers on `m3-systems.1`, the Playwright journey.
```

- [ ] **Step 2: Edit TD-111**

In `docs/technical-decisions.md`, in the TD-111 row, replace "ICU syntax is validated in `Application` before a snapshot is stored, so `Domain` keeps no package dependency." with "ICU syntax is validated by a dependency-free subset validator in `Domain` (`IcuMessage`), and a frontend test formats every message with `intl-messageformat` (plan-time refinement, 2026-10-16: no .NET ICU package exposes the branch structure needed to require `female`/`male`/`other`)."

- [ ] **Step 3: Commit**

```bash
git switch -c feature/m3a-1-foundation origin/develop
git add docs
git commit -m "docs: refine the M3-A design for ICU validation, decision carry and PR split

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 1: Freeze M1 with a golden state-hash test

**Files:**
- Create: `source/backend/Tests/Domain/Rulesets/M1GoldenTests.cs`
- Create: `source/backend/Tests/Domain/Rulesets/m1-golden-hashes.txt` (generated)
- Modify: `source/backend/Tests/Tests.csproj` (copy the text file to output)

**Interfaces:**
- Consumes: `CareerRunner.Run`, `CareerFactory.Create`, `BuiltInPolicies.All`, `TestWorld.Engine`, `TestWorld.Content`.
- Produces: `M1GoldenTests.Compute()` returning `IReadOnlyList<string>` lines `"{policy} {seed} {hash}"`.

- [ ] **Step 1: Write the test**

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Policies;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

/// <summary>
/// Pins the final state hash of full M1 careers. Any refactoring that changes an M1 result, draw order or the
/// canonical JSON of an M1 state fails here. Regenerate only for a deliberate M1 change (never in M3).
/// </summary>
public class M1GoldenTests
{
    private const string FileName = "m1-golden-hashes.txt";

    public static IReadOnlyList<string> Compute()
    {
        var lines = new List<string>();
        var backgrounds = Enum.GetValues<Background>();
        foreach (var policy in BuiltInPolicies.All)
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var background = backgrounds[(int)(seed % (ulong)backgrounds.Length)];
                var difficulty = seed % 2 == 0 ? Difficulty.Easy : Difficulty.Normal;
                var c = TestWorld.Content;
                var start = CareerFactory.Create(M1Ruleset.RulesetId, c.Digest, c.Profiles[background], background, difficulty, PronounSet.Woman, seed);
                var result = CareerRunner.Run(TestWorld.Engine, c, start, policy);
                lines.Add($"{policy.Name} {seed} {result.FinalHash}");
            }
        }

        return lines;
    }

    [Fact]
    public void M1_careers_reproduce_their_recorded_final_hashes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Domain", "Rulesets", FileName);
        var actual = Compute();
        if (Environment.GetEnvironmentVariable("UPDATE_M1_GOLDEN") == "1")
        {
            File.WriteAllLines(Path.Combine(TestPaths.RepositoryRoot, "source", "backend", "Tests", "Domain", "Rulesets", FileName), actual);
        }

        Assert.Equal(File.ReadAllLines(path), actual);
    }
}
```

`TestPaths.RepositoryRoot` is the repository root used by the existing content tests.

In `Tests.csproj` add:

```xml
<ItemGroup>
  <None Update="Domain/Rulesets/m1-golden-hashes.txt" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Generate the file on the unchanged code**

Run: `UPDATE_M1_GOLDEN=1 dotnet test --filter M1GoldenTests` (Git Bash). Then run `dotnet test --filter M1GoldenTests` without the variable.
Expected: PASS, and `m1-golden-hashes.txt` has 100 lines.

- [ ] **Step 3: Commit**

```bash
git add source/backend/Tests
git commit -m "test: pin M1 career results with golden state hashes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Hash-neutral state fields for flags, schedules, decisions and carry

**Files:**
- Create: `source/backend/Domain/Simulation/ScheduledEvent.cs`, `source/backend/Domain/Simulation/DecisionCarry.cs`
- Modify: `source/backend/Domain/Careers/CareerState.cs`, `source/backend/Domain/Simulation/PendingWeek.cs`, `source/backend/Domain/Careers/CareerInvariants.cs`
- Test: `source/backend/Tests/Domain/Serialization/CanonicalJsonTests.cs` (existing file, add tests)

**Interfaces:**
- Produces:
  - `record ScheduledEvent(string EventId, int DueWeek)`
  - `record DecisionCarry(ImmutableArray<RollResult> Rolls, long CashPln, Fixed FollowersPercent, ImmutableArray<ReportLine> Lines)` with `static DecisionCarry Empty`
  - `CareerState.Flags : ImmutableDictionary<string,int>?` (flag → last active week), `CareerState.Scheduled : ImmutableArray<ScheduledEvent>?`, `CareerState.PendingDecisions : ImmutableArray<string>?`, `CareerState.Carry : DecisionCarry?`, `CareerState.UsedOneTime : ImmutableHashSet<string>?`
  - `PendingWeek.ActionRoll : RollResult?`, `PendingWeek.FollowersPercent : Fixed?`, `PendingWeek.ViralChance : Fixed?`, `PendingWeek.Carry : DecisionCarry?`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Stored_m1_state_round_trips_without_new_fields()
{
    var s = TestCareers.Fresh(); // existing helper; use the M1 starting-state helper in Tests/Support/TestCareers.cs
    var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(s));
    foreach (var name in new[] { "Flags", "Scheduled", "PendingDecisions", "Carry", "UsedOneTime" })
    {
        Assert.DoesNotContain($"\"{name}\"", json, StringComparison.Ordinal);
    }

    Assert.Equal(StateHash.Of(s), StateHash.Of(CanonicalJson.Deserialize(CanonicalJson.Serialize(s))));
}

[Fact]
public void M3_fields_round_trip_in_ordinal_order()
{
    var s = TestCareers.Fresh() with
    {
        Flags = ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal).Add("b", 5).Add("a", 1040),
        Scheduled = [new ScheduledEvent("rivalry-2", 12)],
        PendingDecisions = ["burnout-recovery"],
        UsedOneTime = ImmutableHashSet.Create(StringComparer.Ordinal, "first-week"),
        Carry = DecisionCarry.Empty with { CashPln = -200 },
    };
    var bytes = CanonicalJson.Serialize(s);
    var json = System.Text.Encoding.UTF8.GetString(bytes);
    Assert.True(json.IndexOf("\"a\":1040", StringComparison.Ordinal) < json.IndexOf("\"b\":5", StringComparison.Ordinal));
    Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes)));
}
```

If `TestCareers` has no `Fresh()`, add one there that builds `CareerFactory.Create(M1Ruleset.RulesetId, TestWorld.Content.Digest, TestWorld.Content.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Woman, 7)`.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter CanonicalJsonTests`
Expected: compile errors for `Flags`, `ScheduledEvent`, `DecisionCarry`.

- [ ] **Step 3: Implement**

`Domain/Simulation/ScheduledEvent.cs`:

```csharp
namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>A follow-up event that becomes due in <see cref="DueWeek"/> (a live slot or a decision before that week).</summary>
public sealed record ScheduledEvent(string EventId, int DueWeek);
```

`Domain/Simulation/DecisionCarry.cs`:

```csharp
using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Domain.Simulation;

/// <summary>
/// Results of decisions answered between weeks that belong to the next week's report: their rolls, cash and
/// follower changes and stat lines. The next <c>StartWeek</c> moves the carry into the pending week.
/// </summary>
public sealed record DecisionCarry(ImmutableArray<RollResult> Rolls, long CashPln, Fixed FollowersPercent, ImmutableArray<ReportLine> Lines)
{
    public static DecisionCarry Empty { get; } = new([], 0, Fixed.Zero, []);
}
```

In `CareerState.cs` add after `Pending` (keep declaration order: new properties last so existing JSON order is unchanged):

```csharp
    /// <summary>M3: flag → last week in which it is active (<see cref="CareerWeek.Last"/> for permanent).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<string, int>? Flags { get; init; }

    /// <summary>M3: follow-up events waiting for their due week, in scheduling order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<ScheduledEvent>? Scheduled { get; init; }

    /// <summary>M3: mandatory decisions to answer before the next week starts, in order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<string>? PendingDecisions { get; init; }

    /// <summary>M3: decision results waiting for the next week's report.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DecisionCarry? Carry { get; init; }

    /// <summary>M3: one-time events already shown.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableHashSet<string>? UsedOneTime { get; init; }
```

Add `using System.Text.Json.Serialization;`. In `PendingWeek.cs` convert the positional record to keep its parameters and add init-only nullable properties with the same attribute:

```csharp
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RollResult? ActionRoll { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Fixed? FollowersPercent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Fixed? ViralChance { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DecisionCarry? Carry { get; init; }
```

`FixedJsonConverter` handles `Fixed?` through the source generator's nullable wrapper; the existing `Int32OrdinalDictionaryJsonConverter` and `OrdinalSetJsonConverter` cover the new dictionary and set.

In `CareerInvariants.Validate` add:

```csharp
        if (s.PendingDecisions is { Length: > 0 } && s.Pending is not null)
        {
            violations.Add("decision-during-week");
        }

        if (s.Flags is { } flags && flags.Any(f => f.Value < s.LastSettledWeek))
        {
            violations.Add("expired-flag-kept");
        }
```

(match the existing method's collection name; it returns a list of codes).

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter "CanonicalJsonTests|M1GoldenTests"`
Expected: PASS (golden unchanged).

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): add hash-neutral M3 state for flags, follow-ups, decisions and carry

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: Move shared rule steps out of `M1Ruleset`

**Files:**
- Create: `source/backend/Domain/Rulesets/Shared/RuleSteps.cs`, `source/backend/Domain/Rulesets/Shared/EffectApplier.cs`
- Modify: `source/backend/Domain/Rulesets/M1Ruleset.cs`

**Interfaces:**
- Produces:
  - `RuleSteps.CommandRejection(CareerState s, long expectedVersion) : DomainError?`
  - `RuleSteps.Roll(ChoiceDefinition choice, ChoiceOdds odds, ref Pcg32 rng) : (int? Roll, RollOutcome Outcome, ImmutableArray<Effect> Effects)`
  - `RuleSteps.FindEvent(ContentSnapshot c, string id) : Result<EventDefinition>`
  - `RuleSteps.End(CareerState s, EndingReason reason, ContentSnapshot c) : CareerState`
  - `RuleSteps.Accumulate(ImmutableDictionary<string,long> totals, IEnumerable<ReportLine> lines)`
  - `EffectApplier.ToWeek(CareerState s, string sourceId, ImmutableArray<Effect> effects, ref PendingWeek pending) : CareerState` — exactly today's `ApplyEffects`, extended in Task 6.

- [ ] **Step 1: Move the code**

Cut `CommandRejection`, `Roll`, `End`, `Accumulate` from `M1Ruleset` into `public static class RuleSteps` (namespace `PolskiStreamerSimulator.Domain.Rulesets.Shared`) unchanged except `private` → `public`. Add:

```csharp
    public static Result<EventDefinition> FindEvent(ContentSnapshot c, string id)
        => c.Events.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)) is { } definition
            ? Result<EventDefinition>.Ok(definition)
            : Result<EventDefinition>.Fail(DomainErrorCode.ContentMismatch, $"missing-event-{id}");
```

and make `M1Ruleset.NextEvent` use it. Cut `ApplyEffects` into `public static class EffectApplier` as `ToWeek(...)`. Replace the call sites in `M1Ruleset` with `RuleSteps.…`/`EffectApplier.ToWeek(...)`.

- [ ] **Step 2: Run the whole backend suite**

Run: `dotnet test`
Expected: PASS, golden unchanged.

- [ ] **Step 3: Commit**

```bash
git add source/backend/Domain
git commit -m "refactor(domain): move shared rule steps out of the M1 ruleset

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4: ICU subset validator

**Files:**
- Create: `source/backend/Domain/Content/IcuMessage.cs`
- Test: `source/backend/Tests/Domain/Content/IcuMessageTests.cs`

**Interfaces:**
- Produces: `IcuMessage.Validate(string message) : string?` — `null` when valid, otherwise a stable problem code: `empty`, `unbalanced`, `unsupported-argument`, `select-not-gender`, `missing-branch:female|male|other`, `unknown-branch:{key}`, `unterminated-quote`.

- [ ] **Step 1: Write the failing tests**

```csharp
using PolskiStreamerSimulator.Domain.Content;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

public class IcuMessageTests
{
    [Theory]
    [InlineData("Czat szaleje.")]
    [InlineData("{gender, select, female {Zrobiłaś to.} male {Zrobiłeś to.} other {Zrobiłoś to.}}")]
    [InlineData("Widzowie: {gender, select, female {byłaś} male {byłeś} other {byłoś}} świetna.")]
    [InlineData("Napis '''cytat''' w tekście")]
    [InlineData("{gender, select, female {A {gender, select, female {x} male {y} other {z}}} male {B} other {C}}")]
    public void Accepts_the_supported_subset(string message) => Assert.Null(IcuMessage.Validate(message));

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("{gender, select, female {a} male {b}}", "missing-branch:other")]
    [InlineData("{gender, select, male {b} other {c}}", "missing-branch:female")]
    [InlineData("{gender, select, female {a} other {c}}", "missing-branch:male")]
    [InlineData("{gender, select, female {a} male {b} other {c} x {d}}", "unknown-branch:x")]
    [InlineData("{count, plural, one {a} other {b}}", "unsupported-argument")]
    [InlineData("{mood, select, female {a} male {b} other {c}}", "select-not-gender")]
    [InlineData("Otwarte { bez końca", "unbalanced")]
    [InlineData("Zamknięte } bez otwarcia", "unbalanced")]
    [InlineData("Ktoś's quote '{", "unterminated-quote")]
    public void Rejects_with_a_code(string message, string code) => Assert.Equal(code, IcuMessage.Validate(message));
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter IcuMessageTests`
Expected: compile error `IcuMessage` not found.

- [ ] **Step 3: Implement**

```csharp
namespace PolskiStreamerSimulator.Domain.Content;

/// <summary>
/// Validates the ICU MessageFormat subset used by event text: literal text with <c>''</c> escapes and
/// <c>'…'</c> quoting, and <c>{gender, select, female {…} male {…} other {…}}</c> whose branches may nest the
/// same subset. Every other argument form is rejected so the browser's <c>intl-messageformat</c> renders exactly
/// what was validated.
/// </summary>
public static class IcuMessage
{
    private static readonly string[] Branches = ["female", "male", "other"];

    public static string? Validate(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "empty";
        }

        var position = 0;
        var problem = ParseMessage(message, ref position, nested: false);
        return problem ?? (position == message.Length ? null : "unbalanced");
    }

    /// <summary>Reads literal text and arguments until the end, or until a closing brace when nested.</summary>
    private static string? ParseMessage(string m, ref int i, bool nested)
    {
        while (i < m.Length)
        {
            var ch = m[i];
            if (ch == '\'')
            {
                if (SkipQuote(m, ref i) is { } quoteProblem)
                {
                    return quoteProblem;
                }

                continue;
            }

            if (ch == '}')
            {
                return nested ? null : "unbalanced";
            }

            if (ch == '{')
            {
                if (ParseArgument(m, ref i) is { } argumentProblem)
                {
                    return argumentProblem;
                }

                continue;
            }

            i++;
        }

        return nested ? "unbalanced" : null;
    }

    /// <summary>
    /// ICU quoting: <c>''</c> is one apostrophe; an apostrophe before a brace starts a quoted literal that ends at the
    /// next single apostrophe; any other apostrophe is literal.
    /// </summary>
    private static string? SkipQuote(string m, ref int i)
    {
        if (i + 1 < m.Length && m[i + 1] == '\'')
        {
            i += 2;
            return null;
        }

        if (i + 1 < m.Length && (m[i + 1] == '{' || m[i + 1] == '}'))
        {
            var end = m.IndexOf('\'', i + 1);
            if (end < 0)
            {
                return "unterminated-quote";
            }

            i = end + 1;
            return null;
        }

        i++;
        return null;
    }

    private static string? ParseArgument(string m, ref int i)
    {
        var close = m.IndexOfAny([',', '}'], i + 1);
        if (close < 0)
        {
            return "unbalanced";
        }

        var name = m[(i + 1)..close].Trim();
        if (m[close] == '}')
        {
            // A simple {name} argument; the content uses none, but ICU allows it.
            i = close + 1;
            return name.Length == 0 ? "unsupported-argument" : null;
        }

        var typeEnd = m.IndexOf(',', close + 1);
        if (typeEnd < 0 || m[(close + 1)..typeEnd].Trim() != "select")
        {
            return "unsupported-argument";
        }

        if (name != "gender")
        {
            return "select-not-gender";
        }

        i = typeEnd + 1;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            SkipWhitespace(m, ref i);
            if (i >= m.Length)
            {
                return "unbalanced";
            }

            if (m[i] == '}')
            {
                i++;
                break;
            }

            var keyStart = i;
            while (i < m.Length && !char.IsWhiteSpace(m[i]) && m[i] != '{' && m[i] != '}')
            {
                i++;
            }

            var key = m[keyStart..i];
            if (!Branches.Contains(key))
            {
                return $"unknown-branch:{key}";
            }

            seen.Add(key);
            SkipWhitespace(m, ref i);
            if (i >= m.Length || m[i] != '{')
            {
                return "unbalanced";
            }

            i++;
            if (ParseMessage(m, ref i, nested: true) is { } branchProblem)
            {
                return branchProblem;
            }

            i++; // the branch's closing brace
        }

        return Branches.FirstOrDefault(b => !seen.Contains(b)) is { } missing ? $"missing-branch:{missing}" : null;
    }

    private static void SkipWhitespace(string m, ref int i)
    {
        while (i < m.Length && char.IsWhiteSpace(m[i]))
        {
            i++;
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter IcuMessageTests`
Expected: PASS. If `"Ktoś's quote '{"` does not yield `unterminated-quote`, trace `SkipQuote`: the first `'` is followed by `s` (literal), the second by `{` (quote start without end) — fix the scan, not the test.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): validate the ICU gender-select subset used by event text

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5: Event schema v2 model, loader and validator

**Files:**
- Modify: `source/backend/Domain/Content/EventDefinition.cs`, `ContentSnapshot.cs`, `ContentValidator.cs`, `BalanceConstants.cs`
- Modify: `source/backend/Infrastructure/Content/JsonContentSource.cs`
- Create: `source/backend/Tests/Support/M3TestContent.cs`
- Test: `source/backend/Tests/Domain/Content/ContentValidatorV2Tests.cs`, `source/backend/Tests/Infrastructure/JsonContentSourceTests.cs` (add tests)

**Interfaces:**
- Produces:
  - `enum EventPhase { Live, Milestone, Crisis }`
  - `StatKey.Followers` (read-only in eligibility and modifiers; `StatDelta` on it is invalid)
  - `EffectKind` adds `FollowersPercent, SkillDelta, ViralChance, SetFlag, ClearFlag, FollowUp`
  - `record Effect(EffectKind Kind, StatKey? Stat, Fixed Amount, Category? Category = null, string? Target = null, int? Weeks = null)`
  - `record ChoiceRequirement(long? MinCashPln, Fixed? MinEnergy)`
  - `ChoiceDefinition` gains `ChoiceRequirement? Requires = null, string? SuccessText = null, string? FailureText = null` (appended optional parameters); `LabelKey` becomes `string?`.
  - `Eligibility` gains `ImmutableArray<Category> Categories = default, ImmutableArray<string> RequiresFlags = default, ImmutableArray<string> ForbidsFlags = default`.
  - `EventDefinition` gains `int Version = 1, EventPhase Phase = EventPhase.Live, string? Title = null, bool OneTime = false, ImmutableArray<string> Tags = default`; `TitleKey` becomes `string?`. `TextPl` keeps its name and holds ICU text in schema 2.
  - `ContentSnapshot` gains `int Schema = 1, string RulesetId = M1Ruleset.RulesetId` — use the string literal `"m1-prototype.1"` to avoid a Content→Rulesets dependency.
  - `BalanceConstants` gains `RecoveryCrisisEventId : string?`, `DecisionChancePerWeek : Fixed` (default 0) and `NetworkCollaborate : ChoiceDefinition?`, `NetworkEnergyCost : Fixed` (default 0).
  - `M3TestContent.Valid() : ContentSnapshot` — the M1 snapshot converted to schema 2 plus a milestone, a crisis and the recovery crisis.

- [ ] **Step 1: Write the failing validator tests**

```csharp
using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Content;

public class ContentValidatorV2Tests
{
    private static readonly ContentSnapshot Valid = M3TestContent.Valid();

    private static ContentSnapshot WithEvent(string id, Func<EventDefinition, EventDefinition> change)
        => Valid with { Events = [.. Valid.Events.Select(e => e.Id == id ? change(e) : e)] };

    [Fact]
    public void Test_content_is_valid() => Assert.Empty(ContentValidator.Validate(Valid));

    [Fact]
    public void Event_whose_every_choice_has_requirements_has_no_legal_option()
    {
        var c = WithEvent("chat-raid", e => e with
        {
            Choices = [.. e.Choices.Select(ch => ch with { Requires = new ChoiceRequirement(500, null) })],
        });
        Assert.Contains("no-legal-choice:chat-raid", ContentValidator.Validate(c));
    }

    [Fact]
    public void Missing_grammatical_branch_is_reported_with_its_field()
    {
        var c = WithEvent("chat-raid", e => e with { TextPl = "{gender, select, female {a} other {c}}" });
        Assert.Contains("text:chat-raid/text:missing-branch:male", ContentValidator.Validate(c));
    }

    [Fact]
    public void Follow_up_to_unknown_event_is_reported()
    {
        var c = WithEvent("chat-raid", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with
            {
                Outcomes = e.Choices[0].Outcomes with
                {
                    Success = [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "nope", Weeks: 2)],
                },
            }),
        });
        Assert.Contains("broken-follow-up:chat-raid/nope", ContentValidator.Validate(c));
    }

    [Fact]
    public void Required_flag_that_nothing_sets_is_reported()
    {
        var c = WithEvent("chat-raid", e => e with { Eligibility = e.Eligibility with { RequiresFlags = ["ghost"] } });
        Assert.Contains("unset-flag:chat-raid/ghost", ContentValidator.Validate(c));
    }

    [Fact]
    public void Audience_effect_in_a_decision_event_is_rejected()
    {
        var c = WithEvent("test-crisis", e => e with
        {
            Choices = e.Choices.SetItem(0, e.Choices[0] with
            {
                Outcomes = e.Choices[0].Outcomes with { Success = [new Effect(EffectKind.AudienceMultiplier, null, Fixed.FromInt(2))] },
            }),
        });
        Assert.Contains($"effect-phase:test-crisis/{Valid.Events.Single(e => e.Id == "test-crisis").Choices[0].Id}", ContentValidator.Validate(c));
    }

    [Fact]
    public void Missing_recovery_crisis_event_is_reported()
    {
        var c = Valid with { Events = [.. Valid.Events.Where(e => e.Id != "burnout-recovery")] };
        Assert.Contains("missing-recovery-crisis:burnout-recovery", ContentValidator.Validate(c));
    }

    [Fact]
    public void Category_without_enough_live_events_names_category_and_week()
    {
        var c = Valid with
        {
            Events = [.. Valid.Events.Select(e => e.Phase == EventPhase.Live
                ? e with { Eligibility = e.Eligibility with { Categories = [PolskiStreamerSimulator.Domain.Careers.Category.Chess] } }
                : e)],
        };
        Assert.Contains("too-few-eligible:Fps:week-1", ContentValidator.Validate(c));
    }

    [Fact]
    public void Schema_one_rejects_m3_effect_kinds()
    {
        var m1 = TestContent.Valid();
        var e = m1.Events[0];
        var changed = m1 with
        {
            Events = m1.Events.SetItem(0, e with
            {
                Choices = e.Choices.SetItem(0, e.Choices[0] with
                {
                    Outcomes = e.Choices[0].Outcomes with { Success = [new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "x")] },
                }),
            }),
        };
        Assert.Contains($"effect-invalid:{e.Id}/{e.Choices[0].Id}", ContentValidator.Validate(changed));
    }
}
```

- [ ] **Step 2: Create `M3TestContent`**

```csharp
using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>
/// Schema-2 content for M3 rule tests: the M1 events as live events plus a one-time milestone in week 2, a
/// repeatable crisis and the recovery crisis. Built in code so rule tests do not depend on authored Polish content.
/// </summary>
public static class M3TestContent
{
    public const string RulesetId = "m3-systems.1";

    private static readonly Lazy<ContentSnapshot> Snapshot = new(Build);

    public static ContentSnapshot Valid() => Snapshot.Value;

    private static ContentSnapshot Build()
    {
        var m1 = TestContent.Valid();
        var live = m1.Events.Select(e => e with { Title = $"Tytuł {e.Id}", TitleKey = null }).ToImmutableArray();
        return m1 with
        {
            Schema = 2,
            RulesetId = RulesetId,
            Digest = "m3-test",
            Balance = m1.Balance with
            {
                RecoveryCrisisEventId = "burnout-recovery",
                DecisionChancePerWeek = Fixed.FromRatio(6, 100),
            },
            Events = live.AddRange(
            [
                Decision("first-week", EventPhase.Milestone, fromWeek: 2, toWeek: 2, oneTime: true),
                Decision("test-crisis", EventPhase.Crisis, fromWeek: 1, toWeek: 1040, oneTime: false),
                Recovery(),
            ]),
        };
    }

    public static EventDefinition Decision(string id, EventPhase phase, int fromWeek, int toWeek, bool oneTime) => new(
        id,
        null,
        "{gender, select, female {Zrobiłaś krok.} male {Zrobiłeś krok.} other {Zrobiłoś krok.}}",
        1,
        13,
        new Eligibility(fromWeek, toWeek, ImmutableDictionary<StatKey, Fixed>.Empty, ImmutableDictionary<StatKey, Fixed>.Empty),
        [
            new ChoiceDefinition("calm", null, "Spokojnie", true, Fixed.One, [], false,
                new OutcomeSet([new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(1))], [], null, null)),
            new ChoiceDefinition("bold", null, "Odważnie", false, Fixed.FromRatio(1, 2), [], false,
                new OutcomeSet(
                    [new Effect(EffectKind.CashDelta, null, Fixed.FromInt(300))],
                    [new Effect(EffectKind.CashDelta, null, Fixed.FromInt(-200)), new Effect(EffectKind.FollowersPercent, null, Fixed.FromInt(-2))],
                    null,
                    null)),
        ],
        Phase: phase,
        Title: $"Tytuł {id}",
        OneTime: oneTime);

    private static EventDefinition Recovery() => Decision("burnout-recovery", EventPhase.Crisis, 1, 1040, false) with
    {
        Weight = 1,
        Choices =
        [
            new ChoiceDefinition("week-off", null, "Wolny tydzień", true, Fixed.One, [], false,
                new OutcomeSet(
                    [
                        new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "sys:rest-only", Weeks: 1),
                        new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "sys:short-stream", Weeks: 1),
                    ],
                    [],
                    null,
                    null)),
            new ChoiceDefinition("push-through", null, "Ciśniemy dalej", false, Fixed.FromRatio(1, 2), [], false,
                new OutcomeSet(
                    [new Effect(EffectKind.StatDelta, StatKey.Energy, Fixed.FromInt(10))],
                    [new Effect(EffectKind.StatDelta, StatKey.Burnout, Fixed.FromInt(10)), new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(-5))],
                    null,
                    null)),
        ],
    };
}
```

The `Decision` call relies on `EventDefinition`'s new optional parameters being named `Phase`, `Title`, `OneTime`; the M1 events must use a 1-week cooldown and stay eligible for every category, so the supply check passes for all five categories.

- [ ] **Step 3: Run to see them fail**

Run: `dotnet test --filter ContentValidatorV2Tests`
Expected: compile errors for the new members.

- [ ] **Step 4: Implement the model**

Apply the interface changes listed above to `EventDefinition.cs`, `ContentSnapshot.cs` and `BalanceConstants.cs` (in `BalanceConstants` add `public string? RecoveryCrisisEventId { get; init; }`, `public Fixed DecisionChancePerWeek { get; init; }`, `public ChoiceDefinition? NetworkCollaborate { get; init; }`, `public Fixed NetworkEnergyCost { get; init; }`). Add `StatKey.Followers` as the **last** enum member and in `Stats.Get` return `Fixed.FromInt(s.Followers)`; in `Stats.Add` throw `ArgumentOutOfRangeException` for `Followers` (followers change only through `FollowersPercent` and settlement).

- [ ] **Step 5: Implement the validator**

In `ContentValidator.Validate` keep every existing check, then when `c.Schema == 2` add the schema-2 checks; when `c.Schema == 1`, `IsValidEffect` must reject the six new kinds and `StatKey.Followers` stat deltas. Replace `ValidateWeeklySupply` for schema 2 with a per-category version. Full additions:

```csharp
    private static readonly HashSet<EffectKind> DecisionForbidden =
        [EffectKind.AudienceMultiplier, EffectKind.EventQuality, EffectKind.ViralChance];

    private static void ValidateSchemaTwo(ContentSnapshot c, List<string> violations)
    {
        var ids = c.Events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var setFlags = c.Events
            .SelectMany(e => e.Choices)
            .Concat(c.Balance.NetworkCollaborate is { } network ? [network] : [])
            .SelectMany(AllEffects)
            .Where(effect => effect.Kind == EffectKind.SetFlag)
            .Select(effect => effect.Target!)
            .ToHashSet(StringComparer.Ordinal);
        var followUpTargets = c.Events.SelectMany(e => e.Choices).SelectMany(AllEffects)
            .Where(effect => effect.Kind == EffectKind.FollowUp)
            .Select(effect => effect.Target!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var e in c.Events)
        {
            if (e.Version < 1)
            {
                violations.Add($"version:{e.Id}");
            }

            if (e.Choices.IsDefaultOrEmpty || e.Choices.All(choice => choice.Requires is not null))
            {
                violations.Add($"no-legal-choice:{e.Id}");
            }

            foreach (var (field, text) in Texts(e))
            {
                if (text is null)
                {
                    violations.Add($"text:{e.Id}/{field}:missing");
                }
                else if (IcuMessage.Validate(text) is { } problem)
                {
                    violations.Add($"text:{e.Id}/{field}:{problem}");
                }
            }

            foreach (var flag in (e.Eligibility.RequiresFlags.IsDefault ? [] : e.Eligibility.RequiresFlags).Where(f => !setFlags.Contains(f)))
            {
                violations.Add($"unset-flag:{e.Id}/{flag}");
            }

            foreach (var choice in e.Choices.IsDefault ? [] : e.Choices)
            {
                foreach (var effect in AllEffects(choice).Where(effect => effect.Kind == EffectKind.FollowUp && !ids.Contains(effect.Target!)))
                {
                    violations.Add($"broken-follow-up:{e.Id}/{effect.Target}");
                }

                if (e.Phase != EventPhase.Live && AllEffects(choice).Any(effect => DecisionForbidden.Contains(effect.Kind)))
                {
                    violations.Add($"effect-phase:{e.Id}/{choice.Id}");
                }
            }

            var isDecision = e.Phase != EventPhase.Live;
            var triggered = string.Equals(e.Id, c.Balance.RecoveryCrisisEventId, StringComparison.Ordinal) || followUpTargets.Contains(e.Id);
            if (isDecision && !HasValidWeeks(e) && !triggered)
            {
                violations.Add($"unreachable-decision:{e.Id}");
            }
        }

        if (c.Balance.RecoveryCrisisEventId is { } recovery
            && !c.Events.Any(e => e.Id == recovery && e.Phase == EventPhase.Crisis))
        {
            violations.Add($"missing-recovery-crisis:{recovery}");
        }

        ValidateCategorySupply(c, violations);
    }

    private static IEnumerable<(string Field, string? Text)> Texts(EventDefinition e)
    {
        yield return ("title", e.Title);
        yield return ("text", e.TextPl);
        foreach (var choice in e.Choices.IsDefault ? [] : e.Choices)
        {
            yield return ($"{choice.Id}.label", choice.LabelPl);
            if (choice.SuccessText is { } success)
            {
                yield return ($"{choice.Id}.success", success);
            }

            if (choice.FailureText is { } failure)
            {
                yield return ($"{choice.Id}.failure", failure);
            }
        }
    }

    private static IEnumerable<Effect> AllEffects(ChoiceDefinition choice)
        => choice.Outcomes.Success
            .Concat(choice.Outcomes.Failure)
            .Concat(choice.Outcomes.CriticalSuccess ?? [])
            .Concat(choice.Outcomes.CriticalFailure ?? []);

    /// <summary>Every category needs enough live events by week range and category; reports the first week of each short run.</summary>
    private static void ValidateCategorySupply(ContentSnapshot c, List<string> violations)
    {
        var live = c.Events.Where(e => e.Phase == EventPhase.Live && HasValidWeeks(e)).ToArray();
        foreach (var category in Enum.GetValues<Category>())
        {
            var previousShort = false;
            for (var week = CareerWeek.First; week <= CareerWeek.Last; week++)
            {
                var eligible = live.Count(e => e.Eligibility.FromWeek <= week && week <= e.Eligibility.ToWeek
                    && (e.Eligibility.Categories.IsDefaultOrEmpty || e.Eligibility.Categories.Contains(category)));
                var isShort = eligible < c.Balance.EventsPerWeekMax;
                if (isShort && !previousShort)
                {
                    violations.Add($"too-few-eligible:{category}:week-{week}");
                }

                previousShort = isShort;
            }
        }
    }
```

Extend `IsValidEffect` with the schema parameter:

```csharp
    private static bool IsValidEffect(Effect effect, int schema) => effect.Kind switch
    {
        EffectKind.StatDelta => effect.Stat is { } stat && stat != StatKey.Followers && Abs(effect.Amount) <= Fixed.Hundred,
        EffectKind.CashDelta => effect.Stat is null
            && effect.Amount.Raw % Fixed.Scale == 0
            && Abs(effect.Amount) <= Fixed.FromInt(MaxCashEffectPln),
        EffectKind.AudienceMultiplier => effect.Stat is null && effect.Amount > Fixed.Zero && effect.Amount <= Fixed.FromInt(10),
        EffectKind.EventQuality => effect.Stat is null && Abs(effect.Amount) <= Fixed.Hundred,
        EffectKind.FollowersPercent => schema == 2 && Abs(effect.Amount) <= Fixed.FromInt(20),
        EffectKind.SkillDelta => schema == 2 && effect.Category is not null && Abs(effect.Amount) <= Fixed.FromInt(10),
        EffectKind.ViralChance => schema == 2 && effect.Amount >= Fixed.Zero && effect.Amount <= Fixed.FromInt(30),
        EffectKind.SetFlag => schema == 2 && !string.IsNullOrEmpty(effect.Target) && effect.Weeks is null or >= 1,
        EffectKind.ClearFlag => schema == 2 && !string.IsNullOrEmpty(effect.Target),
        EffectKind.FollowUp => schema == 2 && !string.IsNullOrEmpty(effect.Target) && effect.Weeks is >= 1 and <= 52,
        _ => false,
    };
```

`ValidateEvent` passes `c.Schema`; the network choice in balance (when present) runs through the same choice checks with event ID `balance.networkCollaborate`. For schema 2, `ValidateWeeklySupply` is replaced by `ValidateCategorySupply` (only live events count); for schema 1 it stays as is.

- [ ] **Step 6: Extend the loader**

In `JsonContentSource.Parse` read `"schema"` (optional int, default 1) from `events.json` and `"ruleset"` (optional string, default `"m1-prototype.1"`) from `balance.json`, and pass both to `ContentSnapshot`. In `ReadEvent`, for schema 2 read `version`, `phase` (`live`/`milestone`/`crisis`, camel-case parsed into `EventPhase`), `title`, `text` (into `TextPl`), optional `oneTime`, `tags`, and in eligibility optional `categories`, `requiresFlags`, `forbidsFlags`; `cooldownWeeks` is optional with default `13`. For schema 1 keep reading `titleKey` and `textPl`. In `ReadChoice` for schema 2 read `label` (into `LabelPl`), optional `successText`, `failureText` and `requires` (`minCashPln`, `minEnergy`); for schema 1 keep `labelKey`/`labelPl`. In `ReadEffect` read optional `category`, `flag`/`event` (into `Target`) and `weeks`. In `ReadBalance` read optional `recoveryCrisisEventId`, `decisionChancePerWeek` (default 0), `networkEnergyCost` (default 0) and `networkCollaborate` (a choice object via `ReadChoice(…, schema: 2)`).

Pass the schema through the reader methods as a parameter. Add loader tests to `JsonContentSourceTests.cs`:

```csharp
[Fact]
public void Schema_two_event_reads_icu_text_phase_and_flags()
{
    var events = """
        { "schema": 2, "events": [ {
          "id": "x", "version": 2, "phase": "crisis",
          "title": "T", "text": "{gender, select, female {a} male {b} other {c}}",
          "weight": 1, "oneTime": true, "tags": ["rivalry"],
          "eligibility": { "fromWeek": 1, "toWeek": 1040, "categories": ["Chess"], "requiresFlags": ["f"] },
          "choices": [ { "id": "ok", "label": "L", "guaranteed": true,
            "outcomes": { "success": [ { "kind": "SetFlag", "flag": "f", "weeks": 4, "amount": 0 } ] } } ] } ] }
        """u8.ToArray();
    var e = JsonContentSource.ParseEvents(events).Single();
    Assert.Equal((2, EventPhase.Crisis, "T", 13, true), (e.Version, e.Phase, e.Title, e.CooldownWeeks, e.OneTime));
    Assert.Equal([Category.Chess], e.Eligibility.Categories);
    Assert.Equal(("f", 4), (e.Choices[0].Outcomes.Success[0].Target, e.Choices[0].Outcomes.Success[0].Weeks));
}
```

Expose `public static ImmutableArray<EventDefinition> ParseEvents(byte[] eventsBytes)` from `JsonContentSource` (used by this test and by Task 6) that reads the schema field and the events array without validation.

- [ ] **Step 7: Run tests**

Run: `dotnet test`
Expected: PASS, including golden and the existing `JsonContentSourceTests` for M1.

- [ ] **Step 8: Commit**

```bash
git add source/backend
git commit -m "feat(content): add event schema v2 with phases, flags, follow-ups and validated ICU text

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 6: Refuse a changed event under the same version

**Files:**
- Create: `source/backend/Infrastructure/Content/EventVersions.cs`
- Modify: `source/backend/Infrastructure/Persistence/EfContentStore.cs`, `source/backend/Tests/Support/InMemoryContentStore.cs`
- Test: `source/backend/Tests/Infrastructure/EventVersionsTests.cs`, `source/backend/Tests/Infrastructure/Persistence/EfContentStoreTests.cs` (add one)

**Interfaces:**
- Produces: `EventVersions.Conflicts(byte[] candidateEvents, IEnumerable<byte[]> storedEvents) : IReadOnlyList<string>` returning `"{id}@{version}"` for every schema-2 event whose JSON differs (`JsonElement.DeepEquals`) from a stored event with the same ID and version.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using PolskiStreamerSimulator.Infrastructure.Content;

namespace PolskiStreamerSimulator.Tests.Infrastructure;

public class EventVersionsTests
{
    private static byte[] Events(string body) => Encoding.UTF8.GetBytes($$"""{ "schema": 2, "events": [ {{body}} ] }""");

    [Fact]
    public void Same_version_with_different_content_conflicts()
    {
        var stored = Events("""{ "id": "a", "version": 1, "text": "x" }""");
        var candidate = Events("""{ "id": "a", "version": 1, "text": "y" }""");
        Assert.Equal(["a@1"], EventVersions.Conflicts(candidate, [stored]));
    }

    [Fact]
    public void Whitespace_and_property_order_do_not_conflict()
    {
        var stored = Events("""{ "id": "a", "version": 1, "text": "x" }""");
        var candidate = Events("""{"text":"x","version":1,"id":"a"}""");
        Assert.Empty(EventVersions.Conflicts(candidate, [stored]));
    }

    [Fact]
    public void New_version_does_not_conflict()
    {
        var stored = Events("""{ "id": "a", "version": 1, "text": "x" }""");
        var candidate = Events("""{ "id": "a", "version": 2, "text": "y" }""");
        Assert.Empty(EventVersions.Conflicts(candidate, [stored]));
    }

    [Fact]
    public void Schema_one_files_are_ignored()
    {
        var m1 = Encoding.UTF8.GetBytes("""{ "events": [ { "id": "a", "textPl": "x" } ] }""");
        Assert.Empty(EventVersions.Conflicts(Events("""{ "id": "a", "version": 1, "text": "y" }"""), [m1]));
    }
}
```

`JsonElement.DeepEquals` treats objects as equal regardless of property order (.NET 9+); confirm this holds in the test above.

In `EfContentStoreTests.cs` add a Testcontainers test that stores `M3` files, then stores files with one event's text changed under the same version and expects `InvalidDataException` whose message contains `event-version-changed:` and leaves the row count at one.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter EventVersionsTests`
Expected: compile error.

- [ ] **Step 3: Implement**

```csharp
using System.Text.Json;

namespace PolskiStreamerSimulator.Infrastructure.Content;

/// <summary>A published event version never changes: the same (id, version) must have the same JSON everywhere.</summary>
public static class EventVersions
{
    public static IReadOnlyList<string> Conflicts(byte[] candidateEvents, IEnumerable<byte[]> storedEvents)
    {
        using var candidate = JsonDocument.Parse(candidateEvents);
        var byKey = Versioned(candidate.RootElement).ToDictionary(e => Key(e), e => e, StringComparer.Ordinal);
        var conflicts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var stored in storedEvents)
        {
            using var document = JsonDocument.Parse(stored);
            foreach (var old in Versioned(document.RootElement))
            {
                if (byKey.TryGetValue(Key(old), out var current) && !JsonElement.DeepEquals(old, current))
                {
                    conflicts.Add(Key(old));
                }
            }
        }

        return [.. conflicts];
    }

    private static IEnumerable<JsonElement> Versioned(JsonElement root)
        => root.TryGetProperty("schema", out var schema) && schema.GetInt32() == 2
            ? root.GetProperty("events").EnumerateArray()
            : [];

    private static string Key(JsonElement e) => $"{e.GetProperty("id").GetString()}@{e.GetProperty("version").GetInt32()}";
}
```

In `EfContentStore.EnsureStoredAsync`, when the digest is not yet stored, load `EventsJson` of every stored row (`AsNoTracking().Select(r => r.EventsJson).ToListAsync`) and throw `new InvalidDataException($"Content changes published events: {string.Join(", ", conflicts.Select(k => $"event-version-changed:{k}"))}")` when `Conflicts` is not empty. Apply the same check in `InMemoryContentStore`.

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter "EventVersionsTests|EfContentStoreTests"`
Expected: PASS (Docker must be running for the Testcontainers test).

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(content): refuse a snapshot that changes a published event version

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 7: `M3Ruleset` with flags, follow-ups and the decision mechanism

**Files:**
- Create: `source/backend/Domain/Rulesets/M3Ruleset.cs`, `source/backend/Domain/Rulesets/M3/Flags.cs`, `source/backend/Domain/Rulesets/M3/DecisionQueue.cs`
- Modify: `source/backend/Domain/Rulesets/IRuleset.cs`, `M1Ruleset.cs`, `source/backend/Domain/Simulation/WeekEngine.cs`, `DomainError.cs`, `EventSelector.cs`, `WeekReport.cs`, `CareerRunner.cs`, `source/backend/Domain/Rulesets/Shared/EffectApplier.cs`, `source/backend/Tests/Support/LegacyRuleset.cs`, `PausingRuleset.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3DecisionTests.cs`, `M3FlagTests.cs`, `M3FollowUpTests.cs`

**Interfaces:**
- Consumes: Task 2 state fields, Task 3 `RuleSteps`/`EffectApplier`, Task 5 model, `M3TestContent`.
- Produces:
  - `IRuleset.PresentDecision(CareerState s, ContentSnapshot c) : Result<EventPresentation>`
  - `IRuleset.ResolveDecision(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion) : Result<CareerState>`
  - `IRuleset.PresentAction(CareerState s, ContentSnapshot c, WeekPlan plan) : ChoiceOdds?` (M1: always `null`; used in Task 9)
  - `WeekEngine` passthroughs with the same names.
  - `DomainErrorCode.NoPendingDecision`, `DomainErrorCode.DecisionPending`
  - `M3Ruleset.RulesetId = "m3-systems.1"`
  - `WeekReport` gains optional trailing members `ImmutableArray<RollResult> DecisionRolls = default`, `RollResult? ActionRoll = null`.
  - `Flags.IsActive(CareerState s, string flag) : bool`, `Flags.Set(CareerState s, string flag, int? weeks) : CareerState`, `Flags.Clear(...)`, `Flags.Expire(CareerState s, int settledWeek) : CareerState`
  - `DecisionQueue.Open(CareerState s, ContentSnapshot c, ref Pcg32 rng) : CareerState` (fills `PendingDecisions` for the new `s.Week`)
  - `EffectApplier.ToCarry(CareerState s, string sourceId, ImmutableArray<Effect> effects) : CareerState` (decisions)
  - `CareerRunner` answers queued decisions through `policy.ChooseEventChoice` before planning.

- [ ] **Step 1: Write the failing tests**

`M3DecisionTests.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3DecisionTests
{
    private static readonly WeekEngine Engine = new(new RulesetRegistry([new M1Ruleset(), new M3Ruleset()]));
    private static readonly ContentSnapshot C = M3TestContent.Valid();

    private static CareerState Fresh(ulong seed = 3)
        => CareerFactory.Create(M3Ruleset.RulesetId, C.Digest, C.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, seed);

    private static CareerState PlayWeek(CareerState s)
    {
        s = Engine.StartWeek(s, C, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion).Value;
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            var e = Engine.PresentEvent(s, C).Value;
            s = Engine.ResolveEvent(s, C, e.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return Engine.SettleWeek(s, C, s.StateVersion).Value.State;
    }

    [Fact]
    public void One_time_milestone_opens_before_week_two_and_blocks_start()
    {
        var s = PlayWeek(Fresh());
        Assert.Equal(["first-week"], s.PendingDecisions);
        Assert.True(Engine.RequiresPlayerDecision(s, C).Value);
        var start = Engine.StartWeek(s, C, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion);
        Assert.Equal(DomainErrorCode.DecisionPending, start.Error.Code);
    }

    [Fact]
    public void Resolving_a_decision_applies_stats_now_and_carries_cash_into_the_next_report()
    {
        var s = PlayWeek(Fresh());
        var cash = s.CashPln;
        s = Engine.ResolveDecision(s, C, "bold", s.StateVersion).Value;
        Assert.Empty(s.PendingDecisions!.Value);
        Assert.Equal(cash, s.CashPln);
        Assert.Single(s.Carry!.Rolls);
        Assert.Contains("first-week", s.UsedOneTime!);

        s = Engine.StartWeek(s, C, new WeekPlan(MainAction.NoAction, Category.Fps), s.StateVersion).Value;
        Assert.Null(s.Carry);
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, C, Engine.PresentEvent(s, C).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        var (after, report) = Engine.SettleWeek(s, C, s.StateVersion).Value;
        Assert.Single(report.DecisionRolls);
        Assert.Equal(after.CashPln - s.CashPln, report.CashDelta);
        Assert.Contains(report.Income.Concat(report.Costs), l => l.Detail == "first-week");
    }

    [Fact]
    public void One_time_milestone_never_opens_twice()
    {
        var s = PlayWeek(Fresh());
        s = Engine.ResolveDecision(s, C, "calm", s.StateVersion).Value;
        for (var i = 0; i < 30 && s.Status == CareerStatus.Active; i++)
        {
            while (s.PendingDecisions is { Length: > 0 } queue)
            {
                Assert.NotEqual("first-week", queue[0]);
                s = Engine.ResolveDecision(s, C, "calm", s.StateVersion).Value;
            }

            s = PlayWeek(s);
        }
    }

    [Fact]
    public void Resolve_decision_without_one_is_rejected()
        => Assert.Equal(DomainErrorCode.NoPendingDecision, Engine.ResolveDecision(Fresh(), C, "calm", 0).Error.Code);

    [Fact]
    public void M1_career_has_no_decisions()
    {
        var m1 = TestWorld.Content;
        var s = CareerFactory.Create(M1Ruleset.RulesetId, m1.Digest, m1.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 3);
        Assert.Equal(DomainErrorCode.NoPendingDecision, Engine.PresentDecision(s, m1).Error.Code);
    }

    [Fact]
    public void Repeatable_decisions_average_at_most_one_per_thirteen_weeks()
    {
        var opened = 0;
        var weeks = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var s = Fresh(seed);
            for (var i = 0; i < 104 && s.Status == CareerStatus.Active; i++, weeks++)
            {
                while (s.PendingDecisions is { Length: > 0 } queue)
                {
                    opened += queue[0] == "test-crisis" ? 1 : 0;
                    s = Engine.ResolveDecision(s, C, "calm", s.StateVersion).Value;
                }

                s = PlayWeek(s);
            }
        }

        Assert.InRange(opened / (double)weeks, 0.0, 1.0 / 13);
    }
}
```

`M3FlagTests.cs`: a `SetFlag` effect with `weeks: 1` resolved as a decision before week *w* is active during week *w* (`Flags.IsActive`) and gone after its settlement; without `weeks` it lasts until week 1,040; `ClearFlag` removes it; a live event with `RequiresFlags = ["x"]` is selectable only while `x` is active and one with `ForbidsFlags = ["x"]` only while it is not. Build the states directly with `Flags.Set`, then call `EventSelector.Select` with a content containing just enough live events.

`M3FollowUpTests.cs`: a live choice with `FollowUp(target: "test-crisis", weeks: 2)` resolved in week 1 puts `ScheduledEvent("test-crisis", 3)` in `Scheduled`; after settling week 2 the queue opens it before week 3 even though `DecisionChancePerWeek` is 0; a follow-up to a live event is placed in the first event slot of its due week and removed from `Scheduled`.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "M3DecisionTests|M3FlagTests|M3FollowUpTests"`
Expected: compile errors (`M3Ruleset` missing).

- [ ] **Step 3: Implement the interface additions**

`IRuleset` gains the three members; `M1Ruleset` implements them as:

```csharp
    public Result<EventPresentation> PresentDecision(CareerState s, ContentSnapshot c)
        => Result<EventPresentation>.Fail(DomainErrorCode.NoPendingDecision, "no-pending-decision");

    public Result<CareerState> ResolveDecision(CareerState s, ContentSnapshot c, string choiceId, long expectedVersion)
        => Result<CareerState>.Fail(DomainErrorCode.NoPendingDecision, "no-pending-decision");

    public ChoiceOdds? PresentAction(CareerState s, ContentSnapshot c, WeekPlan plan) => null;
```

Update `LegacyRuleset` and `PausingRuleset` in `Tests/Support` to delegate the new members to their wrapped ruleset. Add the passthroughs to `WeekEngine` following the existing pattern.

- [ ] **Step 4: Implement `Flags` and `DecisionQueue`**

```csharp
using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3;

public static class Flags
{
    public static bool IsActive(CareerState s, string flag)
        => s.Flags is { } flags && flags.TryGetValue(flag, out var last) && last >= s.Week.Value;

    /// <summary>Active from now through <c>weeks</c> weeks including the current one; permanent without <c>weeks</c>.</summary>
    public static CareerState Set(CareerState s, string flag, int? weeks)
        => s with
        {
            Flags = (s.Flags ?? ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal))
                .SetItem(flag, weeks is { } w ? s.Week.Value + w - 1 : CareerWeek.Last),
        };

    public static CareerState Clear(CareerState s, string flag)
        => s.Flags is { } flags ? s with { Flags = flags.Remove(flag) } : s;

    public static CareerState Expire(CareerState s, int settledWeek)
        => s.Flags is { } flags ? s with { Flags = flags.RemoveRange(flags.Where(f => f.Value <= settledWeek).Select(f => f.Key)) } : s;
}
```

```csharp
using System.Collections.Immutable;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Domain.Rulesets.M3;

/// <summary>
/// Opens the mandatory decisions before a week, in order: due decision follow-ups, the recovery crisis when energy
/// is 0 or burnout 100, the first eligible one-time milestone, then — only if nothing opened — one repeatable
/// milestone or crisis after a seeded <see cref="BalanceConstants.DecisionChancePerWeek"/> roll. At most two open.
/// </summary>
public static class DecisionQueue
{
    public const int MaxPerWeek = 2;

    public static CareerState Open(CareerState s, ContentSnapshot c, ref Pcg32 rng)
    {
        var week = s.Week.Value;
        var queue = ImmutableArray.CreateBuilder<string>();
        var scheduled = (s.Scheduled ?? []).ToList();
        foreach (var due in scheduled.Where(x => x.DueWeek <= week && IsDecision(c, x.EventId)).ToList())
        {
            if (queue.Count < MaxPerWeek)
            {
                queue.Add(due.EventId);
                scheduled.Remove(due);
            }
        }

        if (c.Balance.RecoveryCrisisEventId is { } recovery
            && (s.Energy == Fixed.Zero || s.Burnout == Fixed.Hundred)
            && queue.Count < MaxPerWeek && !queue.Contains(recovery))
        {
            queue.Add(recovery);
        }

        var candidates = c.Events
            .Where(e => e.Phase != EventPhase.Live && e.Id != c.Balance.RecoveryCrisisEventId && !queue.Contains(e.Id))
            .Where(e => EventSelector.IsEligible(s, e, category: null))
            .ToList();
        if (queue.Count < MaxPerWeek && candidates.FirstOrDefault(e => e.OneTime) is { } milestone)
        {
            queue.Add(milestone.Id);
        }
        else if (queue.Count == 0 && c.Balance.DecisionChancePerWeek > Fixed.Zero && candidates.Any(e => !e.OneTime))
        {
            (var draw, rng) = rng.NextInt(1, 100);
            if (Fixed.FromInt(draw) <= c.Balance.DecisionChancePerWeek * Fixed.Hundred)
            {
                var repeatable = candidates.Where(e => !e.OneTime).ToList();
                (var ticket, rng) = rng.NextInt(1, repeatable.Sum(e => e.Weight));
                queue.Add(EventSelector.PickByWeight(repeatable, ticket).Id);
            }
        }

        return s with
        {
            PendingDecisions = queue.ToImmutable(),
            Scheduled = [.. scheduled],
        };
    }

    private static bool IsDecision(ContentSnapshot c, string id)
        => c.Events.FirstOrDefault(e => e.Id == id) is { Phase: not EventPhase.Live };
}
```

Refactor `EventSelector`: make `IsEligible(CareerState s, EventDefinition e, Category? category)` public; it additionally checks `e.Eligibility.Categories` (only when `category` is not null), `RequiresFlags`/`ForbidsFlags` via `Flags.IsActive`, `e.OneTime && s.UsedOneTime contains e.Id` → ineligible, and **only live events** when called from `Select`. Extract `PickByWeight(IReadOnlyList<EventDefinition> candidates, int ticket)` from the existing loop (same cumulative order). Add a `Select` overload used by M3 that first takes due live follow-ups from `s.Scheduled` (in order, up to the drawn count, skipping ones that are cooling down) and then draws the rest; M1 keeps calling the original overload so its draw order is unchanged. For schema-1 content every event is `Live` and has no categories or flags, so M1 eligibility is unchanged — the golden test confirms it.

- [ ] **Step 5: Implement `EffectApplier` extensions**

In `EffectApplier.ToWeek` add cases:

```csharp
                case EffectKind.FollowersPercent:
                    pending = pending with { FollowersPercent = (pending.FollowersPercent ?? Fixed.Zero) + effect.Amount };
                    break;
                case EffectKind.SkillDelta:
                    var category = effect.Category!.Value;
                    s = s with { Skill = s.Skill.With(category, Stats.Bounded(s.Skill[category] + effect.Amount)) };
                    break;
                case EffectKind.ViralChance:
                    pending = pending with { ViralChance = (pending.ViralChance ?? Fixed.Zero) + effect.Amount };
                    break;
                case EffectKind.SetFlag:
                    s = Flags.Set(s, effect.Target!, effect.Weeks);
                    break;
                case EffectKind.ClearFlag:
                    s = Flags.Clear(s, effect.Target!);
                    break;
                case EffectKind.FollowUp:
                    s = s with { Scheduled = (s.Scheduled ?? []).Add(new ScheduledEvent(effect.Target!, s.Week.Value + effect.Weeks!.Value)) };
                    break;
```

Add `ToCarry` for decisions: `StatDelta`, `SkillDelta`, `SetFlag`, `ClearFlag`, `FollowUp` behave as above (applied to the state now); `CashDelta` adds to `Carry.CashPln` and appends an `income:event`/`cost:event` line with `Detail: sourceId`; `FollowersPercent` adds to `Carry.FollowersPercent`; stat-change lines (`Stats.Changes(before, after, sourceId)`) are appended to `Carry.Lines`. Other kinds throw (`ContentValidator` forbids them in decisions).

- [ ] **Step 6: Implement `M3Ruleset`**

`M3Ruleset` copies `M1Ruleset`'s structure and calls the shared steps. Differences in this task:

- `StartWeek`: rejects with `DecisionPending`/`"decision-pending"` when `PendingDecisions` is not empty; uses the M3 `EventSelector.Select` overload with the plan's category; moves `s.Carry` into the pending week — `CashDeltaFromEvents += carry.CashPln`, `Lines = carry.Lines.AddRange(actionLines)`, `FollowersPercent = carry.FollowersPercent`, `Carry = carry` — and sets `s.Carry = null`.
- `ResolveEvent`: also records one-time use (`UsedOneTime`) for one-time live events.
- `SettleWeek`: after the M1 settlement steps, applies `FollowersPercent` to followers (`round(followers × pct / 100)`, after the audience net change, clamped so followers stay ≥ 0, reported as `ReportLine("followers:event", AmountPln: null, Delta: pct)`), puts `pending.Carry.Rolls` into `WeekReport.DecisionRolls`, expires flags (`Flags.Expire(state, s.Week.Value)`), and — when the career did not end — calls `DecisionQueue.Open` for the new week with the state's RNG.
- `PresentDecision`: fails with `NoPendingDecision` when the queue is empty, otherwise returns odds for the first queued event exactly like `PresentEvent`.
- `ResolveDecision`: `CommandRejection`, find the first queued event, `OddsCalculator.For`, `RuleSteps.Roll`, `EffectApplier.ToCarry`, append the roll to `Carry.Rolls`, set the cooldown and one-time use, pop the queue, increment `StateVersion`.
- `RequiresPlayerDecision`: `s.PendingDecisions is { Length: > 0 }`.
- `LegalPlans`, `PlanRejection`, `ApplyAction`: same as M1 in this task (Task 9 and Task 10 change them).

`OddsCalculator.PrimaryCategory` must not throw for decisions: when `s.Pending` is null and the stat is a primary-category stat, use the category of the last settled plan if available, otherwise `Category.JustChatting`; document this in the method summary. (Decisions should prefer non-primary stats; the content review checks it.)

- [ ] **Step 7: Make the runner answer decisions**

In `CareerRunner.Run`, before `policy.ShouldRetire`, loop while `engine.PresentDecision(s, c)` succeeds: choose with `policy.ChooseEventChoice(s, presentation)`, `engine.ResolveDecision`, check invariants. Stop on any failure other than `NoPendingDecision`.

- [ ] **Step 8: Run tests**

Run: `dotnet test`
Expected: PASS, golden unchanged. If `Repeatable_decisions_average_at_most_one_per_thirteen_weeks` fails, verify the queue only rolls when nothing else opened — do not loosen the bound.

- [ ] **Step 9: Commit and open PR 1**

```bash
git add source/backend
git commit -m "feat(domain): add the M3 ruleset with flags, follow-ups and mandatory decisions

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/m3a-1-foundation
gh pr create --base develop --title "M3-A PR 1: rules and content foundation" --body "$(cat <<'EOF'
Part of #21 (schema v2, validator, ICU, immutability) and the decision mechanism for #17. M1 results are pinned by a golden state-hash test.

Spec: docs/superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md
Plan: docs/superpowers/plans/2026-10-16-m3a-rules-and-content-foundation.md (Tasks 0–7)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI, check SonarQube Cloud (zero open issues), and ask the owner to merge.

---

# PR 2 — Rules and content

Start from the latest `develop` after PR 1 merged: `git switch -c feature/m3a-2-rules origin/develop`.

Shared test helper for this PR — create `source/backend/Tests/Support/M3Play.cs` in Task 8, Step 1:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;

namespace PolskiStreamerSimulator.Tests.Support;

/// <summary>Plays M3 careers on <see cref="M3TestContent"/> for rule tests.</summary>
public static class M3Play
{
    public static WeekEngine Engine { get; } = new(new RulesetRegistry([new M1Ruleset(), new M3Ruleset()]));

    public static ContentSnapshot C => M3TestContent.Valid();

    public static CareerState Fresh(ulong seed = 3)
        => CareerFactory.Create(M3Ruleset.RulesetId, C.Digest, C.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, seed);

    public static CareerState Start(CareerState s, MainAction action, Category category = Category.Fps)
        => Engine.StartWeek(s, C, new WeekPlan(action, category), s.StateVersion).Value;

    /// <summary>Resolves every live event with its last (guaranteed in test content) choice, then settles.</summary>
    public static (CareerState State, WeekReport Report) SettleAll(CareerState s)
    {
        while (s.Pending!.Resolved.Length < s.Pending.EventIds.Length)
        {
            s = Engine.ResolveEvent(s, C, Engine.PresentEvent(s, C).Value.Choices[^1].ChoiceId, s.StateVersion).Value;
        }

        return Engine.SettleWeek(s, C, s.StateVersion).Value;
    }

    public static CareerState ResolveQueued(CareerState s, string choiceId)
        => Engine.ResolveDecision(s, C, choiceId, s.StateVersion).Value;

    /// <summary>Re-seeds the career until resolving <paramref name="choiceId"/> gives <paramref name="wanted"/>.</summary>
    public static CareerState ResolveQueuedUntil(CareerState s, string choiceId, RollOutcome wanted)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var next = ResolveQueued(s with { Rng = Pcg32.Create(seed, CareerFactory.CareerStream) }, choiceId);
            if (next.Carry!.Rolls[^1].Outcome == wanted)
            {
                return next;
            }
        }

        throw new InvalidOperationException($"No seed in 1–200 gives {wanted}.");
    }

    /// <summary>Plays Gambling weeks over seeds until a report has a viral clip.</summary>
    public static (CareerState State, WeekReport Report) UntilViral()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var s = Fresh(seed) with { Followers = 20_000 };
            for (var week = 0; week < 30; week++)
            {
                while (s.PendingDecisions is { Length: > 0 })
                {
                    s = ResolveQueued(s, Engine.PresentDecision(s, C).Value.Choices[0].ChoiceId);
                }

                var settled = SettleAll(Start(s, MainAction.NoAction, Category.Gambling));
                if (settled.Report.Viral is not null)
                {
                    return settled;
                }

                s = settled.State;
            }
        }

        throw new InvalidOperationException("No viral week found.");
    }
}
```

Move the local `Engine`, `C`, `Fresh` and `PlayWeek` helpers of Task 7's tests onto `M3Play` in the same step.

### Task 8: Category profiles, mastery and the boredom rebalance as balance data

**Files:**
- Create: `source/backend/Domain/Content/CategoryProfile.cs`, `source/backend/Tests/Support/M3Play.cs`
- Modify: `source/backend/Domain/Content/BalanceConstants.cs`, `source/backend/Infrastructure/Content/JsonContentSource.cs`, `source/backend/Domain/Simulation/Settlement/AudienceRules.cs`, `FinanceRules.cs`, `ProgressionRules.cs`, `source/backend/Domain/Rulesets/M3Ruleset.cs`
- Test: `source/backend/Tests/Domain/Simulation/CategoryProfileTests.cs`

**Interfaces:**
- Produces:
  - `record CategoryProfile(Fixed Reach, Fixed NoiseMin, Fixed NoiseMax, Fixed Conversion, Fixed Churn, int BoredomGainMin, int BoredomGainMax, Fixed SubYield, Fixed DonationYield, Fixed AdYield, Fixed QualitySkillWeight, Fixed CredibilityPerStream, Fixed DramaPerStream, Fixed ViralBase)` with `static CategoryProfile Neutral(BalanceConstants b)` (reach, conversion, churn, yields and skill weight 1; noise and boredom gain from the global balance values; per-stream effects and viral 0).
  - `BalanceConstants.Categories : CategoryValues<CategoryProfile>?` (null in M1), `BalanceConstants.Profile(Category c) : CategoryProfile` (returns `Neutral(this)` when `Categories` is null), `MasteryFromSkill : Fixed` (M1 default 101), `MasteryBoredomRelief : int` (default 0), `MasteryChurnFactor : Fixed` (default 1).
  - `ProgressionRules.ApplyCategoryReputation(CareerState s, Category primary, BalanceConstants b) : CareerState`.

- [ ] **Step 1: Write the failing tests**

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class CategoryProfileTests
{
    private static readonly BalanceConstants M1 = TestWorld.Content.Balance;

    private static BalanceConstants With(Category category, Func<CategoryProfile, CategoryProfile> change)
    {
        var all = new CategoryValues<CategoryProfile>([.. Enum.GetValues<Category>().Select(M1.Profile)]);
        return M1 with { Categories = all.With(category, change(M1.Profile(category))) };
    }

    private static PendingWeek Week(Category category)
        => new(new CareerWeek(5), new WeekPlan(MainAction.NoAction, category), [], [], Fixed.Zero, Fixed.One, 0, []);

    [Fact]
    public void M1_profiles_are_neutral()
    {
        Assert.Null(M1.Categories);
        Assert.All(Enum.GetValues<Category>(), c => Assert.Equal(CategoryProfile.Neutral(M1), M1.Profile(c)));
    }

    [Fact]
    public void Reach_scales_live_viewers()
    {
        var s = TestCareers.Fresh() with { Followers = 10_000 };
        var rng1 = s.Rng;
        var rng2 = s.Rng;
        var neutral = AudienceRules.Compute(s, Week(Category.Chess), ref rng1, M1);
        var lower = AudienceRules.Compute(s, Week(Category.Chess), ref rng2, With(Category.Chess, p => p with { Reach = Fixed.FromRatio(3, 4) }));
        Assert.True(lower.LiveViewers < neutral.LiveViewers);
    }

    [Fact]
    public void Subscription_yield_tilts_income()
    {
        var s = TestCareers.Fresh();
        long Subs(BalanceConstants b) => FinanceRules.Compute(s, Week(Category.Chess), 1_000, b).Income.Single(l => l.Code == "income:subscriptions").AmountPln!.Value;
        Assert.True(Subs(With(Category.Chess, p => p with { SubYield = Fixed.FromRatio(13, 10) })) > Subs(M1));
    }

    [Fact]
    public void Mastery_lowers_boredom_gain_for_a_master()
    {
        var b = M1 with { MasteryFromSkill = Fixed.FromInt(70), MasteryBoredomRelief = 1 };
        var master = TestCareers.Fresh() with { Skill = CategoryValues<Fixed>.Uniform(Fixed.FromInt(80)) };
        var novice = master with { Skill = CategoryValues<Fixed>.Uniform(Fixed.FromInt(20)) };
        var rngA = master.Rng;
        var rngB = novice.Rng;
        var a = ProgressionRules.ApplySkillAndBoredom(master, Category.Fps, master.Skill, ref rngA, b);
        var n = ProgressionRules.ApplySkillAndBoredom(novice, Category.Fps, novice.Skill, ref rngB, b);
        Assert.Equal(n.Boredom[Category.Fps] - Fixed.One, a.Boredom[Category.Fps]);
    }

    [Fact]
    public void Per_stream_reputation_effects_apply_only_to_their_category()
    {
        var b = With(Category.Gambling, p => p with { DramaPerStream = Fixed.One, CredibilityPerStream = Fixed.FromRatio(-3, 10) });
        var s = TestCareers.Fresh();
        var after = ProgressionRules.ApplyCategoryReputation(s, Category.Gambling, b);
        Assert.Equal((s.Drama + Fixed.One, s.Credibility - Fixed.FromRatio(3, 10)), (after.Drama, after.Credibility));
        Assert.Equal(s, ProgressionRules.ApplyCategoryReputation(s, Category.Fps, b));
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter CategoryProfileTests`
Expected: compile errors.

- [ ] **Step 3: Implement**

`CategoryProfile.cs` with the record and `Neutral`. In the formulas, insert each profile factor next to the existing term so a neutral `Fixed.One` leaves the raw value unchanged (`x × One` is exact in `Fixed`; the golden test proves it):

- `AudienceRules.Compute`: the quality term becomes `(b.QualitySkill * profile.QualitySkillWeight) * skill`; the baseline multiplies `reach * b.PlatformReach * profile.Reach * (QualityOffset + quality/100)`; the noise draw uses `profile.NoiseMin, profile.NoiseMax`; `gained` multiplies by `profile.Conversion` last; `lost` uses `followers * ((b.ChurnRate + (b.ChurnBoredomFactor * boredom)) * profile.Churn * mastery)` with `mastery = skill >= b.MasteryFromSkill ? b.MasteryChurnFactor : Fixed.One`.
- `FinanceRules.Compute`: subscriptions, donations and ads each multiply by `profile.SubYield`, `profile.DonationYield`, `profile.AdYield` as the last factor before rounding.
- `ProgressionRules.ApplySkillAndBoredom`: the primary category draws `rng.NextInt(profile.BoredomGainMin, profile.BoredomGainMax)`; when skill ≥ `b.MasteryFromSkill` and `b.MasteryBoredomRelief > 0`, the gain becomes `max(1, gain − relief)`.
- `ProgressionRules.ApplyCategoryReputation`: adds `CredibilityPerStream` and `DramaPerStream` through `Stats.Add`; returns `s` unchanged when both are zero. Only `M3Ruleset.SettleWeek` calls it, right after stream fatigue, and its stat lines use detail `"stream"`.

Loader: optional `"categories": { "<Category>": { "reach": …, "noiseMin": …, "noiseMax": …, "conversion": …, "churn": …, "boredomGainMin": …, "boredomGainMax": …, "subYield": …, "donationYield": …, "adYield": …, "qualitySkillWeight": …, "credibilityPerStream": …, "dramaPerStream": …, "viralBase": … } }` (all five categories required when the section exists) and `"mastery": { "fromSkill": …, "boredomRelief": …, "churnFactor": … }`.

- [ ] **Step 4: Run tests**

Run: `dotnet test`
Expected: PASS, golden unchanged.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): give categories their own reach, volatility, churn, income and reputation profiles

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 9: `NetworkCollaborate` and the burnout odds penalty

**Files:**
- Modify: `source/backend/Domain/Simulation/WeekPlan.cs`, `OddsCalculator.cs`, `source/backend/Domain/Content/BalanceConstants.cs`, `source/backend/Infrastructure/Content/JsonContentSource.cs`, `source/backend/Domain/Rulesets/M3Ruleset.cs`, `M1Ruleset.cs`, `source/backend/Tests/Support/M3TestContent.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3ActionTests.cs`, `source/backend/Tests/Domain/Simulation/OddsCalculatorTests.cs` (add)

**Interfaces:**
- Produces:
  - `MainAction.NetworkCollaborate` (appended last).
  - `BalanceConstants.BurnoutPenaltyFrom : Fixed` (M1 default 101), `BurnoutPenaltyPerPoint : Fixed` (default 0), `BurnoutPenaltyCap : Fixed` (default 0); modifier label `modifier.burnout`; loader section `"burnoutPenalty": { "from", "perPoint", "cap" }`.
  - `M3Ruleset.PresentAction` returns `OddsCalculator.For(s, b.NetworkCollaborate!, b)` for a network plan, `null` otherwise.
  - `PendingWeek.ActionRoll` set in a network week; `WeekReport.ActionRoll` copies it.

- [ ] **Step 1: Extend the test content**

In `M3TestContent.Build` set in the balance: `NetworkEnergyCost = Fixed.FromInt(10)` and

```csharp
NetworkCollaborate = new ChoiceDefinition(
    "collaborate", null, "Zagadaj do innych twórców", false, Fixed.FromRatio(35, 100),
    [
        new ChanceModifier(StatKey.Credibility, Fixed.FromRatio(3, 1000), null, "modifier.credibility"),
        new ChanceModifier(StatKey.Drama, Fixed.FromRatio(-3, 1000), null, "modifier.drama"),
    ],
    true,
    new OutcomeSet(
        [new Effect(EffectKind.AudienceMultiplier, null, Fixed.FromRatio(12, 10)), new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.One)],
        [new Effect(EffectKind.StatDelta, StatKey.Drama, Fixed.FromInt(4)), new Effect(EffectKind.StatDelta, StatKey.Credibility, Fixed.FromInt(-2))],
        [new Effect(EffectKind.SetFlag, null, Fixed.Zero, Target: "collab-friend", Weeks: 26)],
        [new Effect(EffectKind.FollowUp, null, Fixed.Zero, Target: "test-crisis", Weeks: 1)])),
```

and `BurnoutPenaltyFrom = 60`, `BurnoutPenaltyPerPoint = 0.0025`, `BurnoutPenaltyCap = 0.10` (as `Fixed.FromRatio`).

- [ ] **Step 2: Write the failing tests**

`OddsCalculatorTests.cs` (build a choice with a credibility modifier so no primary-category stat is read):

```csharp
private static readonly ChoiceDefinition CredibilityChoice = new(
    "c", null, "c", false, Fixed.FromRatio(1, 2),
    [new ChanceModifier(StatKey.Credibility, Fixed.FromRatio(1, 1000), null, "modifier.credibility")],
    false,
    new OutcomeSet([], [], null, null));

[Theory]
[InlineData(59, 0)]
[InlineData(60, 0)]
[InlineData(80, -5)]
[InlineData(100, -10)]
public void Burnout_penalty_grows_above_sixty_and_is_capped(int burnout, int expectedPoints)
{
    var s = TestCareers.Fresh() with { Burnout = Fixed.FromInt(burnout) };
    var line = OddsCalculator.For(s, CredibilityChoice, M3Play.C.Balance).Modifiers.SingleOrDefault(m => m.LabelKey == "modifier.burnout");
    Assert.Equal(expectedPoints, line is null ? 0 : (int)(line.Value * Fixed.Hundred).RoundToLong());
}

[Fact]
public void M1_balance_never_adds_a_burnout_modifier()
{
    var s = TestCareers.Fresh() with { Burnout = Fixed.Hundred };
    Assert.DoesNotContain(OddsCalculator.For(s, CredibilityChoice, TestWorld.Content.Balance).Modifiers, m => m.LabelKey == "modifier.burnout");
}
```

`M3ActionTests.cs`:

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3ActionTests
{
    [Fact]
    public void Network_plan_is_offered_with_visible_odds_in_every_category()
    {
        var s = M3Play.Fresh();
        var plans = M3Play.Engine.LegalPlans(s, M3Play.C).Value.Where(p => p.Plan.Action == MainAction.NetworkCollaborate).ToList();
        Assert.Equal(5, plans.Count);
        var odds = M3Play.Engine.PresentAction(s, M3Play.C, plans[0].Plan).Value!;
        Assert.Equal(50, odds.Threshold); // 0.35 + 0.003 × 50 credibility − 0.003 × 0 drama
        Assert.Equal(["modifier.credibility", "modifier.drama"], odds.Modifiers.Select(m => m.LabelKey));
    }

    [Fact]
    public void Network_week_costs_energy_records_its_roll_and_reports_it()
    {
        var s = M3Play.Fresh();
        var started = M3Play.Start(s, MainAction.NetworkCollaborate, Category.JustChatting);
        Assert.Equal(s.Energy - Fixed.FromInt(10), started.Energy);
        Assert.NotNull(started.Pending!.ActionRoll);
        Assert.Equal(started.Pending.ActionRoll, M3Play.SettleAll(started).Report.ActionRoll);
    }

    [Fact]
    public void Network_needs_energy()
    {
        var s = M3Play.Fresh() with { Energy = Fixed.FromInt(9) };
        Assert.All(
            M3Play.Engine.LegalPlans(s, M3Play.C).Value.Where(p => p.Plan.Action == MainAction.NetworkCollaborate),
            p => Assert.Equal("not-enough-energy", p.DisabledReason));
    }

    [Fact]
    public void M1_rejects_network()
    {
        var m1 = TestWorld.Content;
        var s = CareerFactory.Create(M1Ruleset.RulesetId, m1.Digest, m1.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 3);
        var result = M3Play.Engine.StartWeek(s, m1, new WeekPlan(MainAction.NetworkCollaborate, Category.Fps), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "unsupported-action"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Every_week_has_exactly_one_main_action_even_with_decisions()
    {
        var s = M3Play.Fresh();
        for (var i = 0; i < 40 && s.Status == CareerStatus.Active; i++)
        {
            while (s.PendingDecisions is { Length: > 0 })
            {
                s = M3Play.ResolveQueued(s, M3Play.Engine.PresentDecision(s, M3Play.C).Value.Choices[0].ChoiceId);
                Assert.Null(s.Pending);
            }

            var action = i % 3 == 0 && s.Energy >= Fixed.FromInt(10) ? MainAction.NetworkCollaborate : MainAction.Rest;
            var started = M3Play.Start(s, action);
            Assert.Equal(action, started.Pending!.Plan.Action);
            var second = M3Play.Engine.StartWeek(started, M3Play.C, new WeekPlan(MainAction.Rest, Category.Fps), started.StateVersion);
            Assert.False(second.IsSuccess);
            s = M3Play.SettleAll(started).State;
        }
    }
}
```

- [ ] **Step 3: Run to see them fail**

Run: `dotnet test --filter "M3ActionTests|OddsCalculatorTests"`
Expected: compile errors / FAIL.

- [ ] **Step 4: Implement**

- `OddsCalculator.For`: after the content modifiers and before the easy bonus, when `s.Burnout > b.BurnoutPenaltyFrom`, add `new ModifierLine("modifier.burnout", -FixedMath.Min(b.BurnoutPenaltyCap, (s.Burnout - b.BurnoutPenaltyFrom) * b.BurnoutPenaltyPerPoint))`. At exactly 60 the penalty is zero, so no line is shown.
- `M1Ruleset.PlanRejection`: return `"unsupported-action"` for `NetworkCollaborate` before any other rule.
- `M3Ruleset.AllPlans`: the M1 list plus `new WeekPlan(MainAction.NetworkCollaborate, category)` per category.
- `M3Ruleset.PlanRejection`: network needs `b.NetworkCollaborate is not null` (`"unsupported-action"`) and `s.Energy >= b.NetworkEnergyCost` (`"not-enough-energy"`).
- `M3Ruleset.StartWeek` for network: build the pending week, subtract `NetworkEnergyCost`, compute odds, roll with `RuleSteps.Roll`, apply effects through `EffectApplier.ToWeek(state, "network", effects, ref pending)`, set `pending = pending with { ActionRoll = new RollResult("network", choice.Id, roll, odds.Threshold, odds.Modifiers, outcome) }`, count a critical in `Metrics.Criticals`. Event selection runs after the roll.
- `M3Ruleset.SettleWeek` passes `pending.ActionRoll` into `WeekReport.ActionRoll`.

- [ ] **Step 5: Run tests**

Run: `dotnet test`
Expected: PASS, golden unchanged.

- [ ] **Step 6: Commit**

```bash
git add source/backend
git commit -m "feat(domain): add Network/collaborate with visible odds and a burnout odds penalty

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 10: Recovery crisis replaces forced rest in M3

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3Ruleset.cs`, `source/backend/Domain/Simulation/Settlement/ProgressionRules.cs`, `BalanceConstants.cs`, `JsonContentSource.cs`, `source/backend/Tests/Support/M3TestContent.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3RecoveryTests.cs`, `source/backend/Tests/Domain/Simulation/InvariantProperties.cs` (add an M3 property)

**Interfaces:**
- Produces: `BalanceConstants.ShortStreamFatigue : Fixed` (default 1), `ShortStreamAudience : Fixed` (default 1), loader section `"shortStream": { "fatigue", "audience" }`; reserved flags `sys:rest-only` and `sys:short-stream`; `ProgressionRules.ApplyStreamFatigue(CareerState s, BalanceConstants b, Fixed factor)` overload (the existing overload calls it with `Fixed.One`).

- [ ] **Step 1: Write the failing tests**

Set `ShortStreamFatigue = 0.5` and `ShortStreamAudience = 0.7` in `M3TestContent`.

```csharp
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets.M3;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Rulesets;

public class M3RecoveryTests
{
    private static CareerState Exhausted() => M3Play.Fresh() with { Energy = Fixed.FromInt(5), Burnout = Fixed.FromInt(99) };

    private static CareerState InCrisis() => M3Play.SettleAll(M3Play.Start(Exhausted(), MainAction.NoAction)).State;

    [Fact]
    public void Zero_energy_after_settlement_opens_the_recovery_crisis()
        => Assert.Contains("burnout-recovery", InCrisis().PendingDecisions!.Value);

    [Fact]
    public void Week_off_leaves_only_rest_and_shortens_the_stream()
    {
        var s = InCrisis();
        while (s.PendingDecisions![0] != "burnout-recovery")
        {
            s = M3Play.ResolveQueued(s, "calm");
        }

        s = M3Play.ResolveQueued(s, "week-off");
        while (s.PendingDecisions is { Length: > 0 })
        {
            s = M3Play.ResolveQueued(s, "calm");
        }

        var legal = M3Play.Engine.LegalPlans(s, M3Play.C).Value;
        Assert.All(legal.Where(p => p.Plan.Action != MainAction.Rest), p => Assert.Equal("rest-only", p.DisabledReason));
        Assert.Contains(legal, p => p.Plan.Action == MainAction.Rest && p.DisabledReason is null);

        var (after, report) = M3Play.SettleAll(M3Play.Start(s, MainAction.Rest));
        Assert.Equal(-Fixed.FromInt(4), report.StatChanges.Single(l => l.Code == "stat:Energy" && l.Detail == "stream").Delta);
        Assert.False(Flags.IsActive(after, "sys:rest-only"));
    }

    [Fact]
    public void Push_through_failure_still_allows_any_plan()
    {
        var s = InCrisis();
        while (s.PendingDecisions![0] != "burnout-recovery")
        {
            s = M3Play.ResolveQueued(s, "calm");
        }

        s = M3Play.ResolveQueuedUntil(s, "push-through", RollOutcome.Failure);
        while (s.PendingDecisions is { Length: > 0 })
        {
            s = M3Play.ResolveQueued(s, "calm");
        }

        Assert.Contains(M3Play.Engine.LegalPlans(s, M3Play.C).Value, p => p.Plan.Action == MainAction.TrainCategory && p.DisabledReason is null);
    }

    [Fact]
    public void M3_never_applies_M1_forced_rest()
    {
        var s = M3Play.Fresh() with { Energy = Fixed.Zero };
        Assert.Contains(M3Play.Engine.LegalPlans(s, M3Play.C).Value, p => p.Plan.Action == MainAction.NoAction && p.DisabledReason is null);
    }
}
```

The test crisis and the week-2 milestone have a `calm` choice; the loops answer them so the recovery crisis is reached regardless of queue order.

Add an FsCheck property to `InvariantProperties.cs` mirroring the existing M1 property, on `M3Play.C` with `RandomLegalPolicy` answering decisions too: 200 weeks keep energy, burnout, drama and credibility within 0–100, `CareerInvariants.Validate` stays empty after every command, and every report reconciles (`Σ income − Σ costs == CashDelta`, and the cash change between settled states equals `CashDelta`).

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "M3RecoveryTests|InvariantProperties"`
Expected: FAIL.

- [ ] **Step 3: Implement**

- `M3Ruleset.PlanRejection`: no M1 `forced-rest` rule; add `Flags.IsActive(s, "sys:rest-only") && plan.Action != MainAction.Rest → "rest-only"`.
- `M3Ruleset.SettleWeek`: when `Flags.IsActive(s, "sys:short-stream")`, call `ApplyStreamFatigue(s, b, b.ShortStreamFatigue)` and multiply the pending week's `AudienceMultiplier` by `b.ShortStreamAudience` before `AudienceRules.Compute`.
- `DecisionQueue.Open` (Task 7) opens the recovery crisis whenever the condition holds, ignoring its cooldown.

- [ ] **Step 4: Run tests**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): open a recovery crisis at zero energy or full burnout in M3

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 11: Viral clips

**Files:**
- Create: `source/backend/Domain/Simulation/ViralResult.cs`
- Modify: `source/backend/Domain/Simulation/Settlement/AudienceRules.cs`, `WeekReport.cs`, `EventPresentation.cs`, `OddsCalculator.cs`, `BalanceConstants.cs`, `JsonContentSource.cs`, `source/backend/Domain/Rulesets/M3Ruleset.cs`, `source/backend/Tests/Support/M3TestContent.cs`
- Test: `source/backend/Tests/Domain/Simulation/ViralTests.cs`

**Interfaces:**
- Produces:
  - `record ViralResult(int Roll, int Threshold, Fixed Multiplier, long FollowersBonus)`; `WeekReport.Viral : ViralResult?` (trailing optional); `AudienceResult` gains trailing `ViralResult? Viral = null`.
  - `AudienceRules.Quality(CareerState s, PendingWeek p, BalanceConstants b) : Fixed` (extracted) and `AudienceRules.Compute(…, Fixed viralChance)`; the existing signature calls it with `Fixed.Zero`, which never draws.
  - Balance (loader section `"viral"`): `ViralQualityFrom`, `ViralPerQualityPoint`, `ViralPerCritical`, `ViralChanceCap`, `ViralMultiplierMin`, `ViralMultiplierMax`, `ViralFollowerShare`, `ViralViewerShare`, `ViralFollowerMin`; all zero in M1.
  - `ChoiceOdds` gains trailing `Fixed ViralOnCritical = default`.
  - `M3Ruleset.ViralChance(BalanceConstants b, Category category, Fixed quality, int criticals, Fixed fromEffects) : Fixed`.
  - A viral week sets flag `viral-clip` for 4 weeks.

- [ ] **Step 1: Extend the test content**

In `M3TestContent.Build`, give the balance the `content/m3` numbers from Task 12 Step 3 (categories, viral section) by building them in code with the same values, so this task does not depend on Task 12.

- [ ] **Step 2: Write the failing tests**

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Numerics;
using PolskiStreamerSimulator.Domain.Rulesets;
using PolskiStreamerSimulator.Domain.Simulation;
using PolskiStreamerSimulator.Domain.Simulation.Settlement;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Domain.Simulation;

public class ViralTests
{
    private static PendingWeek Week() => new(new CareerWeek(5), new WeekPlan(MainAction.NoAction, Category.Gambling), [], [], Fixed.Zero, Fixed.One, 0, []);

    private static AudienceResult FirstViral(CareerState s)
    {
        for (ulong seed = 1; seed <= 500; seed++)
        {
            var x = s with { Rng = Pcg32.Create(seed, CareerFactory.CareerStream) };
            var rng = x.Rng;
            var result = AudienceRules.Compute(x, Week(), ref rng, M3Play.C.Balance, Fixed.FromRatio(30, 100));
            if (result.Viral is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("no viral");
    }

    [Fact]
    public void Zero_chance_never_draws()
    {
        var s = TestCareers.Fresh() with { Followers = 5_000 };
        var withChance = s.Rng;
        var without = s.Rng;
        _ = AudienceRules.Compute(s, Week(), ref withChance, TestWorld.Content.Balance, Fixed.Zero);
        _ = AudienceRules.Compute(s, Week(), ref without, TestWorld.Content.Balance);
        Assert.Equal(without, withChance);
    }

    [Fact]
    public void Viral_multiplier_and_follower_bonus_are_bounded()
    {
        var b = M3Play.C.Balance;
        var viral = FirstViral(TestCareers.Fresh() with { Followers = 50_000 }).Viral!;
        Assert.InRange(viral.Multiplier, b.ViralMultiplierMin, b.ViralMultiplierMax);
        Assert.InRange(viral.FollowersBonus, b.ViralFollowerMin, 5_000);
    }

    [Fact]
    public void Small_career_gets_the_minimum_bonus()
        => Assert.Equal(100, FirstViral(TestCareers.Fresh() with { Followers = 0 }).Viral!.FollowersBonus);

    [Fact]
    public void Chance_combines_its_parts_and_is_capped()
    {
        var b = M3Play.C.Balance;
        Assert.Equal(b.ViralChanceCap, M3Ruleset.ViralChance(b, Category.Gambling, Fixed.Hundred, 5, Fixed.FromInt(30)));
        Assert.Equal(b.Profile(Category.Chess).ViralBase, M3Ruleset.ViralChance(b, Category.Chess, Fixed.FromInt(70), 0, Fixed.Zero));
        Assert.Equal(
            b.Profile(Category.Fps).ViralBase + (Fixed.FromInt(10) * b.ViralPerQualityPoint) + b.ViralPerCritical,
            M3Ruleset.ViralChance(b, Category.Fps, Fixed.FromInt(80), 1, Fixed.Zero));
    }

    [Fact]
    public void Report_keeps_followers_and_live_viewers_separate_in_a_viral_week()
    {
        var (state, report) = M3Play.UntilViral();
        Assert.Equal(report.FollowersGained - report.FollowersLost, report.FollowersNet);
        Assert.True(report.FollowersGained >= report.Viral!.FollowersBonus);
        Assert.True(PolskiStreamerSimulator.Domain.Rulesets.M3.Flags.IsActive(state, "viral-clip"));
    }

    [Fact]
    public void Odds_show_the_viral_chance_a_critical_can_add()
    {
        var critical = M3Play.C.Events.SelectMany(e => e.Choices).First(c => c.AllowsCritical);
        Assert.Equal(M3Play.C.Balance.ViralPerCritical, OddsCalculator.For(M3Play.Fresh(), critical, M3Play.C.Balance).ViralOnCritical);
    }
}
```

If no test-content choice allows criticals in a way `OddsCalculator` can present without a pending week, use `M3Play.C.Balance.NetworkCollaborate!` in the last test.

- [ ] **Step 3: Run to see them fail**

Run: `dotnet test --filter ViralTests`
Expected: FAIL.

- [ ] **Step 4: Implement**

```csharp
    /// <summary>
    /// Category base + per-point bonus above the quality threshold + bonus per critical outcome + event effects
    /// (percentage points), capped. Gameplay §11.
    /// </summary>
    public static Fixed ViralChance(BalanceConstants b, Category category, Fixed quality, int criticals, Fixed fromEffects)
    {
        var aboveThreshold = FixedMath.Max(Fixed.Zero, quality - b.ViralQualityFrom);
        var chance = b.Profile(category).ViralBase
            + (aboveThreshold * b.ViralPerQualityPoint)
            + (Fixed.FromInt(criticals) * b.ViralPerCritical)
            + (fromEffects / Fixed.Hundred);
        return FixedMath.Min(b.ViralChanceCap, chance);
    }
```

In `AudienceRules.Compute(…, Fixed viralChance)`: after the noise draw and before the existing `ViralCapMultiplier × baseline` cap, when `viralChance > Fixed.Zero`, draw `roll = NextInt(1, 100)` with `threshold = round(100 × viralChance)`; when `roll <= threshold`, draw `multiplier = NextFixed(ViralMultiplierMin, ViralMultiplierMax)`, multiply `uncapped`, keep the cap, and compute `bonus = max(ViralFollowerMin, round(min(ViralFollowerShare × followers, ViralViewerShare × live)))`, added to `gained`. Return `Viral = new ViralResult(roll, threshold, multiplier, bonus)`.

`M3Ruleset.SettleWeek`: `criticals` counts critical outcomes in `pending.Resolved` and `pending.ActionRoll`; `quality = AudienceRules.Quality(…)`; pass `ViralChance(b, primary, quality, criticals, pending.ViralChance ?? Fixed.Zero)`; copy `Viral` into the report; when viral, `Flags.Set(state, "viral-clip", 4)` and add `Metrics` nothing new (the simulator reads reports). `OddsCalculator.For` sets `ViralOnCritical = choice.AllowsCritical ? b.ViralPerCritical : Fixed.Zero`.

- [ ] **Step 5: Run tests**

Run: `dotnet test`
Expected: PASS, golden unchanged.

- [ ] **Step 6: Commit**

```bash
git add source/backend
git commit -m "feat(domain): add viral clips with published chances and bounded audience gains

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 12: `content/m3` balance and the representative Polish events

**Files:**
- Create: `content/m3/balance.json`, `content/m3/events.json`
- Modify: `source/backend/Tests/Support/TestPaths.cs` (`ContentM3`)
- Test: `source/backend/Tests/Content/M3ContentTests.cs`

**Interfaces:**
- Produces: a valid schema-2 snapshot with `"ruleset": "m3-systems.1"`; event IDs used later: `first-week` (milestone, week 2 only, one-time), `burnout-recovery` (crisis; choices `week-off`, `push-through`), `first-viral` (milestone, requires flag `viral-clip`, one-time), `followers-10k` (milestone, `min.Followers` 10,000, one-time), arc `rivalry-callout` → `rivalry-crisis` → `rivalry-rematch`, `collab-fallout` (crisis, network critical failure).

- [ ] **Step 1: Write the failing content tests**

```csharp
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Domain.Content;
using PolskiStreamerSimulator.Infrastructure.Content;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Content;

public class M3ContentTests
{
    private static readonly ContentSnapshot C = new JsonContentSource(TestPaths.ContentM3).Load();

    [Fact]
    public void Loads_valid_for_the_m3_ruleset() => Assert.Equal(("m3-systems.1", 2), (C.RulesetId, C.Schema));

    [Fact]
    public void Has_the_representative_set()
    {
        var live = C.Events.Where(e => e.Phase == EventPhase.Live).ToList();
        foreach (var category in Enum.GetValues<Category>())
        {
            Assert.Equal(5, live.Count(e => e.Eligibility.Categories is [var only] && only == category));
        }

        Assert.True(live.Count(e => e.Eligibility.Categories.IsDefaultOrEmpty) >= 8);
        Assert.True(C.Events.Count(e => e.Phase != EventPhase.Live) >= 4);
        Assert.InRange(C.Events.Length, 38, 45);
    }

    [Fact]
    public void Every_gendered_text_lists_three_forms()
    {
        var texts = C.Events
            .SelectMany(e => new[] { e.Title, e.TextPl }.Concat(e.Choices.SelectMany(ch => new[] { ch.LabelPl, ch.SuccessText, ch.FailureText })))
            .OfType<string>()
            .Where(t => t.Contains("{gender", StringComparison.Ordinal));
        Assert.All(texts, t =>
        {
            Assert.Contains("female {", t, StringComparison.Ordinal);
            Assert.Contains(" male {", t, StringComparison.Ordinal);
            Assert.Contains("other {", t, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Arc_and_triggered_events_are_wired()
    {
        var effects = C.Events.SelectMany(e => e.Choices).SelectMany(ch => ch.Outcomes.Success.Concat(ch.Outcomes.Failure)).ToList();
        Assert.Contains(effects, f => f.Kind == EffectKind.FollowUp && f.Target == "rivalry-crisis");
        Assert.Contains(C.Balance.NetworkCollaborate!.Outcomes.CriticalFailure!.Value, f => f.Target == "collab-fallout");
        Assert.Equal("burnout-recovery", C.Balance.RecoveryCrisisEventId);
        Assert.Contains(C.Events, e => e.Id == "first-week" && e.OneTime && e.Eligibility is { FromWeek: 2, ToWeek: 2 });
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter M3ContentTests`
Expected: FAIL (directory missing).

- [ ] **Step 3: Write `content/m3/balance.json`**

Copy `content/m1/balance.json`, set `"version": "m3.1"`, add `"ruleset": "m3-systems.1"`, set `"boredomDivisor": 400` and `"churnBoredomFactor": 0.00005`, and add:

```json
  "categories": {
    "JustChatting": { "reach": 1.00, "noiseMin": 0.85, "noiseMax": 1.15, "conversion": 1.15, "churn": 1.00, "boredomGainMin": 2, "boredomGainMax": 4, "subYield": 1.00, "donationYield": 1.30, "adYield": 1.00, "qualitySkillWeight": 1.0, "credibilityPerStream": 0, "dramaPerStream": 0, "viralBase": 0.02 },
    "Fps":          { "reach": 1.10, "noiseMin": 0.85, "noiseMax": 1.15, "conversion": 1.00, "churn": 1.00, "boredomGainMin": 2, "boredomGainMax": 4, "subYield": 1.00, "donationYield": 1.00, "adYield": 1.10, "qualitySkillWeight": 1.2, "credibilityPerStream": 0, "dramaPerStream": 0, "viralBase": 0.03 },
    "Moba":         { "reach": 0.95, "noiseMin": 0.85, "noiseMax": 1.15, "conversion": 1.00, "churn": 0.85, "boredomGainMin": 1, "boredomGainMax": 3, "subYield": 1.10, "donationYield": 1.00, "adYield": 1.00, "qualitySkillWeight": 1.0, "credibilityPerStream": 0, "dramaPerStream": 0, "viralBase": 0.02 },
    "Chess":        { "reach": 0.75, "noiseMin": 0.92, "noiseMax": 1.08, "conversion": 1.05, "churn": 0.70, "boredomGainMin": 1, "boredomGainMax": 3, "subYield": 1.30, "donationYield": 1.00, "adYield": 1.00, "qualitySkillWeight": 1.0, "credibilityPerStream": 0.2, "dramaPerStream": 0, "viralBase": 0.01 },
    "Gambling":     { "reach": 1.25, "noiseMin": 0.70, "noiseMax": 1.40, "conversion": 0.90, "churn": 1.20, "boredomGainMin": 2, "boredomGainMax": 4, "subYield": 1.00, "donationYield": 1.20, "adYield": 1.00, "qualitySkillWeight": 1.0, "credibilityPerStream": -0.3, "dramaPerStream": 1, "viralBase": 0.04 }
  },
  "mastery": { "fromSkill": 70, "boredomRelief": 1, "churnFactor": 0.85 },
  "burnoutPenalty": { "from": 60, "perPoint": 0.0025, "cap": 0.10 },
  "shortStream": { "fatigue": 0.5, "audience": 0.7 },
  "viral": { "qualityFrom": 70, "perQualityPoint": 0.002, "perCritical": 0.05, "chanceCap": 0.30, "multiplierMin": 1.5, "multiplierMax": 3.0, "followerShare": 0.10, "viewerShare": 0.20, "followerMin": 100 },
  "decisionChancePerWeek": 0.06,
  "recoveryCrisisEventId": "burnout-recovery",
  "networkEnergyCost": 10,
  "networkCollaborate": {
    "id": "collaborate",
    "label": "Zagadaj do innych twórców",
    "baseChance": 0.35,
    "allowsCritical": true,
    "modifiers": [
      { "stat": "Credibility", "perPoint": 0.003, "labelKey": "modifier.credibility" },
      { "stat": "Drama", "perPoint": -0.003, "labelKey": "modifier.drama" }
    ],
    "successText": "Wspólny stream siadł. Czat drugiej strony został u ciebie na dłużej.",
    "failureText": "{gender, select, female {Trafiłaś} male {Trafiłeś} other {Trafiłoś}} na kogoś, kto szuka afery. Klipy już krążą.",
    "outcomes": {
      "success": [ { "kind": "AudienceMultiplier", "amount": 1.2 }, { "kind": "StatDelta", "stat": "Credibility", "amount": 1 } ],
      "failure": [ { "kind": "StatDelta", "stat": "Drama", "amount": 4 }, { "kind": "StatDelta", "stat": "Credibility", "amount": -2 } ],
      "criticalSuccess": [ { "kind": "AudienceMultiplier", "amount": 1.2 }, { "kind": "SetFlag", "flag": "collab-friend", "weeks": 26, "amount": 0 } ],
      "criticalFailure": [ { "kind": "StatDelta", "stat": "Drama", "amount": 6 }, { "kind": "FollowUp", "event": "collab-fallout", "weeks": 1, "amount": 0 } ]
    }
  }
```

- [ ] **Step 4: Write `content/m3/events.json`**

`{ "schema": 2, "events": [ … ] }` with `"cooldownWeeks": 4` on repeatable live events (TD-113) and these 42 events. Each follows the content specification's quality rules: a discernible trade-off, at least one always-legal choice, disclosed odds, satire of Polish streaming culture, fictional names only, no real people, no protected-identity jokes, gambling only with fictional currency and without links or payment prompts. Past-tense text addressed to the player uses the gender select with the neutral `other` form. Decisions read only drama, credibility, energy, burnout or followers in modifiers (not primary-category stats).

| # | ID | Phase | Categories | Weeks | Premise |
| --- | --- | --- | --- | --- | --- |
| 1–2 | `jc-hot-take`, `jc-donate-tts` | live | JustChatting | 1–1040 | A hot take splits chat; a text-to-speech donation reads something awkward |
| 3 | `jc-first-regulars` | live | JustChatting | 1–260 | The first regulars want a name for the community |
| 4 | `jc-podcast-invite` | live | JustChatting | 261–780 | A small podcast asks for a live call-in |
| 5 | `jc-old-clip` | live | JustChatting | 781–1040 | Chat digs up a cringe clip from year one |
| 6–7 | `fps-clutch`, `fps-cheater-accusation` | live | Fps | 1–1040 | A 1v4 clutch chance; chat accuses an opponent (or you) of cheating |
| 8 | `fps-ranked-grind` | live | Fps | 1–260 | A demotion game on stream |
| 9 | `fps-tournament-qual` | live | Fps | 261–780 | A community cup qualifier |
| 10 | `fps-aim-decline` | live | Fps | 781–1040 | Reaction-time jokes from younger viewers |
| 11–12 | `moba-tilt`, `moba-backseat` | live | Moba | 1–1040 | A teammate tilts; chat backseats every move |
| 13 | `moba-new-patch` | live | Moba | 1–260 | Patch day, nobody knows the meta |
| 14 | `moba-duo-pro` | live | Moba | 261–780 | A semi-pro offers a duo queue |
| 15 | `moba-veteran-guide` | live | Moba | 781–1040 | Viewers ask for a beginner-guide marathon |
| 16–17 | `chess-blunder`, `chess-gambit` | live | Chess | 1–1040 | A visible blunder; a risky gambit from chat |
| 18 | `chess-puzzle-rush` | live | Chess | 1–260 | Puzzle rush with chat counting |
| 19 | `chess-simul` | live | Chess | 261–780 | A simul against viewers |
| 20 | `chess-old-rival` | live | Chess | 781–1040 | A former club rival challenges you |
| 21–22 | `gamba-big-win`, `gamba-promo-code` | live | Gambling | 1–1040 | A fictional-currency jackpot; chat asks for a fictional promo code |
| 23 | `gamba-first-slots` | live | Gambling | 1–260 | First fictional-slot session, chat warns about tilt |
| 24 | `gamba-mod-warning` | live | Gambling | 261–780 | A moderator warns that the platform watches gambling streams |
| 25 | `gamba-still-fun` | live | Gambling | 781–1040 | Viewers ask whether this is still fun |
| 26–33 | `x-raid`, `x-tech-failure`, `x-pizza-delivery`, `x-neighbour-noise`, `x-chat-meme`, `x-pet-cameo`, `x-copyright-music`, `x-sub-train` | live | all | 1–1040 | Cross-category situations |
| 34 | `rivalry-callout` | live | all | 27–1040 | Another streamer calls you out; the provocative choice sets flag `rivalry` (52 weeks) and schedules `rivalry-crisis` in 4 weeks |
| 35 | `rivalry-rematch` | live | all | 27–1040 | Requires flag `rivalry`; a public rematch; success clears `rivalry` |
| 36 | `first-week` | milestone | — | 2–2 | One-time: first week done, choose the channel's tone (credibility or drama) |
| 37 | `burnout-recovery` | crisis | — | 1–1040 | `week-off` (guaranteed; `sys:rest-only` and `sys:short-stream` for 1 week) or `push-through` (50 %: +10 energy; failure +10 burnout, −5 credibility) |
| 38 | `first-viral` | milestone | — | 1–1040 | One-time, requires flag `viral-clip`: how to use the moment |
| 39 | `followers-10k` | milestone | — | 1–1040 | One-time, `min.Followers` 10,000: a community celebration costs money or credibility |
| 40 | `rivalry-crisis` | crisis | — | 1–1040, weight 1 | Reached as a follow-up; responses trade drama, credibility and followers |
| 41 | `collab-fallout` | crisis | — | 1–1040, weight 1 | Network critical failure follow-up: apology or counter-attack |
| 42 | `community-burnout-poll` | crisis | — | 53–1040, `min.Burnout` 50 | Chat notices you are tired; repeatable |

`rivalry-crisis` and `collab-fallout` also become repeatable random crises through the decision roll; give them `"forbidsFlags": []` and a 26-week cooldown so they stay rare.

Shape of one complete event (use it for all):

```json
{
  "id": "chess-blunder",
  "version": 1,
  "phase": "live",
  "title": "Hetman na widelcu",
  "text": "{gender, select, female {Podstawiłaś} male {Podstawiłeś} other {Podstawiłoś}} hetmana w trzecim ruchu. Czat spamuje „??”, a ktoś już robi klipa.",
  "weight": 3,
  "cooldownWeeks": 4,
  "tags": [],
  "eligibility": { "fromWeek": 1, "toWeek": 1040, "categories": ["Chess"] },
  "choices": [
    {
      "id": "own-it",
      "label": "Przyznaj się i zrób z tego lekcję",
      "baseChance": 0.6,
      "modifiers": [ { "stat": "PrimarySkill", "perPoint": 0.002, "labelKey": "modifier.primary-skill" } ],
      "allowsCritical": true,
      "successText": "Widzowie doceniają szczerość. Analiza błędu ląduje w ulubionych.",
      "failureText": "Tłumaczenie brzmi jak wymówka i czat to wyczuwa.",
      "outcomes": {
        "success": [ { "kind": "StatDelta", "stat": "Credibility", "amount": 3 }, { "kind": "EventQuality", "amount": 4 } ],
        "failure": [ { "kind": "StatDelta", "stat": "Drama", "amount": 2 } ],
        "criticalSuccess": [ { "kind": "StatDelta", "stat": "Credibility", "amount": 4 }, { "kind": "ViralChance", "amount": 10 } ]
      }
    },
    {
      "id": "play-on",
      "label": "Graj dalej, jakby to był plan",
      "guaranteed": true,
      "successText": "Partia się sypie, ale stream leci dalej.",
      "outcomes": { "success": [ { "kind": "EventQuality", "amount": -2 } ] }
    }
  ]
}
```

Write the copy by hand; keep each `text` under 220 characters and each `label` under 70 so the 320-pixel layout holds.

- [ ] **Step 5: Run tests**

Run: `dotnet test`
Expected: PASS. Fix the content until `ContentValidator` and the tests pass; never relax validator rules for it.

- [ ] **Step 6: Commit**

```bash
git add content/m3 source/backend
git commit -m "feat(content): add the M3 balance and representative Polish events

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 13: Simulator support, M3 policies and the baseline report

**Files:**
- Modify: `source/backend/Tools/BatchSimulator/BatchRunner.cs`, `BatchOptions.cs`, `BatchStatistics.cs`, `ReportWriter.cs`, `source/backend/Domain/Simulation/Policies/BuiltInPolicies.cs`, `PolicyDecisions.cs`
- Create: `docs/reports/m3a-baseline-simulation.md` (generated plus findings)
- Test: `source/backend/Tests/BatchSimulator/*` (extend), `source/backend/Tests/Domain/Simulation/PolicyTests.cs` (extend)

**Interfaces:**
- Produces:
  - `BuiltInPolicies.Networker` (Balanced plus `NetworkCollaborate` every third week when energy ≥ 40 and no higher rule fires; reason `network-scheduled` with `everyWeeks`), `BuiltInPolicies.SpecialistOf(Category c)` (name `Specialist-{Category}`), `BuiltInPolicies.M3All` = `Balanced, Grinder, RestHeavy, Rotator, Networker` and the five specialists. `All` stays the M1 list.
  - A plan blocked by `rest-only` makes every policy choose `Rest` with reason `fallback-rest`.
  - Simulator result rows gain dominant category, final drama, final credibility, live-viewer coefficient of variation, leading income source, decisions answered and viral weeks.

- [ ] **Step 1: Write the failing tests**

In `PolicyTests.cs`:

```csharp
[Fact]
public void Networker_networks_every_third_free_week()
{
    var s = M3Play.Fresh() with { Week = new CareerWeek(3), Energy = Fixed.FromInt(80) };
    var decision = BuiltInPolicies.Networker.ChoosePlan(s, M3Play.C, M3Play.Engine.LegalPlans(s, M3Play.C).Value);
    Assert.Equal(MainAction.NetworkCollaborate, decision.Plan.Action);
    Assert.Equal("network-scheduled", decision.Reason.Code);
}

[Fact]
public void Specialist_streams_only_its_category_even_when_bored()
{
    var policy = BuiltInPolicies.SpecialistOf(Category.Chess);
    var s = M3Play.Fresh() with { Boredom = CategoryValues<Fixed>.Uniform(Fixed.FromInt(90)) };
    Assert.Equal(Category.Chess, policy.ChoosePlan(s, M3Play.C, M3Play.Engine.LegalPlans(s, M3Play.C).Value).Plan.Category);
}

[Fact]
public void M3_policies_finish_careers_without_invariant_violations()
{
    var c = new JsonContentSource(TestPaths.ContentM3).Load();
    foreach (var policy in BuiltInPolicies.M3All)
    {
        var start = CareerFactory.Create(c.RulesetId, c.Digest, c.Profiles[Background.ChessClubRegular], Background.ChessClubRegular, Difficulty.Normal, PronounSet.Nonbinary, 11);
        var run = CareerRunner.Run(M3Play.Engine, c, start, policy);
        Assert.Empty(run.InvariantViolations);
        Assert.Equal(CareerStatus.Ended, run.Final.Status);
    }
}
```

Match `decision.Reason.Code` to the actual member names in `PolicyDecisions.cs`.

Extend the batch-simulator tests: `--content <repo>/content/m3 --careers 20` uses `m3-systems.1` (from `content.RulesetId`) and `BuiltInPolicies.M3All`, and the report contains the headings `By category specialist`, `Decisions and virals` and `Criteria`.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "PolicyTests|BatchSimulator"`
Expected: FAIL.

- [ ] **Step 3: Implement**

- `BatchRunner`: `new WeekEngine(new RulesetRegistry([new M1Ruleset(), new M3Ruleset()]))`, ruleset ID from `content.RulesetId`, default policies `M3All` for M3 content and `All` for M1 unless `--policies` names them (`BatchOptions.Parse` must accept M3 names; pass the available list by ruleset after loading content).
- `RulePolicy`: the network rule; `CategoryMode.Fixed` with a category for specialists; `fallback-rest` on `rest-only`.
- Statistics: per career, final drama and credibility, the coefficient of variation of weekly live viewers (via `onWeek`), the income code with the largest total in `Metrics.IncomeBySource`, decisions answered (count `DecisionRolls` in reports) and viral weeks (`report.Viral is not null`).
- `ReportWriter`: for M3, the sections "By category specialist" (median followers, median cash, median drama, median credibility, viewer CV, leading income, completion rate), "Decisions and virals" (average decisions per 13 weeks for Balanced, virals per career) and "Criteria" evaluating spec §8 with PASS/FAIL.

- [ ] **Step 4: Generate and tune**

Run (from `source/backend`): `dotnet run --project Tools/BatchSimulator -c Release -- --careers 10000 --seed 42 --content ../../content/m3 --out ../../docs/reports/m3a-generated`

Write `docs/reports/m3a-baseline-simulation.md` in the shape of `m1-baseline-simulation.md`: date, summary, findings, criteria results, follow-ups, a "Tuning log", then the generated report. Delete the `m3a-generated` working directory after copying.

If a criterion fails, change only `content/m3/balance.json` category, mastery, boredom or decision-chance values, rerun, and log each change and its reason. Stop after five rounds; a criterion that still fails becomes an open follow-up for M5 ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)) and is stated in the PR description for the owner.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test`
Expected: PASS.

```bash
git add source/backend docs/reports content/m3
git commit -m "feat(simulator): simulate the M3 ruleset with category specialists and report the M3-A criteria

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 14: Document the M3 rules and open PR 2

**Files:**
- Modify: `docs/gameplay-systems.md` (new §11), `docs/content-and-experience.md` (§2, §5), `docs/requirements-traceability.md` (REQ-07, REQ-08, REQ-11, REQ-22), `docs/content-capacity-estimate.md` (§1), `docs/technical-decisions.md` (TD-113 if tuning changed it)

- [ ] **Step 1: Write gameplay §11 "M3-A ruleset (`m3-systems.1`)"**

Status "Agent proposal, tuned by the M3-A simulation". Subsections with the final numbers from `content/m3/balance.json`: category profiles; mastery and the boredom rebalance; burnout penalty; `Network/collaborate`; recovery crisis and short stream; mandatory decisions (queue order, `decisionChancePerWeek`, at most two per week, decision carry); viral clips (formula, caps, `viral-clip` flag); flags and follow-ups. Link the simulation report. Mark the M1 forced-rest bullet in §10 as "M1 only; M3 uses the recovery crisis (§11)".

- [ ] **Step 2: Update the content specification**

§2: schema-v2 fields (`version`, `phase`, ICU `title`/`text`/`label`/`successText`/`failureText`, `categories`, `requiresFlags`/`forbidsFlags`, `oneTime`, `requires`, the new effect kinds) and the immutability rule (TD-112). §5: replace "explicit copy variants or named grammatical forms" with the ICU `select` convention, the gender-neutral `other` form and the apostrophe note (TD-111). `content-capacity-estimate.md` §1: a template is one schema-v2 event; the pronoun-variant time is the time to write the ICU select branches.

- [ ] **Step 3: Update traceability**

REQ-08: add "M3-A ([#16](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/16): category profiles, mastery and boredom rebalance, measured in the [M3-A report](reports/m3a-baseline-simulation.md))". REQ-11 and REQ-22: add "M3-A ([#21](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/21): ICU gender select validated in content)". REQ-07: note that category reputation effects and network risk start in M3-A; balance stays in M5. Set those rows to `In progress (M3)`.

- [ ] **Step 4: Commit, push and open PR 2**

```bash
git add docs
git commit -m "docs: describe the M3-A ruleset, schema v2 and ICU convention

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/m3a-2-rules
gh pr create --base develop --title "M3-A PR 2: M3 rules and representative content" --body "$(cat <<'EOF'
Implements the M3-A rules for #16 and #17 and the representative content for #21: category profiles, mastery, virals, the burnout penalty, Network/collaborate, the recovery crisis, decision opening, ~40 Polish events and the M3-A simulation report.

Criteria results: docs/reports/m3a-baseline-simulation.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI, check SonarQube Cloud (zero open issues), ask the owner to merge.

---

# PR 3 — API and play

Start from the latest `develop` after PR 2 merged: `git switch -c feature/m3a-3-play origin/develop`.

### Task 15: API contract, commands and new careers on `m3-systems.1`

**Files:**
- Modify: `source/backend/Application/ApplicationServices.cs`, `Careers/Contracts.cs`, `Careers/CareerViewBuilder.cs`, `Careers/ReportViews.cs`, `Careers/SubmitAction.cs`, `Careers/CreateCareer.cs`, `Careers/Persistence/CareerRecords.cs`, `Careers/DomainErrors.cs`
- Modify: `source/backend/WebApp/Careers/Requests.cs`, `source/backend/WebApp/appsettings.json`, `source/backend/WebApp/WebApp.csproj`, `source/backend/WebApp/Dockerfile` (comment), `source/backend/WebApp/openapi/v1.json` (regenerated), `source/backend/Infrastructure/Content/ContentOptions.cs`
- Test: `source/backend/Tests/Application/Careers/DecisionFlowTests.cs`, `source/backend/Tests/WebApp/*` (extend the HTTP journey), `source/backend/Tests/Infrastructure/Persistence/*` (M1 and M3 careers coexist)

**Interfaces:**
- Consumes: `WeekEngine.PresentDecision/ResolveDecision/PresentAction`, `ContentSnapshot.RulesetId`, `WeekReport.DecisionRolls/ActionRoll/Viral`.
- Produces (API contract, consumed by Task 16):
  - `NextStepKind.ResolveDecision` (appended), with `NextStepView.Event` filled from `PresentDecision`.
  - `EventView(string EventId, string? TitleKey, string? Title, string Text, IReadOnlyList<ChoiceView> Choices, bool Mandatory)` — `Text` replaces `TextPl`; M1 events give `TitleKey` and plain `Text`, M3 events give ICU `Title` and `Text`.
  - `ChoiceView(string ChoiceId, string? LabelKey, string Label, bool Guaranteed, decimal Probability, int Threshold, IReadOnlyList<ModifierView> Modifiers, bool AllowsCritical, decimal ViralOnCritical, string? UnavailableReason)` — `Label` replaces `LabelPl`; `UnavailableReason` is `"needs-cash"` or `"needs-energy"` when `Requires` is not met (and `ResolveEvent`/`ResolveDecision` reject such a choice with `IllegalAction`/`choice-unavailable`).
  - `PlanOptionView(PlanView Plan, string? DisabledReason, ChoiceView? Odds)` — `Odds` for a network plan.
  - `ActionType.ResolveDecision`; `ActionKind.ResolveDecision` (log `kind` value `ResolveDecision`, 15 characters, fits the 16-character column).
  - `WeekReportView` gains `IReadOnlyList<RollView> DecisionRolls`, `RollView? ActionRoll`, `ViralView? Viral` with `ViralView(int Roll, int Threshold, decimal Multiplier, long FollowersBonus)`.
  - `CareerView` gains `string RulesetId`.
  - Domain error `DecisionPending` → HTTP 409 `decision-pending`; `NoPendingDecision` → 409 `no-pending-decision` (follow the existing mapping in `DomainErrors.cs`).

- [ ] **Step 1: Write the failing application tests**

`DecisionFlowTests.cs` (use the existing `AppTestKit` with `InMemoryCareerStore`/`InMemoryContentStore` and the repository `content/m3`; follow how the existing application tests create a career and submit actions):

```csharp
[Fact]
public async Task New_career_uses_the_ruleset_of_the_current_content()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var created = await kit.CreateCareerAsync();
    Assert.Equal("m3-systems.1", created.Career.RulesetId);
}

[Fact]
public async Task After_week_one_the_next_step_is_the_mandatory_milestone()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var career = await kit.PlayManualWeekAsync(await kit.CreateCareerAsync());
    Assert.Equal(NextStepKind.ResolveDecision, career.Next.Kind);
    Assert.Equal("first-week", career.Next.Event!.EventId);
    Assert.True(career.Next.Event.Mandatory);
    Assert.Contains("{gender, select,", career.Next.Event.Text + career.Next.Event.Title + string.Concat(career.Next.Event.Choices.Select(c => c.Label)), StringComparison.Ordinal);
}

[Fact]
public async Task Repeated_resolve_decision_returns_the_stored_outcome()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var career = await kit.PlayManualWeekAsync(await kit.CreateCareerAsync());
    var input = new ActionInput(career.Version, ActionType.ResolveDecision, null, null, null, null, career.Next.Event!.Choices[0].ChoiceId);
    var commandId = Guid.NewGuid();
    var first = await kit.SubmitAsync(career.CareerId, commandId, input);
    var second = await kit.SubmitAsync(career.CareerId, commandId, input);
    Assert.Equal(ContractJson.Serialize(first.Value), ContractJson.Serialize(second.Value));
    Assert.NotNull(first.Value.Roll);
    Assert.Equal(career.Version + 1, (await kit.GetAsync(career.CareerId)).Version);
}

[Fact]
public async Task Start_week_while_a_decision_waits_is_a_conflict()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var career = await kit.PlayManualWeekAsync(await kit.CreateCareerAsync());
    var result = await kit.SubmitAsync(career.CareerId, Guid.NewGuid(), new ActionInput(career.Version, ActionType.StartWeek, MainAction.Rest, Category.Fps, null, null, null));
    Assert.Equal("decision-pending", result.Error.Detail);
}

[Fact]
public async Task Batch_stops_before_a_queued_decision_and_resolving_it_allows_planning()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var career = await kit.CreateCareerAsync();
    var batch = await kit.RunBatchAsync(career.CareerId, career.Career.Version);
    Assert.Equal(BatchStopReason.PausedForDecision, batch.Value.StopReason);
    Assert.Equal(1, batch.Value.WeeksCommitted);
    var view = batch.Value.Career;
    Assert.Equal(NextStepKind.ResolveDecision, view.Next.Kind);

    var resolved = await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), new ActionInput(view.Version, ActionType.ResolveDecision, null, null, null, null, view.Next.Event!.Choices[0].ChoiceId));
    Assert.Equal(NextStepKind.PlanWeek, resolved.Value.Career.Next.Kind);
}

[Fact]
public async Task Network_plan_carries_its_odds()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var view = (await kit.CreateCareerAsync()).Career;
    var network = view.Next.Plans.First(p => p.Plan.MainAction == MainAction.NetworkCollaborate);
    Assert.NotNull(network.Odds);
    Assert.All(view.Next.Plans.Where(p => p.Plan.MainAction != MainAction.NetworkCollaborate), p => Assert.Null(p.Odds));
}

[Fact]
public async Task M1_careers_keep_playing_after_the_switch()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM1);
    var m1 = await kit.CreateCareerAsync();
    kit.SwitchCurrentContent(TestPaths.ContentM3);
    var played = await kit.PlayManualWeekAsync(m1);
    Assert.Equal("m1-prototype.1", played.RulesetId);
    Assert.Equal(NextStepKind.PlanWeek, played.Next.Kind);
    Assert.DoesNotContain(played.Next.Plans, p => p.Plan.MainAction == MainAction.NetworkCollaborate);
}
```

Add the helpers these tests need to `AppTestKit` (`WithContent(string directory)`, `SwitchCurrentContent`, `CreateCareerAsync`, `PlayManualWeekAsync` that resolves every event with its first available choice and settles, `SubmitAsync`, `RunBatchAsync`, `GetAsync`) if they do not exist under other names; reuse existing names where they do.

In the WebApp HTTP tests, extend the existing journey with `type: "resolveDecision"` and assert a `409` with `decision-pending` for an early `startWeek`. In the Testcontainers persistence tests, store an M1 and an M3 snapshot and play one week of each.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "DecisionFlowTests"`
Expected: compile errors.

- [ ] **Step 3: Implement**

- `ApplicationServices`: `new RulesetRegistry([new M1Ruleset(), new M3Ruleset()])`.
- `CreateCareer`: `CareerFactory.Create(snapshot.RulesetId, …)`.
- `ContentOptions.Directory` default and `appsettings.json` `Content:Directory` → `content/m3`; `WebApp.csproj` adds `<None Include="..\..\..\content\m3\*.json" LinkBase="content\m3" CopyToOutputDirectory="PreserveNewest" />` next to the M1 line (M1 stays: stored M1 careers load their snapshot from the database, but tests and tools still read `content/m1`). Update the Dockerfile comment to name both directories.
- `CareerViewBuilder.Next`: Ended → Ended; `Pending is null` and `engine.RequiresPlayerDecision` → `ResolveDecision` with `Event(engine.PresentDecision(…), c, mandatory: true, s)`; `Pending is null` → `PlanWeek` with `Odds = engine.PresentAction(s, c, plan) is { } odds ? Choice(odds, b.NetworkCollaborate!, s) : null`; otherwise as before. `Event(...)` maps `Title`, `TitleKey`, `Text = definition.TextPl`; `Choice(...)` maps `Label = choice.LabelPl`, `ViralOnCritical`, `UnavailableReason` from `choice.Requires` against the state.
- `SubmitAction.Execute`: `ActionType.ResolveDecision` needs `ChoiceId`; calls `engine.ResolveDecision`; the roll view is `ReportViews.Roll(next.Carry!.Rolls[^1])`. `KindOf` maps it to `ActionKind.ResolveDecision`. `Defined(...)` already covers enum values.
- `ReportViews.Report`: map `DecisionRolls` (empty list when default), `ActionRoll`, `Viral`.
- `ResolveEvent`/`ResolveDecision` in `M3Ruleset` reject a choice whose `Requires` is not met (`IllegalAction`, `choice-unavailable`) — add a Domain test to `M3DecisionTests` for it.
- `Requests.cs`: `When(r => r.Type is ActionType.ResolveEvent or ActionType.ResolveDecision, () => RuleFor(r => r.ChoiceId).NotEmpty().MaximumLength(64));`.
- `DomainErrors.cs`: map the two new codes to 409 problems with their details.
- Regenerate the OpenAPI document: from `source/backend`, `dotnet build -c Release && dotnet run --project WebApp -c Release --no-build -- openapi --document v1 --output openapi/v1.json`.

- [ ] **Step 4: Run tests**

Run: `dotnet test`
Expected: PASS (Docker running for Testcontainers).

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(api): serve M3 careers with mandatory decisions, network odds and ICU event text

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 16: Frontend — ICU text, decision step, network odds and new report lines

**Files:**
- Create: `source/frontend/src/content/icu.ts`, `source/frontend/src/content/icu.test.ts`
- Modify: `source/frontend/package.json` (add `intl-messageformat`), `src/api/generated/*` (regenerated), `src/features/career/stage/LiveEvent.tsx`, `LiveEvent.test.tsx`, `Stage.tsx`, `PlanWeek.tsx`, `PlanWeek.test.tsx`, `WeekReport.tsx`, `WeekReport.test.tsx`, `RollResult.tsx`, `src/features/career/plans.ts`, `plans.test.ts`, `reportLabels.ts`, `src/locales/pl/translation.json`, `src/features/career/batch/BatchResult.tsx` (new reason codes)

**Interfaces:**
- Consumes: Task 15 contract (`NextStepKind.resolveDecision`, `EventView.title/titleKey/text/mandatory`, `ChoiceView.label/viralOnCritical/unavailableReason`, `PlanOptionView.odds`, `ActionType.resolveDecision`, `WeekReportView.decisionRolls/actionRoll/viral`).
- Produces: `formatContent(text: string, pronouns: PronounSet): string`.

- [ ] **Step 1: Add the dependency and regenerate the client**

Run (from `source/frontend`): `npm install --save-exact intl-messageformat` then `npm run api:generate`.

- [ ] **Step 2: Write the failing tests**

`src/content/icu.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { PronounSet } from '@/api/generated/game.schemas';

import { formatContent } from './icu';

const text = '{gender, select, female {Zrobiłaś to.} male {Zrobiłeś to.} other {Zrobiłoś to.}}';

describe('formatContent', () => {
  it.each([
    [PronounSet.woman, 'Zrobiłaś to.'],
    [PronounSet.man, 'Zrobiłeś to.'],
    [PronounSet.nonbinary, 'Zrobiłoś to.'],
  ])('picks the %s form', (pronouns, expected) => {
    expect(formatContent(text, pronouns)).toBe(expected);
  });

  it('keeps plain text and Polish quotes', () => {
    expect(formatContent('Czat pisze „F”.', PronounSet.woman)).toBe('Czat pisze „F”.');
  });

  it('reads a doubled apostrophe as one', () => {
    expect(formatContent("Rock''n''roll", PronounSet.man)).toBe("Rock'n'roll");
  });

  it('falls back to the raw text when a message is malformed', () => {
    expect(formatContent('Otwarte { bez końca', PronounSet.man)).toBe('Otwarte { bez końca');
  });
});
```

`LiveEvent.test.tsx` additions (use the existing render helpers and fixtures; extend the fixture with `title`, `text`, `label`): an M3 event renders `title` and `text` formatted for the career's pronouns; a choice with `unavailableReason: 'needs-cash'` is a disabled radio with the translated reason; a choice with `viralOnCritical: 0.05` shows "kryt. sukces: +5 pp szansy na viral"; in decision mode (`career.next.kind === 'resolveDecision'`) the heading shows the "Decyzja" badge instead of "LIVE" and submitting sends `type: 'resolveDecision'`.

`PlanWeek.test.tsx` additions: the action list renders only actions that appear in `career.next.plans` (an M1 career has no network radio); choosing the network action shows its chance and modifiers before the start button; a `rest-only` block shows `disabled.rest-only`.

`WeekReport.test.tsx` additions: decision rolls and the action roll appear with roll and threshold; a viral week shows the viral line with the follower bonus while live viewers and followers stay in separate rows; a `followers:event` stat line shows a percentage.

- [ ] **Step 3: Run to see them fail**

Run: `npm test`
Expected: FAIL (`./icu` missing, contract fields renamed).

- [ ] **Step 4: Implement**

`src/content/icu.ts`:

```ts
import { IntlMessageFormat } from 'intl-messageformat';

import { PronounSet } from '@/api/generated/game.schemas';

const gender: Record<PronounSet, 'female' | 'male' | 'other'> = {
  [PronounSet.woman]: 'female',
  [PronounSet.man]: 'male',
  [PronounSet.nonbinary]: 'other',
};

/**
 * Formats event text from the career's content snapshot (ICU MessageFormat, TD-111) with the career's pronouns.
 * The server validated the message; a malformed one is shown raw rather than breaking the screen.
 */
export function formatContent(text: string, pronouns: PronounSet): string {
  try {
    return String(new IntlMessageFormat(text, 'pl-PL', undefined, { ignoreTag: true }).format({ gender: gender[pronouns] }));
  } catch {
    return text;
  }
}
```

- `LiveEvent.tsx`: heading `event.title === null ? t(event.titleKey ?? 'event.untitled', { defaultValue: t('event.untitled') }) : formatContent(event.title, career.pronouns)`; body `formatContent(event.text, career.pronouns)`; labels `formatContent(choice.label, career.pronouns)`; disabled radio and reason for `unavailableReason`; the viral hint; badge and action type by `career.next.kind` (`resolveDecision` → badge `event.decision`, type `ActionType.resolveDecision`). Keep the component name; rename the prop doc to "one live situation or mandatory decision".
- `Stage.tsx`: render `LiveEvent` for both `NextStepKind.resolveEvent` and `NextStepKind.resolveDecision`.
- `plans.ts`: add `offeredActions(plans): MainAction[]` (unique actions in server order); `PlanWeek` iterates it instead of `Object.values(MainAction)`; when the chosen option has `odds`, render the same chance/modifier block as `LiveEvent` (extract it into `src/features/career/stage/ChoiceOdds.tsx` and use it in both).
- `WeekReport.tsx` and `RollResult.tsx`: decision rolls and the action roll with roll/threshold; the viral line; `followers:event`.
- `translation.json` (Polish): `action.networkCollaborate` "Networking i kolaboracje", `event.decision` "Decyzja", `event.viralHint` "kryt. sukces: +{{value}} pp szansy na viral", `disabled.decision-pending` "Najpierw podejmij decyzję", `disabled.rest-only` "Ten tydzień to odpoczynek", `disabled.not-enough-energy` "Za mało energii", `disabled.unsupported-action` "Niedostępne w tej karierze", `choice.needs-cash` "Brakuje pieniędzy", `choice.needs-energy` "Brakuje energii", `modifier.burnout` "Wypalenie", `modifier.drama` "Drama", `report.viral` "Klip poszedł viralem: widzowie ×{{multiplier}}, +{{followers}} obserwujących (rzut {{roll}} ≤ {{threshold}})", `report.decisions` "Decyzje przed tygodniem", `report.actionRoll` "Networking", `report.followersEvent` "Obserwujący (wydarzenia): {{value}}%", `reason.network-scheduled` "Networking co {{everyWeeks}} tydzień", and stat detail labels for `network` and the new event IDs fall back to the generic "wydarzenie". Have the owner review the Polish copy in the PR.

- [ ] **Step 5: Run checks**

Run: `npm run typecheck && npm run lint && npm run format:check && npm run test:coverage`
Expected: PASS, line coverage ≥ 80 %.

- [ ] **Step 6: Commit**

```bash
git add source/frontend
git commit -m "feat(frontend): render ICU event text, mandatory decisions, network odds and viral reports

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 17: Cross-check every `content/m3` message with `intl-messageformat`

**Files:**
- Create: `source/frontend/src/content/m3Content.test.ts`

- [ ] **Step 1: Write the test**

```ts
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

import { PronounSet } from '@/api/generated/game.schemas';

import { formatContent } from './icu';

interface Choice { label: string; successText?: string; failureText?: string }
interface ContentEvent { id: string; title: string; text: string; choices: Choice[] }

const root = resolve(__dirname, '../../../../content/m3');
const events = (JSON.parse(readFileSync(resolve(root, 'events.json'), 'utf8')) as { events: ContentEvent[] }).events;
const network = (JSON.parse(readFileSync(resolve(root, 'balance.json'), 'utf8')) as { networkCollaborate: Choice }).networkCollaborate;

const messages: [string, string][] = [
  ...events.flatMap((e) => [
    [`${e.id}.title`, e.title] as [string, string],
    [`${e.id}.text`, e.text] as [string, string],
    ...e.choices.flatMap((c) =>
      [c.label, c.successText, c.failureText].filter((m): m is string => m !== undefined).map((m) => [`${e.id}.choice`, m] as [string, string]),
    ),
  ]),
  ...[network.label, network.successText, network.failureText].filter((m): m is string => m !== undefined).map((m) => ['network', m] as [string, string]),
];

describe('content/m3 messages', () => {
  it.each(messages)('%s formats for every pronoun set', (_, message) => {
    const forms = Object.values(PronounSet).map((p) => formatContent(message, p));
    forms.forEach((form) => {
      expect(form).not.toContain('{');
      expect(form.trim()).not.toBe('');
    });
    if (message.includes('{gender, select,')) {
      expect(new Set(forms).size).toBe(3);
    }
  });
});
```

`formatContent` returns the raw text on a parse error, so `not.toContain('{')` catches malformed messages too.

- [ ] **Step 2: Run it**

Run: `npm test -- m3Content`
Expected: PASS. A failure means the content and the server validator disagree with `intl-messageformat`: fix the content and add the failing case to `IcuMessageTests` so the server rejects it too.

- [ ] **Step 3: Commit**

```bash
git add source/frontend/src/content
git commit -m "test(frontend): format every M3 event message with intl-messageformat for all pronouns

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 18: Playwright journey through the week-2 milestone

**Files:**
- Modify: `source/frontend/e2e/journey.spec.ts`

- [ ] **Step 1: Extend the journey**

After the week-1 report (`pl.report.next`), expect the decision step and answer it, then continue with the batch as before:

```ts
  await expect(page.getByText(pl.event.decision)).toBeVisible();
  await expectAccessible(page, 'mandatory decision');
  await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
  await page.getByRole('button', { name: pl.event.choose }).click();
  await page.getByRole('button', { name: pl.roll.next }).click();
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
```

The batch may stop on another decision; the existing check already accepts any legitimate stop message — add `pl.batch.stop.pausedForDecision` (use the actual key) to `stops` if it is not in the list. The final reload assertion compares the desk text before and after, which still holds.

- [ ] **Step 2: Run the stack and the journey**

Run (from the repository root): `docker compose up -d --build`, then from `source/frontend`: `npm run e2e`.
Expected: PASS in every theme with no axe violations. Stop the stack with `docker compose down`.

- [ ] **Step 3: Commit**

```bash
git add source/frontend/e2e
git commit -m "test: play the week-two milestone in the browser journey

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 19: Close out documentation, issues and PR 3

**Files:**
- Modify: `docs/requirements-traceability.md`, `docs/technology-stack.md` (frontend libraries: `intl-messageformat`), `docs/technical-architecture.md` (career API: `resolveDecision`, decision step, event text from the snapshot), `docs/superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md` (status: delivered parts), `docs/game-design-document.md` (only if a product choice changed during review)

- [ ] **Step 1: Update documents**

- Traceability: REQ-04 adds "M3-A: real mandatory decisions pause the batch (`resolveDecision`)"; REQ-08, REQ-11, REQ-22 keep `In progress (M3)` with the PR links; REQ-10 notes the gambling events use fictional currency only.
- Technology stack: add `intl-messageformat` (exact version from `package.json`) to the frontend library table with its purpose (TD-111).
- Technical architecture: the next step list gains "mandatory decision"; the action list gains `resolveDecision`; event text comes from the career's pinned snapshot as ICU strings.
- Add a technical decision only for choices made during implementation that are not yet recorded (for example `UnavailableReason` codes or the `ChoiceOdds.tsx` extraction is not a decision; a changed number is).

- [ ] **Step 2: Update GitHub issues**

Comment on #16, #17 and #21 with what M3-A delivered and the evidence (tests, report), and on #7 with the schema and the representative events the owner can time (link `content/m3/events.json` and `docs/templates/content-authoring-log.csv`). Do not close issues; they close with the M3 release into `main`.

```bash
gh issue comment 21 --body "M3-A delivered schema v2, the validator (no legal option, missing text and grammatical forms, broken follow-ups and flags), ICU MessageFormat text and immutable event versions. Evidence: ContentValidatorV2Tests, IcuMessageTests, EventVersionsTests, EfContentStoreTests. Closes with the M3 release."
```

(Write the #16, #17 and #7 comments the same way with their own evidence.)

- [ ] **Step 3: Commit, push and open PR 3**

```bash
git add docs
git commit -m "docs: record the M3-A API, decision step and ICU rendering

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/m3a-3-play
gh pr create --base develop --title "M3-A PR 3: play M3 careers in the browser" --body "$(cat <<'EOF'
New careers start on m3-systems.1: mandatory decisions with the resolveDecision step, Network/collaborate odds on the plan screen, ICU event text in all three grammatical forms, decision and viral lines in reports. M1 careers keep playing. Part of #16, #17 and #21.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI (including `frontend-e2e`), check SonarQube Cloud (zero open issues on the PR), ask the owner to merge, then check `&branch=develop`.

---

## Spec coverage

| Spec section | Tasks |
| --- | --- |
| §2 decomposition, owner decisions | recorded before the plan (TD-109–TD-113); Task 0 |
| §4.1 schema v2 | 5 |
| §4.2 grammatical forms | 4, 16, 17 |
| §4.3 validation | 4, 5 |
| §4.4 immutability | 6 |
| §4.5 representative content | 12 |
| §5.1 actions, Network | 9 |
| §5.2 energy, burnout, recovery crisis | 9, 10 |
| §6 mandatory decisions | 7, 15, 16, 18 |
| §7.1 category profiles | 8, 12 |
| §7.2 mastery, boredom rebalance | 8, 12, 13 |
| §7.3 viral clips | 11, 16 |
| §8 simulation criteria | 13 |
| §9.1 code structure, golden test | 1, 2, 3, 7 |
| §9.2 testing | every task; 10 (properties), 17 (cross-check), 18 (journey) |
| §9.3 delivery | PR steps in 7, 14, 19 |
| §10 documentation | 0, 14, 19 |
