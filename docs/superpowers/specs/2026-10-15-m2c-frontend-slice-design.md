# M2-C Frontend Slice — Design

Date: 2026-10-15
Status: **Approved design (owner, 2026-10-15 brainstorming session).** §2–§8 were approved section by section in conversation; the written spec awaits the owner's review.
Milestone: [M2 — Vertical slice](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/2), sub-project C ([TD-85](../../technical-decisions.md)), delivering [#8](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/8), [#11](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/11), [#12](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/12), the client side of [#13](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/13) and the M2-C part of [#14](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/14).
Builds on: [M2-B backend slice](2026-10-14-m2b-backend-slice-design.md) (career API, 13-week batch, committed OpenAPI document).

## 1. Intent

Sub-project C turns the M2-B API into a playable browser slice. A player passes the 18+ gate, creates a career, plays one full manual week, runs a 13-week batch, reloads the page and sees the same server-confirmed state. The whole stack starts locally with one command.

Success means:

- the Playwright journey (gate → new career → manual week → 13-week batch → reload) passes in CI against the real API and SQL Server;
- the frontend has at least 80% line coverage on its own, and SonarQube Cloud stays at zero open issues (TD-96);
- the game screen works from a 320 CSS-pixel viewport, by keyboard and with a screen reader (WCAG 2.2 AA), in every visual theme;
- the generated API client is never edited by hand, and CI fails on contract drift.

### Owner decisions taken in this session

| Topic | Decision |
| --- | --- |
| Content scope | Keep the M1 ruleset and `content/m1` (TD-90). The vertical slice's representative events for each adult theme (gambling, adult creator work, privacy incident) and the tone check with players move to M3, where the full content schema arrives. |
| Local stack | One `docker compose up` starts SQL Server, the API built from a multi-stage `Dockerfile` in Development mode, and Vite with the frontend source mounted and hot reload (§7). |
| Browser tests | One Playwright journey, run locally against Compose and in CI as a new required job `frontend-e2e` (§8). |
| Game screen layout | Layout A: a top bar, a wide stage for the current step and a narrower statistics rail that collapses to a drawer on mobile (§4). |
| Visual style | A deliberately amateur, retro look with sound UI/UX. Three selectable themes — "cheap OBS overlay" (default), "Windows 98 shareware" and "2005 fan site" — all delivered in M2-C (§5). |
| Screen state | Server-driven: one game route; the stage renders what the API's `next.kind` says (§3). |

### Agent proposals accepted with the design

- `GET /v1/career-options` exposes the starting profiles and difficulty multipliers from the current content, so the creation screen shows real numbers without duplicating content in the client (§6).
- Polish the OpenAPI document before generating the client: bearer security scheme, string enums, Problem Details with `code`, documented `500`, readable operation names (§6; checklist on #8).
- Mock the API in component tests with MSW at the HTTP level, so tests run through the generated client and Axios (§8).
- Self-host one OFL heading font per theme; body text uses a readable system font in every theme (§5).
- Deliver C as four PRs (§8).
- Move the test admin login of #14's smoke test to M4, where Identity and `/admin` arrive.

## 2. Scope

**In scope**

- `source/frontend`: React 19.3.0, TypeScript and Vite with TanStack Router, TanStack Query and Axios, an Orval-generated client, Zustand for transient UI state, React Hook Form with Zod, Tailwind CSS with owned shadcn/ui components, and i18next with Polish copy ([technology stack](../../technology-stack.md)).
- Screens: the 18+ gate and neutral exit, home with the careers known to this browser, character creation, the game screen (week plan, live events, settlement, weekly report, 13-week batch, ended career), settings with the theme picker (§4, §5).
- Backend: `GET /v1/career-options`, the OpenAPI polish, and a multi-stage `Dockerfile` for `WebApp` (§6).
- Root `compose.yaml` and `.env.example` (§7).
- Tooling: ESLint (the owner's package set), Prettier, Vitest with Testing Library and MSW, Playwright with axe, Lefthook frontend commands (§8).
- CI: required jobs `frontend` and `frontend-e2e`; the `sonar` job analyses the TypeScript source with LCOV coverage; ruleset updates (§8).
- Decision records, documentation and issue updates (§9).

**Out of scope**

- Leaderboard, manual save slots and import/export, recovery keys, the six-month inactivity expiry (M4).
- `Sensitive content` and Streamer Friendly settings (M3–M5).
- Career history, the full ending screen with score breakdown and PDF diploma, 4- and 52-week batches, a player-configured strategy (M3).
- Events for the adult themes and the tone check with players (M3, owner decision above).
- Identity, `/admin` and the admin login of the smoke test (M4).
- Deployment, production images and the content security policy (M6, [#42](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/42)).
- Early retirement (no M2 screen uses `Retire`).

## 3. Architecture and data flow

**Layout of `source/frontend/src`**

| Folder | Contents |
| --- | --- |
| `app/` | Router, providers (TanStack Query, i18next, theme), query client configuration. |
| `api/generated/` | Orval output. Never edited by hand; excluded from ESLint, Prettier, coverage and SonarQube Cloud. |
| `api/http.ts` | The one Axios instance (Orval mutator): base URL `/v1`, timeout, cancellation, and normalization of Problem Details into `ApiError { status, code, detail, errors }`. |
| `careers/` | Known careers in `localStorage` (`careerId`, nickname, secret, creation time) and the helper that adds `Authorization: Bearer <secret>` to a career's requests. |
| `features/age-gate/`, `features/home/`, `features/create-career/`, `features/career/`, `features/settings/` | Feature components, hooks and screens. `features/career` holds the stage views: plan, event, settlement, report, batch and ended. |
| `components/ui/` | Owned shadcn/ui source, restyled through theme tokens (§5). |
| `locales/pl/` | All Polish UI copy, including reason codes, disabled reasons, report lines and enum labels. |
| `styles/` | Tailwind entry, theme token sets and per-theme decoration. |

**Routes (TanStack Router)**

- `/` — home; `/nowa-kariera` — character creation; `/kariera/$careerId` — the game screen.
- The 18+ gate wraps the router. Until the player confirms, only a neutral background and the gate dialog render; no route, game text or career data loads. Declining shows a neutral exit view.

**Server-driven game screen**

- The career is the TanStack Query entry `getCareer(careerId)`. The stage renders the view for `next.kind` (`planWeek`, `resolveEvent`, `settleWeek`, `ended`); there are no per-step routes.
- A step or batch gets its `commandId` (`crypto.randomUUID()`) when the player confirms it. A retry after a network failure sends the same request with the same ID, so the server returns the stored result instead of rolling again. Retries happen only for network errors and timeouts, at most twice, then the player sees a retry button that reuses the ID.
- `POST /v1/careers` has no idempotency key in M2, so it does not automatically retry after a network failure (TD-106). A lost creation response can still strand its secret until a later recovery or idempotent-creation design is available.
- The response's career view replaces the cached career at once. A reload simply fetches the career again, so the screen always shows server-confirmed state; the query cache is not a save.
- Transient results the next view needs — the roll just revealed, the report of the week just settled, the batch result — live in component state until the player moves on. After a reload, the plan view shows the career's `lastReport` instead.
- Primary actions are disabled while their request is pending, and the mutation ignores a second activation, so a double click sends one request.

**Errors**

| Response | Behaviour |
| --- | --- |
| Network error or timeout | Banner "Wybór nie został zapisany — spróbuj ponownie"; the retry reuses the `commandId`. No unconfirmed result is shown. |
| `409 stale-version` | Another tab changed the career: refetch and say so. |
| `409 career-ended`, `422` codes | Refetch and show the localized reason; the stage follows the fresh `next.kind`. |
| `404 career-not-found` | "Nie znaleziono kariery" with an option to forget it in this browser. |
| `400 validation-failed` | Field errors mapped into the creation form; elsewhere a generic message (a client bug). |
| `500` | Generic message and a reload button. |

**State and forms**

- Zustand holds only UI state shared across components, such as the mobile statistics drawer. It never holds game state.
- Character creation uses React Hook Form with a Zod schema that mirrors the server's nickname rule (3–20 characters after trimming: letters, digits, space, `_`, `-`, `.`); the API stays the authority, and its `400` errors map onto the fields.
- i18next uses only `pl`. Event text and choice labels come from the API (`textPl`, `labelPl`); event titles, reason codes, disabled reasons, report line codes and enum labels use locale keys with a complete neutral fallback. `Intl` formats PLN, percentages and counts for `pl-PL`.

## 4. Screens

1. **18+ gate.** A blocking dialog over a neutral background states the 18+ rating and names gambling, adult creator work, privacy incidents and strong language as themes. Actions: `Mam 18 lat lub więcej — wchodzę` and `Wychodzę`. Escape, clicking outside and navigating back never confirm. The acknowledgment is stored locally; cleared storage shows the gate again. After confirming, a one-time notice explains that play needs a connection and careers are saved on the server.
2. **Home.** Careers known to this browser (nickname, start date, `Kontynuuj`) and `Nowa kariera`. A notice says that access to a career lives only in this browser and is lost if site data is cleared; recovery keys follow in M4. A `Ustawienia` button opens settings.
3. **Character creation.** Nickname; four background cards with starting cash, followers, equipment, strongest category and score multiplier from `GET /v1/career-options`; difficulty with its multiplier; pronouns (`ona`, `on`, neutral form); a summary before `Zaczynam karierę`. On success the career joins the known list and the game screen opens. A failed options request shows an error and retry action (TD-104). If browser storage cannot save the access secret, show an error and do not send the creation request (TD-105).
4. **Game screen** (layout A).
   - **Top bar:** week, cash, followers, last-stream viewers, save status (`Zapisano`, `Zapisuję…`, `Brak połączenia`), settings.
   - **Statistics rail:** energy, burnout, drama and credibility as labelled bars with numbers; skill and boredom per category; equipment tiers. On mobile it collapses to a `Statystyki` drawer.
   - **Stage by `next.kind`:**
     - *Week plan:* main action (five buttons), category (five), and the equipment slot for an upgrade. Disabled options show their localized reason (for example `Nie stać cię`, `Musisz odpocząć`). Primary `Zaczynam stream`; secondary `Auto: 13 tygodni`. The last report sits collapsed below.
     - *Live event:* event text and choices with their chance (`58%` or `pewne`), the modifier breakdown and whether a critical result is possible. No timer. After a choice, the roll against the threshold and the outcome appear in words and with an icon and are announced to screen readers; `Dalej` moves on.
     - *Settlement:* `Kończę tydzień` settles the week and opens the report.
     - *Weekly report:* live viewers, followers gained and lost, income and cost lines, cash change and stat changes; `Następny tydzień`.
     - *Batch:* a confirmation, then `Streamujesz 13 tygodni…` while the one request runs. The result shows a banner with the stop reason (`completed`, `pausedForDecision` — "the game is waiting for your decision", `careerEnded`, `interrupted` — "the career changed in another tab"), the summary with start and end values, and a ledger per week: the plan and its reason, each event choice with its reason and roll, and the week report. On mobile the ledger becomes cards.
     - *Ended:* the ending reason. The full ending screen arrives in M3.
5. **Settings.** The theme picker (§5), reachable from home and the top bar.

**Accessibility.** After a step changes, focus moves to the stage heading. Roll results and changed odds are announced through `aria-live`. Every action works by keyboard; focus stays visible; the 320-pixel viewport needs no horizontal scrolling. Gains, losses and risks always carry a sign and a text label, never colour alone. Nicknames render as text. Decorative motion and the tilt of overlay buttons switch off under `prefers-reduced-motion`.

## 5. Visual themes

The game looks deliberately cheap and amateur; its usability does not. Three themes share every component, the layout and the copy; a theme is a set of CSS tokens (colours, fonts, borders, shadows, radii) plus a few decorations, selected by `data-theme` on `<html>`.

| Theme | Look |
| --- | --- |
| `overlay` (default) | A cheap OBS overlay from old streams: neon frames, a green-screen top bar, yellow buttons stuck on slightly crooked, an Impact-style heading font. |
| `win98` | Windows 98 shareware: grey bevelled windows, a blue title bar, dotted focus, block progress bars. |
| `web2005` | A 2005 fan site: glossy gradients, a Comic-style heading font, dashed boxes, a visitor counter, red `NOWE!` labels. |

- Heading fonts: Anton (OFL) for `overlay` and Comic Neue (OFL) for `web2005`, both self-hosted with their Polish glyphs (the plan verifies the coverage before use); `win98` uses a Tahoma/Verdana system stack. Body text, odds and numbers use a readable system font in every theme. Font files ship with the app; no third-party font CDN.
- Every theme meets WCAG 2.2 AA contrast for text and focus indicators on its own; neon and gradients are borders and accents, not text backgrounds that fail contrast.
- The choice is stored in `localStorage`, applied before the first paint to avoid a flash, and never sent to the server. It changes presentation only.

## 6. Backend changes

- **`GET /v1/career-options`** (anonymous, `getCareerOptions`). Returns, from the current content, each background's starting cash, followers, drama, credibility, energy, skill per category, equipment per slot and score multiplier, and each difficulty's score multiplier. An `Application` query with a handler and tests; the response is part of the committed OpenAPI document.
- **OpenAPI polish** (checklist on #8): a bearer security scheme applied to the career operations; enum schemas with `"type": "string"` and without `null` in shared schemas; Problem Details with `code` (and `errors` for `400`); a documented `500`; readable summaries and tags. Orval must generate a working client without custom schema patches.
- **`WebApp/Dockerfile`**: a multi-stage build (SDK build, `aspnet` runtime) that copies `content/m1`, runs as a non-root user and listens on 8080. M6 reuses it for the production image; Roslyn ships with it because of runtime code generation (TD-94).
- No CORS: locally Vite proxies `/v1` to the API, and production serves the client and the API from one origin.

## 7. Local stack

Root `compose.yaml`, started with `docker compose up`:

| Service | Behaviour |
| --- | --- |
| `sql` | `mcr.microsoft.com/mssql/server:2025-RTM-ubuntu-22.04` (the image the tests pin), a named volume, a `sqlcmd` healthcheck. |
| `api` | Built from `WebApp/Dockerfile`; `ASPNETCORE_ENVIRONMENT=Development`, so migrations apply at startup; starts when `sql` is healthy; port 8080. |
| `web` | A pinned Node image running `npm ci` and `vite dev --host`; `source/frontend` bind-mounted, `node_modules` in a named volume; file-watch polling (needed with Docker Desktop on Windows); Vite proxies `/v1` to `http://api:8080`; port 5173. |

- The SA password and the connection string come from a root `.env` that git ignores; `.env.example` documents each variable without a real value. Compose stops with a clear message when a variable is missing. No staging or production secret is ever used locally.
- After backend changes, `docker compose up --build api` rebuilds the API.
- The local run is documented in `docs/development-and-delivery.md`: copy `.env.example` to `.env`, set the password, run `docker compose up`, open `http://localhost:5173`.

## 8. Testing, CI and delivery

**Test-driven development** ([TD-58](../../technical-decisions.md)) applies to every production change in the frontend and backend.

- **Vitest with Testing Library (jsdom)** for components and features: the gate (Escape, outside click and back navigation do not confirm; declining shows the exit), the creation form and its server errors, each stage view, the double-click guard, retries with the same `commandId`, error mapping, reason-code copy, the batch result for every stop reason, theme switching and persistence.
- **MSW** answers HTTP requests in component tests, so they exercise the generated client and Axios.
- **Coverage:** V8 provider, LCOV output, an 80% line gate for the frontend alone. Excluded: `api/generated`, `main.tsx`, test files and test support.
- **Playwright** (Chromium) runs one journey against the Compose stack: gate → new career → full manual week → 13-week batch → reload shows the same state. `@axe-core/playwright` checks the gate, creation, plan, event, report and batch result in all three themes; any violation fails the run.

**CI** (`.github/workflows/ci.yml`):

| Job | Steps | Required |
| --- | --- | --- |
| `frontend` | `npm ci`; Orval generation and `git diff --exit-code` on `api/generated`; `tsc --noEmit`; ESLint; `prettier --check`; Vitest with the 80% gate. | Yes (new) |
| `frontend-e2e` | `docker compose up -d --wait` with a CI-only password; Playwright; the report uploaded as an artifact on failure. | Yes (new) |
| `sonar` | Adds `source/frontend/src` with `sonar.javascript.lcov.reportPaths`; excludes `api/generated` and tests from the main analysis. Target: zero open issues. | Yes (existing) |

The `main` and `develop` rulesets (`.github/rulesets/*.json`) add `frontend` and `frontend-e2e` as required checks; they are applied to the live rulesets with the owner's yes, as for `backend-integration`.

**Lefthook** adds `npm run lint -- --fix` on staged frontend files (rejecting the commit when the fixer changes a file, as `dotnet format` does) and the fast Vitest suite.

**Delivery:** four PRs into `develop`, each reviewed by a fresh reviewer before its merge and merged only with zero SonarQube Cloud issues:

| PR | Branch | Contents | Issues |
| --- | --- | --- | --- |
| 1 | `feature/m2c-08-contract` (holds this spec) | `GET /v1/career-options` and the OpenAPI polish; the frontend scaffold with ESLint, Prettier, Vitest and MSW; Orval with the drift check; the `frontend` job; SonarQube Cloud for TypeScript; Lefthook | #8, part of #14 |
| 2 | `feature/m2c-11-entry` | Theme tokens and settings, the 18+ gate, home, character creation | #11 |
| 3 | `feature/m2c-12-week` | The game screen and the manual week | #12 |
| 4 | `feature/m2c-13-14-batch-stack` | The batch UI; `Dockerfile`, `compose.yaml`, `.env.example`; Playwright with axe; the `frontend-e2e` job; ruleset updates | client side of #13, #14 |

After PR 4 every M2 issue is on `develop`. Following TD-96, `release/m2` is cut from `develop` and merged into `main` with `Closes` for every delivered M1 and M2 issue and #58; the milestones close and `main` is reconciled into `develop`.

## 9. Decision records and documentation

Recorded during implementation, in English:

- **GDD decision log** (owner decisions): the three selectable visual themes with the overlay default; the adult-theme events and tone check moved to M3.
- **TD-98** (agent proposal accepted): frontend architecture — server-driven game screen, known careers with per-career secrets in `localStorage`, one `commandId` per confirmed intent with same-ID retries, MSW in component tests, self-hosted OFL theme fonts.
- **TD-99** (owner decision, 2026-10-15): the local Compose stack with the API built from a multi-stage `Dockerfile`, Vite with polling, and secrets from an ignored `.env`.
- **TD-100** (owner decision, 2026-10-15; the four-PR split is an agent proposal accepted with the design): the required `frontend` and `frontend-e2e` jobs, SonarQube Cloud for TypeScript with LCOV, Lefthook frontend commands, the ruleset updates and the four-PR delivery.
- **TD-101** (agent proposal accepted): `GET /v1/career-options` and the OpenAPI polish for the generated client.

Documentation updates: `validation-and-delivery.md` (vertical-slice criterion: adult-theme events and the tone check move to M3; the smoke test's admin login moves to M4), `content-and-experience.md` §3–§4 (M2 home screen, visual themes), `technology-stack.md` (exact frontend versions, MSW, Playwright with axe, fonts), `repository-standards.md` (frontend config files, `compose.yaml`, `.env.example`, generated client), `development-and-delivery.md` (local Compose run, CI jobs, Lefthook), `technical-architecture.md` (the career-options endpoint), `requirements-traceability.md` (rows delivered by C), and the issue texts of #8, #11, #12, #13 and #14.

## 10. Acceptance criteria → evidence

| Issue criterion | Evidence |
| --- | --- |
| #8 The frontend builds and has a component test | The `frontend` job builds, typechecks and runs Vitest. |
| #8 Generated API code is never edited by hand | `api/generated` is regenerated in CI and diffed; it is excluded from lint and formatting so nobody "fixes" it. |
| #8 CI detects contract drift | The backend OpenAPI drift check plus the Orval regeneration diff in `frontend`. |
| #11 Escape and outside clicks cannot bypass the gate | Vitest tests for Escape, outside click and back navigation; the Playwright journey starts at the gate. |
| #11 Declining shows a neutral exit | Vitest test of the exit view; no game request is made before confirmation. |
| #11 The starting profile reaches the API | MSW asserts the creation request body; the Playwright journey creates a real career. |
| #11 A lost creation response does not trigger duplicate requests | The creation component test returns a network error and asserts exactly one `POST /v1/careers` (TD-106). |
| #12 A full week can be played in the browser | The Playwright journey plays plan, every event and settlement. |
| #12 Choices cannot be submitted twice | Vitest: a double activation sends one request; retries reuse the `commandId`. |
| #12 Refreshing restores the server-confirmed state | The Playwright journey reloads and compares the visible state. |
| #13 (client) Batch UI with cumulative report and pause prompt | Vitest for each stop reason; the Playwright journey runs a real 13-week batch. |
| #14 Local smoke test covers career start, choice and reload | The Playwright journey on `docker compose up`. The test admin login moves to M4. |
| #14 CI blocks failed tests or coverage | Required `frontend` (80% gate) and `frontend-e2e` jobs. |
| #14 Secrets are kept in local environment/secrets storage | `.env` is ignored; Compose fails without its variables; CI uses a generated throwaway password. |

## 11. Risks

- **Orval and the Wolverine-generated document.** PR 1 generates the client from the polished document before any screen depends on it.
- **File watching on Windows.** Polling in the `web` container is slower than native watching; `npm run dev` on the host stays possible for heavy UI work.
- **Docker Desktop resources.** SQL Server needs about 2 GB of memory; the local run documents the requirement.
- **End-to-end flakiness and CI time.** One journey, Playwright auto-waiting, no fixed sleeps; Compose images are cached between runs where possible.
- **Theme contrast.** Neon and gradient themes can fail contrast; axe checks every theme in CI, and decoration stays off text.
- **Secrets in browser storage.** XSS exposure is reduced by rendering all user text as text and by the content security policy in M6.
- **Library compatibility with React 19.3.0.** Versions are pinned in the lockfile at scaffolding time and recorded in the technology stack.
