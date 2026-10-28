# M3-C Automation and Endings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 4-, 13- and 52-week batches under a player strategy, data-driven achievements, the ending summary and the visible score breakdown, without changing `m1-prototype.1`, `m3-systems.1` or stored requests.

**Spec:** `docs/superpowers/specs/2026-10-26-m3c-automation-and-endings-design.md`

## Global Constraints

- `Domain` references only the BCL; no `double`, `decimal`, `DateTime` or `System.Random` there.
- `M1GoldenTests` and `M3GoldenTests` stay green after every task.
- New nullable state and request fields carry `JsonIgnore(WhenWritingNull)`.
- TDD: each behaviour starts with a failing test. Coverage ≥ 80 %. SonarQube Cloud zero open issues on every PR.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A stored 13-week batch request without a strategy re-serializes byte for byte and replays as `Balanced`.
2. A strategy whose preferred plan is disabled (no cash, rest-only week, decision pending) never sends an illegal plan.
3. Achievements on `m3-systems.1` content and on M1 content keep the hard-coded set.
4. A wager or upgrade never takes cash below the reserve.

## Delivery

| PR | Branch | Tasks |
| --- | --- | --- |
| 1 Rules, content and simulation | `feature/m3c-1-rules` | 0–6 |
| 2 API and play | `feature/m3c-2-play` | 7–10 |

# PR 1

### Task 0: Record the design
- [ ] Spec, GDD rows, TD-118, gameplay §8 pointer, #23 scope, #27 comment. Commit `docs: record the M3-C automation and endings design`.

### Task 1: `Strategy` and its validation
- Create `Domain/Simulation/Policies/Strategy.cs`: enums `CategoryPlan { Strongest, Focus, Rotate }`, `RiskAppetite { Safe, Balanced, Bold }`; `record Strategy(CategoryPlan CategoryPlan, Category? Focus, ImmutableArray<MainAction> Routine, int RestBelowEnergy, int RestAboveBurnout, long ReservePln, bool Upgrades, RiskAppetite Risk, bool AcceptOffers, WagerTier? Wager, bool SideBusiness)` with `Default` and `IReadOnlyList<string> Errors()` (spec §5 codes).
- Tests `StrategyTests`: default has no errors; each error code; equality compares the routine by sequence.

### Task 2: `StrategyPolicy`
- Create `Domain/Simulation/Policies/StrategyPolicy.cs` implementing `IChoicePolicy` (spec §5 plan order, wager, offers, events, fallback).
- Tests `StrategyPolicyTests` on `M3BPlay`: routine step and reason; forced rest; due installment; reserve blocks upgrade and wager; wager tier attached on Gambling; side-business week; fallback-safe when the routine action is disabled; offers accepted/declined; risk threshold for gambles; ban move; a full career with each simulator strategy has no invariant violation.
- `BuiltInPolicies.Strategies` = `Strategy-Default`, `Strategy-Safe`, `Strategy-Bold`; `BuiltInPolicies.M3CAll = [.. M3BAll, .. Strategies]`; `BatchRunner.PoliciesFor(SystemsV2)` returns `M3CAll`.

### Task 3: Achievements section, metrics and rules
- `Domain/Content/AchievementDefinition.cs` (`AchievementKind`, record with `Id, Name, Description, Kind, Threshold?, Points`); `BalanceConstants.Achievements : ImmutableArray<AchievementDefinition>?`; loader reads `achievements`; validator rules.
- `CareerMetrics`: nullable `PeakDrama`, `SponsorInstallments`; `M3Ruleset` updates them at settlement when the section exists.
- `Achievements.Evaluate(s, completed, defs)` and `Points(achievements, defs)` use the definitions when present; `WeekSettlement.Close`, `RuleSteps.End` and `Retire` pass the balance's definitions.
- Tests: loader, validator, each kind, never revoked, M1/M3-A unchanged (golden), retire evaluates.

### Task 4: `EndingSummary` and ending tests
- `Domain/Scoring/EndingSummary.cs` (spec §7).
- `EndingFlowTests`: each of the five endings through the engine on M3-B content with a summary and a final score.

### Task 5: Content
- `content/m3/balance.json` gains `achievements` (spec §6 set, ICU Polish names and descriptions); content tests and the frontend ICU cross-check include them.

### Task 6: Simulator section, report and tuning; open PR 1
- `CareerSummary` gains `AchievementPoints`, `Achievements`; report section "Achievements and strategies" and its criteria (spec §9); `docs/reports/m3c-baseline-simulation.md`; tune achievements; gameplay §13; open PR 1.

# PR 2

### Task 7: Batch contract
- `BatchInput` with optional `Strategy` and weeks 4/13/52; `BatchRequest` and validators; `RunBatchHandler` builds `StrategyPolicy`; tests for 4 and 52 weeks, strategy repeat and `command-id-reused`, invalid strategy `validation-failed`, stored request byte-for-byte.

### Task 8: Career view
- `AchievementView`, `ScoreView`, `EndingSummaryView`; `CareerView` members; `StoredContract` defaults; OpenAPI.

### Task 9: Frontend
- Batch dialog: length and strategy form (localStorage per career); ending view with narrative, score table and achievements; translations; tests.

### Task 10: Journey and close-out; open PR 2
- Playwright 4-week batch with a strategy; docs (traceability, architecture, spec status); issue comments; PR 2.
