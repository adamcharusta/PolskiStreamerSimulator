# Claude Agent Guide

Read and follow [AGENTS.md](AGENTS.md); it is the canonical agent guide for this repository. The quick map below helps locate the relevant specification before work begins.

| Document | Purpose |
| --- | --- |
| `README.md` | Project entry point and links to the main specifications. |
| `docs/game-design-document.md` | Current product scope, confirmed choices, and product decision history. |
| `docs/gameplay-systems.md` | Simulation rules, formulas, economy, automation, and score. |
| `docs/content-and-experience.md` | Event data, Polish content, screens, accessibility, and filters. |
| `docs/technical-architecture.md` | Online API, persistence, ranking integrity, and security boundaries. |
| `docs/technology-stack.md` | Source layout, .NET/React stack, libraries, and testing tools. |
| `docs/infrastructure-and-operations.md` | k3s, Ansible/Helm, SQL Server, Vault, backups, and recovery. |
| `docs/development-and-delivery.md` | Git Flow, local setup, CI, staging, RC, and production releases. |
| `docs/repository-standards.md` | Editor, ignore, format, lint, and package conventions. |
| `docs/technical-decisions.md` | Current technical decisions, defaults, open inputs, and superseded choices. |
| `docs/validation-and-delivery.md` | Acceptance criteria, test gates, balance validation, and risks. |
| `docs/requirements-traceability.md` | Requirement → source → acceptance criterion → issue/milestone → status; superseded guidance → replacement. |
| `docs/reports/` | Generated simulation reports (M1 baseline) and their findings. |
| `docs/superpowers/` | Milestone design specs and implementation plans (M1 simulation prototype). |
| `docs/core-concept.md` | Original concept; the current GDD and specifications take precedence. |
| `docs/polski-pilkarz-simulator-analysis.md` | Historical reference-game design analysis, not current requirements. |
| `docs/polski-pilkarz-simulator-code-analysis.md` | Historical reference-game code analysis, not an implementation plan. |

Write documentation and decision records in **English** regardless of the conversation language, unless the owner explicitly requests another language. Record every owner or agent decision that changes the documented plan in the relevant documentation during the same task, including its source, reason, impact, and any superseded guidance. Update affected specifications and acceptance criteria; keep related GitHub tasks aligned. See [AGENTS.md](AGENTS.md) for the full procedure. Player-facing game copy remains Polish unless the product specification changes.
