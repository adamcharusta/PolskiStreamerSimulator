# M3-A Baseline Simulation Report

Date: 2026-10-17. Source: batch simulator on ruleset `m3-systems.1` and content `m3.1` (spec [2026-10-16 M3-A §8](../superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md#8-simulation-and-acceptance); issues [#16](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/16), [#17](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/17)), reproduced with:

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --content ../../content/m3
```

For M3 content the simulator runs the ten M3 policies (`BuiltInPolicies.M3All`: Balanced, Grinder, RestHeavy, Rotator, Networker and one `Specialist-{Category}` per category, [gameplay §10](../gameplay-systems.md#m3-simulation-policies-agent-proposal)). The deterministic section below is byte-identical for the same arguments and content; the performance section depends on the machine. The [M1 baseline](m1-baseline-simulation.md) is unchanged: M1 content still runs the five M1 policies with the M1 background and difficulty assignment, and a rerun of its command reproduces its tables.

## Summary

- **Every career completes.** All 10,000 careers reach week 1,040 with no insolvency and no invariant failure across all ten policies, four backgrounds and both difficulties.
- **Pacing passes after the rivalry content fix.** Balanced answers 0.60 mandatory decisions per 13 weeks (limit 1); before the fix it answered 2.39, about 2.0 of them `rivalry-crisis` follow-ups from a `rivalry-callout` loop (tuning round 2, ruling R21).
- **Specialization is no longer viable by the §8 measure.** The fix removed the frequent `rivalry-crisis` `diss` outcomes (+6% followers each) that had inflated every policy alike. Growth now depends on audience mechanics, where rotation dominates: the Rotator median is 62.7 million followers, while the specialists reach 3.9% (Chess, 2.4 million) to 15.6% (Gambling, 9.8 million) of it, far below the 50% limit. Every specialist still completes 100% of its careers.
- **Distinct paths fail for one pair.** Just Chatting (donations), FPS (ads) and Gambling (subscriptions, drama 44, viewer CV 0.906) stand apart, but MOBA and Chess match on all four measures: both lead with subscriptions, median drama 39, credibility 100, viewer CV 0.710 vs 0.729.
- **Growth is lower but still unbounded.** Median final followers fell from billions to tens of millions (Balanced 27.1 million, Rotator 62.7 million), still far above M1's millions. Viral clips (25–71 per career) and the unbounded linear follower term remain. Every policy except Grinder still reaches both score caps at the median.
- **Drama no longer saturates; credibility does.** Median final drama is 39–44 for the specialists and the Rotator; median credibility is 99.4–100 for all of them.
- **Decisions and virals by policy.** Grinder, which never rests voluntarily, answers 6.24 decisions per 13 weeks (mostly the recovery crisis); every other policy answers 0.60–0.61.
- **Cost of a full run.** 10,400,000 simulated weeks took about 37.2 s on 16 logical processors (about 3.6 µs per week, 3.72 ms per career), the same order as M1.

## Criteria results

| Criterion (spec §8) | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | FAIL | MOBA and Chess match on all four measures (subscriptions; drama 39 vs 39; credibility 100 vs 100; viewer CV 0.710 vs 0.729). Every other pair differs. |
| Viable specialization (#16, REQ-08) | FAIL | Completion 100% for every specialist (limit 90%), but median followers are 3.9–15.6% of the Rotator median 62,689,093 (limit 50%). |
| Pacing | PASS | Balanced: 0.60 mandatory decisions per 13 weeks (limit 1). |

## Follow-ups

These are inputs for the remaining M3-A tuning and the balancing work in milestone 5 ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)).

1. **Viable specialization (open criterion).** With the rivalry follower boosts gone, rotation dominates as in M1: a single category accumulates boredom, which cuts live viewers and adds churn. The boredom rebalance (`boredomDivisor` 400, `churnBoredomFactor` 0.00005) and mastery are the levers in spec §7.2; no further tuning round has been run after the content fix yet.
2. **Distinct paths (open criterion).** MOBA and Chess differ in the table (reach 0.95 vs 0.75, churn 0.85 vs 0.70, subscriptions ×1.10 vs ×1.30, Chess +0.2 credibility per stream), but none of these shows in the four measures: both lead with subscriptions, credibility saturates, and their viewer CVs are 0.02 apart.
3. **Bound audience growth (M5, ruling R22).** The M1 follow-up (linear follower term, conversion without diminishing returns) still applies, now with viral clips on top; cap or diminish viral follower gains and follower-percentage effects at large audiences.
4. **Credibility saturation (M5, ruling R22).** Credibility ends at 99–100 for every policy, so category side effects such as Gambling's −0.3 credibility per stream have no visible result.
5. **Grinder decision load.** 6.24 decisions per 13 weeks for a policy that never rests; the recovery-crisis frequency is expected but worth checking for player fatigue.

## Tuning log

Rounds 0–1 change `content/m3/balance.json`; round 2 changes `content/m3/events.json` under controller ruling R21, which overrides the balance-values-only tuning rule for this one change. The content is not published yet, so its version stays `m3.1` and the event versions stay 1. Each round reruns the 10,000-career command above.

| Round | Change | Reason | Result |
| --- | --- | --- | --- |
| 0 (baseline) | Agent-proposed values from spec §7.1. | — | Distinct paths FAIL: Just Chatting ~ Gambling and FPS ~ MOBA matched on all four measures (every specialist led with subscriptions, drama and credibility saturated, CV gaps 0.02 and 0.04). Viable specialization PASS. Pacing FAIL (2.39). |
| 1 | Just Chatting `subYield` 1.00 → 0.90 and `donationYield` 1.30 → 1.40; FPS `subYield` 1.00 → 0.65, `donationYield` 1.00 → 0.80 and `adYield` 1.10 → 2.80. | Per live viewer the base yields are 0.20 PLN subscriptions, 0.15 donations and 0.05 ads, so the proposed tilts (donations ×1.30, ads ×1.10) never changed the leading source. The new values make Just Chatting donation-led (0.21 vs 0.18) and FPS ad-led (0.14 vs 0.13 and 0.12), as the §7.1 table intends, keeping FPS income per viewer near the base (0.39 vs 0.40). | Distinct paths PASS, viable specialization PASS (Chess 95.1% of the Rotator), pacing FAIL (2.39). A diagnostic run with `decisionChancePerWeek` 0 (scratch copy, not adopted) still gave 2.04: the follow-up loop, not the random roll, drove pacing. |
| 2 — rivalry content fix (ruling R21) | `rivalry-callout`: `cooldownWeeks` 4 → 52 and `weight` 3 → 1; its `fire-back` success no longer schedules the `rivalry-crisis` follow-up (it still sets the `rivalry` flag); the failure outcome still schedules it. | A successful `rivalry-rematch` cleared the `rivalry` flag, so `rivalry-callout` returned within weeks, and both `fire-back` outcomes scheduled `rivalry-crisis`, whose follow-ups bypass its 26-week cooldown: about 2.0 crises per 13 weeks for Balanced. | Pacing PASS (0.60). Distinct paths FAIL (MOBA ~ Chess). Viable specialization FAIL (specialists 3.9–15.6% of the Rotator): the frequent `diss` +6% follower outcomes had inflated every policy, hiding the boredom penalty on single-category play. |

The runs use a corrected career assignment. With an even number of policies (ten for M3), the M1 rule "background `i mod 4`" paired each policy with only two backgrounds (for example, every Chess specialist was a Dorm Room Newcomer or a Chess Club Regular). The simulator now rotates each policy through all eight background and difficulty combinations when the policy count is even and keeps the M1 rule for odd counts, so the M1 report is unaffected. Round 1 was rerun with the corrected assignment, and its criteria results did not change.

## Generated report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Rotator, Networker, Specialist-JustChatting, Specialist-Fps, Specialist-Moba, Specialist-Chess, Specialist-Gambling.
- Ruleset `m3-systems.1`; content m3.1 (`1d6f47ab11aac70de9bcb7e4a9b23e65b1472c9409800d0eb4609b7dc4ccaca2`).
- Completion rate: 100.0% (10,000 of 10,000).

#### Endings

| Ending | Careers | Share |
| --- | ---: | ---: |
| Completed | 10,000 | 100.0% |
| Insolvency | 0 | 0.0% |
| EarlyRetirement | 0 | 0.0% |

#### By policy

| Policy | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 1,000 | 1,000 | 0 | 8,150,982 / 27,102,906 / 52,083,089 / 68,995,001 | 63,161,285 / 189,775,212 / 309,760,380 / 393,104,611 | 90,000 / 99,000 / 110,000 / 110,000 |
| Grinder | 1,000 | 1,000 | 0 | 353 / 642 / 585,600 / 830,166 | 434,495 / 587,663 / 7,202,965 / 9,259,174 | 56,255 / 64,258 / 104,930 / 106,915 |
| RestHeavy | 1,000 | 1,000 | 0 | 9,465,979 / 33,727,833 / 64,714,238 / 91,101,956 | 71,889,319 / 227,730,229 / 377,229,442 / 517,606,604 | 90,000 / 99,000 / 110,000 / 110,000 |
| Rotator | 1,000 | 1,000 | 0 | 41,974,925 / 62,689,093 / 95,662,833 / 132,214,333 | 245,777,163 / 338,064,103 / 467,985,435 / 670,698,430 | 90,000 / 99,000 / 110,000 / 110,000 |
| Networker | 1,000 | 1,000 | 0 | 8,140,828 / 31,547,101 / 53,810,000 / 82,010,825 | 64,346,909 / 215,221,762 / 321,406,864 / 428,218,921 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-JustChatting | 1,000 | 1,000 | 0 | 3,789,873 / 5,176,294 / 6,864,731 / 9,077,520 | 52,455,793 / 64,348,677 / 78,694,215 / 97,262,016 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Fps | 1,000 | 1,000 | 0 | 4,463,704 / 6,251,941 / 9,017,724 / 13,883,524 | 58,054,551 / 73,742,950 / 95,295,758 / 135,960,837 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Moba | 1,000 | 1,000 | 0 | 2,111,182 / 2,921,822 / 4,128,599 / 5,043,921 | 28,861,351 / 36,807,644 / 48,220,139 / 60,194,569 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Chess | 1,000 | 1,000 | 0 | 1,871,730 / 2,431,146 / 3,093,038 / 3,902,096 | 22,472,320 / 26,712,226 / 31,583,053 / 38,025,325 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Gambling | 1,000 | 1,000 | 0 | 3,389,606 / 9,752,695 / 115,518,690 / 209,418,937 | 63,320,613 / 124,969,035 / 727,810,472 / 1,204,868,455 | 90,000 / 99,000 / 110,000 / 110,000 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,500 | 0 | 543 / 6,554,043 / 75,496,545 / 161,172,347 | 539,147 / 78,998,005 / 425,290,482 / 886,698,920 | 65,165 / 100,000 / 110,000 / 110,000 |
| LocalMemeCreator | 2,500 | 2,500 | 0 | 537 / 6,691,569 / 76,151,205 / 196,196,676 | 489,908 / 80,169,901 / 431,771,490 / 996,317,716 | 61,637 / 96,000 / 105,600 / 105,600 |
| ChessClubRegular | 2,500 | 2,500 | 0 | 327,647 / 6,533,765 / 75,068,173 / 197,022,268 | 3,758,107 / 68,728,195 / 420,816,396 / 1,204,868,455 | 91,761 / 98,000 / 107,800 / 107,800 |
| IndustryInsider | 2,500 | 2,500 | 0 | 613 / 6,669,344 / 73,105,970 / 209,418,937 | 621,370 / 80,567,098 / 425,030,597 / 1,090,819,026 | 60,044 / 90,000 / 99,000 / 99,000 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,000 | 5,000 | 0 | 708 / 7,230,474 / 74,402,603 / 209,418,937 | 597,137 / 70,271,689 / 506,915,564 / 1,204,868,455 | 67,228 / 105,600 / 110,000 / 110,000 |
| Easy | 5,000 | 5,000 | 0 | 575 / 6,241,284 / 75,222,102 / 132,214,333 | 576,633 / 78,481,907 / 400,674,156 / 670,698,430 | 60,683 / 96,000 / 100,000 / 100,000 |

#### By category specialist

Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.

| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Specialist-JustChatting | 1,000 | 100.0% | 5,176,294 | 64,348,677 | 41.0 | 100.0 | 0.750 | income:donations |
| Specialist-Fps | 1,000 | 100.0% | 6,251,941 | 73,742,950 | 39.0 | 100.0 | 0.802 | income:ads |
| Specialist-Moba | 1,000 | 100.0% | 2,921,822 | 36,807,644 | 39.0 | 100.0 | 0.710 | income:subscriptions |
| Specialist-Chess | 1,000 | 100.0% | 2,431,146 | 26,712,226 | 39.0 | 100.0 | 0.729 | income:subscriptions |
| Specialist-Gambling | 1,000 | 100.0% | 9,752,695 | 124,969,035 | 44.0 | 99.4 | 0.906 | income:subscriptions |
| Rotator | 1,000 | 100.0% | 62,689,093 | 338,064,103 | 41.0 | 99.7 | 1.317 | income:subscriptions |

#### Decisions and virals

| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |
| --- | ---: | ---: | ---: | ---: | ---: |
| Balanced | 1,000 | 47,968 | 0.60 | 46,855 | 46.85 |
| Grinder | 1,000 | 498,907 | 6.24 | 25,361 | 25.36 |
| RestHeavy | 1,000 | 48,047 | 0.60 | 51,791 | 51.79 |
| Rotator | 1,000 | 48,259 | 0.60 | 44,212 | 44.21 |
| Networker | 1,000 | 48,538 | 0.61 | 46,756 | 46.76 |
| Specialist-JustChatting | 1,000 | 47,731 | 0.60 | 48,427 | 48.43 |
| Specialist-Fps | 1,000 | 47,646 | 0.60 | 70,715 | 70.72 |
| Specialist-Moba | 1,000 | 47,758 | 0.60 | 44,495 | 44.49 |
| Specialist-Chess | 1,000 | 47,787 | 0.60 | 36,149 | 36.15 |
| Specialist-Gambling | 1,000 | 47,645 | 0.60 | 63,289 | 63.29 |

#### Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | FAIL | Matching on all four measures: Specialist-Moba ~ Specialist-Chess. |
| Viable specialization (#16, REQ-08) | FAIL | Below 90.0% completion or 50.0% of the Rotator median followers (62,689,093): Specialist-JustChatting (completion 100.0%, followers 8.3% of Rotator); Specialist-Fps (completion 100.0%, followers 10.0% of Rotator); Specialist-Moba (completion 100.0%, followers 4.7% of Rotator); Specialist-Chess (completion 100.0%, followers 3.9% of Rotator); Specialist-Gambling (completion 100.0%, followers 15.6% of Rotator). |
| Pacing | PASS | Balanced answers 0.60 mandatory decisions per 13 weeks (limit 1). |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 0 | 13679457532755275413 | Balanced | DormRoomNewcomer | Normal | Completed | 1,040 | 29,497,412 | 191,209,578 | 110,000 |
| 8 | 13477024926058894539 | Specialist-Chess | DormRoomNewcomer | Normal | Completed | 1,040 | 2,253,806 | 22,770,756 | 110,000 |
| 17 | 10849667979899222076 | Specialist-Moba | DormRoomNewcomer | Normal | Completed | 1,040 | 2,484,116 | 30,427,109 | 110,000 |
| 26 | 368342364169337290 | Specialist-Fps | DormRoomNewcomer | Normal | Completed | 1,040 | 4,506,853 | 61,784,128 | 110,000 |
| 35 | 7086638178683056257 | Specialist-JustChatting | DormRoomNewcomer | Normal | Completed | 1,040 | 4,942,074 | 59,753,321 | 110,000 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 4941 | 10928591004461181089 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 176 | 591,703 | 53,069 |
| 9981 | 5951426456716168714 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 201 | 533,162 | 53,195 |
| 6381 | 12290934264810289996 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 245 | 426,786 | 53,213 |
| 7901 | 5484961590838076470 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 208 | 602,022 | 53,600 |
| 8141 | 2657883517687932961 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 242 | 528,593 | 53,716 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 37.182 s |
| Careers per second | 268.9 |
| Simulated weeks | 10,400,000 |
| ns per simulated week | 3,575 |
| Mean wall time per career | 3.72 ms |
| Allocated | 493798.6 MiB (517,785,324,064 bytes) |
| Peak working set | 111.0 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.13 (Microsoft Windows 10.0.26200) |
