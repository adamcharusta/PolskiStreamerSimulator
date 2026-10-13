# M2-A Delivery Foundation — Design

Date: 2026-10-13
Status: **Approved design (owner, 2026-10-13 brainstorming session)**. The implementation plan is not written yet.
Milestone: [M2 — Vertical slice](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/2), sub-project A, delivering the CI and Lefthook parts of [#14](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/14) and all of [#15](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/15).

## 1. Intent

M2 must give players a working slice: an anonymous career, one manual week, a 13-week batch, server autosave and reload, started locally with one command. Its exit gate is player validation. Sub-project A comes first so that all later M2 work reaches `develop` through the documented PR flow with automated gates ([TD-72](../../technical-decisions.md)). The alternative was another local merge, as with M1 ([TD-84](../../technical-decisions.md)). A also completes the task deferred by [TD-81](../../technical-decisions.md).

Success means three things. Every PR into `develop` or `main` runs required backend, SonarQube Cloud and branch-policy checks. Direct pushes and force pushes to `main` and `develop` are rejected. A local pre-commit hook gives the same feedback before code leaves the machine.

### Owner decisions taken in this session

| Topic | Decision |
| --- | --- |
| M2 decomposition | Split M2 into three sub-projects, each with its own spec, plan and implementation: **A** delivery foundation (#14 CI/Lefthook/Sonar, #15); **B** backend slice (#9, #10, #13 server side); **C** frontend slice (#8, #11, #12, #13 UI, #14 Compose and smoke test). Order: A → B → C. |
| SonarQube Cloud | Include it in A as a required check. The owner creates the organization and project and adds the `SONAR_TOKEN` secret. |
| Emergency bypass | Repository admins may bypass the rulesets **only through a pull request** (`bypass_mode: pull_request`). Direct pushes stay blocked for everyone. |
| CI layout | One workflow, `.github/workflows/ci.yml`, with stable job names `backend`, `sonar` and `pr-policy`. No path filters. |
| Hook installation | Lefthook from a minimal root `package.json`, pinned through `package-lock.json`. |

### Agent proposals accepted with the design

- Pin third-party actions to full commit SHAs with a version comment.
- Enforce the backend coverage gate with coverlet.MTP's threshold option.
- Use only merge commits on `main` and `develop`, and delete merged branches automatically.
- Reconcile releases and hotfixes through a PR from `main` into `develop`.
- Mark integration tests with the trait `Category=Integration`.

## 2. Scope

**In scope**

- `.github/workflows/ci.yml` and the coverage gate.
- SonarQube Cloud analysis of the C# backend, with a pinned SonarScanner for .NET in `source/backend/.config/dotnet-tools.json`.
- GitHub repository rulesets for `main` and `develop`, and the repository merge settings.
- Root `package.json`, `package-lock.json` and `lefthook.yml`, plus a `/node_modules/` entry in the root `.gitignore`.
- Decision records and documentation updates (§8).

**Out of scope**

- Frontend CI jobs and frontend lint hooks (sub-project C).
- The SQL Server/Testcontainers integration job (sub-project B).
- Docker Compose and the local smoke test (C).
- Deployment to staging, release-candidate or production environments (M6, [#42](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/42)).
- Dependabot.
- Tag protection.

## 3. Rollout order

Steps marked **[external]** change GitHub state, and the agent asks the owner for an explicit yes immediately before each one. Steps marked **[owner]** are done by the owner.

1. On `feature/m2a-delivery-foundation` (from `develop`), add the workflow, tool manifest, Lefthook files and documentation. Verify locally: `actionlint`, the CI commands run locally, and the hook tested on real commits (§7).
2. **[external]** Push `develop` for the first time, then the feature branch. Open the PR into `develop`. CI runs; `sonar` fails until step 3.
3. **[owner]** Create the SonarQube Cloud organization and project by importing the GitHub repository, and disable *Automatic Analysis*. Mark `develop` as a long-lived branch (TD-89 later made it the main branch). Add the repository secret `SONAR_TOKEN`. The agent supplies exact steps and adjusts the organization and project key constants if Sonar assigns different ones. Then re-run CI: through `workflow_dispatch` on `develop` for the first branch analysis, and on the PR.
4. **[external]** Open three probe PRs and close them unmerged:
   - a PR with a formatting violation, expecting a red `backend`;
   - a PR that lowers coverage below 80%, expecting a red `backend`;
   - a PR from a `feature/*` branch into `main`, expecting a red `pr-policy`.
5. **[external]** Create the rulesets (§5) once the three checks have reported under their final names. Apply the repository merge settings. Verify with the rules API.
6. **[external]** Merge the sub-project A PR into `develop` through the ruleset. This is the first merge under TD-72.

## 4. CI workflow (`.github/workflows/ci.yml`)

**Triggers**

- `pull_request` targeting `develop` or `main`.
- `push` to `develop` or `main`.
- `workflow_dispatch`.

**Common settings**

- Concurrency group per workflow and ref. Superseded runs are cancelled only for `pull_request` events, never for pushes to `develop` or `main`.
- Default token permissions `contents: read`.
- Runner `ubuntu-latest`, timeout 15 minutes per job.
- Every third-party action is pinned to a full commit SHA with its version tag in a comment.
- Backend steps run with `working-directory: source/backend` ([TD-55](../../technical-decisions.md)).

### Job `backend` (required)

1. Check out the repository. Set up .NET from `source/backend/global.json`, with NuGet caching keyed on `Directory.Packages.props` and the project files.
2. `dotnet restore PolskiStreamerSimulator.slnx`.
3. `dotnet build PolskiStreamerSimulator.slnx -c Release --no-restore`. Warnings already fail the build through `Directory.Build.props`.
4. `dotnet format PolskiStreamerSimulator.slnx --verify-no-changes --no-restore`.
5. `dotnet test PolskiStreamerSimulator.slnx -c Release --no-build --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total`. The test run fails when total backend line coverage is below 80%. The filters come from `Tests/testconfig.json`: tests, the batch tool entry point and generated code are excluded.
6. Upload the Cobertura report as a workflow artifact. Write the total line coverage to the job summary.

### Job `sonar` (required)

1. Check out with `fetch-depth: 0` so SonarQube Cloud can detect new code. Set up .NET as in `backend`, then `dotnet tool restore`.
2. Run `dotnet sonarscanner begin` with these parameters:
   - the organization and project key, as constants in the workflow (defaults `adamcharusta` and `adamcharusta_PolskiStreamerSimulator`);
   - `sonar.host.url=https://sonarcloud.io` and the token from `secrets.SONAR_TOKEN`;
   - `sonar.cs.opencover.reportsPaths` pointing at the coverage output;
   - `sonar.qualitygate.wait=true`;
   - coverage exclusions matching coverlet (`**/Tools/BatchSimulator/Program.cs`, generated `*.g.cs`), and analysis exclusions for `content/**`, `docs/**` and `**/Tests/Golden/**`.
3. Build with `-c Release`.
4. Test with `--coverlet --coverlet-output-format opencover`. SonarQube Cloud imports C# coverage in OpenCover format, not Cobertura.
5. Run `dotnet sonarscanner end`. A failed quality gate fails the job.

Settings and constraints:

- The quality gate is the default **Sonar way**. Its conditions apply to new code, including coverage ≥ 80% and duplication ≤ 3%.
- Long-lived branches are `main`, `develop` and `release/*`.
- *Amended by TD-89 (2026-10-13):* the Free plan analyses only the main branch, so `develop` is the SonarQube Cloud main branch and the job runs only on PRs and pushes to `develop`.
- PRs from forks and from bots without access to secrets cannot pass `sonar`. Admins merge such PRs through the PR-only bypass after review.

### Job `pr-policy` (required, `pull_request` events only)

- For a PR into `main`, the job passes only when the head branch matches `release/*` or `hotfix/*`. Otherwise it fails with a message naming the allowed sources.
- For a PR into `develop` it always passes. It still runs because a required check that never reports blocks merging.
- On push events it is skipped.
- It is a short Bash step with no third-party action.

### Later extensions

- B adds `backend-integration` for SQL Server through Testcontainers, filtered by `Category=Integration`. Integration tests are excluded from `backend` from then on.
- C adds `frontend`: lint, typecheck, Vitest with its own 80% line-coverage gate, Prettier and the Orval drift check. C also extends `sonar` to the TypeScript source with Vitest LCOV coverage in the same SonarQube Cloud project (TD-73).
- Each new job name is added to the rulesets' required checks in the same sub-project.

## 5. Repository rules

### Ruleset `main` (target `refs/heads/main`, enforcement active)

- `deletion` and `non_fast_forward`: no deleting and no force pushes.
- `pull_request`: 0 required approvals. The owner is the sole maintainer, and GitHub does not allow self-approval. `allowed_merge_methods: ["merge"]`.
- `required_status_checks`: `backend`, `sonar` and `pr-policy`, with **strict** (up-to-date branch) enabled, so the merged tree is the tested tree ([development and delivery](../../development-and-delivery.md)).
- Bypass actor: the Repository admin role in `pull_request` mode.

### Ruleset `develop` (target `refs/heads/develop`)

Same rules as `main`, except the up-to-date requirement is **off**. Every push to `develop` re-runs CI on the exact commit, and with a single author the strict mode mostly adds rebases.

### Repository settings

- Disable squash and rebase merging, which keeps the settings consistent with the rulesets.
- Enable `delete_branch_on_merge`.
- Merge commits use GitHub's default title format.

### Release and hotfix reconciliation

- Release flow: `release/*` merges into `main` by PR. The branch is then deleted automatically. A follow-up PR from **`main` into `develop`** brings the exact released tree back, and the same goes into any active `release/*`. `pr-policy` allows it.
- Hotfix flow: `hotfix/*` branches from `main`, merges into `main` by PR, then follows the same reconciliation.
- Conflicts are resolved in the reconciliation PR, and its checks must pass.

## 6. Local pre-commit hook (Lefthook)

**Installation**

- A root `package.json` (`"private": true`) holds a single exact-version devDependency on `lefthook`, at the newest stable version when implemented (2.2.1 at design time), together with `package-lock.json`.
- Running `npm install` once in the repository root installs Lefthook, and its post-install script registers the Git hooks.
- `/node_modules/` is added to the root `.gitignore`.
- CI does not install it.

**`lefthook.yml` `pre-commit`**: commands run in order (piped). A failure stops the remaining commands and rejects the commit.

1. `backend-format`
   - Runs when staged files match `source/backend/**`.
   - Executes `dotnet format PolskiStreamerSimulator.slnx --include <staged files>` from `source/backend`.
   - If the formatter changes any file, the commit is rejected. The fixes stay unstaged in the working tree for the developer to review and stage; nothing is staged automatically ([development and delivery](../../development-and-delivery.md)).
2. `backend-tests`
   - Runs when staged files match `source/backend/**` or `content/**`. Content affects the golden full-career hash.
   - Executes `dotnet test PolskiStreamerSimulator.slnx --filter-not-trait "Category=Integration"` from `source/backend`. No integration tests exist yet; B adds them with this trait.
3. Frontend linting (`npm run lint -- --fix`) is added in C under the same rules.

**Behaviour**

- Documentation-only commits trigger no command.
- `git commit --no-verify` remains the documented emergency escape; CI re-checks everything.

## 7. Verification

These are configuration and workflow changes rather than game behaviour, so the evidence is real runs, not unit tests. The `pr-policy` logic is checked by an actual failing PR.

1. `actionlint` reports no problems for `ci.yml`.
2. Hook behaviour on real commits in this branch:
   - a staged C# formatting violation is rejected and the fix is left unstaged;
   - a clean backend commit runs format and tests and succeeds;
   - a documentation-only commit runs nothing.
3. The sub-project A PR shows green `backend`, `sonar` and `pr-policy`, with the coverage value in the job summary.
4. The three probe PRs (§3 step 4) fail in the expected job and cannot be merged. They are closed without merging, and their branches are deleted.
5. `gh api repos/adamcharusta/PolskiStreamerSimulator/rules/branches/main` and `.../develop` return the configured rules.
6. The sub-project A PR merges into `develop` through the ruleset.

No test pushes directly to `main`. A misconfigured ruleset would let that commit land on the production branch, where it could not be removed without a force push.

## 8. Decision records and documentation

[`technical-decisions.md`](../../technical-decisions.md), all dated 2026-10-13:

- **TD-85** (owner decision): M2 decomposition into A, B and C, in the order A → B → C.
- **TD-86** (owner decision): a single `ci.yml` with required `backend`, `sonar` and `pr-policy`; CI-based SonarQube Cloud analysis with the Sonar way gate. SHA-pinned actions and the coverlet threshold are agent proposals.
- **TD-87** (owner decision): rulesets for `main` and `develop`, with PR-only admin bypass, merge commits only, automatic branch deletion and reconciliation through a PR from `main` into `develop`. This resolves the open bypass policy in [development and delivery](../../development-and-delivery.md).
- **TD-88** (agent proposal accepted by the owner): Lefthook from the root `package.json`, the hook behaviour in §6, and the `Category=Integration` trait.
- TD-81 is annotated as delivered by TD-86 and TD-88.

Other updates:

- [`development-and-delivery.md`](../../development-and-delivery.md):
  - the implemented rules and the reconciliation procedure;
  - what CI runs now and what B, C and M6 add;
  - hook installation;
  - replace the outdated statement that `develop` does not exist yet.
- [`repository-standards.md`](../../repository-standards.md): the root `package.json`, `package-lock.json`, `lefthook.yml`, `.github/workflows/ci.yml` and `source/backend/.config/dotnet-tools.json`.
- README: hook installation, and how to run the CI checks locally.
- [`requirements-traceability.md`](../../requirements-traceability.md):
  - REQ-26 and REQ-27 point to #14 and #15 with status `In progress (M2)`;
  - after the rulesets and CI are verified, the delivered parts are marked implemented.
- GitHub issue #14: the agent proposes updated wording that records the A/C split. It edits the issue only after the owner approves.

## 9. Risks

- **SonarQube Cloud is an external service.** An outage or a mis-set quality gate blocks merges. Admins can merge the affected PR through the PR-only bypass, and GitHub records the bypass.
- **SHA-pinned actions need manual updates** until Dependabot is introduced.
- **The pre-commit hook adds about 30–40 seconds** to backend commits, mostly the build. If that proves too slow, the test step can move to a pre-push hook; that would be a recorded decision.
