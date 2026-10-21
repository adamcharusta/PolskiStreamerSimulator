# M3-B Economy and Risk Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ruleset `m3-systems.2` with sponsors (offers, one-off deals, contracts, breaches), a fictional-PLN wager on gambling streams, an optional text-only side business, three platforms with policy strikes, bans, migration and two new endings, and make new careers play it in the browser, while `m1-prototype.1` and `m3-systems.1` careers keep byte-identical results.

**Architecture:** `m3-systems.1` is frozen by a second golden state-hash test before anything moves. `M3Ruleset` takes its ruleset ID in the constructor and is registered twice; every M3-B rule reads an optional balance section (`platforms`, `migration`, `policy`, `sponsors`, `wagers`, `sideBusiness`, `platformBan`) or the optional `sponsors` array of `events.json`, and is inert when it is absent, so a `m3-systems.1` snapshot plays exactly as before. The new rules live in small static classes under `Domain/Rulesets/M3B/` (`SponsorRules`, `WagerRules`, `SideBusinessRules`, `PolicyRules`, `PlatformRules`) that `M3Ruleset` calls at fixed points of `StartWeek` and `SettleWeek`. New state is nullable and omitted from canonical JSON when null; a null field means "the M3-A default" (Czatnia, no strikes, no offers), so `CareerFactory` does not change. Cash movements are report lines placed in the pending week before `WeekSettlement.Core`, so the existing finance path reconciles them.

**Tech Stack:** .NET 10 (`Domain` without packages, xUnit v3, FsCheck), EF Core SQL Server with Testcontainers, Wolverine HTTP, React 19 + TanStack Query + i18next, `intl-messageformat`, Orval, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md`

## Plan-time rulings

Decisions the spec leaves open or the code forces. Task 0 records every ruling marked *(spec edit)* in the spec (§15) and in TD-117.

1. **Ruleset IDs.** `M3Ruleset.RulesetId` is renamed `M3Ruleset.SystemsV1 = "m3-systems.1"`; `M3Ruleset.SystemsV2 = "m3-systems.2"` is added; `M3Ruleset(string id)` accepts only these two; `M3Ruleset.Versions` holds one instance of each for registries.
2. **Balance sections** *(spec edit, §3/§4)*: `platforms`, `migration`, `policy`, `sponsors`, `wagers`, `sideBusiness`, `platformBan`. Each is `null` on `BalanceConstants` when absent; a rule whose section is null does nothing and draws nothing.
3. **Null means default.** `CareerState.Platform == null` means Czatnia, `PlatformSinceWeek == null` means week 1; `Offers`, `Contracts`, `Strikes`, `BannedPlatforms` null mean empty. The fields are written only when they first change, so `CareerFactory.Create` stays as it is and no M1 or `m3-systems.1` hash moves.
4. **Strikes as a list.** Strikes are `ImmutableArray<PolicyStrike>` (`Platform`, `GivenWeek`) in the order given, not a dictionary, so canonical JSON needs no new converter. A strike is active while `GivenWeek + policy.strikeWeeks > settledWeek`.
5. **Wager in the plan.** `WeekPlan` gains trailing `WagerTier? Wager = null` and `Platform? TargetPlatform = null`, both `[property: JsonIgnore(WhenWritingNull)]`. `WagerTier` is `Low, Medium, High`; *no wager is `null`* (the spec's `None`). Wager variants exist only for `Gambling` plans.
6. **Spendable cash for a stake** *(spec edit, §7.1)*: `spendable = cash + min(0, carry cash) − the plan's equipment price`; a stake is legal when `spendable > 0` and `stake × 4 ≤ spendable` (25 %). `StartWeek` re-runs the same check, so a stale plan is refused with `needs-cash` instead of overdrawing. Event choice requirements (`needs-cash`) keep reading current cash (TD-116 open question answered: unchanged).
7. **Wager timing.** The wager rolls at `StartWeek` after the main action (draw order: action, network or pitch roll, wager roll, migration draw, event selection). Its cash is a pending line (`income:wager` net win or `cost:wager` stake) that settles with the week like event cash; drama applies at once; the audience factor goes into `PendingWeek.AudienceMultiplier`. Exposure per tier: Low 1, Medium 2, High 3 *(spec edit, §5.2)*.
8. **Frozen pre-week values.** Policy exposure and migration retention are computed from the state before the week starts and frozen in the pending week (`PendingWeek.Exposure`, `PendingWeek.Migration`), so the figures on the plan screen are the figures used *(spec edit, §5.2/§5.4)*.
9. **Migration timing** *(spec edit, §5.4)*: the move of a `PlanPlatformMigration` week executes at that week's settlement after the audience change (the week streams on the old platform). The ±0.05 retention draw happens at `StartWeek`. "Executing without an announcement" happens only through the `platform-ban` decision; a first `PlanPlatformMigration` toward a target always announces, a second one within 4 weeks executes.
10. **Ban timing** *(spec edit, §5.3)*: bans are evaluated only at settlement, after the strike roll; a strike from a live event or a decision counts at the next settlement. `GlobalBan` (crisis decisions only) sets the system flag `sys:global-ban`; the career ends with `GlobalBan` at the next settlement. Ending order at settlement: `Insolvency`, `GlobalBan`, `AllPlatformsBanned`, `Completed`.
11. **Ruleset-built decision.** `platform-ban` is not a content event. `PlatformRules.BanDecision(s, c)` builds a synthetic `EventDefinition` (phase `Crisis`, title and text from `balance.platformBan`, one guaranteed choice `move-{Platform}` per accessible platform with the label from `platformBan.choices`). `EventPresentation` gains `EventDefinition? Definition = null`, set for this decision, so `CareerViewBuilder` never looks it up in `c.Events`. `ChoiceOdds` gains `MigrationEstimate? Migration = null`. The move applies the platform switch at once and carries the follower retention into the next settlement through `DecisionCarry.Migration`; `Retire` folds it like the carry cash.
12. **Offer IDs and limits.** Offer ID = `"{templateId}:{offeredWeek}"`; at most one open offer per template; the contract keeps the offer's ID. A template is "the same sponsor".
13. **Offer chance** *(spec edit, §6.2)*: `magnitude` = the number of `k ≥ 1` with `followers ≥ 1,000 × 10^k` (integers only); `chance = min(cap, base + perMagnitude × magnitude)`; no draw when no template is eligible. Payment `= min(cap, paymentPln + paymentPerThousandFollowersPln × (followers / 1,000))`, integer division, fixed when the offer opens.
14. **Insider lead.** `balance.sponsors.startingLeads` maps a background to a template ID. A new overload `CareerFactory.Create(ContentSnapshot c, Background, Difficulty, PronounSet, ulong seed)` adds the lead offer; `CreateCareerHandler` and the batch simulator use it; tests that pin hashes keep the old overload.
15. **Pitch odds** *(spec edit, §6.2)*: `balance.sponsors.pitch` is a schema-2 `ChoiceDefinition` (base 0.15, `Credibility` modifier 0.004 → 35 % at credibility 50; the burnout penalty applies through `OddsCalculator`; no critical). Its outcomes may hold only `StatDelta`; on success the ruleset opens one offer by weight. The pitch is disabled with `no-sponsor-fits` when no task is due and no template is eligible.
16. **Contract schedule** *(spec edit, §6.3)*: accepted between weeks with `s.Week = A`, the first installment is due in `A + dueEveryWeeks − 1`, then every `dueEveryWeeks` weeks through `TermEndWeek = A + termWeeks − 1` (validator: `termWeeks % dueEveryWeeks == 0`). One `PitchFulfillSponsor` fulfils every installment due that week whose required category matches. A week off (`sys:rest-only`) moves a due installment one week later and extends `TermEndWeek` by one. A one-off deal is due in week `A`.
17. **Tied contracts.** A contract is tied to a platform when its template lists platforms; a migration or ban move ends every contract whose template lists platforms that exclude the new platform, without penalty.
18. **`respondOffer`** is a free command between weeks (allowed while decisions are queued, refused during a week with `week-in-progress`). Refusals are `IllegalAction` with `unknown-offer` (also for an expired offer, which is removed at settlement), `contract-limit`, `same-sponsor` or `prerequisite:<name>`; no new error codes. Log kind `RespondOffer` (12 characters, fits the 16-character `kind` column).
19. **Command fields.** `ActionInput`/`ActionRequest` gain trailing `WagerTier? Wager`, `Platform? TargetPlatform`, `string? OfferId`, `bool? Accept`, omitted from JSON when null, so a stored M2/M3-A request re-serializes byte for byte (TD-116 comparison). New view fields are required record parameters filled by `StoredContract` for older rows.
20. **Effects need content.** `EffectApplier.ToWeek` and `ToCarry` gain a `ContentSnapshot c` parameter, because `SponsorOffer` must read its template.
21. **Platform factors.** `PlatformRules.Effective(BalanceConstants b, CareerState s, Category c)` returns `b with { PlatformReach = reach × categoryReach, PlatformSubYield, PlatformAdYield }` for the current platform (or `b` unchanged when `b.Platforms` is null); settlement passes `c with { Balance = effective }` to `WeekSettlement.Core`. No formula in `AudienceRules`/`FinanceRules` changes.
22. **Drama decay.** In `policy.dramaDecay` (1). A week "added drama" when any stat line `stat:Drama` of the week (pending lines, settlement lines, carried decision lines) has a positive delta; otherwise drama drops by the decay with a `stat:Drama` line, detail `decay`.
23. **Plan preview.** `IRuleset.PresentPlan(s, c, plan) : PlanPreview` gives the wager terms, sponsor consequences, policy exposure and migration estimate of one plan; `PresentAction` also returns the pitch odds. Both are pure.
24. **Policies.** `BuiltInPolicies.M3All` is frozen (it feeds the `m3-systems.1` golden test); `BuiltInPolicies.M3BAll = M3All + Gambler, SideHustler, SponsorHunter, Migrator`. `IChoicePolicy` gains `OfferDecision ChooseOffer(CareerState s, ContentSnapshot c, SponsorOffer offer, string? blockedReason)`; `CareerRunner` and the browser batch answer offers before planning each week. Every platform gives the same base retention, so Balanced's `ban-move` breaks ties by the larger announced bonus, then the target's reach factor, then enum order. Migrator announces in the first legal week (week 27) and executes in week 28 *(spec edit, §9)*.
25. **Content count** *(spec edit, §8)*: 15 events — 6 platform/policy (the sixth is `policy-first-strike`, a crisis with `minStrikes: 1`), 4 side-business, 3 wager live events and 2 sponsor events that use `SponsorOffer`. Existing events are never edited under the same version (TD-112); a changed event or sponsor template gets a new version. Sponsor templates are immutable by `(id, version)` like events.
26. **Pre-flight rulings.** Pre-flight rulings F1–F14 (2026-10-21) are applied in the tasks below.

## Global Constraints

