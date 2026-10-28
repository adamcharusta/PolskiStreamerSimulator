# M3-C Baseline Simulation Report

Date: 2026-10-26. Source: batch simulator on ruleset `m3-systems.2` and content `m3.2` with the `achievements` section (spec [2026-10-26 M3-C §9](../superpowers/specs/2026-10-26-m3c-automation-and-endings-design.md#9-simulation); issues [#22](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/22) and [#23](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/23)). Status: **Agent proposal**; every number was tuned by this run and still needs owner review.

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --content ../../content/m3
```

The simulator runs seventeen policies (`BuiltInPolicies.M3CAll`): the fourteen M3-B policies plus `Strategy-Default`, `Strategy-Safe` and `Strategy-Bold`, which play through the same `StrategyPolicy` as the browser batch ([gameplay §13](../gameplay-systems.md#13-m3-c-automation-and-endings)). The deterministic section below is byte-identical for the same arguments.

## Summary

- **All three M3-C criteria pass after four tuning rounds**, and the M3-A and M3-B criteria still pass (the M3-B ones are judged on the M3-B policies, ruling below).
- **Strategies work.** No strategy career goes insolvent. The cautious strategy ends with median drama 10.8 and the bold one with 100, so the strategy choice changes how a career plays.
- **The bold strategy always loses its platforms.** Every `Strategy-Bold` career (Gambling focus, a Low wager every week, drama tolerated up to 60) ends with `AllPlatformsBanned`, with median cash 1.3 million PLN against 420–476 million for the other strategies. The plan screen shows the exposure and the strike chance, but a player who sets this strategy for 52-week batches will lose the career (follow-up below).
- **Achievements separate careers.** Achievement points per career are 1 / 7 / 10 / 12 (p5 / median / p95 / max), so the 10-point cap of the score binds only at the top; Balanced's median is 7.
- **`scandal-survivor` is almost unreachable** (2 careers in 10,000): a policy that reaches drama 90 never brings it back to 30.

## Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Strategies are viable (#22) | PASS | Highest insolvency 0 % (limit 10 %). |
| Strategy choices matter (#22) | PASS | Median final drama 10.8 (Strategy-Safe) vs 100.0 (Strategy-Bold); gap 89.2 (at least 10). |
| Achievements separate careers (#23) | PASS | 9 achievements earned by 5–95 % of careers (at least 6); Balanced median points 7 (below the cap 10). |

## Rulings

- **M3-B criteria population.** The M3-B criteria are judged on the M3-B policies only; the strategy policies have their own criteria. Without this, `Strategy-Bold` alone pushes ban endings to 6.8 % of all careers. Cost: the M3-B ban criterion no longer sees a player strategy.
- **Score ties.** "Gambling is not dominant" accepts an equal median score (Gambler 96,030 vs Balanced 96,040 here) because scores saturate near the cap (M5); the median cash must still be lower.
- **`dramaSurvived`** needs the final drama at 30 or below after the peak, and **`platformMove`** needs a move with no ban. Both earlier rules paid points for drama and for bans, which gambling careers collect, against gameplay §9 (no score for drama).

## Follow-ups

- **M5 balance ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)):** a bold strategy ends every career in bans; consider a stronger warning on the batch dialog or drama-aware wagering.
- **M5 balance:** `scandal-survivor` needs a recovery path that some policy takes; review the threshold and the drama decay together.
- **M5 balance:** follower and cash thresholds (up to 100 million followers and 1 billion PLN) reflect the runaway M3 economy and must be rescaled with the economy cap.

## Tuning log

| Round | Change | Reason | Result |
| --- | --- | --- | --- |
| 0 (baseline) | Spec §6 set: followers 10k / 100k / 1m, cash 1m / 10m, mastery 90, specialist 520, 24 installments; Strategy-Bold with a Medium wager and the side business. | First run. | Achievements FAIL (Balanced median 12 points); M3-B bans FAIL (6.8 % ban endings, all Strategy-Bold). |
| 1 | Followers 1m / 10m / 100m, cash 100m / 1b, mastery 100, specialist 780, installments 150; Strategy-Bold wagers Low and keeps no side business. | Every threshold was reached by almost every career in the runaway economy. | Balanced median 10 points (= cap); Strategy-Bold still 100 % banned; Gambling score above Balanced. |
| 2 | `career-completed` and `trusted-partner` 1 point each; M3-B criteria on the M3-B policies; score tie accepted. | Lower the reachable total; judge each sub-project's criteria on its own policies. | Achievements PASS; Gambling FAIL (score 99,000 vs 98,010: `scandal-survivor` paid drama). |
| 3 | `dramaSurvived` needs final drama ≤ 30. | An achievement must not pay for drama. | Gambling still FAIL (`new-home` paid forced moves after bans). |
| 4 | `platformMove` needs no banned platform. | An achievement must not pay for bans. | All criteria PASS. |

## Generated report

### M3 batch simulation report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Rotator, Networker, Specialist-JustChatting, Specialist-Fps, Specialist-Moba, Specialist-Chess, Specialist-Gambling, Gambler, SideHustler, SponsorHunter, Migrator, Strategy-Default, Strategy-Safe, Strategy-Bold.
- Ruleset `m3-systems.2`; content m3.2 (`8708cb6cad71d090f46af7b42e2b2f453e74e65bccc7d152ddcbd7d35265aa48`).
- Completion rate: 93.2% (9,319 of 10,000).

#### Endings

| Ending | Careers | Share |
| --- | ---: | ---: |
| Completed | 9,319 | 93.2% |
| Insolvency | 0 | 0.0% |
| EarlyRetirement | 0 | 0.0% |
| GlobalBan | 176 | 1.8% |
| AllPlatformsBanned | 505 | 5.1% |

#### By policy

| Policy | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 589 | 589 | 0 | 53,847,459 / 87,090,183 / 142,135,546 / 227,794,396 | 290,805,369 / 476,903,506 / 707,359,024 / 1,051,653,285 | 87,300 / 96,040 / 106,700 / 108,900 |
| Grinder | 589 | 589 | 0 | 1,319 / 3,542 / 5,114,637 / 8,111,273 | 779,547 / 1,248,430 / 33,516,699 / 47,725,666 | 61,124 / 70,274 / 102,410 / 102,410 |
| RestHeavy | 589 | 589 | 0 | 68,195,525 / 110,040,575 / 176,800,454 / 255,248,331 | 361,155,998 / 566,718,207 / 869,484,500 / 1,136,204,125 | 89,100 / 98,000 / 106,700 / 108,900 |
| Rotator | 589 | 589 | 0 | 62,465,839 / 98,519,698 / 164,446,675 / 321,751,620 | 363,130,910 / 493,753,706 / 728,312,672 / 1,478,503,813 | 88,200 / 96,040 / 105,644 / 108,900 |
| Networker | 588 | 588 | 0 | 54,092,445 / 94,935,943 / 170,032,428 / 282,216,554 | 296,253,255 / 502,317,575 / 837,504,768 / 1,381,999,422 | 89,100 / 96,040 / 106,700 / 108,900 |
| Specialist-JustChatting | 588 | 588 | 0 | 35,593,027 / 54,248,483 / 109,314,470 / 145,995,803 | 237,397,002 / 332,034,719 / 582,310,365 / 773,668,294 | 86,400 / 96,000 / 105,600 / 107,800 |
| Specialist-Fps | 588 | 588 | 0 | 34,796,374 / 52,193,263 / 76,762,313 / 112,236,319 | 234,084,567 / 316,868,285 / 434,767,438 / 613,454,593 | 86,400 / 96,000 / 105,600 / 105,600 |
| Specialist-Moba | 588 | 588 | 0 | 48,839,185 / 77,250,327 / 124,054,151 / 215,316,013 | 275,804,733 / 404,487,157 / 601,209,128 / 865,174,078 | 86,400 / 96,000 / 105,600 / 106,700 |
| Specialist-Chess | 588 | 588 | 0 | 36,788,924 / 51,082,761 / 73,151,934 / 95,019,683 | 190,782,353 / 251,491,628 / 331,023,841 / 423,796,034 | 87,300 / 97,000 / 106,700 / 106,700 |
| Specialist-Gambling | 588 | 588 | 0 | 11,567,434 / 62,820,341 / 201,942,321 / 349,527,162 | 117,874,730 / 513,620,751 / 1,145,512,658 / 1,903,386,369 | 87,300 / 97,020 / 106,700 / 110,000 |
| Gambler | 588 | 495 | 0 | 3,391,807 / 31,103,359 / 124,605,187 / 262,805,879 | 38,175,075 / 351,675,006 / 763,301,737 / 1,658,756,625 | 87,300 / 96,030 / 106,700 / 110,000 |
| SideHustler | 588 | 588 | 0 | 273,728 / 74,455,078 / 228,116,981 / 521,987,900 | 18,024,893 / 509,003,305 / 1,233,172,011 / 3,175,127,467 | 86,400 / 96,000 / 105,600 / 108,900 |
| SponsorHunter | 588 | 588 | 0 | 38,563,040 / 80,600,861 / 142,724,868 / 222,485,277 | 261,436,319 / 434,044,840 / 701,289,078 / 1,061,279,110 | 86,400 / 96,000 / 105,600 / 108,900 |
| Migrator | 588 | 588 | 0 | 112,052,697 / 280,404,053 / 519,900,920 / 761,709,192 | 518,703,262 / 1,371,982,001 / 2,318,010,414 / 3,259,508,263 | 90,000 / 99,000 / 110,000 / 110,000 |
| Strategy-Default | 588 | 588 | 0 | 50,885,062 / 74,698,632 / 110,434,389 / 151,531,646 | 286,316,996 / 419,699,991 / 567,729,089 / 725,305,022 | 87,300 / 96,030 / 106,700 / 108,900 |
| Strategy-Safe | 588 | 588 | 0 | 57,000,326 / 85,635,770 / 136,459,620 / 218,136,141 | 344,559,738 / 476,373,296 / 665,091,338 / 944,967,065 | 87,300 / 97,020 / 106,700 / 108,900 |
| Strategy-Bold | 588 | 0 | 0 | 26,446 / 167,588 / 1,258,097 / 4,402,902 | 201,293 / 1,292,213 / 12,319,770 / 48,685,738 | 67,093 / 79,204 / 92,075 / 101,200 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,327 | 0 | 4,830 / 71,176,385 / 308,341,274 / 761,709,192 | 990,732 / 415,239,816 / 1,566,843,774 / 3,259,508,263 | 74,457 / 99,000 / 108,900 / 110,000 |
| LocalMemeCreator | 2,500 | 2,326 | 0 | 3,744 / 72,593,693 / 238,042,922 / 562,334,948 | 962,979 / 431,217,425 / 1,202,891,571 / 2,326,318,414 | 70,978 / 95,040 / 104,544 / 105,600 |
| ChessClubRegular | 2,500 | 2,337 | 0 | 112,356 / 70,449,575 / 149,301,727 / 349,527,162 | 2,107,356 / 380,859,850 / 739,887,809 / 1,669,559,521 | 78,791 / 98,000 / 105,644 / 107,800 |
| IndustryInsider | 2,500 | 2,329 | 0 | 5,706 / 73,965,983 / 240,007,260 / 640,711,223 | 1,298,531 / 441,897,334 / 1,215,737,208 / 2,665,184,022 | 68,011 / 89,100 / 98,010 / 99,000 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,000 | 4,632 | 0 | 29,756 / 62,421,550 / 196,075,258 / 694,654,189 | 1,152,205 / 366,866,182 / 1,025,656,570 / 2,973,638,055 | 76,048 / 102,432 / 107,800 / 110,000 |
| Easy | 5,000 | 4,687 | 0 | 27,422 / 83,473,847 / 225,511,225 / 761,709,192 | 1,091,419 / 466,066,276 / 1,138,102,855 / 3,259,508,263 | 68,566 / 94,080 / 98,000 / 100,000 |

#### By category specialist

Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.

| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Specialist-JustChatting | 588 | 100.0% | 54,248,483 | 332,034,719 | 40.8 | 100.0 | 1.139 | income:donations |
| Specialist-Fps | 588 | 100.0% | 52,193,263 | 316,868,285 | 38.2 | 100.0 | 1.139 | income:ads |
| Specialist-Moba | 588 | 100.0% | 77,250,327 | 404,487,157 | 41.0 | 100.0 | 1.212 | income:subscriptions |
| Specialist-Chess | 588 | 100.0% | 51,082,761 | 251,491,628 | 7.4 | 100.0 | 1.171 | income:subscriptions |
| Specialist-Gambling | 588 | 100.0% | 62,820,341 | 513,620,751 | 99.8 | 99.4 | 1.228 | income:subscriptions |
| Rotator | 589 | 100.0% | 98,519,698 | 493,753,706 | 43.7 | 99.7 | 1.366 | income:subscriptions |

#### Decisions and virals

| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |
| --- | ---: | ---: | ---: | ---: | ---: |
| Balanced | 589 | 37,007 | 0.79 | 26,985 | 45.81 |
| Grinder | 589 | 290,696 | 6.17 | 14,847 | 25.21 |
| RestHeavy | 589 | 32,566 | 0.69 | 29,695 | 50.42 |
| Rotator | 589 | 38,689 | 0.82 | 24,363 | 41.36 |
| Networker | 588 | 38,002 | 0.81 | 26,914 | 45.77 |
| Specialist-JustChatting | 588 | 32,871 | 0.70 | 28,320 | 48.16 |
| Specialist-Fps | 588 | 32,926 | 0.70 | 41,766 | 71.03 |
| Specialist-Moba | 588 | 32,972 | 0.70 | 25,696 | 43.70 |
| Specialist-Chess | 588 | 37,710 | 0.80 | 20,773 | 35.33 |
| Specialist-Gambling | 588 | 37,904 | 0.81 | 36,745 | 62.49 |
| Gambler | 588 | 36,313 | 0.81 | 34,885 | 59.33 |
| SideHustler | 588 | 47,823 | 1.02 | 26,667 | 45.35 |
| SponsorHunter | 588 | 44,517 | 0.95 | 26,041 | 44.29 |
| Migrator | 588 | 38,679 | 0.82 | 26,750 | 45.49 |
| Strategy-Default | 588 | 35,901 | 0.76 | 26,226 | 44.60 |
| Strategy-Safe | 588 | 32,120 | 0.68 | 28,488 | 48.45 |
| Strategy-Bold | 588 | 11,233 | 1.10 | 5,948 | 10.12 |

#### Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | No two of 5 specialists match on leading income, median drama (within 5), median credibility (within 5) and viewer CV (within 0.05). |
| Viable specialization (#16, REQ-08) | PASS | Lowest completion 100.0% (Specialist-JustChatting); lowest median followers 51,082,761 = 51.9% of the Rotator median 98,519,698 (Specialist-Chess). |
| Pacing | PASS | Balanced answers 0.79 mandatory decisions per 13 weeks (limit 1). |

#### Economy and risk

Medians by the nearest-rank method. Sponsor share is sponsor income over all income; breach rate is sponsor penalties over paid installments plus penalties; banned counts careers that lost at least one platform; ban endings are `GlobalBan` and `AllPlatformsBanned`.

| Policy | Careers | Insolvency | Median cash PLN | Median score | Sponsor share | Installments | Breach rate | Strikes | Banned | Ban endings | Migrations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 589 | 0.0% | 476,903,506 | 96,040 | 6.3% | 124,234 | 4.2% | 1 | 0 | 0 | 0 |
| Grinder | 589 | 0.0% | 1,248,430 | 70,274 | 10.9% | 62,225 | 20.9% | 0 | 0 | 0 | 0 |
| RestHeavy | 589 | 0.0% | 566,718,207 | 98,000 | 5.6% | 126,358 | 3.3% | 1 | 0 | 0 | 0 |
| Rotator | 589 | 0.0% | 493,753,706 | 96,040 | 6.6% | 136,682 | 4.2% | 431 | 41 | 0 | 44 |
| Networker | 588 | 0.0% | 502,317,575 | 96,040 | 7.6% | 137,154 | 4.2% | 2 | 0 | 0 | 0 |
| Specialist-JustChatting | 588 | 0.0% | 332,034,719 | 96,000 | 1.3% | 31,455 | 0.4% | 0 | 0 | 0 | 0 |
| Specialist-Fps | 588 | 0.0% | 316,868,285 | 96,000 | 3.6% | 60,556 | 0.0% | 0 | 0 | 0 | 0 |
| Specialist-Moba | 588 | 0.0% | 404,487,157 | 96,000 | 1.4% | 43,519 | 0.8% | 0 | 0 | 0 | 0 |
| Specialist-Chess | 588 | 0.0% | 251,491,628 | 97,000 | 8.4% | 115,875 | 0.9% | 0 | 0 | 0 | 0 |
| Specialist-Gambling | 588 | 0.0% | 513,620,751 | 97,020 | 8.6% | 133,305 | 1.6% | 4,926 | 441 | 0 | 683 |
| Gambler | 588 | 0.0% | 351,675,006 | 96,030 | 10.6% | 125,343 | 1.6% | 5,987 | 444 | 93 | 761 |
| SideHustler | 588 | 0.0% | 509,003,305 | 96,000 | 7.4% | 147,626 | 4.1% | 3,324 | 303 | 0 | 418 |
| SponsorHunter | 588 | 0.0% | 434,044,840 | 96,000 | 12.4% | 190,567 | 5.1% | 0 | 0 | 0 | 0 |
| Migrator | 588 | 0.0% | 1,371,982,001 | 99,000 | 3.4% | 136,680 | 3.8% | 0 | 0 | 0 | 588 |
| Strategy-Default | 588 | 0.0% | 419,699,991 | 96,030 | 5.4% | 112,004 | 3.5% | 0 | 0 | 0 | 0 |
| Strategy-Safe | 588 | 0.0% | 476,373,296 | 97,020 | 6.3% | 135,847 | 1.0% | 0 | 0 | 0 | 0 |
| Strategy-Bold | 588 | 0.0% | 1,292,213 | 79,204 | 8.7% | 21,771 | 1.7% | 1,192 | 545 | 588 | 1,026 |

#### M3-B criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| No side business needed (#20) | PASS | Balanced, which never opens the side business, avoids insolvency in 100.0% of careers (at least 90.0%). |
| Gambling is not dominant (#20) | PASS | Gambler median cash 351,675,006 vs Balanced 476,903,506; median score 96,030 vs 96,040; Gambler insolvency 0.0%. |
| Side business is a trade-off (#20) | PASS | SideHustler median cash 509,003,305 = 1.07x Balanced 476,903,506 (between 1.00x and 1.50x). |
| Bans are rare but real (#19) | PASS | 63.5% of Gambler and SideHustler careers lost a platform (at least 1.0%); 1.1% of all careers ended on a global or total ban (below 2.0%). |
| Sponsors matter (#18) | PASS | SponsorHunter earns 12.4% of its income from sponsors (at least 10.0%); breach rate 5.1%. |
| Reconciliation (#18) | PASS | 0 weeks whose cash change differs from their report lines. |

#### Achievements and strategies

Achievement points per career p5 / p50 / p95 / max: 1 / 7 / 10 / 12 (the score counts at most 10).

| Achievement | Careers | Share |
| --- | ---: | ---: |
| followers-1m | 8,892 | 88.9% |
| followers-10m | 8,611 | 86.1% |
| followers-100m | 2,692 | 26.9% |
| cash-100m | 8,616 | 86.2% |
| cash-1b | 572 | 5.7% |
| career-completed | 9,319 | 93.2% |
| skill-master | 9,611 | 96.1% |
| one-category | 6,715 | 67.2% |
| new-home | 588 | 5.9% |
| trusted-partner | 7,066 | 70.7% |
| scandal-survivor | 2 | 0.0% |

| Strategy | Careers | Insolvency | Median cash PLN | Median drama | Median achievement points |
| --- | ---: | ---: | ---: | ---: | ---: |
| Strategy-Default | 588 | 0.0% | 419,699,991 | 38.6 | 7 |
| Strategy-Safe | 588 | 0.0% | 476,373,296 | 10.8 | 7 |
| Strategy-Bold | 588 | 0.0% | 1,292,213 | 100.0 | 0 |

#### M3-C criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Strategies are viable (#22) | PASS | Highest insolvency 0.0% (Strategy-Default); limit 10.0%. |
| Strategy choices matter (#22) | PASS | Median final drama 10.8 (Strategy-Safe) vs 100.0 (Strategy-Bold); gap 89.2, at least 10. |
| Achievements separate careers (#23) | PASS | 9 achievements earned by 5.0%–95.0% of careers (at least 6); Balanced median points 7 (below the cap 10). |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 64 | 2475505609494469522 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 332,649,137 | 1,635,946,500 | 110,000 |
| 200 | 8210609491974554501 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 270,851,493 | 1,327,217,614 | 110,000 |
| 336 | 2532086991697423611 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 540,656,535 | 2,748,961,058 | 110,000 |
| 472 | 12015995521068461274 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 257,839,222 | 1,475,649,551 | 110,000 |
| 536 | 6423655175863383394 | Specialist-Gambling | DormRoomNewcomer | Normal | Completed | 1,040 | 163,244,431 | 1,040,059,460 | 110,000 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 7855 | 3113112394521249591 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,225 | 1,033,568 | 58,345 |
| 919 | 7099095723836540844 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,253 | 1,152,307 | 58,684 |
| 6631 | 1511818058074176664 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,477 | 1,061,846 | 58,961 |
| 6359 | 7477315631422596753 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,416 | 1,169,093 | 59,079 |
| 8535 | 1982075726648422695 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,574 | 1,062,450 | 59,149 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 76.187 s |
| Careers per second | 131.3 |
| Simulated weeks | 9,891,773 |
| ns per simulated week | 7,702 |
| Mean wall time per career | 7.62 ms |
| Allocated | 772178.9 MiB (809,688,264,640 bytes) |
| Peak working set | 114.3 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.13 (Microsoft Windows 10.0.26200) |
