# Reference Game Analysis (Source Level): Polski Piłkarz Simulator 1.45

Source: https://www.tetrycy.com.pl/polskipilkarzsimulator/1.45/
Analyzed on: October 12, 2026

Companion to `polski-pilkarz-simulator-analysis.md`, which describes two playthroughs. This document is derived from the game's JavaScript, which ships unminified with Polish identifiers and comments. It states exact formulas and constants rather than inferred behavior. Written in English to match the existing document.

## Method and Reliability

The game is fully client-side: 12 JS files, ~14,500 lines, no server calls for game logic. Every formula below was read from source. Line references point at version 1.45 as served on the analysis date.

Counts that could be miscounted by naive text search were re-derived by executing the data file and inspecting the resulting objects. Where the playthrough document and the source disagree, the source wins — with one caveat noted under *Limits*.

| File | Lines | Subsystem |
| --- | --- | --- |
| `10-polska-kariera-core.js` | 7096 | Career loop: progression, season simulation, market, UI, saves |
| `08-polska-kariera-dane-klubow.js` | 2230 | Club database |
| `02-nss-silnik-meczowy.js` | 1072 | Interactive match engine |
| `09-polska-kariera-decyzje.js` | 810 | Event/decision catalog |
| `06-nss-kontroler-ui.js` | 756 | Match UI controller |
| `03-nss-turnieje.js` | 686 | Tournament formats and qualification |
| `08a-club-country-colours.js` | 595 | Club colour palettes |
| `07-adapter-polska-kariera.js` | 569 | Bridge between career and match engine |
| `01-nss-dane-reprezentacji.js` | 240 | National team data |
| `01a-nss-szanse-reprezentacji.js` | 227 | Qualification probability table |
| `09b-polska-kariera-naturalizacja.js` | 126 | Naturalization |
| `09a-polska-kariera-imiona-zawodnikow.js` | 59 | Name pool |

The `nss-` prefix marks a shared engine reused by the author's other games; the career game talks to it only through `07-adapter-polska-kariera.js`. That boundary is the cleanest architectural idea in the codebase and the one most worth copying.

## Starting State

`10-core.js:826-862`. A new career rolls:

| Attribute | Initial value |
| --- | --- |
| `overall` | per start option (below) |
| `talent` | `rand(35,95)` — fixed for the whole career |
| `professionalism` | `rand(35,95)` |
| `injuryRisk` | `rand(8,30)` |
| `adaptability` | `rand(35,90)` |
| `recognition` | `rand(12,32)` |
| `loyalty` | 0 |
| `hardRetirementAge` | `rand(52,61)` — hidden |
| `age` | 16, `seasonYear` 2026 |

Start options (`10-core.js:829-833`, and the HTML `<select>`): `backyard` 29–34, `normal` 39–45, `syrenka` 50–55, `wonderkid` 60–66. A fifth hidden option, `sysio` 69–74, appears only when the player name is exactly "Filip Sysio" (`setSpecialStartOption`, `10-core.js:577`). "Jakub Olkiewicz" is a second, separate name easter egg deliberately excluded from the random name pool (comment in `09a`). Two difficulties: `normal` and `easy`.

Note that `talent` is rolled independently of the start option. A `backyard` career can roll talent 95 and a `wonderkid` can roll 35.

## Progression

### OVR scale

`overallCap()` (`10-core.js:379-382`):

```js
const standard = state && state.legendEraActive ? 125 : 99;
return Math.max(standard, state?.grajewskiOverallCap || 0);
```

The working scale is **20–99**. 125 is reachable only inside Legend Era (below). `grajewskiOverallCap` is a third, event-granted override.

### Season OVR change

`10-core.js:2515`. Six additive components, then clamped:

```
growth = ageDrift + seasonGrowth + environment + talentMod + minutesMod + injuryOvrPenalty
overall = clamp(overall + growth, 20, overallCap())
```

A declared crisis forces `growth <= -1`; a "career season" forces `growth >= +1`.

**`ageDrift`** — `rollNaturalGrowth(age)` (`10-core.js:1309`) is a weighted table per age band, not a curve. Selected bands (normal difficulty, `[weight%, delta]`):

