# M4-E Content Administration — Implementation Plan

Spec: [M4-E design](../specs/2026-10-29-m4e-content-administration-design.md) (TD-125, #29). Two PRs into `develop`: PR 1 is the API (`feature/m4e-1-content`); PR 2 is the `/admin` „Treść” screens (`feature/m4e-2-content-screens`), stacked on PR 1.

## PR 1 — API

0. **Docs:** spec, this plan, TD-125, GDD log row, architecture note, REQ-20.
1. **Application/Content:**
   - `ContentMechanics`: strip display strings and versions, then hash.
   - `ContentDiff`: compare two pairs of documents.
   - `ContentDraftDocuments`: replace, add or remove an event and replace the balance, writing deterministic UTF-8.
   - `IContentCompiler` port: parse, validate and fingerprint.
   - `IContentStore`: find files by digest, and list version conflicts.
2. **Seasons:**
   - `Season.MechanicsHash` and `Pins(ruleset, mechanics)`.
   - `EnsureCurrentAsync(ruleset, digest, mechanics, now)`, which backfills the fingerprint of an open season with the same digest.
   - `ICurrentContent.MechanicsHash`, and ranked creation checks the fingerprint.
3. **Publications and draft:**
   - Ports `IContentPublications` and `IContentDrafts`, with EF stores and in-memory stores.
   - Migration `M4eContent`.
   - `PublishedContent` (`ICurrentContent`) that can be switched at runtime.
   - `DatabaseInitializer` publishes the release and loads the newest publication.
   - `PublishedContentRefresher`, a hosted service running every 30 s.
4. **Handlers:**
   - Read the draft with its check.
   - Create and discard the draft; put or remove an event; put the balance.
   - Simulate (`ContentSimulation` with `CareerRunner`, 40 careers).
   - Publish, revert, list publications, export.
   - New error codes.
5. **WebApp:**
   - `AdminContentEndpoints`.
   - Anti-forgery on `PUT` and `DELETE`.
   - OpenAPI summaries; regenerate the document.
6. **Tests:**
   - Unit tests for the fingerprint, diff and handlers.
   - Store contract tests.
   - API tests on SQL Server.
   - A migration test.
7. Regenerate the frontend client, open the PR and get Sonar to 0.

## PR 2 — Screens

1. „Treść” in `/admin`:
   - draft status;
   - event list and JSON editor with a preview in the three pronoun forms;
   - balance constants;
   - check, simulation and publication;
   - history with revert and export.
2. Polish copy and Vitest tests.
3. Playwright: a title fix is published as text-only, keeps the season, and appears in the history.
