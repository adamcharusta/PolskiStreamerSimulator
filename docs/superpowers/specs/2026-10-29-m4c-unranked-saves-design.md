# M4-C Unranked Saves — Design

Date: 2026-10-29
Status: **Agent proposal; API in PR 1, screens in PR 2 (2026-10-29).** The owner asked the agent to deliver every M4 sub-project in order without a design interview ("tak zrób je po kolei wszystkie", 2026-10-29; "zmergowane" after M4-B merged). Every rule, field and wording below is an agent proposal for owner review.
Milestone: [M4 — Online play and administration](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4), sub-project C, delivering [#26](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/26) (TD-123). The M4 decomposition is in the [M4-A design §0](2026-10-29-m4a-career-access-and-retention-design.md).

## 1. Intent

An unranked career is the player's sandbox. The player can keep three manual snapshots of it, go back to any of them, and carry it to another server or device as a file. A ranked career never gets any of this, because a rewind or an edited file would change a published score (TD-02, TD-05).

Success means (#26, REQ-16):

- Each unranked career has three independent slots. Saving one never changes the other two, and restoring one never changes any slot.
- Ranked careers have no slots, export or import. Every save endpoint answers a ranked career with `409 career-ranked`.
- An import creates a **new** unranked career. A file that is invalid, damaged, newer than the server or built on content the server does not have is rejected, and no existing career or slot changes.
- After a restore, the history shows the restored line of play. Weeks from the abandoned line are not mixed in.

## 2. Scope

**In scope**

- `Application`: slot list, save and restore; export and import of a versioned save file; history and latest-report reads that follow restores.
- `Infrastructure`: a `game.unranked_save_slots` table, a `restored_from_sequence` column and a `Restore` kind on `career_actions`, and an EF Core migration.
- `WebApp`: the endpoints in §6, a 64 KiB body limit on import, and a regenerated OpenAPI document.
- Frontend: a `/kariera/{id}/zapisy` screen with the three slots and export, and an `/importuj` screen that reads a file and hands over the new career's recovery key. Both get Polish copy.

**Out of scope**

- Older export schemas. Schema 1 is the first one. Later schemas add upgraders and keep a fixture per supported schema (validation §4).
- Repeat-safe import. A lost import response leaves an orphan career that the six-month sweep deletes (§5).
- Notices (M4-F) and content administration (M4-E).

## 3. Slots

- Each unranked career has slots numbered 1, 2 and 3, stored in `game.unranked_save_slots`: the career ID and slot form the key, plus the canonical state JSON, the action-log sequence at save time and the save time. Rows cascade with the career, so the retention sweep deletes slots with the career (TD-46).
- **Save** writes the current server state into one slot and replaces whatever it held. The request carries `expectedVersion`. A mismatch answers `409 stale-version`, so the player always saves the state they are looking at.
- **Restore** replaces the career state with the slot's state. The restored state's version becomes `current + 1`, so a command sent against the pre-restore view fails as stale instead of applying to the restored state.
- A restore is one log entry (`Restore`). It carries the request's `commandId`, so a retried restore returns the stored answer instead of restoring twice. It also carries `restored_from_sequence`, the log sequence the slot was saved at.
- A restore counts as a successful gameplay action. It sets the last action and the expiry date. Restoring an active state clears the ending date; restoring an ended state sets it to the restore time.
- Saving a slot is not a gameplay action and leaves retention unchanged (M4-A §6).
- The state includes the random generator. Restoring and repeating the same choices reproduces the same draws, while different choices diverge. Saving is allowed at any step, including in the middle of a week.
- **Unchanged by a restore:** published entries (unranked careers have none) and command idempotency. A duplicate of an old command still returns its original stored answer, and the client reloads the career after a restore anyway.

## 4. History after a restore

The log stays append-only. Reports from an abandoned line remain stored, but reads skip them. A read walks the log backwards from the newest entry. When it reaches a `Restore` entry, it jumps to that entry's `restored_from_sequence` and keeps walking from there. Because every slot records where its line of play ended, the walk works for any order of saves and restores. The history list and the latest report on the career view both use this walk. In SQL Server the latest report takes one query plus one more for each restore crossed.

## 5. Export and import

The export file is UTF-8 JSON named `kariera-<nick>-tydzien-<n>.json`:

```json
{ "format": "polski-streamer-simulator-save", "schema": 1, "exportedAt": "…", "nickname": "…", "state": { …canonical CareerState… } }
```

- The file has no signature. It holds the player's own unranked data, and the server validates it like any other input.
- History is not exported: a 20-year career has about 1,040 week reports. The imported career starts with an empty history and no last report.
- **Import** (`POST /v1/careers/import`, body `{ save }`) runs these checks in order, and the first failure answers `422` with its code:
  1. `format` is not the expected value, or the JSON shape is wrong: `import-invalid`.
  2. `schema` is above 1: `import-newer`.
  3. `schema` is below 1: `import-invalid`.
  4. The nickname fails the career nickname rule: `import-invalid`.
  5. The state does not deserialize: `import-invalid`.
  6. The state's ruleset is not registered, or its content digest is not stored on this server or belongs to another ruleset: `import-unsupported`.
  7. The state violates `CareerInvariants`, or the career view cannot be built from it with its snapshot: `import-invalid`.
- An import that passes every check creates a new career: a new ID, secret and recovery key, no season (unranked forever), and the imported state with its version kept. It answers like career creation (`201`, `CreatedCareer`).
- Validation finishes before anything is stored, so a rejected import cannot change any existing career or slot.
- Exporting is a read and leaves retention unchanged. An export of an ended career imports as an ended unranked career.
- A body over 64 KiB answers `413 payload-too-large`. A file from an M3 career played to week 1,040 is about 2.2 KB, and a test keeps it under a quarter of the limit.

## 6. API

| Method and path | Purpose | Notes |
| --- | --- | --- |
| `GET /v1/careers/{id}/saves` | List the three slots | Each slot is either `{ slot, empty: true }` or `{ slot, savedAt, week, lastSettledWeek, status, cashPln, followers }`. Ranked careers get `409 career-ranked`. |
| `PUT /v1/careers/{id}/saves/{slot}` | Save to a slot | Body `{ expectedVersion }`; returns the slot list. Errors: `404 slot-not-found` (slot outside 1–3), `409 stale-version`, `409 career-ranked`. |
| `POST /v1/careers/{id}/saves/{slot}/restore` | Restore a slot | Body `{ commandId, expectedVersion }`; returns an action outcome (career view, no roll or report), like a command. Errors: `409 slot-empty`, `409 stale-version`, `409 career-ranked`, `422 command-id-reused`. |
| `GET /v1/careers/{id}/export` | Download the save file | Returns the file as JSON; the client names and saves it, since the request needs the bearer header. Ranked careers get `409 career-ranked`. |
| `POST /v1/careers/import` | New career from a file | Unauthenticated, like creation, and rate-limited with it. Body limit 64 KiB. |

These five endpoints replace the planned `POST /v1/careers/{id}/saves` row in the architecture table.

## 7. Screens (PR 2)

- **`/kariera/{id}/zapisy`** (link „Zapisy” in the top bar of an unranked career):
  - Three slot cards, each showing „Pusty” or the week, cash, followers and save time.
  - „Zapisz tutaj” asks for confirmation before overwriting a filled slot.
  - „Wczytaj” always asks for confirmation and warns that progress not stored in a slot will be lost.
  - „Eksportuj zapis” downloads the file.
  - A ranked career's screen only explains that ranked careers have no manual saves.
- **`/importuj`** (link „Importuj zapis” on the home screen):
  - A file picker. The file is read locally and sent as `save`.
  - Each error code gets its own Polish explanation.
  - On success the new career is remembered with its secret, the recovery key is shown with the existing key box, and a „Przejdź do kariery” link leads to the career.

## 8. Rulings

1. Three numbered slots per unranked career, each overwritten explicitly and cascaded with the career.
2. A restore bumps the version, is logged with its command ID and counts as gameplay for retention; saving and exporting do not.
3. History follows `restored_from_sequence` backwards; abandoned reports stay stored but unread.
4. Export schema 1 is unsigned JSON without history. Import always creates a new unranked career after full validation.
5. Ranked careers answer every save endpoint with `409 career-ranked`.

## 9. Testing

- **Unit:**
  - Save and restore paths: ranked, stale, empty slot, out-of-range slot, and a repeated restore command.
  - The history walk across nested restores (tree of branches).
  - Each step of the import validation, plus a schema-1 fixture file that imports.
- **Store contract (in-memory and SQL Server):**
  - The three slots are independent.
  - Slots cascade with the deleted career.
  - A restore entry is stored, and the latest report follows it.
- **API:**
  - Export then import round-trips the state hash.
  - Ranked careers get `409` on all five endpoints.
  - An oversized body gets `413`.
  - A rejected import leaves the career count unchanged.
- **Migration:** existing careers have no slots, and existing log entries have no restore base.
- **Frontend:**
  - Slot cards and both confirmations.
  - The restore reloads the career.
  - Import errors and the success screen with the key.
  - A Playwright journey: save, play a week, restore and see the earlier week; export, import, open the imported career.