| Age | Distribution |
| --- | --- |
| ≤18 | `[[10,0],[50,1],[35,2],[5,3]]` |
| ≤21 | `[[30,0],[52,1],[15,2],[3,3]]` |
| ≤23 | `[[50,0],[45,1],[5,2]]` |
| ≤27 | `[[15,-1],[55,0],[28,1],[2,2]]` |
| ≤30 | `[[22,-1],[60,0],[17,1],[1,2]]` |
| 34 | `[[22,-2],[45,-1],[31,0],[2,1]]` |
| 35 | `[[10,-1],[45,-2],[35,-3],[10,-4]]` |
| ≥43 | `[[10,-5],[30,-6],[40,-7],[20,-8]]` |

Decline becomes mandatory at 35. `easy` difficulty shifts the young bands upward (age ≤18 easy: `[[4,0],[38,1],[45,2],[13,3]]`) but does **not** soften the post-35 collapse. A separate `veteranProfessionalismProtection` recovers 0–2 OVR at age ≥35 with professionalism ≥60.

**`seasonGrowth`** — taken straight from the form roll's `seasonBonus`: crisis −2, poor −1, normal 0, good +1, great +2, career season +3.

**`environment`** — `rollEnvironmentGrowth()` (`10-core.js:1673`) returns −1…+2 from `baseEnvironmentLearning(age, tier) × env.growthFactor × difficultyGrowthFactor` (easy = 1.25) minus a negative risk term. Club environment is itself rolled (`rollClubEnvironment`, `10-core.js:1591`); `TOKSYCZNE` (score ≤ −10) forces delta 0 and makes positive growth impossible, while `WYJĄTKOWE`/`ŚWIETNE` (factors 1.65/1.45) suppress the negative term entirely.

**`talentMod`** — `+1` if `rand(1,100) <= talentChance` (`10-core.js:2496`):

```
age ≤18: clamp(round(8 + talent*0.35), 12, 43)
age ≤21: clamp(round(5 + talent*0.30), 10, 35)
age ≤24: clamp(round(4 + talent*0.20),  7, 24)
age ≤27: clamp(round(2 + talent*0.10),  4, 12)
age ≥28: no talent bonus at all
```

On `easy`: `clamp(talentChance + 10, 0, 60)`.

**`minutesMod`** — `−1` if `apps < 8` and `age <= 27`, on a roll against 65% (age ≤20), 50% (≤23) or 30% otherwise, multiplied by 0.65 on easy (`10-core.js:2509`).

### Form and season grade

`rollSeasonForm()` (`10-core.js:1350`) picks one of six bands:

| Band | Base weight | `seasonBonus` | `hierarchy` | `performanceMod` |
| --- | --- | --- | --- | --- |
| crisis | 4% | −2 | −18 | −6 |
| poor | 9% | −1 | −8 | −3 |
| normal | 49% | 0 | 0 | 0 |
| good | 25% | +1 | +15 | +3 |
| great | 11% | +2 | +35 | +6 |
| career | 2% | +3 | +50 | +10 |

Weights are reshaped by professionalism via a softmax: `w = base * exp(profMod * grade / 22)` where `profMod = clamp(round((professionalism-50)/18), -3, 3)`. Professionalism is therefore a distribution-shaping stat, not a flat bonus — a genuinely good design choice.

Nine season grades (`10-core.js:1789`), index 0–8: `BEZNADZIEJNY, SŁABY, PONIŻEJ OCZEKIWAŃ, PRZECIĘTNY, PRZYZWOITY, DOBRY, ŚWIETNY, WYBITNY, HISTORYCZNY`. `evaluateSeasonGrade` (`10-core.js:1981`) builds a raw score from form position, production delta and a context nudge, then **caps it by minutes share** (`minuteGradeCap`, `10-core.js:1912`): share <5% caps at grade 0, <15% at 2, <25% at 4, <40% at 6, <55% at 7. Goalkeepers are capped on *appearances* instead of minutes — <3 apps caps at 2, <6 at 4, <10 at 5, <15 at 6, <25 at 7 — because a keeper who plays plays all 90, so minutes share carries no information about their role.

### Playing time

`projectedStartChance(club, extraBoost)` (`10-core.js:1540`) — this is the number printed on offer cards:

