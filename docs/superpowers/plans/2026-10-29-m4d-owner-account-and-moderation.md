# M4-D Owner Account and Moderation — Implementation Plan

Spec: [M4-D design](../specs/2026-10-29-m4d-owner-account-and-moderation-design.md) (TD-124, #28, #30). Two PRs into `develop`: PR 1 API (`feature/m4d-1-owner`), PR 2 `/admin` screens (`feature/m4d-2-admin`, stacked).

## PR 1 — API

0. **Docs:** the spec, this plan, TD-124, the GDD log row, the architecture notes and REQ-19 in the traceability matrix.
1. **Packages:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore` and `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (10.0.13).
2. **Infrastructure:**
   - `GameDbContext : IdentityUserContext<IdentityUser>, IDataProtectionKeyContext`, with its tables in schema `owner`.
   - `OwnerUserStore`, which stores recovery codes as hashes.
   - `AuditRecordRow` and `LeaderboardEntryRow.HiddenReason`.
   - The `M4dOwner` migration.
3. **Application:**
   - `IModerationStore`: list a season including hidden entries, set an entry hidden or visible together with its audit record, list audit records.
   - Handlers for hide, restore, the season listing and the audit read.
   - `ExtendSeason` writes an audit record through the season store.
   - The in-memory stores and the contract tests.
4. **WebApp:**
   - Identity core with the sign-in manager, lockout, cookies that answer 401 instead of redirecting, anti-forgery and the owner policy.
   - `AdminEndpoints` with the login steps, session, logout, moderation, extension and audit.
   - The `owner-create` command with `--reset`.
   - A sign-in rate-limit policy, the Problem Details codes and OpenAPI with cookie security; then regenerate the OpenAPI document.
5. **Tests:**
   - API: 401 everywhere, TOTP sign-in, single-use recovery codes, lockout, anti-forgery and a moderation round trip.
   - Command: create once, then reset.
   - A migration test.
6. Regenerate the frontend client, open the PR and get Sonar to 0.

## PR 2 — `/admin` screens

1. A lazy route `/admin`: the sign-in steps, then the panel with ranking moderation, the season extension form, the audit list and logout. The admin client sends cookies and the `X-CSRF-TOKEN` header.
2. Polish copy and Vitest tests.
3. A Playwright journey: create the owner inside the API container, sign in with a computed TOTP code, hide the published E2E entry and check that `/ranking` no longer shows it.
