# Reference Game Analysis: Polski Piłkarz Simulator 1.45

Source: https://www.tetrycy.com.pl/polskipilkarzsimulator/1.45/  
Observed on: October 12, 2026

## Scope

I played two careers across nine seasons in total (ages 16–19 and 16–20) and manually played a CONCACAF Champions Cup final. I examined character creation, initial club selection, season reports, decisions, the transfer market, career history, and save options. This document describes behavior observed in those playthroughs. It is neither a complete event catalog nor a reconstruction of the game's code.

| Playthrough | Setup | Key events |
| --- | --- | --- |
| A | Striker, “Normal guy,” 42 OVR, normal difficulty | Started in Poland's fourth tier, had a strong season, then transferred to the second tier with a 12% projected chance of playing. Played only 29 minutes and fell from 46 to 44 OVR. Moved to the third tier, experienced club relegation, then moved to a regional league and suffered a moderate injury. |
| B | Striker, “Wonderkid,” 66 OVR, easy difficulty | Started in the Ekstraklasa and experienced club relegation. After a transfer, scored 20 goals in the first tier and received a Poland U-19 call-up. Later scored 20 Ekstraklasa goals and earned four individual honors. Transfers to MLS and the Premier League raised wages but reduced playing time. An interactive cup final appeared in MLS. |

## Core Game Loop

1. **Create a player:** Choose a name, position, stronger foot, Polish region, one of four starting OVR tiers, difficulty, and a supported club.
2. **Choose an initial contract:** Pick one of four offers. Each card shows the club, competition level, a short description, and estimated pay.
3. **Play a season:** The game simulates appearances, player statistics, form, club results, injuries, development, awards, and media profile. In one playthrough, a cup final interrupted the simulation with a choice to play or simulate it.
4. **Review the season:** The report shows appearances, goals, assists, minutes, OVR before and after, form, final rating, league position, trophies, awards, environment, injuries, and media profile. An expandable panel explains dice rolls and contributing factors.
5. **Resolve a story event:** Most events offer two choices. Potential effects and often their probabilities appear before selection; the outcome and changed attributes appear afterward.
6. **Choose the next club:** Stay or take another offer. Each option shows projected monthly pay and chance of playing. Selecting a club ends the year; the next season begins one year older.
7. **Track the career:** A point total and expandable timeline remain available. The timeline records seasons, transfers, choices, awards, call-ups, and random rolls.

## Observed Mechanics

### Development and Season Rating

- OVR uses a 1–125 scale. The report breaks its change into age, season performance, environment, talent, and sometimes a penalty for insufficient minutes. In playthrough A, 29 minutes led to a 46→44 change: `age +1`, `season -2`, `environment 0`, `insufficient minutes -1`.
- Form is rolled and provides the starting point for a season rating. Match statistics can change the rating, but limited playing time caps the final result. In playthrough B, three goals in 225 minutes still produced a “Below expectations” rating because the player featured for only 7% of the available time.
- Reports expose individual 1–100 rolls and the probability of a talent bonus. Observed “Wonderkid” seasons showed talent bonus chances around 39–46%, varying by season. The complete formula was not established.
- Club results do not simply follow player results. A club can be relegated even while the player improves or wins awards. The roll details state that the player's contribution to team strength is weighted by minutes played.
- Injury risk is displayed on the player card. Injuries can remove appearances; I observed a minor injury costing three matches and a moderate injury costing six.

### Contracts and Transfers

- Offers reflect competition level, geography, season performance, player strength, and career context. Descriptions included “local market,” “step up,” “return home,” and “club you support,” alongside foreign competition tiers T2–T6.
- Pay and projected playing time form a meaningful trade-off. After his first season, the player in playthrough A could remain in the fourth tier for PLN 750 per month with roughly a 73% chance of playing, or move to the second tier for PLN 2,580 with a 12% chance. The transfer resulted in one appearance and OVR regression.
- Media profile affects market visibility. An event added `+20 media profile` (59→79); Premier League T2 offers then appeared despite an average MLS season. The offer labels explicitly cited “media profile 79/100.”
- Staying raises loyalty by two or three points, on a scale capped at 15. The game's description says loyalty helps with selection, the club environment, and contract renewals. I did not experimentally measure the size of those effects.
- Clubs can be promoted or relegated. A change in club division affects the projected pay and terms of staying.

