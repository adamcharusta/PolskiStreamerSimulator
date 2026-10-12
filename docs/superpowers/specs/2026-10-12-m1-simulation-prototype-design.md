# M1 Simulation Prototype — Design

Date: 2026-10-12
Status: **Approved design (owner, 2026-10-12 brainstorming session)** — implementation plan pending.
Milestone: [M1 — Simulation prototype](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/1), issues #1–#7.

## 1. Intent

Prove that a deterministic, dependency-free C# `Domain` core can play a valid 1,040-week career, with tests for state invariants, a report of extreme paths, and measured full-career performance. M1 has no UI, HTTP API or database. Exit gate (milestone description): a valid full run and documented edge cases.

### Owner decisions taken in this session

| Topic | Decision |
| --- | --- |
| Mechanics scope | **Minimal core.** Five categories exist as data; actions Rest, Train category, Improve production, Manage community, No action; one platform (Czatnia); four backgrounds; two or three synthetic live events per manual week; 45/45/10 score; endings Completed (week 1,040), Insolvency, Early retirement. Sponsors, gambling, adult side business, Network/collaborate, platform migration and bans are deferred to milestone 3 ("System complete"). |
| Content and constants | Versioned JSON in the repository, validated and loaded into an immutable `ContentSnapshot` with a SHA-256 digest. `Domain` receives the snapshot and does not know its source. |
| Arithmetic | Fixed-point `Fixed` (64-bit, scale 10^6), own integer square root and `log10`, own PCG32 generator. No `double`, `float`, `decimal` or `System.Random` in simulation code. |
| Git | Create `develop` from `main`; one `feature/*` branch per issue. Branches are stacked in dependency order because pull requests are not merged yet. The agent commits locally; the owner pushes and opens PRs. |
| Batch runner | Separate console project `source/backend/Tools/BatchSimulator`. |
| Build environment | The owner unblocks .NET SDK and NuGet hosts so the agent can run `dotnet build/test` itself (TDD). |

### Agent defaults (to record as "Agent proposal" in the decision register)

- No Wolverine, EF Core, Mapster, FluentValidation or Identity packages in M1: they arrive with the vertical slice (M2). M1 creates the four layer projects with their dependency direction but leaves Application/Infrastructure/WebApp minimal.
- Lefthook pre-commit hooks and GitHub Actions CI are out of M1 scope (not in any M1 issue); they get their own task.
- Issue #7 delivers a method, a measurement template and a proposed launch floor. Its acceptance criteria require owner measurement and approval, so the issue stays open after the agent's part.
- Missing numeric constants (for example `paidConversion`, `donationYield`, equipment tier prices) are added to `balance.json` and to `gameplay-systems.md` as agent proposals.

## 2. Source layout and boundaries (#2)

```text
source/backend/
  PolskiStreamerSimulator.slnx
  Domain/                 pure simulation, no NuGet dependencies
    Numerics/             Fixed, FixedMath (Sqrt, Log10, Clamp, Round), Pcg32
    Careers/              CareerState, Character, Background, Difficulty, PronounSet,
                          CareerWeek, Stats, EndingReason, CareerInvariants, CareerFactory
    Content/              ContentSnapshot, BalanceConstants, EventDefinition, ChoiceDefinition,
                          Effect, ContentValidator
    Rulesets/             IRuleset, RulesetId, RulesetRegistry, M1Ruleset
    Simulation/           WeekEngine, WeekPlan, PendingWeek, EventPresentation, RollResult,
                          WeekReport, ReportLine, IChoicePolicy, DomainError
    Scoring/              ScoreCalculator, ScoreBreakdown
    Serialization/        CanonicalJson, StateHash
  Application/            IContentSource port only
  Infrastructure/         JsonContentSource (content/*.json -> ContentSnapshot)
  WebApp/                 minimal ASP.NET Core host with /health; no game endpoints
  Tests/                  xUnit: unit, property (FsCheck), golden fixtures, architecture
  Tools/BatchSimulator/   console runner and report writer
content/m1/               balance.json, events.json
```

- Dependency direction: Application → Domain; Infrastructure → Application, Domain; WebApp → all; Tests → all; BatchSimulator → Domain, Infrastructure.
- An architecture test fails if `Domain` references any assembly outside the BCL (in particular EF Core, ASP.NET Core, Wolverine) or uses `System.Random`, `DateTime.Now`, `float`, `double` or `decimal` in its public or internal API surface.
- Unit tests run without SQL Server (there is no database in M1).
- Packages are pinned centrally in `Directory.Packages.props`.

## 3. Numerics

