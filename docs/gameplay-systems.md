# Gameplay and Balance Specification

Status: **approved prototype baseline, subject to balancing**, 2026-10-12. Product direction comes from the [game design document](game-design-document.md). The owner approved the numerical defaults in this file for the first prototype; they remain tunable through simulation and player tests before the ranked ruleset is frozen. Currency is fictional PLN and never converts to real money.

The owner can edit named, bounded numeric balance constants and event definitions in `/admin` as drafts. Constants include starting values, costs and income yields, growth and risk factors, platform coefficients, thresholds, score normalization and difficulty/background multipliers. The server validates and publishes them as immutable versions consumed by the C# `Domain` engine. A career remains pinned to the version selected when it starts. Adding/removing an event or changing eligibility, odds, effects or numeric constants changes mechanics and therefore starts a new leaderboard season for new ranked careers. Published events remain available for older careers even when archived from future versions. Text-only event corrections create a content revision without changing mechanics.

## 1. Career and Turn Contract

A career starts at age 18, week 1, and ends after week 1,040 (age 38), voluntary retirement, insolvency at balance **≤ −1,000 PLN**, or a global ban / no accessible streaming platform. A ban on one platform alone leaves the career running and prompts migration. An ended career is read-only; the player can view its history, summary, diploma, and ranking eligibility.

Each week has a deterministic order:

1. Apply recurring effects and surface a mandatory milestone or crisis before the player commits the week.
2. Choose exactly one main between-stream action. `No action` is valid. The action resolves before the stream and can create an opportunity or modify stats.
3. Choose a primary category, eligible sponsorship commitment, and available optional stream modifier (for example a wager). A stream can make one event-driven category switch; the original choice remains the primary category for skill/boredom accounting.
4. Resolve two or three untimed live-chat situations in manual mode. Each presents consequences and a success probability before commitment, then the roll, modifiers, and result. A no-choice flavor event may appear but does not count toward the two or three.
5. Settle viewers, follower change, all income and costs, contracts, skill/boredom, energy/burnout, and expiring effects. Generate one detailed weekly report. Check ending conditions. Append the action and outcome to history and autosave atomically.

An action is never silently skipped because the week is automated. The game uses the player's saved action policy for every simulated week. A mandatory choice pauses a batch **before** consuming that week. Dismissing a report or closing the browser cannot reroll a completed week. Every 52nd settled week produces an annual review; it pauses a batch only when it includes a mandatory choice. **Proposed pacing guardrail:** routine batches should require no more than one mandatory interruption per 13 simulated weeks on average, excluding emergencies created by player policy.

### Manual versus automated play

Manual play offers every live choice. `Auto-resolve this week` executes the same turn with the saved strategy and still exposes the full report. Batch options are 4, 13, and 52 weeks. A batch shows a compact ledger, milestone highlights, and a button to inspect any week. It stops before a mandatory story choice, platform switch after a ban, insolvency/end, retirement prompt, or other irreversible event. It also stops after a critical failure that requires a response. Routine choices use the policy and do not interrupt.

The 1–2-hour full-career target assumes a mix of selected manual weeks and batch automation. **Proposal:** target 25–60 manual weeks, with the rest batched; measure rather than impose this ratio. The UI never claims that 1,040 fully manual weeks fit the time target.

## 2. Persistent State and Invariants

| State | Proposed range / meaning | Invariant |
| --- | --- | --- |
| `week` | 1–1,040 | Advances once per settled stream. |
| `cashPln` | Integer PLN, may be negative | Spend actions require available cash; insolvency at ≤ −1,000 after settlement. |
| `followers` | Reachable followers on current platform | Nonnegative integer; switching migrates a fraction, former-platform audience is recorded historically but no longer contributes to current reach or final score. |
| `liveViewers` | Per-stream average concurrent viewers | Nonnegative integer; recorded per week, never treated as followers. |
| `drama`, `credibility` | 0–100, separate | Neither is a direct score input; both affect gates, risk, and outcomes. |
| `energy`, `burnout` | 0–100 | High burnout lowers performance and may trigger a forced recovery event. |
| `skill[category]` | 0–100 for five launch categories | Practice gains diminish; disuse decays slowly. |
| `boredom[category]` | 0–100 | Repetition raises it; breaks lower it. Specialization remains possible. |
| `platform`, `bannedPlatforms` | One active of three, zero or more bans | Active platform cannot be banned. |
| `equipment` | Microphone, camera, computer, connection, each tier 0–4 | Bought equipment persists; upkeep is separate. |
| `contracts`, `adultSideBusiness`, `achievements` | Versioned records | Contract and side-business obligations are explicit in the weekly report. |

Track cumulative career metrics separately: total streams, total views, gross and net earnings by source, category history, sponsor fulfillment, incidents, platform moves, achievements, and ending reason. Gender selection affects only the character's pronouns in player-facing text. It cannot alter event selection, dialogue content, state transitions, random odds, or scores.

### Starting character — proposed profiles

All start on **Czatnia** (working fictional name), with 0 drama, 50 credibility, 70 energy, 0 burnout, and basic equipment unless shown below. Names and numbers are reviewable.

| Background | Opening advantage | Drawback | Proposed score multiplier |
| --- | --- | --- | --- |
| Dorm Room Newcomer | 2,000 PLN; +8 FPS skill | 0 followers; weak equipment | 1.00 |
| Local Meme Creator | 600 followers; +8 Just Chatting skill | 20 drama; 42 credibility | 0.96 |
| Chess Club Regular | +15 Chess skill; +8 credibility | 500 PLN; slow early discovery | 0.98 |
| Industry Insider | 1,500 followers; 5,000 PLN; one sponsor lead | 15 drama; higher opening upkeep | 0.90 |

