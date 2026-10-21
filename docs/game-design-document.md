# Polski Streamer Simulator — Game Design Document

Status: **Design draft for review** (2026-10-12). This document distinguishes user-confirmed direction from proposed implementation defaults. Detailed rules, content, interface, technology, and release checks are linked below.

## Document Map

- [Gameplay and balance specification](gameplay-systems.md): turn order, state, formulas, progression, economy, score, and automated play.
- [Content and experience specification](content-and-experience.md): event authoring, fictional world, 18+ themes, screen flows, accessibility, and localization.
- [Technical architecture](technical-architecture.md): online careers, C# simulation, server-authoritative ranking, data contracts, and deployment.
- [Technology stack and repository layout](technology-stack.md): root directories, .NET layers, Wolverine, database and Identity, React libraries, OpenAPI/Orval, and tests.
- [Infrastructure and operations](infrastructure-and-operations.md): Ansible/k3s deployment, shared SQL Server and Vault, namespaces, HPA, backups and recovery.
- [Development and delivery](development-and-delivery.md): Git Flow (`main`, `develop`, feature, bugfix, release and hotfix), GitHub Actions, automatic releases and local Docker Compose.
- [Repository standards](repository-standards.md): three scoped ignore files, line endings, editor rules, .NET build configuration, and React lint/format packages.
- [Technical decision register](technical-decisions.md): confirmed constraints, stack and hosting choices, and implementation follow-ups.
- [Validation and delivery plan](validation-and-delivery.md): measurable acceptance criteria, balance scenarios, release gates, and decisions to review.
- [Original core concept](core-concept.md) and [reference game analysis](polski-pilkarz-simulator-analysis.md) provide background; where they differ, this document and its linked specifications take precedence.

**Decision status:** The product choices in Section 2 were confirmed in the interview. The numerical defaults in [Gameplay and Balance Specification](gameplay-systems.md) are approved as the **first prototype baseline**, subject to simulation and player-test changes before the ranked ruleset is frozen. Proposed content counts and other implementation details remain reviewable. Confirmed fictional platform names are Czatnia, PulsLive, and Nurt.

## 1. Product Vision

The player builds a fictional Polish streaming career, seeking audience growth and wealth through content choices, skill development, equipment, sponsorships, relationships, and risk. Both a respected, low-drama career and a controversial, high-drama career must be viable.

The game takes structural inspiration from *Polski Piłkarz Simulator 1.45*: a visible career progression loop, consequential choices, transparent random outcomes, and a readable career history. The existing reference analyses are [`polski-pilkarz-simulator-analysis.md`](polski-pilkarz-simulator-analysis.md) and [`polski-pilkarz-simulator-code-analysis.md`](polski-pilkarz-simulator-code-analysis.md).

## 2. Confirmed Product Decisions

