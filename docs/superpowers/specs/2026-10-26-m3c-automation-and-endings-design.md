# M3-C Automation and Endings — Design

Date: 2026-10-26
Status: **Agent proposal; rules, content and simulation delivered in PR 1 (2026-10-26), numbers tuned by the [M3-C baseline simulation](../../reports/m3c-baseline-simulation.md)** (final values in [gameplay §13](../../gameplay-systems.md#13-m3-c-automation-and-endings)). The owner asked the agent to design sub-project C without a design interview ("tak rób", 2026-10-26, answering whether the agent should design C itself as it did B). Every rule and number below is an agent proposal to validate in review and by the M3-C simulation; nothing here is a confirmed owner choice unless it cites the GDD.
Milestone: [M3 — Complete gameplay systems](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/3), sub-project C, delivering [#22](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/22) and [#23](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/23) ([TD-109](../../technical-decisions.md)).
Builds on: [M3-B economy and risk](2026-10-21-m3b-economy-and-risk-design.md) (ruleset `m3-systems.2`, sponsor offers, platform bans, policies `M3BAll`).

## 1. Intent

The player can let the game play 4, 13 or 52 weeks under a strategy they set, see why each automatic choice was made, and finish a career with every ending, a data-driven achievement set and a visible score breakdown.

Success means:

- A batch of 4, 13 or 52 weeks commits each week on its own, stops before every mandatory decision (including a platform ban) and at an ending, and a repeated request never plays a week twice (#22).
- A player strategy (category plan, weekly routine, rest thresholds, cash reserve, risk appetite, sponsors, wager, side business) drives the batch; every plan, offer and event choice records a reason, including the fallback when the preferred plan is not legal (#22).
- Achievements are content data with Polish names; the domain computes the score with the approved 45/45/10 model; every ending has tests; the ending view shows F, W, A, each contribution, both multipliers, the total, the earned achievements and a templated career summary (#23).

## 2. Confirmed inputs (from the GDD and specifications)

- Batch lengths 4, 13 and 52 weeks, pausing for major events; the player sets a between-stream plan for the batch (GDD decision log 2026-10-12).
- The player sets a strategy that guides event choices during automatic streams (GDD 2026-10-12).
- Score `round(100,000 × (0.45F + 0.45W + 0.10A)) × difficulty × background` with `A = min(1, points / 10)` (gameplay §9, owner-approved split); achievements give no hidden multipliers.
- The ending uses templates driven by achievements, platform history, dominant category, reputation and ending reason; the PDF diploma is sub-project D (#24).
- Gameplay §8: a strategy has a category rotation, a weekly action sequence, a sponsor acceptance rule, a financial reserve, risk appetite, a drama target, a rest threshold and event-response priorities; automation picks only legal options and records a fallback reason; it never spends beyond the reserve.

## 3. Scope

**In scope**

- `Domain`: `Strategy` and its validation; `StrategyPolicy : IChoicePolicy`; the optional balance section `achievements` with data-driven achievement rules and two nullable metrics; `EndingSummary`; validator rules.
- `Application`/`WebApp`: batch weeks 4, 13 or 52; an optional `strategy` in the batch request; career view members for achievements, the score breakdown and the ending summary; OpenAPI regenerated.
- Frontend: batch length and strategy form in the batch dialog (remembered per career in the browser), the ending view with the score breakdown, achievements and summary.
- `content/m3`: the `achievements` section (11 achievements).
- Batch simulator: three strategy policies and an "Achievements and strategies" report section with criteria.

**Out of scope**

- Event-response priorities (growth, cash, credibility, safety): a policy sees odds, not outcomes, so a priority cannot be honoured without exposing outcome classes; deferred to M5 content work (§10 ruling 1).
- Strategies stored on the server as named objects; ranked eligibility and leaderboard submission (#27, M4); the dashboard, history, PDF diploma and annual review screens (D, #24).

## 4. Batch (#22)

- `BatchInput(long ExpectedVersion, int Weeks, Strategy? Strategy = null)`; `Weeks` must be 4, 13 or 52 (`validation-failed` otherwise). `Strategy` is omitted from JSON when null, so stored 13-week requests serialize byte for byte (TD-91); a request without a strategy plays `Balanced` exactly as before.
- Unchanged mechanics: each week is its own `autoWeek` log entry; a repeat with the same command ID replays the stored weeks and continues only when nothing else moved the career; offers are answered by the policy inside the week's entry; the batch stops at an ending or before a week that needs the player (any queued decision, including `platform-ban`).
- The strategy is part of the stored request, so a repeat must send the same strategy (`command-id-reused` otherwise) and the log shows which strategy chose each week.
- "Saved strategies": the browser remembers the last strategy per career and offers it again; the server keeps every strategy used in the batch requests (ruling 2).

## 5. Strategy

`Strategy` (all fields required in the request; the client sends the defaults):

| Field | Values | Default | Meaning |
| --- | --- | --- | --- |
| `categoryPlan` | `strongest`, `focus`, `rotate` | `strongest` | Strongest starting skill (least bored while it is boring, boredom ≥ 60), one fixed category, or rotation by week |
| `focus` | category or null | null | Required for `focus` |
| `routine` | 1–8 of `noAction`, `trainCategory`, `manageCommunity`, `networkCollaborate`, `pitchFulfillSponsor` | `noAction, noAction, noAction, trainCategory` | The free week's action: `routine[(week − 1) mod length]` |
| `restBelowEnergy` | 0–80 | 30 | Rest when energy is below |
| `restAboveBurnout` | 50–100 | 70 | Rest when burnout is above (100: only forced rest) |
| `reservePln` | 0–1,000,000 | 0 | Cash never spent below this by upgrades or wagers |
| `upgrades` | bool | true | Buy the lowest-tier slot when `cash − price ≥ max(reserve, 2 × price)` |
| `risk` | `safe`, `balanced`, `bold` | `balanced` | Gamble threshold 65 % / 50 % / 35 %; manage the community above drama 25 / 40 / 60 |
| `acceptOffers` | bool | true | Accept offers that do not clash with the strategy |
| `wager` | null, `low`, `medium`, `high` | null | Stake on Gambling weeks when legal and `cash − stake ≥ reserve` |
| `sideBusiness` | bool | false | Work on the side business when `week mod 4 = 2` and the action is legal (18+ content, optional) |

Plan order for a week: forced rest (energy 0 or burnout 100) → a due sponsor installment (`PitchFulfillSponsor` in its category) → rest thresholds → community management above the drama limit → upgrade → side-business week → the routine action. A due one-off sets the category. The wager is attached to a Gambling plan that is not a rest. When the preferred plan is disabled, the policy tries `noAction` in the same category, then `rest`; the reason is `fallback-safe` with `preferred` and `disabledReason`. Offers: accepted when `acceptOffers`, the engine would accept and the deal does not clash (a required category other than a `focus`, a gambling deal while the strategy never streams Gambling, a gambling-forbidding deal while it focuses Gambling or wagers, an adult-business-forbidding deal while `sideBusiness`). Event choices: the best gamble at or above the threshold, otherwise a guaranteed choice; the ban decision picks the highest retention, then the larger reach.

New reason codes: `routine-step` (`step`, `of`, `action`), `fallback-safe` (`preferred`, `disabledReason`), `strategy-wager` (`tier`), `strategy-side-business`, `strategy-upgrade` (`slot`, `pricePln`, `reservePln`); existing codes are reused for forced rest, rest, drama, due installments, offers and event choices. The category rule names are `bonus`, `least-bored`, `fixed`, `rotation`, `sponsor-safe`.

Validation (`Strategy.Errors()`), each a stable code: `focus-missing`, `focus-unexpected`, `routine-length`, `routine-action`, `energy-range`, `burnout-range`, `reserve-range`.

## 6. Achievements and score (#23)

- `balance.achievements` (optional; absent keeps the hard-coded M1 set, so `m1-prototype.1` and `m3-systems.1` results never change): a list of `{ id, name, description, kind, threshold?, points }`. `name` and `description` are Polish ICU strings shown by the client.
- Kinds: `followers` (followers ≥ threshold at a settlement), `cash` (cash ≥ threshold PLN), `completed` (week 1,040 settled), `mastery` (skill ≥ threshold in any category), `specialist` (weeks streamed in one category ≥ threshold), `platformMove` (a move with no banned platform; tuned 2026-10-26, proposed: any move), `sponsorInstallments` (paid installments ≥ threshold), `dramaSurvived` (completed with peak drama ≥ threshold and final drama ≤ 30; tuned 2026-10-26, proposed: no recovery condition).
- New nullable metrics, written only when the section exists: `PeakDrama` and `SponsorInstallments` on `CareerMetrics` (omitted from canonical JSON when null). A move is read from `PlatformSinceWeek > 1`.
- Achievements are evaluated at every settlement and at retirement, are never revoked, and their points feed `A` exactly as today (`AchievementPointsCap` 10). The score formula, weights and multipliers are unchanged.
- Validator: unique IDs, known kind, threshold present and positive for the kinds that need one, points 1–5, ICU name and description; `achievement:<rule>` violation codes.

Proposed `content/m3` set (11 achievements, 16 points before tuning; the tuned set is in gameplay §13): `followers-10k` (1), `followers-100k` (1), `followers-1m` (2), `cash-1m` (1), `career-completed` (2), `skill-master` (mastery 90, 1), `one-category` (specialist 520 weeks, 1), `new-home` (platform move, 2), `trusted-partner` (24 installments, 2), `scandal-survivor` (completed with peak drama ≥ 90, 2), `cash-10m` (1).

## 7. Endings and summary (#23)

- Endings: `Completed`, `Insolvency`, `EarlyRetirement`, `GlobalBan`, `AllPlatformsBanned` (all exist); every one gets an end-to-end ending test through the engine.
- `EndingSummary.Of(CareerState s, ContentSnapshot c)`: ending reason, weeks played, dominant category (most streamed), reputation (`trusted`: credibility ≥ 70 and drama ≤ 30; `controversial`: drama ≥ 60; `steady` otherwise), final platform, banned platforms, peak follower milestone and the earned achievements with their points. Computed from state, never stored.
- The client composes the Polish narrative from templates keyed by the ending reason, the reputation and the dominant category, plus the earned achievement names.

## 8. API and minimal UI

- `CareerView` gains `Achievements: IReadOnlyList<AchievementView>` (`id`, `name`, `description`, `points`, `earned`; all definitions of the career's content, earned first) , `Score: ScoreView?` (F, W, A, the three contributions, base score, both multipliers, final score; set when ended) and `Summary: EndingSummaryView?` (set when ended). Stored responses written before C read with `achievements` `[]` and both new members null (TD-91 reader).
- Batch dialog: a length choice (4, 13, 52) and a collapsible strategy form with the §5 fields and their defaults; invalid input shows the field error before sending. Ending view: the narrative, the score table and the achievement list. Polish copy for owner review.
- The Playwright journey runs a 4-week batch with a non-default strategy.

## 9. Simulation

- Three strategy policies join the `m3-systems.2` list: `Strategy-Default` (the §5 defaults), `Strategy-Safe` (risk `safe`, rest below 50, reserve 2,000 PLN, no wager, routine `noAction, manageCommunity, trainCategory`) and `Strategy-Bold` (risk `bold`, focus Gambling, wager `low`, routine `noAction, networkCollaborate`; tuned 2026-10-26, proposed: wager `medium` and the side business).
- The report gains "Achievements and strategies": the share of careers earning each achievement, the distribution of `A`, and per strategy the medians. Criteria:
  - **Strategies are viable:** each strategy policy avoids insolvency in ≥ 90 % of careers.
  - **Strategy choices matter:** Strategy-Safe and Strategy-Bold differ by at least 10 points of median final drama.
  - **Achievements separate careers:** Balanced's median `A` is below 1, and at least six achievements are earned by between 5 % and 95 % of all careers.
- Achievement thresholds and points are tuned against these criteria (at most five rounds; a criterion still failing becomes an M5 follow-up).

## 10. Rulings

1. Event-response priorities are deferred (§3); risk appetite covers the gamble-or-safe choice. Cost: one gameplay §8 field missing until M5.
2. Strategies are remembered in the browser per career and recorded in each batch request, not stored as server objects. Cost: a strategy does not follow the player to another browser.
3. Ranked eligibility moves to #27 (leaderboard), because no career is ranked before M4. Cost: #23's scope text changes.
4. Achievements are content data, so the new set changes only careers whose content snapshot has the section; no new ruleset ID is needed (the snapshot is pinned per career).

## 11. Delivery

Two PRs into `develop` from `feature/m3c-*` branches:

1. **Rules, content and simulation** (`feature/m3c-1-rules`): this spec and its decision records, `Strategy` and `StrategyPolicy`, the achievements section, metrics, rules and validator, `EndingSummary`, ending tests, content, the simulator section, the M3-C report and its tuning.
2. **API and play** (`feature/m3c-2-play`): batch weeks and strategy in the contract, career view members, stored-response defaults, OpenAPI, frontend dialog and ending view, Playwright journey, documentation close-out.

Issues #22 and #23 close with the M3 release into `main`.

## 12. Acceptance criteria → evidence

| Criterion | Evidence |
| --- | --- |
| #22 Each week commits separately | Existing batch tests extended to 4 and 52 weeks |
| #22 A retry cannot duplicate batch actions | Repeat tests with a strategy; different strategy under the same ID is `command-id-reused` |
| #22 The log explains policy choices and fallbacks | `StrategyPolicyTests` for every reason code, including `fallback-safe` |
| #23 Domain computes the score | `ScoreCalculator` tests; achievement points from the section |
| #23 Every ending type has tests | `EndingFlowTests`, one per ending |
| #23 Score breakdown and multipliers visible | View tests; ending view test; Playwright |