The user approved these four background concepts as the test baseline. They are asymmetric starts, not intended to force a particular career route. Their numeric modifiers remain proposed. The multiplier is displayed at creation and in the score breakdown. Difficulty is chosen separately and does not change a background's label.

## 3. Stream Performance and Audience

The following formulas define a **first balancing model**, not final tuned numbers. All random draws come from the seeded simulation engine. Floor at zero and round only at the named output boundary.

`quality = clamp(0, 100, 25 + 0.35 × primarySkill + 4 × equipmentAverage + eventQuality - burnout × 0.20 + energy × 0.10)`.

`baselineViewers = max(5, (50 + 1.5 × primarySkill + 35 × sqrt(followers) + 0.025 × followers) × platformReach × (0.6 + quality/100))`.

`liveViewers = round(baselineViewers × categoryTrend × (1 - boredom/250) × eventAudienceMultiplier × seededNoise(0.85..1.15))`.

`newFollowers = round(liveViewers × conversionRate × credibilityFactor × categoryGrowthFactor + viralBonus - churn)`, with `conversionRate` proposed at 0.03–0.08 per weekly stream. The square-root term in discovery allows growth from a zero-follower start while the linear term helps established creators grow; both require calibration against 1,040-week simulations. Churn depends on boredom, repeated missed commitments, prolonged inactivity, and negative incidents. The weekly report shows *gross gained*, *lost*, and *net change* separately. Trend and growth factors vary by category and platform, but no category is intrinsically a losing choice.

Viral clips can result from high quality **or** entertaining failure. A viral result has a published chance before a risky player choice, then produces a bounded one-week viewer spike and a smaller follower gain. Critical success and failure should change the narrative, not multiply the entire career trajectory without limit. **Proposed caps:** at most 3× normal viewers for one stream and at most 10% of current followers as immediate viral gain (minimum 100 for small careers). Tune after simulation.

Skills gain `max(0.2, (100 - skill)/100) × practiceFactor` for a streamed category; recurring use never reaches zero growth. An unused category loses at most 0.1 point per week after 13 consecutive inactive weeks, with a floor at its starting baseline. Consecutive repetition increases boredom by 2–4; switching category decreases boredom in the unused category by 1–2. A specialist can counter boredom through improved production, events, collaborations, and occasional related variations.

## 4. Choices, Odds, and Difficulty

Every chance-bearing choice computes its probability from visible factors: base chance, relevant skill, equipment, energy/burnout, credibility/drama when relevant, platform modifiers, and temporary effects. `p = clamp(0.05, 0.95, base + sum(modifiers))`. The screen shows the rounded percent and a short modifier list before commitment. A seeded integer roll from 1–100 succeeds if `roll ≤ round(100p)`; the result screen shows the roll, threshold, and modifiers. Reserve roll 1 for critical success and 100 for critical failure **only when the choice permits critical outcomes**. Thus a critical state never contradicts the displayed success threshold. Choices without randomness say `Guaranteed` and explain their deterministic cost.

**Proposed difficulty defaults:** easy adds +10 percentage points to risky choices and reduces ordinary upkeep by 15%; normal uses baseline odds and costs. Final score multiplier: easy 1.00, normal 1.10. The report and ending show both. Difficulty cannot change mid-career. Rare events and failure modes remain present on easy.

Event choices must display any *known* fixed cost, possible downside, and affected stats. Hidden story consequences are allowed only when they are narrative reveals; the event's mechanical risk class remains visible. There are no real-time decisions or reflex checks.

## 5. Weekly Action Catalog

Exactly one main action may be chosen each week, including during auto batches. A regular stream costs a proposed 8 energy and adds 3 burnout after the chosen action; unusually demanding events can add more. Rest applies before that cost. At energy 0 or burnout 100, the next week opens with a mandatory recovery choice that may cancel a sponsor obligation. This prevents permanent zero-energy play without removing the weekly decision:

| Action | Main effect | Constraints and trade-off |
| --- | --- | --- |
| Rest | +25 energy, −12 burnout | Forfeits a growth, business, or equipment action. |
| Train category | +2–4 skill in chosen category | −8 energy; diminishing at high skill. |
| Improve production | Buy or maintain one equipment tier | Requires cash; tier prices and upkeep rise. |
| Network/collaborate | Chance of audience crossover or creator relationship | Can cause drama or credibility loss if handled poorly. |
| Pitch/fulfill sponsor | Find an offer or fulfill a contract task | Missing a due task has contractual penalty. |
| Manage community | Raise credibility or reduce drama/boredom | Small immediate audience effect. |
| Adult side business work | Open or update the fictional text-only side business | Requires eligibility and energy; creates future income and reputation/policy exposure. |
| Plan platform migration | Announce/execute a switch when eligible | Audience retention varies; old contracts may be affected. |
| No action | Recover +5 energy | Opportunity cost; cannot fulfill a due active task. |

Optional free selections before stream (category, ad intensity, accepted sponsor offer, event response) do not create a second main action. One-off sponsor offers can be accepted as a pre-stream commitment; multi-week contracts may require weekly `Pitch/fulfill sponsor` actions on explicit due weeks. An action that is unaffordable remains visible with a disabled state and reason.

## 6. Economy and Obligations

All amounts are integer PLN. Income per settled week:

`subscriptions = round(liveViewers × paidConversion × platformSubYield)`;
`donations = round(liveViewers × donationYield × eventFactor)`;
`ads = round(liveViewers × platformAdYield × adLoadChoice)`;
`sponsors = sum(fulfilledOneOffPayments, dueContractPayments)`;
`adultSideIncome = round(followers × sideConversion × sideCatalogQuality)`;
`costs = livingCost + equipmentUpkeep + platformFees + contractPenalties + optionalWagers + incidentCosts`.

