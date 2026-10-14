# M2-B Backend Slice — Design

Date: 2026-10-14
Status: **Approved design (owner, 2026-10-14 brainstorming session).** §3–§6 were approved in conversation; §7 and §8 were approved with the written spec.
Milestone: [M2 — Vertical slice](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/2), sub-project B ([TD-85](../../technical-decisions.md)), delivering [#9](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/9), [#10](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/10) and the server side of [#13](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/13).

## 1. Intent

Sub-project B builds the backend that sub-project C turns into a playable browser slice: an anonymous career, one manual week, a 13-week batch, and server autosave that survives an API restart. It wraps the M1 `Domain` in application, persistence and HTTP layers without changing the M1 game rules.

Success means:

- a career created over HTTP can play a full manual week and a 13-week batch, and reloads unchanged after the API restarts;
- a repeated or concurrent request never settles a step twice or draws a different random result;
- one career's secret never opens another career;
- C can generate its client from a committed OpenAPI document.

### Owner decisions taken in this session

| Topic | Decision |
| --- | --- |
| Scope | Rules and content are the M1 ruleset `m1-prototype.1` and `content/m1`. Out of B: `/admin` and Identity, seasons and leaderboard, manual save slots and import, recovery keys, 4- and 52-week batches. |
| Mandatory-decision pause (#13) | Build only the pause mechanism. `IRuleset` gains a query "does the next week need a player decision before it starts?"; the M1 ruleset always answers no. A test ruleset proves the pause. Real mandatory events arrive with M3 content. |
| Batch policy (#13) | One fixed policy, `Balanced`. The player does not configure a strategy in M2. Every plan and event choice records its reason. |
| Ranking | Every M2 career is unranked. The API accepts no ranked choice; the `ranked` column is always `false`. |
| Persistence | Approach A: a state snapshot per career plus an append-only command log (§4). |
| Character nickname | The rule in §5 stands for M2. A nickname generator inspired by real streamers is a later owner idea, outside B. |

### Agent proposals accepted with the design

- Merge the proposed `GET /v1/careers/{id}/choices` into `GET /v1/careers/{id}` (§5).
- Send the career secret as `Authorization: Bearer …` and store only its SHA-256 hash.
- Answer an unknown career and a wrong secret with the same `404 career-not-found`.
- No idempotency key for career creation.
- Commit the OpenAPI document to the repository and check it in CI.
- Defer Serilog, rate limiting and the six-month retention sweep; store `last_action_at` now.

## 2. Scope

**In scope**

- `Application`: commands, queries and Wolverine handlers for creating, reading and advancing a career and for the 13-week batch; ports for persistence, secret hashing and time.
- `Infrastructure`: EF Core SQL Server context, migrations, the career store, the content snapshot store and secret hashing.
- `WebApp`: `WolverineFx.Http` endpoints, FluentValidation, Mapster mapping, Problem Details, the OpenAPI document.
- `Domain`: reasons in the built-in policies and the pause query on `IRuleset` (§7). No change to M1 rules or results.
- Tests: unit and HTTP tests without a database, and SQL Server integration tests with Testcontainers.
- CI: a required `backend-integration` job and the OpenAPI drift check (§8).
- Decision records, documentation and issue updates (§9).

**Out of scope**

- Everything in the owner's scope decision above.
- The React client, Docker Compose and the local smoke test (C).
- Serilog, rate limiting, the retention sweep (later milestones; `last_action_at` is stored).
- Early retirement over HTTP (`Retire` exists in `Domain` but no M2 screen uses it).
- Deployment and production migrations (M6, [#42](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/42)).

**Later ideas recorded here**

- A nickname generator based on real streamers (owner idea). Before it ships, check that a generated nickname does not impersonate a specific real person; this belongs to the legal review the architecture already requires for ranking names.

## 3. Layers and packages

| Project | Adds |
| --- | --- |
| `Application` | `CreateCareer`, `GetCareer`, `SubmitAction` and `RunBatch` messages with Wolverine handlers. Ports: `ICareerStore` (read a career, find a stored command, append a command and the new state atomically when the version matches), `IContentStore` (snapshot by digest), `ISecretHasher`. Uses `TimeProvider` for timestamps. No EF Core or ASP.NET Core dependency. |
| `Infrastructure` | `GameDbContext` (schema `game`), migrations, `EfCareerStore`, `EfContentStore`, `Sha256SecretHasher`. At startup it stores the `content/m1` snapshot under its digest if absent. |
| `WebApp` | Thin `WolverineFx.Http` endpoints that call the handlers, FluentValidation request validators, Mapster DTO mapping, Problem Details mapping, the OpenAPI document. Applies migrations at startup only in Development and tests. |
| `Domain` | Policy reasons and `IRuleset.RequiresPlayerDecision` (§7). |
| `Tests` | Handler, validator, mapping and HTTP tests on an in-memory store; integration tests with `Category=Integration`. |

New packages, versions pinned in the plan: `WolverineFx`, `WolverineFx.Http`, `WolverineFx.FluentValidation`, `WolverineFx.Http.FluentValidation`, `Microsoft.EntityFrameworkCore.SqlServer` and design-time tools, `Mapster`, `Microsoft.AspNetCore.OpenApi`, `Testcontainers.MsSql`. [TD-80](../../technical-decisions.md) assigned these libraries to M2. The handlers keep unit tests on an in-memory `ICareerStore`, so the pre-commit hook still needs no Docker.

## 4. Data model

All game tables live in the SQL Server schema `game`; Identity later gets its own schema.

| Table | Columns and keys |
| --- | --- |
| `careers` | `id` (GUID v7, server-generated, primary key), `nickname`, `ruleset_id`, `content_digest` (foreign key to `content_snapshots`), `ranked` (always `false` in M2), `status`, `state_version` (concurrency check), `state_json` (canonical JSON from `Domain`), `state_hash` (SHA-256), `created_at`, `last_action_at`. |
| `career_secrets` | `career_id` (primary key, foreign key), `secret_hash`, `created_at`. Separate table so a later recovery flow can rotate access without a migration. The raw secret is never stored. |
| `career_actions` | Primary key `(career_id, sequence)`; `command_id`, `step` with a unique index on `(career_id, command_id, step)`; `kind` (`startWeek`, `resolveEvent`, `settleWeek`, `autoWeek`); `request_json`; `response_json` (returned on a repeat); `state_version_after`; `state_hash_after`; `created_at`. A manual command has `step` 0; a batch writes one row per committed week with `step` 1–13. Append-only. |
| `content_snapshots` | `digest` (primary key), `balance_json` and `events_json` stored byte-for-byte, `created_at`. The digest is the SHA-256 of the two files, exactly as `JsonContentSource` computes it; loading recomputes and checks it. |

Database guarantees:

- The unique command index means two identical concurrent requests cannot both be stored; the loser returns the winner's stored response.
- The `state_version` condition on the `careers` update means two tabs at the same version cannot both advance the career; the loser gets a conflict.
- The log row and the new state are written in one transaction (the #9 atomicity criterion).

## 5. API and access

| Endpoint | Behaviour |
| --- | --- |
| `POST /v1/careers` | Body `nickname`, `background`, `difficulty`, `pronouns`. Returns `201` with `careerId`, the secret (shown only in this response) and the career view. |
| `GET /v1/careers/{id}` | Career view: version, status and ending, week, statistics, the last week report, and the **next step**. |
| `POST /v1/careers/{id}/actions` | Body `commandId`, `expectedVersion` and one action: `startWeek` (`mainAction`, `category`), `resolveEvent` (`choiceId`) or `settleWeek`. Returns the new view plus the roll and outcome or the week report. |
| `POST /v1/careers/{id}/batches` | Body `commandId`, `expectedVersion`, `weeks` (only `13` is valid in M2). Returns the batch result (§7). |

The **next step** is one of: the week plans with the reason each disabled plan is unavailable; the current live event with its choices and odds; "settle the week"; "career ended". Event titles, texts and choice labels come from the career's pinned content snapshot (`titleKey`, `textPl`, `labelKey`, `labelPl`). This replaces the separate `GET …/choices` of the proposed API, so options and state always share one version.

**Access.** The secret is 32 random bytes from a cryptographic generator, base64url-encoded, sent as `Authorization: Bearer …`. A cookie is not used: the browser holds a separate secret per career, and a header needs no CSRF protection. The server stores only its SHA-256 hash and compares hashes in constant time; a slow password hash is unnecessary for 256 random bits. A missing header returns `401 missing-secret`. A wrong secret and an unknown career both return `404 career-not-found`, so a caller cannot probe which careers exist.

**Errors.** RFC 9457 Problem Details with a stable `code` extension:

| Status | Codes |
| --- | --- |
| `400` | `validation-failed` (with field errors) |
| `401` | `missing-secret` |
| `404` | `career-not-found` |
| `409` | `stale-version`, `career-ended` |
| `422` | `illegal-action`, `unknown-choice`, `events-unresolved`, `no-pending-week`, `week-in-progress`, `command-id-reused` |
| `500` | `internal-error` (no internal details) |

`Domain` errors map one-to-one: `IllegalAction` → `illegal-action` (with the domain's stable detail), `StaleVersion` → `stale-version`, `WeekAlreadySettled` → `stale-version`, `NoPendingWeek` → `no-pending-week`, `EventsUnresolved` → `events-unresolved`, `CareerEnded` → `career-ended`, `UnknownChoice` → `unknown-choice`; `UnknownRuleset` and `ContentMismatch` are server faults (`500`).

**Contract.** Enum values are JSON strings in camelCase (`dormRoomNewcomer`, `trainCategory`). `Fixed` values are exposed as JSON numbers through an exact `decimal` conversion (at most six decimal places). The OpenAPI document is generated with Wolverine's `openapi` command (it never starts the host or opens the database) into `source/backend/WebApp/openapi/v1.json`, committed, and checked for drift in CI; C's Orval reads this file.

**Character nickname.** 3–20 characters after trimming; letters including Polish diacritics, digits, space, `_`, `-` and `.`. It is display metadata on `careers`, not part of the domain state, and differs from the 1–5-character leaderboard nickname.

## 6. Command flow, repeats and concurrency

For `POST …/actions`:

1. **Access:** load the career's secret hash and compare it in constant time.
2. **Repeat:** look up `(career, commandId, step 0)` in `career_actions`. If it exists with an identical request, return the stored response without recomputing; with a different request, return `422 command-id-reused`.
3. **Version:** load the state; if `expectedVersion` differs from `state_version`, return `409 stale-version`.
4. **Domain:** take the pinned content (cached by digest) and the ruleset from `RulesetRegistry`, run the step. A domain error is returned and nothing is stored.
5. **Store:** in one transaction, append the `career_actions` row and update `careers` where `state_version` equals the expected version. If the update matches no row or the command index rejects the insert, read the log again: a row with this `commandId` means a concurrent duplicate won, so return its response; otherwise return `409 stale-version`.

A step cannot be re-rolled: the PCG32 generator is part of the career state, so the same state always yields the same roll, even under a new `commandId`. A repeat after a lost response returns exactly the stored result.

**Creation** has no repeat key. A retry after a lost response creates a second career; the abandoned one falls to the later six-month retention sweep. The career seed comes from the server's cryptographic generator.

**Reading** computes the view from the state: legal plans, the current event with odds, or "settle the week". The last week report comes from the latest log row that contains one, so a page refresh shows the same server-confirmed report (#12).

`last_action_at` changes only after a successful gameplay command; reads and repeats do not touch it, matching the six-month rule. An unexpected exception returns `500 internal-error` without internal details.

## 7. Thirteen-week batch (#13 server side)

**Domain changes.**

- `IChoicePolicy` returns each plan and event choice together with a reason: a stable kebab-case code plus named numeric arguments (for example `rest-low-energy` with `energy` and `threshold`, `gamble-best-odds` with `probability`, `take-guaranteed`). C localizes the reason; `Domain` holds no Polish text. All five built-in policies give reasons; their decisions do not change, so the M1 golden results and the batch-simulator report stay identical.
- `IRuleset.RequiresPlayerDecision(state, content)` answers whether the next week needs a player decision before it starts. `M1Ruleset` returns `false`. The test ruleset wraps `M1Ruleset` and returns `true` for a chosen week.

**Request.** `commandId`, `expectedVersion`, `weeks = 13`. The career must be active (`409 career-ended` otherwise) with no week in progress (`422 week-in-progress`), and the version must match (`409 stale-version`).

**Loop.** For each of up to 13 weeks:

1. stop with `careerEnded` if the career has ended;
2. stop with `pausedForDecision` if `RequiresPlayerDecision` is true — before `StartWeek`, so the week is not consumed;
3. otherwise play the week with `Balanced`: legal plans → plan with reason → `StartWeek` → for each event, odds → choice with reason → `ResolveEvent` → `SettleWeek`;
4. commit the week as one `autoWeek` log row (`step` = week index in the batch) with the plan, every choice, the reasons, the rolls and the week report, in one transaction with the state update.

If a week's commit fails its version check because another request changed the career, the batch stops with `interrupted` and reports the weeks it committed. A repeat of the same batch command is not another request: when it commits a week first, the batch continues from that week, so concurrent repeats store each week once and answer alike (TD-97). A batch that runs all 13 weeks stops with `completed`.

**Response.** `weeksCommitted`, `stopReason` (`completed`, `pausedForDecision`, `careerEnded`, `interrupted`), the per-week ledger (week, plan and reason, each event's choice, reason, roll, threshold and outcome, the week report), a cumulative summary (net followers, cash change, income and cost totals, start and end values of the main statistics) and the final career view.

**Repeats and lost connections.** The loop does not stop when the client disconnects; it observes only application shutdown, and every committed week is visible through `GET`. A repeat with the same `commandId`:

- with no stored rows runs the batch (a pause before the first week changes nothing, so the repeat pauses identically);
- with stored rows, when the career is still at the last row's `state_version_after` and the batch had neither finished nor stopped, resumes the remaining weeks (recovery after a crash);
- otherwise rebuilds the response from the stored rows; the stop reason is then recomputed as `completed` when all 13 weeks were committed and `interrupted` otherwise, because a pause or an ending is reproduced only while the career is unchanged (TD-97).

A batch that committed no week stores nothing: its repeat runs as a new batch (`409 stale-version` once the career has moved on), and its command ID stays free for another command (TD-97).

Thirteen weeks take microseconds of domain work and 13 short transactions, well inside one HTTP request; a persisted job is not needed for M2.

## 8. Testing, CI and delivery

**Test-driven development** ([TD-58](../../technical-decisions.md)) applies to every production change.

**Without a database** (unit tests; they run in the pre-commit hook and the `backend` job):

- handlers on an in-memory `ICareerStore`, fake hasher and fixed `TimeProvider`: repeat with an identical and a different request, stale version, every domain-error mapping, the batch stop reasons, pause without consuming the week, resume after a partial batch;
- policy reasons, validators, Mapster mappings, Problem Details mapping, constant-time secret comparison;
- HTTP tests through `WebApplicationFactory` with the in-memory store: access rules, status codes, OpenAPI document generation.

**With SQL Server** (`Category=Integration`, Testcontainers MsSql, one container per test run and a fresh database per test class):

- migrations on an empty database, and no pending model changes;
- restart survival: play through one host, dispose it, start a second host on the same database, read the same state and hash;
- atomicity: a command interceptor fails the log insert that follows the `careers` update; neither the row nor the new state is stored;
- concurrency: parallel requests with the same `commandId` store one row and return identical responses; parallel requests with different `commandId`s at one version return one `200` and one `409`;
- secret isolation: career B's secret on career A returns `404`;
- the full flow: create, one manual week, a 13-week batch, reload.

**CI** ([TD-86](../../technical-decisions.md)):

- `backend` runs tests with `--filter-not-trait "Category=Integration"`, keeps the 80% gate, and checks that the build left `source/backend/WebApp/openapi/v1.json` unchanged.
- New required job `backend-integration` builds and runs `--filter-trait "Category=Integration"` on `ubuntu-latest` (Docker is available there).
- `sonar` keeps running every test, so SonarQube Cloud's new-code coverage includes the integration tests.
- The `main` and `develop` rulesets add `backend-integration` as a required check, in `.github/rulesets/*.json` and live; the live change needs the owner's yes at that time.
- The SQL Server test image tag is pinned. Developers need Docker only for integration tests.

**Delivery.** Three PRs into `develop`, each merged before the next branch starts:

1. `feature/m2b-09-persistence` (closes #9): this spec and the plan, packages, the `Application` ports, the EF Core context, migrations, career and content stores, the integration test fixture, the `backend-integration` job and ruleset change, store-level restart and atomicity tests.
2. `feature/m2b-10-career-api` (closes #10): handlers for create, read and actions, secrets, endpoints, validation, Problem Details, the OpenAPI document and drift check, HTTP-level restart, concurrency and isolation tests.
3. `feature/m2b-13-batch` (refers to #13, which stays open for C's UI): policy reasons, the pause query, the batch handler and endpoint, the batch tests.

Each push, PR, ruleset change and merge needs the owner's explicit yes.

## 9. Decision records and documentation

Recorded during implementation, in English:

- **TD-90** (owner decision): M2-B scope, unranked-only careers, the fixed `Balanced` batch policy with recorded reasons, and the pause mechanism without M1 mandatory events.
- **TD-91** (owner decision): persistence as a state snapshot plus an append-only command log (§4).
- **TD-92** (agent proposal accepted): API surface and access — merged choices endpoint, bearer secret with SHA-256 hash, uniform `404`, Problem Details codes, no creation repeat key, committed OpenAPI document, nickname rule.
- **TD-93** (agent proposal accepted): the `backend-integration` required job and the three-PR delivery; amends TD-86's job list as TD-86 foresaw.

Documentation updates: `technical-architecture.md` (API table and tables), `technology-stack.md` (OpenAPI artifact path), `development-and-delivery.md` (current CI state, Docker for integration tests), `repository-standards.md` (OpenAPI artifact row), `content-and-experience.md` (character nickname rule), the GDD decision log (unranked M2 careers, fixed batch policy), and `requirements-traceability.md` rows for saves, idempotency and automation. Issue #13 gets a "Delivery split" section like #14 (server in B, UI in C).

## 10. Acceptance criteria → evidence

| Issue criterion | Evidence |
| --- | --- |
| #9 Confirmed weeks survive an API restart | Integration test with two hosts on one database (store level in PR 1, HTTP level in PR 2). |
| #9 Migrations work on an empty database | Integration test: migrate an empty database; no pending model changes. |
| #9 A test covers atomic persistence | Integration test with the failing log insert after the `careers` update. |
| #10 Duplicate requests and concurrent tabs cannot settle a week twice | Parallel same-`commandId` and parallel same-version tests (unit and integration). |
| #10 One career's secret cannot open another career | HTTP and integration tests returning `404 career-not-found`. |
| #10 Invalid choices return a stable error | HTTP tests for `unknown-choice` and `illegal-action` Problem Details. |
| #13 All 13 weeks commit without skipped saves | 13 `autoWeek` rows with consecutive sequences, a continuous version chain and `LastSettledWeek` + 13. |
| #13 Pausing does not consume the current week | Test ruleset pausing before week *k*: `k − 1` weeks committed, no pending week, the next step is a week plan. |
| #13 The policy's selection reason is recorded | Every `autoWeek` row holds the plan reason and each event-choice reason. |

## 11. Risks

- **Wolverine on .NET 10 and OpenAPI from Wolverine endpoints.** PR 2 starts with a small endpoint and the document generation before building the rest.
- **SQL Server container cost.** The image download adds about a minute per CI run; one container per run keeps the rest short. Developers on Windows need Docker Desktop with Linux containers.
- **Ruleset default `require_extra_approval_for_unattributed_changes`.** Watch the first B PR for an unexpected approval prompt (M2-A ruling).
- **Secrets in browser storage.** Exposure through XSS is mitigated by the content security policy planned for the client and deployment (C and M6).
- **Batch length.** 13 weeks fit in a request; 52-week batches may need the persisted job foreseen by the architecture.