| Area | Decision |
| --- | --- |
| Delivery | Browser game playable on desktop and mobile. |
| Primary cadence | One detailed stream per in-game week. |
| Stream resolution | The player may play a stream manually or have it resolved automatically. |
| Manual stream | Select a category, then resolve typically two or three live events. |
| Starting profile | Choose one of four backgrounds, each with benefits and drawbacks. |
| Platform progression | Everyone begins on the same fictional platform; switching among three total platforms becomes possible later. |
| Automatic decision policy | The player sets a strategy, and automatic resolution selects event responses according to it. |
| Audience model | Track persistent followers separately from live viewers for each stream. |
| Economy coverage | Include subscriptions, donations, advertising, sponsorships, and costs. |
| Stream category | Pick one primary category per stream, with occasional changes during the broadcast. |
| Final score | Followers and final wealth receive similar weight after normalization, with an achievement bonus. |
| Social state | Track drama and credibility as separate attributes. |
| Platform bans | A permanent ban from one platform permits migration. A ban covering all available platforms ends the career. |
| Weekly action budget | One main action outside the stream per week. |
| Randomness transparency | Show success chances before a choice; show the roll and causal explanation afterward. |
| Difficulty | Easy and normal modes. Normal mode earns a final-score bonus relative to easy. Exact modifiers remain open. |
| Tone | Satire of Polish internet culture with internally consistent consequences. |
| Starting age | 18 years old. |
| Initial categories | Just Chatting, FPS, MOBA, Chess, and Gambling. Additional categories may come later. |
| Launch language | Polish UI, with an architecture that permits later localization. |
| Save model | Server autosave after each settled step; three manual slots and save export/import for unranked careers only. |
| Leaderboard | Online global top 100 at launch, with a separate top-100 season after material balance changes and past seasons viewable. |
| Leaderboard identity | No account required; the player supplies a pseudonym when submitting a result. |
| Leaderboard integrity | The server owns all career state and calculates the score from C# `Domain` rules. |
| Leaderboard difficulty | One shared table with an explicit difficulty multiplier. |
| Gambling | Wagers use in-game currency only and affect stream outcomes. No real-money staking. |
| Adult creator work | Text-based choices, financial outcomes, and consequences without nudity. |
| Privacy and rule incidents | Use fictional data, fictional platform rules, and in-game consequences. |
| Full-career session target | A mixed manual/automatic 20-year career should take roughly 1–2 real-time hours. |
| Batch simulation | Resolve 4, 13, or 52 weeks at once, pausing for major events. |
| Batch action policy | The player sets a plan for between-stream actions over the simulated period. |
| Category specialization | A one-category career remains viable, with diminishing skill growth and an audience-boredom risk. |
| Starting backgrounds | Four distinct backgrounds, each with benefits and drawbacks. |
| Career path balance | Controversy has higher upside and greater risk; a low-drama path is steadier. Both must remain viable. |
| Adult creator side activity | Optional side business requiring weekly work and producing later income. |
| Connectivity | Playing requires a live connection. During an outage, show the last confirmed report without accepting new choices. |
| Technology stack | React 19.3.0/TypeScript/Vite client and a .NET 10 `Domain`/`Application`/`Infrastructure`/`WebApp` solution under `source/`; Wolverine HTTP/CQRS, EF Core/MSSQL, with all game rules in C# `Domain`. |
| Hosting | Ansible-managed k3s on `ovh`, with `ovh2` outside the cluster for backups; production at `polskistreamersimulator.pl`, invited-tester staging at `staging.polskistreamersimulator.pl`, and restricted release candidates at `rc.polskistreamersimulator.pl`. SQL Server and Vault use shared `database` and `vault` namespaces. Host capacity must be verified before deployment. |
| Deployment | Git Flow: feature/bugfix branches feed `develop`; passing `develop` pushes deploy staging. A `release/*` candidate is tested separately, then merged by PR to production `main`. `hotfix/*` branches from `main` and returns fixes to `develop`. Each passing `main` push deploys production automatically after tests and Ansible conformity checks. Docker Compose supports local development only. |
| Owner administration | Owner-only `/admin` panel using Tailwind/shadcn/ui, ASP.NET Core Identity cookie login, authenticator-app TOTP and one-time recovery codes. Manage leaderboard entries, events and balance constants through reviewed draft versions; create the owner account through a one-time server command. No player accounts. |
| PWA installation | Optional installable app; both it and the regular browser page require a connection to play. |
| Ranking backup | Daily SQL Server backups of careers and ranking on `ovh2`, retaining 30 daily and 12 monthly copies, with periodic restore tests and Discord failure alerts to the owner. Launch recovery-point and recovery-time targets: approximately 24 hours each. |
| Browser support | Current Chrome, Edge, Firefox, and Safari on desktop and phones. |
| Accessibility | Meet WCAG 2.2 AA for the playable interface at public launch. |
| Streamer Friendly mode | Optional and off by default; mask risky words and phrases with asterisks in all player-visible text, including the PDF diploma, without changing game mechanics or results. |
| Gameplay analytics | Disabled by default; enabled only by revocable player consent. |
| Leaderboard name | At most five characters, in arcade style. |
| Starting platform | One shared platform for every new career; other platforms become choices later. |
| Manual stream interaction | Turn-based situations represented through chat, with no response timer. |
| Sponsorship contracts | Both one-off deals and multi-week contracts. |
| Energy and burnout | A separate load/burnout system; rest consumes the week's main between-stream action. |
| Platform migration | Only a fraction of followers moves, determined by credibility and circumstances. |
| Background score normalization | Easier backgrounds reduce ranked score through a visible multiplier. |
| Anonymous careers | The browser stores a separate secret for each career, and the player can export that career's recovery key. Ranked careers cannot be rewound; no three-starts-per-day limit. |
| Unfinished-career retention | Delete an unfinished career after six months without a successful gameplay action; show the expiry rule before play. |
| Finished-career retention | Keep a finished career and its detailed history for 12 months; retain season leaderboard results indefinitely as separate minimal records. |
| Closed-season submissions | Accept valid ranked careers for 90 days after their season closes; announce a uniform extension if a ranking outage overlaps the deadline. |
| Autosave | Save every settled step on the server; three manual slots and export/import exist only for unranked careers. |
| Public launch scope | The first public release includes the full 20-year career and all confirmed systems. |
| Final-score weighting | Followers and wealth contribute at similar weights after normalization. |
| Platforms at launch | Three fictional streaming platforms with distinct audience profiles. |
| Character gender | Woman, man, or nonbinary; editable pronouns. The choice changes pronouns in player-facing text only. It does not change events, dialogue content, odds, rewards, or availability. |
| Visual direction | A streamer dashboard with live-chat presentation and satirical details. |
| Business model | Free to play, with no advertising inside gameplay. |
| Setting | Fictional platforms, brands, and personalities inspired by Polish streaming culture. |
| Audience rating | Adults only (18+), with a blocking warning dialog and explicit age confirmation before any game content appears. Gambling, adult-content work, privacy leaks and strong language are named in the warning. |
| Language of project documentation | English; project documentation lives in `docs/`. |
| Repository tooling | Use scoped root/frontend/backend `.gitignore` files, root `.gitattributes` and editor settings, .NET centralized build/package/SDK configuration, and the selected React ESLint/Prettier stack. |