- `Domain` references only the BCL; no `System.Random`, `DateTime`, `double`, `float`, `decimal` or `Math.Sqrt/Log` (enforced by `Tests/Architecture/LayerDependencyTests.cs`).
- Every random draw comes from the career's `Pcg32` in `CareerState.Rng`; a skipped draw must be skipped identically on replay.
- `m1-prototype.1` **and** `m3-systems.1` results must not change: `M1GoldenTests` and `M3GoldenTests` (Task 1) stay green after every task.
- New nullable state fields carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` (positional record members use `[property: …]`).
- Player-facing text is Polish; documentation, identifiers and decision records are English.
- Event and sponsor text is ICU MessageFormat: `{gender, select, female {…} male {…} other {…}}`; `other` is the gender-neutral form (`zrobiłoś`); an ASCII apostrophe is written `''`; simple `{name}` arguments are rejected (TD-114).
- TDD: each behaviour starts with a failing test. Backend and frontend line coverage ≥ 80 %. SonarQube Cloud zero open issues on every PR (TD-96).
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Backend commands run from `source/backend`; frontend commands from `source/frontend`.

## Review Focus

1. **A stored `m3-systems.1` career after the deploy** — its state JSON (with or without a pending week) must deserialize, keep its hash, resolve to `m3-systems.1` and keep playing with no B rule firing. Pinned by Task 1 (`M3_systems_1_careers_reproduce_their_recorded_final_hashes`), Task 2 (`Stored_m3_systems_1_career_resolves_to_its_own_ruleset`) and Task 3 (`Stored_m3a_state_round_trips_without_new_fields`).
2. **A contract due in a week the recovery crisis forces to rest** — the installment must move one week without a penalty, and the term must keep its installment count. Pinned by Task 9 (`Week_off_postpones_a_due_installment_without_penalty`).
3. **A wager when cash drops below the stake between planning and start** — an upgrade in the same plan or a negative decision carry must make the stake `needs-cash`, and `StartWeek` must refuse a stale plan rather than overdraw. Pinned by Task 10 (`Upgrade_in_the_same_plan_leaves_too_little_cash_for_the_stake`, `Negative_decision_carry_counts_against_the_stake`, `Start_week_refuses_a_stake_the_state_no_longer_covers`).
4. **A ban of the current platform while a decision, offer or contract is pending, and while a batch runs** — the ban decision opens first, the batch stops before it, tied contracts end without penalty, offers survive and are re-checked on accept, and a forced move with zero starting followers applies its retention only to the followers gained that week. Pinned by Task 16 (`Ban_opens_before_a_queued_recovery_crisis`, `Ban_move_ends_tied_contracts_without_penalty`, `Offer_for_the_banned_platform_is_refused_after_the_move`), Task 15 (`Migration_with_zero_followers_moves_and_reports_zero`) and Task 20 (`Batch_stops_before_the_platform_ban_decision`).
5. **`respondOffer` repeated with the same command ID, or sent for an expired offer** — the repeat returns the stored outcome and changes nothing; an expired offer is refused with `unknown-offer`. Pinned by Task 8 (`Responding_to_an_expired_offer_is_rejected`) and Task 20 (`Repeated_respond_offer_returns_the_stored_outcome`).

## File structure

**Domain**
- `Domain/Careers/Enums.cs` (modify): `Platform`, `WagerTier`, `SponsorKind`; `EndingReason.GlobalBan`, `EndingReason.AllPlatformsBanned`.
- `Domain/Careers/CareerState.cs`, `CareerInvariants.cs`, `CareerFactory.cs` (modify).
- `Domain/Careers/EconomyState.cs` (new): `PolicyStrike`, `SponsorOffer`, `SponsorContract`, `SideBusiness`, `Migration`.
- `Domain/Content/SponsorTemplate.cs` (new): template, prerequisites, breach.
- `Domain/Content/EconomyBalance.cs` (new): `PlatformProfile`, `MigrationBalance`, `PolicyBalance`, `SponsorBalance`, `WagerBalance`, `SideBusinessBalance`, `PlatformBanText`.
- `Domain/Content/EventDefinition.cs`, `ContentSnapshot.cs`, `BalanceConstants.cs`, `ContentValidator.cs` (modify).
- `Domain/Rulesets/M3Ruleset.cs`, `IRuleset.cs`, `M1Ruleset.cs`, `M3/DecisionQueue.cs`, `Shared/EffectApplier.cs`, `Shared/LiveEvents.cs`, `Shared/WeekActions.cs`, `Shared/WeekSettlement.cs` (modify).
- `Domain/Rulesets/M3B/SponsorRules.cs`, `WagerRules.cs`, `SideBusinessRules.cs`, `PolicyRules.cs`, `PlatformRules.cs`, `PlanPreview.cs` (new).
- `Domain/Simulation/WeekPlan.cs`, `WeekReport.cs`, `PendingWeek.cs`, `DecisionCarry.cs`, `EventPresentation.cs`, `EventSelector.cs`, `WeekEngine.cs`, `CareerRunner.cs`, `Policies/*.cs` (modify).

**Application / Infrastructure / WebApp**
- `Infrastructure/Content/JsonContentSource.cs` (sponsors, eligibility, sections), `EventVersions.cs` (sponsor versions) (modify).
- `Application/Careers/Contracts.cs`, `CareerViewBuilder.cs`, `ReportViews.cs`, `SubmitAction.cs`, `StoredContract.cs`, `RunBatch.cs`, `CreateCareer.cs`, `Persistence/CareerRecords.cs`, `Application/ApplicationServices.cs` (modify).
- `WebApp/Careers/Requests.cs`, `WebApp/openapi/v1.json` (modify, regenerated).

**Content and tools**
- `content/m3/balance.json`, `content/m3/events.json` (modify: ruleset `m3-systems.2`, sections, 12 sponsor templates, 15 events).
- `source/backend/Tests/Golden/m3-systems.1/` (new: frozen copy of today's `content/m3`).
- `Tools/BatchSimulator/BatchRunner.cs`, `BatchStatistics.cs`, `ReportWriter.cs` (modify); `docs/reports/m3b-baseline-simulation.md` (new, generated).

**Tests (new)**
- `Tests/Domain/Rulesets/M3GoldenTests.cs`, `m3-golden-hashes.txt`; `Tests/Support/M3BTestContent.cs`, `M3BPlay.cs`; `Tests/Domain/Rulesets/M3B/*Tests.cs`; `Tests/Domain/Content/ContentValidatorM3BTests.cs`; `Tests/Application/Careers/EconomyFlowTests.cs`.

**Frontend**
- `src/features/career/PlatformCard.tsx`, `OffersCard.tsx` (new, with tests); `CareerDesk.tsx`, `plans.ts`, `reportLabels.ts`, `stage/PlanWeek.tsx`, `stage/ChoiceOdds.tsx`, `stage/WeekReport.tsx`, `src/locales/pl/translation.json`, `src/content/m3Content.test.ts`, `src/api/generated/*` (regenerated), `e2e/journey.spec.ts` (modify).

## Delivery

Four PRs into `develop`, each from its own branch, merged before the next starts.

| PR | Branch | Tasks |
| --- | --- | --- |
| 1 Foundation | `feature/m3b-1-foundation` | 0–6 |
| 2 Economy rules | `feature/m3b-2-economy` | 7–12 |
| 3 Platform rules, content and simulation | `feature/m3b-3-platforms` | 13–19 |
| 4 API and play | `feature/m3b-4-play` | 20–23 |

PR 1's branch already exists and is stacked on `feature/m3a-3-play` because M3-A PRs #81 and #83 await merge: work on it as is, and **rebase it onto `develop` after #81 and #83 merge**, before opening PR 1. PRs 2–4 branch from the latest `develop` after the previous PR merged (`git switch -c feature/m3b-<n>-<name> origin/develop`).

Before each PR: `dotnet test` (all, including the two golden tests), and `npm run lint && npm run typecheck && npm test -- --coverage` when the frontend changed; then open the PR, wait for CI, check SonarQube Cloud with `https://sonarcloud.io/api/issues/search?componentKeys=adamcharusta_PolskiStreamerSimulator&pullRequest=<n>&resolved=false` (must be `"total":0`) and ask the owner to merge. After the merge, check `&branch=develop`.

---

# PR 1 — Foundation

### Task 0: Record the design and the plan-time rulings

**Files:**
- Add: `docs/superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md` (currently untracked), `docs/superpowers/plans/2026-10-21-m3b-economy-and-risk.md`
- Modify: the spec (§5.4, §8, new §15), `docs/game-design-document.md` (§6 decision log), `docs/technical-decisions.md` (TD-117), `docs/requirements-traceability.md`

- [ ] **Step 1: Edit the spec**

In §5.4 replace "A second use within those 4 weeks executes it; executing without an announcement is allowed with bonus 0." with "A second use within those 4 weeks executes it at that week's settlement. A move without an announcement happens only through the `platform-ban` decision (bonus 0)."

In §8, in the content bullet, replace "6 platform and policy events (copyright, gambling promo, privacy incident with a disclosed `GlobalBan` risk, PulsLive discovery, Nurt community), 4 side-business events, 3 wager-related live events" with "6 platform and policy events (copyright, gambling promo, privacy incident with a disclosed `GlobalBan` risk, PulsLive discovery, Nurt community, first-strike warning), 4 side-business events, 3 wager-related live events and 2 sponsor events that open a named offer (15 events)".

Append:

```markdown
## 15. Plan-time rulings (2026-10-21)

Agent proposal/default, recorded with the implementation plan (`docs/superpowers/plans/2026-10-21-m3b-economy-and-risk.md`, "Plan-time rulings"); they refine the sections named and are recorded in TD-117.

- §3/§4: the optional balance sections are `platforms`, `migration`, `policy`, `sponsors`, `wagers`, `sideBusiness` and `platformBan`; a rule whose section is absent does nothing and draws nothing.
- §5.2: wager exposure is 1 (Low), 2 (Medium), 3 (High). Exposure and the strike chance are computed from the plan and the state before the week starts, so the plan screen shows the figures that are rolled.
- §5.3: bans are evaluated only at settlement, after the strike roll. `GlobalBan` sets `sys:global-ban`; the career ends at the next settlement. Ending order: insolvency, global ban, all platforms banned, completion.
- §5.4: retention is computed from the state before the week and frozen with its ±0.05 draw at week start; the move executes at that week's settlement after the audience change. A `platform-ban` move switches the platform at once and applies the retention at the next settlement.
- §6.2: offer chance uses integer orders of magnitude (`followers ≥ 1,000 × 10^k`); no draw when no template is eligible; payment is fixed when the offer opens. The pitch is a balance choice with base 15 % + 0.4 points per credibility point (35 % at 50) and is disabled (`no-sponsor-fits`) when no task is due and no template fits. Offer IDs are `{templateId}:{week}`; one open offer per template.
- §6.3: the first installment is due `dueEveryWeeks − 1` weeks after the week of acceptance and then every `dueEveryWeeks` weeks through `termWeeks`; one `PitchFulfillSponsor` fulfils every installment due that week; a week off moves the due installment and the term end one week. A contract is tied to a platform when its template lists platforms.
- §7.1: a stake must fit 25 % of spendable cash (cash, minus a negative decision carry, minus the plan's equipment price); no wager is `null` rather than `None`.
- §9: Migrator announces in week 27 (its first legal week) and moves in week 28. Retention does not depend on the target, so Balanced's `ban-move` prefers an announced target, then higher reach.
```

- [ ] **Step 2: Add the GDD decision-log rows**

Append to `docs/game-design-document.md` §6, each with source "Agent proposal/default, 2026-10-21 (M3-B design; the owner asked the agent to design sub-project B without an interview, 'zacznij sam')":

| Date | Decision |
| --- | --- |
| 2026-10-21 | M3-B economy and risk design adopted as an agent proposal: sponsors with visible terms and breaches, a fictional-PLN wager, an optional text-only side business, three platforms with strikes, bans and migration ([design](superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md)). Previous plan: GDD §2 and gameplay §6–§7 described the systems without rules. Impact: ruleset `m3-systems.2`; every number is tuned by the M3-B simulation. |
| 2026-10-21 | Wager stakes 100 / 500 / 2,000 PLN, at most 25 % of spendable cash, chance 45 %, payout 2× (expected −10 % of the stake). Reason: gambling must never pay on average. |
| 2026-10-21 | Side business legal from week 52 with at least 2,000 followers; never required by any content or policy path. |
| 2026-10-21 | Three active policy strikes (26-week expiry) ban the career from that platform permanently; a disclosed `GlobalBan` risk only on crisis failure outcomes. |
| 2026-10-21 | Sponsor offers are a free selection between weeks (`respondOffer`), never a main action and never a batch pause. |
| 2026-10-21 | Ad intensity (`adLoadChoice`) is not a plan option; `adLoad` stays a balance constant. Reason: it would triple the plan list for little decision value. Superseded: gameplay §6 `adLoadChoice` as a weekly choice. |

- [ ] **Step 3: Add TD-117**

Add after TD-116 in `docs/technical-decisions.md` (date 2026-10-21, source "Agent proposal/default (M3-B design §4, §12 and plan rulings)"; previous plan: one M3 ruleset ID; reason: keep stored `m3-systems.1` careers byte-identical while B adds rules; impact: the items below):

"(1) `M3Ruleset` takes its ID in the constructor and is registered as `m3-systems.1` and `m3-systems.2`; every M3-B rule reads an optional balance section and is inert without it; (2) a golden test pins 20 seeds × the frozen `BuiltInPolicies.M3All` × 1,040 weeks on a copy of the M3-A content in `Tests/Golden/m3-systems.1/`; (3) sponsor templates live in a top-level `sponsors` array of `events.json` (schema 2) and are immutable by `(id, version)` like events (TD-112); (4) the `platform-ban` decision is built by the ruleset from `balance.platforms`/`platformBan`, and `EventPresentation.Definition` carries it to the view; (5) new state fields are nullable, omitted when null, and null means the M3-A default; (6) new `ActionInput` fields are omitted from JSON when null so stored requests compare byte for byte; the log kind is `RespondOffer`; (7) platform factors enter settlement as an effective `BalanceConstants` copy; (8) the plan-time rulings in spec §15; (9) sponsor templates and events that were never on `main` may change in place under their version while M3-B is tuned (exception to the TD-112 immutability rule, no stored snapshot outside development holds them)."

- [ ] **Step 4: Update traceability**

In `docs/requirements-traceability.md` add "M3-B ([design](superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md), [#18](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/18), [#19](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/19), [#20](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/20))" to the issue column of REQ-01 (global and all-platform ban endings), REQ-06 (sponsor, wager and side-business lines reconcile), REQ-07 (trusted versus sensational sponsors), REQ-09 (migration and the ban decision) and REQ-10 (fictional-PLN wager, non-explicit side business), and set their status to `In progress (M3)`. Add a superseded row: "Gameplay §6 `adLoadChoice` as a weekly choice | `adLoad` balance constant (GDD 2026-10-21) | GDD §6".

- [ ] **Step 5: Commit**

```bash
git add docs
git commit -m "docs: record the M3-B economy and risk design, decisions and plan

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 1: Freeze `m3-systems.1` with a golden state-hash test

**Files:**
- Create: `source/backend/Tests/Golden/m3-systems.1/balance.json`, `events.json` (byte copies of today's `content/m3`)
- Create: `source/backend/Tests/Domain/Rulesets/M3GoldenTests.cs`, `m3-golden-hashes.txt` (generated)
- Modify: `source/backend/Tests/Tests.csproj`

**Interfaces:**
- Consumes: `CareerRunner.Run`, `CareerFactory.Create`, `BuiltInPolicies.M3All`, `M3Play.Engine`, `JsonContentSource`, `TestPaths.Golden`.
- Produces: `M3GoldenTests.Content` (the frozen snapshot), `M3GoldenTests.Compute()` → lines `"{policy} {seed} {hash}"`.

- [ ] **Step 1: Copy the content**

```bash
mkdir -p source/backend/Tests/Golden/m3-systems.1
cp content/m3/balance.json content/m3/events.json source/backend/Tests/Golden/m3-systems.1/
```

Never edit these files; they are the M3-A content as it shipped to `develop`.

- [ ] **Step 2: Write the test**

```csharp
public class M3GoldenTests
{
    private const string FileName = "m3-golden-hashes.txt";

    public static ContentSnapshot Content { get; } = new JsonContentSource(TestPaths.Golden("m3-systems.1")).Load();

    public static IReadOnlyList<string> Compute()
    {
        var lines = new List<string>();
        var backgrounds = Enum.GetValues<Background>();
        foreach (var policy in BuiltInPolicies.M3All)
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var background = backgrounds[(int)(seed % (ulong)backgrounds.Length)];
                var difficulty = seed % 2 == 0 ? Difficulty.Easy : Difficulty.Normal;
                var c = Content;
                var start = CareerFactory.Create(c.RulesetId, c.Digest, c.Profiles[background], background, difficulty, PronounSet.Nonbinary, seed);
                var result = CareerRunner.Run(M3Play.Engine, c, start, policy);
                Assert.Empty(result.InvariantViolations);
                lines.Add($"{policy.Name} {seed} {result.FinalHash}");
            }
        }

        return lines;
    }

    [Fact]
    public void M3_systems_1_careers_reproduce_their_recorded_final_hashes()
    {
        Assert.Equal("m3-systems.1", Content.RulesetId);
        var path = Path.Combine(AppContext.BaseDirectory, "Domain", "Rulesets", FileName);
        var actual = Compute();
        if (Environment.GetEnvironmentVariable("UPDATE_M3_GOLDEN") == "1")
        {
            File.WriteAllLines(Path.Combine(TestPaths.RepositoryRoot, "source", "backend", "Tests", "Domain", "Rulesets", FileName), actual);
        }

        Assert.Equal(File.ReadAllLines(path), actual);
    }
}
```

Add a class comment like `M1GoldenTests` ("regenerate only for a deliberate `m3-systems.1` change — never in M3-B"). In `Tests.csproj` add `<None Update="Domain/Rulesets/m3-golden-hashes.txt" CopyToOutputDirectory="PreserveNewest" />`.

- [ ] **Step 3: Generate on the unchanged code**

Run (Git Bash, twice as the M1 test notes): `UPDATE_M3_GOLDEN=1 dotnet test --filter M3GoldenTests`, then `dotnet test --filter M3GoldenTests`.
Expected: PASS; `m3-golden-hashes.txt` has 200 lines (10 policies × 20 seeds).

- [ ] **Step 4: Commit**

```bash
git add source/backend/Tests
git commit -m "test: pin m3-systems.1 career results with golden state hashes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Parameterised `M3Ruleset` and the `m3-systems.2` registration

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3Ruleset.cs`, `Application/ApplicationServices.cs`, `Tools/BatchSimulator/BatchRunner.cs`, `ReportWriter.cs`, `content/m3/balance.json`
- Modify (rename `M3Ruleset.RulesetId` → `SystemsV1`): `Tests/Support/M3Play.cs`, `M3TestContent.cs`, every test listed by `grep -rln "M3Ruleset.RulesetId\|m3-systems.1" source/backend --include=*.cs`
- Test: `source/backend/Tests/Domain/Rulesets/RulesetRegistryTests.cs`, `Tests/Content/M3ContentTests.cs`, `Tests/Application/Careers/DecisionFlowTests.cs`

**Interfaces:**
- Produces: `M3Ruleset(string id)`, `const string M3Ruleset.SystemsV1 = "m3-systems.1"`, `const string M3Ruleset.SystemsV2 = "m3-systems.2"`, `static IReadOnlyList<IRuleset> M3Ruleset.Versions`, `static bool M3Ruleset.IsM3(string rulesetId)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void M3_ruleset_is_registered_for_both_ids()
{
    var registry = new RulesetRegistry([new M1Ruleset(), .. M3Ruleset.Versions]);
    Assert.Equal("m3-systems.1", registry.Resolve("m3-systems.1").Value.Id);
    Assert.Equal("m3-systems.2", registry.Resolve("m3-systems.2").Value.Id);
}

[Fact]
public void M3_ruleset_refuses_an_unknown_id()
    => Assert.Throws<ArgumentException>(() => new M3Ruleset("m3-systems.9"));

[Fact]
public void Stored_m3_systems_1_career_resolves_to_its_own_ruleset()
{
    var c = M3GoldenTests.Content;
    var s = CareerFactory.Create(c.RulesetId, c.Digest, c.Profiles[Background.DormRoomNewcomer], Background.DormRoomNewcomer, Difficulty.Normal, PronounSet.Man, 5);
    var restored = CanonicalJson.Deserialize(CanonicalJson.Serialize(s));
    var week = M3Play.Engine.StartWeek(restored, c, new WeekPlan(MainAction.NoAction, Category.Fps), restored.StateVersion);
    Assert.True(week.IsSuccess);
    Assert.Equal("m3-systems.1", week.Value.RulesetId);
}
```

In `M3ContentTests.Loads_valid_for_the_m3_ruleset` expect `("m3-systems.2", 2)`; in `DecisionFlowTests.New_career_uses_the_ruleset_of_the_current_content` expect `"m3-systems.2"`.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "RulesetRegistryTests|M3ContentTests|DecisionFlowTests"` — Expected: compile errors for `M3Ruleset.Versions`.

- [ ] **Step 3: Implement**

- `M3Ruleset`: primary constructor `M3Ruleset(string id)` throwing `ArgumentException` unless `id` is `SystemsV1` or `SystemsV2`; `public string Id => id;`; `Versions = [new M3Ruleset(SystemsV1), new M3Ruleset(SystemsV2)]`; `IsM3(id) => id is SystemsV1 or SystemsV2`. Class comment: "registered as both IDs; M3-B rules are switched by balance sections, never by the ID".
- Rename every `M3Ruleset.RulesetId` to `M3Ruleset.SystemsV1`; `M3TestContent.RulesetId` stays `"m3-systems.1"`.
- Registries: `ApplicationServices`, `BatchRunner`, `M3Play.Engine` use `new RulesetRegistry([new M1Ruleset(), .. M3Ruleset.Versions])`.
- `BatchRunner.PoliciesFor`: `M3Ruleset.IsM3(rulesetId) ? BuiltInPolicies.M3All : BuiltInPolicies.All` (Task 18 adds the M3-B list); `ReportWriter.IsM3` uses `M3Ruleset.IsM3`.
- `content/m3/balance.json`: `"version": "m3.2"`, `"ruleset": "m3-systems.2"`. Nothing else changes, so the content plays exactly as `m3-systems.1`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS, including both golden tests.

- [ ] **Step 5: Commit**

```bash
git add source/backend content/m3
git commit -m "feat(domain): register M3Ruleset as m3-systems.1 and m3-systems.2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: Hash-neutral state for platforms, strikes, sponsors, side business and migration

**Files:**
- Create: `source/backend/Domain/Careers/EconomyState.cs`
- Modify: `Domain/Careers/Enums.cs`, `CareerState.cs`, `CareerInvariants.cs`, `Domain/Simulation/WeekPlan.cs`, `PendingWeek.cs`, `DecisionCarry.cs`, `WeekReport.cs`
- Test: `Tests/Domain/Serialization/CanonicalJsonTests.cs`, `Tests/Domain/Careers/CareerInvariantsTests.cs`

**Interfaces:**
- Produces:
  - `enum Platform { Czatnia, PulsLive, Nurt }`, `enum WagerTier { Low, Medium, High }`, `enum SponsorKind { OneOff, Contract }`; `EndingReason` appends `GlobalBan`, `AllPlatformsBanned`.
  - `record PolicyStrike(Platform Platform, int GivenWeek)`
  - `record SponsorOffer(string OfferId, string TemplateId, int OfferedWeek, int ExpiresAfterWeek, long PaymentPln)`
  - `record SponsorContract(string ContractId, string TemplateId, SponsorKind Kind, int AcceptedWeek, int NextDueWeek, int TermEndWeek, long PaymentPln, int Missed)`
  - `record SideBusiness(int OpenedWeek, Fixed Quality)`
  - `record Migration(Platform From, Platform To, Fixed Retention, bool Forced)`
  - `record WagerResult(WagerTier Tier, long StakePln, int Roll, int Threshold, bool Won, long NetPln)`
  - `record StrikeRoll(Platform Platform, int Exposure, int Threshold, int? Roll, bool Strike, bool Banned)`
  - `record MigrationResult(Platform From, Platform To, Fixed Retention, long FollowersBefore, long FollowersAfter)`
  - `CareerState` (appended, all nullable): `Platform? Platform`, `int? PlatformSinceWeek`, `ImmutableArray<Platform>? BannedPlatforms`, `ImmutableArray<PolicyStrike>? Strikes`, `long? FormerFollowers`, `ImmutableArray<SponsorOffer>? Offers`, `ImmutableArray<SponsorContract>? Contracts`, `SideBusiness? SideBusiness`
  - `WeekPlan(MainAction Action, Category Category, Category? TrainTarget = null, EquipmentSlot? UpgradeSlot = null, WagerTier? Wager = null, Platform? TargetPlatform = null)` — the two new members omitted when null.
  - `PendingWeek`: `WagerResult? Wager`, `int? Exposure`, `Migration? Migration`; `DecisionCarry`: `Migration? Migration` (init, omitted when null).
  - `WeekReport` appends `StrikeRoll? Strike = null, WagerResult? Wager = null, MigrationResult? Migration = null`.
  - `CareerState` helpers in `EconomyState.cs` (static class `Economy`): `Platform Current(CareerState s)`, `int SinceWeek(CareerState s)`, `bool IsBanned(CareerState s, Platform p)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Stored_m3a_state_round_trips_without_new_fields()
{
    var s = M3Play.Fresh();
    var started = M3Play.Start(s, MainAction.NoAction);
    foreach (var state in new[] { s, started })
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(state));
        foreach (var name in new[] { "Platform", "PlatformSinceWeek", "BannedPlatforms", "Strikes", "FormerFollowers", "Offers", "Contracts", "SideBusiness", "Wager", "TargetPlatform", "Exposure", "Migration" })
        {
            Assert.DoesNotContain($"\"{name}\"", json, StringComparison.Ordinal);
        }

        Assert.Equal(StateHash.Of(state), StateHash.Of(CanonicalJson.Deserialize(CanonicalJson.Serialize(state))));
    }
}

[Fact]
public void M3b_fields_round_trip()
{
    var s = M3Play.Fresh() with
    {
        Platform = Platform.PulsLive,
        PlatformSinceWeek = 30,
        BannedPlatforms = [Platform.Czatnia],
        Strikes = [new PolicyStrike(Platform.Czatnia, 12), new PolicyStrike(Platform.PulsLive, 31)],
        FormerFollowers = 4_200,
        Offers = [new SponsorOffer("trusted-tea:31", "trusted-tea", 31, 32, 400)],
        Contracts = [new SponsorContract("hype:20", "hype", SponsorKind.Contract, 20, 23, 31, 500, 1)],
        SideBusiness = new SideBusiness(60, Fixed.FromInt(45)),
    };
    var bytes = CanonicalJson.Serialize(s);
    Assert.Equal(bytes, CanonicalJson.Serialize(CanonicalJson.Deserialize(bytes)));
}
```

In `CareerInvariantsTests` (data rows, each a state built from `M3Play.Fresh()`):

| State change | Violation |
| --- | --- |
| three `SponsorContract` entries | `contract-limit` |
| two offers with the same `TemplateId` | `offer-duplicate` |
| `Platform = Nurt`, `BannedPlatforms = [Nurt]`, `PendingDecisions = []` | `banned-platform-unanswered` |
| `SideBusiness = new(60, Fixed.FromInt(101))` | `side-business-quality` |
| `FormerFollowers = -1` | `former-followers-negative` |

and `Banned_platform_with_the_ban_decision_queued_is_valid` (same banned state with `PendingDecisions = ["platform-ban"]` → no violation).

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "CanonicalJsonTests|CareerInvariantsTests"` — Expected: compile errors.

- [ ] **Step 3: Implement**

Declare the records in `Domain/Careers/EconomyState.cs` (`WagerResult`, `StrikeRoll`, `MigrationResult` in `Domain/Simulation/EconomyResults.cs`). Append the `CareerState` members after `UsedOneTime` in the listed order with the `WhenWritingNull` attribute. `WeekPlan` uses `[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` on the two new parameters. `CareerInvariants.Validate` adds, after the existing checks and in this order: `contract-limit` (> 2 contracts), `offer-duplicate`, `banned-platform-unanswered` (active, `Pending is null`, current platform banned, and `PendingDecisions` does not contain `"platform-ban"`), `side-business-quality` (outside 0–100), `former-followers-negative`. `Economy.Current(s) => s.Platform ?? Platform.Czatnia`; `SinceWeek(s) => s.PlatformSinceWeek ?? CareerWeek.First`.

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS (both golden tests unchanged).

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): add hash-neutral state for platforms, sponsors and the side business

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4: Sponsor templates, platform eligibility and the new effect kinds in `events.json`

**Files:**
- Create: `source/backend/Domain/Content/SponsorTemplate.cs`
- Modify: `Domain/Content/EventDefinition.cs`, `ContentSnapshot.cs`, `Domain/Simulation/EventSelector.cs`, `Infrastructure/Content/JsonContentSource.cs`, `EventVersions.cs`
- Test: `Tests/Infrastructure/JsonContentSourceTests.cs`, `EventVersionsTests.cs`, `Tests/Domain/Simulation/EventSelectorTests.cs` (create if absent; else add to `StartWeekTests.cs`)

**Interfaces:**
- Produces:
  - `record SponsorPrerequisites(long MinFollowers, Fixed? MinCredibility, Fixed? MaxDrama, Fixed? MinDrama)`
  - `record SponsorBreach(long CashPln, Fixed Credibility)`
  - `record SponsorTemplate(string Id, int Version, string Name, string Pitch, SponsorKind Kind, long PaymentPln, long PaymentPerThousandFollowersPln, long PaymentCapPln, int? TermWeeks, int? DueEveryWeeks, Category? RequiredCategory, ImmutableArray<Platform> Platforms, SponsorPrerequisites Prerequisites, ImmutableArray<string> IncompatibleWith, SponsorBreach Breach, int Weight, ImmutableArray<string> Tags)` with `const string Gambling = "gambling"`, `const string AdultBusiness = "adult-business"`.
  - `ContentSnapshot` appends `ImmutableArray<SponsorTemplate> Sponsors = default` (normalized to empty like `Tags`).
  - `Eligibility` appends `ImmutableArray<Platform> Platforms = default` (empty = every platform) and `int MinStrikes = 0`.
  - `EffectKind` appends `PolicyStrike` (amount 1–2), `GlobalBan` (amount 0), `SponsorOffer` (`Target` = template ID, amount 0).
  - `EventSelector.IsEligible` also requires the current platform in `Platforms` (when non-empty) and `Economy.ActiveStrikes(s, current, s.LastSettledWeek, b.Policy?.StrikeWeeks ?? 26) >= MinStrikes` (`b` is the selector's `BalanceConstants`); add `static int ActiveStrikes(CareerState s, Platform platform, int settledWeek, int strikeWeeks)` to `Economy` now (count of strikes on `platform` with `GivenWeek + strikeWeeks > settledWeek`). `PolicyRules` does not exist before Task 13; Task 14's `PolicyRules.ActiveStrikes` delegates to `Economy.ActiveStrikes`.
  - JSON: `sponsors[]` objects with camelCase keys `id, version, name, pitch, kind ("oneOff"|"contract"), paymentPln, paymentPerThousandFollowersPln, paymentCapPln, termWeeks, dueEveryWeeks, requiredCategory, platforms, prerequisites {minFollowers, minCredibility, maxDrama, minDrama}, incompatibleWith, breach {cashPln, credibility}, weight, tags`; eligibility `platforms`, `minStrikes`; effect `{"kind":"SponsorOffer","sponsor":"id","amount":0}`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Schema_two_reads_sponsor_templates()
{
    var events = SchemaTwoWith(sponsors: """
        [{ "id": "trusted-tea", "version": 1, "name": "Herbata Spokój", "pitch": "Spokojna marka szuka spokojnego kanału.",
           "kind": "contract", "paymentPln": 300, "paymentPerThousandFollowersPln": 20, "paymentCapPln": 1000,
           "termWeeks": 12, "dueEveryWeeks": 4, "platforms": [], "prerequisites": { "minFollowers": 1000, "minCredibility": 40, "maxDrama": 40 },
           "incompatibleWith": ["gambling"], "breach": { "cashPln": 200, "credibility": -3 }, "weight": 3 }]
        """);
    var c = JsonContentSource.Parse(TestContentFiles.Balance, events, "test");
    var t = Assert.Single(c.Sponsors);
    Assert.Equal((SponsorKind.Contract, 300L, 20L, 1000L, 12, 4), (t.Kind, t.PaymentPln, t.PaymentPerThousandFollowersPln, t.PaymentCapPln, t.TermWeeks, t.DueEveryWeeks));
    Assert.Equal(Fixed.FromInt(40), t.Prerequisites.MaxDrama);
    Assert.Null(t.Prerequisites.MinDrama);
    Assert.Equal(["gambling"], t.IncompatibleWith.ToArray());
    Assert.Equal(new SponsorBreach(200, Fixed.FromInt(-3)), t.Breach);
}

[Fact]
public void Sponsors_are_rejected_in_schema_one()
    => Assert.Throws<InvalidDataException>(() => JsonContentSource.Parse(TestContentFiles.Balance, SchemaOneWithSponsors(), "test"));

[Fact]
public void Eligibility_reads_platforms_and_min_strikes() { /* parse an event with "platforms": ["Nurt"], "minStrikes": 1 → Eligibility.Platforms == [Nurt], MinStrikes == 1 */ }

