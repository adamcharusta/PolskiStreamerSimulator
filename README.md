# PolskiStreamerSimulator

Polski Streamer Simulator is a free, satirical 18+ browser career game in which the player guides a fictional Polish streamer through a 20-year (1,040-week) career. Each week the player picks one main action and a primary category, plays or automates a stream with two or three live events, and watches followers, live viewers, money, drama, credibility and burnout change. All gameplay and scoring rules run in a deterministic, server-authoritative C# `Domain`, and finished careers compete on a season-scoped global top 100.

**Status:** M1 simulation prototype and M2 vertical slice released to `main` and closed on 2026-10-16 ([M1](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/1), [M2](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/2)); M3 complete gameplay systems released to `main` on 2026-10-28 ([M3](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/3); the content-capacity estimate [#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7) moved to M5, TD-120). The game is playable locally: create a ranked or unranked career, play manual weeks and 4/13/52-week strategy batches with five categories, sponsors, wagers, the optional side business, platforms and bans, browse the week history and reach any of the five endings with a PDF diploma, in the browser against the HTTP API and SQL Server (see *Run locally*). The [M1](docs/reports/m1-baseline-simulation.md) to [M3-C](docs/reports/m3c-baseline-simulation.md) simulation reports cover full 1,040-week careers. M4 online play and administration released to `main` on 2026-10-31 ([M4](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4)): per-career recovery keys and retention, ranked careers with season leaderboards, unranked save slots with export and import, the owner's `/admin` panel with two-factor sign-in, moderation, an audit trail and content publication, and opt-in analytics with outage notices. Next: M5.

## Repository layout

```text
docs/                      English specifications, decision records and reports
  reports/                 generated simulation reports
  superpowers/             design specs and implementation plans for agent work
content/
  m1/                      versioned M1 content and balance snapshot (JSON)
source/
  backend/                 .NET 10 solution (PolskiStreamerSimulator.slnx)
    Domain/                pure deterministic simulation, no NuGet dependencies
    Application/           use-case ports
    Infrastructure/        adapters (content loading)
    WebApp/                ASP.NET Core host
    Tests/                 xUnit, FsCheck and architecture tests
    Tools/BatchSimulator/  console runner for seeded careers and reports
  frontend/                React client (M2-C; TD-98)
k8s/                       Ansible, Helm and environment values (later milestone)
.github/
  workflows/               CI (backend, backend integration, frontend, browser journey, SonarQube Cloud, PR policy)
  rulesets/                main and develop branch rules
  scripts/                 CI helper scripts
```

## Specifications

- [Game design document](docs/game-design-document.md) — product scope, confirmed choices and the product decision log.
- [Gameplay systems](docs/gameplay-systems.md) — simulation rules, formulas, economy, automation and score.
- [Content and experience](docs/content-and-experience.md) — event data, Polish content, screens, accessibility and filters.
- [Technical architecture](docs/technical-architecture.md) — online API, persistence, ranking integrity and security boundaries.
- [Technology stack](docs/technology-stack.md) — source layout, .NET/React stack, libraries and testing tools.
- [Infrastructure and operations](docs/infrastructure-and-operations.md) — k3s, Ansible/Helm, SQL Server, Vault, backups and recovery.
- [Development and delivery](docs/development-and-delivery.md) — Git Flow, local setup, CI, staging, release candidates and production.
- [Repository standards](docs/repository-standards.md) — editor, ignore, format, lint and package conventions.
- [Technical decision register](docs/technical-decisions.md) — confirmed technical decisions, defaults, open inputs and superseded choices.
- [Validation and delivery plan](docs/validation-and-delivery.md) — release stages, acceptance criteria, test gates and risks.
- [Requirements traceability](docs/requirements-traceability.md) — requirement → source → acceptance criterion → issue/milestone → status, and superseded decisions with their replacements.
- [Content production capacity estimate](docs/content-capacity-estimate.md) — measurement method, effort model and proposed launch floor of event templates, pending owner measurement.
- [M1 baseline simulation report](docs/reports/m1-baseline-simulation.md) — 10,000 seeded careers: endings, outcome percentiles, extremes, cost of a full career and balance follow-ups.
- [Original core concept](docs/core-concept.md) — background only; the GDD and specifications above take precedence.
- [Reference game analysis](docs/polski-pilkarz-simulator-analysis.md) and [reference code analysis](docs/polski-pilkarz-simulator-code-analysis.md) — historical references, not requirements.

Agents start with [AGENTS.md](AGENTS.md).

## Run locally

Requires Docker Desktop (about 2 GB of memory for SQL Server). From the repository root:

```sh
cp .env.example .env          # then set MSSQL_SA_PASSWORD to a local-only password
docker compose up             # SQL Server, the API on :8080 and Vite on :5173
# open http://localhost:5173
```

`docker compose down -v` resets the local database. See [local Docker Compose](docs/development-and-delivery.md#local-docker-compose) for rebuilding the API and running the browser journey.

## Owner account and `/admin`

The owner panel at `/admin` is never linked from the game. It has exactly one account, which only a command run next to the API creates; there is no registration page ([M4-D design](docs/superpowers/specs/2026-10-29-m4d-owner-account-and-moderation-design.md), TD-124). Run it once after the database is migrated:

```sh
# local Docker Compose stack
docker compose exec api dotnet PolskiStreamerSimulator.WebApp.dll owner-create --username <name>
# from source/backend against the database in ConnectionStrings__Game
dotnet run --project WebApp -- owner-create --username <name>
```

The command prints, once, a generated password, the authenticator key with its `otpauth://` URI, and ten recovery codes. Add the key to an authenticator app and keep the password and codes offline; the server stores only hashes of the codes, and each code signs in once. A second `owner-create` is refused.

Sign-in at `/admin` takes the password, then the six-digit code from the app or, behind „Użyj kodu zapasowego”, one recovery code. Five failed attempts lock the account for 15 minutes, and a sign-in lasts 8 hours. Passwords and second factors cannot be changed in the browser: after a lost phone, lost codes or a leaked password, run

```sh
docker compose exec api dotnet PolskiStreamerSimulator.WebApp.dll owner-create --reset
```

which prints a new password, authenticator key and recovery codes, unlocks the account and signs out every session.

In the panel the owner hides an entry (also the way to revoke an invalid result) and restores it, each with a required reason; a hidden entry keeps its score and returns to its rank when restored. Hiding, restoring and season deadline extensions (also `season-extend` on the command line) are kept in the panel's „Dziennik zmian” audit trail.

## Build and test

Requires the .NET 10 SDK (pinned by `source/backend/global.json`). Run every `dotnet` command from `source/backend`:

```sh
cd source/backend
dotnet build PolskiStreamerSimulator.slnx
dotnet test PolskiStreamerSimulator.slnx
dotnet run --project Tools/BatchSimulator -- --careers 1000 --seed 42
```

Install the Git hooks once. This requires Node.js; the hooks run `dotnet format` and the fast backend tests before each backend commit:

```sh
npm install
```

CI runs the same backend checks as:

```sh
cd source/backend
dotnet build PolskiStreamerSimulator.slnx -c Release
dotnet format PolskiStreamerSimulator.slnx --verify-no-changes
dotnet test PolskiStreamerSimulator.slnx -c Release --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total
```

Changes reach `develop` and `main` only through pull requests; see [development and delivery](docs/development-and-delivery.md). Every change has a GitHub issue, which closes when the change reaches `main` with its milestone release, and SonarQube Cloud stays at zero open issues ([issues, milestones and releases](docs/development-and-delivery.md#issues-milestones-and-releases)).
