# M4-A Career Access and Retention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** a recovery key per career, recovery with secret rotation, repeat-safe creation and the six-/12-month retention sweep, without changing any ruleset, golden hash or stored request.

**Spec:** `docs/superpowers/specs/2026-10-29-m4a-career-access-and-retention-design.md`

## Global Constraints

- `Domain` is untouched; `M1GoldenTests` and `M3GoldenTests` stay green.
- The server stores only SHA-256 hashes of the secret, the recovery key and the creation ID; `ToString()` of every result that carries one redacts it.
- Reads, recovery, key rotation and duplicate commands never change `last_action_at`, `ended_at` or `expires_at`.
- TDD: each behaviour starts with a failing test. Coverage ≥ 80 %. SonarQube Cloud zero open issues on every PR.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A recovery key of career A never opens career B, also when B's ID is pasted into A's key.
2. A repeat of a creation after 24 hours or after the first action never rotates credentials.
3. The sweep never deletes a career whose `expires_at` is in the future, and a rolled-back duplicate command never moves it.
4. Rate-limit rejections are Problem Details with `code`.

## Delivery

| PR | Branch | Tasks |
| --- | --- | --- |
| 1 Access and retention API | `feature/m4a-1-access` | 0–7 |
| 2 Recovery screens | `feature/m4a-2-screens` | 8–11 |

# PR 1

### Task 0: Record the design
- [ ] Spec, plan, GDD decision row, TD-121 (decomposition and M4-A), TD-105/TD-106 superseded notes, architecture §2 and API table, traceability REQ-13/14/15, issue comments on #25 and #64. Commit `docs: record the M4 decomposition and the M4-A access and retention design`.

### Task 1: Store port and contract tests
- `NewCareer` gains `RecoveryHash`, `CreationHash?`, `CreationRequestHash?`, `ExpiresAt`; `StoredCareer` gains `EndedAt?`, `ExpiresAt`.
- `ICareerStore`: `CreateAsync` returns `CreateOutcome { Created, DuplicateCreation }`; `FindCredentialsAsync(careerId)` → `CareerCredentials(SecretHash, RecoveryHash?, CreationRequestHash?)` next to `FindSecretHashAsync`, which every bearer request keeps using; `FindByCreationAsync(creationHash)` → career ID; `ReplaceCredentialsAsync(careerId, secretHash?, recoveryHash?)`; `DeleteExpiredAsync(now, limit)` → deleted count.
- `AppendAsync`: an ended state sets `ended_at = at`, `expires_at = at + 12 months`; otherwise `expires_at = at + 6 months` (`CareerRetention`).
- `CareerStoreContract` cases for each member, run on `InMemoryCareerStore` and `EfCareerStore`.

### Task 2: Schema and migration
- `CareerRow.EndedAt`, `ExpiresAt` (index `ix_careers_expires_at`); `CareerSecretRow.RecoveryHash`, `CreationHash` (unique filtered index), `CreationRequestHash`, `RotatedAt`.
- Migration `M4aCareerAccess` backfills `expires_at` (`DATEADD(month, 6, last_action_at)`; finished careers use the last action as their ending). `MigrationTests` cover the backfill.

### Task 3: Recovery keys
- `Application/Careers/RecoveryKeys.cs`: `Format(careerId, random)`, `TryParse(key, out careerId)`; tests for round trip, wrong prefix, bad hex, wrong length, whitespace trimmed.

### Task 4: Handlers
- `CreateCareer` gains `CreationId?`; `CreatedCareer` gains `RecoveryKey`; repeat rules of spec §4 (`creation-id-reused`, `creation-closed`, concurrent repeat).
- `RecoverCareer(RecoveryKey)` → `RecoveredCareer(CareerId, Secret, Career)`; `recovery-failed` for every failure.
- `IssueRecoveryKey(CareerId, Secret)` → `IssuedRecoveryKey(CareerId, RecoveryKey)`.
- `CareerView.ExpiresAt?` from the stored career.
- Tests: isolation of two careers, rotation kills the old secret, the key survives recovery, state and `last_action_at` unchanged, redaction.

### Task 5: Endpoints and rate limits
- `CreateCareerRequest.CreationId?`; `POST /v1/careers/recover`; `POST /v1/careers/{id}/recovery-key`; problem codes (`recovery-failed` 404, `creation-id-reused` 422, `creation-closed` 409, `rate-limited` 429).
- `RateLimitOptions` and the two policies (spec §7); API tests with a lowered limit.

### Task 6: Retention sweep
- `RetentionOptions` (`Enabled`, `Interval`, `BatchSize`); `RetentionSweep` (Application) deletes in batches until none remain; hosted `RetentionSweeper`; JasperFx command `retention-sweep`.
- Tests: expired unfinished and finished careers deleted with their secrets and actions; future ones kept; a read, a recovery and a duplicate command do not extend; SQL test of the cascade.

### Task 7: Contract and close-out; open PR 1
- Regenerate OpenAPI and the Orval client; architecture API table; infrastructure restore step names `retention-sweep`; open PR 1.

# PR 2

### Task 8: Pending creation and retries
- `pendingCreation` storage module; creation sends `creationId` and retries a lost connection; the home screen finishes a pending creation; storing failure shows the key.

### Task 9: Key screen and career menu
- Route `/kariera/$careerId/klucz`; copy and download; the career screen link; issuing a key for older careers; `KnownCareer.recoveryKey`.

### Task 10: Recovery route and expiry line
- Route `/odzyskaj` (paste and file); error copy; expiry line on the career and ending screens; rule on the creation screen and home notice.

### Task 11: Journey and close-out; open PR 2
- Playwright: create, save key, clear storage, recover, continue; docs (traceability, spec status); issue comments; PR 2.