```js
const qualityGap = state.overall - club.strength;
const loyaltyBonus = (club.name === state.club?.name) ? Math.min(7, Math.max(0, state.loyalty) * .65) : 0;
const raw = 48 + qualityGap*4 + (state.professionalism-50)*.18 + extraBoost + loyaltyBonus;
const minimum = qualityGap <= -25 ? 1 : 4;
return Math.round(clamp(raw, minimum, 97));
```

In-season, form is applied on top: `startChance = clamp(openingStartChance + form.hierarchy, 1, 97)`, capped again by `qualityGapStartChanceCap`. Appearances are then **38 independent Bernoulli trials** at that rate, plus a rotation bonus of `rand(0,5)` if the rate is ≥20 or `rand(0,2)` if ≥10 (`10-core.js:1565`).

Minutes: GK `apps*90`; others `round(apps * (starter ? rand(68,88) : rand(20,58)))`, where `starter = startChance > 62 || careerAppsFloor >= 20`.

Goals and assists are Poisson draws (`10-core.js:2141`):

```
skill       = clamp(0.68 + (overall-38)/48, 0.58, 1.42)
opposition  = clamp(1 - (club.strength-overall)*0.012, 0.72, 1.18)
goalLambda  = n90 * rates.g * skill * productionMultiplier * opposition * finishingBias
```

Per-90 base rates by position: GK (0.001, 0.008), DEF (0.035, 0.055), MID (0.12, 0.22), FWD (0.34, 0.13).

### Injuries

Two independent systems (`10-core.js:2272`, `2375`).

**ACL** — only if `injuryRisk >= 40`. Base 1% (risk ≥43) or 0.5%, plus 2pp at age ≥40, 1.5pp at ≥37, 1pp at ≥34, 0.5pp at ≥30. On a hit: `apps = 0`, OVR −2, `injuryRisk += 5`. A career-ending-grade event with no in-game warning beyond the risk number.

**Regular** — `injuryChance = clamp(injuryRisk + env.injuryMod, 3, 50)`, severity from `rand(1,100)` plus modifiers (+4 if risk ≥30, +3 if age ≥32, +3 if age ≥36):

| Severity | Roll | Matches lost | Risk rise | OVR |
| --- | --- | --- | --- | --- |
| DROBNY | ≤55 | `rand(1,4)` | +1 | 0 |
| ŚREDNI | 56–82 | `rand(5,9)` | +2 | 0 |
| POWAŻNY | 83–95 | `rand(10,16)` | +3 | −1 |
| BARDZO CIĘŻKI | ≥96 | `rand(17,28)` | +5 | −2 |

### Legend Era and retirement

Two consecutive seasons at exactly OVR 99 unlock Legend Era at the start of the third (`10-core.js:6306`): OVR jumps to `99 + rand(1,5)` and the cap rises to 125. It ends when OVR falls below 100.

`legendHistoricalRanking` (`10-core.js:444`) then ranks the player's peak against **9 fictional legends** drawn from a pool of 25. Messi (120–125), Pelé (119–124), Maradona (118–123) and Ronaldo (117–122) are always present; five more are drawn once per career, each scored randomly inside its own range. Ties favour the legend.

Retirement is voluntary from age 39 (offered as a market choice) or forced at the hidden `hardRetirementAge` of `rand(52,61)`. There is no starvation path: if no offer qualifies, a regional tier-1 club is always supplied as a fallback (`10-core.js:6173`).

### Career score

Per season (`10-core.js:2638`):

```
score += round(performance/3) + max(0, seasonTier*2) + (seasonNationalCaps > 0 ? 12 : 0)
```

Plus +18 per promotion and a per-trophy value (Polish title +45, Champions League higher, World Cup 120, continental 90). At retirement (`10-core.js:6882`):

```
score += nationalCaps*3 + trophies.length*24 + awards.length*18 + max(0, peakOverall-60)*2
```

The displayed total is then scaled (`careerScoreScale`, `10-core.js:788`): start multiplier `backyard 1.30, normal 1.15, syrenka 1.00, wonderkid 0.85, sysio 0.75`, difficulty multiplier `easy 0.80, normal 1.00`. This is an elegant handicap system — an easy wonderkid run is scored at 0.68× a hard backyard run — and the game shows the multiplier string to the player.

## Clubs and the Transfer Market

