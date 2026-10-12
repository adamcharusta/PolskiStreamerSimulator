# Agent Guide

This file applies to the whole repository. Read the relevant current specifications before changing code, content, infrastructure, or delivery plans. Later owner decisions supersede older proposals; do not treat historical reference documents as current requirements.

## Documentation map

| File | What to find there |
| --- | --- |
| `README.md` | Project entry point and links to the main specifications. |
| `docs/game-design-document.md` | Product vision, confirmed product decisions, core loop, document map, and chronological product decision log. Start here for scope. |
| `docs/gameplay-systems.md` | Career rules, weekly loop, statistics, formulas, economy, automation, endings, and scoring. Numerical values are a prototype baseline until a ranked ruleset is frozen. |
| `docs/content-and-experience.md` | Fictional world, event structure, Polish copy, adult themes, screen flows, accessibility, and text filters. |
| `docs/technical-architecture.md` | Online/server-authoritative boundaries, career API, persistence, ranking integrity, security, and deployment architecture. |
| `docs/technology-stack.md` | Repository layout, .NET layers, React stack, libraries, API client generation, and test tools. |
| `docs/infrastructure-and-operations.md` | VPS and k3s topology, Ansible/Helm, SQL Server, Vault, environments, backups, and recovery. |
| `docs/development-and-delivery.md` | Git Flow, local development, CI, coverage, staging, release candidates, and production deployment. |
| `docs/repository-standards.md` | Ignore files, line endings, editor rules, formatting, linting, and package configuration. |
| `docs/technical-decisions.md` | Confirmed technical/workflow decisions, implementation defaults to validate, outstanding deployment inputs, and superseded choices. |
| `docs/validation-and-delivery.md` | Release stages, acceptance criteria, simulation and test priorities, risks, and documentation maintenance. |
| `docs/requirements-traceability.md` | Requirement IDs mapped to source, acceptance criterion, issue/milestone and status; superseded guidance with its replacement. |
| `docs/superpowers/` | Design specs (`specs/`) and implementation plans (`plans/`) for milestone work, such as the M1 simulation prototype. |
| `docs/core-concept.md` | Original concept only; use the current GDD and linked specifications where they differ. |
| `docs/polski-pilkarz-simulator-analysis.md` | Reference-game design analysis, not requirements for this game. |
| `docs/polski-pilkarz-simulator-code-analysis.md` | Reference-game code analysis, not an implementation plan for this repository. |

Application code belongs under `source/`, infrastructure under `k8s/`, and GitHub Actions under `.github/workflows/`.

## Language and decision records

- Write project documentation and decision records in **English**, regardless of the language used in chat or by the person making the decision. Use another documentation language only when the owner explicitly asks for it. This does not change the planned Polish player-facing game text.
- Document **every** new decision or change to the documented plan, whether it comes from the owner or from an agent. This includes additions, removals, changed behavior, values, scope, architecture, tools, operations, quality gates, and delivery assumptions. Do it in the same task as the change; do not leave the chat or a GitHub issue as the only record.
- Update the affected primary specification(s), not just a general note. Update acceptance criteria and related documents when the change affects them. Keep cross-references consistent.
- Record the date, source (`Owner decision` or `Agent proposal/default`), previous plan, new decision, reason, and impact or open questions. Put product decisions in the GDD decision log and technical/workflow/operations decisions in the technical decision register. Clearly distinguish confirmed owner choices from agent proposals that still need validation.
- When a new decision replaces an old one, mark the old guidance as superseded or update it so two conflicting instructions do not appear current. If a related GitHub issue or milestone exists, bring its description into line with the documentation.
- Do not silently change ranked rules, scoring, random odds, or published content versions; follow the versioning and season rules in the specifications.
