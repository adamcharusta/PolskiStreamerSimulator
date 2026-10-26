# M3-B Baseline Simulation Report

Date: 2026-10-26. Source: batch simulator on ruleset `m3-systems.2` and content `m3.2` (spec [2026-10-21 M3-B §9](../superpowers/specs/2026-10-21-m3b-economy-and-risk-design.md#9-automation-and-simulation); issues [#18](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/18), [#19](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/19) and [#20](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/20)). Status: **Agent proposal**; every number was tuned by this run and still needs owner review.

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42 --content ../../content/m3
```

For `m3-systems.2` content the simulator runs fourteen policies (`BuiltInPolicies.M3BAll`): the ten M3-A policies plus Gambler, SideHustler, SponsorHunter and Migrator ([gameplay §12](../gameplay-systems.md#12-m3-b-ruleset-m3-systems2)). Careers start through `CareerFactory.Create(ContentSnapshot …)`, so Industry Insider careers open with their lead offer. The deterministic section below is byte-identical for the same arguments; the performance section depends on the machine.

## Summary

- **All six M3-B criteria pass after five tuning rounds**, and the three M3-A criteria (distinct paths, viable specialization, pacing) still pass on the new content.
- **Bans stay a real but bounded risk.** 1.2 % of all careers end with `AllPlatformsBanned` (limit 2 %), all of them Gambler careers; no career ends with `GlobalBan`, because no policy takes the 30 % gamble that risks it. 65 % of Gambler and SideHustler careers lose at least one platform, while the policies that avoid Gambling and high drama never get a strike.
- **Gambling does not pay.** Gambler's median final cash is 322 million PLN against Balanced's 476 million, and its median score 99,000 against 100,000. Specialist-Gambling (no wager) still ends above Balanced on cash, an M3-A category property (follow-up below).
- **The side business is a trade-off.** SideHustler ends at 1.11× Balanced's median cash, with a much wider spread (p5 16 million against 298 million) because the business keeps its policy exposure and credibility cost.
- **Sponsors matter.** SponsorHunter earns 12.6 % of its income from sponsors (breach rate 5.2 %); Balanced 6.3 %.
- **Every week reconciles.** Zero weeks whose income minus costs differs from the cash change, and no invariant failure.
- **Growth is still unbounded (M5).** Median final cash is in the hundreds of millions, as in M3-A; sponsor caps had to scale with it (round 1 and 2). The economy cap belongs to the M5 balance pass ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)).

## Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| No side business needed (#20) | PASS | Balanced, which never opens the side business, avoids insolvency in 100 % of careers (limit 90 %). |
| Gambling is not dominant (#20) | PASS | Gambler median cash 321,822,678 vs Balanced 475,823,530; median score 99,000 vs 100,000; Gambler insolvency 0 %. |
| Side business is a trade-off (#20) | PASS | SideHustler median cash 525,942,129 = 1.11× Balanced (between 1.00× and 1.50×). |
| Bans are rare but real (#19) | PASS | 65.0 % of Gambler and SideHustler careers lost a platform (at least 1 %); 1.2 % of all careers ended on a global or total ban (below 2 %). |
| Sponsors matter (#18) | PASS | SponsorHunter sponsor share 12.6 % (at least 10 %); breach rate 5.2 %. |
| Reconciliation (#18) | PASS | 0 weeks out of balance. |

## Findings

- **Drama decay changes every path.** At 1 point a week (spec §5.5) decay halved the Just Chatting specialist's followers (79 → 35 million) and failed the M3-A viable-specialization criterion; an ablation without decay restored it (73.6 million). At 0.2 a week every specialist keeps at least 53.6 % of the Rotator median.
- **The Easy chance bonus makes a below-even gamble look even.** With `deny` at 45 %, Easy careers saw 55 % and the policies took it, so `GlobalBan` rose to 35 % of careers in round 1. At 30 % (40 % on Easy) no policy takes it; a player still sees the disclosed risk.
- **A wager that always raised the audience rewarded gambling.** With ×1.15 on a win and ×1.05 on a loss every wager added viewers; at ×1.05 / ×0.85 the expected effect is slightly negative and Gambler falls below Balanced.
- **Strikes come mostly from exposure, not events.** After round 2 the policies that never stream Gambling and keep drama below 70 get no strike at all, and the event strikes (copyright music, gambling promo code) rarely add up to three within 26 weeks.

## Follow-ups

- **M5 balance ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)):** cap the runaway economy; then rescale the sponsor payment caps (now 160,000–600,000 PLN per installment) and the side-business income scale (100), which were raised to stay meaningful against it.
- **M5 balance:** 65 % of the risky careers losing a platform may be more than "rare"; the criterion only sets a floor. Review the strike chance with the owner.
- **M5 balance:** Specialist-Gambling (no wager) ends above Balanced on cash (544 million); the Gambling category profile is an M3-A tuning result.

## Tuning log

| Round | Change | Reason | Result |
| --- | --- | --- | --- |
| 0 (baseline) | Spec §5–§7 values; 12 sponsor templates and 15 events as written. | First run. | FAIL: side business (0.02×), bans (49.3 % ban endings; GlobalBan 17.3 %), sponsors (0.1 %); M3-A viable specialization FAIL. |
| 1 | `policy.chancePerExposure` 0.015 → 0.005, `policy.chanceCap` 0.15 → 0.06; `policy-privacy-leak` `deny` 0.9 with the credibility modifier → 0.45 without it; `policy-copyright-strike` `keep-music` 0.6 → 0.75; sponsor `paymentCapPln` ×100. | Strikes and the privacy crisis ended half the careers; capped sponsor payments were negligible against late-career income. | GlobalBan 35.2 % (Easy +10 % lifted `deny` to 55 %); sponsors 7.3 %. |
| 2 | `policy.chancePerExposure` 0.005 → 0.003; `deny` 0.45 → 0.3; `policy-gambling-promo` `show-code` 0.5 → 0.35; sponsor `paymentCapPln` ×2. | Keep both risky gambles below even odds on Easy; fewer strikes per exposure. | Bans, sponsors PASS; Gambler FAIL (above Balanced); M3-A viable specialization FAIL (Just Chatting 41.7 % of Rotator). |
| 3 | `policy.dramaDecay` 1 → 0.2; `wagers.winAudience` 1.15 → 1.05, `wagers.lossAudience` 1.05 → 0.85. | Ablation showed drama decay caused the Just Chatting drop; a wager must not raise the audience on average. | Gambling, viable specialization PASS; side business FAIL (0.91×). |
| 4 | `sideBusiness.incomeScale` 10 → 40. | The side business paid too little to offset its policy exposure. | Side business FAIL (0.98×). |
| 5 | `sideBusiness.incomeScale` 40 → 100. | Same. | All criteria PASS. |

The content changes in rounds 1 and 2 edit events and sponsor templates that have never been released to `main`; TD-117 records that exception to the TD-112 immutability rule.

## Generated report

### M3 batch simulation report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Rotator, Networker, Specialist-JustChatting, Specialist-Fps, Specialist-Moba, Specialist-Chess, Specialist-Gambling, Gambler, SideHustler, SponsorHunter, Migrator.
- Ruleset `m3-systems.2`; content m3.2 (`691b5b8840752a1919468dc32f57498ff695a81765dabf8be36205b2cc8bb817`).
- Completion rate: 98.8% (9,882 of 10,000).

#### Endings

| Ending | Careers | Share |
| --- | ---: | ---: |
| Completed | 9,882 | 98.8% |
| Insolvency | 0 | 0.0% |
| EarlyRetirement | 0 | 0.0% |
| GlobalBan | 0 | 0.0% |
| AllPlatformsBanned | 118 | 1.2% |

#### By policy

| Policy | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 715 | 715 | 0 | 54,994,806 / 88,976,343 / 144,117,372 / 221,815,724 | 298,166,485 / 475,823,530 / 717,052,404 / 988,182,793 | 90,000 / 100,000 / 110,000 / 110,000 |
| Grinder | 715 | 715 | 0 | 1,417 / 3,722 / 5,022,593 / 8,163,452 | 817,045 / 1,264,741 / 33,134,389 / 48,729,961 | 63,466 / 72,832 / 107,800 / 107,800 |
| RestHeavy | 715 | 715 | 0 | 66,110,632 / 112,514,819 / 179,077,150 / 256,309,579 | 346,698,470 / 579,939,164 / 875,623,671 / 1,170,276,541 | 90,000 / 100,000 / 110,000 / 110,000 |
| Rotator | 715 | 715 | 0 | 61,619,374 / 96,485,251 / 163,800,196 / 349,626,732 | 349,491,105 / 489,998,700 / 751,847,675 / 1,440,739,773 | 90,000 / 99,000 / 110,000 / 110,000 |
| Networker | 714 | 714 | 0 | 54,369,422 / 95,081,216 / 176,838,720 / 309,956,981 | 292,064,554 / 508,392,170 / 848,754,205 / 1,295,001,221 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-JustChatting | 714 | 714 | 0 | 35,273,281 / 54,720,813 / 110,951,201 / 161,382,217 | 232,109,286 / 328,817,795 / 592,517,955 / 805,940,185 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Fps | 714 | 714 | 0 | 34,634,577 / 52,043,645 / 78,622,676 / 109,421,125 | 234,558,183 / 320,868,621 / 434,984,333 / 618,966,333 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Moba | 714 | 714 | 0 | 49,903,972 / 80,130,544 / 131,189,125 / 308,458,387 | 278,014,681 / 415,974,659 / 619,770,191 / 1,103,032,916 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist-Chess | 714 | 714 | 0 | 35,721,022 / 51,727,528 / 74,740,318 / 94,223,426 | 190,280,713 / 255,557,983 / 336,895,255 / 392,477,979 | 90,000 / 100,000 / 110,000 / 110,000 |
| Specialist-Gambling | 714 | 714 | 0 | 11,490,551 / 68,692,823 / 214,565,411 / 565,284,888 | 121,962,212 / 544,210,874 / 1,194,363,169 / 2,873,250,817 | 90,000 / 100,000 / 110,000 / 110,000 |
| Gambler | 714 | 596 | 0 | 3,010,962 / 27,894,550 / 115,299,845 / 211,956,859 | 29,024,773 / 321,822,678 / 739,324,284 / 1,262,842,704 | 90,000 / 99,000 / 110,000 / 110,000 |
| SideHustler | 714 | 714 | 0 | 216,829 / 73,164,338 / 216,153,704 / 585,090,492 | 16,354,439 / 525,942,129 / 1,212,003,377 / 2,854,784,030 | 90,000 / 99,000 / 110,000 / 110,000 |
| SponsorHunter | 714 | 714 | 0 | 40,216,521 / 75,100,563 / 139,076,878 / 225,125,791 | 263,743,384 / 423,461,292 / 668,950,078 / 974,793,211 | 90,000 / 99,000 / 110,000 / 110,000 |
| Migrator | 714 | 714 | 0 | 111,617,386 / 275,362,336 / 550,826,927 / 1,011,778,303 | 512,326,800 / 1,362,993,722 / 2,456,136,193 / 4,342,256,442 | 90,000 / 99,000 / 110,000 / 110,000 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,476 | 0 | 3,595 / 73,371,838 / 352,464,127 / 1,011,778,303 | 1,241,559 / 431,018,409 / 1,702,896,694 / 4,342,256,442 | 77,077 / 100,000 / 110,000 / 110,000 |
| LocalMemeCreator | 2,501 | 2,468 | 0 | 3,101 / 74,419,018 / 266,316,881 / 813,112,830 | 1,085,524 / 444,863,220 / 1,346,717,530 / 3,420,979,829 | 73,211 / 96,000 / 105,600 / 105,600 |
| ChessClubRegular | 2,500 | 2,467 | 0 | 3,487,081 / 69,595,858 / 160,238,536 / 565,284,888 | 24,225,371 / 373,264,904 / 790,861,060 / 2,873,250,817 | 98,000 / 98,456 / 107,800 / 107,800 |
| IndustryInsider | 2,499 | 2,471 | 0 | 4,915 / 76,865,665 / 264,268,362 / 820,878,450 | 1,461,309 / 450,616,976 / 1,305,407,708 / 3,150,234,052 | 70,432 / 90,000 / 99,000 / 99,000 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,001 | 4,912 | 0 | 7,729 / 62,014,253 / 225,323,539 / 1,011,778,303 | 1,733,650 / 364,064,021 / 1,154,363,763 / 4,342,256,442 | 80,667 / 105,600 / 110,000 / 110,000 |
| Easy | 4,999 | 4,970 | 0 | 6,456 / 86,129,837 / 259,630,301 / 822,557,435 | 1,664,341 / 481,295,778 / 1,305,407,708 / 3,410,245,363 | 70,442 / 96,000 / 100,000 / 100,000 |

#### By category specialist

Medians by the nearest-rank method; viewer CV is the coefficient of variation of a career's weekly live viewers; leading income is the most common largest income source. The Rotator is the reference for viable specialization.

| Policy | Careers | Completion | Median followers | Median cash PLN | Median drama | Median credibility | Median viewer CV | Leading income |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Specialist-JustChatting | 714 | 100.0% | 54,720,813 | 328,817,795 | 40.6 | 100.0 | 1.137 | income:donations |
| Specialist-Fps | 714 | 100.0% | 52,043,645 | 320,868,621 | 38.4 | 100.0 | 1.128 | income:ads |
| Specialist-Moba | 714 | 100.0% | 80,130,544 | 415,974,659 | 41.3 | 100.0 | 1.218 | income:subscriptions |
| Specialist-Chess | 714 | 100.0% | 51,727,528 | 255,557,983 | 8.4 | 100.0 | 1.176 | income:subscriptions |
| Specialist-Gambling | 714 | 100.0% | 68,692,823 | 544,210,874 | 99.8 | 99.4 | 1.230 | income:subscriptions |
| Rotator | 715 | 100.0% | 96,485,251 | 489,998,700 | 43.7 | 99.7 | 1.363 | income:subscriptions |

#### Decisions and virals

| Policy | Careers | Mandatory decisions | Decisions per 13 weeks | Viral weeks | Virals per career |
| --- | ---: | ---: | ---: | ---: | ---: |
| Balanced | 715 | 44,724 | 0.78 | 32,488 | 45.44 |
| Grinder | 715 | 353,180 | 6.17 | 18,386 | 25.71 |
| RestHeavy | 715 | 39,706 | 0.69 | 35,818 | 50.10 |
| Rotator | 715 | 46,522 | 0.81 | 29,416 | 41.14 |
| Networker | 714 | 46,076 | 0.81 | 32,815 | 45.96 |
| Specialist-JustChatting | 714 | 40,022 | 0.70 | 34,367 | 48.13 |
| Specialist-Fps | 714 | 40,093 | 0.70 | 50,724 | 71.04 |
| Specialist-Moba | 714 | 39,950 | 0.70 | 31,488 | 44.10 |
| Specialist-Chess | 714 | 45,859 | 0.80 | 25,501 | 35.72 |
| Specialist-Gambling | 714 | 46,124 | 0.81 | 44,159 | 61.85 |
| Gambler | 714 | 44,127 | 0.81 | 42,028 | 58.86 |
| SideHustler | 714 | 57,994 | 1.02 | 32,430 | 45.42 |
| SponsorHunter | 714 | 53,417 | 0.94 | 31,539 | 44.17 |
| Migrator | 714 | 47,203 | 0.83 | 32,719 | 45.82 |

#### Criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| Distinct paths (#16) | PASS | No two of 5 specialists match on leading income, median drama (within 5), median credibility (within 5) and viewer CV (within 0.05). |
| Viable specialization (#16, REQ-08) | PASS | Lowest completion 100.0% (Specialist-JustChatting); lowest median followers 51,727,528 = 53.6% of the Rotator median 96,485,251 (Specialist-Chess). |
| Pacing | PASS | Balanced answers 0.78 mandatory decisions per 13 weeks (limit 1). |

#### Economy and risk

Medians by the nearest-rank method. Sponsor share is sponsor income over all income; breach rate is sponsor penalties over paid installments plus penalties; banned counts careers that lost at least one platform; ban endings are `GlobalBan` and `AllPlatformsBanned`.

| Policy | Careers | Insolvency | Median cash PLN | Median score | Sponsor share | Installments | Breach rate | Strikes | Banned | Ban endings | Migrations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 715 | 0.0% | 475,823,530 | 100,000 | 6.3% | 149,802 | 4.2% | 0 | 0 | 0 | 0 |
| Grinder | 715 | 0.0% | 1,264,741 | 72,832 | 10.6% | 77,158 | 20.8% | 0 | 0 | 0 | 0 |
| RestHeavy | 715 | 0.0% | 579,939,164 | 100,000 | 5.7% | 155,314 | 3.3% | 0 | 0 | 0 | 0 |
| Rotator | 715 | 0.0% | 489,998,700 | 99,000 | 6.5% | 165,204 | 4.2% | 515 | 45 | 0 | 47 |
| Networker | 714 | 0.0% | 508,392,170 | 99,000 | 7.5% | 166,902 | 4.0% | 2 | 0 | 0 | 0 |
| Specialist-JustChatting | 714 | 0.0% | 328,817,795 | 99,000 | 1.4% | 38,064 | 0.4% | 0 | 0 | 0 | 0 |
| Specialist-Fps | 714 | 0.0% | 320,868,621 | 99,000 | 3.6% | 73,258 | 0.0% | 0 | 0 | 0 | 0 |
| Specialist-Moba | 714 | 0.0% | 415,974,659 | 99,000 | 1.4% | 52,731 | 0.8% | 0 | 0 | 0 | 0 |
| Specialist-Chess | 714 | 0.0% | 255,557,983 | 100,000 | 8.3% | 140,356 | 0.9% | 0 | 0 | 0 | 0 |
| Specialist-Gambling | 714 | 0.0% | 544,210,874 | 100,000 | 8.5% | 161,833 | 1.7% | 6,014 | 535 | 0 | 813 |
| Gambler | 714 | 0.0% | 321,822,678 | 99,000 | 11.1% | 151,584 | 1.7% | 7,257 | 542 | 118 | 965 |
| SideHustler | 714 | 0.0% | 525,942,129 | 99,000 | 7.4% | 179,244 | 4.1% | 4,102 | 386 | 0 | 540 |
| SponsorHunter | 714 | 0.0% | 423,461,292 | 99,000 | 12.6% | 230,211 | 5.2% | 0 | 0 | 0 | 0 |
| Migrator | 714 | 0.0% | 1,362,993,722 | 99,000 | 3.3% | 166,119 | 3.9% | 0 | 0 | 0 | 714 |

#### M3-B criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| No side business needed (#20) | PASS | Balanced, which never opens the side business, avoids insolvency in 100.0% of careers (at least 90.0%). |
| Gambling is not dominant (#20) | PASS | Gambler median cash 321,822,678 vs Balanced 475,823,530; median score 99,000 vs 100,000; Gambler insolvency 0.0%. |
| Side business is a trade-off (#20) | PASS | SideHustler median cash 525,942,129 = 1.11x Balanced 475,823,530 (between 1.00x and 1.50x). |
| Bans are rare but real (#19) | PASS | 65.0% of Gambler and SideHustler careers lost a platform (at least 1.0%); 1.2% of all careers ended on a global or total ban (below 2.0%). |
| Sponsors matter (#18) | PASS | SponsorHunter earns 12.6% of its income from sponsors (at least 10.0%); breach rate 5.2%. |
| Reconciliation (#18) | PASS | 0 weeks whose cash change differs from their report lines. |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 0 | 13679457532755275413 | Balanced | DormRoomNewcomer | Normal | Completed | 1,040 | 121,072,261 | 523,651,378 | 110,000 |
| 8 | 13477024926058894539 | Specialist-Chess | DormRoomNewcomer | Normal | Completed | 1,040 | 48,531,988 | 247,581,273 | 110,000 |
| 21 | 10120733601449672245 | Specialist-Moba | DormRoomNewcomer | Normal | Completed | 1,040 | 51,031,188 | 303,940,222 | 110,000 |
| 34 | 16570158482149399474 | Specialist-Fps | DormRoomNewcomer | Normal | Completed | 1,040 | 58,321,887 | 348,074,051 | 110,000 |
| 47 | 15057826196960218502 | Specialist-JustChatting | DormRoomNewcomer | Normal | Completed | 1,040 | 41,092,776 | 258,919,260 | 110,000 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 2997 | 5352789347696622238 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,145 | 891,007 | 59,574 |
| 4789 | 9180428893401307020 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,482 | 1,014,254 | 60,656 |
| 197 | 13214800971078148629 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,528 | 1,162,168 | 61,087 |
| 1989 | 16538678858601893261 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,651 | 1,130,141 | 61,243 |
| 5013 | 8653634979005914567 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 1,680 | 1,146,900 | 61,331 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 73.236 s |
| Careers per second | 136.5 |
| Simulated weeks | 10,361,903 |
| ns per simulated week | 7,067 |
| Mean wall time per career | 7.32 ms |
| Allocated | 797152.0 MiB (835,874,500,856 bytes) |
| Peak working set | 113.6 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.13 (Microsoft Windows 10.0.26200) |
