# PolskiStreamerSimulator

Polski Streamer Simulator is a free, satirical 18+ browser career game in which the player guides a fictional Polish streamer through a 20-year (1,040-week) career. Each week the player picks one main action and a primary category, plays or automates a stream with two or three live events, and watches followers, live viewers, money, drama, credibility and burnout change. All gameplay and scoring rules run in a deterministic, server-authoritative C# `Domain`, and finished careers compete on a season-scoped global top 100.

**Status:** M1 simulation prototype complete pending the content-capacity estimate ([#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7); [milestone](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/1)). The deterministic C# `Domain` plays full 1,040-week careers; see the [M1 baseline simulation report](docs/reports/m1-baseline-simulation.md). There is no playable UI, HTTP game API or database yet.

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
  frontend/                React client (later milestone)
k8s/                       Ansible, Helm and environment values (later milestone)
.github/workflows/         GitHub Actions (later milestone)
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
- [M1 baseline simulation report](docs/reports/m1-baseline-simulation.md) — 10,000 seeded careers: endings, outcome percentiles, extremes, cost of a full career and balance follow-ups.
- [Original core concept](docs/core-concept.md) — background only; the GDD and specifications above take precedence.
- [Reference game analysis](docs/polski-pilkarz-simulator-analysis.md) and [reference code analysis](docs/polski-pilkarz-simulator-code-analysis.md) — historical references, not requirements.

Agents start with [AGENTS.md](AGENTS.md).

## Build and test

Requires the .NET 10 SDK (pinned by `source/backend/global.json`). Run every `dotnet` command from `source/backend`:

```sh
cd source/backend
dotnet build PolskiStreamerSimulator.slnx
dotnet test PolskiStreamerSimulator.slnx
dotnet run --project Tools/BatchSimulator -- --careers 1000 --seed 42
```