### Database

Re-derived by executing `08-polska-kariera-dane-klubow.js`:

- **860 Polish clubs** across 16 voivodeships. By tier: `1: 508, 2: 239, 3: 61, 4: 16, 5: 18, 6: 18`. Tier names: 1 *ligi regionalne*, 2 *IV liga*, 3 *III liga*, 4 *II liga*, 5 *I liga*, 6 *Ekstraklasa*. Strength ranges are non-overlapping-ish by design: tier 1 is 20–41, tier 6 is 64–80.
- **1299 foreign clubs**, 141 countries, 175 leagues, graded `foreignTier` 1–6: `1: 10, 2: 57, 3: 145, 4: 539, 5: 504, 6: 44`. Zones: Europa 1023, Azja 127, Ameryka Południowa 43, Ameryka Północna 32, Ameryka Środkowa 25, Afryka 28, Oceania 21.
- Club schema is minimal: Polish `{name, region, tier, strength}`, foreign `{name, country, foreignTier, tier, strength, league, zone, marketRegion}`. Note that foreign clubs reuse the `tier` field with values 4–8, which is why a naive text search over the file reports wildly inflated Polish tier counts.
- `foreignTier` 1 is the European elite (Real Madrid 98, PSG 98, Barcelona 96), 6 is deliberately exotic (Mongolian, Bhutanese, Samoan leagues at strength 42–49).

Only `state.club` is mutated on promotion/relegation; the global database is untouched, so careers do not contaminate each other.

### Offers

Career start (`generateOffers`, `10-core.js:867`) always produces exactly 4 cards, with target tiers per start option (`normal` → tiers 2/3/4 plus one foreign T6; `wonderkid` → 1/5/6 plus foreign T4). The supported club has a 65% chance of displacing a same-type slot if it is within reach.

Mid-career (`annualMarketOffers`, `10-core.js:5155`) always produces exactly 3 offers (4 with the permanent `extraMarketOffer` flag); "stay" is added separately. Each candidate gets an `interestChance` (`10-core.js:5215`) starting at 34 and adjusted by, among others:

```
+ strengthDiff*1.8                     // overall - club.strength
+ clamp((performance-36)*0.65, -18, 22)
+ min(11, apps*0.35)
+ (recognition-25)*0.18
+ 16 / +8                              // outstanding / great season
- levelDiff*13                         // moving up is heavily penalised
- 16 (apps<8), -10 more (apps<4)
- 18 (lowStreak>=2), -10 more (>=3)
- 28 (corruptionShadow>0)
+ rand(-12, 12)                        // then clamp(2, 96)
```

Going abroad has hard gates (`10-core.js:650-651`):

| foreignTier | required OVR | required recognition |
| --- | --- | --- |
| T1 | 80 | 73 |
| T2 | 76 | 56 |
| T3 | 65 | 39 |
| T4 | 49 | 25 |
| T5 | 36 | 12 |
| T6 | 28 | 6 |

First move out of Poland relaxes these by −4 OVR / −7 recognition, and is additionally gated by a window roll (`firstForeignWindow`, `10-core.js:718`):

```
chance = 8 + (recognition-18)*1.05 + (performance-32)*0.40 + max(0, overall-58)*1.10
         + 25 (senior international) + 8 (age<=21) + 5 (tier 6) - 12 (apps<8)
```

T1 and T2 carry an extra requirement: `performance >= 34` or senior caps or `recognition >= 82` for T1 (29 / 62 for T2). At tier 3 and below, two of the three offers are locked to the player's own voivodeship. A `WYBITNY` season (grade 7) guarantees one offer a full market level up.

### Wages

This is the most surprising finding. `calcAnnualSalaryForClub` (`10-core.js:1116`):

```js
const mediaFactor = .35 + clamp(state.recognition||20, 0, 100) * .013;
const raw = contractLevelBase(club) * mediaFactor * .75;   // min 4500/yr
```

`contractLevelBase` log-interpolates the club's market level against:

```
[[1,12000],[2,22000],[3,40000],[4,75000],[5,150000],[6,360000],
 [6.25,600000],[6.9,1400000],[7.65,3200000],[8.45,7200000]]
```

