# M5-E Balance Simulation Report

Date: 2026-11-02. Source: batch simulator on ruleset `m3-systems.2` and content `m3.3` (spec [2026-11-02 M5-E](../superpowers/specs/2026-11-02-m5e-balance-design.md); issue [#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)). Reproduce with:

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --content ../../content/m3
```

The run plays 10,000 careers over the seventeen M3-C policies, four backgrounds and two difficulties, as the [M3-C report](m3c-baseline-simulation.md) did, and adds the four M5-E balance criteria (`Tools/BatchSimulator/BalanceCriteria.cs`).

## Summary

Before M5-E (content `m3.2`, with the 20 M5 sample events) the economy ran away: the Balanced median career ended with 96.5 million followers and 525 million PLN, the score saturated (Balanced p95 108,900 of 110,000), the Migrator ended with three times the Balanced followers, and no Strategy-Bold career reached week 1,040. The cause is the audience model: credibility doubles conversion at 100, so the term linear in followers alone recruited more followers each week than churn removed, and growth compounded for twenty years; virals worth up to 10 % of followers each, about fifty per career, added to it.

After M5-E (content `m3.3`) growth settles where churn balances the square-root discovery term: the Balanced median ends with 3,965,388 followers (p5–p95 3.15–5.08 million) and 31.2 million PLN, the score keeps a 19,537-point p5–p95 spread with p95 at 92.5 % of the maximum, the Migrator leads the Balanced median score by 5 %, and 38.3 % of Strategy-Bold careers complete with 89 % of the Strategy-Safe median score and twice its spread. All sixteen M3-A, M3-B, M3-C and M5-E criteria pass; no invariant failed.

| Policy | Followers p50 before → after | Cash PLN p50 before → after | Score p50 (p5–p95) after | Completion after |
| --- | ---: | ---: | ---: | ---: |
| Balanced | 96,458,083 → 3,965,388 | 524,821,103 → 31,214,409 | 91,242 (82,238–101,775) | 100 % |
| Migrator | 298,354,428 → 4,913,825 | 1,402,979,877 → 39,975,256 | 95,863 (85,045–107,663) | 100 % |
| Strategy-Safe | 88,935,679 → 3,543,694 | 484,952,126 → 29,159,181 | 91,648 (81,576–100,650) | 100 % |
| Strategy-Bold | 318,054 → 716,197 | 2,415,681 → 9,894,533 | 81,613 (56,670–95,850) | 38.3 % (was 0 %) |

## Changes and reasons

| Constant | Before → after | Reason |
| --- | --- | --- |
| `viewersLinearFollowers` | 0.025 → 0.005 | With credibility at 100 the linear term alone out-recruited churn; now growth has an equilibrium. |
| `viewersSqrtFollowers` | 35 → 25 | Places the equilibrium in the low millions. |
| `viral.followerShare` | 0.10 → 0.02 | A viral clip may not multiply the trajectory (gameplay §3); fifty 10 % jumps compounded. |
| `viral.perQualityPoint` | 0.002 → 0.001 | Fewer quality-driven virals (about 41 instead of 52 per Balanced career). |
| `scoreFollowersCap` / `scoreCashCap` | 1,000,000 / 10,000,000 → 10,000,000 / 100,000,000 | The caps sit above the new p95 so the score separates careers. |
| Achievements `followers-1m/10m/100m`, `cash-100m/1b` | → `followers-100k/1m/5m`, `cash-10m/50m` | Thresholds follow the new scale; new IDs and names, the same points. |
| `platforms.PulsLive.reach` / `platforms.Nurt.reach` | 1.2 / 0.85 → 1.1 / 0.92 | At equilibrium followers grow with the square of reach, so one move to PulsLive was a dominant path. |
| `policy.strikeWeeks` | 26 → 13 | Three strikes within 26 weeks ended every bold career; strikes now fade in a quarter. |
| `categories.Fps.reach` | 1.10 → 1.20 | Keeps the FPS specialist above half the Rotator median (REQ-08). |
| `categories.Moba` noise / `dramaPerStream` | 0.85–1.15 / 0.5 → 0.78–1.22 / 0.65 | Keeps MOBA distinct from Chess (#16): a swingier, more heated category. |

## Tuning log

1. Viral share 0.02 and half the quality chance alone: Balanced median 62 million followers. The virals were not the main driver.
2. Linear 0.005 and square root 25: Balanced 3.8 million followers and 29 million PLN, but three criteria failed (MOBA ~ Chess, FPS below half the Rotator, too few separating achievements).
3. Score caps, rescaled achievements, FPS reach, MOBA noise: only MOBA ~ Chess failed. MOBA drama 1.0 overshot to Gambling's drama; 0.75 still did; 0.65 separated it.
4. PulsLive 1.1 and Nurt 0.92: the Migrator's followers fell from 1.55× to 1.25× the Balanced median.
5. Strikes over 13 weeks: Strategy-Bold completion 0 % → 38 %; raising Gambling reach to 1.2 made the Gambler richer than Balanced, so it stayed at 1.15. A lower strike chance (0.002 per exposure, 26 weeks) kept Bold at 3 % completion.

## Follow-ups

- Moderated 60–120-minute playtests on PC and phone (#36) remain for the owner; the simulator cannot measure reading time.
- Grinder still ends small (median 1,991 followers): playing every week without rest keeps it in burnout. This is the intended cost of grinding, not a viable path.
- Season one freezes these rules when the owner approves them; any later balance change opens a new season (2026-10-31 decision).

## Generated report

## M3 batch simulation report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Rotator, Networker, Specialist-JustChatting, Specialist-Fps, Specialist-Moba, Specialist-Chess, Specialist-Gambling, Gambler, SideHustler, SponsorHunter, Migrator, Strategy-Default, Strategy-Safe, Strategy-Bold.
- Ruleset `m3-systems.2`; content m3.3 (`74f8c87197c92fd773e0cee3424800f551a6772b63ffc4dbdde8cfb5c27dab84`).
- Completion rate: 96.4% (9,635 of 10,000).

#### Endings

| Ending | Careers | Share |
| --- | ---: | ---: |
| Completed | 9,635 | 96.4% |
| Insolvency | 2 | 0.0% |
| EarlyRetirement | 0 | 0.0% |
| GlobalBan | 267 | 2.7% |
| AllPlatformsBanned | 96 | 1.0% |

#### By policy

| Policy | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 589 | 589 | 0 | 3,153,002 / 3,965,388 / 5,079,999 / 6,022,856 | 24,162,907 / 31,214,409 / 39,083,130 / 44,207,937 | 82,238 / 91,242 / 101,775 / 105,149 |
| Grinder | 589 | 589 | 0 | 1,161 / 1,991 / 725,900 / 1,336,644 | 509,004 / 696,154 / 5,981,488 / 10,045,759 | 50,135 / 58,024 / 86,879 / 90,947 |
| RestHeavy | 589 | 589 | 0 | 3,343,960 / 4,209,194 / 5,439,037 / 7,544,896 | 26,019,937 / 33,214,351 / 42,306,221 / 50,089,158 | 82,300 / 91,643 / 104,217 / 107,148 |
| Rotator | 589 | 589 | 0 | 3,624,309 / 4,764,207 / 6,140,861 / 8,850,008 | 30,083,766 / 35,329,310 / 43,168,337 / 53,202,849 | 82,426 / 92,112 / 100,793 / 103,508 |
| Networker | 588 | 588 | 0 | 3,276,803 / 4,404,473 / 6,030,191 / 8,071,955 | 26,564,289 / 34,367,506 / 44,179,332 / 53,408,809 | 82,782 / 91,837 / 104,581 / 107,033 |
| Specialist-JustChatting | 588 | 587 | 1 | 1,929,937 / 2,538,626 / 3,905,201 / 5,093,627 | 18,224,554 / 21,194,675 / 29,039,066 / 37,249,874 | 78,953 / 88,013 / 98,737 / 101,266 |
| Specialist-Fps | 588 | 588 | 0 | 2,243,966 / 2,733,223 / 3,335,203 / 3,977,898 | 21,938,302 / 24,468,714 / 27,579,726 / 31,015,868 | 80,256 / 88,568 / 97,654 / 99,287 |
| Specialist-Moba | 588 | 587 | 1 | 3,274,511 / 4,381,507 / 6,124,689 / 7,514,837 | 23,779,246 / 30,250,925 / 40,488,050 / 48,451,288 | 81,793 / 91,851 / 101,027 / 104,438 |
| Specialist-Chess | 588 | 588 | 0 | 2,811,452 / 3,391,167 / 4,140,325 / 5,017,922 | 18,398,305 / 20,671,465 / 23,169,704 / 25,579,348 | 80,549 / 89,019 / 97,946 / 99,853 |
| Specialist-Gambling | 588 | 588 | 0 | 1,686,058 / 2,969,717 / 4,165,431 / 4,981,528 | 25,777,787 / 31,993,165 / 38,966,518 / 46,227,525 | 81,779 / 90,549 / 99,437 / 101,643 |
| Gambler | 588 | 588 | 0 | 1,497,665 / 2,704,381 / 3,616,102 / 4,646,322 | 23,781,113 / 29,226,491 / 34,907,466 / 39,215,648 | 81,569 / 89,883 / 98,970 / 100,313 |
| SideHustler | 588 | 588 | 0 | 2,480,373 / 4,014,127 / 5,623,357 / 7,760,936 | 31,722,116 / 40,801,214 / 52,772,102 / 67,506,262 | 82,183 / 92,410 / 100,349 / 105,656 |
| SponsorHunter | 588 | 588 | 0 | 2,733,848 / 3,919,852 / 5,478,104 / 7,421,792 | 27,299,197 / 33,853,633 / 43,445,619 / 48,817,913 | 81,660 / 91,521 / 100,769 / 103,623 |
| Migrator | 588 | 588 | 0 | 3,668,781 / 4,913,825 / 7,761,838 / 10,015,345 | 25,258,374 / 39,975,256 / 59,970,120 / 71,511,696 | 85,045 / 95,863 / 107,663 / 108,957 |
| Strategy-Default | 588 | 588 | 0 | 2,841,909 / 3,544,730 / 4,479,330 / 5,611,483 | 23,256,955 / 27,378,179 / 33,855,615 / 37,916,898 | 81,555 / 90,288 / 101,146 / 101,851 |
| Strategy-Safe | 588 | 588 | 0 | 2,820,203 / 3,543,694 / 5,301,999 / 6,232,304 | 25,487,102 / 29,159,181 / 34,931,454 / 39,546,493 | 81,576 / 91,648 / 100,650 / 102,292 |
| Strategy-Bold | 588 | 225 | 0 | 38,429 / 716,197 / 2,173,906 / 3,737,420 | 200,954 / 9,894,533 / 24,622,338 / 35,446,932 | 56,670 / 81,613 / 95,850 / 98,889 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,410 | 0 | 2,464 / 3,676,526 / 6,163,998 / 10,015,345 | 814,391 / 31,625,675 / 51,591,513 / 71,511,696 | 63,460 / 95,019 / 104,655 / 108,957 |
| LocalMemeCreator | 2,500 | 2,403 | 0 | 2,169 / 3,366,370 / 5,227,736 / 7,617,403 | 599,185 / 29,458,686 / 42,080,858 / 52,599,370 | 59,820 / 90,415 / 97,629 / 102,896 |
| ChessClubRegular | 2,500 | 2,407 | 2 | 316,420 / 3,831,529 / 5,479,962 / 8,850,008 | 3,083,886 / 27,163,864 / 39,271,525 / 57,779,722 | 73,638 / 92,520 / 100,668 / 103,100 |
| IndustryInsider | 2,500 | 2,415 | 0 | 2,273 / 3,402,483 / 5,320,888 / 7,523,574 | 682,214 / 29,819,242 / 42,542,778 / 58,428,395 | 56,598 / 85,067 / 91,643 / 96,318 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,000 | 4,909 | 2 | 293,901 / 3,419,578 / 5,385,654 / 10,015,345 | 3,668,591 / 28,690,326 / 41,787,057 / 71,511,696 | 79,291 / 96,075 / 102,060 / 108,957 |
| Easy | 5,000 | 4,726 | 0 | 20,824 / 3,683,184 / 5,671,569 / 9,049,456 | 644,238 / 29,897,634 / 45,123,682 / 69,273,217 | 57,121 / 87,570 / 93,530 / 98,824 |

#### By category specialist

Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.

| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Specialist-JustChatting | 588 | 99.8% | 2,538,626 | 21,194,675 | 40.8 | 100.0 | 0.653 | income:donations |
| Specialist-Fps | 588 | 100.0% | 2,733,223 | 24,468,714 | 39.8 | 100.0 | 0.670 | income:ads |
| Specialist-Moba | 588 | 99.8% | 4,381,507 | 30,250,925 | 53.6 | 100.0 | 0.728 | income:subscriptions |
| Specialist-Chess | 588 | 100.0% | 3,391,167 | 20,671,465 | 38.8 | 100.0 | 0.690 | income:subscriptions |
| Specialist-Gambling | 588 | 100.0% | 2,969,717 | 31,993,165 | 99.8 | 99.4 | 0.733 | income:subscriptions |
| Rotator | 589 | 100.0% | 4,764,207 | 35,329,310 | 89.5 | 99.7 | 0.808 | income:subscriptions |

#### Decisions and virals

| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |
| --- | ---: | ---: | ---: | ---: | ---: |
| Balanced | 589 | 43,570 | 0.92 | 24,097 | 40.91 |
| Grinder | 589 | 298,694 | 6.34 | 17,953 | 30.48 |
| RestHeavy | 589 | 39,207 | 0.83 | 25,333 | 43.01 |
| Rotator | 589 | 46,221 | 0.98 | 22,619 | 38.40 |
| Networker | 588 | 45,214 | 0.96 | 24,235 | 41.22 |
| Specialist-JustChatting | 588 | 39,214 | 0.84 | 25,007 | 42.53 |
| Specialist-Fps | 588 | 38,789 | 0.82 | 34,827 | 59.23 |
| Specialist-Moba | 588 | 40,292 | 0.86 | 23,906 | 40.66 |
| Specialist-Chess | 588 | 43,401 | 0.92 | 17,903 | 30.45 |
| Specialist-Gambling | 588 | 42,009 | 0.89 | 34,427 | 58.55 |
| Gambler | 588 | 41,929 | 0.89 | 34,607 | 58.86 |
| SideHustler | 588 | 54,968 | 1.17 | 23,586 | 40.11 |
| SponsorHunter | 588 | 51,961 | 1.10 | 22,893 | 38.93 |
| Migrator | 588 | 45,259 | 0.96 | 23,956 | 40.74 |
| Strategy-Default | 588 | 41,926 | 0.89 | 23,707 | 40.32 |
| Strategy-Safe | 588 | 35,950 | 0.76 | 24,396 | 41.49 |
| Strategy-Bold | 588 | 29,596 | 0.97 | 19,473 | 33.12 |

#### Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | No two of 5 specialists match on leading income, median drama (within 5), median credibility (within 5) and viewer CV (within 0.05). |
| Viable specialization (#16, REQ-08) | PASS | Lowest completion 99.8% (Specialist-JustChatting); lowest median followers 2,538,626 = 53.3% of the Rotator median 4,764,207 (Specialist-JustChatting). |
| Pacing | PASS | Balanced answers 0.92 mandatory decisions per 13 weeks (limit 1). |

#### Economy and risk

Medians by the nearest-rank method. Sponsor share is sponsor income over all income; breach rate is sponsor penalties over paid installments plus penalties; banned counts careers that lost at least one platform; ban endings are `GlobalBan` and `AllPlatformsBanned`.

| Policy | Careers | Insolvency | Median cash PLN | Median score | Sponsor share | Installments | Breach rate | Strikes | Banned | Ban endings | Migrations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 589 | 0.0% | 31,214,409 | 91,242 | 16.8% | 117,444 | 4.0% | 1 | 0 | 0 | 0 |
| Grinder | 589 | 0.0% | 696,154 | 58,024 | 9.7% | 57,624 | 19.2% | 0 | 0 | 0 | 0 |
| RestHeavy | 589 | 0.0% | 33,214,351 | 91,643 | 17.1% | 119,087 | 3.1% | 0 | 0 | 0 | 0 |
| Rotator | 589 | 0.0% | 35,329,310 | 92,112 | 23.4% | 142,355 | 3.8% | 817 | 1 | 0 | 1 |
| Networker | 588 | 0.0% | 34,367,506 | 91,837 | 21.3% | 132,596 | 4.0% | 1 | 0 | 0 | 0 |
| Specialist-JustChatting | 588 | 0.2% | 21,194,675 | 88,013 | 3.7% | 32,539 | 0.6% | 0 | 0 | 0 | 0 |
| Specialist-Fps | 588 | 0.0% | 24,468,714 | 88,568 | 5.6% | 50,818 | 0.0% | 0 | 0 | 0 | 0 |
| Specialist-Moba | 588 | 0.2% | 30,250,925 | 91,851 | 14.6% | 75,297 | 1.3% | 0 | 0 | 0 | 0 |
| Specialist-Chess | 588 | 0.0% | 20,671,465 | 89,019 | 11.6% | 80,510 | 1.0% | 0 | 0 | 0 | 0 |
| Specialist-Gambling | 588 | 0.0% | 31,993,165 | 90,549 | 18.1% | 116,183 | 1.3% | 5,940 | 140 | 0 | 157 |
| Gambler | 588 | 0.0% | 29,226,491 | 89,883 | 18.0% | 115,760 | 1.3% | 6,243 | 108 | 0 | 129 |
| SideHustler | 588 | 0.0% | 40,801,214 | 92,410 | 19.5% | 143,050 | 4.1% | 3,818 | 73 | 0 | 77 |
| SponsorHunter | 588 | 0.0% | 33,853,633 | 91,521 | 29.5% | 191,093 | 4.9% | 2 | 0 | 0 | 0 |
| Migrator | 588 | 0.0% | 39,975,256 | 95,863 | 19.9% | 124,906 | 3.6% | 0 | 0 | 0 | 588 |
| Strategy-Default | 588 | 0.0% | 27,378,179 | 90,288 | 12.4% | 102,596 | 3.6% | 0 | 0 | 0 | 0 |
| Strategy-Safe | 588 | 0.0% | 29,159,181 | 91,648 | 14.9% | 124,503 | 0.8% | 0 | 0 | 0 | 0 |
| Strategy-Bold | 588 | 0.0% | 9,894,533 | 81,613 | 15.7% | 71,241 | 1.8% | 3,736 | 378 | 363 | 608 |

#### M3-B criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| No side business needed (#20) | PASS | Balanced, which never opens the side business, avoids insolvency in 100.0% of careers (at least 90.0%). |
| Gambling is not dominant (#20) | PASS | Gambler median cash 29,226,491 vs Balanced 31,214,409; median score 89,883 vs 91,242; Gambler insolvency 0.0%. |
| Side business is a trade-off (#20) | PASS | SideHustler median cash 40,801,214 = 1.31x Balanced 31,214,409 (between 1.00x and 1.50x). |
| Bans are rare but real (#19) | PASS | 15.4% of Gambler and SideHustler careers lost a platform (at least 1.0%); 0.0% of all careers ended on a global or total ban (below 2.0%). |
| Sponsors matter (#18) | PASS | SponsorHunter earns 29.5% of its income from sponsors (at least 10.0%); breach rate 4.9%. |
| Reconciliation (#18) | PASS | 0 weeks whose cash change differs from their report lines. |

#### Achievements and strategies

Achievement points per career p5 / p50 / p95 / max: 3 / 7 / 9 / 12 (the score counts at most 10).

| Achievement | Careers | Share |
| --- | ---: | ---: |
| followers-100k | 9,387 | 93.9% |
| followers-1m | 9,080 | 90.8% |
| followers-5m | 1,120 | 11.2% |
| cash-10m | 9,114 | 91.1% |
| cash-50m | 176 | 1.8% |
| career-completed | 9,635 | 96.4% |
| skill-master | 9,871 | 98.7% |
| one-category | 8,181 | 81.8% |
| new-home | 588 | 5.9% |
| trusted-partner | 7,145 | 71.5% |
| scandal-survivor | 2 | 0.0% |

| Strategy | Careers | Insolvency | Median cash PLN | Median drama | Median achievement points |
| --- | ---: | ---: | ---: | ---: | ---: |
| Strategy-Default | 588 | 0.0% | 27,378,179 | 39.4 | 7 |
| Strategy-Safe | 588 | 0.0% | 29,159,181 | 13.6 | 7 |
| Strategy-Bold | 588 | 0.0% | 9,894,533 | 100.0 | 4 |

#### M3-C criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Strategies are viable (#22) | PASS | Highest insolvency 0.0% (Strategy-Default); limit 10.0%. |
| Strategy choices matter (#22) | PASS | Median final drama 13.6 (Strategy-Safe) vs 100.0 (Strategy-Bold); gap 86.5, at least 10. |
| Achievements separate careers (#23) | PASS | 7 achievements earned by 5.0%–95.0% of careers (at least 6); Balanced median points 7 (below the cap 10). |

#### M5-E balance criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Plausible economy (#36) | PASS | Balanced median followers 3,965,388 (between 1,000,000 and 10,000,000) and cash 31,214,409 PLN (between 10,000,000 and 100,000,000). |
| Score keeps its range (#36) | PASS | Balanced score p95 101,775 = 92.5% of 110,000 (at most 95.0%); p5–p95 spread 19,537 (at least 10.0% of the maximum). |
| No dominant path (#36) | PASS | Highest median score 95,863 (Migrator) = 1.05x Balanced 91,242 (at most 1.10x). |
| Both risk routes compete (REQ-07, #36) | PASS | Strategy-Bold completes 38.3% (at least 25.0%); median score 81,613 = 89.1% of Strategy-Safe 91,648 (at least 80.0%); p5–p95 spread 39,180 vs 19,074 (wider). |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 2376 | 3505723896596618732 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 10,015,345 | 67,849,452 | 108,957 |
| 9448 | 17130794311364149076 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 9,796,237 | 66,743,654 | 108,851 |
| 1016 | 4802287749994152546 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 9,454,101 | 67,518,626 | 108,772 |
| 7816 | 5194028927222336491 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 9,480,030 | 63,643,787 | 108,622 |
| 2784 | 17391719833422653579 | Migrator | DormRoomNewcomer | Normal | Completed | 1,040 | 9,150,279 | 66,203,300 | 108,618 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 2946 | 11317175863968933086 | Specialist-JustChatting | ChessClubRegular | Normal | Insolvency | 10 | 169 | -1,028 | 15,457 |
| 2098 | 8593808060268807669 | Specialist-Moba | ChessClubRegular | Normal | Insolvency | 13 | 200 | -1,011 | 15,961 |
| 919 | 7099095723836540844 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,038 | 532,728 | 49,145 |
| 3095 | 14340216599036994114 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,014 | 558,751 | 49,190 |
| 5135 | 11792216807861690144 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,024 | 561,276 | 49,225 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 86.229 s |
| Careers per second | 116.0 |
| Simulated weeks | 10,182,726 |
| ns per simulated week | 8,468 |
| Mean wall time per career | 8.62 ms |
| Allocated | 765557.2 MiB (802,744,888,176 bytes) |
| Peak working set | 114.2 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.14 (Microsoft Windows 10.0.26200) |