## 3. Baseline from the Core Concept

These rules come from the original [`core-concept.md`](core-concept.md). The interview and linked specifications refine them where needed.

- Player creation includes gender and nickname; careers start at age 18.
- Followers and wealth in PLN contribute with similar normalized weights to the final score, with an achievement bonus. Drama changes the kinds of opportunities and events available but does not directly contribute to that score. Credibility is a separate attribute.
- The first full version contains Just Chatting, FPS, MOBA, Chess, and Gambling. Category skills improve through practice and can decline through disuse. Repeating one category can bore the audience. Streams can create viral clips through impressive play or entertaining failure.
- Decisions use random rolls adjusted by player attributes and can yield critical success or critical failure.
- Events can occur before a stream, during a stream, and between streams. Equipment, sponsors, other creators, viewers, and platform incidents can affect the outcome.
- A career lasts at most 20 in-game years. The player may retire earlier. A global ban covering all platforms or balance reaching PLN −1,000 ends the career; a ban on one platform permits migration.
- The ending includes a score, a templated career summary, and a locally exportable PDF diploma. An online global top-100 leaderboard is required at launch, separated into seasons after material balance changes.

## 4. Core Loop — Summary

1. Choose a character background and review the common starting platform, current audience, balance, drama, skills, equipment, and active opportunities.
2. Choose one main between-stream action, one primary stream category, and any eligible commercial commitment.
3. Resolve the stream manually through two or three live events, or use automatic resolution under the saved strategy.
4. Present a transparent weekly report: live viewers, follower change, subscriptions, donations, advertising, sponsorship income, costs, skill changes, credibility/drama changes, and their causes.
5. Autosave the settled week, then advance or end the career.
6. At milestones, unlock new equipment, opportunities, narrative arcs, and career goals.

The exact turn specification and proposed defaults are in [Gameplay and balance specification](gameplay-systems.md).

## 5. Proposed Rules Requiring Review