where market level is the Polish tier 1–6, or for foreign clubs `{6:4.25, 5:5.15, 4:6.25, 3:6.9, 2:7.65, 1:8.45}` (`clubMarketLevel`, `10-core.js:638`).

**OVR does not enter the wage formula at all** — the source comment says so explicitly ("OVR decyduje o tym, dokąd gracz może trafić, ale nie pompuje pensji drugi raz"). Pay is club tier × recognition only, and recognition alone swings wages by 2.7× across its 0–100 range. Market *value* (`calcValue`, `10-core.js:1101`) is a separate formula using age and recognition. `marketClubFactor` (`10-core.js:1085`) is defined but never called — dead code.

### Contracts

Transfer fees, contract lengths, release clauses and agent percentages **do not exist**. Moves are free, contracts are implicitly one season, and the market reopens every summer. The only duration mechanic is the `early_contract` event's 10-year lock (`longContractActive`, `10-core.js:642`), which blocks the market entirely for ten windows and freezes the wage.

Loyalty is 0–15, gained `rand(2,3)` per season stayed and reset to 0 on any move. It buys exactly three things: up to +7 start chance at the current club, up to +3 interest when staying, and up to 12pp of protection against non-renewal (`10-core.js:6057`).

## Events

`09-polska-kariera-decyzje.js` defines **68 events** (verified by count of `event('…')` definitions), plus 3 injected directly by the core (first national call-up, post-serious-injury, and an age-39+ "quiet year" non-event). Every event has exactly two choices.

Schema: `event(id, weight, cooldown, maxUses, when, make)`. `weight` is clamped up to a minimum of 7 at runtime (`tagEvent`, `10-core.js:4381`), so declared weights below 7 are meaningless. `cooldown` is a minimum age gap; 99 means once per career. Most events have `maxUses: 1`.

**Exactly one event fires per season** (`makeDecision`, `10-core.js:4497`), selected by: hard overrides → a separate 5% roll for `italiano_anti_polish_coach` → a priority tier for story events → an escalating quiet-year chance from age 39 (30%, +10pp/year, 100% at 46) → weighted pick. The critical line is `10-core.js:4534`:

```js
const unseen = regularDecisions.filter(d => !(state.eventMemory[d.id]?.count));
```

Unseen events always beat seen ones. Within a single career, repeats are structurally suppressed until the eligible pool is exhausted. `eventMemory` is per-career and saved with the state, so repeats *across* careers are expected by design.

Events write to a wide set of persistent flags, not just stats: `boost` (−20…+25 start chance), `nextAppsFactor` (0 = a season with zero matches), `forcedSeasonFormRoll`, `marketBonus`, `blockMarketOnce`, `guaranteedForeignOffers`, `extraMarketOffer` (permanent +1 offer, from `miekarski_or_pisz`), `noRenewAfterAge`, `marketLockSeasons`, `corruptionShadow`. Money is never modified directly — events reach the wallet only through the market.

Design spread, by example:

- `citko_training` — 1% for **+20 OVR**, 99% for +25pp injury risk. A deliberate trap.
- `corruption_offer` — 70% guaranteed promotion for the club, 30% −5 OVR plus a suspended season and a 3-season reputation shadow that subtracts 28 from every interest roll.
- `young_minutes` (revolt branch) — 50% for +50pp start chance, 50% for a zero-appearance season *and* a refused renewal.
- `german_anti_polish_coach` / `italiano_anti_polish_coach` — staying forces `nextAppsFactor = 0`, the worst form roll, and a market lock. Country-gated.
- `early_contract` — +10 loyalty for a 10-window transfer lock.
- `miekarski_or_pisz` — a permanent extra market offer versus a 50/25/25 roll for +1/0/+2 OVR.
- `grajewski_friendly` — win multiplies OVR by 1.25 and raises the personal cap; lose ends the career immediately.

Roughly 27 events are situation-gated (country, region, age band, promotion, OVR floor, reserve status); the remainder are available from age 18. There are no true multi-stage chains; `corruption_offer` is the closest, resolving one season later.

## National Team

The companion document lists this as unverified. The source fully specifies it.