[Fact]
public void Unknown_platform_in_eligibility_is_rejected() { /* "platforms": ["Kick"] → InvalidDataException mentioning "unknown Platform 'Kick'" */ }
```

`SchemaTwoWith(sponsors)` is a local helper that adds a `sponsors` property to `TestContentFiles.SchemaTwoEvents()`; `SchemaOneWithSponsors()` is a local helper that adds the same property to the schema-1 events from `TestContentFiles`. Write the two comment-only tests above out with real assertions for the values in their comments; a test without an assertion is a review failure. In `EventVersionsTests`: `Changed_sponsor_under_the_same_version_conflicts` (same `trusted-tea@1` with `paymentPln` 301 → conflict `sponsor:trusted-tea@1`). In the selector tests: `Platform_eligibility_follows_the_current_platform` (an event with `Platforms = [Nurt]` is eligible only with `Platform = Nurt`) and `Min_strikes_counts_only_active_strikes_on_the_current_platform` (strike given week 1, `LastSettledWeek` 26 → active; 27 → not).

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter "JsonContentSourceTests|EventVersionsTests|EventSelector"` — Expected: compile errors.

- [ ] **Step 3: Implement**

- Loader: `ReadEvents` returns sponsors too (`Optional(root, "sponsors")` read only for schema 2; present in schema 1 → `Invalid("events.json.sponsors", "is not allowed with schema 1 events")`). Kind strings `oneOff`/`contract` through `ToPascalCase`. `ReadEffect` takes the target from `flag`, `event` or `sponsor`. Eligibility `platforms` with `ParseEnum<Platform>`; `minStrikes` via `OptionalInt`, default 0.
- `EventVersions`: key events as before (`{id}@{version}`) and sponsors as `sponsor:{id}@{version}`; `Describe` unchanged.
- The effect switch in `EffectApplier` (`ToWeek`, `ToCarry`): the three new kinds throw `ArgumentOutOfRangeException` until Tasks 8 and 14 implement them (no content uses them yet).

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(content): read sponsor templates, platform eligibility and the M3-B effect kinds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5: Optional M3-B balance sections

**Files:**
- Create: `source/backend/Domain/Content/EconomyBalance.cs`
- Modify: `Domain/Content/BalanceConstants.cs`, `Infrastructure/Content/JsonContentSource.cs`
- Test: `Tests/Infrastructure/JsonContentSourceTests.cs`

**Interfaces:**
- Produces (all records; `BalanceConstants` members default to `null`):
  - `PlatformProfile(Fixed Reach, Fixed SubYield, Fixed AdYield, Fixed PolicyRisk, CategoryValues<Fixed> CategoryReach)`; `BalanceConstants.Platforms : ImmutableDictionary<Platform, PlatformProfile>?` (JSON `platforms.{Czatnia|PulsLive|Nurt}` with `reach, subYield, adYield, policyRisk, categoryReach {Category: factor}`; omitted categories are 1.00).
  - `MigrationBalance(Fixed EnergyCost, int MinWeeksOnPlatform, int AnnounceWeeks, Fixed AnnounceCredibility, Fixed AnnouncedBonus, Fixed BanPenalty, Fixed RetentionBase, Fixed CredibilityDivisor, Fixed DramaDivisor, Fixed RetentionMin, Fixed RetentionMax, Fixed Jitter)` — JSON `migration` (proposed: 6, 26, 4, −1, 0.10, 0.10, 0.40, 250, 500, 0.20, 0.80, 0.05).
  - `PolicyBalance(int GamblingExposure, int SideBusinessExposure, int HighDramaExposure, Fixed HighDramaFrom, Fixed ChancePerExposure, Fixed ChanceCap, int StrikeWeeks, int StrikesToBan, Fixed DramaDecay)` — JSON `policy` (1, 1, 1, 70, 0.015, 0.15, 26, 3, 1).
  - `SponsorBalance(Fixed OfferBase, Fixed OfferPerMagnitude, Fixed OfferCap, int OfferWeeks, int MaxContracts, Fixed PitchEnergyCost, ChoiceDefinition Pitch, Fixed TerminationCredibility, int MissesToTerminate, ImmutableDictionary<Background, string> StartingLeads)` — JSON `sponsors` (0.08, 0.02, 0.20, 2, 2, 8, pitch choice, −5, 2, `{ "IndustryInsider": "<id>" }`).
  - `WagerBalance(ImmutableDictionary<WagerTier, long> StakePln, ImmutableDictionary<WagerTier, int> Exposure, Fixed MaxShareOfCash, Fixed Chance, Fixed PayoutMultiple, Fixed WinAudience, Fixed LossAudience, Fixed WinDrama, Fixed LossDrama)` — JSON `wagers` (100/500/2000, 1/2/3, 0.25, 0.45, 2, 1.15, 1.05, 2, 3).
  - `SideBusinessBalance(int FromWeek, long MinFollowers, Fixed EnergyCost, Fixed BurnoutGain, Fixed QualityPerWork, Fixed QualityDecay, Fixed IncomePerFollower, Fixed IncomeScale, Fixed CredibilityPerWeek)` — JSON `sideBusiness` (52, 2000, 12, 4, 15, 3, 0.0005, 10, −0.2).
  - `PlatformBanText(string Title, string Text, ImmutableDictionary<Platform, string> Choices)` — JSON `platformBan`.
  - `BalanceConstants.Migration`, `.Policy`, `.Sponsors`, `.Wagers`, `.SideBusiness`, `.PlatformBan`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Balance_without_m3b_sections_leaves_them_null()
{
    var b = new JsonContentSource(TestPaths.Golden("m3-systems.1")).Load().Balance;   // frozen copy from Task 1; content/m3 gains these sections in Task 17
    Assert.Null(b.Platforms);
    Assert.Null(b.Migration);
    Assert.Null(b.Policy);
    Assert.Null(b.Sponsors);
    Assert.Null(b.Wagers);
    Assert.Null(b.SideBusiness);
    Assert.Null(b.PlatformBan);
}

[Fact]
public void Balance_reads_the_m3b_sections()
{
    var c = JsonContentSource.Parse(M3bBalance(), M3bEvents(), "test");   // helpers write the proposed values above
    var b = c.Balance;
    Assert.Equal(Fixed.FromRatio(13, 10), b.Platforms![Platform.PulsLive].PolicyRisk);
    Assert.Equal(Fixed.FromRatio(11, 10), b.Platforms[Platform.PulsLive].CategoryReach[Category.Fps]);
    Assert.Equal(Fixed.One, b.Platforms[Platform.Czatnia].CategoryReach[Category.Chess]);
    Assert.Equal(26, b.Migration!.MinWeeksOnPlatform);
    Assert.Equal(Fixed.FromRatio(15, 1000), b.Policy!.ChancePerExposure);
    Assert.Equal(2000L, b.Wagers!.StakePln[WagerTier.High]);
    Assert.Equal(3, b.Wagers.Exposure[WagerTier.High]);
    Assert.Equal(52, b.SideBusiness!.FromWeek);
    Assert.Equal(Fixed.FromRatio(15, 100), b.Sponsors!.Pitch.BaseChance);
    Assert.Equal("trusted-tea", b.Sponsors.StartingLeads[Background.IndustryInsider]);
    Assert.Equal(3, b.PlatformBan!.Choices.Count);
}

[Fact]
public void A_section_missing_one_value_is_rejected() { /* "policy" without "chanceCap" → InvalidDataException "balance.json.policy.chanceCap: is missing." */ }

[Fact]
public void M3b_sections_are_rejected_with_schema_one_events() { /* M1 events + "wagers" → InvalidDataException */ }
```

Write the two comment-only tests above out with real assertions for the values in their comments; a test without an assertion is a review failure.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter JsonContentSourceTests` — Expected: compile errors.

- [ ] **Step 3: Implement**

Read each section with the existing `Section`/`ReadFixed`/`ReadInt` helpers; every value is required once its section exists. The pitch uses `ReadChoice(…, schema: 2)`. Like `networkCollaborate`, any M3-B section with schema-1 events is rejected. Keep `ReadBalance` readable by moving the seven readers into private methods `ReadPlatforms`, `ReadMigration`, ….

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(content): read the optional M3-B balance sections

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 6: Validator rules for sponsors, bans and the new sections; open PR 1

**Files:**
- Modify: `source/backend/Domain/Content/ContentValidator.cs`
- Create: `source/backend/Tests/Domain/Content/ContentValidatorM3BTests.cs`

**Interfaces:**
- Consumes: Task 4 and Task 5 models.
- Produces: violation codes (exact strings) —
  - `sponsor-duplicate:{id}`, `sponsor-version:{id}` (< 1), `sponsor-payment:{id}` (`paymentPln ≤ 0`, per-thousand < 0, or cap < payment), `sponsor-breach:{id}` (`cashPln < 0`, `cashPln > paymentCapPln`, or credibility outside −20…0), `sponsor-term:{id}` (contract without positive `termWeeks`/`dueEveryWeeks`, `termWeeks % dueEveryWeeks ≠ 0`, or a one-off with either set), `sponsor-no-path:{id}` (prerequisites outside 0–100, `minDrama > maxDrama`, negative `minFollowers`, or `requiredCategory` Gambling with `incompatibleWith` gambling), `sponsor-incompatible:{id}/{value}` (not `gambling`/`adult-business`), `sponsor-weight:{id}`, `text:sponsor:{id}/name|pitch:{problem}`.
  - `global-ban-placement:{event}/{choice}` (a `GlobalBan` outside the `Failure`/`CriticalFailure` of a non-guaranteed choice of a `crisis` event, or in the network or pitch choice).
  - `unknown-sponsor:{owner}/{target}` (`SponsorOffer` target not in `c.Sponsors`).
  - `effect-invalid:{owner}/{choice}` for `PolicyStrike` outside 1–2 or not whole, `GlobalBan`/`SponsorOffer` with an amount ≠ 0 or extra fields; all three only in schema 2.
  - `balance:platforms` (missing platform, factor ≤ 0, risk < 0), `balance:migration` (min > max, either outside 0–1, divisors ≤ 0, jitter < 0), `balance:policy` (cap outside 0–1, strikes to ban < 1, strike weeks < 1), `balance:wagers` (missing tier, stake ≤ 0, chance outside 0–1, payout < 1, share outside (0, 1]), `balance:side-business` (from week outside 1–1040, quality values outside 0–100), `balance:sponsors` (offer chances outside 0–1, base > cap, max contracts < 1, misses < 1), `balance:pitch` (pitch effects other than `StatDelta`, or critical allowed), `missing-lead:{background}/{id}` (unknown template) and `lead-unmet:{background}/{id}` (the background's starting profile fails the template's prerequisites), `text:platformBan/{field}:{problem}` and `balance:platform-ban` (a platform without a choice label), `balance:platform-ban-missing` (`platforms` present without `platformBan` or `migration`).

- [ ] **Step 1: Write the failing tests**

Build each case from a valid M3-B snapshot (`M3BTestContent.Valid()` does not exist yet; use a local `Valid()` that adds the Task 5 sections and one template to `M3TestContent.Valid()` — Task 7 moves it to `M3BTestContent`). One `[Theory]` row per code, for example:

```csharp
[Fact]
public void Global_ban_on_a_live_event_is_rejected()
{
    var c = WithEventEffect("x-tech-failure", choice: 0, failure: new Effect(EffectKind.GlobalBan, null, Fixed.Zero));
    Assert.Contains("global-ban-placement:x-tech-failure/" + c.Events.First(e => e.Id == "x-tech-failure").Choices[0].Id, ContentValidator.Validate(c));
}

[Fact]
public void Global_ban_on_a_crisis_failure_of_a_rolled_choice_is_valid()
{
    var c = WithEventEffect("test-crisis", choice: RolledChoice, failure: new Effect(EffectKind.GlobalBan, null, Fixed.Zero));
    Assert.DoesNotContain(ContentValidator.Validate(c), v => v.StartsWith("global-ban-placement", StringComparison.Ordinal));
}

[Fact]
public void Breach_larger_than_the_payment_cap_is_rejected()
    => Assert.Contains("sponsor-breach:trusted-tea", ContentValidator.Validate(WithSponsor(t => t with { Breach = new SponsorBreach(1_001, Fixed.FromInt(-3)) })));

[Fact]
public void Gambling_sponsor_that_forbids_gambling_has_no_path()
    => Assert.Contains("sponsor-no-path:trusted-tea", ContentValidator.Validate(WithSponsor(t => t with { RequiredCategory = Category.Gambling })));

[Fact]
public void M3_content_and_the_golden_copy_stay_valid()
{
    Assert.Empty(ContentValidator.Validate(new JsonContentSource(TestPaths.ContentM3).Load()));
    Assert.Empty(ContentValidator.Validate(M3GoldenTests.Content));
}
```

If `test-crisis` in `M3TestContent` has no rolled choice, add one there (`"risky"`, base 0.5) in this step; it does not affect the golden tests (they load the golden copy).

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter ContentValidatorM3BTests` — Expected: FAIL.

- [ ] **Step 3: Implement**

Add `ValidateSponsors(c, violations)` and `ValidateEconomyBalance(c, violations)` called from `ValidateSchemaTwo`; extend `IsValidSchemaTwoEffect` with the three kinds; add `side-business` to `EngineFlags` (the engine sets it). Keep each check a small private method; reuse `ValidateTexts` for ICU.

- [ ] **Step 4: Run all tests and open PR 1**

Run: `dotnet test` — Expected: PASS.

```bash
git add source/backend
git commit -m "feat(content): validate sponsor templates, ban effects and the M3-B balance

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

After #81 and #83 merged: `git fetch && git rebase origin/develop`, rerun `dotnet test`, then:

```bash
git push -u origin feature/m3b-1-foundation
gh pr create --base develop --title "M3-B PR 1: economy and risk foundation" --body "$(cat <<'EOF'
Design, decisions (GDD, TD-117) and plan for M3-B; m3-systems.1 frozen by a golden state-hash test; M3Ruleset registered as m3-systems.1 and m3-systems.2 (content/m3 now m3-systems.2, still M3-A rules); hash-neutral state for platforms, strikes, offers, contracts, the side business and migration; sponsor templates, platform eligibility, new effect kinds and balance sections with validator rules. Part of #18, #19 and #20.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI, check SonarQube (`"total":0`), ask the owner to merge, then check `&branch=develop`.

---

# PR 2 — Economy rules

Start from the latest `develop` after PR 1 merged: `git switch -c feature/m3b-2-economy origin/develop`.

### Task 7: M3-B test content and the shape of the new plans

**Files:**
- Create: `source/backend/Tests/Support/M3BTestContent.cs`, `M3BPlay.cs`, `source/backend/Domain/Rulesets/M3B/WagerRules.cs`
- Modify: `source/backend/Domain/Simulation/WeekPlan.cs` (`MainAction`), `Domain/Rulesets/Shared/WeekActions.cs`, `Tests/Domain/Content/ContentValidatorM3BTests.cs` (switch to `M3BTestContent`)
- Test: `source/backend/Tests/Domain/Rulesets/M3B/PlanShapeTests.cs`

**Interfaces:**
- Consumes: Task 3 state, Task 5 sections, `M3TestContent.Valid()`.
- Produces:
  - `MainAction` appends `PitchFulfillSponsor`, `SideBusinessWork`, `PlanPlatformMigration`.
  - `M3BTestContent.Valid()` — `M3TestContent.Valid()` with `RulesetId = "m3-systems.2"`, `Digest = "m3b-test"`, the Task 5 proposed values for all seven sections (`platformBan`: title "Ban na platformie", text "Platforma zamyka ci kanał na stałe. Dokąd przenosisz społeczność?", choices "Przenieś się na Czatnię" / "Przenieś się na PulsLive" / "Przenieś się na Nurt"), `startingLeads = { IndustryInsider: "trusted-tea" }` and four templates:

| Id | Kind | Payment / per 1k / cap | Term / every | Category | Platforms | Prerequisites | Incompatible | Breach | Weight |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | ---: |
| `trusted-tea` | Contract | 300 / 20 / 1,000 | 12 / 4 | — | — | followers ≥ 1,000, credibility ≥ 40, drama ≤ 40 | gambling | 200 PLN, −3 | 3 |
| `hype-drink` | OneOff | 400 / 0 / 400 | — | — | — | drama ≥ 50 | — | 100 PLN, −2 | 2 |
| `chess-board` | OneOff | 250 / 0 / 250 | — | Chess | — | — | adult-business | 100 PLN, −1 | 1 |
| `puls-gear` | Contract | 500 / 0 / 800 | 8 / 4 | — | PulsLive | followers ≥ 5,000 | — | 300 PLN, −4 | 1 |

  The test Insider profile must meet `trusted-tea` (the validator's `lead-unmet` checks it); if `TestContent`'s Insider has fewer than 1,000 followers, lower `minFollowers` to its value and adjust the payment assertions of Task 8 only if the follower count changes them.
  - `M3BPlay` mirrors `M3Play` on `M3BTestContent`: `Engine` (= `M3Play.Engine`), `C`, `Fresh(ulong seed = 3, Background background = Background.DormRoomNewcomer)` (the Task 8 factory overload once it exists), `Start(CareerState s, WeekPlan plan)`, `SettleAll(CareerState s)`, `Week(CareerState s, WeekPlan plan) : (CareerState State, WeekReport Report)`, `Respond(CareerState s, string offerId, bool accept)` (Task 8), `ResolveQueued(CareerState s, string choiceId) : CareerState` (Task 16; answers the first queued decision with that choice, like `M3Play`'s decision helper), `Plan(MainAction a, Category c = Category.Fps, WagerTier? wager = null, Platform? target = null)`.
  - `WeekActions.PlanRejection` reasons for the new plans, after the shared ended / in-progress / ruleset rules and in this order: `unsupported-action` (section missing), `wager-category` (a wager on a non-Gambling plan), `not-enough-energy`, `too-early`, `needs-followers` (side business), `too-soon`, `platform-banned` (migration), `no-sponsor-fits` (Task 9 fills the rule), `needs-cash` (wager, after the existing upgrade checks).
  - `WeekActions.AllPlans(CareerState s, BalanceConstants b)` — the M1/M3-A plans in today's order, then per category `PitchFulfillSponsor` (when `b.Sponsors`), `SideBusinessWork` (when `b.SideBusiness`), `PlanPlatformMigration` per other platform in enum order (when `b.Platforms` and `b.Migration`); for `Gambling` (when `b.Wagers`) each plan is followed by its `Low`, `Medium`, `High` variants.
  - `ApplyAction`: pitch −`PitchEnergyCost` energy; side-business work −`EnergyCost` energy and +`BurnoutGain` burnout (Task 11 adds quality); migration −`EnergyCost` energy (Task 15 adds the announcement).
  - `WagerRules.Spendable(CareerState s, BalanceConstants b, WeekPlan plan) : long` (ruling 6) and `WagerRules.Affordable(long stakePln, long spendablePln, WagerBalance w) : bool` (`spendable > 0 && stake ≤ spendable × MaxShareOfCash`).

- [ ] **Step 1: Write the failing tests**

```csharp
public class PlanShapeTests
{
    private static readonly ContentSnapshot C = M3BPlay.C;

    [Fact]
    public void M3a_content_offers_none_of_the_new_plans()
    {
        var plans = M3Play.Engine.LegalPlans(M3Play.Fresh(), M3Play.C).Value;
        Assert.DoesNotContain(plans, p => p.Plan.Action is MainAction.PitchFulfillSponsor or MainAction.SideBusinessWork or MainAction.PlanPlatformMigration);
        Assert.DoesNotContain(plans, p => p.Plan.Wager is not null);
    }

    [Fact]
    public void Gambling_plans_have_three_wager_variants_and_others_none()
    {
        var plans = M3BPlay.Engine.LegalPlans(M3BPlay.Fresh(), C).Value;
        Assert.Equal(3, plans.Count(p => p.Plan is { Action: MainAction.NoAction, Category: Category.Gambling, Wager: not null }));
        Assert.DoesNotContain(plans, p => p.Plan.Category != Category.Gambling && p.Plan.Wager is not null);
    }

    [Fact]
    public void Migration_targets_exclude_the_current_platform()
    {
        var s = M3BPlay.Fresh() with { Platform = Platform.Nurt };
        var targets = M3BPlay.Engine.LegalPlans(s, C).Value
            .Where(p => p.Plan is { Action: MainAction.PlanPlatformMigration, Category: Category.Fps })
            .Select(p => p.Plan.TargetPlatform);
        Assert.Equal([Platform.Czatnia, Platform.PulsLive], targets);
    }

