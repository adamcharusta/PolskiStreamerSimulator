# Content Production Capacity Estimate

Status: **Agent proposal, pending owner measurement and approval**, 2026-10-13 ([#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7)). **Scheduled for M5** (owner decision, 2026-10-28, TD-120): the M3 release closes M3 without the measurement and sign-off, which now happen before the M5 content lock. Previously scheduled for M3 (owner decision, 2026-10-16). This document gives a method to measure how long event content takes to produce, an effort model, an agent estimate built on stated assumptions, and a proposed launch floor by career phase and category. Nothing here is a launch requirement until the owner records measured times and an approved floor in the sign-off section below and in the [content specification](content-and-experience.md#2-content-structure).

## 1. What one template is

A **template** is one publishable schema-v2 event as defined in [content and experience §2](content-and-experience.md#2-content-structure) (the representative set is `content/m3/events.json`); the pronoun-variant time is the time to write its ICU `select` branches:

- stable ID, phase, title and Polish situation text;
- eligibility (week range, category, platform, stats, flags), weight and cooldown;
- two or three responses with disclosed costs, odds, modifiers and declarative outcomes for success, failure and any permitted critical result;
- the Polish grammatical forms required for the woman, man and nonbinary character options, written as ICU `select` branches (pronoun agreement only; no gender-specific content);
- the explanation text shown with the outcome;
- QA: a validator pass, a proofread, a check of every grammatical form, and a simulated run that shows the event and each outcome at least once.

Parametric inserts (names, category details) do not create new templates; a new situation or a materially different set of responses does.

## 2. Measurement method

1. Write **five representative templates per category** (25 in total): two early-career, two mid-career and one late-career situation, covering at least one chance-bearing response with critical outcomes and one guaranteed response in each category. Add five cross-category templates.
2. Time each template in four separate parts, in minutes: first draft (`write_minutes`), the pronoun-form variants (`pronoun_variant_minutes`), QA as listed in §1 (`qa_minutes`) and any rework after QA or review (`revision_minutes`).
3. Record one row per template in [`templates/content-authoring-log.csv`](templates/content-authoring-log.csv), using its header: `date,author,template_id,category,phase,write_minutes,pronoun_variant_minutes,qa_minutes,revision_minutes,notes`.
4. Discard the first two templates of each author as warm-up when computing medians, but keep them in the log.
5. From the remaining rows compute the median of each column and the revision factor `median(revision) / median(write + variants + qa)`, and insert them into the effort model in §3.

## 3. Effort model

`hours = n × (write + variants + qa) × (1 + revision factor) / 60`

where `n` is the number of templates and the per-template minutes are the measured medians.

### Agent estimate (assumptions, not measurements)

| Assumption | Value |
| --- | ---: |
| Write | 40 min |
| Pronoun-form variants | 15 min |
| QA | 15 min |
| Revision factor | 25% |
| Hours per template | (40 + 15 + 15) × 1.25 / 60 ≈ 1.46 h |

| Scope | Templates | Estimated hours |
| --- | ---: | ---: |
| Planning estimate in the content specification (100 category + 40 cross-category live events, 25 between-stream, 15 sponsor, 12 arcs, 20 milestones/crises) | 212 | ≈ 309 h |
| Proposed launch floor (§4) | 173 | ≈ 252 h |
| Proposed live-event floor only | 130 | ≈ 190 h |

At an assumed 10 focused writing hours per week, the proposed floor is roughly 25 weeks of work for one author. These numbers only illustrate the model; the owner's measured medians replace every assumption.

## 4. Proposed launch floor

### Demand from one career

- A manual week shows two or three live events (average 2.5); automated weeks select events the same way.
- The default cooldown is 13 weeks, so in steady state about `13 × 2.5 = 32.5` distinct events are used per 13-week window.
- To keep every week supplied without breaking the cooldown, at least **33 live events must be eligible in any week** for the category being streamed.
- The proposal splits that supply into **13 category-specific** and **20 cross-category** eligible events, so cross-category situations carry most of the variety while each category keeps a recognisable voice.

### Phases

| Phase | Career weeks | Years |
| --- | --- | --- |
| Early | 1–260 | 1–5 |
| Mid | 261–780 | 6–15 |
| Late | 781–1,040 | 16–20 |

### Live-event floor by phase and category

Templates may span more than one phase. Per category, the proposal authors **20** live templates: 6 eligible in all phases, 4 early–mid, 4 mid–late, 3 early only and 3 late only. Cross-category, it authors **30**: 10 in all phases, 5 early–mid, 5 mid–late, 5 early only and 5 late only.

| Eligible live templates | Early | Mid | Late | Authored |
| --- | ---: | ---: | ---: | ---: |
| Just Chatting | 13 | 14 | 13 | 20 |
| FPS | 13 | 14 | 13 | 20 |
| MOBA | 13 | 14 | 13 | 20 |
| Chess | 13 | 14 | 13 | 20 |
| Gambling | 13 | 14 | 13 | 20 |
| Cross-category | 20 | 20 | 20 | 30 |
| **Eligible per week for one category** | **33** | **34** | **33** | — |
| **Authored live templates** | | | | **130** |

Arithmetic for one category: early `6 + 4 + 3 = 13`, mid `6 + 4 + 4 = 14`, late `6 + 4 + 3 = 13`; cross-category early `10 + 5 + 5 = 20`, mid `10 + 5 + 5 = 20`, late `10 + 5 + 5 = 20`. Weeks that rotate categories draw from more than one category pool, so they repeat less than a single-category career.

### Other content (provisional)

These kinds depend on M3 frequencies (sponsor offers, arcs, crises), so their floor is provisional and should be revisited when those systems are simulated.

| Kind | Planning estimate | Proposed floor |
| --- | ---: | ---: |
| Between-stream opportunities | 25 | 15 |
| Sponsor templates | 15 | 10 |
| Platform/rivalry arcs | 12 | 6 |
| Milestones/crises | 20 | 12 |
| **Total** | 72 | **43** |

The proposed launch floor is therefore **173 templates** (130 live + 43 other). It stays below the 212-template planning estimate, which remains a target for later content releases.

### Risks and checks

- If the measured hours per template exceed about 2.5 h, consider lowering the cross-category floor to 16 eligible per phase and accept occasional least-recently-used repeats.
- The batch simulator can log repetition per career once the content exists; validate the floor with the milestone 5 balance runs ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)).
- Memorable story beats need longer cooldowns or one-time flags and are not counted as reusable supply.

## 5. Owner sign-off

| Item | Value |
| --- | --- |
| Measurement dates | |
| Authors | |
| Templates measured | |
| Median write minutes | |
| Median pronoun-variant minutes | |
| Median QA minutes | |
| Revision factor | |
| Hours per template | |
| Approved live-event floor by phase and category | |
| Approved floor for other content | |
| Approved by / date | |

When this section is complete, record the approved floor in [content and experience §2](content-and-experience.md#2-content-structure) and the [validation plan](validation-and-delivery.md#4-risks-and-required-decisions), add a GDD decision-log entry, and close [#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7).