**Hard gates** (`10-core.js:4048`): the senior team requires either Ekstraklasa (tier 6) or a foreign T1–T3 club. Anything lower returns a call-up chance of 0 regardless of OVR. A second hard floor: `nationalSelectionOverall() < 74` returns 0, where `nationalSelectionOverall() = overall + (85 - teamOvr)` — for Poland (base 85) that is simply OVR ≥ 74. An active `corruptionShadow` zeroes the national team unconditionally.

**Youth call-ups are deterministic**, not rolled:

- U-19 (`10-core.js:4177`): `age <= 19 && overall >= 51 && seasonTier >= 3 && performance > 30`
- U-21 (`10-core.js:4187`): `age <= 22 && overall >= 58 && seasonTier >= 4 && performance > 33`

Both are cosmetic — they set a label, grant no caps, and do not set `seniorInternational`.

**Senior probability** by band (`10-core.js:4053`), rolled against `rand(1,100)` and clamped to 1–94:

| `nationalSelectionOverall()` | Gate | Base chance |
| --- | --- | --- |
| <74 | — | 0 |
| 74–76 | perf ≥62, apps ≥24 | `2 + (ovr-74)*4 + clamp((perf-62)*0.8,0,8) + rec*0.04` |
| 77–80 | perf ≥40, apps ≥15 | `8 + (ovr-77)*7 + clamp((perf-42)*0.8,0,16) + rec*0.10` |
| 81–84 | — | `35 + (ovr-81)*9 + clamp((perf-35)*0.7,0,16) + rec*0.12` |
| 85+ | — | `70 + min(15,(ovr-85)*3) + clamp((perf-35)*0.45,0,10) + rec*0.08` |

Existing internationals start from higher bases (6/28/55/82). Penalties: −18 if `performance < 22`, −15 if `apps < 8`.

**Data** (`01-nss-dane-reprezentacji.js`): ~200 countries as `[name, zone, baseOvr]` — Poland 85, Spain 98, France 95, England 93. Russia, Belarus and Israel are excluded. Rival nations are abstract strength numbers; individual opposition players are not modelled. Tournament strength is jittered `rand(base-2, base+2)`, with an 8% "golden generation" chance that draws Poland from `[90,91,92]` instead. Qualification odds are a hardcoded lookup table (`01a`), not a formula — Poland is `{world: 50, continental: 75}`.

**Naturalization** (`09b`) is absent from the companion document entirely. Conditions, all required simultaneously: the one-time offer unused, zero senior caps for anyone, three consecutive seasons in the same foreign country, the last season at grade ≥7, the club in that country's top division, and the offering nation's `baseOvr < 75`. Accepting permanently switches the represented country; declining burns the offer. It is irreversible and only ever offered by weak nations — a real strategic fork between a likely Poland career and guaranteed tournament football elsewhere.

**Tournaments** (`03-nss-turnieje.js`, scheduled in `09b:32`): World Cup from 2030 every 4 years (48 teams, 12 groups, 8 best thirds, R32), Euro from 2028 every 4 (24 teams, 6 groups, 4 best thirds, R16), AFCON 2027/2y, Asian Cup 2027/4y, Gold Cup 2027/2y, Copa América 2028/4y, OFC 2028/4y. A tournament only fires if the player was called up that season. The draw seeds by strength rank, caps UEFA at 2 per World Cup group, and retries the shuffle up to 500 times to avoid same-group rematches. Matches can be played in the interactive engine or simulated.

Rewards: +12 score per season with any cap, +3 per cap at retirement, +5/+3/+2 recognition depending on call-up context, trophies worth 120 (World) or 90 (continental). **National team play never raises OVR directly.**

## Interactive Match Engine

`buildMatchBeats` (`02:290`) generates `rand(21,28)` discrete beats at unique minutes in 3–89, plus `rand(7,10)` extra-time beats and a penalty phase at minute 120. Each beat is assigned to a side by a Bernoulli draw on:

```
homeShare = clamp(0.5 + tanh(diff/12)*0.15, 0.35, 0.65)
```

Background beats resolve to a goal with `goalP = clamp(0.11 + tanh((atk-def)/12)*0.07, 0.05, 0.18)`.

The player's contribution to team strength is explicitly diluted: `clamp(teamRating + (playerOVR - teamRating)/11 + 0.6, 1, 99)` for a starting captain, scaled by `(90-m)/90` for a substitute. One player is one eleventh of a team — the same weighting principle the season simulation uses.