### Decisions and Events

- Events usually present two approaches: safer development versus risk, or relationships versus professionalism. Examples include attending a veterans' poker night (`+5 loyalty`, 50% risk of `-1 OVR`) versus declining (`+10 professionalism`); and fighting for a place (`50% chance of +10 percentage points to playing chance`, otherwise `-7`) versus a revolt with much larger upside and a severe failure outcome.
- Some decisions have long-term consequences. A ten-year contract grants `+10 loyalty` but makes staying the only option for the next ten transfer windows.
- Several events repeated across the two careers, including poker night, a highlight-video edit, and the ten-year contract. This may reduce freshness over many playthroughs.
- Decision results enter the timeline with the roll and attribute changes. A separate “Continue” step leads from the result to the transfer market.

### Goals and Recognition

- The game awards career points, but the screens I examined did not show a complete scoring formula. In playthrough B, the total rose through 13→52→151→165→179 points.
- I observed Team of the Season, Top Scorer, Player of the Season, and Polish Breakthrough of the Year awards. Awards raised media profile and received their own timeline entries.
- A Poland U-19 call-up appeared at age 17 after a strong first-tier season. I did not reach a senior national-team debut, so the national tournament rules remain unverified.

### Interactive Match

- The CONCACAF Champions Cup final could be played or simulated. The interactive match shows the score, both teams' strength, minute-by-minute commentary, player condition, and match rating. It offers three simulation speeds.
- Play pauses for selected player situations. Available actions depend on context; I encountered a tackle, shot, and header. An action may have variants such as normal versus aggressive tackling or technical versus powerful shooting.
- The match screen lists detailed skills, including shooting, dribbling, passing, vision, first touch, and positioning. Actions update the match rating and counters for attempts, losses of possession, passes, and goals. The final I played ended in a 0–1 loss with a 5.5 player rating.
- The final's appearance was added to the season accounting as `+1 match`, `+90 minutes`, `+0 goals`, and `+0 assists`.

## Interface and Pacing

- On desktop, a persistent player card sits on the left; the season report, decisions, and offers occupy the right. Offer colors follow club colors. Typography and borders evoke a sports newspaper.
- Important consequences are visible before a decision, while the random rolls and OVR factors can be inspected after the season. This makes unfavorable results easier to understand.
- One turn covers a whole season. Career progress is fast, but there is little direct control between the start and end of a typical season apart from occasional interactive matches.
- Options include currency, a new or old season panel, a classic or club-based theme, new game, save, and load. The save dialog describes local saves with unlimited slots and states that saving works between seasons, outside an active decision or match. The game also offers PWA installation.

## Directions for Polski Streamer Simulator — Not Yet a Design Specification

1. Keep a clear loop: **creator development → content and events → period report → decision → new opportunities**. A streamer game needs a shorter time unit than a football season so content and platform choices recur more often.
2. Show trade-offs before selection: fast audience growth versus burnout, revenue versus independence, and a large platform versus difficulty gaining exposure. Show projected reach, costs, and risks as clearly as the reference game shows pay and playing chances.
3. Separate creator skill from public visibility. In the reference game, media profile can unlock strong offers after an average season; for a streamer, reach could unlock sponsors without guaranteeing content quality or viewer retention.
4. Preserve result breakdowns and a history of random rolls, while distinguishing consequences of player choices from chance. Reports should explain changes in viewers, income, skills, and reputation.
5. Build more connected events and long-term consequences to prevent repeated playthroughs from exhausting the event pool quickly. Collaborations, conflicts, trends, platform changes, moderation, equipment, and mental health could form distinct event lines.
6. Occasional interactive “major streams” could serve the role of playable finals: rare events with real-time choices whose results feed into the period report.

## Limits of This Analysis

I did not test retirement, the senior national team and its tournament, every player position or starting tier, the full event catalog, or whether a saved career reloads correctly after restarting the browser. Individual playthroughs do not establish the exact probabilities or formulas of hidden mechanics.
