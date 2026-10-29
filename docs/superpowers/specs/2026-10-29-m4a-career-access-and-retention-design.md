# M4-A Career Access and Retention — Design

Date: 2026-10-29
Status: **Agent proposal; API in PR 1 ([#94](https://github.com/adamcharusta/PolskiStreamerSimulator/pull/94)), screens in PR 2 (2026-10-29).** The owner asked the agent to deliver every M4 sub-project in order without a design interview ("tak zrób je po kolei wszystkie", 2026-10-29, answering whether to skip the questions). Every rule, field and wording below is an agent proposal for owner review.
Milestone: [M4 — Online play and administration](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4), sub-project A, delivering [#25](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/25) and [#64](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/64) (TD-121).

## 0. M4 decomposition

M4 is split into six sub-projects, each with its own design, plan and PRs, delivered in this order because each depends on the previous ones:

| Sub-project | Issues | Content |
| --- | --- | --- |
| **A** Career access and retention | #25, #64 | Recovery key per career, recovery and credential rotation, repeat-safe creation, six-/12-month retention sweep. |
| **B** Ranked careers and leaderboard | #27 | Ranked career start, seasons and submission windows, nickname publication, top 100. |
| **C** Unranked saves | #26 | Three manual slots, export/import, ranked rewind prohibition. |
| **D** Owner account and moderation | #28, #30 | Identity owner with TOTP and recovery codes, `/admin`, hide/restore entries, audit trail. |
| **E** Content administration | #29 | Event and balance drafts, validation, candidate simulation, atomic publication, season rollover. |
| **F** Consent and notices | #31 | Analytics consent, retention and outage notices. |

## 1. Intent

A player never loses a career just because a browser forgot it, and the server never keeps a player's data longer than promised. Each career gets its own recovery key that restores that career, and only that career, on any device. Creating a career is safe to repeat, so a lost response no longer strands a career without access. Careers expire on the documented schedule, and the player sees the date.

Success means (#25, #64, REQ-13, REQ-14, REQ-15):

- Recovering one career with its key never opens another career.
- Recovery replaces the browser secret; the old secret stops working, and the career state does not change.
- Repeating a creation request with the same creation ID returns the same career with working access material, and the server stores one career.
- An unfinished career is deleted six calendar months after its last successful gameplay action, and a finished career 12 calendar months after its ending, together with its secrets and action log. Reads, recoveries and duplicate retries do not move either date.
- The career screen shows the expiry date; creation explains the rule before play.

## 2. Scope

**In scope**

- `Application`/`Infrastructure`: recovery key hash and creation ID hash in `career_secrets`, `ended_at` and `expires_at` on `careers`, the retention sweep, a `retention-sweep` command, an EF Core migration.
- `WebApp`: `creationId` on `POST /v1/careers`; `POST /v1/careers/recover`; `POST /v1/careers/{id}/recovery-key`; `expiresAt` on the career view; ASP.NET Core rate limiting on the three access endpoints; OpenAPI regenerated.
- Frontend: pending creation kept in storage and retried, the recovery-key screen after creation, a recovery route, the key in the career menu, the expiry line, Polish copy.

**Out of scope**

- Leaderboard records that survive deletion: they do not exist yet; sub-project B stores them without a foreign key to `careers` and tests that a sweep leaves them (issue #25's third criterion is verified there).
- Privacy notice and outage notice: sub-project F.
- Forwarded-header trust for rate-limit partitions behind Traefik: M6 deployment ([#40](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/40), Traefik in front of the API); locally and in CI the remote address is used.

## 3. Recovery key

- 32 random bytes, encoded as base64url, prefixed with the career ID: `pss1.<careerId as 32 lowercase hex>.<43 base64url characters>`. The prefix lets the server find the career without a lookup by key and lets the player tell keys apart. The server stores only the SHA-256 of the whole key string in `career_secrets.recovery_hash` (256 bits of randomness, so no slow hash, as for the secret, TD-92).
- `POST /v1/careers` returns `recoveryKey` next to `secret`, once. Both are redacted from `ToString()`.
- `POST /v1/careers/{id}/recovery-key` (bearer secret) issues a new key and replaces the hash; the old key stops working. It serves careers created before M4-A (no key yet) and a player who wants to rotate a key they think leaked. It changes neither the state version nor `last_action_at`.
- `POST /v1/careers/recover` with `{ recoveryKey }`: parses the career ID, checks the hash in constant time, issues a **new secret** and returns `RecoveredCareer(careerId, secret, career)`. The old secret stops working (credential rotation); the recovery key stays valid, so the player's saved copy keeps working. A malformed key, an unknown career, a deleted career and a wrong key all return `404 recovery-failed`, so the answer reveals nothing. Recovery changes neither the state version nor `last_action_at`.
- One active secret per career: recovering on a phone signs the old browser out of that career; the old browser can recover again with the key. Agent call; multiple secrets per career would need a device list and revocation, which no requirement asks for.

## 4. Repeat-safe creation (#64)

- `CreateCareerRequest` gains `creationId` (UUID, required for new clients; optional so older clients still work, and then no repeat safety applies).
- The server stores the SHA-256 of the creation ID and of the canonical request (`nickname`, `background`, `difficulty`, `pronouns`) in `career_secrets` (`creation_hash`, unique, and `creation_request_hash`). The raw creation ID is a short-lived credential, so it is never stored.
- A repeat with the same creation ID and the same request, **within 24 hours of creation and while the career has no logged action**, returns the same career with a **new** secret and a **new** recovery key (the earlier ones are replaced). The server cannot return the original secret because it never stores it; replacing it is equivalent for the client, which never received the first one. One career is stored.
- A repeat with the same creation ID and a different request returns `422 creation-id-reused`. A repeat after 24 hours or after the first action returns `409 creation-closed`; the client then offers recovery with the key.
- Concurrent repeats: the unique index on `creation_hash` lets one insert win; the loser reads the winner and answers as a repeat.
- Retention (#64 asks how long the creation command is kept): the hash stays with the career and is deleted with it; it is honoured only for 24 hours, so no separate cleanup is needed.

## 5. Browser flow

- Before sending, the client generates the creation ID (`crypto.randomUUID()`) and writes a **pending creation** (`creationId`, request, time) to `localStorage` under `pss.pendingCreation.v1`. The existing storage check (TD-105) is this write.
- The mutation now retries a lost connection with the same body (the `shouldRetry` policy); TD-106's ban on automatic retries is superseded.
- When the tab closed before the answer, the home screen finds the pending creation (younger than 24 hours) and finishes it with one request, then shows the key screen. An older pending creation is dropped.
- On success the client stores the career (`careerId`, `nickname`, `secret`, `recoveryKey`, `createdAt`) and clears the pending creation. When that write fails, the screen shows the recovery key at once with the message that the browser could not save access and the key is the only way back; it never invites a second creation.
- After creation the career route opens the **key screen** (`/kariera/$careerId/klucz`): the key in a monospace box, "Kopiuj" and "Pobierz plik" (a `.txt` with the key and one line of instructions), the warning that a lost key and lost browser data cannot be restored, and "Zapisałem klucz — graj". The key is also available later from the career screen ("Klucz odzyskiwania"), where careers without one can create one.
- Recovery route `/odzyskaj`, linked from the home screen and from a missing-career message: a text field (paste) and a file input (reads the first `pss1.` token of the file); on success the career is stored and opened; `recovery-failed` reads "Nie znaleziono kariery dla tego klucza. Mogła wygasnąć albo klucz jest niepełny."; `429` reads "Za dużo prób. Spróbuj za kilka minut."
- `KnownCareer` gains an optional `recoveryKey`; stored entries without it stay valid.

## 6. Retention (#25)

- `careers.ended_at` (nullable) is set when a write moves the status to ended; `careers.expires_at` is recomputed on every successful gameplay write: `last_action_at + 6 months` while unfinished, `ended_at + 12 months` once ended (calendar months, `DateTimeOffset.AddMonths` in UTC). Creation sets `created_at + 6 months`. Rows created before the migration are backfilled the same way.
- Only `AppendAsync` (a committed gameplay action) and creation write these columns. Reads, history, recovery, key rotation and a rolled-back duplicate command do not.
- The sweep deletes careers with `expires_at <= now` in batches of 200; the cascade removes `career_secrets` and `career_actions`. Content snapshots stay (immutable, shared, needed for audit).
- A hosted `RetentionSweeper` runs it at startup and then every hour (`Retention:Interval`, `Retention:Enabled`). The JasperFx command `retention-sweep` runs one sweep and exits, for the restore procedure (rerun before reopening the API) in infrastructure §backups.
- The career view gains `expiresAt`. The career screen shows "Kariera wygaśnie {data}, jeśli do tego czasu nie rozegrasz tygodnia." while unfinished and "Historia i dyplom są dostępne do {data}." once ended. The creation screen and the home notice state the rule: six months without a played week, 12 months after the ending.

## 7. Rate limiting

ASP.NET Core rate limiter, fixed window per remote IP address, rejecting with `429` Problem Details (`code: rate-limited`):

| Policy | Endpoints | Limit (agent default) |
| --- | --- | --- |
| `career-create` | `POST /v1/careers` | 20 per 10 minutes |
| `career-recover` | `POST /v1/careers/recover`, `POST /v1/careers/{id}/recovery-key` | 10 per 10 minutes |

The limits are options (`RateLimits:*`) so tests and staging can change them.

## 8. Rulings

1. A repeat returns new credentials instead of the original ones, because storing raw secrets would break TD-92. Cost: a client that somehow received both responses keeps only the second; the first secret is dead.
2. One active secret per career (§3). Cost: playing one career on two devices needs a recovery on each switch.
3. `expires_at` is stored, not computed in the query, so the sweep is an index range scan and the view shows the same date the sweep uses. Cost: one column to keep in step; every write path goes through `AppendAsync` or creation.
4. The key lives in browser storage next to the secret, so the career menu can show it again. Cost: a stolen browser profile yields both; the key's purpose is device loss, not a second factor.

## 9. Delivery

Two PRs: **1 Access and retention API** (`feature/m4a-1-access`: schema and migration, store, handlers, endpoints, rate limits, sweep, command, OpenAPI, docs) and **2 Recovery screens** (`feature/m4a-2-screens`: pending creation, key screen, recovery route, expiry line, copy, Playwright). Issues #25 and #64 close with the M4 release into `main`.