    [Theory]
    [InlineData(26, "too-soon")]
    [InlineData(27, null)]
    public void Migration_needs_26_weeks_on_the_platform(int week, string? reason)
        => Assert.Equal(reason, Reason(M3BPlay.Fresh() with { Week = new CareerWeek(week), Energy = Fixed.FromInt(50) }, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)));

    [Fact]
    public void Migration_to_a_banned_platform_is_disabled()
        => Assert.Equal("platform-banned", Reason(M3BPlay.Fresh() with { Week = new CareerWeek(40), BannedPlatforms = [Platform.Nurt] }, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)));

    [Theory]
    [InlineData(51, 5_000, "too-early")]
    [InlineData(52, 1_999, "needs-followers")]
    [InlineData(52, 2_000, null)]
    public void Side_business_opens_from_week_52_with_2000_followers(int week, long followers, string? reason)
        => Assert.Equal(reason, Reason(M3BPlay.Fresh() with { Week = new CareerWeek(week), Followers = followers, Energy = Fixed.FromInt(50) }, M3BPlay.Plan(MainAction.SideBusinessWork)));

    [Fact]
    public void Side_business_needs_12_energy()
        => Assert.Equal("not-enough-energy", Reason(M3BPlay.Fresh() with { Week = new CareerWeek(60), Followers = 3_000, Energy = Fixed.FromInt(11) }, M3BPlay.Plan(MainAction.SideBusinessWork)));

    [Fact]
    public void Wager_on_a_non_gambling_plan_is_refused()
    {
        var result = M3BPlay.Engine.StartWeek(M3BPlay.Fresh(), C, M3BPlay.Plan(MainAction.NoAction, Category.Fps, WagerTier.Low), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "wager-category"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Side_business_work_costs_12_energy_and_adds_4_burnout()
    {
        var s = M3BPlay.Fresh() with { Week = new CareerWeek(60), Followers = 3_000, Energy = Fixed.FromInt(50) };
        var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.SideBusinessWork));
        Assert.Equal(Fixed.FromInt(38), started.Energy);
        Assert.Equal(s.Burnout + Fixed.FromInt(4), started.Burnout);
    }

    private static string? Reason(CareerState s, WeekPlan plan)
        => M3BPlay.Engine.LegalPlans(s, C).Value.Single(p => p.Plan == plan).DisabledReason;
}
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter PlanShapeTests` — Expected: compile errors.

- [ ] **Step 3: Implement**

Add the enum values, test content, helpers, plan generation and rejections listed above. `AllPlans` takes the state only to know the current platform; it must yield exactly today's sequence when every M3-B section is null.

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS (both golden tests unchanged).

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): offer pitch, side-business, migration and wager plans when content defines them

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 8: Offers, the Insider lead and `RespondOffer`

**Files:**
- Create: `source/backend/Domain/Rulesets/M3B/SponsorRules.cs`
- Modify: `Domain/Rulesets/IRuleset.cs`, `M1Ruleset.cs`, `M3Ruleset.cs`, `Shared/EffectApplier.cs`, `Shared/LiveEvents.cs`, `Domain/Simulation/WeekEngine.cs`, `Domain/Careers/CareerFactory.cs`, `Tests/Support/LegacyRuleset.cs`, `PausingRuleset.cs`, `M3BPlay.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/OfferTests.cs`

**Interfaces:**
- Consumes: Task 3 `SponsorOffer`/`SponsorContract`, Task 4 `SponsorTemplate`, Task 5 `SponsorBalance`.
- Produces:
  - `SponsorRules.OfferChance(long followers, SponsorBalance b) : Fixed`
  - `SponsorRules.Payment(SponsorTemplate t, long followers) : long`
  - `SponsorRules.Template(ContentSnapshot c, string templateId) : SponsorTemplate`
  - `SponsorRules.Eligible(CareerState s, ContentSnapshot c) : IReadOnlyList<SponsorTemplate>` (prerequisites met now, current platform allowed, no open offer and no active contract for the template, and not `adult-business`-incompatible while the side business is open; until Task 11 adds `SideBusinessRules.IsOpen`, test it inline as `s.SideBusiness is { Quality.Raw: > 0 }` and switch to `IsOpen` in Task 11)
  - `SponsorRules.Open(CareerState s, SponsorTemplate t, int expiresAfterWeek) : CareerState`
  - `SponsorRules.AfterSettlement(CareerState s, ContentSnapshot c, int settledWeek, ref Pcg32 rng) : CareerState` — removes offers with `ExpiresAfterWeek ≤ settledWeek`, then rolls; draws nothing when `c.Balance.Sponsors` is null or no template is eligible.
  - `SponsorRules.AcceptRejection(CareerState s, ContentSnapshot c, SponsorOffer offer) : string?` — `contract-limit`, `same-sponsor`, `prerequisite:followers`, `prerequisite:credibility`, `prerequisite:drama`, `prerequisite:platform`, `prerequisite:adult-business`.
  - `SponsorRules.Lead(CareerState s, ContentSnapshot c) : CareerState`
  - `CareerFactory.Create(ContentSnapshot c, Background background, Difficulty difficulty, PronounSet pronouns, ulong seed) : CareerState`
  - `IRuleset.RespondOffer(CareerState s, ContentSnapshot c, string offerId, bool accept, long expectedVersion) : Result<CareerState>` and `WeekEngine.RespondOffer` with the same signature; M1 and the test rulesets fail with `IllegalAction`/`unknown-offer`.
  - `EffectApplier.ToWeek(CareerState s, ContentSnapshot c, string sourceId, ImmutableArray<Effect> effects, ref PendingWeek pending)` and `EffectApplier.ToCarry(CareerState s, ContentSnapshot c, string sourceId, ImmutableArray<Effect> effects)`; `SponsorOffer` opens the named template's offer for `OfferWeeks` unless the template already has an open offer or an active contract.

- [ ] **Step 1: Write the failing tests**

```csharp
public class OfferTests
{
    private static readonly SponsorBalance B = M3BPlay.C.Balance.Sponsors!;

    [Theory]
    [InlineData(0, 8)]
    [InlineData(9_999, 8)]
    [InlineData(10_000, 10)]
    [InlineData(100_000, 12)]
    [InlineData(10_000_000, 16)]
    [InlineData(1_000_000_000, 20)]
    [InlineData(100_000_000_000, 20)]
    public void Offer_chance_grows_two_points_per_order_of_magnitude(long followers, int percent)
        => Assert.Equal(Fixed.FromRatio(percent, 100), SponsorRules.OfferChance(followers, B));

    [Theory]
    [InlineData(5_500, 400)]
    [InlineData(100_000, 1_000)]
    public void Payment_adds_per_thousand_followers_up_to_the_cap(long followers, long payment)
        => Assert.Equal(payment, SponsorRules.Payment(SponsorRules.Template(M3BPlay.C, "trusted-tea"), followers));

    [Fact]
    public void Insider_starts_with_the_lead_offer_and_others_do_not()
    {
        var insider = M3BPlay.Fresh(background: Background.IndustryInsider);
        var offer = Assert.Single(insider.Offers!.Value);
        Assert.Equal(("trusted-tea:1", "trusted-tea", 2), (offer.OfferId, offer.TemplateId, offer.ExpiresAfterWeek));
        Assert.Null(M3BPlay.Fresh().Offers);
    }

    [Fact]
    public void Accepting_a_contract_schedules_its_installments()
    {
        var s = Insider(week: 10);
        var accepted = M3BPlay.Respond(s, "trusted-tea:10", accept: true);
        var contract = Assert.Single(accepted.Contracts!.Value);
        Assert.Equal(("trusted-tea:10", 13, 21, 0), (contract.ContractId, contract.NextDueWeek, contract.TermEndWeek, contract.Missed));
        Assert.Empty(accepted.Offers!.Value);
        Assert.Equal(s.StateVersion + 1, accepted.StateVersion);
        Assert.Equal(s.Rng, accepted.Rng);
    }

    [Fact]
    public void Declining_removes_the_offer()
        => Assert.Empty(M3BPlay.Respond(Insider(week: 10), "trusted-tea:10", accept: false).Offers!.Value);

    [Fact]
    public void Third_contract_is_refused()
        => Assert.Equal("contract-limit", Refusal(Insider(week: 10) with { Contracts = [Contract("hype-drink"), Contract("puls-gear")] }));

    [Fact]
    public void Second_contract_with_the_same_sponsor_is_refused()
        => Assert.Equal("same-sponsor", Refusal(Insider(week: 10) with { Contracts = [Contract("trusted-tea")] }));

    [Fact]
    public void Lapsed_prerequisite_is_refused_at_acceptance()
        => Assert.Equal("prerequisite:drama", Refusal(Insider(week: 10) with { Drama = Fixed.FromInt(41) }));

    [Fact]
    public void Responding_to_an_expired_offer_is_rejected()
    {
        var s = M3BPlay.Fresh(background: Background.IndustryInsider);
        s = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction)).State;
        s = M3BPlay.Week(M3BPlay.AnswerDecisions(s), M3BPlay.Plan(MainAction.NoAction)).State;   // week 2 settled: the lead expired
        var result = M3BPlay.Engine.RespondOffer(s, M3BPlay.C, "trusted-tea:1", true, s.StateVersion);
        Assert.Equal((DomainErrorCode.IllegalAction, "unknown-offer"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Respond_offer_during_a_week_is_refused()
    {
        var started = M3BPlay.Start(M3BPlay.Fresh(background: Background.IndustryInsider), M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal("week-in-progress", M3BPlay.Engine.RespondOffer(started, M3BPlay.C, "trusted-tea:1", true, started.StateVersion).Error.Detail);
    }

    [Fact]
    public void Offer_roll_draws_nothing_without_an_eligible_template()
    {
        var s = M3BPlay.Fresh() with { Followers = 0 };
        var c = M3BPlay.C with { Sponsors = [.. M3BPlay.C.Sponsors.Where(t => t.Id != "chess-board")] };
        var rng = s.Rng;
        Assert.Equal(s, SponsorRules.AfterSettlement(s, c, 1, ref rng));
        Assert.Equal(s.Rng, rng);
    }

    [Fact]
    public void An_offer_opened_after_settlement_waits_two_weeks()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var (after, _) = M3BPlay.Week(M3BPlay.Fresh(seed) with { Followers = 20_000 }, M3BPlay.Plan(MainAction.NoAction));
            if (after.Offers is [var offer])
            {
                Assert.Equal((2, 3), (offer.OfferedWeek, offer.ExpiresAfterWeek));
                return;
            }
        }

        Assert.Fail("No seed in 1–200 opened an offer.");
    }

    [Fact]
    public void Sponsor_offer_effect_opens_the_named_template()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh(), M3BPlay.C, "test", [new Effect(EffectKind.SponsorOffer, null, Fixed.Zero, Target: "hype-drink")]);
        Assert.Equal(("hype-drink", 400L), (s.Offers!.Value[0].TemplateId, s.Offers.Value[0].PaymentPln));
    }

    [Fact]
    public void M1_career_has_no_offers_to_answer()
        => Assert.Equal("unknown-offer", TestWorld.Engine.RespondOffer(TestCareers.Fresh(), TestWorld.Content, "x", true, 0).Error.Detail);

    private static CareerState Insider(int week)
        => M3BPlay.Fresh(background: Background.IndustryInsider) with
        {
            Week = new CareerWeek(week),
            Drama = Fixed.FromInt(15),
            Offers = [new SponsorOffer($"trusted-tea:{week}", "trusted-tea", week, week + 1, 400)],
        };

    private static SponsorContract Contract(string templateId) => new($"{templateId}:1", templateId, SponsorKind.Contract, 1, 4, 12, 300, 0);

    private static string Refusal(CareerState s) => M3BPlay.Engine.RespondOffer(s, M3BPlay.C, s.Offers!.Value[0].OfferId, true, s.StateVersion).Error.Detail;
}
```

Add `M3BPlay.AnswerDecisions(CareerState s)` (answers every queued decision with its first available choice).

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter OfferTests` — Expected: compile errors.

- [ ] **Step 3: Implement**

- The roll is `rng.NextInt(1, 100) ≤ round(100 × chance)`; on a hit a weight ticket `rng.NextInt(1, Σweight)` picks the template by cumulative weight in content order (as `DecisionQueue` does). Every offer (roll, pitch, effect, lead) opens with `OfferedWeek = s.Week` and `ExpiresAfterWeek = s.Week + OfferWeeks − 1`, so it can be answered before two weeks are played (an offer opened after settling week 1 waits through weeks 2 and 3 and is removed when week 3 settles).
- Accept: `OneOff` → `NextDueWeek = TermEndWeek = s.Week`; `Contract` → `NextDueWeek = s.Week + DueEveryWeeks − 1`, `TermEndWeek = s.Week + TermWeeks − 1`; `ContractId = OfferId`; payment from the offer.
- `M3Ruleset.RespondOffer`: `RuleSteps.CommandRejection` → `week-in-progress` when `Pending` is set → `unknown-offer` → `AcceptRejection` (accept only) → new state with `StateVersion + 1`; draws nothing.
- `M3Ruleset.SettleWeek`: after `Close` and only without an ending, call `SponsorRules.AfterSettlement` before `DecisionQueue.Open` on the same `rng`.
- `CareerFactory.Create(ContentSnapshot c, …)` = the existing overload with `c.RulesetId`, `c.Digest`, `c.Profiles[background]`, then `SponsorRules.Lead` (offered week 1, expires after week 2).
- Pass `c` at every `EffectApplier` call site (`LiveEvents.Resolve`, `M3Ruleset.ResolveDecision`, `RollNetwork`).

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS (golden tests: no sponsors → no draw).

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): open, expire and answer sponsor offers

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 9: Contracts, one-off deals, breaches and `PitchFulfillSponsor`

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3B/SponsorRules.cs`, `Domain/Rulesets/M3Ruleset.cs`, `Shared/WeekActions.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/ContractTests.cs`

**Interfaces:**
- Consumes: Task 8.
- Produces:
  - `SponsorRules.DueThisWeek(CareerState s) : ImmutableArray<SponsorContract>` (kind `Contract`, `NextDueWeek == s.Week`)
  - `SponsorRules.Settle(CareerState s, PendingWeek p, ContentSnapshot c) : (CareerState State, PendingWeek Pending)` — called by `M3Ruleset.SettleWeek` before `WeekSettlement.Core`; adds `income:sponsor` and `cost:sponsor-penalty` lines (detail = template ID) and `stat:Credibility` lines (detail `sponsor`).
  - `enum SponsorConsequenceKind { MissedInstallment, Breach }`, `record SponsorConsequence(string ContractId, string TemplateId, SponsorConsequenceKind Kind, long PenaltyPln, Fixed Credibility, bool Terminates)`, `SponsorRules.Consequences(CareerState s, ContentSnapshot c, WeekPlan plan) : ImmutableArray<SponsorConsequence>`.
  - `SponsorRules.Incompatible(CareerState s, WeekPlan plan) : ImmutableHashSet<string>` (`gambling` for a Gambling plan, `adult-business` for a `SideBusinessWork` plan or while the side business is open, so the preview shows the breach that settlement applies; the open check is inline `s.SideBusiness is { Quality.Raw: > 0 }` until Task 11 adds `SideBusinessRules.IsOpen`).
  - Pitch: `M3Ruleset.PresentAction` returns `OddsCalculator.For(s, b.Sponsors.Pitch, b, plan.Category)` for a pitch plan with nothing due, otherwise keeps today's result; `StartWeek` rolls it (`PendingWeek.ActionRoll` with event ID `"pitch"`), applies its effects and, on `Success`/`CriticalSuccess`, opens one eligible offer by weight (draw after the roll). `no-sponsor-fits` when nothing is due and `Eligible` is empty.

Rules at the settlement of week `w`, per active contract in list order, at most one money line per contract:
- *Contract due in `w`:* fulfilled when the plan is `PitchFulfillSponsor` and the required category (if any) matches → `income:sponsor` = payment, `NextDueWeek += DueEveryWeeks`, removed after its last installment. Week off (`sys:rest-only` active in `w`) → `NextDueWeek += 1`, `TermEndWeek += 1`, no line. Otherwise missed → `cost:sponsor-penalty = min(breach.cashPln, payment)`, `breach.credibility`, `Missed += 1`, `NextDueWeek += DueEveryWeeks`; at `MissesToTerminate` it ends with a further `TerminationCredibility` (−5) as a separate `stat:Credibility` line (detail `sponsor`), so a terminating miss shows two lines: the breach credibility (−3) and the termination (−5).
- *One-off due in `w`:* paid when the category matches and no listed incompatibility happened; otherwise breached (penalty and credibility as above); removed either way.
- *Incompatibility:* a contract whose `IncompatibleWith` meets `Incompatible(s, plan)` is breached once (penalty and credibility) and ends; this replaces its installment line that week.

- [ ] **Step 1: Write the failing tests**

```csharp
public class ContractTests
{
    // trusted-tea accepted in week 10: due 13, 17, 21; payment 400; breach 200 PLN, −3.
    private static CareerState Due(int week = 13, int missed = 0, int nextDue = 13)
        => M3BPlay.Fresh() with
        {
            Week = new CareerWeek(week), LastSettledWeek = week - 1, Energy = Fixed.FromInt(60), Credibility = Fixed.FromInt(50),
            Contracts = [new SponsorContract("trusted-tea:10", "trusted-tea", SponsorKind.Contract, 10, nextDue, 21, 400, missed)],
        };

    [Fact]
    public void Pitch_in_a_due_week_fulfils_and_pays_the_installment()
    {
        var (after, report) = M3BPlay.Week(Due(), M3BPlay.Plan(MainAction.PitchFulfillSponsor));
        Assert.Contains(report.Income, l => l is { Code: "income:sponsor", AmountPln: 400, Detail: "trusted-tea" });
        Assert.Equal(17, after.Contracts!.Value[0].NextDueWeek);
        Assert.Null(report.ActionRoll);
    }

    [Fact]
    public void Missed_installment_costs_the_capped_penalty_and_credibility()
    {
        var (after, report) = M3BPlay.Week(Due(), M3BPlay.Plan(MainAction.NoAction));
        Assert.Contains(report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 200, Detail: "trusted-tea" });
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Credibility" && l.Detail == "sponsor" && l.Delta == Fixed.FromInt(-3));
        Assert.Equal((1, 17), (after.Contracts!.Value[0].Missed, after.Contracts.Value[0].NextDueWeek));
    }

    [Fact]
    public void Second_miss_terminates_with_five_more_credibility()
    {
        var (after, report) = M3BPlay.Week(Due(missed: 1), M3BPlay.Plan(MainAction.NoAction));
        Assert.Empty(after.Contracts!.Value);
        var lines = report.StatChanges.Where(l => l.Code == "stat:Credibility" && l.Detail == "sponsor").Select(l => l.Delta).ToArray();
        Assert.Equal([Fixed.FromInt(-3), Fixed.FromInt(-5)], lines);   // breach credibility, then the termination line
    }

    [Fact]
    public void Gambling_stream_breaches_a_gambling_incompatible_contract_once()
    {
        var (after, report) = M3BPlay.Week(Due(week: 15, nextDue: 17), M3BPlay.Plan(MainAction.NoAction, Category.Gambling));
        Assert.Single(report.Costs, l => l.Code == "cost:sponsor-penalty");
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Week_off_postpones_a_due_installment_without_penalty()
    {
        var (after, report) = M3BPlay.Week(CareerFlags.Set(Due(), "sys:rest-only", 1), M3BPlay.Plan(MainAction.Rest));
        Assert.DoesNotContain(report.Costs, l => l.Code == "cost:sponsor-penalty");
        var contract = after.Contracts!.Value[0];
        Assert.Equal((14, 22, 0), (contract.NextDueWeek, contract.TermEndWeek, contract.Missed));
    }

    [Fact]
    public void One_off_pays_in_the_next_stream_when_the_category_matches()
    {
        var (after, report) = M3BPlay.Week(ChessDeal(), M3BPlay.Plan(MainAction.NoAction, Category.Chess));
        Assert.Contains(report.Income, l => l is { Code: "income:sponsor", AmountPln: 250 });
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void One_off_in_the_wrong_category_is_a_breach()
    {
        var (after, report) = M3BPlay.Week(ChessDeal(), M3BPlay.Plan(MainAction.NoAction, Category.Fps));
        Assert.Contains(report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 100 });
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Plan_preview_shows_the_penalty_of_a_missed_installment()
    {
        var consequence = Assert.Single(SponsorRules.Consequences(Due(), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction)));
        Assert.Equal((SponsorConsequenceKind.MissedInstallment, 200L, Fixed.FromInt(-3), false), (consequence.Kind, consequence.PenaltyPln, consequence.Credibility, consequence.Terminates));
        Assert.Empty(SponsorRules.Consequences(Due(), M3BPlay.C, M3BPlay.Plan(MainAction.PitchFulfillSponsor)));
    }

    [Fact]
    public void Pitch_without_a_due_task_rolls_with_visible_odds()
    {
        var s = Due(week: 15, nextDue: 17) with { Followers = 2_000 };
        Assert.Equal(35, M3BPlay.Engine.PresentAction(s, M3BPlay.C, M3BPlay.Plan(MainAction.PitchFulfillSponsor)).Value!.Threshold);
        var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.PitchFulfillSponsor));
        Assert.Equal(("pitch", 35), (started.Pending!.ActionRoll!.EventId, started.Pending.ActionRoll.Threshold));
    }

    [Fact]
    public void Successful_pitch_opens_an_offer_at_once()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var s = Due(week: 15, nextDue: 17) with { Followers = 2_000, Rng = Pcg32.Create(seed, CareerFactory.CareerStream) };
            var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.PitchFulfillSponsor));
            if (started.Pending!.ActionRoll!.Outcome == RollOutcome.Success)
            {
                Assert.Equal(16, Assert.Single(started.Offers!.Value).ExpiresAfterWeek);
                return;
            }
        }

        Assert.Fail("No seed in 1–200 gave a successful pitch.");
    }

    [Fact]
    public void Pitch_with_nothing_due_and_no_fitting_sponsor_is_disabled()
    {
        var s = M3BPlay.Fresh() with { Followers = 0, Energy = Fixed.FromInt(60) };
        var c = M3BPlay.C with { Sponsors = [.. M3BPlay.C.Sponsors.Where(t => t.Id != "chess-board")] };
        Assert.Equal("no-sponsor-fits", M3BPlay.Engine.LegalPlans(s, c).Value.Single(p => p.Plan == M3BPlay.Plan(MainAction.PitchFulfillSponsor)).DisabledReason);
    }

    private static CareerState ChessDeal()
        => M3BPlay.Fresh() with { Week = new CareerWeek(5), LastSettledWeek = 4, Contracts = [new SponsorContract("chess-board:5", "chess-board", SponsorKind.OneOff, 5, 5, 5, 250, 0)] };
}
```

The `Sponsors` override in the last-but-one test uses a snapshot whose digest no longer matches; build it with `M3BTestContent.Valid() with { Sponsors = …, Digest = "m3b-test" }` and a career created on it, or call `WeekActions.LegalPlans` directly if the engine rejects the digest.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet test --filter ContractTests` — Expected: FAIL.

- [ ] **Step 3: Implement**

Implement the rules above in `SponsorRules.Settle` and call it from `M3Ruleset.SettleWeek` after `ApplyCategoryReputation` and before `Core`, adding its stat lines the way the category step does. The pitch roll shares a private `RollAction(CareerState s, ContentSnapshot c, WeekPlan plan, ActionApplied applied, string eventId, ChoiceDefinition choice, ref Pcg32 rng)` with `RollNetwork`, so the network path keeps its draw order.

- [ ] **Step 4: Run all tests**

