# M1 Baseline Simulation Report

Date: 2026-10-13. Source: batch simulator ([#6](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/6)) on ruleset `m1-prototype.1` and content `m1.1`, reproduced with:

```sh
cd source/backend
dotnet run -c Release --project Tools/BatchSimulator -- --careers 10000 --seed 42
```

The deterministic section below is byte-identical for the same arguments and content; the performance section depends on the machine. The M1 numbers are a prototype baseline ([gameplay §10](../gameplay-systems.md#10-m1-prototype-ruleset-m1-prototype1), [TD-83](../technical-decisions.md)), so these results describe the current constants, not the intended balance.

## Summary

- **Every career completes.** All 10,000 careers reach week 1,040 with no insolvency and no invariant failure, across all five policies, four backgrounds and both difficulties. Income outgrows living cost and upkeep within the opening weeks, so the insolvency ending is unreachable for the built-in policies.
- **Scores saturate.** 6,000 careers (every Balanced, RestHeavy and Rotator career) reach both normalization caps (1,000,000 followers and 10,000,000 PLN) and all 10 achievement points. Their final score (90,000–110,000) is decided only by the difficulty and background multipliers.
- **Growth runs away.** Rotator careers reach a median of 8.1 million followers and 73.5 million PLN; the linear follower term (0.025 × followers) and conversion compound without a ceiling.
- **Rotation dominates specialization.** Median followers: Rotator 8.1 M, Balanced 1.7 M, Specialist 102 k, Grinder 74 k. Boredom on a single category cuts live viewers and adds churn so strongly that the specialization requirement (REQ-08: a one-category career can succeed, only slower) is not met.
- **Multipliers outweigh play.** Easy careers score lower than normal ones at the median (90,000 vs 99,000) because the 1.10 normal multiplier applies to saturated base scores; Industry Insider (0.90) cannot exceed 99,000. Background starting advantages change median outcomes by less than 2%.
- **Cost of a full career.** 10,400,000 simulated weeks took 40.5 s on 16 logical processors (about 3.9 µs per week and 4.05 ms per career of wall-clock time). A separate single-threaded run of 200 careers measured about 26.9 ms per 1,040-week career (25.8 µs per week). The immutable state allocates about 29.6 KB per simulated week; the peak working set stayed near 105 MiB.

## Balance follow-ups

These are inputs for the balancing work in milestone 5 ([#36](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/36)) and the M3 systems; none changes the M1 ruleset.

1. Bound audience growth: shrink or remove the linear follower discovery term and add diminishing returns to follower conversion.
2. Scale operating costs with audience size (gameplay §6) and add the M3 cost and risk systems (sponsors, drama incidents, bans) so that weak strategies can become insolvent.
3. Revisit the 1,000,000-follower and 10,000,000-PLN normalization caps against the new p95 outcomes so top careers stay distinguishable.
4. Rebalance boredom (audience factor and churn) so a specialist career is slower but viable, and rotation is not strictly dominant.
5. Re-evaluate the difficulty and background multipliers once base scores no longer saturate.
6. Consider reducing per-week allocations (for example, fewer intermediate immutable copies during settlement) before large M5 balance sweeps.

## Generated report

### Deterministic results

- Careers: 10,000; master seed 42; policies: Balanced, Grinder, RestHeavy, Specialist, Rotator.
- Ruleset `m1-prototype.1`; content m1.1 (`37e4f6d0a7a4c4a175b0383613b8fe5dfc5393bca08c94dca76c126b054c4bb2`).
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
| Balanced | 2,000 | 2,000 | 0 | 1,369,320 / 1,690,367 / 1,956,251 / 2,133,281 | 25,219,923 / 27,304,975 / 29,164,063 / 30,902,360 | 90,000 / 99,000 / 110,000 / 110,000 |
| Grinder | 2,000 | 2,000 | 0 | 64,569 / 73,652 / 83,789 / 88,520 | 3,077,913 / 3,487,734 / 3,982,448 / 4,112,272 | 77,665 / 85,129 / 93,322 / 93,638 |
| RestHeavy | 2,000 | 2,000 | 0 | 1,460,212 / 1,823,460 / 2,106,293 / 2,338,652 | 26,921,447 / 29,247,661 / 31,231,268 / 33,067,153 | 90,000 / 99,000 / 110,000 / 110,000 |
| Specialist | 2,000 | 2,000 | 0 | 91,185 / 102,329 / 129,432 / 135,386 | 3,922,507 / 4,338,844 / 5,558,624 / 5,834,583 | 82,410 / 89,891 / 95,432 / 99,031 |
| Rotator | 2,000 | 2,000 | 0 | 7,122,831 / 8,121,794 / 10,558,831 / 11,301,074 | 64,655,626 / 73,547,427 / 93,685,370 / 99,031,435 | 90,000 / 99,000 / 110,000 / 110,000 |

#### By background

| Background | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DormRoomNewcomer | 2,500 | 2,500 | 0 | 65,000 / 1,541,589 / 9,989,888 / 10,767,049 | 3,090,666 / 26,742,554 / 88,523,288 / 93,987,600 | 85,438 / 100,000 / 110,000 / 110,000 |
| LocalMemeCreator | 2,500 | 2,500 | 0 | 70,004 / 1,551,754 / 10,137,732 / 11,121,620 | 3,312,504 / 27,121,512 / 90,191,344 / 98,864,651 | 82,452 / 96,000 / 105,600 / 105,600 |
| ChessClubRegular | 2,500 | 2,500 | 0 | 70,264 / 1,552,649 / 10,162,851 / 11,301,074 | 3,297,342 / 27,105,725 / 90,236,566 / 99,031,435 | 84,157 / 98,000 / 107,800 / 107,800 |
| IndustryInsider | 2,500 | 2,500 | 0 | 75,104 / 1,559,711 / 10,265,936 / 11,012,627 | 3,546,821 / 27,438,832 / 91,725,903 / 97,114,308 | 77,685 / 90,000 / 99,000 / 99,000 |

#### By difficulty

| Difficulty | Careers | Completed | Insolvency | Followers p5 / p50 / p95 / max | Cash PLN p5 / p50 / p95 / max | Score p5 / p50 / p95 / max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Normal | 5,000 | 5,000 | 0 | 67,073 / 1,819,508 / 7,532,171 / 8,121,794 | 3,178,525 / 27,693,365 / 68,261,953 / 73,547,427 | 85,129 / 99,000 / 110,000 / 110,000 |
| Easy | 5,000 | 5,000 | 0 | 74,410 / 1,470,833 / 10,349,643 / 11,301,074 | 3,538,904 / 26,602,863 / 92,053,686 / 99,031,435 | 77,900 / 90,000 / 100,000 / 100,000 |

#### Top 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 0 | 13679457532755275413 | Balanced | DormRoomNewcomer | Normal | Completed | 1,040 | 1,990,189 | 28,900,679 | 110,000 |
| 24 | 3178540356880283481 | Rotator | DormRoomNewcomer | Normal | Completed | 1,040 | 7,115,518 | 64,475,394 | 110,000 |
| 32 | 5329533497540776603 | RestHeavy | DormRoomNewcomer | Normal | Completed | 1,040 | 2,070,251 | 31,285,289 | 110,000 |
| 40 | 1002036849063315980 | Balanced | DormRoomNewcomer | Normal | Completed | 1,040 | 1,847,282 | 27,371,685 | 110,000 |
| 64 | 2475505609494469522 | Rotator | DormRoomNewcomer | Normal | Completed | 1,040 | 7,326,216 | 67,120,439 | 110,000 |

#### Bottom 5 careers by score

| Career | Seed | Policy | Background | Difficulty | Ending | Weeks | Followers | Cash PLN | Score |
| ---: | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: |
| 471 | 8714100217554564121 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 79,171 | 3,812,588 | 77,442 |
| 631 | 7182589460665118036 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 79,221 | 3,823,950 | 77,451 |
| 2511 | 10732667188777012763 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 78,676 | 3,862,383 | 77,457 |
| 9111 | 8791690178097276605 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 78,441 | 3,883,044 | 77,461 |
| 791 | 2419886441074615048 | Grinder | IndustryInsider | Easy | Completed | 1,040 | 78,928 | 3,866,524 | 77,468 |

#### Invariant failures

Invariant failures: none.

### Performance

| Metric | Value |
| --- | ---: |
| Elapsed | 40.505 s |
| Careers per second | 246.9 |
| Simulated weeks | 10,400,000 |
| ns per simulated week | 3,894 |
| Mean wall time per career | 4.05 ms |
| Allocated | 293578.2 MiB (307,839,025,288 bytes) |
| Peak working set | 104.6 MiB |
| Parallelism | unbounded on 16 logical processors |
| Runtime | .NET 10.0.12 (Microsoft Windows 10.0.26200) |
