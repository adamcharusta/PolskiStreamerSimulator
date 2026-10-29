# M4-C Unranked Saves — Implementation Plan

Spec: [M4-C design](../specs/2026-10-29-m4c-unranked-saves-design.md) (TD-123, #26). Two PRs into `develop`: PR 1 API (`feature/m4c-1-saves`), PR 2 screens (`feature/m4c-2-screens`, stacked).

## PR 1 — API

0. Docs: spec, this plan, TD-123, GDD log row, architecture API rows, traceability REQ-16.
1. **Log line of play.**
   - `ActionKind.Restore`, plus `RestoredFrom` on `NewAction` and `StoredAction`.
   - `CareerLineage.Current(actions)`: a backwards walk with jumps.
   - `ListReportsAsync` returns the current line, and `FindLatestReportAsync(careerId, atOrBefore)` walks from a sequence.
   - Both stores, and the contract tests with nested restores.
2. **Restore append.** `AppendAsync` writes `restored_from_sequence`. A `Restore` entry also sets the ending date from the restored state, clearing it for an active state.
3. **Slots.**
   - `ISaveSlotStore`: list, find, and save. Save records the sequence of the log entry that produced the saved version, or 0 when there is none.
   - `game.unranked_save_slots` with a cascade, `EfSaveSlotStore`, the in-memory store, and contract tests.
   - The migration `M4cSaves`.
4. **Handlers.**
   - `ListSaveSlots`, `SaveToSlot` and `RestoreSlot`. Restore is repeat-safe by command ID and returns an `ActionOutcome`.
   - `ExportCareer` returns `SaveFile`.
   - `ImportCareer` runs the §5 validation order and creates a new unranked career. When an imported state has ended, `CreateAsync` stores the ending date.
   - New error codes: `slot-not-found`, `slot-empty`, `career-ranked`, `import-invalid`, `import-newer`, `import-unsupported`.
5. **WebApp.**
   - The five endpoints. Import is rate-limited with the creation policy and has a 256 KiB body limit.
   - Request validators, the `ApiProblems` statuses, OpenAPI summaries and security, then `openapi/v1.json` regenerated.
6. **Tests.**
   - Unit tests per handler path, plus the schema-1 fixture `Tests/Fixtures/saves/schema-1.json`.
   - API tests: round trip, ranked `409`, oversized body `413`, and a rejected import that leaves the career count unchanged.
   - A migration test.
   - A size test for a full 1,040-week state.
7. Regenerate the frontend client, open the PR and get Sonar to 0.

## PR 2 — Screens

1. `/kariera/$careerId/zapisy` route.
   - Slot cards with confirmations.
   - Restore reloads the career query.
   - The export downloads `kariera-<nick>-tydzien-<n>.json`.
   - The top-bar link appears only for unranked careers.
2. `/importuj` route.
   - A file input.
   - The error copy per code.
   - On success it remembers the career and secret, shows the key box and links to the career.
   - A home link.
3. Polish copy, Vitest tests, and a Playwright journey (save → week → restore; export → import), followed by axe.