Run: `dotnet test` — Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): settle sponsor installments, one-off deals and breaches; pitch for offers

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 10: The wager

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3B/WagerRules.cs`, `Domain/Rulesets/M3Ruleset.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/WagerTests.cs`

**Interfaces:**
- Consumes: Task 7 `Spendable`/`Affordable`, Task 3 `WagerResult`.
- Produces:
  - `record WagerTerms(WagerTier Tier, long StakePln, Fixed Chance, long PayoutPln, long LossPln)`; `WagerRules.Terms(WagerTier tier, WagerBalance w) : WagerTerms` (payout = stake × `PayoutMultiple`, loss = stake).
  - `WagerRules.Roll(CareerState s, WeekPlan plan, WagerBalance w, ref PendingWeek pending, ref Pcg32 rng) : CareerState` — one `NextInt(1, 100)` against `round(100 × chance)` (45); win: `income:wager` = payout − stake, audience × `WinAudience`, drama + `WinDrama`; loss: `cost:wager` = stake, audience × `LossAudience`, drama + `LossDrama`; stat line detail `wager`; sets `PendingWeek.Wager`.
  - `WeekReport.Wager` from the pending week.

- [ ] **Step 1: Write the failing tests**

```csharp
public class WagerTests
{
    private static CareerState Cash(long pln) => M3BPlay.Fresh() with { CashPln = pln, Energy = Fixed.FromInt(60) };

    [Theory]
    [InlineData(WagerTier.Low, 399, "needs-cash")]
    [InlineData(WagerTier.Low, 400, null)]
    [InlineData(WagerTier.Medium, 2_000, null)]
    [InlineData(WagerTier.High, 7_999, "needs-cash")]
    [InlineData(WagerTier.Low, 0, "needs-cash")]
    public void Stake_must_fit_a_quarter_of_positive_cash(WagerTier tier, long cash, string? reason)
        => Assert.Equal(reason, Reason(Cash(cash), M3BPlay.Plan(MainAction.NoAction, Category.Gambling, tier)));

    [Fact]
    public void Upgrade_in_the_same_plan_leaves_too_little_cash_for_the_stake()
    {
        var plan = new WeekPlan(MainAction.ImproveProduction, Category.Gambling, UpgradeSlot: EquipmentSlot.Microphone, Wager: WagerTier.Low);
        Assert.Equal("needs-cash", Reason(Cash(800), plan));   // 800 − 600 = 200 spendable < 400
        Assert.Null(Reason(Cash(1_000), plan));               // 400 spendable
    }

    [Fact]
    public void Negative_decision_carry_counts_against_the_stake()
        => Assert.Equal("needs-cash", Reason(Cash(400) with { Carry = DecisionCarry.Empty with { CashPln = -1 } }, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low)));

    [Fact]
    public void Start_week_refuses_a_stake_the_state_no_longer_covers()
    {
        var result = M3BPlay.Engine.StartWeek(Cash(399), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low), 0);
        Assert.Equal((DomainErrorCode.IllegalAction, "needs-cash"), (result.Error.Code, result.Error.Detail));
    }

    [Fact]
    public void Terms_show_stake_chance_payout_and_loss()
        => Assert.Equal(new WagerTerms(WagerTier.Medium, 500, Fixed.FromRatio(45, 100), 1_000, 500), WagerRules.Terms(WagerTier.Medium, M3BPlay.C.Balance.Wagers!));

    [Fact]
    public void Won_wager_pays_the_net_win_and_reconciles()
    {
        var (before, after, report) = UntilWager(won: true);
        Assert.Contains(report.Income, l => l is { Code: "income:wager", AmountPln: 100 });
        Assert.Equal((true, 45), (report.Wager!.Won, report.Wager.Threshold));
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Drama" && l.Detail == "wager" && l.Delta == Fixed.FromInt(2));
        Assert.Equal(after.CashPln - before.CashPln, report.CashDelta);
    }

    [Fact]
    public void Lost_wager_costs_the_stake_and_three_drama()
    {
        var (_, _, report) = UntilWager(won: false);
        Assert.Contains(report.Costs, l => l is { Code: "cost:wager", AmountPln: 100 });
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Drama" && l.Detail == "wager" && l.Delta == Fixed.FromInt(3));
    }

    [Theory]
    [InlineData(true, 115)]
    [InlineData(false, 105)]
    public void Wager_raises_the_audience_multiplier(bool won, int hundredths)
    {
        var started = UntilStarted(won);
        Assert.Equal(Fixed.FromRatio(hundredths, 100), started.Pending!.AudienceMultiplier);
    }

    [Fact]
    public void Expected_value_of_a_stake_is_minus_ten_percent()
    {
        var w = M3BPlay.C.Balance.Wagers!;
        var terms = WagerRules.Terms(WagerTier.High, w);
        Assert.Equal(Fixed.FromInt(-200), (w.Chance * Fixed.FromInt(terms.PayoutPln - terms.StakePln)) - ((Fixed.One - w.Chance) * Fixed.FromInt(terms.LossPln)));
    }
}
```

`UntilStarted(won)` searches seeds 1–200 for a started `Low` Gambling `NoAction` week (cash 4,000, no events with audience effects: use a career whose selected events are resolved before checking, or assert on the multiplier right after `StartWeek` before any event resolves); `UntilWager(won)` settles that week with `M3BPlay.SettleAll` and returns the state before, after and the report; `Reason` as in Task 7.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter WagerTests`; Expected: FAIL.

- [ ] **Step 3: Implement**

`WagerRules.Roll` runs in `M3Ruleset.StartWeek` after the action roll (network or pitch) and before event selection, only when `plan.Wager` is set. `M3Ruleset.SettleWeek` puts `pending.Wager` into the report.

- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): add the fictional-PLN wager to gambling streams

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 11: The side business

**Files:**
- Create: `source/backend/Domain/Rulesets/M3B/SideBusinessRules.cs`
- Modify: `Domain/Rulesets/Shared/WeekActions.cs`, `Domain/Rulesets/M3Ruleset.cs`, `Domain/Rulesets/M3B/SponsorRules.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/SideBusinessTests.cs`

**Interfaces:**
- Produces:
  - `SideBusinessRules.Work(CareerState s, SideBusinessBalance b) : CareerState` — the first use sets `SideBusiness(s.Week, 0)` and the permanent flag `side-business`; each use adds `QualityPerWork` (15), capped at 100.
  - `SideBusinessRules.Income(long followers, Fixed quality, SideBusinessBalance b) : long` = `round(followers × IncomePerFollower × quality / 100 × IncomeScale)`.
  - `SideBusinessRules.Settle(CareerState s, PendingWeek p, SideBusinessBalance? b) : (CareerState, PendingWeek)` — while quality > 0: `income:side-business` from the quality at settlement start, `CredibilityPerWeek` (−0.2, stat line detail `side-business`), then quality − `QualityDecay` (3), not below 0. Called after `SponsorRules.Settle`, before `Core`.
  - `SideBusinessRules.IsOpen(CareerState s) : bool` (quality > 0), used by sponsor eligibility and incompatibility and by policy exposure (Task 14).

- [ ] **Step 1: Write the failing tests**

```csharp
public class SideBusinessTests
{
    private static CareerState Ready => M3BPlay.Fresh() with { Week = new CareerWeek(52), LastSettledWeek = 51, Followers = 10_000, Energy = Fixed.FromInt(60) };

    [Fact]
    public void First_work_opens_the_business()
    {
        var (after, _) = M3BPlay.Week(Ready, M3BPlay.Plan(MainAction.SideBusinessWork));
        Assert.Equal(52, after.SideBusiness!.OpenedWeek);
        Assert.Equal(Fixed.FromInt(12), after.SideBusiness.Quality);   // +15 at start, −3 decay at settlement
        Assert.True(CareerFlags.IsActive(after, "side-business"));
    }

    [Theory]
    [InlineData(10_000, 30, 15)]
    [InlineData(20_000, 100, 100)]
    [InlineData(20_000, 0, 0)]
    public void Income_scales_with_followers_and_quality(long followers, int quality, long income)
        => Assert.Equal(income, SideBusinessRules.Income(followers, Fixed.FromInt(quality), M3BPlay.C.Balance.SideBusiness!));

    [Fact]
    public void Open_business_pays_costs_credibility_and_decays()
    {
        var (after, report) = M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(30)) }, M3BPlay.Plan(MainAction.NoAction));
        Assert.Contains(report.Income, l => l is { Code: "income:side-business", AmountPln: 15 });
        Assert.Contains(report.StatChanges, l => l.Code == "stat:Credibility" && l.Detail == "side-business" && l.Delta == Fixed.FromRatio(-2, 10));
        Assert.Equal(Fixed.FromInt(27), after.SideBusiness!.Quality);
    }

    [Fact]
    public void Dormant_business_pays_and_costs_nothing()
    {
        var (after, report) = M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.Zero) }, M3BPlay.Plan(MainAction.NoAction));
        Assert.DoesNotContain(report.Income, l => l.Code == "income:side-business");
        Assert.DoesNotContain(report.StatChanges, l => l.Detail == "side-business");
        Assert.Equal(Fixed.Zero, after.SideBusiness!.Quality);
    }

    [Fact]
    public void Quality_caps_at_100()
        => Assert.Equal(Fixed.FromInt(97), M3BPlay.Week(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(95)) }, M3BPlay.Plan(MainAction.SideBusinessWork)).State.SideBusiness!.Quality);

    [Fact]
    public void Opening_breaches_an_adult_business_incompatible_deal()
    {
        var s = Ready with { Contracts = [new SponsorContract("chess-board:52", "chess-board", SponsorKind.OneOff, 52, 53, 53, 250, 0)] };
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.SideBusinessWork));
        Assert.Contains(report.Costs, l => l is { Code: "cost:sponsor-penalty", AmountPln: 100, Detail: "chess-board" });
        Assert.Empty(after.Contracts!.Value);
    }

    [Fact]
    public void Open_business_stops_adult_incompatible_offers()
        => Assert.DoesNotContain(SponsorRules.Eligible(Ready with { SideBusiness = new SideBusiness(40, Fixed.FromInt(30)) }, M3BPlay.C), t => t.Id == "chess-board");
}
```

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter SideBusinessTests`; Expected: FAIL.
- [ ] **Step 3: Implement** — as listed; `WeekActions.ApplyAction` calls `SideBusinessRules.Work` for `SideBusinessWork`.
- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): add the optional text-only side business

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 12: Plan preview and reconciliation over the new lines; open PR 2

**Files:**
- Create: `source/backend/Domain/Rulesets/M3B/PlanPreview.cs`
- Modify: `Domain/Rulesets/IRuleset.cs`, `M1Ruleset.cs`, `M3Ruleset.cs`, `Domain/Simulation/WeekEngine.cs`, `Tests/Support/LegacyRuleset.cs`, `PausingRuleset.cs`, `M3BPlay.cs`, `docs/gameplay-systems.md`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/PlanPreviewTests.cs`, `Tests/Domain/Simulation/ReconciliationProperties.cs`

**Interfaces:**
- Produces:
  - `record PlanPreview(WagerTerms? Wager, ImmutableArray<SponsorConsequence> Sponsors)` with `static PlanPreview None` (built with `ImmutableArray<SponsorConsequence>.Empty`; every rule that returns "no sponsors" also uses `.Empty`, never `default`, so `ImmutableArray` reference equality holds); Task 14 appends `PolicyExposure? Policy = null`, Task 15 appends `MigrationEstimate? Migration = null`.
  - `IRuleset.PresentPlan(CareerState s, ContentSnapshot c, WeekPlan plan) : PlanPreview`; `WeekEngine.PresentPlan(...) : Result<PlanPreview>`; M1 and the test rulesets return `PlanPreview.None`.
  - `M3BPlay.RandomWeek(CareerState s, ref Pcg32 rng) : (CareerState BeforeStart, CareerState After, WeekReport Report)` — answers decisions, responds to every offer at random, picks a random enabled plan, resolves events at random, settles; `BeforeStart` is the state after decisions and offers.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Preview_lists_the_wager_terms_and_the_sponsor_breach()
{
    var s = M3BPlay.Fresh() with { CashPln = 4_000, Contracts = [new SponsorContract("trusted-tea:1", "trusted-tea", SponsorKind.Contract, 1, 4, 12, 300, 0)] };
    var preview = M3BPlay.Engine.PresentPlan(s, M3BPlay.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Medium)).Value;
    Assert.Equal(500, preview.Wager!.StakePln);
    Assert.Equal(SponsorConsequenceKind.Breach, Assert.Single(preview.Sponsors).Kind);
}

[Fact]
public void M3a_preview_is_empty()
{
    var preview = M3Play.Engine.PresentPlan(M3Play.Fresh(), M3Play.C, new WeekPlan(MainAction.NoAction, Category.Fps)).Value;
    Assert.Null(preview.Wager);
    Assert.True(preview.Sponsors.IsDefaultOrEmpty);
    Assert.Null(preview.Policy);
    Assert.Null(preview.Migration);
}
```

In `ReconciliationProperties`:

```csharp
[Property(MaxTest = 100)]
public bool M3b_cash_delta_reconciles_with_report_lines(PositiveInt seed, NonNegativeInt weeks)
{
    var s = M3BPlay.Fresh((ulong)seed.Get, Background.IndustryInsider) with { Followers = 5_000, CashPln = 6_000 };
    var rng = Pcg32.Create((ulong)seed.Get, 11);
    for (var i = 0; i < weeks.Get % 80 && s.Status == CareerStatus.Active; i++)
    {
        var (beforeStart, after, report) = M3BPlay.RandomWeek(s, ref rng);
        var lines = report.Income.Sum(l => l.AmountPln!.Value) - report.Costs.Sum(l => l.AmountPln!.Value);
        if (lines != report.CashDelta || report.CashDelta != after.CashPln - beforeStart.CashPln)
        {
            return false;
        }

        s = after;
    }

    return true;
}
```

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter "PlanPreviewTests|ReconciliationProperties"`; Expected: compile errors.
- [ ] **Step 3: Implement** — `M3Ruleset.PresentPlan` = `new PlanPreview(plan.Wager is { } t && c.Balance.Wagers is { } w ? WagerRules.Terms(t, w) : null, SponsorRules.Consequences(s, c, plan))`.
- [ ] **Step 4: Run all tests, update docs and open PR 2**

Run: `dotnet test` — Expected: PASS (both golden tests).

Add to `docs/gameplay-systems.md` a section `## 12. M3-B Ruleset (m3-systems.2)` with "Status: in progress; rules from the [M3-B design](superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md); Task 19 records the tuned numbers", and at the end of §6 "M3-B rules for sponsors, the wager and the side business: §12."

```bash
git add source/backend docs
git commit -m "feat(domain): preview plan risks and reconcile the M3-B cash lines

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/m3b-2-economy
gh pr create --base develop --title "M3-B PR 2: sponsors, wager and side business rules" --body "$(cat <<'EOF'
Sponsor offers (after-settlement roll, Insider lead, respondOffer), one-off deals and contracts with installments, missed-task penalties, incompatibility breaches and week-off postponement; PitchFulfillSponsor; the fictional-PLN wager on gambling plans; the optional side business; a plan preview with wager terms and sponsor consequences; every new cash movement is a report line and reconciles (property test). All rules are inert on m3-systems.1 (golden tests). Part of #18 and #20.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI, check SonarQube (`"total":0`), ask the owner to merge, then check `&branch=develop`.

---

# PR 3 — Platform rules, content and simulation

Start from the latest `develop` after PR 2 merged: `git switch -c feature/m3b-3-platforms origin/develop`.

### Task 13: Platform factors and drama decay

**Files:**
- Create: `source/backend/Domain/Rulesets/M3B/PlatformRules.cs`, `PolicyRules.cs`
- Modify: `source/backend/Domain/Rulesets/M3Ruleset.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/PlatformFactorTests.cs`

**Interfaces:**
- Produces:
  - `PlatformRules.Effective(BalanceConstants b, CareerState s, Category category) : BalanceConstants` — `b` itself when `b.Platforms` is null; otherwise `b with { PlatformReach = profile.Reach × profile.CategoryReach[category], PlatformSubYield = profile.SubYield, PlatformAdYield = profile.AdYield }` for `Economy.Current(s)`.
  - `PolicyRules.AddedDrama(IEnumerable<ReportLine> lines) : bool` (any `stat:Drama` line with a positive delta).
  - `PolicyRules.DecayDrama(SettledCore core, IEnumerable<ReportLine> weekLines, PolicyBalance? p) : SettledCore` — when `p` is set and the week added no drama, `Drama −= DramaDecay` (not below 0) with a `stat:Drama` line, detail `decay`.
  - `M3Ruleset.SettleWeek` passes `c with { Balance = PlatformRules.Effective(c.Balance, s, pending.Plan.Category) }` to `WeekSettlement.Core` and calls `DecayDrama` after `ApplyFollowersPercent` with the pending lines, the core stat lines and the carried lines.

- [ ] **Step 1: Write the failing tests**

```csharp
public class PlatformFactorTests
{
    [Fact]
    public void Platform_factors_replace_the_scalar_ones()
    {
        var b = PlatformRules.Effective(M3BPlay.C.Balance, M3BPlay.Fresh() with { Platform = Platform.PulsLive }, Category.Fps);
        Assert.Equal((Fixed.FromRatio(132, 100), Fixed.FromRatio(85, 100), Fixed.FromRatio(110, 100)), (b.PlatformReach, b.PlatformSubYield, b.PlatformAdYield));
        var nurtChess = PlatformRules.Effective(M3BPlay.C.Balance, M3BPlay.Fresh() with { Platform = Platform.Nurt }, Category.Chess);
        Assert.Equal(Fixed.FromRatio(102, 100), nurtChess.PlatformReach);   // 0.85 × 1.20
    }

    [Fact]
    public void Balance_without_platforms_is_returned_unchanged()
        => Assert.Same(M3Play.C.Balance, PlatformRules.Effective(M3Play.C.Balance, M3Play.Fresh(), Category.Fps));

    [Fact]
    public void Puls_live_draws_more_viewers_than_czatnia_from_the_same_state()
    {
        var czatnia = M3BPlay.Fresh(9) with { Followers = 5_000 };
        var (_, onCzatnia) = M3BPlay.Week(czatnia, M3BPlay.Plan(MainAction.NoAction));
        var (_, onPuls) = M3BPlay.Week(czatnia with { Platform = Platform.PulsLive }, M3BPlay.Plan(MainAction.NoAction));
        Assert.True(onPuls.LiveViewers > onCzatnia.LiveViewers);
    }

