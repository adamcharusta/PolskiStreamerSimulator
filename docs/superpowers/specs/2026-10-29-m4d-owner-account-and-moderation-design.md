# M4-D Owner Account and Moderation — Design

Date: 2026-10-29
Status: **Agent proposal; API in PR 1, `/admin` screens in PR 2 (2026-10-29).** The owner asked the agent to deliver every M4 sub-project in order without a design interview ("tak zrób je po kolei wszystkie", 2026-10-29; "zmergowane" after M4-C merged). Every rule, field and wording below is an agent proposal for owner review.
Milestone: [M4 — Online play and administration](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4), sub-project D, delivering [#28](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/28) and [#30](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/30) (TD-124). The M4 decomposition is in the [M4-A design §0](2026-10-29-m4a-career-access-and-retention-design.md).

## 1. Intent

The owner, and nobody else, can sign in to a private `/admin` area with a password and an authenticator app. There the owner can hide an abusive or invalid leaderboard entry, restore it, and move a closed season's deadline. Every change leaves a durable record of who changed what, when and why. Players stay accountless (TD-37). The API decides access; hiding the route in the UI is cosmetic (TD-38).

Success means (#28, #30, REQ-19):

- The only way to create the account is a command run on the server. There is no registration endpoint, and the command refuses to create a second owner.
- Signing in needs the password **and** either a current authenticator code or an unused recovery code. Each recovery code works once.
- Every admin endpoint answers `401` without a full owner sign-in. Every mutating admin endpoint also answers `400 antiforgery-failed` without a valid anti-forgery token.
- Hidden entries disappear from the public table and from public ranks. Restoring an entry brings it back with its original score and publication time.
- Each hide, restore and deadline extension writes an audit record in the same transaction as the change: actor, action, target, reason, time, and the old and new values.

## 2. Scope

**In scope**

- `Infrastructure`:
  - ASP.NET Core Identity user tables in a new `owner` schema of the same database.
  - Data-protection keys stored in SQL Server, so cookies survive restarts and work across replicas.
  - Recovery codes stored as SHA-256 hashes.
  - `game.audit_records` and `leaderboard_entries.hidden_reason`.
- `WebApp`:
  - The `owner-create` command.
  - Cookie authentication, the owner policy, the anti-forgery check, and the admin login, session, moderation, extension and audit endpoints.
  - The OpenAPI document regenerated.
- `Application`: moderation (list, hide, restore) and the audit read. Season extension now writes an audit record too.
- Frontend: a lazy-loaded `/admin` route with sign-in (password, then code), the season table with hide and restore, the extension form and the audit list. All copy is Polish.

**Out of scope**

- Content and balance administration (M4-E, #29). Its audit records will use the same table.
- Changing the password or TOTP key from the browser. The command with `--reset` replaces both, because a server shell is the trust anchor.
- Login audit records. Sign-ins and failures go to the operational log only.

## 3. Owner account

- **Storage.** Identity runs on `IdentityUserContext<IdentityUser>`, with no roles because there is one owner. Its tables are `owner.users`, `owner.user_claims`, `owner.user_logins` and `owner.user_tokens`, all in the game database, and `GameDbContext` migrates them.
- **`owner-create --username <name>`:**
  - Refuses if any owner exists.
  - Otherwise creates the user with a random 24-character password, enables two-factor sign-in and generates the authenticator key and 10 recovery codes.
  - Prints all of these once, with the `otpauth://` URI. Nothing is read from the command line or the environment, so no secret ends up in shell history or deployment config (TD-69).
- **`owner-create --reset`** replaces the existing owner's password, authenticator key and recovery codes, and invalidates current sessions through the security stamp. It is the way back after a lost phone and lost codes.
- **Recovery codes.** The store keeps only their SHA-256 hashes, and redeeming one removes it. The TOTP key has to stay readable to check codes, so it is stored as Identity stores it.
- **Lockout.** Five failed password or code attempts lock the account for 15 minutes. The sign-in endpoints also share a rate limit of 10 requests per 10 minutes per address.

## 4. Sessions and CSRF

- **Cookies.** Identity's application cookie is named `pss.owner`, with `HttpOnly`, `SameSite=Strict` and `Secure` (outside Development). It expires after 8 hours idle and 24 hours at most. Between the password and the code, Identity's own two-factor cookie holds the half-finished sign-in.
- **No redirects.** A missing or partial sign-in answers `401 owner-unauthorized` as Problem Details. Cookie authentication never redirects.
- **Anti-forgery.** ASP.NET Core anti-forgery with the header `X-CSRF-TOKEN`:
  - `GET /v1/admin/session` and both sign-in steps return `csrfToken`, which is bound to the signed-in user.
  - Every `POST` under `/v1/admin` except the two sign-in steps must send it; otherwise the answer is `400 antiforgery-failed`.
  - The sign-in steps rely on `SameSite=Strict`, the rate limit and the lockout instead.

## 5. Moderation and audit

- **Hiding.** An entry is hidden by setting `hidden_at`, plus a new `hidden_reason`. The public table, public ranks and a career's own panel already treat a hidden entry as absent (M4-B §6).
- **Revoking.** An invalid result is revoked by hiding it with a reason. It stays restorable, so an accidental revoke can be undone (validation §5).
- **Restoring** clears both fields. The score, weeks, publication time and receipt never change, so a restored entry returns to its original rank.
- **The audit record** goes to `game.audit_records`: `id`, `at`, `actor` (the owner's user name, or `command` for the CLI), `action` (`leaderboard.hide`, `leaderboard.restore`, `season.extend`), `target` (the entry ID or season number), `reason`, `old_json` and `new_json`.
  - The record is written in the same transaction as the change.
  - Hiding a hidden entry, or restoring a visible one, changes nothing and writes no record.
- **A reason is required:** 1–500 characters after trimming.

## 6. API

All endpoints are under `/v1/admin`, with no cache, and excluded from public rate limits.

| Method and path | Purpose | Notes |
| --- | --- | --- |
| `POST /login` | Password step | Body `{ username, password }`. Returns `{ next: "code" }` and sets the two-factor cookie. Errors: `401 owner-login-failed` (same answer for an unknown user or a wrong password), `423 owner-locked-out`, `429`. |
| `POST /login/code` | Second step | Body `{ code }` or `{ recoveryCode }`. Returns `{ username, csrfToken, recoveryCodesLeft }`. Errors: `401 owner-login-failed`, `423`, `429`. |
| `GET /session` | The signed-in owner | Returns `{ username, csrfToken, recoveryCodesLeft }`, or `401`. |
| `POST /logout` | Sign out | Requires anti-forgery. |
| `GET /leaderboard?season` | Every entry of a season, hidden ones included | The season defaults to the current one. Rows hold the public fields plus the entry and career IDs, ruleset, final state hash, `hiddenAt` and `hiddenReason`, in leaderboard order. `404 season-not-found`. |
| `POST /leaderboard/{entryId}/hide` | Hide or revoke | Body `{ reason }`. Errors: `404 entry-not-found`, `409 entry-hidden`. |
| `POST /leaderboard/{entryId}/restore` | Restore | Body `{ reason }`. Errors: `404 entry-not-found`, `409 entry-visible`. |
| `POST /seasons/{number}/extension` | Move a closed deadline | Body `{ until, reason }`. The same rules as `season-extend`. |
| `GET /audit?limit` | Newest audit records | Default 50, at most 200. |

## 7. Screens (PR 2)

- **`/admin`** is lazy loaded. It is never linked from the game, and the API is the access boundary.
- **Sign-in.** The user name and password come first, then a six-digit code field with a „Użyj kodu zapasowego” toggle. When fewer than three recovery codes are left, the panel warns the owner.
- **The panel** has three sections:
  - **Ranking:** a season picker and every entry in a table, hidden ones marked „Ukryty: <reason>”. Each row has „Ukryj” or „Przywróć”, which opens an inline reason field.
  - **Termin sezonu:** for closed seasons, a date and reason form.
  - **Dziennik zmian:** the newest audit records.
- „Wyloguj” signs the owner out.

## 8. Rulings

1. Identity tables in schema `owner` of the same database. Data-protection keys go in `owner.data_protection_keys`.
2. The owner is created and reset only by `owner-create`, which prints a generated password, TOTP key and 10 hashed single-use recovery codes.
3. Cookie: `SameSite=Strict`, 8 hours idle and 24 hours absolute. Lockout: 5 failures for 15 minutes. Sign-in rate limit: 10 per 10 minutes.
4. Anti-forgery header on every authenticated admin `POST`.
5. A hide (also used to revoke) and a restore always need a reason. Each change writes a transactional audit record, and a change that does nothing writes none.
6. Season extension from the command or the form writes the same audit record.

## 9. Testing

- **Unit:**
  - Moderation paths: hide, restore, repeats, missing entry, reason rule.
  - The hidden entry is absent from the table and from ranks; restore keeps the score and rank.
  - The extension writes an audit record.
- **Store contract (in-memory and SQL Server):** the hide and its audit record go in one transaction; nothing is written when nothing changes; audit ordering.
- **Recovery code store:** the stored codes are hashes, and a code redeems once.
- **API:**
  - No registration endpoint exists.
  - Every admin endpoint answers `401` without a sign-in and with only the password step.
  - A sign-in with TOTP and with a recovery code (the second use of the code fails).
  - Lockout after five failures.
  - A `POST` without or with a wrong anti-forgery token answers `400`.
  - Hide and restore round trip; the public table hides the entry.
- **Command:** creating twice is refused; `--reset` replaces the credentials.
- **Migration:** the owner tables exist, and no owner is seeded.
- **Frontend:** the sign-in steps and errors, hiding with a reason, restoring, the extension form and the audit list. A Playwright journey creates the owner with the command inside the API container, computes TOTP, signs in, hides the entry it published and checks the public table.
