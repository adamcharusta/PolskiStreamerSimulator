# Technology Stack and Source Layout

Status: **technical interview specification**, 2026-10-12. This document records the owner's library choices and distinguishes them from implementation defaults. Read it with the [technical architecture](technical-architecture.md) and [technical decision register](technical-decisions.md). Application and test code belong under `source/`; documentation is under `docs/`; Kubernetes and Ansible assets are under `k8s/`; GitHub Actions workflows are under `.github/workflows/`. Add root `scripts/` only for shared helper scripts that become necessary. Gameplay is online-only and the C# `Domain` project owns its rules.

## Source layout

The proposed layout applies the owner's four-project .NET solution and keeps React in the same `source/` tree:

```text
docs/                         English project documentation
content/                      versioned content and balance snapshots (M1: content/m1/*.json; TD-76)
.github/
  workflows/                  GitHub Actions workflows
source/
  backend/
    PolskiStreamerSimulator.slnx
    Domain/                   game rules, content models, ranking invariants
    Application/              CQRS messages, handlers, application policies
    Infrastructure/           EF Core, Identity, SQL Server, external adapters
    WebApp/                   ASP.NET Core host, Wolverine HTTP endpoints, OpenAPI
    Tests/                    xUnit v3 unit, property and integration tests (Microsoft.Testing.Platform; TD-82)
    Tools/BatchSimulator/     console batch simulator for seeded careers; not deployed (TD-78)
  frontend/                   React + TypeScript + Vite, game and /admin route
    src/
      features/               feature-level components, hooks, state and screens
      components/ui/          owned shadcn/ui component source
      api/generated/          Orval output; never hand-edit
      locales/                Polish UI copy and future translations
k8s/
  ansible/                    k3s inventory, playbooks and roles
  charts/                     pinned Helm assets for shared/app workloads
  environments/               staging, release-candidate and production values
scripts/                       optional shared helper scripts, only when needed
compose.yaml                   local development only
```

The root directory contains cross-project configuration files and the local Compose entry point. `source/` is for backend/frontend application and test code, while `k8s/` is the infrastructure-as-code boundary. The initial versioned event catalog and balance defaults are seeded from repository data; owner edits and immutable published snapshots live in SQL Server. Polish interface translations live under frontend `locales/`; editable event copy belongs to versioned event data. Exact leaf folder names may be adjusted during scaffolding, but these top-level boundaries and four .NET layers are confirmed. `WebApp` means the **server-side REST API host**. React is the separate client project. The public game and owner-only `/admin` area share one React application; the admin route is lazy loaded.