- **Pacing:** The linked specification proposes major-event interruption rules and a target mixed-mode play profile; these require prototype measurement.
- **Stream procedure:** Proposed exact action list, event rules, and report layout require playtesting.
- **Progression:** Four background profiles, starting audience and cash, skill scales, skill gain and decay, equipment tiers, category boredom, burnout and rest, viral clips, and milestones.
- **Economy, progression, platform, randomness, and scoring:** Initial rules and formulas are in [Gameplay and balance specification](gameplay-systems.md); numeric balance requires simulation and player tests.
- **Content and experience:** Initial event inventory, age presentation, 18+ themes, fictional naming, mobile flow, and accessibility are in [Content and experience specification](content-and-experience.md).
- **Technical design:** Online career storage, anonymous recovery, server-authoritative ranking, moderation, data retention, and deployment are in [Technical architecture](technical-architecture.md).
- **Quality and delivery:** Proposed measurable gates and release stages require review against actual capacity and hosting budget.

## 6. Decision Log

This is a chronological record. Later entries supersede earlier ones when they conflict; the current requirements are in Section 2 and the linked specifications.

| Date | Decision | Source |
| --- | --- | --- |
| 2026-10-12 | Target desktop and mobile browsers. | User interview |
| 2026-10-12 | One detailed stream per in-game week. | User interview |
| 2026-10-12 | Use fictional counterparts for platforms, brands, and personalities. | User interview |
| 2026-10-12 | Allow both manual and automatic stream resolution. | User interview |
| 2026-10-12 | A manual stream normally includes a category choice and two or three live events. | User interview |
| 2026-10-12 | Target an 18+ game with full mechanics for gambling, adult-content work, and privacy leaks. | User interview |
| 2026-10-12 | Offer starting backgrounds with distinct advantages and disadvantages. | User interview |
| 2026-10-12 | Start on one fictional platform and allow platform switches later. | User interview |
| 2026-10-12 | Let the player set a strategy that guides event choices during automatic streams. | User interview |
| 2026-10-12 | Track persistent followers and per-stream live viewers separately. | User interview |
| 2026-10-12 | Model subscriptions, donations, advertising, sponsorship income, and costs. | User interview |
| 2026-10-12 | Give each stream one primary category, with occasional mid-stream switches. | User interview |
| 2026-10-12 | Base the final score on followers and wealth, plus an achievement bonus. | User interview |
| 2026-10-12 | Track drama and credibility separately. | User interview |
| 2026-10-12 | Permit migration after a platform-specific ban; end the career after a global ban. | User interview |
| 2026-10-12 | Allow one main between-stream action per week. | User interview |
| 2026-10-12 | Show success probability before a choice and the roll with an explanation afterward. | User interview |
| 2026-10-12 | Provide easy and normal difficulty, with a score bonus for normal. | User interview |
| 2026-10-12 | Use a satirical Polish-internet tone with consistent consequences. | User interview |
| 2026-10-12 | Start the player at age 18. | User interview |
| 2026-10-12 | Launch with the five categories from the core concept; add more later. | User interview |
| 2026-10-12 | Launch with Polish UI and support later localization. | User interview |
| 2026-10-12 | Use multiple local save slots with export and import. | User interview |
| 2026-10-12 | Ship a global online top-100 leaderboard in the first release. | User interview |
| 2026-10-12 | Allow leaderboard submission without an account, using a pseudonym. | User interview |
| 2026-10-12 | Verify a submitted career on the server before accepting its score. | User interview |
| 2026-10-12 | Use one leaderboard for easy and normal modes with difficulty scaling. | User interview |
| 2026-10-12 | Confine gambling wagers to in-game currency and model their stream consequences. | User interview |
| 2026-10-12 | Present adult creator work through text, finances, and consequences without nudity. | User interview |
| 2026-10-12 | Use fictional data and platform rules for leaks and policy incidents. | User interview |
| 2026-10-12 | Target roughly 1–2 hours for a full 20-year career using both manual and automatic streams. | User interview |
| 2026-10-12 | Use a streamer-dashboard visual direction with chat and satirical details. | User interview |
| 2026-10-12 | Release the game for free without in-game advertising. | User interview |
| 2026-10-12 | Support 4-, 13-, and 52-week batch simulation, pausing for major events. | User interview |
| 2026-10-12 | Let the player set a between-stream action plan for batch simulation. | User interview |
| 2026-10-12 | Keep single-category specialization viable, with diminishing growth and boredom risk. | User interview |
| 2026-10-12 | Create four distinct starting backgrounds. | User interview |
| 2026-10-12 | Give the controversial path higher upside and greater risk. | User interview |
| 2026-10-12 | Model the fictional adult creator platform as an optional weekly side activity with later income. | User interview |
| 2026-10-12 | Support offline career play and saves after first load; require connectivity only for leaderboard access. | User interview |
| 2026-10-12 | Leave stack and hosting choice to the design proposal. | User interview |
| 2026-10-12 | Limit leaderboard pseudonyms to five characters. | User interview |
| 2026-10-12 | Give all careers the same starting platform and unlock platform choice later. | User interview |
| 2026-10-12 | Present manual streams as untimed, turn-based chat situations. | User interview |
| 2026-10-12 | Offer both one-off and multi-week sponsorship contracts. | User interview |
| 2026-10-12 | Track energy and burnout separately; resting uses the weekly main action. | User interview |
| 2026-10-12 | Migrate only a context-dependent fraction of followers on platform switch. | User interview |
| 2026-10-12 | Apply visible score multipliers to easier starting backgrounds. | User interview |
| 2026-10-12 | Permit offline ranked runs only with a pre-issued signed start; other offline runs are unranked. | User interview |
| 2026-10-12 | Autosave after every completed week and provide manual slots. | User interview |
| 2026-10-12 | Include the full 20-year career and all confirmed systems in the first public release. | User interview |
| 2026-10-12 | Give followers and wealth similar weight in the final score after normalization. | User interview |
| 2026-10-12 | Launch with three distinct fictional streaming platforms. | User interview |
| 2026-10-12 | Offer woman, man, and nonbinary character options with editable pronouns. Gender affects pronouns in text only; the earlier idea of gender-dependent events or dialogue is superseded. | User correction |
| 2026-10-12 | Show an 18+ warning screen and require age confirmation before play. | User interview |
| 2026-10-12 | Use a 45% followers, 45% wealth, 10% achievements score split as the starting balance model. | User interview |
| 2026-10-12 | Accept the limits of server replay for offline ranked careers and limit issued ranked starts. | User interview |
| 2026-10-12 | Use Czatnia, PulsLive, and Nurt as the three fictional platform names. | User interview |
| 2026-10-12 | Use the four proposed backgrounds as the test baseline; tune their numbers through playtesting. | User interview |
| 2026-10-12 | Start a separate leaderboard season after a material balance change and retain past season tables. | User interview |
| 2026-10-12 | Include a locally generated PDF career diploma in the first public release. | User interview |
| 2026-10-12 | Choose React and ASP.NET Core for the app, with a shared TypeScript simulation in the browser and a private Node replay worker. | User technical interview |
| 2026-10-12 | Host the leaderboard services on an owned VPS with Docker Compose. | User technical interview |
| 2026-10-12 | Finish started careers on their original ruleset after updates. | User technical interview |
| 2026-10-12 | Gather gameplay analytics only after revocable player consent. | User technical interview |
| 2026-10-12 | Support current Chrome, Edge, Firefox, and Safari on desktop and phones. | User technical interview |
| 2026-10-12 | Use the existing VPS for hosting, subject to capacity and service inventory. | User technical interview |
| 2026-10-12 | Run automated tests and staging before a manually approved production deployment. | User technical interview |
| 2026-10-12 | Moderate the leaderboard through private administrative tools, without a public admin panel. | User technical interview |
| 2026-10-12 | Provide three manual save slots alongside weekly autosaves. | User technical interview |
| 2026-10-12 | Limit issued ranked starts to three per rolling 24 hours per abuse-control key. | User technical interview |
| 2026-10-12 | Accept old-season ranked submissions for 90 days after season close. | User technical interview |
| 2026-10-12 | Use Vite for the React client and Microsoft SQL Server rather than PostgreSQL for online ranking data. | User technical interview |
| 2026-10-12 | Offer optional PWA installation while keeping normal browser play complete. | User technical interview |
| 2026-10-12 | Make a daily SQL Server backup outside the VPS and test restores periodically. | User technical interview |
| 2026-10-12 | Run a new SQL Server instance for the game on the existing VPS, subject to resource inventory. | User technical interview |
| 2026-10-12 | Identify the deployment VPS by SSH alias `ovh`; capacity and existing services remain unverified because the SSH handshake timed out. | User technical interview and read-only inventory attempt |
| 2026-10-12 | Use `polskistreamersimulator.pl` as the production domain and the separate VPS at SSH alias `ovh2` for off-host database backups; defer further VPS checks. | User technical interview |
| 2026-10-12 | Use `staging.polskistreamersimulator.pl` for staging, retain 30 daily and 12 monthly backups on `ovh2`, and alert the owner through Discord when backup or ranking fails. | User technical interview |
| 2026-10-12 | Keep offline play available during ranking outages and queue completed ranked submissions locally for later retry; restrict staging to invited testers; accept an approximately 24-hour ranking recovery point at launch. | User technical interview |
| 2026-10-12 | Aim to restore ranking service within 24 hours, extend a season submission deadline for everyone if an outage overlaps it, and restrict leaderboard moderation to the owner. | User technical interview |
| 2026-10-12 | Put all code under `source/`; use four .NET projects (`Domain`, `Application`, `Infrastructure`, `WebApp`) with Wolverine HTTP and in-process CQRS, while keeping React separate. | User technical interview |
| 2026-10-12 | Add an owner-only MUI admin route at launch, with ASP.NET Core Identity cookie login and a second factor; this supersedes the earlier CLI-only launch proposal. | User technical interview |
| 2026-10-12 | Use Orval/OpenAPI, TanStack Query + Axios, TanStack Router, React Hook Form + Zod, Tailwind + shadcn/ui, Zustand, i18next, Mapster, FluentValidation, EF Core/MSSQL, and Serilog console/SQL logging. | User technical interview |
| 2026-10-12 | Use Vitest, Testing Library, Playwright, xUnit and Testcontainers/MSSQL; do not adopt Wolverine durable queues at launch. | User technical interview |
| 2026-10-12 | Remove MUI from the admin route; use Tailwind/shadcn/ui there as in the game, with optional Recharts. Choose .NET 10, React 19.3.0, Vite and `pdf-lib`. | User correction |
| 2026-10-12 | Move all gameplay rules into C# `Domain` and require online play. This supersedes the TypeScript simulation, Node replay worker, offline saves and signed offline starts. | User correction |
| 2026-10-12 | Keep anonymous browser-held career secrets with exportable recovery keys; retain three manual slots/export-import for unranked careers only; remove the three-ranked-starts-per-day limit. | User technical interview |
| 2026-10-12 | Accept approximately 24 hours of possible career-progress loss after a severe database failure; delete unfinished careers after six months without activity; use a separate recovery key for each career. | User technical interview |
| 2026-10-12 | Retain finished careers and their detailed history for 12 months after ending, while keeping season leaderboard results indefinitely as separate records. | User technical interview |
| 2026-10-12 | Replace the Docker Compose deployment proposal with Ansible-managed k3s; run reusable SQL Server and HashiCorp Vault services in separate `database` and `vault` namespaces. | User technical direction |
| 2026-10-12 | Work on `develop`, release automatically from pushes to `master` through tests, Ansible checks and Kubernetes deployment; use Docker Compose to bring up the project locally. This supersedes manual approval inside the production workflow. | User technical direction |
| 2026-10-12 | Promote `develop` to `master` by tested PR merge, deploy passing `develop` commits to staging automatically, and stop a release if Ansible detects infrastructure drift instead of repairing it in the app workflow. | User technical interview |
| 2026-10-12 | Adopt Git Flow with production `main`, integration `develop`, `feature/*`, `bugfix/*`, `release/*` and `hotfix/*`; test release candidates in a separate environment. A hotfix PR targets `main` directly and is then reconciled into `develop` and any open release. This supersedes the earlier `master` naming and direct `develop`→production promotion. | User correction and technical interview |
| 2026-10-12 | Add root, TypeScript and .NET `.gitignore` files; `.gitattributes`; editor rules for backend, docs and scripts; .NET `Directory.Build.props`, `Directory.Packages.props` and `global.json`; and the specified React ESLint/Prettier package set. | User tooling direction |
| 2026-10-12 | Keep top-level `docs/`, `.github/workflows/`, `source/` for backend/frontend, `k8s/` for infrastructure, and optional `scripts/`. Move the planned Ansible/Helm assets out of `source/`; keep local Compose at repository root. | User repository-structure correction |
| 2026-10-12 | Use TDD for production behavior and bug fixes: write a failing test first, implement the behavior, then refactor. Enforce at least 80% line coverage separately for backend and frontend in CI. | User development-quality decision |
| 2026-10-12 | Keep `ovh2` outside k3s as the independent backup server; use `rc.polskistreamersimulator.pl` for restricted release candidates; manage shared services and the game application with Helm. | User infrastructure interview |
| 2026-10-12 | Accept the current gameplay numbers as prototype defaults for simulation and player testing, not as final ranked balance; decide the launch event-count floor after estimating writing capacity; require WCAG 2.2 AA at public launch. | User product-scope interview |
| 2026-10-12 | Auto-unseal Vault through an external KMS, keep separate encrypted Vault snapshots on `ovh2`, and use authenticator-app TOTP with one-time recovery codes for owner administration. | User operations interview |
| 2026-10-12 | Add an optional, default-off Streamer Friendly text-masking mode for all player-visible text and the PDF diploma; keep event selection, choices, odds and scoring identical. | User feature decision and follow-up |
| 2026-10-12 | Present the 18+ content warning as a blocking entry dialog with explicit adult confirmation or exit before showing game content. | User interface clarification |
| 2026-10-12 | Defer the external KMS vendor until accounts and costs are compared; retain 30 daily and 12 monthly encrypted Vault snapshots on `ovh2`; create the sole owner account using a one-time server command. | User operations follow-up |
| 2026-10-12 | Let the owner add, edit and remove events and change balance constants from `/admin`; publish validated, versioned changes while existing careers retain their original ruleset and material balance changes start a new leaderboard season. | User admin-scope addition |
| 2026-10-12 | Add local pre-commit formatting, fixable lint and fast unit tests; rerun tests on `develop`; use SonarQube Cloud quality checks on PRs to `develop` and `main` while preserving Git Flow. | User development-workflow interview |
| 2026-10-12 | Clarify pre-commit auto-fixes: `dotnet format` applies to the backend and ESLint `--fix` to the frontend; check Prettier in CI. | User tooling correction |
| 2026-10-12 | Build the M1 simulation prototype as a minimal mechanics core: five categories as data, actions Rest/Train/Improve production/Manage community/No action, one platform (Czatnia), four backgrounds, two or three synthetic live events per manual week, the 45/45/10 score and endings Completed, Insolvency and Early retirement. Sponsors, gambling, adult side business, Network/collaborate, migration and bans move to milestone 3. This supersedes the "one category" wording of milestone 1 (see [TD-75](technical-decisions.md)). | Owner decision (M1 brainstorming) |
| 2026-10-13 | For the M1 simulation ruleset `m1-prototype.1`, fill unspecified numbers (equipment prices and upkeep tiers, community-management effects, conversion, churn and income yields, missing background values), allow only `Rest` after energy 0 or burnout 100 instead of a mandatory recovery choice, use a minimal four-achievement set and five simulation policies (see [gameplay §10](gameplay-systems.md#10-m1-prototype-ruleset-m1-prototype1) and [TD-83](technical-decisions.md)). Pending owner review before any ranked season. | Agent proposal |
| 2026-10-13 | Propose a content-capacity measurement method, effort model and launch floor of 173 templates (130 live events by phase and category, 43 other), below the 212-template planning estimate; the floor stays pending until the owner measures authoring/QA time and approves it ([content capacity estimate](content-capacity-estimate.md), [#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7)). | Agent proposal |
| 2026-10-14 | The M2 slice creates only unranked careers; its 13-week batch plays one fixed strategy (`Balanced`) and shows the reason for each decision; player-configured strategies, mandatory crises and the ranked choice arrive later. | Owner decision (M2-B brainstorming, TD-90) |
| 2026-10-15 | Offer three selectable visual themes — a cheap OBS overlay from old streams (default), Windows 98 shareware and a 2005 fan site — with a deliberately amateur look and sound usability; the theme changes presentation only. | Owner decision (M2-C brainstorming) |
| 2026-10-15 | Keep the M2 slice on the M1 content; the representative events for each adult theme and the tone check with players move to M3, where the full content schema arrives. | Owner decision (M2-C brainstorming) |
| 2026-10-16 | Move the content-capacity measurement and launch-floor sign-off ([#7](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/7)) from M1 to M3, where the full content schema and content production start. Previously M1 waited for it; the M2 release now closes M1 without it. | Owner decision (M2 release) |
| 2026-10-16 | Build M3 in four sub-projects (rules and content foundation; economy and risk; automation and endings; career screens). New careers move to the M3 ruleset while existing careers keep their M1 rules. The agent writes about 40 representative Polish events in the launch tone; launch content stays in M5. Nonbinary characters use gender-neutral Polish forms (`zrobiłoś`), not underscores or asterisks, so screen readers read them correctly; the forms live in ICU `select` messages in the stored content (TD-109–TD-111). | Owner decision (M3-A brainstorming) |
| 2026-10-16 | In the M3 ruleset, energy 0 or burnout 100 opens a mandatory recovery crisis instead of M1's forced rest; burnout from 60 lowers risky odds visibly; `Network/collaborate` rolls with visible odds; categories get distinct reach, volatility, churn, income and reputation profiles, mastery from skill 70 and viral clips with published chances ([M3-A design](superpowers/specs/2026-10-16-m3a-rules-and-content-foundation-design.md) §5–§7). Numbers are proposals tuned by simulation. | Agent proposal (M3-A design), approved with the written spec by the owner |
| 2026-10-17 | M3-A tuning rulings from the baseline simulation ([report](reports/m3a-baseline-simulation.md)): The rivalry arc fix: `rivalry-callout` is rare (cooldown 52 weeks, weight 1) and only a failed riposte schedules the rivalry crisis; previously both outcomes did, which doubled the mandatory interruptions. | Agent proposal, pending owner review |
| 2026-10-17 | M3-A tuning rulings from the baseline simulation ([report](reports/m3a-baseline-simulation.md)): Tuned category profiles: Chess is the calm, credible, loyal path (low reach, high conversion, low churn, less drama); Gambling is the high-drama, high-variance path with the most growth; MOBA is slightly heated and sticky; Just Chatting leads donations and FPS leads ads. Previously the first proposal in the M3-A design §7.1. | Agent proposal, pending owner review |
| 2026-10-17 | M3-A tuning rulings from the baseline simulation ([report](reports/m3a-baseline-simulation.md)): The recovery crisis choice *take the week off* leads to a short stream (half fatigue, ×0.7 audience) after a week of rest. Previously the week-off response only rested. | Agent proposal, pending owner review |
| 2026-10-17 | M3-A tuning rulings from the baseline simulation ([report](reports/m3a-baseline-simulation.md)): `community-burnout-poll/cut-hours` shortens the streams for two weeks (lighter workload) instead of changing only burnout. | Agent proposal, pending owner review |
| 2026-10-15 | If career options cannot be loaded, show a Polish error and retry action on character creation. Previously the screen kept showing its loading message after a failed request. The retry prevents a dead end; the form still uses only server-supplied profile numbers (TD-104). No open product question; owner validation pending. | Agent proposal/default (M2-C PR 2) |
| 2026-10-15 | If the browser cannot save the access secret, explain the problem and do not create the server career. Previously creation preceded the browser write, so a denied write could leave a career inaccessible after reload. Recovery keys arrive in M4; a storage failure between the check and response still needs that recovery flow (TD-105). Owner validation pending. | Agent proposal/default (M2-C PR 2) |

## 7. Next Step

Review the linked implementation proposals, resolve the remaining product decisions listed in [Validation and delivery plan](validation-and-delivery.md), and calibrate numeric values against prototype playtests.
