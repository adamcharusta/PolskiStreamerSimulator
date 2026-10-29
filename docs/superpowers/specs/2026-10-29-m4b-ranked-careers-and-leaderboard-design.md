# M4-B Ranked Careers and Leaderboard — Design

Date: 2026-10-29
Status: **Agent proposal; API in PR 1 ([#96](https://github.com/adamcharusta/PolskiStreamerSimulator/pull/96)), screens in PR 2 (2026-10-29).** The owner asked the agent to deliver every M4 sub-project in order without a design interview ("tak zrób je po kolei wszystkie", 2026-10-29; "pomergowane, kontynuj" after M4-A merged). Every rule, field and wording below is an agent proposal for owner review.
Milestone: [M4 — Online play and administration](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4), sub-project B, delivering [#27](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/27) and the ranked eligibility moved there from [#23](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/23) (TD-122). The M4 decomposition is in the [M4-A design §0](2026-10-29-m4a-career-access-and-retention-design.md).

## 1. Intent

A finished ranked career can put its server-computed score on the season's public top 100 under a short arcade nickname, and that result outlives the career itself. The player never sends a score; the server publishes what its own `Domain` computed. Seasons separate results played under different rules, and old seasons stay readable.

Success means (#27, REQ-15, REQ-17, REQ-18):

- The browser sends only a nickname; the published score, weeks and ending come from the stored final state. Any client score field is ignored.
- Publishing the same career again returns the existing entry; one career has at most one entry.
- The top 100 orders by score, then fewer weeks, then earlier publication, then career ID; the order is the same on every read.
- A nickname has one to five supported characters and is shown as plain text.
- A closed season accepts results for 90 days after it closes, or until a later deadline announced for the whole season.
- Entries remain after the retention sweep deletes their careers; past seasons stay readable.

## 2. Scope

**In scope**

- `Domain`/`Application`: ranked flag at creation, seasons and their deadlines, leaderboard nickname rule, publication, leaderboard and season reads, a season-extension command.
- `Infrastructure`: `game.seasons`, `game.season_extensions`, `game.leaderboard_entries`, `careers.season_id`, startup season rollover, an EF Core migration.
- `WebApp`: `ranked` on `POST /v1/careers`; `GET`/`POST /v1/careers/{id}/leaderboard-entry`; `GET /v1/leaderboard`; `GET /v1/seasons`; `ranked` and `season` on the career view; the `season-extend` command; OpenAPI regenerated.
- Frontend: ranked or unranked choice on the creation screen, the publication panel on the ending screen, the `/ranking` screen with a season picker, ranked labels, Polish copy.

**Out of scope**

- Hiding or revoking entries, outlier review and the admin panel (M4-D, #28/#30). The tables carry `hidden_at` now so M4-D adds no data migration.
- Season rollover from published content drafts and text-only revisions that keep the season (M4-E, #29).
- Manual slots and import, which only unranked careers get (M4-C, #26).
- Notices about deadline extensions on the home screen (M4-F, #31); the seasons endpoint already returns them.

## 3. Ranked careers

`POST /v1/careers` takes `ranked` (boolean, default `false` so older clients keep creating unranked careers). The value is part of the M4-A request hash, so a repeat with a different choice is `creation-id-reused`. A ranked career pins the season open at creation (`careers.season_id`); an unranked career has no season. Careers created before M4-B stay unranked.

Ranked and unranked careers play the same rules. The differences: only a ranked career can publish, and from M4-C only an unranked career can use manual slots and import. The creation screen explains both before play and preselects **ranked**.

If the open season does not match the content the server pins for new careers (a rolling deployment between two content versions), ranked creation fails with `503 season-unavailable` and unranked creation still works.

## 4. Seasons

A season is identified by the ruleset ID and content digest that new careers pin. `game.seasons` holds `id` (identity, also the public season number), `ruleset_id`, `content_digest`, `opened_at` and `closed_at` (null while open). A filtered unique index allows one open season.

On startup, after migrations and content storage, the server ensures the open season matches the current content. If none is open, it opens season 1. If the open season has another digest, it closes it at the current time and opens the next one in the same transaction. A concurrent start that loses the unique index reads the winner. Until M4-E adds text-only revisions, any content change, including a text fix, therefore starts a season; a rollback to older content also opens a new season rather than reopening a closed one.

**Submission deadline.** An open season always accepts. A closed season accepts until `closed_at + 90 days`, or until the latest extension if one exists. `game.season_extensions` keeps every extension (`season_id`, `deadline`, `reason`, `actor`, `created_at`) as its own audit record. An extension only moves the deadline later and applies to every career in the season; the server compares one timestamp for all of them. The owner adds it with `season-extend --season <n> --until <ISO date> --reason <text>` (actor `command`); M4-D adds the admin form that writes the same row.

`GET /v1/seasons` returns every season newest first: `number`, `openedAt`, `closedAt`, `submissionDeadline` (null while open), `extended` and `current`.

## 5. Leaderboard nickname

The rule: Unicode NFC, trimmed, uppercased with the invariant culture, then one to five characters from `A–Z`, `ĄĆĘŁŃÓŚŹŻ` and `0–9`. No spaces or symbols, so the name renders safely as text and looks like an arcade tag. A rejected name is `400 validation-failed` with the rule; the player corrects it and the ended career is untouched. Offensive names are handled by owner moderation in M4-D, not by a word list.

## 6. Publication

`POST /v1/careers/{id}/leaderboard-entry` with the career's bearer secret and `{ "nickname": "..." }`.

1. The usual career access check (`404 career-not-found`).
2. If an entry for the career exists, return it unchanged (`200`), whatever nickname this request carries. The name is frozen at first publication.
3. Otherwise require, in order: a ranked career (`409 career-unranked`); an ended career with a final score (`409 career-not-ended`); a season still accepting (`409 submission-closed`); a valid nickname (`400 validation-failed`, checked after eligibility so a repeat always returns the stored entry).
4. Store the entry and return it.

An entry copies the public fields and the receipt needed to audit it later: `id` (UUIDv7), `season_id`, `career_id`, `nickname`, `score` (the `Domain` final score), `weeks_played` (the ending summary's weeks), `difficulty`, `ending`, `ruleset_id`, `final_state_version`, `final_state_hash`, `submitted_at`, and `hidden_at` (always null until M4-D). `career_id` has a unique index and **no foreign key**, so the retention sweep deletes the career and leaves the entry. A concurrent duplicate publication loses on the unique index and returns the stored entry.

`GET /v1/careers/{id}/leaderboard-entry` (bearer) tells the ending screen what to offer: `status` is `unranked`, `notEnded`, `open`, `closed` or `published`, with `season`, `submissionDeadline` and, when published, the `entry` (nickname, score, weeks, submitted at, rank).

## 7. Leaderboard

Ordering: `score` descending, `weeks_played` ascending, `submitted_at` ascending, `career_id` ascending; only entries with `hidden_at` null. Rank is one plus the number of visible entries of the same season ordered before the entry, so a rank outside the top 100 is still reported to its owner.

`GET /v1/leaderboard?season=<number>&limit=<1–100>` returns the season (default: the current one) and its entries: `rank`, `nickname`, `score`, `weeksPlayed`, `difficulty`, `ending`, `submittedAt`. It is public, sends `Cache-Control: public, max-age=30`, and exposes no career ID, secret, state hash or IP address. An unknown season is `404 season-not-found`.

Difficulty shares one table; the score already applies the difficulty multiplier (GDD).

## 8. Career view and stored contracts

`CareerView` gains `ranked` (default `false`) and `season` (season number or null). Both are fixed for a career's life, so stored step responses stay correct; responses stored before M4-B read with the defaults (TD-91). The publication status changes after the ending and therefore lives in its own endpoint (§6), not in the stored view.

## 9. Screens (PR 2)

- **Creation:** a fieldset "Tryb kariery" with "Rankingowa" (preselected; "Wynik trafi do rankingu sezonu. Bez ręcznych zapisów i importu.") and "Bez rankingu" ("Grasz dla siebie; wynik nie trafia do rankingu.").
- **Dashboard and ending:** a "Rankingowa · Sezon N" or "Bez rankingu" label.
- **Ending:** a "Ranking" panel driven by the status endpoint: a nickname form (1–5 characters, hint with the rule, deadline for a closed season), the published entry with its rank and a link to the season table, or the reason publication is not possible.
- **`/ranking`:** a season picker (`?sezon=N`, default current), a table with Miejsce, Nick, Wynik, Tygodnie, Poziom and Zakończenie, an empty state, and the closed season's deadline. Linked from the home screen and the ending panel.

## 10. Rulings

1. A ranked flag at creation, default unranked in the API and ranked in the UI.
2. Seasons keyed by ruleset and content digest, rolled over at startup; one open season.
3. Closed-season deadline `closed_at + 90 days` or the latest uniform extension, stored as audit rows and added by the `season-extend` command.
4. Nickname: 1–5 of `A–Z`, Polish capitals and digits, uppercased.
5. Publication idempotent by career ID; the first nickname is final.
6. Entries copy their public fields and audit receipt and have no foreign key to careers.
7. Ordering score, weeks, publication time, career ID; public top 100 with a 30-second cache.
8. The first ranked season runs on the current prototype balance (`m3-systems.2`). Balance changes start new seasons, so early seasons are effectively test seasons; the owner can decide later whether to label them.

## 11. Testing

- Unit: nickname rule; deadline with and without extensions; ordering and rank with ties; publication paths (unranked, not ended, closed, invalid name, repeat with a different name); season rollover (none, same digest, new digest).
- Store contract (in-memory and SQL Server): unique entry per career, concurrent duplicate, entries survive `DeleteExpiredAsync`, one open season.
- API: client score fields are ignored; leaderboard exposes only public fields and caches; seasons list; `ranked` in the repeat hash.
- Migration: existing careers stay unranked with no season.
- Frontend: creation sends `ranked`; ending panel states; leaderboard table and season picker; Playwright journey publishes a ranked retirement and finds it on `/ranking`.