The game is free and contains no commercial ads; `ads` above are a fictional in-career income source. Show gross revenue, each cost, and net balance change in the report. **Proposed opening values:** living cost 100 PLN/week; tier-0 equipment upkeep 10 PLN/week; early streams should usually cover part, but not all, of costs until audience grows. Larger audiences increase earnings and scale up operating costs, so runaway cash is bounded by upkeep and diminishing conversions.

Sponsor offers state payment, term, required content, relevant platform, deadline, credibility/drama prerequisites, and breach penalty before acceptance. A one-off deal settles in its nominated stream. A multi-week contract reserves named due weeks, pays only fulfilled installments, and applies a capped penalty for missed obligations. At most one active multi-week contract per sponsor and at most two simultaneous contracts overall are proposed to keep weekly decisions readable.

Gambling is an optional stream activity using **only in-game PLN**. A wager requires an affordable stake, has visible probability and payout, and can alter live audience, drama, credibility, platform enforcement, and sponsor compatibility. No purchase, cash-out, or real-money transfer exists. Optional adult creator work is text-only; initial setup and content updates consume weekly actions, then the side business yields later income while active. It can modify burnout, credibility, and sponsor/platform eligibility. It never displays nudity or explicit sexual acts.

When cash is negative, all spend options are disabled; non-spend recovery actions remain available. Apply insolvency at the **end of settlement**, after all earned income and required costs for that week; at ≤ −1,000 PLN the career ends. End-of-week events cannot create hidden charges after the insolvency check.