    [Fact]
    public void A_week_without_new_drama_decays_drama_by_one()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var (after, report) = M3BPlay.Week(M3BPlay.Fresh(seed) with { Drama = Fixed.FromInt(30) }, M3BPlay.Plan(MainAction.NoAction, Category.JustChatting));
            var decay = report.StatChanges.Any(l => l is { Code: "stat:Drama", Detail: "decay" });
            Assert.Equal(!PolicyRules.AddedDrama(report.StatChanges.Where(l => l.Detail != "decay")), decay);
        }
    }

    [Fact]
    public void M3a_content_never_decays_drama()
    {
        var (_, report) = M3Play.SettleAll(M3Play.Start(M3Play.Fresh() with { Drama = Fixed.FromInt(30) }, MainAction.NoAction, Category.JustChatting));
        Assert.DoesNotContain(report.StatChanges, l => l.Detail == "decay");
    }
}
```

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter PlatformFactorTests`; Expected: compile errors.
- [ ] **Step 3: Implement** — as listed.
- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS (golden tests: `Platforms` and `Policy` are null).
- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): apply platform factors and drama decay in m3-systems.2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 14: Policy exposure, strikes, bans and the two endings

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3B/PolicyRules.cs`, `PlatformRules.cs`, `PlanPreview.cs`, `Domain/Rulesets/M3Ruleset.cs`, `M3/DecisionQueue.cs`, `Shared/WeekSettlement.cs`, `Shared/EffectApplier.cs`, `Domain/Careers/EconomyState.cs` (`Economy.ActiveStrikes` already takes `strikeWeeks` since Task 4; no change), `Domain/Simulation/EventSelector.cs` (no change: Task 4 already passes `Policy?.StrikeWeeks ?? 26`)
- Test: `source/backend/Tests/Domain/Rulesets/M3B/PolicyTests.cs`, `Tests/Domain/Simulation/EndingTests.cs`

**Interfaces:**
- Produces:
  - `record PolicyExposure(int Exposure, Fixed Chance, int Threshold)`; `PlanPreview` appends `PolicyExposure? Policy = null` (null when `b.Policy` is null).
  - `PolicyRules.Exposure(CareerState s, WeekPlan plan, ContentSnapshot c) : int` — Gambling plan `GamblingExposure`, wager `Wagers.Exposure[tier]`, `SideBusinessRules.IsOpen(s)` (after this plan's work, if any) `SideBusinessExposure`, `s.Drama ≥ HighDramaFrom` `HighDramaExposure`.
  - `PolicyRules.Chance(int exposure, Fixed policyRisk, PolicyBalance p) : Fixed` = `min(ChanceCap, exposure × ChancePerExposure × policyRisk)`.
  - `PolicyRules.ActiveStrikes(CareerState s, Platform platform, int settledWeek, PolicyBalance p) : int` — delegates to `Economy.ActiveStrikes(s, platform, settledWeek, p.StrikeWeeks)` (Task 4), so the selector and the policy rules share one count.
  - `PolicyRules.Settle(CareerState s, int exposure, ContentSnapshot c, int settledWeek, ref Pcg32 rng) : (CareerState State, StrikeRoll? Roll)` — drops expired strikes; with exposure > 0 draws one `NextInt(1, 100)` against `round(100 × chance)`; a hit adds `PolicyStrike(current, settledWeek)`; reaching `StrikesToBan` active strikes adds the current platform to `BannedPlatforms` (`StrikeRoll.Banned`). No draw with exposure 0 or without `b.Policy`.
  - `PlatformRules.Accessible(CareerState s) : ImmutableArray<Platform>` (not banned, not current).
  - `PolicyRules.Ending(CareerState s) : EndingReason?` — `GlobalBan` when `sys:global-ban` is active; `AllPlatformsBanned` when the current platform is banned and `Accessible` is empty; otherwise null.
  - `WeekSettlement.Close(CareerState s, CareerState state, ContentSnapshot c, EndingReason? ruleEnding = null)` — order insolvency, `ruleEnding`, completion.
  - `PlatformRules.BanDecisionId = "platform-ban"`; `DecisionQueue.Open` puts it first when the current platform is banned and `Accessible` is not empty (it counts toward the two decisions; the recovery crisis comes second).
  - `EffectApplier`: `PolicyStrike` adds `round(amount)` strikes on the current platform given in `s.Week`; `GlobalBan` sets the permanent flag `sys:global-ban`.
  - `PendingWeek.Exposure` set at `StartWeek` from `PolicyRules.Exposure(s, plan, c)` (state before the action); `WeekReport.Strike` filled.

- [ ] **Step 1: Write the failing tests**

```csharp
public class PolicyTests
{
    [Fact]
    public void Exposure_adds_gambling_wager_side_business_and_high_drama()
    {
        var s = M3BPlay.Fresh() with { Drama = Fixed.FromInt(70), SideBusiness = new SideBusiness(52, Fixed.FromInt(30)), CashPln = 4_000 };
        Assert.Equal(5, PolicyRules.Exposure(s, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Medium), M3BPlay.C));
        Assert.Equal(0, PolicyRules.Exposure(M3BPlay.Fresh(), M3BPlay.Plan(MainAction.NoAction), M3BPlay.C));
    }

    [Theory]
    [InlineData(3, 130, 6)]    // 3 × 1.5 % × 1.30 = 5.85 %
    [InlineData(1, 80, 1)]     // 1.2 %
    [InlineData(10, 100, 15)]  // capped
    public void Strike_chance_scales_with_exposure_and_platform_risk(int exposure, int riskHundredths, int threshold)
    {
        var chance = PolicyRules.Chance(exposure, Fixed.FromRatio(riskHundredths, 100), M3BPlay.C.Balance.Policy!);
        Assert.Equal(threshold, (int)(chance * Fixed.Hundred).RoundToLong());
    }

    [Fact]
    public void Plan_preview_shows_exposure_and_chance()
    {
        var preview = M3BPlay.Engine.PresentPlan(M3BPlay.Fresh() with { CashPln = 4_000 }, M3BPlay.C, M3BPlay.Plan(MainAction.NoAction, Category.Gambling, WagerTier.Low)).Value;
        Assert.Equal(new PolicyExposure(2, Fixed.FromRatio(3, 100), 3), preview.Policy);
    }

    [Fact]
    public void A_safe_week_draws_no_strike_roll()
    {
        var s = M3BPlay.Start(M3BPlay.Fresh(), M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal(0, s.Pending!.Exposure);
        Assert.Null(M3BPlay.SettleAll(s).Report.Strike);
    }

    [Fact]
    public void Strikes_expire_after_26_weeks()
    {
        var s = M3BPlay.Fresh() with { Strikes = [new PolicyStrike(Platform.Czatnia, 1)] };
        Assert.Equal(1, PolicyRules.ActiveStrikes(s, Platform.Czatnia, 26, M3BPlay.C.Balance.Policy!));
        Assert.Equal(0, PolicyRules.ActiveStrikes(s, Platform.Czatnia, 27, M3BPlay.C.Balance.Policy!));
    }

    [Fact]
    public void Third_active_strike_bans_the_platform_and_queues_the_ban_decision()
    {
        var (after, report) = UntilStrike(M3BPlay.Fresh() with { Strikes = [new(Platform.Czatnia, 1), new(Platform.Czatnia, 1)], CashPln = 4_000 });
        Assert.True(report.Strike!.Banned);
        Assert.Equal([Platform.Czatnia], after.BannedPlatforms!.Value);
        Assert.Equal("platform-ban", after.PendingDecisions!.Value[0]);
        Assert.Empty(CareerInvariants.Validate(after));
    }

    [Fact]
    public void Ban_without_an_accessible_platform_ends_the_career()
    {
        var s = M3BPlay.Fresh() with { BannedPlatforms = [Platform.PulsLive, Platform.Nurt], Strikes = [new(Platform.Czatnia, 1), new(Platform.Czatnia, 1)], CashPln = 4_000 };
        var (after, report) = UntilStrike(s);
        Assert.Equal((EndingReason.AllPlatformsBanned, CareerStatus.Ended), (report.Ending, after.Status));
        Assert.True(after.FinalScore!.WealthPoints > 0);
    }

    [Fact]
    public void Policy_strike_effect_adds_strikes_on_the_current_platform()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh() with { Platform = Platform.Nurt, Week = new CareerWeek(40) }, M3BPlay.C, "test", [new Effect(EffectKind.PolicyStrike, null, Fixed.FromInt(2))]);
        Assert.Equal([new PolicyStrike(Platform.Nurt, 40), new PolicyStrike(Platform.Nurt, 40)], s.Strikes!.Value);
    }

    [Fact]
    public void Global_ban_ends_the_career_at_the_next_settlement()
    {
        var s = EffectApplier.ToCarry(M3BPlay.Fresh(), M3BPlay.C, "test", [new Effect(EffectKind.GlobalBan, null, Fixed.Zero)]);
        Assert.Equal(CareerStatus.Active, s.Status);
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.NoAction));
        Assert.Equal((EndingReason.GlobalBan, CareerStatus.Ended), (report.Ending, after.Status));
    }

    [Fact]
    public void Insolvency_wins_over_a_global_ban() { /* sys:global-ban active and CashPln -2_000 → Insolvency */ }

    [Fact]
    public void Min_strikes_eligibility_reads_the_policy_strike_weeks() { /* event with MinStrikes 1, strike given week 1, LastSettledWeek 26 → eligible; 27 → not */ }
}
```

`UntilStrike(s)` sets `CashPln = 9_000` (so the `High` stake is legal) and searches seeds 1–400 for a Gambling `High` `NoAction` week (exposure 4, threshold 6 on Czatnia) whose report has a strike. In `EndingTests` add `New_endings_keep_their_wealth_points` (`ScoreCalculator.Compute(…, EndingReason.GlobalBan, …)` equals the `Completed` breakdown for the same inputs).

Write the comment-only tests of this step out with real assertions for the values in their comments; a test without an assertion is a review failure.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter "PolicyTests|EndingTests"`; Expected: FAIL.

- [ ] **Step 3: Implement**

Settlement order in `M3Ruleset.SettleWeek` from this task on: short stream → category reputation → `SponsorRules.Settle` → `SideBusinessRules.Settle` → `Core` (effective balance) → `ApplyFollowersPercent` → migration (Task 15) → `DecayDrama` → `PolicyRules.Settle` (strike draw) → `Close(…, PolicyRules.Ending(state))` → without an ending: `SponsorRules.AfterSettlement`, `DecisionQueue.Open`. `Exposure` is frozen at `StartWeek`; `Settle` reads `pending.Exposure ?? 0`.

- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): roll policy strikes, ban platforms and end careers on global or total bans

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 15: Platform migration

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3B/PlatformRules.cs`, `PlanPreview.cs`, `Domain/Rulesets/M3Ruleset.cs`, `Shared/WeekActions.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/MigrationTests.cs`

**Interfaces:**
- Produces:
  - `record MigrationEstimate(Platform From, Platform To, bool Executes, Fixed RetentionMin, Fixed RetentionMax, long FollowersMin, long FollowersMax, PlatformProfile Target)`; `PlanPreview` appends `MigrationEstimate? Migration = null`.
  - `PlatformRules.AnnouncedFlag(Platform target) : string` = `"migration-announced:{target}"`.
  - `PlatformRules.Retention(CareerState s, Platform target, bool forced, MigrationBalance m) : Fixed` = `clamp(RetentionMin, RetentionMax, RetentionBase + credibility / CredibilityDivisor − drama / DramaDivisor + (announced(target) ? AnnouncedBonus : 0) − (forced ? BanPenalty : 0))`.
  - `PlatformRules.Estimate(CareerState s, ContentSnapshot c, Platform target, bool forced) : MigrationEstimate` (range `r ± Jitter`, clamped; follower bounds `round(followers × bound)`; `Executes` = forced or announced).
  - `PlatformRules.Draw(Fixed retention, MigrationBalance m, ref Pcg32 rng) : Fixed` — `NextFixed(r − Jitter, r + Jitter)` clamped to the min/max.
  - `PlatformRules.ApplyMigration(SettledCore core, Migration? migration, ContentSnapshot c, int settledWeek) : (SettledCore Core, MigrationResult? Result)` — `after = round(followers × retention)`, lost added to `FormerFollowers` and to the week's lost/net followers, line `followers:migration` (delta = retention, detail = target), `Platform = To`, `PlatformSinceWeek = settledWeek + 1` (not for a forced move, already switched), tied contracts end (`EndTiedContracts`).
  - `PlatformRules.EndTiedContracts(CareerState s, ContentSnapshot c, Platform to) : CareerState`.
  - `PlanPlatformMigration` at `StartWeek`: not announced → set the announce flag for `AnnounceWeeks` (4) and `AnnounceCredibility` (−1, stat line detail `migration`), no move; announced → draw the retention and set `PendingWeek.Migration = new Migration(current, target, drawn, Forced: false)`, clear the flag. `WeekReport.Migration` filled at settlement.

- [ ] **Step 1: Write the failing tests**

```csharp
public class MigrationTests
{
    private static readonly MigrationBalance M = M3BPlay.C.Balance.Migration!;

    private static CareerState Ready(long followers = 10_000)
        => M3BPlay.Fresh() with { Week = new CareerWeek(30), LastSettledWeek = 29, Followers = followers, Credibility = Fixed.FromInt(50), Drama = Fixed.Zero, Energy = Fixed.FromInt(60) };

    [Theory]
    [InlineData(50, 0, false, false, 60)]
    [InlineData(100, 0, true, false, 80)]
    [InlineData(0, 100, false, true, 20)]
    [InlineData(50, 0, false, true, 50)]
    public void Retention_follows_the_clamped_formula(int credibility, int drama, bool announced, bool forced, int percent)
    {
        var s = Ready() with { Credibility = Fixed.FromInt(credibility), Drama = Fixed.FromInt(drama) };
        s = announced ? CareerFlags.Set(s, PlatformRules.AnnouncedFlag(Platform.Nurt), 4) : s;
        Assert.Equal(Fixed.FromRatio(percent, 100), PlatformRules.Retention(s, Platform.Nurt, forced, M));
    }

    [Fact]
    public void First_use_announces_without_moving()
    {
        var (after, report) = M3BPlay.Week(Ready(), M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.True(CareerFlags.IsActive(after, "migration-announced:Nurt"));
        Assert.Equal(Platform.Czatnia, Economy.Current(after));
        Assert.Contains(report.StatChanges, l => l is { Code: "stat:Credibility", Detail: "migration" } && l.Delta == Fixed.FromInt(-1));
        Assert.Null(report.Migration);
    }

    [Fact]
    public void Second_use_moves_with_the_shown_range_and_reports_the_exact_count()
    {
        var s = CareerFlags.Set(Ready(), PlatformRules.AnnouncedFlag(Platform.Nurt), 4);
        var estimate = M3BPlay.Engine.PresentPlan(s, M3BPlay.C, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt)).Value.Migration!;
        Assert.Equal((true, Fixed.FromRatio(65, 100), Fixed.FromRatio(75, 100)), (estimate.Executes, estimate.RetentionMin, estimate.RetentionMax));

        var started = M3BPlay.Start(s, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        var (after, report) = M3BPlay.SettleAll(started);
        var moved = report.Migration!;
        Assert.InRange(moved.Retention, estimate.RetentionMin, estimate.RetentionMax);
        Assert.Equal((Platform.Czatnia, Platform.Nurt, 31), (moved.From, moved.To, after.PlatformSinceWeek));
        Assert.Equal(moved.FollowersAfter, after.Followers);
        Assert.Equal(moved.FollowersBefore - moved.FollowersAfter, after.FormerFollowers);
        Assert.Contains(report.StatChanges, l => l is { Code: "followers:migration", Detail: "Nurt" });
        Assert.Equal(report.FollowersGained - report.FollowersLost, report.FollowersNet);
    }

    [Fact]
    public void Migration_with_zero_followers_moves_and_reports_zero()
    {
        var (after, report) = M3BPlay.Week(CareerFlags.Set(Ready(followers: 0), PlatformRules.AnnouncedFlag(Platform.PulsLive), 4), M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.PulsLive));
        // The week's audience step runs before the move, so a career that starts with 0 followers can gain some that week;
        // the retention applies to exactly that gained count.
        var moved = report.Migration!;
        Assert.Equal(report.FollowersGained, moved.FollowersBefore);
        Assert.Equal(moved.FollowersAfter, after.Followers);
        Assert.Equal(moved.FollowersBefore - moved.FollowersAfter, after.FormerFollowers ?? 0);
        Assert.Equal(Platform.PulsLive, Economy.Current(after));
    }

    [Fact]
    public void Move_ends_contracts_tied_to_the_old_platform_without_penalty()
    {
        var s = CareerFlags.Set(Ready() with { Platform = Platform.PulsLive, Contracts = [new SponsorContract("puls-gear:28", "puls-gear", SponsorKind.Contract, 28, 31, 35, 500, 0)] }, PlatformRules.AnnouncedFlag(Platform.Nurt), 4);
        var (after, report) = M3BPlay.Week(s, M3BPlay.Plan(MainAction.PlanPlatformMigration, target: Platform.Nurt));
        Assert.Empty(after.Contracts!.Value);
        Assert.DoesNotContain(report.Costs, l => l.Code == "cost:sponsor-penalty");
    }

    [Fact]
    public void Strikes_on_the_old_platform_stay() { /* Strikes [Czatnia, 20] survive a move to Nurt */ }

    [Fact]
    public void Announcement_expires_after_four_weeks() { /* flag set in week 30 for 4 weeks is inactive in week 34 → the next use announces again */ }
}
```

Write the comment-only tests of this step out with real assertions for the values in their comments; a test without an assertion is a review failure.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter MigrationTests`; Expected: FAIL.
- [ ] **Step 3: Implement** — as listed; the migration draw happens in `StartWeek` after the wager roll and before event selection; `ApplyMigration` runs after `ApplyFollowersPercent`.
- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): announce and execute platform migrations with shown retention

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 16: The `platform-ban` decision

**Files:**
- Modify: `source/backend/Domain/Rulesets/M3B/PlatformRules.cs`, `Domain/Rulesets/M3Ruleset.cs`, `Domain/Simulation/EventPresentation.cs`, `DecisionCarry.cs`
- Test: `source/backend/Tests/Domain/Rulesets/M3B/PlatformBanTests.cs`

**Interfaces:**
- Produces:
  - `EventPresentation(string EventId, ImmutableArray<ChoiceOdds> Choices, EventDefinition? Definition = null)`; `ChoiceOdds` appends `MigrationEstimate? Migration = null`.
  - `PlatformRules.BanDecision(CareerState s, ContentSnapshot c) : EventDefinition` — ID `platform-ban`, phase `Crisis`, `Title`/`TextPl` from `platformBan`, one guaranteed choice `move-{Platform}` per `Accessible(s)` in enum order, labels from `platformBan.choices`, no effects.
  - `M3Ruleset.PresentDecision` for `platform-ban`: guaranteed odds per choice with `Migration = PlatformRules.Estimate(s, c, target, forced: true)` and `Definition` set.
  - `M3Ruleset.ResolveDecision` for `platform-ban`: `UnknownChoice` for a choice not offered; otherwise draws the retention, sets `Platform = target`, `PlatformSinceWeek = s.Week`, ends tied contracts, sets `Carry.Migration = new Migration(from, target, drawn, Forced: true)`, adds the roll `RollResult("platform-ban", choiceId, null, 100, [], Guaranteed)` to the carry and pops the queue; no cooldown, no one-time use.
  - `StartWeek` moves `Carry.Migration` into `PendingWeek.Migration`; `Retire`'s `FoldCarry` applies a carried migration to the followers and `FormerFollowers`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class PlatformBanTests
{
    private static CareerState Banned(params Platform[] alsoBanned)
        => M3BPlay.Fresh() with
        {
            Week = new CareerWeek(40), LastSettledWeek = 39, Followers = 10_000, Credibility = Fixed.FromInt(50), Drama = Fixed.Zero,
            BannedPlatforms = [Platform.Czatnia, .. alsoBanned],
            PendingDecisions = ["platform-ban"],
        };

    [Fact]
    public void Ban_decision_offers_each_accessible_platform_with_its_retention()
    {
        var presented = M3BPlay.Engine.PresentDecision(Banned(), M3BPlay.C).Value;
        Assert.Equal(["move-PulsLive", "move-Nurt"], presented.Choices.Select(o => o.ChoiceId));
        Assert.All(presented.Choices, o => Assert.Equal((true, Fixed.FromRatio(45, 100), Fixed.FromRatio(55, 100)), (o.Guaranteed, o.Migration!.RetentionMin, o.Migration.RetentionMax)));
        Assert.Equal("Ban na platformie", presented.Definition!.Title);
    }

    [Fact]
    public void Ban_decision_skips_a_banned_target()
        => Assert.Equal(["move-Nurt"], M3BPlay.Engine.PresentDecision(Banned(Platform.PulsLive), M3BPlay.C).Value.Choices.Select(o => o.ChoiceId));

    [Fact]
    public void Moving_switches_now_and_applies_retention_at_the_next_settlement()
    {
        var moved = M3BPlay.ResolveQueued(Banned(), "move-Nurt");
        Assert.Equal((Platform.Nurt, 40, true), (Economy.Current(moved), moved.PlatformSinceWeek, moved.Carry!.Migration!.Forced));
        Assert.Equal(10_000, moved.Followers);
        var (after, report) = M3BPlay.Week(moved, M3BPlay.Plan(MainAction.NoAction));
        Assert.InRange(report.Migration!.Retention, Fixed.FromRatio(45, 100), Fixed.FromRatio(55, 100));
        Assert.Contains(report.DecisionRolls, r => r is { EventId: "platform-ban", Outcome: RollOutcome.Guaranteed });
        Assert.Equal(report.Migration.FollowersBefore - report.Migration.FollowersAfter, after.FormerFollowers);
    }

    [Fact]
    public void Ban_opens_before_a_queued_recovery_crisis()
    {
        var s = M3BPlay.Fresh() with { Energy = Fixed.Zero, Strikes = [new(Platform.Czatnia, 1), new(Platform.Czatnia, 1)], CashPln = 9_000 };
        var (after, _) = UntilBan(s);   // a strike week that also ends at energy 0
        Assert.Equal(["platform-ban", "burnout-recovery"], after.PendingDecisions!.Value);
    }

    [Fact]
    public void Ban_move_ends_tied_contracts_without_penalty()
    {
        var s = Banned() with { Platform = Platform.PulsLive, BannedPlatforms = [Platform.PulsLive], Contracts = [new SponsorContract("puls-gear:30", "puls-gear", SponsorKind.Contract, 30, 33, 37, 500, 0)] };
        var moved = M3BPlay.ResolveQueued(s, "move-Nurt");
        Assert.Empty(moved.Contracts!.Value);
        Assert.DoesNotContain(M3BPlay.Week(moved, M3BPlay.Plan(MainAction.NoAction)).Report.Costs, l => l.Code == "cost:sponsor-penalty");
    }

    [Fact]
    public void Offer_for_the_banned_platform_is_refused_after_the_move()
    {
        var s = Banned() with { Platform = Platform.PulsLive, BannedPlatforms = [Platform.PulsLive], Offers = [new SponsorOffer("puls-gear:40", "puls-gear", 40, 41, 500)] };
        var moved = M3BPlay.ResolveQueued(s, "move-Nurt");
        Assert.Single(moved.Offers!.Value);
        Assert.Equal("prerequisite:platform", M3BPlay.Engine.RespondOffer(moved, M3BPlay.C, "puls-gear:40", true, moved.StateVersion).Error.Detail);
    }

    [Fact]
    public void Ban_blocks_the_week_until_answered()
    {
        Assert.True(M3BPlay.Engine.RequiresPlayerDecision(Banned(), M3BPlay.C).Value);
        Assert.Equal(DomainErrorCode.DecisionPending, M3BPlay.Engine.StartWeek(Banned(), M3BPlay.C, M3BPlay.Plan(MainAction.NoAction), 0).Error.Code);
    }

    [Fact]
    public void Retire_after_a_ban_move_applies_the_retention()
    {
        var moved = M3BPlay.ResolveQueued(Banned(), "move-Nurt");
        var retired = M3BPlay.Engine.Retire(moved, M3BPlay.C, moved.StateVersion).Value;
        Assert.Equal(10_000 - retired.Followers, retired.FormerFollowers);
        Assert.InRange(retired.Followers, 4_500, 5_500);
    }

    [Fact]
    public void Unknown_move_is_rejected()
        => Assert.Equal(DomainErrorCode.UnknownChoice, M3BPlay.Engine.ResolveDecision(Banned(Platform.PulsLive), M3BPlay.C, "move-PulsLive", 0).Error.Code);
}
```

`UntilBan(s)` sets `Energy` to the test balance's `StreamEnergyCost` (so the stream leaves energy 0 and the recovery crisis is due) and searches seeds 1–400 for a Gambling `High` `NoAction` week whose report bans the platform.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter PlatformBanTests`; Expected: FAIL.
- [ ] **Step 3: Implement** — as listed; `M3Ruleset.NextDecision` returns `PlatformRules.BanDecision` for `platform-ban` instead of `RuleSteps.FindEvent`.
- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(domain): ask for a platform move after a ban with the retention shown

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 17: `content/m3` — balance sections, 12 sponsor templates and 15 events

**Files:**
- Modify: `content/m3/balance.json`, `content/m3/events.json`, `source/backend/Tests/Content/M3ContentTests.cs`
- Modify: `docs/templates/content-authoring-log.csv` (one row per new template and event with the authoring time)

**Interfaces:**
- Consumes: every M3-B schema and section.
- Produces: content that loads valid as `m3-systems.2`; template and event IDs used by Task 18 tests and the Playwright journey (`bank-skarbonka` is the Insider lead).

Balance: the seven sections with the Task 5 values; `platforms` as spec §5.1 (PulsLive `categoryReach` Fps 1.10, Moba 1.10, Chess 0.90; Nurt Chess 1.20, JustChatting 1.10, Gambling 0.80); `sponsors.pitch` label "Rozpisz ofertę dla sponsorów", success text "Ktoś odpisał. W skrzynce czeka propozycja.", failure text "Cisza w skrzynce. Marki mają dziś inne plany."; `startingLeads: { "IndustryInsider": "bank-skarbonka" }`; `platformBan` texts as in `M3BTestContent`.

