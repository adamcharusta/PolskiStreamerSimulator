# M4-B Ranked Careers and Leaderboard — Implementation Plan

Design: [M4-B design](../specs/2026-10-29-m4b-ranked-careers-and-leaderboard-design.md) (TD-122). PR 1 covers tasks 0–7 (API), PR 2 tasks 8–11 (screens).

**Task 0 — Docs.** Spec, this plan, TD-122, GDD decision row, architecture API table and §4 tables, traceability REQ-17/REQ-18, content §3 notes.

**Task 1 — Nickname and ordering.** `Application/Leaderboard/LeaderboardNicknames` (normalize, validate, rule text) and `LeaderboardOrder` (comparison and rank). Unit tests first.

**Task 2 — Seasons.** `Season` and `SeasonExtension` records, `Seasons.SubmissionDeadline(season, extensions)` and `AcceptsAt(now)`; `ISeasonStore` with `EnsureCurrentAsync(rulesetId, digest, now)`, `FindAsync(number)`, `CurrentAsync`, `ListAsync`, `ExtendAsync`. Deadline and rollover tests on an in-memory store.

**Task 3 — Ranked creation.** `CreateCareer.Ranked`, part of the request hash; `NewCareer.SeasonId`; `StoredCareer.Ranked`/`SeasonId`; `season-unavailable`. `CareerView.Ranked`/`Season`. Tests for ranked and unranked creation, repeat with a changed flag, season mismatch.

**Task 4 — Publication.** `ILeaderboardStore` (`FindByCareerAsync`, `AddAsync` returning added or duplicate, `RankAsync`, `TopAsync`); `PublishResultHandler`, `GetEntryStatusHandler`, `GetLeaderboardHandler`, `ListSeasonsHandler`; error codes `career-unranked`, `career-not-ended`, `submission-closed`, `season-not-found`, `season-unavailable`.

**Task 5 — Persistence.** Rows, mappings and migration `M4bRanking` (`seasons`, `season_extensions`, `leaderboard_entries`, `careers.season_id`); `EfSeasonStore`, `EfLeaderboardStore`; `DatabaseInitializer` ensures the season; store contract tests including entries surviving the sweep; migration test.

**Task 6 — HTTP.** Request records and validators, endpoints, problem statuses, cache header, OpenAPI summaries, `season-extend` command; regenerate `openapi/v1.json`; API tests (client score ignored, public fields only, repeat).

**Task 7 — PR 1.** Format, unit and integration tests, push, PR into `develop`, Sonar zero.

**Task 8 — Client and creation.** Regenerate Orval; creation fieldset and copy; label on the dashboard.

**Task 9 — Ending panel.** Status query, nickname form, published entry, closed and unranked states.

**Task 10 — Leaderboard screen.** `/ranking` route, season picker, table, empty state, home link.

**Task 11 — PR 2.** Vitest, Playwright journey (ranked retirement published and listed), local E2E, PR, Sonar zero.