**Skills** (`02:50`, `155`): 13 outfield attributes — `shooting, heading, dribbling, setPiece, tackling, passing, flair, control, bothFeet, pace, strength, positioning, vision` — plus `goalkeeping` (set to 1 for outfield players). All clamped 1–99. They are *derived*, not stored: a position delta table plus a per-player variation seeded from a hash of name+position (±5, stable across matches) plus a match-day variation (±1.4), scaled by `edgeScale = clamp(min(ovr-1, 99-ovr)/20, 0.28, 1)` so the spread collapses near the extremes, then re-normalised up to 8 times so the mean returns to OVR. The career layer therefore stores one number and the match layer invents a consistent attribute profile from it — a cheap and effective trick.

**Situations**: `setpiece` (13% of home beats, 42/58 corner/free kick), `high` (19%), `ground` (68%), `defense` on away beats, `goalkeeper` for GKs, and `penalty` in a shootout. Actions resolve through a logistic duel:

```
p = clamp(1/(1+exp(-(skill - (opponent.rating + difficulty))/10)) + bonus, 0.05, 0.95)
```

where `skill` is a weighted average over the relevant attributes. Variants trade reward against risk, e.g. tackling uses `{tackling .48, positioning .22, strength .17, pace .13}` with `+0.17` for aggressive versus `+0.03` for normal — and an aggressive failure carries a 3% straight red and 27% yellow, while a normal failure is usually just a foul. Dribbling has seven named variants (`pace, body, side, cruyff, zidane, nutmeg, step`), each success adding `attackBoost += 0.07` up to 0.24, chainable up to five times.

Match rating starts at **6.0** and is clamped 1–10. Goal +1.00, assist +0.65, successful tackle +0.27/+0.30, pass +0.12, dribble +0.22, GK save +0.32…+0.50; shot off target −0.12, lost pass −0.20, failed dribble −0.24, yellow −0.34, second yellow −0.90, straight red −1.15, hesitation/timeout −0.28.

Condition is not a timer: `conditionPct = clamp(sum(current)/sum(base)*100, 0, 100)`, where each action drains the attributes it used by `cost * 3.2 * (0.8 + rng()*0.45)`. Standing around costs nothing; dribbling past three defenders costs a lot.

Cards carry back to the career (and a national red card sets a one-match suspension, `07:240`), but **in-match injuries do not exist** for outfield players and nothing injury-related is written back. The only state the engine returns to the career besides the stat line is `nextMinutesFactor`, and the adapter takes the minimum, so a match can never improve the next season.

## Comparison with the Playthrough Analysis

### Confirmed

- The six-component OVR breakdown, and the specific example `age +1, season -2, environment 0, insufficient minutes -1`.
- Injury durations: observed "minor = 3 matches, moderate = 6" sits inside `DROBNY rand(1,4)` and `ŚREDNI rand(5,9)`.
- Loyalty +2/+3 per season stayed, capped at 15, and its stated effects on selection, environment and renewals.
- The 73%-at-tier-4 versus 12%-at-tier-2 trade-off is exactly what `projectedStartChance` produces at those strength gaps.
- A club can be relegated while the player improves, because player strength is weighted by minutes — the engine dilutes the player to roughly 1/11 of the team in both layers.
- Low minutes capping the season grade. The observed "3 goals in 225 minutes still rated Below expectations at 7% of minutes" is `minuteGradeCap` returning 2, and `PONIŻEJ OCZEKIWAŃ` is literally grade index 2.
- The 10-year contract locking ten windows.
- Recognition unlocking offers that performance alone would not.

### Corrected

| Playthrough claim | Source |
| --- | --- |
| "OVR uses a 1–125 scale" | Working range is **20–99**. 125 applies only in Legend Era, unlocked by two consecutive seasons at exactly 99. |
| "one of four starting OVR tiers" | Four visible plus a hidden fifth (`sysio`, 69–74) gated on the player name. |
| "'Wonderkid' seasons showed talent bonus chances around 39–46%, varying by season. The complete formula was not established." | The formula is `clamp(8 + talent*0.35, 12, 43)` at age ≤18, dropping by age band to nothing at 28. The observed variation was the age band changing, not a per-season roll. Talent itself is `rand(35,95)` and independent of the start option. |
| "Offers reflect … player strength" for pay | Pay is club market level × recognition only. OVR is explicitly excluded from the wage formula; it only decides *which* clubs appear. |
| "Several events repeated across the two careers. This may reduce freshness over many playthroughs." | Within one career, unseen events are hard-prioritised and most are capped at one use. Cross-career repeats are by design: `eventMemory` resets, and one event per season means a 20-season career samples 20 of 68. |
| "The game awards career points, but … did not show a complete scoring formula" | Fully specified per season and at retirement, then multiplied by a start/difficulty handicap the game does display. |

