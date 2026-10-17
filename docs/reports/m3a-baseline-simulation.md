# M3-A Baseline Simulation Report

Date: 2026-10-17. Source: batch simulator on ruleset `m3-systems.1` and content `m3.1` (spec [2026-10-16 M3-A §8](../superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md#8-simulation-and-acceptance); issues [#16](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/16), [#17](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/17)), reproduced with:

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --content ../../content/m3
```

For M3 content the simulator runs the ten M3 policies (`BuiltInPolicies.M3All`: Balanced, Grinder, RestHeavy, Rotator, Networker and one `Specialist-{Category}` per category, [gameplay §10](../gameplay-systems.md#m3-simulation-policies-agent-proposal)). The deterministic section below is byte-identical for the same arguments and content; the performance section depends on the machine. The [M1 baseline](m1-baseline-simulation.md) is unchanged: M1 content still runs the five M1 policies with the M1 background and difficulty assignment, and a rerun of its command reproduces its tables.

## Summary

- **All three M3-A criteria pass after four tuning rounds.** Rounds 0, 1, 3 and 4 changed `content/m3/balance.json`, and round 2 fixed the rivalry events (ruling R21). Round 5 was not needed.
- **Every career completes.** All 10,000 careers reach week 1,040 with no insolvency and no invariant failure across all ten policies, four backgrounds and both difficulties.
- **Specialization is slower but viable.** Every category specialist completes 100% of its careers. Four of the five trail the Rotator's median of 90.4 million followers:

  | Specialist | Share of the Rotator median |
  | --- | ---: |
  | FPS | 63.2% |
  | Chess | 63.6% |
  | Just Chatting | 88.0% |
  | MOBA | 91.9% |
  | Gambling | 143.3% |

  Gambling is the only specialist that beats the Rotator. It pays for that with maximum drama (median 100) and the widest audience swings.
- **Paths are distinct.** Just Chatting leads with donations and FPS with ads. MOBA, Chess and Gambling all lead with subscriptions but are separated by drama: median 40.5, 23 and 100.
- **Pacing passes.** Balanced answers 0.60 mandatory decisions per 13 weeks (limit 1); every policy except Grinder answers 0.60–0.61.
- **Growth is still unbounded (M5).** Median final followers are tens of millions (Balanced 94.7 million, Rotator 90.4 million), far above M1's millions, and every policy except Grinder still reaches both score caps at the median. Viral clips (25–71 per career) add to M1's unbounded linear follower term.
- **Credibility saturates (M5).** Median final credibility is 99.4–100 for every specialist and the Rotator, so it never separates paths; drama now does.
- **Cost of a full run.** 10,400,000 simulated weeks took about 36.2 s on 16 logical processors (about 3.5 µs per week, 3.62 ms per career), the same order as M1.

## Criteria results

| Criterion (spec §8) | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | Leading income: Just Chatting donations, FPS ads, MOBA, Chess and Gambling subscriptions. The three subscription-led specialists differ in median drama (40.5, 23, 100) by more than 5 points. |
| Viable specialization (#16, REQ-08) | PASS | Completion is 100% for every specialist (limit 90%). The lowest median followers is FPS at 57,129,342, which is 63.2% of the Rotator median of 90,436,351 (limit 50%). |
| Pacing | PASS | Balanced: 0.60 mandatory decisions per 13 weeks (limit 1). |

## Follow-ups

These are inputs for the balancing work in milestone 5 ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36), ruling R22). No M3-A criterion is open.

1. **Bound audience growth.** The M1 follow-up still applies: the linear follower term and conversion have no diminishing returns, and viral clips now add to them. Cap or diminish viral follower gains and follower-percentage effects at large audiences, then recheck all three criteria, because the viability margins come from compounding growth rates.
2. **Credibility saturation.** Credibility ends at 99–100 for every policy, so category credibility effects (Chess +0.2 and Gambling −0.3 per stream) have no visible result.
3. **Gambling outgrows the Rotator.** At 143% of the Rotator median, Gambling is the strongest growth path. Its intended costs (maximum drama, −0.3 credibility per stream, churn ×1.20) do not bite while credibility saturates and drama has no ban or sponsor consequences yet (M3-B).
4. **Drama carries the separation.** With credibility saturated and viewer CV mostly measuring growth speed, the subscription-led paths are told apart by drama alone. Recheck once M3-B adds drama consequences.
5. **Grinder decision load.** Grinder answers 6.24 decisions per 13 weeks, a policy that never rests; the recovery-crisis frequency is expected but worth checking for player fatigue.

## Tuning log

Rounds 0–1 and 3–4 change `content/m3/balance.json`. Round 2 changes `content/m3/events.json` under controller ruling R21, which overrode the balance-values-only tuning rule for that one change; rounds 3–5 were authorized by ruling R24. The content is not published yet, so its version stays `m3.1` and the event versions stay 1.

Each adopted round reruns the 10,000-career command above. The candidates for rounds 3 and 4 were first compared on scratch copies of the content, with 1,400 careers and only the Balanced, Rotator and specialist policies; those exploratory runs are not adopted rounds.

| Round | Change | Reason | Result |
| --- | --- | --- | --- |
| 0 (baseline) | Agent-proposed values from spec §7.1. | — | Distinct paths FAIL: Just Chatting ~ Gambling and FPS ~ MOBA matched on all four measures (every specialist led with subscriptions, drama and credibility saturated, CV gaps 0.02 and 0.04). Viable specialization PASS. Pacing FAIL (2.39). |
| 1 | Just Chatting `subYield` 1.00 → 0.90 and `donationYield` 1.30 → 1.40; FPS `subYield` 1.00 → 0.65, `donationYield` 1.00 → 0.80 and `adYield` 1.10 → 2.80. | Per live viewer the base yields are 0.20 PLN subscriptions, 0.15 donations and 0.05 ads, so the proposed tilts (donations ×1.30, ads ×1.10) never changed the leading source. The new values make Just Chatting donation-led (0.21 vs 0.18) and FPS ad-led (0.14 vs 0.13 and 0.12), as the §7.1 table intends, keeping FPS income per viewer near the base (0.39 vs 0.40). | Distinct paths PASS, viable specialization PASS (Chess 95.1% of the Rotator), pacing FAIL (2.39). A diagnostic run with `decisionChancePerWeek` 0 (scratch copy, not adopted) still gave 2.04: the follow-up loop, not the random roll, drove pacing. |
| 2 — rivalry content fix (ruling R21) | `rivalry-callout`: `cooldownWeeks` 4 → 52 and `weight` 3 → 1. Its `fire-back` success no longer schedules the `rivalry-crisis` follow-up but still sets the `rivalry` flag; the failure outcome still schedules it. | A successful `rivalry-rematch` cleared the `rivalry` flag, so `rivalry-callout` returned within weeks. Both `fire-back` outcomes scheduled `rivalry-crisis`, and its follow-ups bypass the crisis's 26-week cooldown, giving about 2.0 crises per 13 weeks for Balanced. | Pacing PASS (0.60). Distinct paths FAIL (MOBA ~ Chess). Viable specialization FAIL (specialists 3.9–15.6% of the Rotator). The frequent `diss` +6% follower outcomes had inflated every policy alike, hiding the boredom penalty on single-category play. |
| 3 (ruling R24) | `churnBoredomFactor` 0.00005 → 0.00001; `boredomDivisor` 400 → 600; mastery `boredomRelief` 1 → 2. | Boredom on the only category a specialist streams climbs to 100. At 0.00005 per point that adds 0.005 to the 0.004 weekly churn, more than doubling it; a scratch run with the factor at 0 showed this term was the main brake. The three changes soften the boredom penalty (spec §7.2) without touching the Rotator, whose boredom stays near 0. | Rotator unchanged (62.7 million). Just Chatting 127%, Gambling 224% and FPS 91% of the Rotator, but MOBA 28.5% and Chess 17.9%: viable specialization FAIL. Distinct paths FAIL (MOBA ~ Chess). Pacing PASS (0.60). |
| 4 (ruling R24) | Chess: `reach` 0.75 → 0.85, `conversion` 1.05 → 1.25, `churn` 0.70 → 0.50, `dramaPerStream` 0 → −0.5. MOBA: `reach` 0.95 → 1.00, `churn` 0.85 → 0.75, `dramaPerStream` 0 → +0.5. Gambling: `reach` 1.25 → 1.15, `dramaPerStream` 1 → 2. | Chess and MOBA were the weak specialists. Chess keeps the lowest reach but gains a loyal, low-churn, high-conversion audience and a calm chat, while MOBA gains a slightly wider, stickier and more heated audience. Their opposite drama effects separate the two subscription-led paths. Gambling trades reach for more drama, which keeps it a high-variance, reputation-costly path and limits how far rotation (which streams Gambling one week in five) gains from the other changes. | All three PASS. Viable specialization: the lowest specialist is FPS at 63.2% of the Rotator (90.4 million). Distinct paths: the drama medians are 40.5, 23 and 100 for the subscription-led paths. Pacing: 0.60. No round 5. |

The runs use a corrected career assignment. With an even number of policies (ten for M3), the M1 rule "background `i mod 4`" paired each policy with only two backgrounds (for example, every Chess specialist was a Dorm Room Newcomer or a Chess Club Regular). The simulator now rotates each policy through all eight background and difficulty combinations when the policy count is even and keeps the M1 rule for odd counts, so the M1 report is unaffected. Round 1 was rerun with the corrected assignment, and its criteria results did not change.

## Generated report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Rotator, Networker, Specialist-JustChatting, Specialist-Fps, Specialist-Moba, Specialist-Chess, Specialist-Gambling.
- Ruleset `m3-systems.1`; content m3.1 (`27a3f8a204966fe845ae62c22cfb1271fe65363fc56c3f2d40c5db878fbd34f1`).
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
| Balanced | 1,000 | 1,000 | 0 | 60,586,538 / 94,742,424 / 140,491,265 / 190,517,254 | 323,787,496 / 491,572,450 / 708,536,983 / 914,130,033 | 90,000 / 99,000 / 110,000 / 110,000 |
| Grinder | 1,000 | 1,000 | 0 | 1,364 / 4,117 / 8,646,283 / 13,221,772 | 794,780 / 1,286,275 / 42,731,234 / 60,712,388 | 63,440 / 73,446 / 107,800 / 107,800 |
| RestHeavy | 1,000 | 1,000 | 0 | 77,300,756 / 120,133,824 / 185,721,565 / 246,055,428 | 392,561,945 / 605,926,730 / 902,234,284 / 1,176,420,140 | 90,000 / 99,000 / 110,000 / 110,000 |
| Rotator | 1,000 | 1,000 | 0 | 63,814,011 / 90,436,351 / 134,821,897 / 197,292,905 | 340,868,813 / 455,776,702 / 631,025,509 / 859,614,629 | 90,000 / 99,000 / 110,000 / 110,000 |
| Networker | 1,000 | 1,000 | 0 | 68,934,246 / 121,099,394 / 237,157,881 / 403,317,462 | 324,905,562 / 609,315,126 / 1,063,925,214 / 1,708,241,803 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-JustChatting | 1,000 | 1,000 | 0 | 42,960,559 / 79,551,149 / 149,277,664 / 214,979,746 | 279,469,254 / 452,016,250 / 748,746,648 / 999,939,178 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Fps | 1,000 | 1,000 | 0 | 37,832,237 / 57,129,342 / 88,680,744 / 159,116,191 | 244,651,483 / 335,321,102 / 474,167,025 / 703,555,302 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Moba | 1,000 | 1,000 | 0 | 53,198,894 / 83,125,640 / 132,102,628 / 199,425,636 | 291,906,646 / 433,391,549 / 631,201,083 / 841,377,894 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Chess | 1,000 | 1,000 | 0 | 40,691,386 / 57,500,599 / 81,111,806 / 107,405,192 | 191,454,648 / 255,312,038 / 340,283,435 / 431,668,388 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Gambling | 1,000 | 1,000 | 0 | 78,617,537 / 129,569,396 / 215,924,376 / 389,640,787 | 505,281,278 / 755,597,696 / 1,163,807,335 / 1,781,491,290 | 90,000 / 99,000 / 110,000 / 110,000 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,500 | 0 | 2,759 / 80,565,625 / 152,538,627 / 293,421,395 | 1,053,728 / 430,492,292 / 804,565,006 / 1,496,076,804 | 73,521 / 100,000 / 110,000 / 110,000 |
| LocalMemeCreator | 2,500 | 2,500 | 0 | 2,782 / 87,544,795 / 181,634,991 / 330,193,825 | 1,018,504 / 472,687,720 / 912,181,078 / 1,483,691,513 | 69,711 / 96,000 / 105,600 / 105,600 |
| ChessClubRegular | 2,500 | 2,500 | 0 | 1,102,993 / 81,710,535 / 157,354,347 / 389,640,787 | 7,544,221 / 399,360,911 / 777,920,806 / 1,781,491,290 | 97,229 / 98,000 / 107,800 / 107,800 |
| IndustryInsider | 2,500 | 2,500 | 0 | 4,425 / 89,513,811 / 189,908,054 / 403,317,462 | 1,407,232 / 478,790,342 / 963,714,502 / 1,708,241,803 | 68,623 / 90,000 / 99,000 / 99,000 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,000 | 5,000 | 0 | 4,867 / 79,951,470 / 171,107,517 / 403,317,462 | 1,316,831 / 406,831,162 / 848,663,506 / 1,708,241,803 | 75,676 / 105,600 / 110,000 / 110,000 |
| Easy | 5,000 | 5,000 | 0 | 3,303 / 89,580,207 / 175,329,122 / 389,640,787 | 1,246,133 / 465,905,072 / 912,276,674 / 1,781,491,290 | 67,641 / 96,000 / 100,000 / 100,000 |

#### By category specialist

Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.

| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Specialist-JustChatting | 1,000 | 100.0% | 79,551,149 | 452,016,250 | 41.0 | 100.0 | 1.183 | income:donations |
| Specialist-Fps | 1,000 | 100.0% | 57,129,342 | 335,321,102 | 39.0 | 100.0 | 1.142 | income:ads |
| Specialist-Moba | 1,000 | 100.0% | 83,125,640 | 433,391,549 | 40.5 | 100.0 | 1.211 | income:subscriptions |
| Specialist-Chess | 1,000 | 100.0% | 57,500,599 | 255,312,038 | 23.0 | 100.0 | 1.192 | income:subscriptions |
| Specialist-Gambling | 1,000 | 100.0% | 129,569,396 | 755,597,696 | 100.0 | 99.4 | 1.327 | income:subscriptions |
| Rotator | 1,000 | 100.0% | 90,436,351 | 455,776,702 | 42.0 | 99.7 | 1.346 | income:subscriptions |

#### Decisions and virals

| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |
| --- | ---: | ---: | ---: | ---: | ---: |
| Balanced | 1,000 | 48,128 | 0.60 | 48,024 | 48.02 |
| Grinder | 1,000 | 498,907 | 6.24 | 25,413 | 25.41 |
| RestHeavy | 1,000 | 47,872 | 0.60 | 54,024 | 54.02 |
| Rotator | 1,000 | 48,263 | 0.60 | 44,003 | 44.00 |
| Networker | 1,000 | 48,742 | 0.61 | 49,334 | 49.33 |
| Specialist-JustChatting | 1,000 | 47,753 | 0.60 | 48,696 | 48.70 |
| Specialist-Fps | 1,000 | 47,746 | 0.60 | 71,278 | 71.28 |
| Specialist-Moba | 1,000 | 47,716 | 0.60 | 44,323 | 44.32 |
| Specialist-Chess | 1,000 | 47,669 | 0.60 | 36,250 | 36.25 |
| Specialist-Gambling | 1,000 | 47,688 | 0.60 | 63,622 | 63.62 |

#### Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | No two of 5 specialists match on leading income, median drama (within 5), median credibility (within 5) and viewer CV (within 0.05). |
| Viable specialization (#16, REQ-08) | PASS | Lowest completion 100.0% (Specialist-JustChatting); lowest median followers 57,129,342 = 63.2% of the Rotator median 90,436,351 (Specialist-Fps). |
| Pacing | PASS | Balanced answers 0.60 mandatory decisions per 13 weeks (limit 1). |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 0 | 13679457532755275413 | Balanced | DormRoomNewcomer | Normal | Completed | 1,040 | 67,926,955 | 411,294,222 | 110,000 |
| 8 | 13477024926058894539 | Specialist-Chess | DormRoomNewcomer | Normal | Completed | 1,040 | 43,109,454 | 190,015,691 | 110,000 |
| 17 | 10849667979899222076 | Specialist-Moba | DormRoomNewcomer | Normal | Completed | 1,040 | 104,108,398 | 497,446,655 | 110,000 |
| 26 | 368342364169337290 | Specialist-Fps | DormRoomNewcomer | Normal | Completed | 1,040 | 55,307,822 | 305,091,264 | 110,000 |
| 35 | 7086638178683056257 | Specialist-JustChatting | DormRoomNewcomer | Normal | Completed | 1,040 | 70,231,051 | 428,987,035 | 110,000 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 6381 | 12290934264810289996 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 772 | 848,422 | 58,297 |
| 9981 | 5951426456716168714 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,121 | 1,148,446 | 60,150 |
| 861 | 13858860973075329849 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,544 | 1,135,477 | 61,059 |
| 5901 | 9303646693160432639 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,570 | 1,179,836 | 61,204 |
| 2621 | 14816393987107580998 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,830 | 1,072,557 | 61,413 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 36.204 s |
| Careers per second | 276.2 |
| Simulated weeks | 10,400,000 |
| ns per simulated week | 3,481 |
| Mean wall time per career | 3.62 ms |
| Allocated | 495143.3 MiB (519,195,420,024 bytes) |
| Peak working set | 109.1 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.13 (Microsoft Windows 10.0.26200) |