Sponsor templates (agent-written Polish names and pitches, satirical, fictional brands; `tags` include `sponsor`, plus `gambling` or `adult-business` where relevant):

| Group | Id | Kind | Payment / per 1k / cap | Term / every | Category / platforms | Prerequisites | Incompatible | Breach | Weight |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | ---: |
| Trusted | `herbata-spokoj` | contract | 250 / 15 / 1,200 | 12 / 4 | — | followers 1,000, credibility ≥ 55, drama ≤ 30 | gambling, adult-business | 200, −3 | 3 |
| Trusted | `bank-skarbonka` | one-off | 300 / 10 / 900 | — | — | followers 1,000, credibility ≥ 45, drama ≤ 35 | gambling | 150, −2 | 3 |
| Trusted | `ksiegarnia-strona` | contract | 200 / 10 / 800 | 8 / 4 | — | followers 2,000, credibility ≥ 50, drama ≤ 40 | adult-business | 150, −2 | 2 |
| Trusted | `meble-ergo` | one-off | 600 / 20 / 2,500 | — | — | followers 10,000, credibility ≥ 60, drama ≤ 25 | gambling, adult-business | 300, −3 | 2 |
| Sensational | `turbo-kisiel` | one-off | 400 / 25 / 2,000 | — | — | followers 2,000, drama ≥ 50 | — | 200, −1 | 3 |
| Sensational | `energol-max` | contract | 500 / 30 / 3,000 | 12 / 4 | — | followers 5,000, drama ≥ 60 | — | 400, −2 | 2 |
| Sensational | `apka-plotki` | contract | 350 / 20 / 1,800 | 8 / 2 | — | followers 3,000, drama ≥ 55 | — | 300, −2 | 2 |
| Category | `szachy-pionek` | contract | 250 / 15 / 1,000 | 12 / 4 | Chess | followers 1,000, credibility ≥ 50 | gambling | 150, −2 | 2 |
| Category | `myszka-celownik` | one-off | 350 / 15 / 1,500 | — | Fps | followers 1,500 | — | 200, −1 | 2 |
| Category | `zetonik-wirtualny` | one-off | 450 / 20 / 1,800 | — | Gambling | followers 2,000, drama ≥ 30 | — | 250, −2 | 1 |
| Platform | `puls-boost` | contract | 400 / 25 / 2,000 | 8 / 4 | PulsLive | followers 3,000 | — | 300, −3 | 2 |
| Platform | `nurt-patron` | contract | 300 / 20 / 1,500 | 12 / 4 | Nurt | followers 1,500, credibility ≥ 55 | — | 200, −3 | 2 |

`zetonik-wirtualny` advertises an in-game chip skin, never a casino, deposit or payout.

Events (schema 2, version 1, ICU, three gendered forms where the player is addressed; cooldown 13 unless stated):

| Id | Phase | Eligibility | Tags | Mechanics |
| --- | --- | --- | --- | --- |
| `policy-copyright-strike` | live | weeks 27–1040 | — | rolled "zostaw muzykę" (base 0.6) fails with `PolicyStrike` 1; guaranteed "wycisz" −quality |
| `policy-gambling-promo` | live | Gambling | gambling | guaranteed "odmów" credibility +2; rolled "pokaż kod" (base 0.5) fails with `PolicyStrike` 1, success `CashDelta` +150 |
| `policy-privacy-leak` | crisis | weeks 105–1040, `minStrikes` 1, cooldown 104 | privacy-incident | guaranteed "przyznaj się i przeproś" credibility −3, drama +2; rolled "zaprzeczaj" (base 0.9, credibility modifier) failure `GlobalBan`; the label states the risk ("ryzyko trwałej blokady wszędzie") |
| `puls-discovery` | live | platforms PulsLive | — | rolled showcase → `AudienceMultiplier` 1.3 / drama +2 |
| `nurt-community` | live | platforms Nurt | community | guaranteed patron night → credibility +2, `CashDelta` +100 |
| `policy-first-strike` | crisis | `minStrikes` 1, cooldown 26 | — | guaranteed "przeczytaj regulamin" drama −2; guaranteed "zignoruj" credibility −1 |
| `side-first-catalog` | milestone | one-time, requires `side-business` | adult-business | pricing choice → credibility ±, `CashDelta` |
| `side-workload` | crisis | requires `side-business`, burnout ≥ 40 | adult-business | cut hours (burnout −5) or push (energy −5, cash +200) |
| `side-leak-rumour` | live | requires `side-business` | adult-business, privacy-incident | synthetic-data rumour; rolled denial or guaranteed calm statement |
| `side-fan-request` | live | requires `side-business` | adult-business | decline politely (credibility +1) or upsell (cash +100, drama +2) |
| `wager-chat-hype` | live | Gambling | gambling | chat urges a bigger stake: guaranteed "trzymam limit" credibility +1 |
| `wager-streak` | live | Gambling | gambling | a lucky streak: rolled "graj dalej" audience ×1.1 / drama +3 |
| `wager-mod-limit` | live | Gambling | gambling | a moderator proposes a weekly limit: accept (drama −2) |
| `sponsor-agency-call` | milestone | one-time, followers ≥ 5,000 | sponsor | guaranteed "umów spotkanie" → `SponsorOffer` `ksiegarnia-strona`; guaranteed "nie teraz" |
| `sponsor-brand-rep` | live | weeks 27–1040 | sponsor | rolled pitch on stream (base 0.5) success `SponsorOffer` `turbo-kisiel`, failure drama +1 |

No gambling text mentions real money, deposits, top-ups, payouts or purchases.

- [ ] **Step 1: Write the failing content tests**

```csharp
[Fact]
public void Has_the_m3b_set()
{
    Assert.Equal(12, C.Sponsors.Length);
    Assert.Equal(4, C.Sponsors.Count(t => t.Prerequisites.MaxDrama is not null && t.RequiredCategory is null && t.Platforms.IsEmpty));
    Assert.Equal(3, C.Sponsors.Count(t => t.Prerequisites.MinDrama is { } d && d >= Fixed.FromInt(50)));
    Assert.Equal(3, C.Sponsors.Count(t => t.RequiredCategory is not null));
    Assert.Equal(2, C.Sponsors.Count(t => !t.Platforms.IsEmpty));
    Assert.Equal(57, C.Events.Length);   // 42 existing M3-A events + the 15 new ones
    Assert.Equal(4, C.Events.Count(e => e.Eligibility.RequiresFlags.Contains("side-business")));
}

[Fact]
public void Global_ban_appears_only_in_the_privacy_crisis()
{
    var owners = C.Events.Where(e => e.Choices.Any(ch => (ch.Outcomes.Failure).Any(f => f.Kind == EffectKind.GlobalBan))).Select(e => e.Id);
    Assert.Equal(["policy-privacy-leak"], owners);
}

[Fact]
public void Gambling_text_never_offers_real_money()
{
    string[] forbidden = ["prawdziw", "wypłac", "wypłat", "doładuj", "doładow", "depozyt", "przelew", "blik", "cash-out", "cashout", "kup żeton", "kupić żeton", "kasa na konto"];
    var texts = C.Events.Where(e => e.Tags.Contains("gambling"))
        .SelectMany(e => new[] { e.Title, e.TextPl }.Concat(e.Choices.SelectMany(ch => new[] { ch.LabelPl, ch.SuccessText, ch.FailureText })))
        .Concat(C.Sponsors.Where(t => t.Tags.Contains("gambling") || t.RequiredCategory == Category.Gambling).SelectMany(t => new[] { t.Name, t.Pitch }))
        .OfType<string>();
    Assert.All(texts, t => Assert.DoesNotContain(forbidden, w => t.Contains(w, StringComparison.OrdinalIgnoreCase)));
}

[Fact]
public void Every_adult_business_and_privacy_event_is_tagged() { /* every event requiring side-business has tag adult-business; policy-privacy-leak and side-leak-rumour have privacy-incident */ }

[Fact]
public void Insider_lead_fits_the_insider_start()
    => Assert.Single(CareerFactory.Create(C, Background.IndustryInsider, Difficulty.Normal, PronounSet.Woman, 1).Offers!.Value);
```

Update `Has_the_representative_set` (`Assert.Equal(57, C.Events.Length)` — 42 existing events plus 15 — replaces the 38–45 range; recount `content/m3/events.json` before writing it and use the actual number; live categories now count `Platforms`-free events only: keep the "5 per category" assertion for the M3-A events by excluding IDs that start with `policy-`, `wager-`, `side-`, `sponsor-`, `puls-`, `nurt-`).

Write the comment-only tests of this step out with real assertions for the values in their comments; a test without an assertion is a review failure.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter M3ContentTests`; Expected: FAIL.
- [ ] **Step 3: Write the content** — as specified; run `dotnet test --filter "M3ContentTests|ContentValidator"` until it passes. Existing events stay byte-identical.
- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS (the golden test reads its own copy).
- [ ] **Step 5: Commit**

```bash
git add content/m3 docs/templates source/backend/Tests
git commit -m "feat(content): add platforms, policy, sponsors, wagers and side-business content for m3-systems.2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 18: Policies for offers, bans and the M3-B strategies

**Files:**
- Modify: `source/backend/Domain/Simulation/Policies/IChoicePolicy.cs`, `PolicyDecisions.cs`, `BuiltInPolicies.cs`, `Domain/Simulation/CareerRunner.cs`, `Tests/Support/RandomLegalPolicy.cs`, `Tools/BatchSimulator/BatchRunner.cs`
- Test: `source/backend/Tests/Domain/Simulation/PolicyTests.cs`

**Interfaces:**
- Produces:
  - `record OfferDecision(bool Accept, PolicyReason Reason)`; `IChoicePolicy.ChooseOffer(CareerState s, ContentSnapshot c, SponsorOffer offer, string? blockedReason) : OfferDecision`.
  - `CareerRunner` answers every open offer (in list order) through `ChooseOffer` and `engine.RespondOffer` after the queued decisions and before planning, checking invariants after each.
  - Balanced (and every `RulePolicy`): accept (`offer-accepted`, args `templateId`) when `blockedReason` is null, the template has no incompatibility the policy will trigger (its category rule never picks Gambling while a `gambling`-incompatible contract is active; it never opens the side business) and its required category is the category the policy streams now or the one it will stream for that deal; otherwise decline (`offer-declined`, args `templateId`, `reason`). On a week with a due installment: `PitchFulfillSponsor` in the contract's category (`sponsor-due`, args `templateId`) after forced rest and before rest/training; with a due one-off, stream its category. Never wager, never the side business; `platform-ban` choice: the move with the highest `Migration.RetentionMax`, then the larger target reach, then enum order (`ban-move`, args `platform`, `retention`).
  - New policies (in `M3BAll` only): `Gambler` (Specialist-Gambling plus `Medium` wager whenever the variant is enabled; reason `wager-scheduled`), `SideHustler` (Balanced plus `SideBusinessWork` every fourth free week when enabled; `side-business-scheduled`), `SponsorHunter` (Balanced plus `PitchFulfillSponsor` every third free week when enabled; `pitch-scheduled`), `Migrator` (Balanced plus `PlanPlatformMigration` toward PulsLive in the first enabled week and again the next week; `migration-scheduled`). A "free week" is one where no forced-rest, rest, due or upgrade rule fired.
  - `BuiltInPolicies.M3BAll = [.. M3All, Gambler, SideHustler, SponsorHunter, Migrator]`; `M3All` keeps its ten policies (comment: frozen by `M3GoldenTests`). `BatchRunner.PoliciesFor`: `SystemsV2` → `M3BAll`, `SystemsV1` → `M3All`, otherwise `All`; `KnownPolicies` includes `M3BAll`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Balanced_accepts_a_fitting_offer_and_declines_a_gambling_deal()
{
    var c = new JsonContentSource(TestPaths.ContentM3).Load();
    var s = CareerFactory.Create(c, Background.IndustryInsider, Difficulty.Normal, PronounSet.Woman, 1);
    var decision = BuiltInPolicies.Balanced.ChooseOffer(s, c, s.Offers!.Value[0], null);
    Assert.Equal((true, "offer-accepted"), (decision.Accept, decision.Reason.Code));
    var gamblingDeal = new SponsorOffer("zetonik-wirtualny:1", "zetonik-wirtualny", 1, 2, 450);
    Assert.Equal("offer-declined", BuiltInPolicies.Balanced.ChooseOffer(s, c, gamblingDeal, null).Reason.Code);
}

[Fact]
public void Balanced_fulfils_a_due_installment_before_training()
{
    var s = M3BPlay.Fresh() with { Week = new CareerWeek(16), LastSettledWeek = 15, Energy = Fixed.FromInt(60), Contracts = [new SponsorContract("trusted-tea:4", "trusted-tea", SponsorKind.Contract, 4, 16, 15 + 12, 400, 0)] };
    var decision = BuiltInPolicies.Balanced.ChoosePlan(s, M3BPlay.C, M3BPlay.Engine.LegalPlans(s, M3BPlay.C).Value);
    Assert.Equal((MainAction.PitchFulfillSponsor, "sponsor-due"), (decision.Plan.Action, decision.Reason.Code));
}

[Fact]
public void Balanced_moves_to_the_best_platform_after_a_ban()
{
    var s = M3BPlay.Fresh() with { Week = new CareerWeek(40), BannedPlatforms = [Platform.Czatnia], PendingDecisions = ["platform-ban"] };
    var decision = BuiltInPolicies.Balanced.ChooseEventChoice(s, M3BPlay.Engine.PresentDecision(s, M3BPlay.C).Value);
    Assert.Equal(("move-PulsLive", "ban-move"), (decision.ChoiceId, decision.Reason.Code));   // equal retention; PulsLive has the larger reach
}

[Fact]
public void Gambler_wagers_medium_when_it_can_afford_it() { /* cash 4_000 → Plan.Wager == Medium, category Gambling, reason wager-scheduled; cash 1_000 → no wager */ }

[Fact]
public void Migrator_announces_in_week_27_and_moves_in_week_28() { /* play weeks 27 and 28 with Migrator on M3BTestContent; after week 28 Economy.Current == PulsLive */ }

[Fact]
public void M3b_policies_finish_careers_without_invariant_violations()
{
    var c = new JsonContentSource(TestPaths.ContentM3).Load();
    foreach (var policy in BuiltInPolicies.M3BAll)
    {
        var start = CareerFactory.Create(c, Background.IndustryInsider, Difficulty.Normal, PronounSet.Nonbinary, 11);
        var run = CareerRunner.Run(M3Play.Engine, c, start, policy);
        Assert.Empty(run.InvariantViolations);
        Assert.Equal(CareerStatus.Ended, run.Final.Status);
    }
}

[Fact]
public void M3All_is_unchanged()
    => Assert.Equal(["Balanced", "Grinder", "RestHeavy", "Rotator", "Networker", "Specialist-JustChatting", "Specialist-Fps", "Specialist-Moba", "Specialist-Chess", "Specialist-Gambling"], BuiltInPolicies.M3All.Select(p => p.Name));
```

Write the comment-only tests of this step out with real assertions for the values in their comments; a test without an assertion is a review failure.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter PolicyTests`; Expected: FAIL.
- [ ] **Step 3: Implement** — as listed; `RandomLegalPolicy.ChooseOffer` accepts at random when not blocked.
- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS (both golden tests: no offers, no bans in their content).
- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(simulator): answer offers and bans in policies; add Gambler, SideHustler, SponsorHunter and Migrator

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 19: The M3-B simulation report and tuning; open PR 3

**Files:**
- Modify: `source/backend/Tools/BatchSimulator/BatchRunner.cs`, `BatchStatistics.cs`, `ReportWriter.cs`, `content/m3/events.json` (sponsor templates only, see Step 4)
- Create: `docs/reports/m3b-baseline-simulation.md`
- Modify: `content/m3/balance.json` (tuning only), `docs/gameplay-systems.md` (§12 with the final numbers; §6 and §7 point to it; §8 lists the new reason codes), `docs/content-and-experience.md` (§2: `sponsors` array, eligibility `platforms`/`minStrikes`, effect kinds `PolicyStrike`/`GlobalBan`/`SponsorOffer`, tags), the spec (status: rules and content delivered)
- Test: `source/backend/Tests/BatchSimulator/BatchRunnerTests.cs`

**Interfaces:**
- Produces: `CareerSummary` init members `SponsorIncome`, `TotalIncome`, `Installments`, `Breaches`, `Strikes`, `PlatformBans`, `Migrations`, `ReconciliationViolations` (filled through `onWeek` and the final metrics); careers start with `CareerFactory.Create(ContentSnapshot …)`; the report for `m3-systems.2` gains the sections "Economy and risk" and "M3-B criteria" (PASS/FAIL per spec §9).

- [ ] **Step 1: Write the failing tests**

Extend `BatchRunnerTests`: `--content <repo>/content/m3 --careers 28` uses `m3-systems.2` and `BuiltInPolicies.M3BAll`; every summary has `ReconciliationViolations == 0`; the report contains "Economy and risk" and "M3-B criteria" with the six criterion names of spec §9 (`No side business needed`, `Gambling is not dominant`, `Side business is a trade-off`, `Bans are rare but real`, `Sponsors matter`, `Reconciliation`).

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter BatchSimulator`; Expected: FAIL.
- [ ] **Step 3: Implement** the measures and sections. Criteria, exactly as spec §9: Balanced non-insolvent ≥ 90 %; Gambler median cash and median score below Balanced's (insolvency rate reported); SideHustler median cash between 1.0× and 1.5× Balanced's; at least 1 % of Gambler + SideHustler careers banned from a platform and fewer than 2 % of all careers ending `GlobalBan` or `AllPlatformsBanned`; SponsorHunter sponsor income ≥ 10 % of its total income (breach rate reported); zero reconciliation violations.

- [ ] **Step 4: Generate and tune**

Run (from `source/backend`): `dotnet run --project Tools/BatchSimulator -c Release -- --careers 10000 --seed 42 --content ../../content/m3 --out ../../docs/reports/m3b-generated`

Write `docs/reports/m3b-baseline-simulation.md` in the shape of the M3-A report (date, summary, findings, criteria, follow-ups, "Tuning log", generated report); delete `m3b-generated`. If a criterion fails, change only numbers in the M3-B balance sections (and sponsor payments/weights in `content/m3/events.json`: unreleased templates — never on `main` — may change in place under their version, which TD-117 records as the exception to the TD-112 immutability rule until the first release; once released they follow TD-112), rerun and log each change with its reason. Stop after five rounds; a criterion still failing becomes a follow-up issue for M5 (#36) named in the PR body.

- [ ] **Step 5: Document**

`docs/gameplay-systems.md` §12 (status "Agent proposal, tuned by the M3-B simulation"): platform table, exposure and strikes, bans and endings, migration and retention, sponsor offers/contracts/breaches, wager, side business, drama decay, the new policies and reason codes (`sponsor-due`, `offer-accepted`, `offer-declined`, `ban-move`, `wager-scheduled`, `side-business-scheduled`, `pitch-scheduled`, `migration-scheduled`), each number as finally tuned. Add a GDD decision-log row "M3-B tuning rulings" only for numbers that changed from the spec, and update spec §5–§7 numbers to match.

- [ ] **Step 6: Run all tests, commit and open PR 3**

Run: `dotnet test` — Expected: PASS.

```bash
git add source/backend content/m3 docs
git commit -m "feat(simulator): report the M3-B economy and risk criteria and tune m3-systems.2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/m3b-3-platforms
gh pr create --base develop --title "M3-B PR 3: platforms, bans, migration, content and simulation" --body "$(cat <<'EOF'
Platform factors, policy exposure and strikes, platform bans with the ruleset-built platform-ban decision, migration with shown retention, GlobalBan and AllPlatformsBanned endings, drama decay; content/m3 balance sections, 12 sponsor templates and 15 events; Balanced answers offers, due tasks and bans; new Gambler, SideHustler, SponsorHunter and Migrator policies; the M3-B baseline report with its criteria and tuning log. m1-prototype.1 and m3-systems.1 golden tests unchanged. Part of #18, #19 and #20.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI, check SonarQube (`"total":0`), ask the owner to merge, then check `&branch=develop`.

---

# PR 4 — API and play

Start from the latest `develop` after PR 3 merged: `git switch -c feature/m3b-4-play origin/develop`.

### Task 20: API contract, `respondOffer`, views and stored responses

**Files:**
- Modify: `source/backend/Application/Careers/Contracts.cs`, `CareerViewBuilder.cs`, `ReportViews.cs`, `SubmitAction.cs`, `StoredContract.cs`, `RunBatch.cs`, `CreateCareer.cs`, `Persistence/CareerRecords.cs`
- Modify: `source/backend/WebApp/Careers/Requests.cs`, `source/backend/WebApp/openapi/v1.json` (regenerated)
- Test: `source/backend/Tests/Application/Careers/EconomyFlowTests.cs` (new), `StoredContractTests.cs`, `CareerViewBuilderTests.cs`, `Tests/WebApp/M3CareerApiTests.cs`