M3-B rules for sponsors, the wager and the side business, with their tuned numbers: [§12](#12-m3-b-ruleset-m3-systems2).

## 7. Drama, Credibility, Platforms, and Bans

Drama measures public controversy; credibility measures trust. A provocative event can raise both audience and drama while lowering credibility, but consequences depend on context. Drama decays slowly when not fed; credibility requires sustained reliable conduct or explicit repair. Both remain in 0–100. Low drama opens trusted collaborations; high drama opens sensational opportunities with larger upside and policy risk. Neither route should be a guaranteed higher-scoring strategy.

**Platform profiles:** Czatnia has balanced reach/earnings and is the common start; PulsLive offers stronger discovery and stricter policy enforcement; Nurt offers slower reach, better direct support, and a niche/community audience. These three names are confirmed. Platform differences are table-driven so balancing does not fork the simulation rules.

| Platform | Proposed reach factor | Proposed sub yield factor | Proposed ad yield factor | Proposed policy risk factor |
| --- | ---: | ---: | ---: | ---: |
| Czatnia | 1.00 | 1.00 | 1.00 | 1.00 |
| PulsLive | 1.20 | 0.85 | 1.10 | 1.30 |
| Nurt | 0.85 | 1.25 | 0.70 | 0.80 |

These factors are initial balance defaults. Category-specific audience preferences and rule details belong in versioned platform data and must be shown before switching. A switch becomes available after a proposed 26 weeks or after a permanent ban, provided another platform is accessible. Its retention proposal is `clamp(0.20, 0.80, 0.40 + credibility/250 - drama/500 + announcedSwitchBonus - banPenalty)`. Show the estimated retention range and then the exact resulting follower count. Former followers stay in history but are no longer current followers.

M3-B rules and tuned numbers for platforms, exposure, strikes, bans, migration and drama decay: [§12](#12-m3-b-ruleset-m3-systems2).

Permanent platform bans require a clear escalating rule path or a rare severe incident with a disclosed risk. A global ban is a distinct terminal outcome; it is not inferred merely from one platform's ban. If all three platforms are permanently unavailable, the career also ends. Appeals are a possible future extension, not a launch recovery path.

## 8. Automation Policy

A strategy is a versioned object with: category rotation (specialize or weighted mix), primary weekly action sequence, sponsor acceptance threshold, financial reserve, risk appetite, drama target, rest threshold, and event-response priorities (growth, cash, credibility, safety). The UI previews example choices and explains ties. Automation chooses only legal options; if no option matches, it selects the least-risk legal option and records a fallback reason. It never silently spends beyond the reserve unless the player has explicitly allowed emergency spending. Major event pauses override policy.

**M2 slice (TD-90).** The slice offers only the 13-week batch, played by the fixed `Balanced` policy instead of a saved strategy. Every plan and event choice records a stable reason code with its values (for example `rest-low-energy` with `energy` and `threshold`), which the client localizes. The plan reasons are `forced-rest` (`energy`, `burnout`), `train-every-week`, `rest-low-energy` (`energy`, `threshold`), `rest-high-burnout` (`burnout`, `limit`), `upgrade-equipment` (`slot`, `pricePln`, `cashPln`), `train-scheduled` (`everyWeeks`), `network-scheduled` (`everyWeeks`; M3 `Networker` only), `manage-community-drama` (`drama`, `limit`), `no-action-default` and `fallback-rest` (`preferred`; in M3 also every plan of a week the recovery crisis limits to `rest-only`); every plan reason also carries `category` and `categoryRule` (`bonus`, `least-bored`, `rotation` or, for the M3 category specialists, `fixed`). The event-choice reasons are `gamble-best-odds` (`probability`, `threshold`), `take-guaranteed` (optional `bestProbability`) and `only-gambles` (`probability`). M3-B adds the plan reasons `sponsor-due`, `wager-scheduled`, `side-business-scheduled`, `pitch-scheduled` and `migration-scheduled`, the category rule `sponsor-safe`, the event-choice reason `ban-move` and the offer reasons `offer-accepted` and `offer-declined` ([§12](#m3-b-simulation-policies)). Values are strings: fixed-point numbers with six decimals (`20.000000`), integers in invariant form and enum values in camelCase. The batch stops before a week that needs the player; the M1 rules have no such week, so the stop is exercised by a test ruleset until M3 adds mandatory decisions.

**M3-C (agent proposal, [design](superpowers/specs/2026-10-26-m3c-automation-and-endings-design.md) §4–§5).** Batches run 4, 13 or 52 weeks under an optional player strategy (category plan, weekly routine, rest thresholds, cash reserve, risk appetite, sponsor acceptance, wager, side business); without one the batch plays `Balanced`. Event-response priorities are deferred to M5. The tuned values and reason codes are in [§13](#13-m3-c-automation-and-endings).

Batch runs are incremental commits, not one opaque transaction: each completed week is recorded and autosaved. Canceling a batch leaves already completed weeks intact. Deterministic random draws and policy choices are included in the ranked action log. The manual and automated paths call the same simulation functions.

## 9. Achievements, Endings, and Ranking Score

Achievements are earned from varied career milestones (for example first 10,000 followers, successful platform move, long trusted sponsorship, category mastery, survival after a scandal). They may unlock narrative templates or cosmetics but do not give hidden score multipliers. **Proposed score model:**

`F = clamp(0, 1, log10(1 + finalFollowers) / log10(1 + 1,000,000))`

`W = clamp(0, 1, log10(1 + max(0, finalCashPln)) / log10(1 + 10,000,000))`

`A = min(1, earnedAchievementPoints / 10)`

`baseScore = round(100,000 × (0.45F + 0.45W + 0.10A))`

`finalScore = round(baseScore × difficultyMultiplier × backgroundMultiplier)`.

The user approved the 45% / 45% / 10% component split as the starting balance model. Follower and wealth components have equal maximum contribution after log normalization; achievements contribute at most 10%. Any bankruptcy cash counts as zero wealth. The end screen shows `F`, `W`, `A`, each contribution, both multipliers, and the total. This formula deliberately avoids awarding score directly for drama. Normalization thresholds and multiplier sizes remain proposed balance constants; simulate diverse careers before freezing them for ranked play. Scores tie by fewer weeks used, then earlier verified submission time, then stable run ID order.

The ending uses templates driven by actual achievements, platform history, dominant category, reputation, and ending reason. The user confirmed a downloadable PDF diploma generated locally with `pdf-lib` from the server's final, read-only report for the first release. A ranked leaderboard entry is allowed only for an ended, server-authoritative ranked career; a failed or early-retired career may publish its lower score. The leaderboard holds the best 100 eligible scores globally **within the career's season**; past seasons remain viewable separately. Ranked careers cannot restore manual slots or import state.

## 10. M1 Prototype Ruleset (`m1-prototype.1`)

Status: **implemented for simulation only**, 2026-10-13. Milestone 1 implements the minimal mechanics core ([TD-75](technical-decisions.md)) in the C# `Domain` as ruleset `m1-prototype.1`, reading every number from the versioned content snapshot [`content/m1/balance.json`](../content/m1/balance.json) and [`content/m1/events.json`](../content/m1/events.json) ([TD-76](technical-decisions.md)). Values from the sections above apply unchanged where they exist (Czatnia factors 1.00, quality/viewer formulas, action effects, 45/45/10 score, insolvency at ≤ −1,000 PLN). The values and simplifications below fill gaps in those sections; they are **Agent proposals** recorded as [TD-83](technical-decisions.md) and remain subject to the balance work in milestone 5.

### Constants added for M1 (Agent proposal)

| Constant (`balance.json` key) | M1 value | Use |
| --- | --- | --- |
| `equipmentPricePln` | 0 / 600 / 1,500 / 3,500 / 8,000 PLN for tiers 0–4 | Price paid by `Improve production` to reach the next tier of one slot. |
| `equipmentUpkeepPerTierPln` | 10 / 20 / 35 / 55 / 80 PLN per slot per week for tiers 0–4 | Weekly upkeep summed over the four slots (tier 0 matches the §6 opening value). |
| `manageCommunityCredibility` / `manageCommunityDramaRelief` / `manageCommunityBoredomRelief` | +3 credibility, −4 drama, −5 boredom in the primary category | `Manage community` effects (all clamped to 0–100). |
| `conversionRate` | 0.05 | Follower gain per live viewer, scaled by `credibility / 50` (within the §3 range 0.03–0.08). |
| `churnRate` / `churnBoredomFactor` | 0.004 + 0.0001 × primary-category boredom | Weekly fraction of followers lost. |
| `paidConversion`, `subPricePln` | 0.02, 10 PLN | Subscriptions: `round(live × 0.02 × platformSubYield × 10)`. |
| `donationYield` | 0.15 PLN per live viewer | Donations (event factor 1 in M1). |
| `adYield`, `adLoad` | 0.05 PLN per live viewer, 1.0 | Ads: `round(live × 0.05 × platformAdYield × adLoad)`. |
| Base skill | 10 in every category | Starting skill before background bonuses (Dorm FPS 18, Meme Just Chatting 18, Chess 25). |
| Local Meme Creator | 1,000 PLN; equipment tier 1 in every slot | Starting cash and equipment not given in §2. |
| Chess Club Regular | 0 followers; equipment tier 1 in every slot | Starting followers and equipment not given in §2. |
| Industry Insider | all skills 10; equipment tier 2 in every slot | No skill bonus; the sponsor lead is deferred with sponsors (M3). |
| `eventsPerWeekMin` / `eventsPerWeekMax` | 2 / 3 | Live events drawn per manual week. |

### M1 rules and simplifications (Agent proposal)

- **Week order:** `StartWeek` validates and applies the main action (and pays an equipment purchase immediately), then draws the event count and picks distinct events by weight among those eligible and not cooling down. Each live event is presented (no random draw) and resolved with a 1–100 roll unless the choice is guaranteed. `SettleWeek` applies stream fatigue (−8 energy, +3 burnout), computes quality, live viewers and followers, itemises income and costs, applies skill and boredom changes, updates metrics and achievements, then checks endings.
- **Forced rest (M1 only; M3 uses the recovery crisis, §11):** at energy 0 or burnout 100 the only legal main action next week is `Rest`. This replaces the mandatory recovery choice of §5 until sponsor obligations exist (M3).
- **Event effects:** stat deltas apply immediately (clamped to 0–100); cash, audience multiplier and event quality accumulate and apply at settlement. A resolved event cannot be selected again until `week + cooldownWeeks`. The five synthetic M1 events use a one-week cooldown because the content specification's 13-week default would starve a five-event pool; authored launch content follows the content specification.
- **Easy difficulty:** +10 percentage points on risky choices (listed as the `difficulty.easy` modifier) and −15% on living cost and equipment upkeep.
- **Skill decay:** an unused category loses 0.1 skill per week from its 14th consecutive inactive week, never below the background's starting skill.
- **Endings:** insolvency (cash ≤ −1,000 PLN after settlement) takes precedence over completion in week 1,040. An ended career keeps its last settled week and rejects every command.
- **Score rounding:** each component's points are rounded separately (`round(100,000 × weight × component)`), and the base score is their sum; this exposes every contribution exactly and differs from rounding the weighted sum by at most one point.

### M1 achievements (Agent proposal)

| ID | Condition | Points |
| --- | --- | ---: |
| `followers-1k` | Reach 1,000 followers at any settlement | 1 |
| `followers-10k` | Reach 10,000 followers | 2 |
| `followers-100k` | Reach 100,000 followers | 3 |
| `career-completed` | Settle week 1,040 | 4 |

Achievements are never revoked when followers later fall. Early retirement evaluates follower milestones before scoring.

### M1 simulation policies (Agent proposal)

The batch simulator and tests use five deterministic policies; all play through the same engine commands as manual play and never retire early.

| Policy | Behaviour |
| --- | --- |
| Balanced | Rest when energy < 30 or burnout > 70; otherwise buy the lowest-tier slot when cash exceeds 3× its price; train every fourth week; manage the community when drama > 40; otherwise no action. Streams the background's highest starting skill, switching to the least-bored category while that category's boredom is ≥ 60. Takes the most likely gamble when its probability is ≥ 50%, otherwise the guaranteed response. |
| Grinder | Trains the bonus category every week; rests only when forced. |
| RestHeavy | Balanced, but rests whenever energy < 60. |
| Specialist | Balanced on the bonus category for the whole career. |
| Rotator | Balanced with the primary category rotating by week number. |

### M3 simulation policies (Agent proposal)

For `m3-systems.1` content the batch simulator defaults to ten policies (`BuiltInPolicies.M3All`, spec 2026-10-16 §8): Balanced, Grinder, RestHeavy and Rotator as above, plus:

| Policy | Behaviour |
| --- | --- |
| Networker | Balanced, but in every third week (`week mod 3 = 0`) with energy ≥ 40 it plays `NetworkCollaborate` when no rest rule fires (reason `network-scheduled` with `everyWeeks`). |
| Specialist-{Category} | One per category: Balanced on that fixed category for the whole career, however bored (category rule `fixed`). |

The M1 `Specialist` (bonus category) stays in the M1 list only. In a week the recovery crisis limits to `Rest` (`rest-only`), every policy rests with reason `fallback-rest`.

## 11. M3-A Ruleset (`m3-systems.1`)

Status: **Agent proposal, tuned by the M3-A simulation**, 2026-10-17. Sub-project A of M3 ([design](superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md), [TD-110](technical-decisions.md)) adds the C# ruleset `m3-systems.1` next to `m1-prototype.1`. Every number below is read from [`content/m3/balance.json`](../content/m3/balance.json) (version `m3.1`); the rest of this document applies unchanged where §11 is silent. New careers start on `m3-systems.1`; M1 careers keep their rules. The criteria and the tuning log are in the [M3-A baseline simulation report](reports/m3a-baseline-simulation.md).

### Category profiles

Each category has a profile in `balance.json` `categories`. Reach scales live viewers; noise is the seeded viewer multiplier range; conversion and churn scale follower gain and loss; the yields scale subscription, donation and ad income per live viewer; the side effects apply once per stream.

| Category | Reach | Viewer noise | Conversion | Churn | Boredom gain | Subs / donations / ads | Skill weight in quality | Credibility / drama per stream | Base viral chance |
| --- | ---: | --- | ---: | ---: | --- | --- | ---: | --- | ---: |
| Just Chatting | 1.00 | 0.85–1.15 | 1.15 | 1.00 | 2–4 | ×0.90 / ×1.40 / ×1.00 | 1.0 | 0 / 0 | 2 % |
| FPS | 1.10 | 0.85–1.15 | 1.00 | 1.00 | 2–4 | ×0.65 / ×0.80 / ×2.80 | 1.2 | 0 / 0 | 3 % |
| MOBA | 1.00 | 0.85–1.15 | 1.00 | 0.75 | 1–3 | ×1.10 / ×1.00 / ×1.00 | 1.0 | 0 / +0.5 | 2 % |
| Chess | 0.85 | 0.92–1.08 | 1.25 | 0.50 | 1–3 | ×1.30 / ×1.00 / ×1.00 | 1.0 | +0.2 / −0.5 | 1 % |
| Gambling | 1.15 | 0.70–1.40 | 0.90 | 1.20 | 2–4 | ×1.00 / ×1.20 / ×1.00 | 1.0 | −0.3 / +2 | 4 % |

The profiles differ from the first proposal in the design spec (§7.1) because the proposed income tilts never changed the leading income source; the tuning rounds are logged in the report. Chess is the calm, credible, loyal path; Gambling is the high-drama, high-variance path that pays in reputation; Just Chatting leads donations, FPS leads ads, and MOBA sits between them. No category is intrinsically losing.

### Mastery and the boredom rebalance (REQ-08)

- **Audience boredom factor:** `1 − boredom / 600` (M1: `/ 250`). **Churn:** `churnRate + 0.00001 × primary-category boredom` (M1: `0.0001`).
- **Mastery:** from skill 70 in the primary category (measured before the week's practice gain), the boredom gain drops by 2 (minimum 1) and churn is multiplied by 0.85.
- Training, practice gains and skill decay keep the M1 rules.

### Burnout penalty

From burnout 60, every chance-bearing choice gets a visible `modifier.burnout` of −0.25 percentage points per burnout point above 60, capped at −10 points. The penalty appears in the odds before the player commits and in the report.

### `Network/collaborate`

The action `NetworkCollaborate` (label "Zagadaj do innych twórców") costs 10 energy and replaces the week's main action. It rolls before the stream with base chance 35 %, +0.3 points per credibility point and −0.3 points per drama point (and the burnout penalty), and allows critical outcomes.

| Result | Effect |
| --- | --- |
| Success | This week's audience ×1.2, +1 credibility |
| Failure | +4 drama, −2 credibility |
| Critical success | Audience ×1.2, +1 credibility and flag `collab-friend` for 26 weeks |
| Critical failure | +6 drama, −2 credibility and the follow-up event `collab-fallout` in 1 week |

A critical outcome replaces the plain one, so each critical row repeats the plain outcome's effects (ruling R25).

### Recovery crisis and short stream

At energy 0 or burnout 100 after settlement, the next week opens with the mandatory crisis `burnout-recovery` instead of M1's forced rest (§10). *Take a week off* is guaranteed: it sets the system flags `sys:rest-only` (the only legal main action is `Rest`) and `sys:short-stream` (a short stream) for one week. A short stream costs half the stream fatigue (`shortStream.fatigue` 0.5) and ×0.7 audience (`shortStream.audience` 0.7). *Push through* rolls at 50 %: success +10 energy, failure +10 burnout and −5 credibility. Energy and burnout stay within 0–100. Content can also set `sys:short-stream`; the `community-burnout-poll` response `cut-hours` uses it for two weeks.

### Mandatory decisions

`CareerState.PendingDecisions` is a queue of event IDs that must be answered before the next week starts. After each settlement the ruleset enqueues, in this order:

1. the recovery crisis when energy is 0 or burnout is 100;
2. `milestone` and `crisis` follow-ups that have come due;
3. the first eligible one-time `milestone` in content order;
4. only if nothing opened, one repeatable `milestone` or `crisis` chosen by weight after a seeded roll with `decisionChancePerWeek` = 6 %.

A week opens with at most two decisions; follow-ups that do not fit stay scheduled. While the queue is not empty the 13-week batch stops before the week (TD-90 mechanism). `ResolveDecision(choiceId)` shows the same odds as a live event and rolls. Stat effects apply at once; cash and follower effects, the roll and its stat lines go into a **decision carry** that the next `StartWeek` moves into the week, so the report reconciles. `Retire` is rejected with `decision-pending` while the queue is not empty ([TD-114](technical-decisions.md)). Mandatory events may not contain audience, quality or viral effects. Pacing guardrail: at most one mandatory interruption per 13 weeks for Balanced; measured 0.60.

### Viral clips

Once per stream, after live viewers are computed:

`viralChance = min(30 %, category base + 0.2 points × max(0, quality − 70) + 5 points × critical outcomes + ViralChance effects)`

A viral stream multiplies live viewers by a seeded factor between 1.5 and 3.0, never above 3× baseline (`viralCapMultiplier`), and adds `max(100, min(10 % of followers, 20 % of the stream's live viewers))` followers. The report has a `viral` line with the roll and the threshold, and the career gets the flag `viral-clip` for 4 weeks (used by the `first-viral` milestone). Gained, lost and net followers stay separate from live viewers (REQ-06).

### Flags and follow-ups

`CareerState` stores career flags (optionally expiring after a number of weeks) and scheduled follow-ups; both are part of the state hash. `SetFlag`, `ClearFlag` and `FollowUp` are declarative effects ([content §2](content-and-experience.md#2-content-structure)). A scheduled `live` follow-up takes one of the event slots of its due stream; a `milestone` or `crisis` follow-up becomes a mandatory decision. Flags prefixed `sys:` are reserved for the ruleset (`sys:rest-only`, `sys:short-stream`). The rivalry arc is the reference: `rivalry-callout` (rare, cooldown 52 weeks) sets `rivalry`, and only a failed riposte schedules `rivalry-crisis`; `rivalry-rematch` can end the flag.

## 12. M3-B Ruleset (`m3-systems.2`)

Status: **Agent proposal, tuned by the M3-B simulation**, 2026-10-26 ([M3-B design](superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md), [M3-B baseline simulation report](reports/m3b-baseline-simulation.md) with the criteria and the tuning log). The wager stakes are an owner decision (2026-10-26); every other number below is read from [`content/m3/balance.json`](../content/m3/balance.json) (version `m3.2`) and still needs owner review. `m3-systems.2` is the M3-A ruleset plus the rules below; each rule reads an optional balance section and is inert without it, so `m3-systems.1` careers play exactly as before ([TD-117](technical-decisions.md)).

### Platforms

Every career starts on Czatnia. Settlement replaces the scalar platform values with the current platform's factors; a category's reach is multiplied by the platform's category reach (1.00 where the table is silent).

| Platform | Reach | Sub yield | Ad yield | Policy risk | Category reach |
| --- | ---: | ---: | ---: | ---: | --- |
| Czatnia | 1.00 | 1.00 | 1.00 | 1.00 | — |
| PulsLive | 1.20 | 0.85 | 1.10 | 1.30 | FPS 1.10, MOBA 1.10, Chess 0.90 |
| Nurt | 0.85 | 1.25 | 0.70 | 0.80 | Chess 1.20, Just Chatting 1.10, Gambling 0.80 |

### Policy exposure, strikes and bans

- **Exposure** is frozen when the week starts, from the state before it: +1 for a Gambling stream, +1 / +2 / +3 for a Low / Medium / High wager, +1 while the side business is open (or for this week's work on it), +1 at drama 70 or more. The plan screen shows the exposure, the chance and the threshold.
- **Strike roll.** With exposure above zero, settlement draws one 1–100 roll against `round(100 × min(6 %, exposure × 0.3 % × policy risk))`; a hit gives a strike on the current platform. Content can give strikes too (`PolicyStrike`). A strike stays active for 26 weeks.
- **Ban.** At settlement, three active strikes on the current platform ban the career from it permanently. When another platform is open, the ruleset-built decision `platform-ban` opens first (before the recovery crisis): one guaranteed choice `move-{Platform}` per open platform, each with its forced-move retention range. Moving switches the platform at once, ends tied contracts without penalty and applies the retention at the next settlement (or at retirement). Strikes on the old platform stay.
- **Endings.** Order at settlement: `Insolvency`, then `GlobalBan` (the system flag `sys:global-ban`, set only by a disclosed failure outcome of a crisis choice), then `AllPlatformsBanned` (current platform banned, no other open), then `Completed`. Both new endings keep their wealth points.

### Migration

`PlanPlatformMigration` (6 energy) needs a target other than the current platform, 26 weeks on the current one and a target that has not banned the career. The first use toward a target announces the move (flag for 4 weeks, −1 credibility, detail `migration`); a second use within those weeks executes it at that week's settlement, after the audience change. Retention is `clamp(0.20, 0.80, 0.40 + credibility / 250 − drama / 500 + 0.10 if announced − 0.10 if forced)`, drawn uniformly within ±0.05 (clamped) when the week starts; the plan screen and the ban decision show the range and the follower counts. The move keeps `round(followers × retention)`; the rest count as lost that week and as former followers (`followers:migration` line). Deals whose template lists platforms that exclude the new one end without penalty.

### Drama decay

A week whose lines add no drama (event, wager, decision or stream) lowers drama by 0.2, never below zero (`stat:Drama`, detail `decay`). The spec's 1 point a week halved the Just Chatting path and was tuned down (report, round 3).

### Sponsors

- **Offers.** After each settlement (unless the career ended) offers whose last week was the settled week expire, then one seeded roll opens at most one offer: `chance = min(20 %, 8 % + 2 points × magnitude)`, where the magnitude counts the `k ≥ 1` with `followers ≥ 1,000 × 10^k`. The template is chosen by weight among those whose prerequisites hold now, that allow the current platform and that have no open offer or deal; no template, no draw. Payment `= min(cap, base + perThousand × ⌊followers / 1,000⌋)`, fixed when the offer opens; the twelve `content/m3` templates have caps of 160,000–600,000 PLN per installment so that deals keep pace with late-career income (report, rounds 1–2; to be rescaled with the M5 economy cap). An offer can be answered in its week and the next one (`offerWeeks` = 2). The Industry Insider starts with its lead offer.
- **`respondOffer(offerId, accept)`** is a free command between weeks, also while decisions are queued; it is refused during a week (`week-in-progress`), for an unknown or expired offer (`unknown-offer`) and, on acceptance, with `contract-limit` (two deals), `same-sponsor` or `prerequisite:<name>`. It draws nothing.
- **Deals.** A one-off is due in the week it was accepted for; a contract's first installment is due `dueEveryWeeks − 1` weeks after acceptance, then every `dueEveryWeeks` weeks through the term. At settlement, in deal order: an incompatible activity (`gambling` for a Gambling stream, `adult-business` for side-business work or an open side business) breaches the deal once and ends it; a due installment is paid (`income:sponsor`) when the plan is `PitchFulfillSponsor` with the required category, is moved one week (with the term end) by a week off (`sys:rest-only`), and is otherwise missed: `cost:sponsor-penalty = min(breach.cashPln, installment)` and the breach credibility; the second miss ends the contract with a further −5 credibility as its own `stat:Credibility` line. A one-off is paid when the stream's category matches and breached otherwise.
- **Pitch.** `PitchFulfillSponsor` (8 energy) fulfils the installments due this week without a roll; with nothing due it rolls the pitch with visible odds (15 % + 0.4 points per credibility point, burnout penalty) and a success opens one fitting offer at once. It is disabled with `no-sponsor-fits` when nothing is due and no template fits.

### Wager

A Gambling plan may stake Low 100, Medium 500 or High 2,000 PLN when the stake is at most 25 % of spendable cash (cash, minus a negative decision carry, minus the plan's equipment price); otherwise the variant is disabled with `needs-cash`, and `StartWeek` refuses a stale plan the same way. The wager rolls once after the action's roll: 45 % to win 2× the stake. A win adds `income:wager` (the net win), +2 drama and ×1.05 live viewers; a loss adds `cost:wager` (the stake), +3 drama and ×0.85 live viewers, so a wager never raises the audience on average. The stakes (Low 100, Medium 500, High 2,000 PLN) are an owner decision (2026-10-26). The money settles with the week; the report shows the roll and threshold.

### Side business

From week 52 with at least 2,000 followers, `SideBusinessWork` (12 energy, +4 burnout) opens the text-only business on first use (permanent flag `side-business`) and adds 15 quality (max 100). While its quality is above zero it pays `income:side-business = round(followers × 0.0005 × quality / 100 × 100)`, costs 0.2 credibility a week (`stat:Credibility`, detail `side-business`) and loses 3 quality per week.

### Plan preview

`PresentPlan` shows, before the player commits, the wager terms of the plan's stake, every missed installment or breach the plan would cause (penalty, credibility, whether the deal ends), the policy exposure with its strike chance and threshold, and for a migration plan the retention and follower range and whether this use executes the move. It never draws a random value.

### M3-B simulation policies

For `m3-systems.2` content the batch simulator runs fourteen policies (`BuiltInPolicies.M3BAll`): the ten of §10 plus the four below. Every policy answers open offers before planning, fulfils a due installment with `PitchFulfillSponsor` in the contract's category (reason `sponsor-due`, `templateId`) right after forced rest, streams the required category of a due one-off, and answers `platform-ban` with the move of the highest retention, then the larger reach (`ban-move`, `platform`, `retention`). An offer is accepted (`offer-accepted`, `templateId`) unless the engine refuses it or it clashes with the policy (`offer-declined`, `templateId`, `reason`): a deal requiring or tagged Gambling for a policy that does not stream Gambling, a deal forbidding gambling for a Gambling specialist, a deal forbidding the side business for the side hustler, or a required category a fixed-category policy never streams. A policy that is not fixed on a category never picks Gambling while a gambling-forbidding contract runs (category rule `sponsor-safe`). No M3-A policy wagers or opens the side business.

| Policy | Behaviour |
| --- | --- |
| Gambler | Specialist-Gambling that stakes the Medium wager on every non-rest week it is enabled (`wager-scheduled`, `tier`, `routine`). |
| SideHustler | Balanced plus `SideBusinessWork` in free weeks with `week mod 4 = 2` when enabled (`side-business-scheduled`). |
| SponsorHunter | Balanced plus `PitchFulfillSponsor` in free weeks with `week mod 3 = 1` when enabled (`pitch-scheduled`). |
| Migrator | Balanced plus `PlanPlatformMigration` toward PulsLive whenever it is enabled and the career is not on PulsLive: announced in week 27, executed in week 28 (`migration-scheduled`, `platform`). |

A free week is one in which no forced-rest, due-installment, rest or upgrade rule fired.

## 13. M3-C Automation and Endings

Status: **Agent proposal, tuned by the M3-C simulation**, 2026-10-26 ([M3-C design](superpowers/specs/2026-10-26-m3c-automation-and-endings-design.md), [M3-C baseline simulation report](reports/m3c-baseline-simulation.md)). The rules below apply on `m3-systems.2` content with the `achievements` section; careers whose content snapshot lacks the section keep the M1 achievement set (§10).

### Batches and strategies

A batch plays 4, 13 or 52 weeks, committing each week on its own, and stops at an ending or before any queued decision. With a strategy the batch plays `StrategyPolicy`; without one it plays `Balanced`. Strategy fields, defaults and bounds:

| Field | Default | Bounds |
| --- | --- | --- |
| Category plan | strongest (least bored while boredom ≥ 60) | strongest, focus (one category), rotate |
| Routine | normal stream ×3, training | 1–8 of normal stream, training, community, networking, sponsor pitch |
| Rest below energy | 30 | 0–80 |
| Rest above burnout | 70 | 50–100 |
| Cash reserve | 0 PLN | 0–1,000,000 |
| Upgrades | on (only when `cash − price ≥ max(reserve, 2 × price)`) | on/off |
| Risk appetite | balanced: gambles from 50 %, community above drama 40 | safe 65 % / 25, bold 35 % / 60 |
| Sponsor offers | accept when they fit | on/off |
| Wager | none | Low, Medium, High on Gambling weeks, never below the reserve |
| Side business | off | on: `week mod 4 = 2` |

Plan order: forced rest, a due sponsor installment, rest thresholds, community above the drama limit, upgrade, side-business week, routine; a due one-off sets the category. A disabled choice falls back to a normal stream, then to rest. Reason codes: `routine-step` (`step`, `of`, `action`), `fallback-safe` (`preferred`, `disabledReason`), `strategy-wager` (`tier`, `routine`), `strategy-side-business`, `strategy-upgrade` (`slot`, `pricePln`, `reservePln`), plus the existing rest, drama, sponsor, offer and event codes (§8, §12). Event-response priorities are deferred to M5.

Simulator strategies: `Strategy-Default` (the defaults), `Strategy-Safe` (safe, rest below 50, reserve 2,000 PLN, routine normal stream / community / training) and `Strategy-Bold` (bold, focus Gambling, Low wager, routine normal stream / networking). Every `Strategy-Bold` career ends with `AllPlatformsBanned` (report follow-up).

### Achievements

| ID | Rule | Points |
| --- | --- | ---: |
| `followers-1m` | 1,000,000 followers | 1 |
| `followers-10m` | 10,000,000 followers | 1 |
| `followers-100m` | 100,000,000 followers | 2 |
| `cash-100m` | 100,000,000 PLN | 1 |
| `cash-1b` | 1,000,000,000 PLN | 1 |
| `career-completed` | week 1,040 settled | 1 |
| `skill-master` | skill 100 in any category | 1 |
| `one-category` | 780 weeks in one category | 1 |
| `new-home` | a platform move with no banned platform | 2 |
| `trusted-partner` | 150 paid sponsor installments | 1 |
| `scandal-survivor` | completed after peak drama ≥ 90, final drama ≤ 30 | 2 |

Achievements are evaluated at every settlement and at retirement and never revoked; their points feed `A = min(1, points / 10)` (§9, unchanged). The metrics `PeakDrama` and `SponsorInstallments` are kept only with the section.

### Ending summary

An ended career has a summary: the ending reason, weeks played, the most streamed category, the reputation (trusted: credibility ≥ 70 and drama ≤ 30; controversial: drama ≥ 60; steady otherwise), the final and banned platforms, the peak follower milestone and the earned achievements with points. The client builds the Polish narrative from it.
