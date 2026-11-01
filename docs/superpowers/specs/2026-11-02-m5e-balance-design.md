# M5-E Balance — Design

Date: 2026-11-02
Status: **Agent proposal (2026-11-02).** Delivered without a design interview at the owner's request ("ok kontynyuj zrób cały millestone 5", 2026-11-01). Every target and value below is an agent proposal for owner review.
Milestone: [M5 — Content, balance, and quality](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/5), sub-project E, delivering the simulation part of [#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36) (TD-131). The M5 decomposition is in the [M5-A design §0](2026-11-01-m5a-ui-polish-design.md).

## 1. Intent

Bring the economy into a plausible range, make the ranking score separate careers, remove dominant paths and let the high-drama route reach week 1,040 (REQ-07), while every M3 criterion keeps passing. The [M5-E report](../../reports/m5e-balance-simulation.md) records the 10,000-career distributions before and after, every change with its reason, and the tuning rounds.

## 2. Targets (agent proposals)

1. **Plausible economy.** The Balanced median career ends with 1–10 million followers and 10–100 million PLN. Before M5-E it ended with 96 million followers, more than twice Poland's population.
2. **Score range.** The Balanced score p95 stays at or below 95 % of the 110,000 maximum, and its p5–p95 spread is at least 10 % of the maximum, so the leaderboard ranks careers rather than ties them at the cap.
3. **No dominant path.** No policy's median score exceeds the Balanced median by more than 10 %.
4. **Both risk routes compete (REQ-07).** Strategy-Bold completes at least 25 % of careers, its median score is at least 80 % of Strategy-Safe's, and its p5–p95 spread is wider (controversy shows greater variance).

The simulator checks the four targets as the "M5-E balance criteria" (`Tools/BatchSimulator/BalanceCriteria.cs`, unit tests in `Tests/BatchSimulator/BalanceCriteriaTests.cs`).

## 3. Changes

Only data changes; the ruleset stays `m3-systems.2`, and the content version becomes `m3.3`. The values and their reasons are in the report and in [gameplay §14](../../gameplay-systems.md#14-m5-e-balance-content-m33): smaller linear and square-root viewer terms, smaller viral follower gains and chances, score caps of 10 million followers and 100 million PLN, five achievements rescaled under new IDs, PulsLive reach 1.10 and Nurt 0.92, strikes active for 13 weeks, FPS reach 1.20 and a swingier, more heated MOBA.

## 4. Not done here

- Moderated 60–120-minute playtests on PC and phone (#36, REQ-05) need people; #36 stays open until the owner records them.
- Event-response priorities for automation stay deferred (gameplay §8); risk appetite still sets the gamble threshold and the drama limit.
- Freezing season one is the owner's approval of these values; a later balance change opens a new season.
