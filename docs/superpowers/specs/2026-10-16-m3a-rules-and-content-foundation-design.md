# M3-A Rules and Content Foundation — Design

Date: 2026-10-16
Status: **Approved design (owner, 2026-10-16 brainstorming session).** The owner approved the M3 split, approach A for rulesets, the representative content set, ICU MessageFormat for grammatical forms and §4 in conversation, asked the agent to complete the design with its recommendations (§5–§9), and approved the written spec.
Milestone: [M3 — Complete gameplay systems](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/3), sub-project A, delivering [#21](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/21), [#17](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/17) and [#16](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/16), and preparing the material for [#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7).
Builds on: [M1 simulation prototype](2026-10-12-m1-simulation-prototype-design.md), [M2-B backend slice](2026-10-14-m2b-backend-slice-design.md) and [M2-C frontend slice](2026-10-15-m2c-frontend-slice-design.md).

## 1. Intent

Sub-project A lays the rule and content foundation that every later M3 system plugs into. It introduces a second ruleset next to `m1-prototype.1`, a declarative event schema stored in the content snapshot, mandatory decisions that pause a batch, the full energy and burnout loop, and five categories with distinct audience behaviour. New careers start on the new ruleset; M2 careers keep playing on M1.

Success means:

- a new career plays on `m3-systems.1` in the browser, including a mandatory crisis that stops a 13-week batch before the week starts;
- an M1 career created before the change keeps producing byte-identical states;
- the content validator rejects an event without a legal response, with missing grammatical forms or with a broken reference, and a published event version can never change;
- a 10,000-career simulation shows five distinct category paths and a viable single-category career.

## 2. M3 decomposition and owner decisions

M3 is split into four sub-projects, each with its own spec, plan and pull requests, delivered in this order:

| Sub-project | Issues | Delivers |
| --- | --- | --- |
| **A — rules and content foundation** (this spec) | #21, #17, #16 | Ruleset `m3-systems.1`, declarative events and validator, mandatory decisions, actions/energy/burnout, categories/skills/audience/virals |
| **B — economy and risk** | #18, #20, #19 | Equipment, revenue, sponsor contracts, fictional-currency gambling, optional 18+ side business, three platforms, migration and bans |
| **C — automation and endings** | #22, #23 | 4/13/52-week batches, saved strategies, all endings, achievements, final score |
| **D — career screens** | #24, #76 | Dashboard, searchable history, reports, ending screen, PDF diploma |

#7 (content capacity) is measured by the owner on the A content schema and closes with the M3 release.

Sub-projects A–C each ship their own API change and the minimal UI that makes the system playable, so the generated client never drifts and the Playwright journey keeps passing. D adds the large dedicated screens.

### Owner decisions taken in this session

| Topic | Decision |
| --- | --- |
| Decomposition | The four sub-projects above, in that order, each including API and minimal UI. |
| Rulesets | Approach A: a new ruleset `m3-systems.1` with `content/m3`; shared rule code is extracted into helpers used by both rulesets; M1 behaviour is frozen by regression tests; M2 careers continue on M1. Rejected: evolving M1 in place (breaks REQ-29) and one feature-flagged engine (version branches inside rules). |
| Content volume | The agent writes about 40 representative Polish events in the satirical launch tone (§4.5). Launch content stays in M5. The owner uses this schema and these events for the #7 measurement. |
| Grammatical forms | ICU MessageFormat `select` on `gender` inside the event text stored in the content snapshot, rendered in the browser with `intl-messageformat`. Rejected: custom markers and i18next context keys (text must live in the database snapshot, not in the frontend bundle). |
| Nonbinary wording | Gender-neutral Polish forms (`zrobiłoś`, `byłoś`), not underscores or asterisks, because screen readers read them correctly. Agent recommendation, approved with the written spec. |

## 3. Scope

**In scope**

- `Domain`: shared rule helpers extracted from `M1Ruleset`; `M3Ruleset`; event schema v2; mandatory decisions; flags and follow-ups; `Network/collaborate`; recovery crisis; category profiles; viral clips; validator rules; the `IcuMessage` ICU subset validator.
- `Application`/`Infrastructure`: schema-v2 content loading, immutability of published event versions, the new `resolveDecision` command, new careers on `m3-systems.1`.
- `WebApp`: `decision` next step, action odds on week plans, ICU strings in the career view; regenerated OpenAPI document.
- Frontend: ICU rendering, the decision step, the new action, category hints, viral and recovery report lines.
- `content/m3`: balance and about 40 events.
- Batch simulator: runs `m3-systems.1` and produces the M3-A baseline report.
- Decision records and documentation (§9).

**Out of scope** (later sub-projects)

- Sponsors, gambling stakes, adult side business, platforms, migration and bans (B). The actions `Pitch/fulfill sponsor`, `Adult side business work` and `Plan platform migration` arrive with B.
- Saved player strategies, 4- and 52-week batches, new endings and the final score model changes (C). The 13-week `Balanced` batch stays.
- History, ending screen, diploma and the annual review screen (D).
- `pre-stream` and `post-stream` event phases (added by B when sponsors need them).
- Balance of runaway growth and score saturation from the [M1 baseline](../../reports/m1-baseline-simulation.md) (M5, [#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)), except the boredom rebalance that REQ-08 needs.

## 4. Event model and validation (#21) — approved in conversation

### 4.1 Schema v2 (`content/m3/events.json`)

`events.json` gains `"schema": 2`; an M1 file without the field is schema 1 and loads as before. Each event has:

- `id` (stable, `kebab-case`), `version` (positive integer);
- `phase`: `live` (two or three per stream), `milestone` or `crisis` (mandatory, before the week starts, §6);
- `title`, `text`, and per choice `label` and per outcome `explanation`: ICU MessageFormat strings (§4.2);
- `eligibility`: `fromWeek`, `toWeek`, `categories` (empty means every category), `min`/`max` stat bounds, `requiresFlags`, `forbidsFlags`;
- `weight`, `cooldownWeeks` (default 13), `oneTime` (never selected twice), `tags` (`gambling`, `adult-business`, `privacy-incident`, `sponsor`, `rivalry`, `community`);
- choices as in M1 (guaranteed or base chance, modifiers, `allowsCritical`, outcomes), plus an optional `requires` block (minimum cash, minimum energy) that makes the choice unavailable with a reason.

New effect kinds:

| Kind | Meaning | Bounds |
| --- | --- | --- |
| `FollowersPercent` | Changes followers by a percentage at settlement | −20 % … +20 % |
| `SkillDelta` | Changes skill in a named category | −10 … +10 |
| `ViralChance` | Adds percentage points to this week's viral roll (§7.3) | 0 … 30 pp |
| `SetFlag` | Sets a career flag, optionally expiring after `weeks` | flag ID required |
| `ClearFlag` | Removes a career flag | flag ID required |
| `FollowUp` | Schedules an event `inWeeks` weeks later | 1 … 52 weeks |

A scheduled `live` follow-up takes one of the event slots of its due stream; a `milestone` or `crisis` follow-up becomes a mandatory decision before its due week. Flags and scheduled follow-ups are part of `CareerState` and the state hash.

### 4.2 Grammatical forms

Player-facing event strings are ICU MessageFormat. A string that needs agreement uses `{gender, select, female {…} male {…} other {…}}`; `other` holds the gender-neutral form and is also the fallback. A string without agreement is plain text. The server returns the raw string from the career's pinned snapshot; the browser formats it with `intl-messageformat`, passing `gender` from the career's pronoun set. Changing pronouns never touches the simulation (REQ-11).

Authors write Polish quotation marks („…”); an ASCII apostrophe must be doubled (`''`) because ICU treats it as an escape.

### 4.3 Validation

`ContentValidator` (Domain) rejects a snapshot when:

- an event has no choice that is always legal (no `requires` block);
- a choice ID repeats, a chance is outside 0–1 or an effect is outside its bounds;
- a `FollowUp` names an unknown event, or a flag in `requiresFlags` is never set by any `SetFlag`;
- a `milestone` or `crisis` event has an empty week range or is reachable neither by eligibility nor by a `FollowUp`;
- for any category and week, fewer `live` events are eligible by week range and category than `eventsPerWeekMax`.

ICU syntax is validated by `IcuMessage` in `Domain`, a dependency-free validator for the subset the content uses: plain text, `''`, ICU quoting and `{gender, select, female {…} male {…} other {…}}` (each branch may nest the same subset). Simple `{name}` arguments and duplicate branch keys are rejected (`unsupported-argument`, `duplicate-branch:{key}`), because the browser formats with only `gender`. Any other ICU construct is rejected. The validator also rejects a `FollowUp` effect that targets a one-time event (`follow-up-one-time:{eventId}/{target}`). A frontend test formats every `content/m3` message with `intl-messageformat` for all three genders, so the two parsers cannot drift.

### 4.4 Immutability

`IContentStore.EnsureStoredAsync` refuses a snapshot in which an event with the same `(id, version)` differs from that event in an already stored snapshot. Changing an event therefore requires a new `version`. Stored rows are never updated; a career keeps the snapshot it was created with.

### 4.5 Representative content

About 40 events in `content/m3/events.json`, written in Polish by the agent:

- 5 per category (two for every phase, one each for early 1–260, mid 261–780 and late 781–1,040) — 25 category events;
- 8 cross-category `live` events for every phase, so 11 `live` events are eligible in any week for any category;
- 4 mandatory events: the burnout recovery crisis (§5.2), a first-viral milestone, a 10,000-follower milestone and a rivalry crisis;
- one three-step arc (rivalry) using flags and follow-ups.

Cooldowns follow the content specification (13 weeks by default). Because 11 eligible events cannot cover 13-week cooldowns (demand is about 2.5 events × cooldown weeks), `content/m3` uses a 4-week default cooldown (demand about 10) and records this as a representative-content exception; the launch floor is M5 work. The owner times the authoring of five of these templates for #7.

## 5. Weekly actions, energy and burnout (#17)

### 5.1 Actions in `m3-systems.1`

The M1 actions stay: `Rest`, `TrainCategory`, `ImproveProduction`, `ManageCommunity`, `NoAction`. A adds **`NetworkCollaborate`**:

- costs 10 energy;
- rolls before the stream with visible odds: base 50 %, `+0.3 pp` per credibility point above 50, `−0.3 pp` per drama point, burnout modifier (§5.2);
- success: this week's audience ×1.20 and +1 credibility; critical success also sets the flag `collab-friend` for 26 weeks;
- failure: +4 drama, −2 credibility; critical failure schedules the rivalry crisis follow-up.

The week plan returned by the API carries this odds presentation, so the player sees chance and modifiers before committing; the roll appears in the report like an event roll. Exactly one main action per week stays an invariant; `Rest` and the recovery crisis consume it.

### 5.2 Energy and burnout

- Stream fatigue, rest and no-action values stay as in M1 balance.
- **Burnout penalty:** at burnout ≥ 60, every chance-bearing choice gets a visible modifier of `−0.25 pp × (burnout − 60)`, up to −10 pp.
- **Recovery crisis:** at energy 0 or burnout 100 after settlement, the next week opens with the mandatory crisis `burnout-recovery` instead of M1's forced rest. Its choices: *take the week off* (guaranteed: the week's only legal action becomes `Rest` and the stream counts as a short stream with half fatigue and ×0.7 audience), *push through* (roll; success +10 energy, failure +10 burnout and −5 credibility). In B the crisis gains the option to cancel a sponsor obligation.
- Energy and burnout stay within 0–100 after every command; tests check the bounds for each action, event effect and the crisis.

## 6. Mandatory decisions

- `CareerState` gains `PendingDecisions`: event IDs that must be answered before the next week starts.
- After each settlement the ruleset enqueues, in this order: the recovery crisis when triggered (energy is 0 or burnout is 100), due `milestone`/`crisis` follow-ups, the first eligible one-time `milestone` in content order, and then, only if nothing opened, one repeatable `milestone` or `crisis` by weight after a seeded `decisionChancePerWeek` roll. One week opens with at most two decisions; follow-ups that do not fit stay scheduled.
- `RequiresPlayerDecision` returns `true` while the queue is not empty, so the 13-week batch stops before the week (TD-90 mechanism) without consuming it.
- New command `ResolveDecision(choiceId)`: presents the same odds as a live event and rolls. Stat effects apply at once. Cash and follower effects, the roll and its stat lines go into a **decision carry** that the next `StartWeek` moves into the week, so the week's report lists every cash movement and reconciles exactly. Audience, quality and viral effects are not allowed in `milestone`/`crisis` events (validator).
- One-time milestones open as soon as they are eligible, in content order (the first eligible one); repeatable milestones and crises open by weight after a seeded `decisionChancePerWeek` roll (balance, proposed 6 %).
- `Retire` is rejected with `decision-pending` while decisions are queued. When it is accepted, it folds the decision carry's cash and follower percentage into the state and clears scheduled follow-ups before scoring. Follower percentage from events and decisions is counted in the report's FollowersGained, FollowersLost and Net, and appears as a `followers:event` stat line.
- The action log stores it as kind `resolveDecision`; repeats with the same command ID return the stored response.
- The proposed pacing guardrail (no more than one mandatory interruption per 13 routine weeks on average) is measured in the simulation report; content weights are tuned to meet it.

## 7. Categories, skills and audience (#16)

### 7.1 Category profiles

`balance.json` gains a `categories` table read by the shared audience and finance helpers. Agent proposal, tuned by the simulation in §8:

| Category | Reach | Viewer noise | Conversion | Churn | Boredom gain | Income tilt | Side effect per stream | Base viral chance |
| --- | ---: | --- | ---: | ---: | --- | --- | --- | ---: |
| Just Chatting | 1.00 | 0.85–1.15 | 1.15 | 1.00 | 2–4 | donations ×1.30 | — | 2 % |
| FPS | 1.10 | 0.85–1.15 | 1.00 | 1.00 | 2–4 | ads ×1.10; skill weight in quality ×1.2 | — | 3 % |
| MOBA | 0.95 | 0.85–1.15 | 1.00 | 0.85 | 1–3 | subs ×1.10 | — | 2 % |
| Chess | 0.75 | 0.92–1.08 | 1.05 | 0.70 | 1–3 | subs ×1.30 | +0.2 credibility | 1 % |
| Gambling | 1.25 | 0.70–1.40 | 0.90 | 1.20 | 2–4 | donations ×1.20 | +1 drama, −0.3 credibility | 4 % |

No category is intrinsically losing: the higher-reach categories pay with volatility, churn or reputation.

**Tuned by the M3-A baseline simulation (2026-10-17, agent, [report](../../reports/m3a-baseline-simulation.md#tuning-log)).** The proposed income tilts never changed the leading income source, because per live viewer the base yields are 0.20 PLN subscriptions, 0.15 donations and 0.05 ads. To meet the distinct-path criterion, `content/m3/balance.json` sets Just Chatting to subscriptions ×0.90 and donations ×1.40, and FPS to subscriptions ×0.65, donations ×0.80 and ads ×2.80. The other values in the table are unchanged.

### 7.2 Skill, decay and specialization

- Training and practice gains keep the M1 rule (`max(0.2, (100 − skill)/100) × factor`); decay keeps 0.1 per week after 13 inactive weeks down to the starting skill.
- **Mastery:** from skill 70 in the primary category, boredom gain drops by 1 (minimum 1) and churn is multiplied by 0.85. Specialists still grow more slowly late, as the gain rule diminishes.
- **Boredom rebalance (REQ-08):** the audience factor changes from `1 − boredom/250` to `1 − boredom/400`, and churn's boredom factor halves. The simulation decides the final values against §8's criteria.

### 7.3 Viral clips

Once per stream, after the live viewers are computed, the ruleset rolls a viral chance:

`viral = category base + 0.2 pp per quality point above 70 + 5 pp per critical outcome in the week + ViralChance effects`, capped at 30 %.

A viral stream multiplies live viewers by a seeded 1.5–3.0, capped at 3× baseline, and adds `max(100, min(10 % of followers, 20 % of the stream's live viewers))` followers. The report shows a `viral` line with the roll and threshold; gained, lost and net followers stay separate from live viewers (REQ-06). A choice that allows critical outcomes shows the viral chance it can add before the player commits.

## 8. Simulation and acceptance

The batch simulator gains the `m3-systems.1` ruleset (taken from the content directory's `ruleset`, so `--content content/m3` selects it; no separate `--ruleset` flag) and two policies: `Networker` (Balanced with `NetworkCollaborate` every third free week) and one `Specialist` per category. A 10,000-career run writes `docs/reports/m3a-baseline-simulation.md`.

Measurable criteria for the M3-A part of the issues:

- **Distinct paths (#16):** no two category specialists match on all four measures: leading income source, median final drama (within 5 points), median final credibility (within 5 points) and the coefficient of variation of weekly live viewers (within 0.05).
- **Viable specialization (#16, REQ-08):** each single-category specialist completes 1,040 weeks without insolvency in at least 90 % of seeds, and its median final followers are at least 50 % of the Rotator median.
- **Pacing:** mandatory decisions average at most one per 13 weeks for Balanced.

The M1 baseline report stays unchanged; an M1 regression test pins state hashes for fixed seeds and policies before any extraction.

## 9. Architecture, testing and delivery

### 9.1 Code structure

- **Freeze first:** a golden test records the final state hash of 20 fixed seeds × five policies × 1,040 weeks on `m1-prototype.1` and must stay green through the whole sub-project.
- **Extract:** odds, audience, finance, progression and event selection become helpers that take a rules profile; `M1Ruleset` calls them with M1 values and keeps its behaviour.
- **Add:** `M3Ruleset` implements `IRuleset` plus the decision query and command; `CareerFactory` takes the ruleset ID; `CreateCareer` uses `m3-systems.1` and `content/m3`.
- **API:** `IRuleset` gains `PresentDecision` and `ResolveDecision`; `M1Ruleset` answers both with `no-pending-decision`.

### 9.2 Testing

- Domain unit tests first (TDD) for every rule above: validator cases, flags and follow-ups, decision queue order, `decision-pending` rejection, recovery crisis, network odds, burnout modifier, category profiles, mastery and viral caps.
- Property tests on bounds: energy, burnout, drama and credibility stay within 0–100 and cash reconciles to report lines after any sequence of legal commands.
- Domain and Application tests: ICU validation errors (`IcuMessage`), immutability refusal, `resolveDecision` repeats, batch stop on a decision.
- Testcontainers: storing a snapshot with a changed event version fails; M1 and M3 careers coexist.
- Frontend: ICU rendering for all three pronoun sets, decision step, network odds; the Playwright journey covers a mandatory decision through a test seed.
- Coverage ≥ 80 % backend and frontend; SonarQube Cloud zero open issues (TD-96).

### 9.3 Delivery

Three PRs into `develop` from `feature/m3a-*` branches, each merged before the next starts:

1. **Foundation:** M1 golden test, hash-neutral state fields, shared rule steps, schema v2 with loader and ICU validator, immutability, `M3Ruleset` with M1-equivalent rules, flags, follow-ups and the decision mechanism.
2. **Rules and content:** category profiles, mastery, virals, burnout penalty, `NetworkCollaborate`, recovery crisis, decision opening rules, the ~40 Polish events in `content/m3`, simulator support and the baseline report. (Content moved here from PR 3 because the simulation needs it.)
3. **API and play:** API and OpenAPI changes, frontend ICU rendering and decision step, new careers on `m3-systems.1`, the Playwright journey.

Issues #16, #17 and #21 stay open until the M3 release reaches `main`.

## 10. Decision records and documentation

- Technical decision register: M3 decomposition, ruleset approach A, ICU MessageFormat in content snapshots, ICU validation by a dependency-free `Domain` subset validator (`IcuMessage`), event-version immutability, the representative-content cooldown exception.
- GDD decision log: gender-neutral nonbinary forms; representative content written by the agent; recovery crisis replacing forced rest; `Network/collaborate` odds.
- Gameplay systems: a new §11 for `m3-systems.1` (categories table, mastery, virals, burnout modifier, recovery crisis, decisions); the boredom rebalance as an agent proposal.
- Content and experience §2 and §5: schema v2 fields and the ICU convention.
- Requirements traceability: REQ-08, REQ-11 and REQ-22 rows gain the M3-A part.
- Issues #16, #17 and #21: link this spec; #7: link the schema and the representative events.

## 11. Acceptance criteria → evidence

| Criterion | Evidence |
| --- | --- |
| #21 An event without a legal option cannot be published | Validator test and content-load test |
| #21 Missing text and broken references detected | Validator tests (follow-up, flag), ICU tests (missing branch, parse error, empty) |
| #21 Published versions immutable | Store test and Testcontainers test |
| #17 Each week has exactly one main action | Domain tests; decision and crisis cannot add a second |
| #17 Rest consumes the action | Recovery crisis test: *take the week off* leaves only `Rest` |
| #17 Energy and burnout bounds tested | Property tests |
| #16 Each category has a distinct path | M3-A simulation report (§8) |
| #16 Single-category specialization remains viable | M3-A simulation report (§8) |
| #16 Reports distinguish followers and live viewers | Report tests with a viral week; frontend report test |

## 12. Risks

- **Extraction changes M1 results.** The golden test runs before and after every extraction step.
- **Forty events feel repetitive.** The 4-week cooldown exception keeps supply up; launch content is M5.
- **ICU apostrophes break Polish text.** Validation fails on parse errors, and the authoring note in §4.2 explains escaping.
- **Category numbers miss the criteria.** They are agent proposals; the simulation tunes them before PR 2 merges, and every change is recorded.
- **Decisions interrupt batches too often.** Pacing is measured in the report and tuned through weights and cooldowns.
