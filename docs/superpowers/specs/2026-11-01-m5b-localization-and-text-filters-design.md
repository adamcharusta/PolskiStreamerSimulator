# M5-B Localization and Text Filters — Design

Date: 2026-11-01
Status: **Agent proposal (2026-11-01).** Delivered without a design interview at the owner's request ("ok kontynyuj zrób cały millestone 5", 2026-11-01). Every rule below is an agent proposal for owner review.
Milestone: [M5 — Content, balance, and quality](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/5), sub-project B, delivering [#35](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/35) (TD-128). The M5 decomposition is in the [M5-A design §0](2026-11-01-m5a-ui-polish-design.md).

## 1. Intent

Finish the Polish presentation layer: no missing translation key, pronoun forms that the player can change, and the two presentation-only text filters of the [content specification §2](../../content-and-experience.md#streamer-friendly-mode). None of them may change an event, roll, score or ranking eligibility.

## 2. Rulings

1. **Settings.** The settings dialog gains a „Filtry tekstu” group with two checkboxes, both off by default: „Tryb Streamer Friendly” and „Łagodniejsze opisy treści wrażliwych”, each with a hint. They are stored per browser in `pss.textFilters.v1`; a missing or malformed value means off. They never reach the server.
2. **Streamer Friendly list.** `source/frontend/src/textFilters/streamerFriendlyList.ts` holds a versioned list (version 1): lower-case Polish and English profanity written without diacritics. A trailing `*` matches every word that starts with the entry; spaces match any whitespace, so multiword phrases work. The list is the agent's first proposal; the owner reviews it as platform policies change, and every change bumps the version.
3. **Matching.** Whole words only (no letter or digit on either side), any letter case, and each plain letter also matches its Polish diacritic form (`a`→`ą`, `c`→`ć`, `e`→`ę`, `l`→`ł`, `n`→`ń`, `o`→`ó`, `s`→`ś`, `z`→`ź`/`ż`). A masked word keeps its first letter; its other letters become `*`; punctuation, digits and whitespace stay.
4. **Where masking applies.** After localization and before display: an i18next post-processor masks every translated string after interpolation (so values passed into a message, announcements and the PDF diploma's translated lines are covered); `formatContent` masks every ICU content string (event titles, texts, choices, outcomes, sponsor names and pitches, achievements); nicknames and deal names rendered directly are wrapped in `masked()` (top bar, career heading, home list, leaderboard, diploma). Switching a filter re-renders every translating component by re-emitting `languageChanged`.
5. **Not masked.** Canonical content, saves and server records; download file names (`*` is not allowed in file names); the owner's `/admin` panel shows nicknames raw so moderation sees what was submitted (its translated strings are still masked).
6. **Sensitive content filter.** `EventView` and `RollView` gain an optional `tags` list (null in responses stored before M5-B, TD-116/TD-119 pattern). With the filter on, an event or roll tagged `adult-business` or `privacy-incident` shows a neutral title, description and outcome text (`filters.sensitiveText.<tag>.*`); choice labels, odds, outcomes and the request sent are unchanged. The current content is already non-explicit, so the neutral wording replaces descriptions rather than hiding choices.
7. **Pronoun editing.** On a career's screens the settings dialog shows „Zaimki w tej karierze” (ona/jej, on/jego, forma neutralna). The choice is stored per career in this browser (`pss.pronouns.v1`) and replaces the creation pronouns in what the screens read (`useCareer` selects it into the career view; the batch result reads it directly). The server keeps the creation pronouns, so the action log, rolls, score and ranking eligibility cannot change; another browser shows the creation pronouns. Previously the content specification promised pronoun editing without saying where it is stored.
8. **Missing keys.** The frontend test setup fails any test that looks up a translation key missing from `pl` without naming its own `defaultValue` (an intended fallback).

## 3. Acceptance

- A full component-test run renders every screen with no missing key (the setup check).
- Filters and pronouns are presentation only: tests show the same request with the Sensitive content filter on, and no request at all for a pronoun change.
- Masking covers translated text, content, nicknames, live announcements and the PDF diploma text; tests cover case, diacritics, word boundaries, punctuation and multiword phrases.