- `Fixed`: `readonly struct` wrapping `long` raw value with scale 1,000,000. Operations: `+ - * /` with explicit rounding (round half away from zero), comparison, `Clamp`, `Round` to `long`, `FromInt`, `FromRatio(num, den)`. Overflow is checked.
- `FixedMath.Sqrt`: integer Newton iteration on the raw value; exact floor result at scale 10^6.
- `FixedMath.Log10`: computed from integer `log2` (bit length) plus a fixed-point polynomial/table refinement; accuracy target ±1e-5 on [1, 10^8]. Golden values are tested.
- `Pcg32` (PCG-XSH-RR 64/32): `NextUInt()`, `NextInt(minInclusive, maxInclusive)` without modulo bias (rejection sampling), `NextFixed(min, max)`. Its full state (`state`, `inc`) is part of `CareerState`.

## 4. Career state, rulesets and invariants (#3)

`CareerState` is an immutable `record`:

- Identity and versions: `RulesetId` (for example `m1-prototype.1`), `ContentDigest`, `StateVersion` (incremented once per accepted command that changes state).
- Character: `Background`, `Difficulty`, `PronounSet`. `PronounSet` is stored only; no rule reads it. A test proves that changing it does not change any outcome hash.
- Time and randomness: `CareerWeek` (1–1,040), `Rng` (PCG32 state).
- Values: `CashPln` (`long`, may be negative), `Followers` (`long`, ≥ 0), `LastLiveViewers` (`long`, ≥ 0), `Drama`, `Credibility`, `Energy`, `Burnout` (`Fixed`, 0–100), `Skill[5]`, `Boredom[5]` (`Fixed`, 0–100), `InactiveWeeks[5]`, `Equipment` (microphone, camera, computer, connection; tier 0–4), `EventCooldowns`.
- Cumulative metrics: total streams, total live views, gross income and costs by source, weeks per category, events resolved, criticals.
- Lifecycle: `Status` (Active, Ended), `EndingReason?`, `FinalScore?`, `PendingWeek?` (see §5).

`CareerInvariants.Validate(state)` returns all violations: week in range; bounded stats in 0–100; followers and viewers ≥ 0; equipment tiers 0–4; Ended ⇔ `EndingReason` and `FinalScore` present; Active ⇒ `CashPln` > −1,000; `PendingWeek` only when Active. Tests, the engine (debug assertion) and the batch runner call it after every step.

Rulesets: `IRuleset` implements the formulas and week order. `RulesetRegistry` maps `RulesetId` to implementations and keeps old ones. The engine always resolves the ruleset from the career, never "latest". An unknown ID raises `DomainError.UnknownRuleset`; there is no fallback. Acceptance test: a registry with a test-only legacy ruleset and `M1Ruleset`; a career created on the legacy ID keeps using it.

Deterministic serialization: canonical JSON through System.Text.Json source generation with a fixed property order, `Fixed` as its raw integer, invariant formatting and no floating point. `StateHash = SHA-256(canonical bytes)`. Tests: round-trip produces identical bytes; the same seed and choices produce the same hash after 1,040 weeks.

## 5. Weekly engine (#4)

The week is a small state machine of pure functions. In-progress data lives in `CareerState.PendingWeek`, so each step can be persisted and replayed later by the API.

1. `StartWeek(state, WeekPlan{Action, Category}, expectedVersion)` — apply recurring effects; validate and apply the main action (before the stream); select 2 or 3 events from eligible events by weight, respecting cooldowns, using PCG32.
2. `PresentEvent(state)` — no random draw. For each choice returns `p = clamp(0.05, 0.95, base + Σ modifiers)`, threshold `round(100p)`, the modifier list (source, value) or `Guaranteed` with its deterministic cost.
3. `ResolveEvent(state, choiceId, expectedVersion)` — roll 1–100; success when `roll ≤ threshold`; roll 1 is a critical success and roll 100 a critical failure only when the choice allows criticals. Records roll, threshold, modifiers and the applied outcome.
4. `SettleWeek(state, expectedVersion)` — viewers, followers (gained, lost, net), income and costs, skill and boredom, energy and burnout, weekly report, ending check, `Week + 1`, `PendingWeek = null`.
5. `Retire(state, expectedVersion)` — only between weeks; ends the career with `EarlyRetirement`.

Errors are returned as `DomainError` values (`IllegalAction`, `StaleVersion`, `WeekAlreadySettled`, `NoPendingWeek`, `EventsUnresolved`, `CareerEnded`, `UnknownChoice`, `UnknownRuleset`). A command with a stale `expectedVersion` never draws a new random value.

Automation: `IChoicePolicy` selects plans and event choices. Manual and automated play call the same functions. M1 policies: Balanced, Grinder (never rests until forced), RestHeavy, Specialist (one category), Rotator.

Actions (M1): Rest (+25 energy, −12 burnout), Train category (+2–4 skill with diminishing returns, −8 energy), Improve production (buy next tier of one equipment slot; requires cash; disabled with reason when unaffordable or cash negative), Manage community (+credibility or −drama, −boredom), No action (+5 energy). A regular stream then costs 8 energy and adds 3 burnout. At energy 0 or burnout 100 the next week only allows Rest (M1 simplification of the mandatory recovery choice).

