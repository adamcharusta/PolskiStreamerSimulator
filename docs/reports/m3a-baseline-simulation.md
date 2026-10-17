# M3-A Baseline Simulation Report

Date: 2026-10-17. Source: batch simulator on ruleset `m3-systems.1` and content `m3.1` (spec [2026-10-16 M3-A §8](../superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md#8-simulation-and-acceptance); issues [#16](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/16), [#17](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/17)), reproduced with:

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --content ../../content/m3
```

For M3 content the simulator runs the ten M3 policies (`BuiltInPolicies.M3All`: Balanced, Grinder, RestHeavy, Rotator, Networker and one `Specialist-{Category}` per category, [gameplay §10](../gameplay-systems.md#m3-simulation-policies-agent-proposal)). The deterministic section below is byte-identical for the same arguments and content; the performance section depends on the machine. The [M1 baseline](m1-baseline-simulation.md) is unchanged: M1 content still runs the five M1 policies with the M1 background and difficulty assignment, and a rerun of its command reproduces its tables.

## Summary

- **Every career completes.** All 10,000 careers reach week 1,040 with no insolvency and no invariant failure across all ten policies, four backgrounds and both difficulties.
- **Specialization is viable and paths are distinct.** Every category specialist completes 100% of its careers, and the weakest one (Chess, median 1.83 billion followers) still reaches 95% of the Rotator median (1.92 billion). After one tuning round the five specialists separate: Just Chatting leads with donations, FPS with ads, and MOBA, Chess and Gambling, which all lead with subscriptions, differ by at least 0.117 in the median viewer coefficient of variation.
- **Pacing fails.** Balanced answers 2.39 mandatory decisions per 13 weeks against a limit of 1. About 2.0 of them are `rivalry-crisis` follow-ups scheduled by the live event `rivalry-callout` (Balanced takes its `fire-back` gamble, which succeeds at 55% or more). The allowed tuning values cannot reach them: with `decisionChancePerWeek` set to 0 (diagnostic run on a scratch copy, 1,000 Balanced careers), Balanced still answers 2.04 per 13 weeks.
- **Growth runs away far beyond M1.** Median final followers are in the billions (Balanced 15.8 billion, Rotator 1.9 billion) against M1's millions. M3 multiplies M1's unbounded linear follower term with viral clips (25–71 per career, each adding up to 10% of followers), follower-percentage decision outcomes (`rivalry-crisis` `diss`: +6%) and the boredom rebalance. Scores still saturate: every policy except Grinder reaches both normalization caps at the median.
- **Drama and credibility saturate.** Median final drama is 93–100 and median credibility 99.4–100 for every specialist and the Rotator, so two of the four distinct-path measures carry no signal; the paths differ only by leading income and viewer variability.
- **Decisions and virals by policy.** Grinder, which never rests voluntarily, answers 8.19 decisions per 13 weeks (mostly the recovery crisis). Viral weeks range from 25 (Grinder) to 71 (FPS specialist, whose 1.2 skill weight lifts quality above the viral threshold sooner) per career.
- **Cost of a full run.** 10,400,000 simulated weeks took about 41.5 s on 16 logical processors (about 4.0 µs per week, 4.15 ms per career), the same order as M1.

## Criteria results

| Criterion (spec §8) | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | No two specialists match on all four measures. Closest pairs: MOBA and Gambling (both subscriptions, viewer CV 2.128 vs 2.245) and Chess and MOBA (both subscriptions, CV 1.921 vs 2.128). |
| Viable specialization (#16, REQ-08) | PASS | Completion 100% for every specialist (limit 90%); lowest median followers 1,830,261,841 (Chess) = 95.1% of the Rotator median 1,924,383,200 (limit 50%). |
| Pacing | FAIL | Balanced: 2.39 mandatory decisions per 13 weeks (limit 1). Open follow-up for M5 ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)); see follow-up 1. |

## Follow-ups

These are inputs for the balancing work in milestone 5 ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)) and the remaining M3 work. None was changed here, because the tuning rule allows only category, mastery, boredom and decision-chance values in `content/m3/balance.json`.

1. **Pacing (open criterion).** Break the rivalry loop: a successful `rivalry-rematch` clears the `rivalry` flag, so `rivalry-callout` (weight 3, 4-week cooldown) is eligible again within weeks, and both outcomes of its `fire-back` choice schedule `rivalry-crisis` 4 weeks later; due follow-ups also bypass the crisis's 26-week cooldown. Options: a longer `rivalry-callout` cooldown or lower weight, keeping the flag after a rematch, or letting due follow-ups respect the target's cooldown. Each needs a new event version or a rules change.
2. **Bound audience growth.** The M1 follow-up (linear follower term, conversion without diminishing returns) is now amplified by viral clips and follower-percentage outcomes; cap or diminish viral follower gains and follower-percentage effects at large audiences.
3. **Make drama and credibility informative.** Both saturate for every policy, so category side effects such as Gambling's −0.3 credibility per stream have no visible result.
4. **Distinct paths rely on income tilts and variability.** MOBA, Chess and Gambling all lead with subscriptions. Once growth is bounded, recheck the viewer CV gaps (today they mostly measure growth speed) and revisit Gambling's intended donation lead.
5. **Grinder decision load.** 8.19 decisions per 13 weeks for a policy that never rests; the recovery-crisis frequency is expected but worth checking for player fatigue.

## Tuning log

Each round changes `content/m3/balance.json` (the content is not published yet, so its version stays `m3.1`) and reruns the 10,000-career command above.

| Round | Change | Reason | Result |
| --- | --- | --- | --- |
| 0 (baseline) | Agent-proposed values from spec §7.1. | — | Distinct paths FAIL: Just Chatting ~ Gambling and FPS ~ MOBA matched on all four measures (every specialist led with subscriptions, drama and credibility saturated, CV gaps 0.02 and 0.04). Viable specialization PASS. Pacing FAIL (2.39). |
| 1 | Just Chatting `subYield` 1.00 → 0.90 and `donationYield` 1.30 → 1.40; FPS `subYield` 1.00 → 0.65, `donationYield` 1.00 → 0.80 and `adYield` 1.10 → 2.80. | Per live viewer the base yields are 0.20 PLN subscriptions, 0.15 donations and 0.05 ads, so the proposed tilts (donations ×1.30, ads ×1.10) never changed the leading source. The new values make Just Chatting donation-led (0.21 vs 0.18) and FPS ad-led (0.14 vs 0.13 and 0.12), as the §7.1 table intends, keeping FPS income per viewer near the base (0.39 vs 0.40). | Distinct paths PASS, viable specialization PASS, pacing FAIL (2.39). |
| — | `decisionChancePerWeek` kept at 0.06. | A diagnostic run with 0 (scratch copy, not adopted) still gave 2.04 decisions per 13 weeks for Balanced: the follow-up loop, not the random roll, drives pacing. Lowering the chance would remove most non-rivalry crises without meeting the criterion. | Pacing recorded as an open follow-up for M5 (#36); no further rounds. |

The final run also uses a corrected career assignment. With an even number of policies (ten for M3), the M1 rule "background `i mod 4`" paired each policy with only two backgrounds (for example, every Chess specialist was a Dorm Room Newcomer or a Chess Club Regular). The simulator now rotates each policy through all eight background and difficulty combinations when the policy count is even and keeps the M1 rule for odd counts, so the M1 report is unaffected. Round 1 was rerun with the corrected assignment; the criteria results did not change.

## Generated report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Rotator, Networker, Specialist-JustChatting, Specialist-Fps, Specialist-Moba, Specialist-Chess, Specialist-Gambling.
- Ruleset `m3-systems.1`; content m3.1 (`67d3b434b65b6cfa2450cc6d9446a500b506d663965b7587fe08ea6ca3fd708a`).
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
| Balanced | 1,000 | 1,000 | 0 | 51,497,525 / 15,786,780,677 / 128,543,504,248 / 385,984,187,851 | 280,023,788 / 31,650,740,937 / 219,611,998,385 / 609,603,344,235 | 90,000 / 99,000 / 110,000 / 110,000 |
| Grinder | 1,000 | 1,000 | 0 | 344 / 781 / 11,953,720 / 39,026,437 | 462,053 / 676,649 / 42,341,807 / 83,466,006 | 55,749 / 66,375 / 107,800 / 107,800 |
| RestHeavy | 1,000 | 1,000 | 0 | 22,283,738 / 19,178,210,640 / 157,270,961,968 / 372,537,519,261 | 138,698,159 / 38,899,063,400 / 283,747,008,341 / 728,669,727,346 | 90,000 / 99,000 / 110,000 / 110,000 |
| Rotator | 1,000 | 1,000 | 0 | 581,425,881 / 1,924,383,200 / 8,174,288,189 / 14,110,484,221 | 1,798,204,648 / 4,754,244,486 / 16,911,842,004 / 27,928,048,526 | 90,000 / 99,000 / 110,000 / 110,000 |
| Networker | 1,000 | 1,000 | 0 | 820,876,451 / 4,460,508,705 / 24,517,870,874 / 115,718,250,368 | 2,290,673,189 / 9,696,858,172 / 45,947,122,910 / 241,144,849,296 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-JustChatting | 1,000 | 1,000 | 0 | 4,157,266,918 / 19,661,963,342 / 100,085,768,573 / 198,650,114,878 | 9,951,545,759 / 41,891,593,093 / 178,387,808,730 / 380,705,092,594 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Fps | 1,000 | 1,000 | 0 | 7,487,309 / 8,150,151,241 / 104,672,609,812 / 306,544,064,854 | 86,531,804 / 18,705,857,516 / 187,145,610,688 / 570,916,114,833 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Moba | 1,000 | 1,000 | 0 | 2,174,580,287 / 9,632,184,714 / 46,279,231,153 / 92,765,573,587 | 4,834,190,748 / 18,204,959,236 / 76,035,519,025 / 154,631,967,587 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Chess | 1,000 | 1,000 | 0 | 2,912,805 / 1,830,261,841 / 14,541,873,994 / 32,377,102,408 | 32,212,997 / 3,650,823,938 / 23,492,626,180 / 48,426,037,763 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Gambling | 1,000 | 1,000 | 0 | 3,676,630,718 / 15,105,511,783 / 67,820,289,743 / 202,327,072,803 | 10,864,673,330 / 38,124,301,296 / 154,961,839,182 / 449,227,579,856 | 90,000 / 99,000 / 110,000 / 110,000 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,500 | 0 | 606 / 7,248,517,484 / 106,933,555,548 / 385,984,187,851 | 632,666 / 16,157,474,531 / 192,074,094,058 / 728,669,727,346 | 67,948 / 100,000 / 110,000 / 110,000 |
| LocalMemeCreator | 2,500 | 2,500 | 0 | 615 / 8,081,131,220 / 93,283,028,502 / 236,543,899,593 | 523,581 / 17,508,488,340 / 171,151,093,500 / 450,554,947,112 | 61,499 / 96,000 / 105,600 / 105,600 |
| ChessClubRegular | 2,500 | 2,500 | 0 | 1,910,097 / 3,492,771,267 / 49,586,049,488 / 239,003,716,457 | 8,799,260 / 6,999,575,195 / 97,369,304,831 / 429,119,143,647 | 97,650 / 98,000 / 107,800 / 107,800 |
| IndustryInsider | 2,500 | 2,500 | 0 | 707 / 7,516,621,318 / 93,151,820,234 / 306,544,064,854 | 647,410 / 16,727,490,603 / 176,514,995,464 / 570,916,114,833 | 58,502 / 90,000 / 99,000 / 99,000 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,000 | 5,000 | 0 | 1,034 / 3,898,923,013 / 23,584,541,591 / 84,148,455,643 | 791,786 / 8,483,690,343 / 48,592,787,576 / 159,205,072,938 | 69,714 / 105,600 / 110,000 / 110,000 |
| Easy | 5,000 | 5,000 | 0 | 553 / 14,676,863,534 / 115,443,865,753 / 385,984,187,851 | 560,764 / 26,646,177,689 / 211,384,157,405 / 728,669,727,346 | 60,639 / 96,000 / 100,000 / 100,000 |

#### By category specialist

Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.

| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Specialist-JustChatting | 1,000 | 100.0% | 19,661,963,342 | 41,891,593,093 | 98.0 | 100.0 | 2.259 | income:donations |
| Specialist-Fps | 1,000 | 100.0% | 8,150,151,241 | 18,705,857,516 | 93.0 | 100.0 | 2.103 | income:ads |
| Specialist-Moba | 1,000 | 100.0% | 9,632,184,714 | 18,204,959,236 | 96.0 | 100.0 | 2.128 | income:subscriptions |
| Specialist-Chess | 1,000 | 100.0% | 1,830,261,841 | 3,650,823,938 | 93.0 | 100.0 | 1.921 | income:subscriptions |
| Specialist-Gambling | 1,000 | 100.0% | 15,105,511,783 | 38,124,301,296 | 100.0 | 99.4 | 2.245 | income:subscriptions |
| Rotator | 1,000 | 100.0% | 1,924,383,200 | 4,754,244,486 | 95.0 | 99.7 | 2.017 | income:subscriptions |

#### Decisions and virals

| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |
| --- | ---: | ---: | ---: | ---: | ---: |
| Balanced | 1,000 | 191,522 | 2.39 | 49,520 | 49.52 |
| Grinder | 1,000 | 655,450 | 8.19 | 25,253 | 25.25 |
| RestHeavy | 1,000 | 191,010 | 2.39 | 54,680 | 54.68 |
| Rotator | 1,000 | 147,938 | 1.85 | 43,378 | 43.38 |
| Networker | 1,000 | 188,613 | 2.36 | 48,046 | 48.05 |
| Specialist-JustChatting | 1,000 | 194,689 | 2.43 | 47,968 | 47.97 |
| Specialist-Fps | 1,000 | 195,597 | 2.44 | 70,677 | 70.68 |
| Specialist-Moba | 1,000 | 195,233 | 2.44 | 44,344 | 44.34 |
| Specialist-Chess | 1,000 | 195,739 | 2.45 | 35,750 | 35.75 |
| Specialist-Gambling | 1,000 | 194,983 | 2.44 | 63,466 | 63.47 |

#### Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | No two of 5 specialists match on leading income, median drama (within 5), median credibility (within 5) and viewer CV (within 0.05). |
| Viable specialization (#16, REQ-08) | PASS | Lowest completion 100.0% (Specialist-JustChatting); lowest median followers 1,830,261,841 = 95.1% of the Rotator median 1,924,383,200 (Specialist-Chess). |
| Pacing | FAIL | Balanced answers 2.39 mandatory decisions per 13 weeks (limit 1). |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 0 | 13679457532755275413 | Balanced | DormRoomNewcomer | Normal | Completed | 1,040 | 20,755,097,072 | 43,839,744,794 | 110,000 |
| 8 | 13477024926058894539 | Specialist-Chess | DormRoomNewcomer | Normal | Completed | 1,040 | 2,100,876,984 | 4,522,359,395 | 110,000 |
| 17 | 10849667979899222076 | Specialist-Moba | DormRoomNewcomer | Normal | Completed | 1,040 | 2,509,796,896 | 4,853,166,141 | 110,000 |
| 26 | 368342364169337290 | Specialist-Fps | DormRoomNewcomer | Normal | Completed | 1,040 | 14,557,766,645 | 29,495,190,162 | 110,000 |
| 35 | 7086638178683056257 | Specialist-JustChatting | DormRoomNewcomer | Normal | Completed | 1,040 | 8,180,660,496 | 20,284,461,972 | 110,000 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 4461 | 7399331450256993467 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 215 | 465,714 | 53,051 |
| 3181 | 16379900398466323925 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 286 | 503,469 | 54,081 |
| 1821 | 15060719335675095360 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 324 | 447,679 | 54,150 |
| 6621 | 12793183514104590309 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 282 | 530,554 | 54,171 |
| 7261 | 15433684600384312850 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 324 | 469,135 | 54,267 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 41.658 s |
| Careers per second | 240.1 |
| Simulated weeks | 10,400,000 |
| ns per simulated week | 4,005 |
| Mean wall time per career | 4.17 ms |
| Allocated | 516050.9 MiB (541,118,588,552 bytes) |
| Peak working set | 110.7 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.13 (Microsoft Windows 10.0.26200) |