### Not Found by the Playthroughs

- **Naturalization.** A one-shot, irreversible choice to represent a foreign nation, gated on three seasons in one country and an outstanding season, offered only by nations below 75 strength.
- **Legend Era and the legend ranking** against a randomised pool of nine fictionalised greats.
- **Corruption.** An event that can buy the club a promotion at a 30% risk of a suspended season and a three-season −28 interest penalty.
- **ACL injuries** as a separate low-probability system gated on `injuryRisk >= 40`.
- **The hidden retirement age** of `rand(52,61)`, and voluntary retirement from 39.
- **Professionalism as a distribution shaper** on the form roll via softmax, rather than a flat bonus.
- **Hard national-team gates** that make the senior team unreachable from outside Ekstraklasa or a foreign top-three tier, at any OVR.
- **Absence of transfer fees, contract lengths and release clauses** — a deliberate simplification, not an oversight.
- **Derived match attributes** seeded from the player's name, so the same career produces a consistent skill profile across matches without storing 13 extra numbers.
- The dead `marketClubFactor`, and `adaptability`, which is rolled at creation and referenced only four times.

### Still Open

The companion document's caveats about never having reached retirement or the senior national team are now closed from source. What remains genuinely unverified is dynamic: whether saved careers reload correctly across browser restarts, how the full 68-event pool actually feels over a long career, and whether the balance targets implied by these constants hold in play.

## Implications for Polski Streamer Simulator

The companion document's six recommendations stand. The source adds five more concrete ones.

1. **Store one number, derive the detail.** The match engine builds 13 attributes from OVR plus a name hash. A streamer sim can keep a single "skill" per discipline and derive per-stream detail the same way — consistent, inspectable, no extra save state.
2. **Separate reach from quality, and make reach pay.** Recognition, not OVR, sets wages here; OVR only gates access. The streamer analogue is direct: audience size determines income, content quality determines which opportunities appear. Keeping them on separate axes is what makes "famous but mediocre" and "excellent but unknown" both playable.
3. **Shape distributions, don't add bonuses.** Professionalism reweights the form table through a softmax rather than adding a flat modifier. Consistency stats that narrow variance read very differently from stats that raise a mean, and the former are more interesting for a game about streaks.
4. **Use an explicit handicap multiplier.** `startMultiplier × difficultyMultiplier`, shown to the player as text, makes scores comparable across start conditions without restricting choice. Cheap to implement, and it makes a leaderboard meaningful.
5. **Keep the adapter boundary.** The career layer and the real-time layer communicate through one narrow interface that passes a player snapshot in and a stat line out, with a single conservative write-back. That separation is why the interactive match can exist at all without entangling the season simulation, and it is the structural decision most worth copying.

Two things to avoid. The hard tier gates on the national team mean a mid-tier career can never see a tournament, no matter how well it goes — a whole subsystem invisible to most players. And one event per season against a 68-event pool means a long career spends most of its turns on a single binary choice; a streamer sim with a shorter time unit should either widen the pool substantially or allow several smaller events per period.

## Limits of This Analysis

This describes version 1.45 as served on October 12, 2026. Reading source establishes what the code does, not what players experience: it says nothing about pacing, difficulty balance or whether the constants above produce enjoyable careers. The playthrough document remains the better guide to how the game feels, and the two should be read together.

Club counts, the event count, the OVR cap, the wage chain, the start-chance formula, the start profiles and the score scaling were re-verified directly against the source after an initial pass produced inflated Polish club counts — the `tier` field is shared between Polish and foreign clubs, so text-search counts over the data file are wrong by roughly 2.5×. Other figures come from single readings of the relevant functions and carry the corresponding risk.