**Interfaces:**
- Consumes: `WeekEngine.RespondOffer`, `PresentPlan`, `PresentAction`, `PresentDecision` (with `Definition`), `SponsorRules.Template`/`AcceptRejection`, `SideBusinessRules.Income`, `PolicyRules.ActiveStrikes`, `CareerFactory.Create(ContentSnapshot …)`.
- Produces (contract consumed by Task 21; every new member of an existing record is a required parameter, filled for older rows by `StoredContract`):
  - `ActionType.RespondOffer`; `ActionKind.RespondOffer`.
  - `ActionInput(… existing …, WagerTier? Wager = null, Platform? TargetPlatform = null, string? OfferId = null, bool? Accept = null)` and `ActionRequest` with the same trailing members, all `[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. Validator: `RespondOffer` needs `OfferId` (1–64 characters) and `Accept`; `Wager`, `TargetPlatform` `IsInEnum`.
  - `PlanView(MainAction MainAction, Category Category, Category? TrainTarget, EquipmentSlot? UpgradeSlot, WagerTier? Wager, Platform? TargetPlatform)`.
  - `PlanOptionView(PlanView Plan, string? DisabledReason, ChoiceView? Odds, WagerTermsView? Wager, PolicyExposureView? Policy, MigrationEstimateView? Migration, IReadOnlyList<SponsorConsequenceView> SponsorConsequences)` with `WagerTermsView(WagerTier Tier, long StakePln, decimal Chance, long PayoutPln, long LossPln)`, `PolicyExposureView(int Exposure, decimal Chance, int Threshold)`, `MigrationEstimateView(Platform From, Platform To, bool Executes, decimal RetentionMin, decimal RetentionMax, long FollowersMin, long FollowersMax, PlatformProfileView Target)`, `PlatformProfileView(decimal Reach, decimal SubYield, decimal AdYield, decimal PolicyRisk)`, `SponsorConsequenceView(string ContractId, string TemplateId, string Kind /* missedInstallment|breach */, long PenaltyPln, decimal Credibility, bool Terminates)`.
  - `ChoiceView` appends `MigrationEstimateView? Migration`; the odds of a pitch plan use the pitch choice definition (not the network one).
  - `CareerView` appends `Platform Platform, int PlatformSinceWeek, IReadOnlyList<StrikeView> Strikes, IReadOnlyList<Platform> BannedPlatforms, IReadOnlyList<OfferView> Offers, IReadOnlyList<ContractView> Contracts, SideBusinessView? SideBusiness` with `StrikeView(Platform Platform, int GivenWeek, int ExpiresAfterWeek)`, `OfferView(string OfferId, string TemplateId, string Name, string Pitch, SponsorKind Kind, long PaymentPln, int? TermWeeks, int? DueEveryWeeks, Category? RequiredCategory, IReadOnlyList<Platform> Platforms, SponsorPrerequisitesView Prerequisites, IReadOnlyList<string> IncompatibleWith, long BreachCashPln, decimal BreachCredibility, int ExpiresAfterWeek, string? AcceptBlockedReason)`, `SponsorPrerequisitesView(long MinFollowers, decimal? MinCredibility, decimal? MaxDrama, decimal? MinDrama)`, `ContractView(string ContractId, string TemplateId, string Name, SponsorKind Kind, long PaymentPln, Category? RequiredCategory, IReadOnlyList<Platform> Platforms, int NextDueWeek, int TermEndWeek, int Missed, long BreachCashPln, decimal BreachCredibility, bool DueThisWeek)`, `SideBusinessView(int OpenedWeek, decimal Quality, long WeeklyIncomePln)`.
  - `WeekReportView` appends `StrikeRollView? Strike, WagerResultView? Wager, MigrationView? Migration` (`StrikeRollView(Platform Platform, int Exposure, int Threshold, int? Roll, bool Strike, bool Banned)`, `WagerResultView(WagerTier Tier, long StakePln, int Roll, int Threshold, bool Won, long NetPln)`, `MigrationView(Platform From, Platform To, decimal Retention, long FollowersBefore, long FollowersAfter)`).
  - `AutoWeekView` appends `IReadOnlyList<AutoOfferView> Offers` with `AutoOfferView(string OfferId, string TemplateId, bool Accepted, ReasonView Reason)`; `RunBatchHandler.PlayWeek` answers offers with Balanced first and commits them inside the week's entry.
  - `StoredContract` defaults for rows written before PR 4: plans `wager`/`targetPlatform` null; plan options `wager`/`policy`/`migration` null and `sponsorConsequences` `[]`; choices `migration` null; career `platform` `"czatnia"`, `platformSinceWeek` 1, `strikes`/`bannedPlatforms`/`offers`/`contracts` `[]`, `sideBusiness` null; reports `strike`/`wager`/`migration` null; auto weeks `offers` `[]`.

- [ ] **Step 1: Write the failing application tests**

`EconomyFlowTests.cs` (`AppTestKit.WithContent(TestPaths.ContentM3)`; add `NewCareerAsync(…, Background background)` usage with `IndustryInsider`):

```csharp
[Fact]
public async Task Insider_career_shows_the_lead_offer_with_its_terms()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
    var offer = Assert.Single(view.Offers);
    Assert.Equal(("bank-skarbonka:1", SponsorKind.OneOff, 2), (offer.OfferId, offer.Kind, offer.ExpiresAfterWeek));
    Assert.True(offer.PaymentPln > 0);
    Assert.Null(offer.AcceptBlockedReason);
    Assert.Equal((Platform.Czatnia, 1), (view.Platform, view.PlatformSinceWeek));
}

[Fact]
public async Task Accepting_an_offer_moves_it_to_contracts()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
    var outcome = (await kit.SubmitAsync(view.CareerId, Guid.NewGuid(), Respond(view, accept: true))).Value;
    Assert.Empty(outcome.Career.Offers);
    Assert.Equal("bank-skarbonka", Assert.Single(outcome.Career.Contracts).TemplateId);
    Assert.Equal(NextStepKind.PlanWeek, outcome.Career.Next.Kind);
}

[Fact]
public async Task Repeated_respond_offer_returns_the_stored_outcome()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var view = (await kit.NewCareerAsync(background: Background.IndustryInsider)).Career;
    var commandId = Guid.NewGuid();
    var first = await kit.SubmitAsync(view.CareerId, commandId, Respond(view, accept: true));
    var second = await kit.SubmitAsync(view.CareerId, commandId, Respond(view, accept: true));
    Assert.Equal(ContractJson.Serialize(first.Value), ContractJson.Serialize(second.Value));
    Assert.Equal(first.Value.Career.Version, (await kit.GetAsync(view.CareerId)).Version);
}

[Fact]
public async Task Respond_offer_needs_an_offer_id_and_an_answer() { /* OfferId null → ValidationFailed */ }

[Fact]
public async Task Unknown_offer_is_an_illegal_action() { /* OfferId "x:1" → ErrorCodes.IllegalAction with detail unknown-offer */ }

[Fact]
public async Task Gambling_plans_show_wager_terms_and_disable_unaffordable_stakes()
{
    var kit = AppTestKit.WithContent(TestPaths.ContentM3);
    var view = (await kit.NewCareerAsync()).Career;   // Dorm: 2,000 PLN
    var medium = view.Next.Plans.Single(p => p.Plan is { MainAction: MainAction.NoAction, Category: Category.Gambling, Wager: WagerTier.Medium });
    Assert.Equal((500L, 0.45m, 1_000L, 500L), (medium.Wager!.StakePln, medium.Wager.Chance, medium.Wager.PayoutPln, medium.Wager.LossPln));
    Assert.Null(medium.DisabledReason);   // 500 ≤ 25 % of 2,000
    var high = view.Next.Plans.Single(p => p.Plan is { MainAction: MainAction.NoAction, Category: Category.Gambling, Wager: WagerTier.High });
    Assert.Equal("needs-cash", high.DisabledReason);
    Assert.Equal(3, medium.Policy!.Exposure);
}

[Fact]
public async Task Start_week_with_a_wager_reports_it()
{
    /* StartWeek NoAction Gambling Wager Low → settle → report.Wager.StakePln == 100 and an income:wager or cost:wager line */
}

[Fact]
public async Task Ban_decision_is_shown_with_retention_ranges() { /* store a career state with BannedPlatforms [Czatnia] and PendingDecisions ["platform-ban"] through kit.Store; GetAsync → Next.Kind ResolveDecision, Event.EventId "platform-ban", Event.Title from platformBan, every choice has Migration with RetentionMin < RetentionMax */ }

[Fact]
public async Task Batch_stops_before_the_platform_ban_decision() { /* same stored state → RunBatchAsync → StopReason PausedForDecision, WeeksCommitted 0 */ }

[Fact]
public async Task Batch_answers_offers_and_records_the_reason()
{
    /* Insider career → RunBatchAsync → Weeks[0].Offers has bank-skarbonka with Reason.Code offer-accepted or offer-declined */
}
```

`Respond(view, accept)` = `new ActionInput(view.Version, ActionType.RespondOffer, null, null, null, null, null, OfferId: view.Offers[0].OfferId, Accept: accept)`.

In `StoredContractTests` add `A_v1_request_with_the_new_optional_fields_still_serializes_byte_for_byte` (the existing golden `contract-v1/action-input.json` round-trips with the new `ActionInput`) and `An_m3a_response_reads_with_the_m3b_defaults` (a current pre-PR-4 response JSON written by the test from today's records, then read: `platform` Czatnia, empty lists, null estimates). In `M3CareerApiTests` add an HTTP round trip of `respondOffer` (200 with the career), and `respondOffer` without `accept` (400 `validation-failed`).

Write the comment-only tests of this step out with real assertions for the values in their comments; a test without an assertion is a review failure. These carry Review Focus 4 and 5 evidence.

- [ ] **Step 2: Run to see them fail** — `dotnet test --filter "EconomyFlowTests|StoredContractTests|M3CareerApiTests|CareerViewBuilderTests"`; Expected: compile errors.

- [ ] **Step 3: Implement**

- `SubmitAction.Execute`: `StartWeek` passes `Wager` and `TargetPlatform` into the `WeekPlan`; `RespondOffer` → `engine.RespondOffer(s, c, input.OfferId!, input.Accept!.Value, input.ExpectedVersion)` mapped to `(next, null, null)`; `KindOf` maps `RespondOffer`; `Defined` checks the two new enums.
- `CareerViewBuilder`: the new career members (names and pitches from the template, `AcceptBlockedReason` from `SponsorRules.AcceptRejection`, weekly side income from `SideBusinessRules.Income`, strikes only while active); plan options call `engine.PresentPlan` once per plan; the event view uses `presented.Definition ?? c.Events.First(…)`; choice `Migration` from `ChoiceOdds.Migration`.
- `CreateCareerHandler` uses `CareerFactory.Create(snapshot, background, difficulty, pronouns, seed)`.
- `RunBatchHandler.PlayWeek`: before planning, for each open offer call `Policy.ChooseOffer` and `engine.RespondOffer`, collecting `AutoOfferView`s.
- Regenerate the OpenAPI document: from `source/backend`, `dotnet build -c Release && dotnet run --project WebApp -c Release --no-build -- openapi --document v1 --output openapi/v1.json`.

- [ ] **Step 4: Run all tests** — `dotnet test`; Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add source/backend
git commit -m "feat(api): add respondOffer, platform, sponsor and risk views to the career contract

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 21: Frontend — platform and offers cards, plan risks, ban decision and report lines

**Files:**
- Modify: `source/frontend/src/api/generated/*` (regenerated), `src/features/career/CareerDesk.tsx`, `plans.ts`, `plans.test.ts`, `reportLabels.ts`, `stage/PlanWeek.tsx`, `stage/PlanWeek.test.tsx`, `stage/ChoiceOdds.tsx`, `stage/LiveEvent.test.tsx`, `stage/WeekReport.tsx`, `stage/WeekReport.test.tsx`, `batch/BatchResult.tsx`, `batch/reasons.ts`, `src/locales/pl/translation.json`
- Create: `src/features/career/PlatformCard.tsx`, `PlatformCard.test.tsx`, `OffersCard.tsx`, `OffersCard.test.tsx`

**Interfaces:**
- Consumes: Task 20 contract (generated types `OfferView`, `ContractView`, `PlanOptionView`, `WagerTier`, `Platform`, `ActionType.respondOffer`).
- Produces:
  - `plans.ts`: `optionFor(plans, action, category, slot, wager: WagerTier | null, target: Platform | null)`; `wagerTiers(plans, action, category) : WagerTier[]`; `migrationTargets(plans, category) : Platform[]`.
  - `PlatformCard({ career })`: current platform, weeks on it, active strikes with their expiry week ("Ostrzeżenie do tygodnia {{week}}"), banned platforms.
  - `OffersCard({ career, secret })`: each offer with name and pitch (`formatContent`), kind, payment, term and cadence, required category, platforms, prerequisites, incompatibilities, breach penalty, expiry week, and **Przyjmij** / **Odrzuć** buttons sending `respondOffer` through `useActionCommand`; *Przyjmij* is disabled with the reason (`offer.blocked.<code>`) when `acceptBlockedReason` is set; `OffersCard` maps the reason code to a dash key before the lookup (`prerequisite:followers` → `offer.blocked.prerequisite-followers`; a `:` would be read as the i18next namespace separator), and the keys are `contract-limit`, `same-sponsor`, `prerequisite-followers`, `prerequisite-credibility`, `prerequisite-drama`, `prerequisite-platform`, `prerequisite-adult-business`; then the active contracts with their next due week, marked "w tym tygodniu" when due. Shown only while planning (`next.kind === planWeek`).
  - `PlanWeek`: the wager selector (none, Low, Medium, High; disabled tiers keep their reason) when the category is Gambling; the target selector for `planPlatformMigration`; for the chosen option a risk panel listing wager terms (stake, chance, payout, loss, "tylko wirtualne złotówki z gry"), policy exposure and chance, the migration estimate (announce vs move, retention range, follower range, target factors) and each sponsor consequence with its penalty; the start command sends `wager` and `targetPlatform`.
  - `ChoiceOdds` shows `choice.migration` (retention and follower range) for the ban decision.
  - `WeekReport` shows the strike roll, the wager result and the migration (exact follower count) lines; `lineLabel` knows `income.sponsor`, `cost.sponsor-penalty`, `income.wager`, `cost.wager`, `income.side-business`; `statChangeText` knows `followers:migration` and the causes `sponsor`, `wager`, `side-business`, `migration`, `decay`.
  - Translations: `action.pitchFulfillSponsor` "Sponsorzy: oferta lub zlecenie", `action.sideBusinessWork` "Biznes poboczny", `action.planPlatformMigration` "Przeprowadzka na inną platformę"; `platform.*` keyed by the generated enum values (`czatnia`, `pulsLive`, `nurt`); `wager.*`; `disabled.*` for `needs-cash`, `too-early`, `needs-followers`, `too-soon`, `platform-banned`, `no-sponsor-fits`, `wager-category`; `offer.*`; `career.ending.globalBan` "Globalny ban — kariera zakończona", `career.ending.allPlatformsBanned` "Bany na wszystkich platformach — kariera zakończona"; `reason.*` for the eight new reason codes. Polish copy for owner review.

- [ ] **Step 1: Regenerate the client** — `npm run api:generate`; `npm run typecheck` shows the compile errors to fix in the next steps.

- [ ] **Step 2: Write the failing tests**

```tsx
it('accepts an offer with a fresh command', async () => {
  const career = careerWith({ offers: [offer({ offerId: 'bank-skarbonka:1' })] });
  render(<OffersCard career={career} secret="s" />);
  await user.click(screen.getByRole('button', { name: pl.offer.accept }));
  expect(submitAction).toHaveBeenCalledWith(
    expect.objectContaining({ type: ActionType.respondOffer, offerId: 'bank-skarbonka:1', accept: true }),
    expect.anything(),
  );
});

it('keeps a blocked offer visible with its reason', () => {
  render(<OffersCard career={careerWith({ offers: [offer({ acceptBlockedReason: 'contract-limit' })] })} secret="s" />);
  expect(screen.getByRole('button', { name: pl.offer.accept })).toBeDisabled();
  expect(screen.getByText(pl.offer.blocked['contract-limit'])).toBeVisible();
});

it('shows stake, chance, payout and loss before a gambling week starts', async () => {
  render(<PlanWeek career={careerWithGamblingPlans()} secret="s" />);
  await user.click(screen.getByRole('radio', { name: pl.category.gambling }));
  await user.click(screen.getByRole('radio', { name: pl.wager.medium }));
  expect(screen.getByText(/500 zł/u)).toBeVisible();
  expect(screen.getByText(/45 %/u)).toBeVisible();
  expect(screen.getByRole('radio', { name: pl.wager.high })).toBeDisabled();
});

it('warns about the penalty of a missed installment', () => { /* option with sponsorConsequences [missedInstallment, 200] → text contains "200 zł" */ });
it('shows the migration range before the move', () => { /* planPlatformMigration + target nurt → retention 65–75 % and follower range */ });
it('reports the exact followers after a migration', () => { /* WeekReport with migration {followersBefore 10000, followersAfter 7012} → "7 012" */ });
it('labels sponsor, wager and side-business lines', () => { /* lineLabel for the five new codes is not the raw code */ });
it('shows the strikes and banned platforms', () => { /* PlatformCard with strikes and bannedPlatforms */ });
it('maps a prerequisite reason code to its dash key', () => { /* acceptBlockedReason 'prerequisite:followers' → pl.offer.blocked['prerequisite-followers'] visible, no raw code */ });
```

`careerWith`, `offer` and `careerWithGamblingPlans` extend the existing fixture builders in the stage tests. Write the comment-only tests of this step out with real assertions for the values in their comments; a test without an assertion is a review failure.

- [ ] **Step 3: Run to see them fail** — `npm test`; Expected: FAIL.
- [ ] **Step 4: Implement** — as listed; `CareerDesk` places `PlatformCard` and `OffersCard` above the stage on the planning step (single column on narrow screens; no horizontal scroll at 320 px).
- [ ] **Step 5: Check** — `npm run lint && npm run typecheck && npm test -- --coverage`; Expected: PASS, line coverage ≥ 80 %.
- [ ] **Step 6: Commit**

```bash
git add source/frontend
git commit -m "feat(frontend): show platforms, sponsor offers, plan risks and the ban decision

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 22: Content cross-check and the Playwright journey

**Files:**
- Modify: `source/frontend/src/content/m3Content.test.ts`, `source/frontend/e2e/journey.spec.ts`

- [ ] **Step 1: Extend the cross-check**

`m3Content.test.ts` also formats, for all three pronoun sets, every sponsor `name` and `pitch`, `balance.sponsors.pitch` label and texts, and `balance.platformBan` title, text and each choice label; the test fails on any message `intl-messageformat` rejects.

- [ ] **Step 2: Extend the journey**

After the existing week-1 path in a second test: create a career with the `IndustryInsider` background, expect the offers card with the lead (`pl.offer.title`), run axe on it (`expectAccessible(page, 'sponsor offers')`), click **Przyjmij**, expect the contract in the contracts list, choose a plan in the deal's category (any category for the lead one-off), play the week to its report and expect a `income:sponsor` or `cost:sponsor-penalty` line label in it; then continue to the next plan screen.

- [ ] **Step 3: Run** — `npm test` and `npm run e2e` (Compose stack as in CI); Expected: PASS.
- [ ] **Step 4: Commit**

```bash
git add source/frontend
git commit -m "test: cross-check sponsor and ban texts and accept an offer in the browser journey

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 23: Close out documentation, issues and PR 4

**Files:**
- Modify: `docs/requirements-traceability.md`, `docs/technical-architecture.md` (career API: `respondOffer`, the new career and report fields, the ruleset-built decision), `docs/gameplay-systems.md` (§12 API notes only if behaviour changed in review), the spec (status: delivered parts, links to PRs), `docs/technical-decisions.md` (a TD only for implementation choices not yet recorded)

- [ ] **Step 1: Update documents** — traceability REQ-01, REQ-06, REQ-07, REQ-09, REQ-10 cite the evidence (test names of the spec §13 table and the M3-B report) and keep `In progress (M3)` until the M3 release; the technical architecture lists `respondOffer` (log kind `RespondOffer`) and the free-selection rule.
- [ ] **Step 2: Update GitHub issues** — comment on #18, #19 and #20 with what M3-B delivered and the evidence; do not close them (they close with the M3 release into `main`).

```bash
gh issue comment 18 --body "M3-B delivered sponsors (offers, one-off deals, contracts, breaches), PitchFulfillSponsor and the new report lines; every cash movement reconciles. Evidence: OfferTests, ContractTests, ReconciliationProperties, EconomyFlowTests, docs/reports/m3b-baseline-simulation.md. Closes with the M3 release."
```

(Write the #19 and #20 comments the same way with their evidence: `PolicyTests`, `MigrationTests`, `PlatformBanTests`; `WagerTests`, `SideBusinessTests`, `Gambling_text_never_offers_real_money`.)

- [ ] **Step 3: Commit, push and open PR 4**

```bash
git add docs
git commit -m "docs: record the M3-B API and close out the economy and risk sub-project

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/m3b-4-play
gh pr create --base develop --title "M3-B PR 4: play the economy and risk systems in the browser" --body "$(cat <<'EOF'
respondOffer command; career view with platform, strikes, bans, offers, contracts and the side business; plan options with wager terms, policy exposure, migration estimates and sponsor consequences; the platform-ban decision with retention ranges; report lines for strikes, wagers and migrations; older stored responses upgraded on read. Frontend platform and offers cards, wager and migration selectors, Playwright journey accepting an offer. Part of #18, #19 and #20.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Wait for CI (including `frontend-e2e`), check SonarQube (zero open issues on the PR), ask the owner to merge, then check `&branch=develop`.

---

## Spec coverage

| Spec section | Tasks |
| --- | --- |
| §1 intent, success bullets | 9 (terms, penalties), 12 (reconciliation), 7/10/20/21 (disabled with reason), 10 (wager disclosure), 18/19 (no side business needed), 15/16 (retention range and exact count), 14/16 (bans, endings) |
| §2 confirmed inputs | 0 (decision log), 5, 13–16 |
| §3 scope | every task; out-of-scope items untouched |
| §4 ruleset and versioning, golden freeze, nullable fields | 1, 2, 3 (and the golden step of every task) |
| §5.1 platform state and profiles | 3, 5, 13, 17 |
| §5.2 policy strikes | 4 (effect), 14, 17 |
| §5.3 ban, forced move, endings | 14, 16 |
| §5.4 migration | 15, 16 |
| §5.5 drama decay and prerequisites | 8 (prerequisites), 13 (decay) |
| §6.1 sponsor templates | 4, 6, 17 |
| §6.2 offers, `respondOffer`, Insider lead, pitch | 8, 9, 20 |
| §6.3 obligations, breaches, week off, migration | 9, 11, 15, 16 |
| §6.4 equipment and revenue grouping, reconciliation | 12, 19, 21 |
| §7.1 wager | 7, 10, 14, 17, 21 |
| §7.2 side business | 7, 11, 14, 17 |
| §8 eligibility, effect kinds, validator, content | 4, 6, 17, 22 |
| §9 automation and simulation criteria | 18, 19 |
| §10 API and minimal UI | 20, 21, 22 |
| §11 delivery | PR steps in 6, 12, 19, 23 |
| §12 decision records and documentation | 0, 12, 19, 23 |
| §13 acceptance evidence | 9, 10, 11, 12, 15, 16, 17, 19, 20, 21 |
| §14 risks | 19 (tuning), 18/20 (offers never pause), 7 (plan list), 1 (golden), 0 (proposals as data) |