Event JSON (M1): `id`, `titleKey`, `textPl` (working copy), `weight`, `cooldownWeeks`, `eligibility` (week range, min/max stats), `choices[]` with `id`, `labelKey`, `guaranteed` or `baseChance`, `modifiers[]` (stat × coefficient, optional cap), `allowsCritical`, and `outcomes` (`success`, `failure`, optional `criticalSuccess`, `criticalFailure`) made of declarative effects: stat delta, cash delta, audience multiplier, event quality. `ContentValidator` checks unique IDs, known stats and effect types, bounded numbers, at least one choice per event, and at least three eligible events in every week from 1 to 1,040.

## 6. Audience, finances, score and endings (#5)

Formulas follow `gameplay-systems.md` §3 and §6 with Czatnia factors 1.0:

- `quality`, `baselineViewers`, `liveViewers` (seeded noise 0.85–1.15, boredom and event multipliers, viral cap 3×), `newFollowers` reported as gained, lost and net; skill gain and disuse decay; boredom growth and relief.
- Income: subscriptions, donations, ads. Costs: living cost 100 PLN/week (easy −15%), equipment upkeep, equipment purchases.
- Followers and live viewers are separate in state, report and score.
- Reconciliation: for every week, `Σ income lines − Σ cost lines == Δ CashPln` (property test).

Endings happen exactly once: `Completed` after settling week 1,040; `Insolvency` when cash ≤ −1,000 at the end of settlement; `EarlyRetirement` through `Retire`. Ended careers are read-only; every command returns `CareerEnded`.

Score: `F = clamp(0,1, log10(1+followers)/log10(1+1,000,000))`; `W` likewise for positive cash with 10,000,000 (Insolvency counts wealth 0); `A = min(1, achievementPoints/10)` (M1 achievement set is minimal: first 1,000 / 10,000 / 100,000 followers, completed career); `base = round(100,000 × (0.45F + 0.45W + 0.10A))`; `final = round(base × difficulty × background)`. `ScoreBreakdown` exposes every component. Tests: monotonic in followers and cash with other inputs fixed; Insolvency and EarlyRetirement produce distinct ending records.

## 7. Batch simulator and performance (#6)

`dotnet run --project Tools/BatchSimulator -- --careers 10000 --seed 42 --policies all --out <dir>`:

- Derives each career seed from the master seed and index; runs careers in parallel; each career is independent.
- Report (Markdown + JSON): completion rate; ending distribution; percentiles (p5, p50, p95, max) of followers, cash and score by policy, background and difficulty; extreme paths (top and bottom five seeds with a short path summary); invariant failures; runtime per career, nanoseconds per week, total allocated bytes, peak working set.
- The deterministic section is byte-identical for identical arguments; timing and memory are in a separate section.
- Any invariant failure gives a non-zero exit code. An xUnit test runs about 200 careers and fails on any violation.
- The first baseline report is saved to `docs/reports/m1-baseline-simulation.md`.

## 8. Documentation (#1, #7)

- `README.md`: purpose, status, repository layout, links to GDD, validation plan and all specifications, local build and test commands.
- `docs/requirements-traceability.md`: requirement ID → source document → acceptance criterion → issue or milestone → status; a "superseded decisions → replacement" table.
- `docs/content-capacity-estimate.md`: method, CSV measurement template, effort model (writing, three pronoun variants, QA) and a proposed launch floor by phase and category, marked as a proposal until the owner measures and approves it.
- Decision register and GDD updates for every decision in §1, per `AGENTS.md`.

## 9. Testing and quality

- TDD red → green → refactor for all production behavior.
- Test kinds: unit, property (FsCheck: random legal choice sequences never break invariants; reconciliation; determinism), golden fixtures (normal week, critical success and failure, insolvency, early retirement, full 1,040-week run hash, old-ruleset career), architecture tests.
- Coverage with coverlet; backend line coverage ≥ 80% (Tests and generated code excluded explicitly).
- `dotnet format --verify-no-changes` and warnings-as-errors (already in `Directory.Build.props`).

## 10. Branches and order

`develop` ← `feature/m1-01-readme-traceability` (#1) → `feature/m1-02-dotnet-scaffold` (#2) → `feature/m1-03-career-state` (#3) → `feature/m1-04-week-engine` (#4) → `feature/m1-05-economy-score-endings` (#5) → `feature/m1-06-batch-simulator` (#6) → `feature/m1-07-content-capacity` (#7). This design document is committed on `develop`. Branch #1 starts from `develop`; #2–#6 stack in order; #7 starts from `develop` (documentation only).

## 11. Out of scope for M1

Sponsors and contracts, gambling, adult side business, Network/collaborate, PulsLive and Nurt, migration and bans, annual review pauses, mandatory story choices beyond forced rest, HTTP endpoints, persistence, Polish copy completeness, frontend, CI workflows, Lefthook.