The root `.gitignore`, `.gitattributes` and `.editorconfig` govern agent/editor artifacts, line endings and deployment scripts. `source/backend/` contains its own `.gitignore`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props` and `global.json`; `source/frontend/` contains its own `.gitignore` and Prettier defaults. The requested ESLint package set and configuration rules are documented in [Repository Standards](repository-standards.md). The executable ESLint config and the lockfile were created with the React app in M2-C (TD-98).

### Layer boundaries

| Project | Responsibility | Dependency rule |
| --- | --- | --- |
| `Domain` | Authoritative 20-year game simulation, declarative event validation, bounded balance parameters, state invariants, score calculation, and server domain models for careers, seasons, ranking and moderation. | No EF Core, ASP.NET Core, Wolverine HTTP, or infrastructure dependency. |
| `Application` | Commands, queries, Wolverine handlers, career orchestration, save-slot policy, admin draft/publication use cases, interfaces for persistence and validation. | Depends on `Domain`, not on `Infrastructure` or `WebApp`. |
| `Infrastructure` | EF Core SQL Server contexts and migrations, event/balance draft and published-snapshot persistence, ASP.NET Core Identity persistence, anonymous career-secret hashing, Serilog SQL sink setup. | Implements application ports; depends on `Application` and `Domain`. |
| `WebApp` | ASP.NET Core composition root, Wolverine HTTP endpoint classes, public and admin authorization, OpenAPI, request/response contracts, exception mapping. | Wires the other layers together. |

The owner superseded the earlier offline/TypeScript simulation design. The browser sends player choices to the API; `Domain` calculates outcomes, state transitions and score in C#. No game simulation runs in React, and no Node replay worker or .NET WebAssembly runtime is needed. The server stores the authoritative career after each accepted choice. Versioned ruleset implementations remain available so a career started before an update can finish on its original rules.

Infrastructure automation belongs under `k8s/ansible/`, with pinned Helm charts and environment values for both shared SQL Server/Vault and the game application under `k8s/`. Keep the development-only Docker Compose entry point at root `compose.yaml`; create `scripts/` only if cross-cutting helpers are needed. Ansible verifies/reconciles k3s prerequisites and shared SQL Server/Vault, while the game's Helm release uses its own production, release-candidate and staging namespaces. `ovh2` remains outside k3s as the backup server; release candidates use `rc.polskistreamersimulator.pl`. See [Infrastructure and Operations](infrastructure-and-operations.md) for the deployment model and [Development and Delivery](development-and-delivery.md) for branch, CI and local-run details.

## ASP.NET Core, Wolverine and data

- Use Wolverine for in-process CQRS commands, queries and handlers. Use `WolverineFx.Http` for HTTP endpoints rather than MVC controller classes. Keep HTTP binding and status codes in `WebApp`; place use-case handlers in `Application`. The owner chose **no Wolverine durable queue/message store at launch**. A long batch simulation, if needed, may use a database-backed background job in the API; ordinary choices are applied transactionally and idempotently. M2 generates Wolverine's handler code at runtime with `WolverineFx.RuntimeCompilation`; M6 decides whether to pre-generate it (TD-94).
- Use FluentValidation for shape and business-input validation. Install both `WolverineFx.Http.FluentValidation` for HTTP endpoint validation and `WolverineFx.FluentValidation` for Wolverine command/query handler middleware. Their registrations must not double-register validators; service/database-dependent checks belong in explicit application validation. Return stable RFC 7807 Problem Details from API failures. [Wolverine HTTP validation](https://wolverinefx.net/guide/http/validation), [Wolverine handler validation](https://wolverinefx.net/guide/handlers/fluent-validation).
- Use Mapster for explicit entity-to-DTO and request-to-command mapping. Mapping does not contain gameplay rules or authorization decisions. Keep OpenAPI DTO shapes stable and test any nontrivial mappings. [Mapster project](https://github.com/MapsterMapper/Mapster).
- Use EF Core with the SQL Server provider for careers, saves, ranking and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` for **owner administration only**. Public players still have no accounts. Put Identity data behind a distinct context/schema or equivalent separation from game tables; only the owner can receive an administrator account. EF Core migrations are reviewed and applied in staging before production. [EF Core SQL Server provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).
- The owner chose ASP.NET Core Identity cookie login with authenticator-app TOTP and one-time recovery codes for `/admin`. Create the sole owner account after migration through a one-time administrative command on the server; do not expose a public registration endpoint or auto-seed production credentials from deployment secrets. Require authorization on every admin API endpoint, not only the React route. Use anti-forgery protection for cookie-authenticated mutations. No player login is part of the game. Wolverine HTTP endpoints support ASP.NET Core authorization metadata. [Wolverine authorization](https://wolverinefx.net/guide/http/security).
- Use Serilog structured logs to console and a SQL Server log table. The SQL sink is for operational diagnostics, while moderation and season changes are written to a separate transactional audit record. Console logs remain available when SQL Server cannot accept events. Filter career/recovery secrets, action histories, and player identifiers; bound retention and write volume so logs do not consume the career database. The MSSQL sink batches events, so it is not a durable audit trail. [Serilog SQL Server sink](https://github.com/serilog-mssql/serilog-sinks-mssqlserver).

### OpenAPI and client generation

`WebApp` publishes a versioned OpenAPI document. Prefer the built-in `Microsoft.AspNetCore.OpenApi` generator and Wolverine's OpenAPI command, which can emit a JSON artifact without starting the host or connecting to SQL Server. CI checks that the generated document and the Orval client are in sync. **Current state (TD-92):** from `source/backend`, `dotnet run --project WebApp -- openapi --document v1 --output openapi/v1.json` writes the committed `source/backend/WebApp/openapi/v1.json` (the output path is relative to `WebApp`), and the `backend` CI job fails when the file differs from the code. Endpoints return typed results, so each operation lists exactly its responses. Career requests carry `Authorization: Bearer <secret>`, which the document does not list as a parameter; the client adds it per career. **Client (TD-101):** `npm run api:generate` in `source/frontend` runs Orval on that file into `src/api/generated/` (`split` mode: `game.ts` and `game.schemas.ts`), committed and never edited by hand; the `frontend` job regenerates it and fails on any difference. Career operations declare the bearer scheme `careerSecret`, so the generated functions take the per-career `Authorization` header as request options. An interactive Swagger/Scalar browser may be added for developer and staging use, protected from public production access. [Wolverine OpenAPI generation](https://wolverinefx.net/guide/http/metadata).

Orval generates TypeScript DTOs and API functions from the OpenAPI artifact. Use TanStack Query (React Query) for career state, decisions, leaderboard and admin data, with Axios as the HTTP transport and one configured client for timeouts, cancellation and error normalization. Orval supports React Query output with Axios. The server database is authoritative; Zustand holds only transient UI state and the browser retains a separate secret for each locally known career plus optional per-career recovery-key exports. A query cache is not a save. Decision mutations use idempotency keys and a state-version precondition so a lost response or second tab cannot repeat a random draw. [Orval output configuration](https://orval.dev/docs/reference/configuration/output/).

The selected visual theme and the age acknowledgment are browser state. If browser storage is unavailable, the app uses the default overlay and shows the 18+ gate on load; explicit theme and gate choices still apply for the current page without crashing (TD-103). Persistence resumes when storage becomes available.

This is a good fit for the requested stack: TanStack Query handles server cache and request lifecycle, Axios provides a configurable transport, and Orval keeps FE contracts tied to the API. Configure mutation retry per endpoint, especially for decisions that roll outcomes; retry only with the same idempotency key. The browser and API should normally share one HTTPS origin in production.

## React application

| Area | Confirmed choice | Boundary |
| --- | --- | --- |
| Build and language | React 19.3.0, TypeScript, Vite | Current desktop and mobile browsers; online connection is required to play. |
| Structure | Feature-based, component-driven architecture | Reusable primitives in `components/ui`; gameplay rules stay in C# `Domain`, outside React components. |
| Routing | TanStack Router | Public game routes and owner-only, lazy-loaded `/admin` route. |
| Forms | React Hook Form + Zod | Character creation, settings and owner login; API remains the authoritative validator. |
| Game and owner UI | Tailwind CSS + shadcn/ui | One component system for both; customize the owned source for the streamer-dashboard visual direction. |
| Local state | Zustand | In-memory UI state only; the server remains authoritative for career progress. |
| Localization | i18next from the start | Polish launch copy with stable keys; later languages can be added without renaming rule IDs. |
| Streamer Friendly text | Versioned client-side masking after localization | Optional, off by default; cover all visible and accessible text and `pdf-lib` output without changing C# rules or saved state. |
| Server state | TanStack Query + Axios, generated by Orval | API data and mutations; do not treat query cache as a save. |
| PDF export | pdf-lib | Generate the diploma locally from the server's final, read-only career report. |
| Optional charts | Recharts | Add only if the owner dashboard benefits from charts; tables remain sufficient at launch. |

The admin route is hidden from normal navigation and protected by Identity, but security is enforced by the API. The owner alone can moderate scores and nicknames, manage event drafts, edit balance constants, preview/validate candidate versions and publish them. The private panel replaces the earlier CLI-only launch proposal. React 19.3.0 was checked against the [official React releases](https://github.com/facebook/react/releases).

### Frontend versions (M2-C, TD-98)

| Area | Packages and exact versions |
| --- | --- |
| Runtime and build | Node 24.22.0 LTS (`.nvmrc`), React/React DOM 19.3.1, TypeScript 6.0.3, Vite 8.3.4 with `@vitejs/plugin-react` 6.1.2 |
| Routing, data and state | `@tanstack/react-router` 1.170.44, `@tanstack/react-query` 5.104.2, Axios 1.20.0, Orval 8.43.0, Zustand 5.0.15 |
| Forms and copy | React Hook Form 7.90.0, Zod 4.6.7, `@hookform/resolvers` 5.9.1, i18next 26.4.5, `react-i18next` 17.0.16 |
| UI | Tailwind CSS 4.3.3, `radix-ui` 1.8.1, `class-variance-authority` 0.7.1, `clsx` 2.1.1, `tailwind-merge` 3.7.0, `@fontsource/anton` and `@fontsource/comic-neue` 5.3.0 |
| Tests | Vitest 5.0.4 with V8 coverage, jsdom 30.1.2, Testing Library (React 16.3.3, DOM 10.4.2, user-event 14.6.8, jest-dom 7.0.1), MSW 3.0.3, Playwright 1.64.1, `@axe-core/playwright` 4.13.0 |
| Lint and format | ESLint 9.39.5 with the owner's plugin set (see repository standards), `typescript-eslint` 8.71.2, Prettier 3.9.10 |

`source/frontend/package.json` pins every version exactly and `package-lock.json` locks the tree; CI installs with `npm ci`.

## Verification and package management

The owner chose Vitest + Testing Library for React component and feature tests (with MSW answering HTTP requests, so tests run through the generated client; TD-98), Playwright for browser journeys, and xUnit + Testcontainers with SQL Server for backend integration tests. Test deterministic C# `Domain` transitions, concurrent/duplicate commands, score calculation, anonymous career recovery and unranked save slots. Include OpenAPI/Orval generation in CI and fail on contract drift. Test owner login, second-factor enforcement, unauthorized admin API access, SQL log-sink failure while console logging continues, and the full career-to-leaderboard path.

Use a root Lefthook pre-commit hook for backend `dotnet format`, frontend `npm run lint -- --fix` and fast xUnit/Vitest tests. GitHub Actions checks Prettier and runs the complete suites, then submits C# and TypeScript analysis plus their coverage reports to SonarQube Cloud for PRs into `develop` and `main`. Its quality-gate job is required for merge; the independently measured backend/frontend 80% line-coverage gates still apply. The initial SonarQube Cloud setup uses one project for the repository and verifies both source trees are scanned.

Implement new behavior and bug fixes using TDD: write a failing test, make it pass with the smallest production change, then refactor. Configure line-coverage reporting for xUnit backend tests and Vitest frontend tests when the projects are scaffolded. CI enforces **80% or more separately in each project**. Explicit exclusions cover tests, generated files, build output and migrations; they must be reviewed rather than expanded merely to meet the threshold. Playwright covers browser journeys in addition to these coverage-producing suites.

Pin compatible package versions in lockfiles and record the exact chosen version set when code is scaffolded. The owner specified .NET 10 and React 19.3.0; use stable compatible Vite and library releases at implementation time. Do not treat library defaults as game rules; ruleset and content versions are independently frozen for ranked seasons.
