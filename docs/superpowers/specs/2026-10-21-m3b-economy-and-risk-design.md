# M3-B Economy and Risk — Design

Date: 2026-10-21
Status: **Agent proposal; rules and content delivered in PR 3 (2026-10-26), numbers tuned by the [M3-B baseline simulation](../../reports/m3b-baseline-simulation.md)**; the wager stakes are an owner decision (2026-10-26). The owner asked the agent to design sub-project B without a design interview ("zacznij sam", 2026-10-21). Every rule and number below is an agent proposal to validate in review and by the M3-B simulation; nothing here is a confirmed owner choice unless it cites the GDD.
Milestone: [M3 — Complete gameplay systems](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/3), sub-project B, delivering [#18](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/18), [#20](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/20) and [#19](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/19).
Builds on: [M3-A rules and content foundation](2026-10-16-m3a-rules-and-content-foundation-design.md) (ruleset `m3-systems.1`, schema-v2 events, mandatory decisions, decision carry, category profiles).

## 1. Intent

Sub-project B adds the money and risk layer the GDD promises: sponsors with real obligations, a fictional-currency wager on gambling streams, an optional text-only side business, and three platforms with different audiences, policy enforcement, migration and bans. Every one of them is a trade-off the player sees before committing; none is a hidden tax or a dominant strategy.

Success means:

- a player sees every sponsor term (payment, deadline, required content, platform, prerequisites, penalty) before accepting, and a missed obligation costs a stated penalty;
- every cash movement of a week, including sponsor payments, penalties, stakes, winnings and side-business income, is a report line, and the lines reconcile to the cash change;
- an unaffordable choice (stake, equipment, plan) stays visible and disabled with a reason;
- a wager uses in-game PLN only, shows stake, chance and payout first, and has no purchase or cash-out path;
- a career completes without ever opening the side business;
- a migration shows the retention range before the move and the exact follower count after it;
- a ban on one platform leaves a legal move to another accessible platform; a global ban, or bans on all three platforms, ends the career.

## 2. Confirmed inputs (from the GDD and specifications)

| Topic | Confirmed source |
| --- | --- |
| Platforms | Czatnia, PulsLive, Nurt; everyone starts on Czatnia; switching later (GDD §2, gameplay §7). |
| Platform factors | Reach / sub yield / ad yield / policy risk table in gameplay §7 (proposed defaults). |
| Migration | Only a fraction of followers moves, set by credibility and circumstances; retention `clamp(0.20, 0.80, 0.40 + credibility/250 − drama/500 + announcedSwitchBonus − banPenalty)`; switch after 26 weeks or after a permanent ban (gameplay §7). |
| Bans | A permanent ban from one platform permits migration; a global ban ends the career; bans on all three platforms end it too (GDD §2, gameplay §7). |
| Sponsors | One-off deals and multi-week contracts; terms visible before acceptance; at most one active contract per sponsor and two at once (gameplay §6). |
| Gambling | In-game PLN only; visible stake, chance and payout; no purchase or cash-out (GDD §2, content §2 adult themes). |
| Side business | Optional, text-only, weekly work, later income; affects burnout, credibility and sponsor/platform eligibility (GDD §2, gameplay §6). |
| Weekly loop | Exactly one main action per week; optional free selections do not add a second action (gameplay §5). |

## 3. Scope

**In scope**

- `Domain`: platform state, policy strikes, bans and migration; sponsor offers and contracts; the `PitchFulfillSponsor`, `SideBusinessWork` and `PlanPlatformMigration` actions; the wager plan option; new effect kinds and eligibility; two endings; validator rules; the ruleset `m3-systems.2`.
- `Application`/`WebApp`: the `respondOffer` command, plan fields for wager and migration target, career view and report fields; regenerated OpenAPI.
- Frontend (minimal playable UI): platform and strike panel, offers and contracts panel, the new actions and the wager selector on the plan screen, the migration estimate, the platform-ban decision, new report lines.
- `content/m3`: balance sections for platforms, sponsors, wagers, side business and policy; sponsor templates; about 15 new events.
- Batch simulator: new policies and the M3-B report.

**Out of scope**

- Saved player strategies, 4/52-week batches, new achievements and score changes (C). The browser batch keeps the `Balanced` policy, extended for B (§9).
- History, ending screen, diploma (D); outcome texts and decision names in reports (#82).
- The `Sensitive content` text filter and the Streamer Friendly mode (content spec; not scheduled in M3). B tags every adult-business and privacy event so the filter can be built later.
- Ad intensity as a plan option (gameplay §6 `adLoadChoice`): it would triple the plan list for little decision value; `adLoad` stays a balance constant.
- Appeals against bans (gameplay §7: future extension).

## 4. Ruleset and versioning

- `content/m3` moves to ruleset **`m3-systems.2`**. `M3Ruleset` takes its ID in the constructor and is registered twice (`m3-systems.1`, `m3-systems.2`). Every B rule reads an optional balance section; when the section is absent (a stored `m3-systems.1` snapshot) the rule is inert, so `m3-systems.1` careers keep their results.
- **Freeze first:** a golden test copies today's `content/m3` into `Tests/Golden/m3-systems.1/` and pins the final state hash of 20 seeds × the M3 policies × 1,040 weeks, exactly as the M1 golden test does. It stays green through the whole sub-project.
- New `CareerState` fields are nullable and omitted from canonical JSON when null, so M1 and `m3-systems.1` hashes never change.
- `m3-systems.1` never reached `main`; the registration is kept so careers created on `develop` or staging keep loading.

## 5. Platforms, policy and bans (#19)

### 5.1 Platform state and profiles

`CareerState.Platform` (`Czatnia`, `PulsLive`, `Nurt`), `PlatformSinceWeek`, `BannedPlatforms`, `Strikes` (per platform: active strike weeks) and `FormerFollowers` (a metric; former followers stay in history only).

`balance.platforms` (agent proposal; the first four columns are the gameplay §7 table):

| Platform | Reach | Sub yield | Ad yield | Policy risk | Category reach (others 1.00) |
| --- | ---: | ---: | ---: | ---: | --- |
| Czatnia | 1.00 | 1.00 | 1.00 | 1.00 | — |
| PulsLive | 1.20 | 0.85 | 1.10 | 1.30 | FPS 1.10, MOBA 1.10, Chess 0.90 |
| Nurt | 0.85 | 1.25 | 0.70 | 0.80 | Chess 1.20, Just Chatting 1.10, Gambling 0.80 |

The factors replace the scalar `platformReach`, `platformSubYield` and `platformAdYield` for the current platform; the category reach multiplies the category profile's reach. The plan screen and the migration action show the target's table row before a switch.

### 5.2 Policy strikes

- **Exposure** each settled week (balance `policy.exposure`): gambling stream 1, wager +1 per stake tier (§7), side business open 1, drama ≥ 70 adds 1.
- **Strike roll:** one seeded roll per settled week with exposure > 0: chance `exposure × 0.3 pp × platform policy risk`, capped at 6 % (tuned 2026-10-26; proposed 1.5 pp and 15 %). The report shows the roll and threshold; the plan screen shows the week's expected exposure and chance.
- Event effect `PolicyStrike` (1–2) adds strikes directly, for disclosed incidents (copyright, privacy, gambling promo).
- A strike expires 26 weeks after it was given. **Three active strikes ban the career from that platform permanently.**

### 5.3 Ban, forced move and endings

- A ban on the current platform queues the system decision **`platform-ban`** (crisis, ahead of every other decision). The ruleset builds its choices from `balance.platforms`: one guaranteed choice per accessible platform, each showing the retention range (§5.4) with `banPenalty` 0.10 applied. The ICU title and text live in `balance.platformBan`.
- If no platform is accessible after the ban, settlement ends the career with **`AllPlatformsBanned`**.
- Event effect `GlobalBan` ends the career at settlement with **`GlobalBan`**. Only `crisis` events may carry it, only on a failure or critical-failure outcome of a choice whose risk is shown, and the validator rejects it anywhere else.
- Both endings score like `Completed` (cash counts; insolvency still zeroes wealth). The final score model stays C's work.

### 5.4 Migration

- Action **`PlanPlatformMigration(target)`** (energy 6) is legal when the career has been on its platform for 26 weeks, the target is not banned and differs from the current one.
- The first use announces the move: flag `migration-announced:<target>` for 4 weeks, which adds `announcedSwitchBonus` 0.10, −1 credibility (the old audience notices). A second use within those 4 weeks executes it at that week's settlement. A move without an announcement happens only through the `platform-ban` decision (bonus 0).
- Retention `r = clamp(0.20, 0.80, 0.40 + credibility/250 − drama/500 + bonus − banPenalty)`; the executed move draws a seeded ±0.05 around `r` (still clamped). The plan shows the range `[r − 0.05, r + 0.05]`; the report shows the exact follower count, a `followers:migration` line and the lost followers added to `FormerFollowers`.
- The move resets `PlatformSinceWeek`, keeps strikes on the old platform (they matter if the player returns) and ends every contract tied to the old platform without penalty or further payment (§6.3).

### 5.5 Drama and credibility

Drama decays by 0.2 at settlement in a week that added no drama (tuned 2026-10-26; proposed 1); credibility is unchanged (it already needs explicit repair). Sponsor prerequisites (§6) use both: trusted sponsors need low drama, sensational sponsors need high drama, so neither route is the higher-scoring default.

## 6. Sponsors, equipment and revenue (#18)

### 6.1 Sponsor templates

`events.json` gains a top-level `sponsors` array (schema 2 stays; the snapshot keeps two files). A template has `id`, `version` (immutable like events, TD-112), ICU `name` and `pitch`, `kind` (`oneOff` or `contract`), `paymentPln` (base) and `paymentPerThousandFollowersPln` with a `paymentCapPln`, `termWeeks` and `dueEveryWeeks` (contracts), `requiredCategory` (optional), `platforms` (empty means every platform), prerequisites (`minFollowers`, `minCredibility`, `maxDrama`, `minDrama`), `incompatibleWith` (`gambling`, `adult-business`), `breach` (`cashPln`, `credibility`) and `weight`.

### 6.2 Offers

- After settlement, a seeded roll opens at most one offer: chance 8 % plus 2 pp per order of magnitude above 1,000 followers, capped at 20 %. The offer is chosen by weight among the templates whose prerequisites the career meets now.
- An offer waits in `CareerState.Offers` for 2 weeks and then expires. It never blocks a week or a batch.
- Command **`respondOffer(offerId, accept)`** between weeks is a free selection, not a main action. Accepting is refused with a reason when the career already has two contracts, an active contract with the same sponsor, or a prerequisite no longer holds.
- The `Industry Insider` background starts with one offer in its queue (the "sponsor lead" of gameplay §2).
- Action **`PitchFulfillSponsor`** (energy 8) fulfils a due contract task when one is due this week; otherwise it pitches: a roll with visible odds (base 35 %, +0.4 pp per credibility point above 50, burnout penalty) that opens an offer immediately on success.

### 6.3 Obligations, payments and breaches

- **One-off deal:** settles in the next stream. It pays `income:sponsor` when the stream's category matches `requiredCategory` (or none is required) and no incompatible activity happened that week; otherwise it is a breach.
- **Contract:** due weeks are every `dueEveryWeeks` weeks from acceptance until the term ends. A due week needs the main action `PitchFulfillSponsor` and, when set, the required category. A fulfilled installment pays `income:sponsor`; a missed one costs `cost:sponsor-penalty` (`breach.cashPln`, capped at the installment) and `breach.credibility`. Two missed installments terminate the contract with a further −5 credibility.
- **Incompatibility:** opening the side business, or a wager or a gambling stream, breaches every active contract that lists it, once, and terminates it.
- **Recovery crisis:** *take the week off* postpones a due task falling in that week by one week without penalty (the M3-A spec promised this for B).
- **Migration:** contracts tied to the old platform end without penalty (§5.4).
- The plan screen lists the week's due tasks; a plan that misses one shows the penalty it will cost before the player commits.

### 6.4 Equipment and revenue

Equipment (four slots, tiers, prices, upkeep) and the subscription, donation and ad lines already exist from M1/M3-A and meet #18 once sponsor lines join them. B adds no new equipment rules. The report groups lines into income, costs and net, and a property test checks that the lines reconcile to the cash change after any legal command sequence (existing test, extended to the new lines).

## 7. Gambling and the side business (#20)

### 7.1 Wager

- A plan with category `Gambling` has an optional `wager`: `None`, `Low` 100 PLN, `Medium` 500 PLN or `High` 2,000 PLN. A stake must not exceed 25 % of cash and needs positive cash; otherwise it stays visible and disabled (`needs-cash`).
- Chance 45 %, payout 2× the stake: a win adds `income:wager` (the net win), a loss adds `cost:wager`. The expected value is negative (−10 % of the stake), so wagering never pays on average.
- Spectacle: a win multiplies the stream's live viewers by 1.05, a loss by 0.85 (tuned 2026-10-26; proposed 1.15 and 1.05); drama +2 on a win, +3 on a loss.
- The stake tier raises policy exposure (§5.2) and breaches sponsors incompatible with `gambling` (§6.3).
- The plan screen shows stake, chance, payout, loss and the policy and sponsor consequences before the week starts. There is no purchase, top-up, cash-out or real-money text anywhere; a content test rejects such wording in gambling events.

### 7.2 Side business

- Action **`SideBusinessWork`** (energy 12, +4 burnout) is legal from week 52 with at least 2,000 followers. It is never required: every content choice and every policy path works without it.
- The first use opens the business (flag `side-business`); each use adds 15 catalog quality (cap 100). Quality decays by 3 per week; at 0 the business is dormant and stops paying.
- Income `income:side-business = round(followers × 0.0005 × quality/100 × 100)` PLN per week (scale tuned 2026-10-26; proposed 10) while open; −0.2 credibility per week while quality > 0; policy exposure +1 (§5.2); sponsors incompatible with `adult-business` stop offering and active ones breach (§6.3).
- All copy is non-explicit text about workload, pricing, audience and reputation, tagged `adult-business`.

## 8. Events, validation and content

- Eligibility gains `platforms` (empty means every platform) and `minStrikes`.
- New effect kinds: `PolicyStrike` (1–2), `GlobalBan` (crisis failure outcomes only), `SponsorOffer` (opens a named template's offer).
- Validator additions: a sponsor template has a legal path (a prerequisite set some career can meet, positive payment, a breach no larger than its payment cap); `GlobalBan` only in crisis failure outcomes; `SponsorOffer` names a known template; eligibility platforms are known.
- Content (agent-written Polish, satirical tone): 12 sponsor templates (4 trusted low-drama, 3 sensational high-drama, 3 category-specific, 2 platform-specific), 6 platform and policy events (copyright, gambling promo, privacy incident with a disclosed `GlobalBan` risk, PulsLive discovery, Nurt community, first-strike warning), 4 side-business events, 3 wager-related live events and 2 sponsor events that open a named offer (15 events). Every new message is ICU and passes the frontend cross-check.

## 9. Automation and simulation

- `Balanced` (the browser batch) gains: accept an offer when its prerequisites hold, it has no incompatibility with the career's activities and the category rule can meet it; `PitchFulfillSponsor` on due weeks ahead of training; no wager; never the side business; migrate only through the `platform-ban` decision, choosing the platform with the highest expected retention. Every choice records a reason code (`sponsor-due`, `offer-accepted`, `offer-declined`, `ban-move`).
- New simulator policies: `Gambler` (Gambling specialist with a `Medium` wager every stream it can afford), `SideHustler` (Balanced plus side-business work every fourth free week), `SponsorHunter` (Balanced plus pitching every third free week), `Migrator` (moves to PulsLive at week 26, announced).
- The M3-B report (`docs/reports/m3b-baseline-simulation.md`, 10,000 careers) checks:
  - **No side business needed:** Balanced completes 1,040 weeks without insolvency in ≥ 90 % of seeds.
  - **Gambling is not dominant:** Gambler's median final cash and median final score are below Balanced's, and its insolvency rate is reported.
  - **Side business is a trade-off:** SideHustler's median final cash is between 1.0× and 1.5× Balanced's.
  - **Bans are rare but real:** at least one Gambler or SideHustler career in 100 is banned from a platform; fewer than 2 % of all careers end with `GlobalBan` or `AllPlatformsBanned`.
  - **Sponsors matter:** SponsorHunter's sponsor income is at least 10 % of its total income, and its breach rate is reported.
  - **Reconciliation:** no week's report lines differ from its cash change (zero violations).
- Numbers in §5–§7 are tuned to meet these criteria before PR 3 merges; every change is logged in the report's tuning log and in the gameplay specification.

## 10. API and minimal UI

- `WeekPlan` gains `Wager` and `TargetPlatform`; `PlanOptionView` carries the wager and migration estimates and the due-task warning.
- `CareerView` gains `platform`, `strikes`, `bannedPlatforms`, `offers`, `contracts` and `sideBusiness`; `WeekReportView` gains the strike roll, wager and migration lines (cash lines already flow through income and costs).
- New action type `respondOffer` (stored in the action log as kind `RespondOffer`, idempotent by command ID like every command); the ban decision uses `resolveDecision`.
- Stored responses written before the change load through the TD-91 reader (`StoredContract`).
- Frontend: a platform card (platform, strikes, banned platforms), an offers and contracts card with accept/decline, the new actions and wager selector on the plan screen with disabled reasons, the migration estimate, report lines; Polish copy for owner review. The Playwright journey accepts an offer and plays past it.

## 11. Delivery

Four PRs into `develop` from `feature/m3b-*` branches:

1. **Foundation:** this spec and the decision records, the `m3-systems.1` golden freeze, the ruleset ID parameter and `m3-systems.2`, state fields, schema additions (`sponsors`, eligibility, effect kinds) and validator rules.
2. **Economy rules:** offers, contracts, breaches, `PitchFulfillSponsor`, the wager, the side business, report lines.
3. **Platform rules, content and simulation:** platforms, strikes, bans, migration, endings, drama decay, the new content, simulator policies, the M3-B report and its tuning.
4. **API and play:** contract, commands, view, frontend, Playwright journey, documentation close-out.

Issues #18, #19 and #20 close with the M3 release into `main`.

## 12. Decision records and documentation

- GDD decision log: this design as an agent proposal (owner opted out of the interview); wager stakes and odds; side-business eligibility; three strikes per platform; offers as a free selection; ad intensity left out.
- Technical decisions: ruleset `m3-systems.2` with a parameterised `M3Ruleset` and inert optional sections; the `m3-systems.1` golden freeze; sponsors inside `events.json`; the ruleset-built `platform-ban` decision.
- Gameplay systems: a §12 for `m3-systems.2` with the final numbers; §6 and §7 point to it.
- Content and experience §2: the `sponsors` array, the new eligibility and effect kinds, tags.
- Requirements traceability: the rows for economy, gambling, side business, platforms and bans.

## 13. Acceptance criteria → evidence

| Criterion | Evidence |
| --- | --- |
| #18 Every cash movement appears in the report | Reconciliation property test; simulation zero violations |
| #18 Contract breaches have consequences | Domain tests for missed installment, termination and incompatibility |
| #18 Unaffordable options remain visible but disabled | View tests for equipment, stakes and offers; frontend tests |
| #20 No real-money payments or cash-outs | No such command or endpoint; content wording test; architecture review |
| #20 A player can finish without the side business | Balanced simulation criterion; no content choice requires it |
| #20 Risks are disclosed before choices | Plan view shows stake, chance, payout, exposure and sponsor consequences; tests |
| #19 Migration displays and applies follower retention | Plan range and report exact-count tests |
| #19 A single-platform ban allows a legal move when available | `platform-ban` decision tests |
| #19 A global ban ends the career | `GlobalBan` and `AllPlatformsBanned` ending tests |

## 14. Risks

- **The economy runs away or starves careers.** Every number is tuned against §9 before PR 3 merges.
- **Too many interruptions.** Offers never pause a batch; only the ban decision does, and bans are rare by criterion.
- **Plan list growth.** Wager tiers apply only to Gambling plans and migration targets only after 26 weeks; the view groups options per action.
- **Changing `m3-systems.1` results by accident.** The golden freeze runs after every task.
- **Owner disagrees with a proposal.** Every rule is a balance value or a small isolated rule, so a change is data or one function.

## 15. Plan-time rulings (2026-10-21)

Agent proposal/default, recorded with the implementation plan (`docs/superpowers/plans/2026-10-21-m3b-economy-and-risk.md`, "Plan-time rulings"); they refine the sections named and are recorded in TD-117.

- §3/§4: the optional balance sections are `platforms`, `migration`, `policy`, `sponsors`, `wagers`, `sideBusiness` and `platformBan`; a rule whose section is absent does nothing and draws nothing.
- §5.2: wager exposure is 1 (Low), 2 (Medium), 3 (High). Exposure and the strike chance are computed from the plan and the state before the week starts, so the plan screen shows the figures that are rolled.
- §5.3: bans are evaluated only at settlement, after the strike roll. `GlobalBan` sets `sys:global-ban`; the career ends at the next settlement. Ending order: insolvency, global ban, all platforms banned, completion.
- §5.4: retention is computed from the state before the week and frozen with its ±0.05 draw at week start; the move executes at that week's settlement after the audience change. A `platform-ban` move switches the platform at once and applies the retention at the next settlement.
- §6.2: offer chance uses integer orders of magnitude (`followers ≥ 1,000 × 10^k`); no draw when no template is eligible; payment is fixed when the offer opens. The pitch is a balance choice with base 15 % + 0.4 points per credibility point (35 % at 50) and is disabled (`no-sponsor-fits`) when no task is due and no template fits. Offer IDs are `{templateId}:{week}`; one open offer per template.
- §6.3: the first installment is due `dueEveryWeeks − 1` weeks after the week of acceptance and then every `dueEveryWeeks` weeks through `termWeeks`; one `PitchFulfillSponsor` fulfils every installment due that week; a week off moves the due installment and the term end one week. A contract is tied to a platform when its template lists platforms.
- §7.1: a stake must fit 25 % of spendable cash (cash, minus a negative decision carry, minus the plan's equipment price); no wager is `null` rather than `None`.
- §6.3 (implementation, PR 2, 2026-10-22): a week off postpones a one-off deal due that week as well as a contract installment, because the recovery crisis can force the week off and §6.3 promises "a due task" moves without penalty. A breach by incompatibility ends the deal with the breach penalty and credibility only; the extra −5 belongs to the second missed installment. A pitch with an installment due fulfils it without a roll; the offer opened by a successful pitch is chosen among the templates that fit after the pitch's own effects.
- §9: Migrator announces in week 27 (its first legal week) and moves in week 28. Retention does not depend on the target, so Balanced's `ban-move` prefers an announced target, then higher reach.
