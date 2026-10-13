# M2-A Delivery Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every PR into `develop` or `main` runs required backend, SonarQube Cloud and branch-policy checks. Rulesets block direct and force pushes. A Lefthook pre-commit hook gives the same backend feedback locally.

**Architecture:** One GitHub Actions workflow (`.github/workflows/ci.yml`) with three stable, always-reporting jobs: `backend`, `sonar` and `pr-policy`. Repository rulesets are versioned as JSON under `.github/rulesets/` and applied through the GitHub API. Lefthook is pinned in a tooling-only root `package.json`. Local configuration and docs are built and verified first (Tasks 1–6). The GitHub-side rollout follows (Tasks 7–11); each of those steps needs the owner's explicit yes at the moment it runs.

**Tech Stack:** GitHub Actions (`ubuntu-latest`), .NET 10 SDK via `actions/setup-dotnet`, Microsoft.Testing.Platform with coverlet.MTP, SonarScanner for .NET 11.3.0 (local dotnet tool), SonarQube Cloud, GitHub repository rulesets, Lefthook 2.2.1 (npm), actionlint 1.7.12, Bash.

**Spec:** `docs/superpowers/specs/2026-10-13-m2a-delivery-foundation-design.md`

## Global Constraints

- Work on branch `feature/m2a-delivery-foundation`, which already holds the spec commit `7367dfb`. `develop` is local only until Task 7.
- Run every `dotnet` command from `source/backend` (TD-55). The host is Windows with Git Bash; CI is Linux.
- Pin third-party actions to these exact commits:
  - `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`
  - `actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0`
  - `actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0`
  - `actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2`
- Use these exact names in the workflow and the rulesets:
  - required checks `backend`, `sonar`, `pr-policy`;
  - SonarQube Cloud organization `adamcharusta` and project key `adamcharusta_PolskiStreamerSimulator`, until Task 8 confirms or corrects them.
- The coverage gate is 80% total backend line coverage: `--coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total`.
- Lefthook `2.2.1` (exact). SonarScanner for .NET `dotnet-sonarscanner` `11.3.0`.
- Documentation and decision records are in English. Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` and uses the repository's configured git identity.
- **External actions** (pushes, PRs, GitHub settings, issue edits) need an explicit "yes" from the owner immediately before each one; Tasks 7–11 mark the steps. The agent never types or reads the `SONAR_TOKEN` value; the owner sets it with `! gh secret set SONAR_TOKEN`.
- Never push directly to `main`.

## Review Focus

- **Branch names in `pr-policy`:** a malicious or unusual head branch such as ``feature/$(touch pwned)`` must be treated as data, never executed. Pinned in Task 2's self-test, case `injection`.
- **Reconciliation PRs from `main` into `develop`** must pass `pr-policy`, or releases can never be reconciled. Pinned in Task 2, case `main-into-develop`.
- **A missing coverage report:** if the OpenCover file is not produced where SonarQube Cloud looks, coverage silently becomes 0%. The `sonar` job must fail explicitly instead. Pinned in Task 3 (explicit existence check) and verified in Task 8.
- **Ruleset check names must match the job names exactly.** A mismatch blocks every merge forever. Verified in Task 10 by the PR's merge state turning `CLEAN`.
- **A missing `SONAR_TOKEN`** must fail with a clear, actionable message rather than a scanner stack trace. Pinned in Task 3 (guard step) and observed in Task 7.

---

### Task 1: Coverage formats, CI gate commands and SonarScanner manifest

**Files:**
- Modify: `source/backend/Tests/testconfig.json`
- Create: `source/backend/.config/dotnet-tools.json`

**Interfaces:**
- Produces:
  - every `dotnet test … --coverlet` run writes `coverage.cobertura.<timestamp>.xml` and `coverage.opencover.<timestamp>.xml` into the `--results-directory`;
  - the local tool command `dotnet sonarscanner` (from `source/backend`);
  - the exact CI gate command line from Global Constraints.

- [ ] **Step 1: Prove the gate fails when the threshold is not met.** Run from `source/backend`:

```bash
dotnet test PolskiStreamerSimulator.slnx -c Release --results-directory "$TEMP/m2a-gate" --coverlet --coverlet-threshold 99 --coverlet-threshold-type line --coverlet-threshold-stat total; echo "exit=$?"
```

Expected:
- all 256 tests pass;
- the output reports total line coverage below the 99% threshold;
- `exit=` is non-zero.

If coverlet.MTP rejects `total` (case), retry with `Total` and use the accepted spelling everywhere, then record a ruling.

- [ ] **Step 2: Prove the gate passes at 80%.**

```bash
dotnet test PolskiStreamerSimulator.slnx -c Release --results-directory "$TEMP/m2a-gate80" --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total; echo "exit=$?"
ls "$TEMP/m2a-gate80"
```

Expected: `exit=0`, and the directory contains `coverage.cobertura.*.xml` only (no OpenCover yet).

- [ ] **Step 3: Write both report formats.** Replace `source/backend/Tests/testconfig.json` with:

```json
{
  "platformOptions": {
    "Coverlet": {
      "include": "[PolskiStreamerSimulator.*]*",
      "exclude": "[PolskiStreamerSimulator.Tests]*,[PolskiStreamerSimulator.BatchSimulator]*Program",
      "excludeByAttribute": "GeneratedCode,GeneratedCodeAttribute,ExcludeFromCodeCoverage,ExcludeFromCodeCoverageAttribute,CompilerGeneratedAttribute",
      "excludeByFile": "**/*.g.cs",
      "format": "cobertura,opencover",
      "includeTestAssembly": false
    }
  }
}
```

- [ ] **Step 4: Verify both reports appear.**

```bash
dotnet test PolskiStreamerSimulator.slnx -c Release --results-directory "$TEMP/m2a-both" --coverlet; echo "exit=$?"; ls "$TEMP/m2a-both"
```

Expected: `exit=0`. The listing shows one `coverage.cobertura.*.xml` and one `coverage.opencover.*.xml`.

- [ ] **Step 5: Add the SonarScanner tool manifest.**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-sonarscanner --version 11.3.0
```

Expected: `source/backend/.config/dotnet-tools.json` exists and lists `"dotnet-sonarscanner": { "version": "11.3.0", "commands": ["dotnet-sonarscanner"] … }`.

- [ ] **Step 6: Verify the tool restores and starts.**

```bash
dotnet tool restore && dotnet sonarscanner /? | head -n 5
```

Expected: the restore succeeds and the scanner prints its usage banner (`SonarScanner for .NET 11.3…`).

- [ ] **Step 7: Run the full suite and format check, then commit.**

```bash
dotnet test PolskiStreamerSimulator.slnx && dotnet format PolskiStreamerSimulator.slnx --verify-no-changes
cd ../.. && git add source/backend/Tests/testconfig.json source/backend/.config/dotnet-tools.json
git commit -m "build: emit OpenCover coverage and pin SonarScanner for .NET" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Expected: 256 tests pass, format exits 0, and the commit succeeds.

---

### Task 2: PR source-branch policy script with self-test

**Files:**
- Create: `.github/scripts/check-pr-source.sh`
- Test: `.github/scripts/check-pr-source.test.sh`

**Interfaces:**
- Produces: `bash .github/scripts/check-pr-source.sh` reads `BASE_REF` and `HEAD_REF` from the environment. It exits 0 when the PR is allowed and 1 with a `::error::` annotation when it is not. `bash .github/scripts/check-pr-source.test.sh` exits 0 only when every case passes.

- [ ] **Step 1: Write the failing self-test** as `.github/scripts/check-pr-source.test.sh`:

```bash
#!/usr/bin/env bash
# Self-test for check-pr-source.sh: each case states the expected exit code for a base/head pair.
set -uo pipefail

script="$(cd "$(dirname "$0")" && pwd)/check-pr-source.sh"
work="$(mktemp -d)"
failures=0

expect() {
  local name=$1 want=$2 base=$3 head=$4 got
  (cd "$work" && BASE_REF="$base" HEAD_REF="$head" bash "$script" >/dev/null 2>&1)
  got=$?
  if [[ $got -ne $want ]]; then
    echo "FAIL $name: base='$base' head='$head' exited $got, want $want"
    failures=$((failures + 1))
  fi
}

expect release-into-main 0 main release/1.0.0
expect hotfix-into-main 0 main hotfix/1.0.1-login
expect feature-into-main 1 main feature/m2a-delivery-foundation
expect bugfix-into-main 1 main bugfix/save-import
expect develop-into-main 1 main develop
expect bare-release-into-main 1 main release
expect lookalike-into-main 1 main xrelease/1.0.0
expect injection 1 main 'feature/$(touch pwned)'
expect feature-into-develop 0 develop feature/x
expect main-into-develop 0 develop main
expect release-into-develop 0 develop release/1.0.0
expect missing-head 1 main ''

if [[ -e "$work/pwned" ]]; then
  echo "FAIL injection: the branch name was executed"
  failures=$((failures + 1))
fi
rm -rf "$work"

if [[ $failures -ne 0 ]]; then
  echo "$failures case(s) failed"
  exit 1
fi
echo "All check-pr-source cases passed"
```

- [ ] **Step 2: Run it to verify it fails.**

```bash
bash .github/scripts/check-pr-source.test.sh; echo "exit=$?"
```

Expected: every case reports `FAIL` because the script does not exist yet (bash exits 127 for every case), and `exit=1`.

- [ ] **Step 3: Write the script** `.github/scripts/check-pr-source.sh`:

```bash
#!/usr/bin/env bash
# Allows only release/* and hotfix/* branches to open pull requests into main (TD-87).
# BASE_REF and HEAD_REF come from the environment and are only compared, never evaluated.
set -euo pipefail

base="${BASE_REF:-}"
head="${HEAD_REF:-}"

if [[ -z "$base" || -z "$head" ]]; then
  echo "::error::BASE_REF and HEAD_REF must be set to the pull request's base and head branches."
  exit 1
fi

if [[ "$base" != "main" ]]; then
  echo "OK: any branch may open a pull request into '$base'."
  exit 0
fi

case "$head" in
  release/* | hotfix/*)
    echo "OK: '$head' may open a pull request into main."
    ;;
  *)
    echo "::error::Only release/* and hotfix/* branches may open pull requests into main; target develop with '$head' instead."
    exit 1
    ;;
esac
```

- [ ] **Step 4: Run the self-test to verify it passes.**

```bash
bash .github/scripts/check-pr-source.test.sh; echo "exit=$?"
```

Expected: `All check-pr-source cases passed` and `exit=0`.

- [ ] **Step 5: Commit.**

```bash
git add .github/scripts/check-pr-source.sh .github/scripts/check-pr-source.test.sh
git commit -m "ci: add PR source-branch policy script with self-test" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`
- Delete: `.github/workflows/.gitkeep`

**Interfaces:**
- Consumes:
  - Task 1: the gate command, both coverage formats and `dotnet sonarscanner`;
  - Task 2: `check-pr-source.sh` and its self-test.
- Produces:
  - check runs named exactly `backend`, `sonar` and `pr-policy`;
  - the artifact `backend-coverage` (Cobertura XML).

- [ ] **Step 1: Install actionlint** (verification tool, user scope):

```bash
winget install --id rhysd.actionlint --version 1.7.12 --scope user --accept-source-agreements --accept-package-agreements
```

Then open a new shell. Expected: `actionlint -version` prints `1.7.12`. If winget is unavailable, stop and report; do not download binaries by other means without asking.

- [ ] **Step 2: Write `.github/workflows/ci.yml`:**

```yaml
name: CI

on:
  pull_request:
    branches: [develop, main]
  push:
    branches: [develop, main]
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

env:
  DOTNET_NOLOGO: "true"
  DOTNET_CLI_TELEMETRY_OPTOUT: "true"
  SONAR_ORGANIZATION: adamcharusta
  SONAR_PROJECT_KEY: adamcharusta_PolskiStreamerSimulator

jobs:
  backend:
    name: backend
    runs-on: ubuntu-latest
    timeout-minutes: 15
    defaults:
      run:
        working-directory: source/backend
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: source/backend/global.json
      - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('source/backend/Directory.Packages.props', 'source/backend/**/*.csproj', 'source/backend/.config/dotnet-tools.json') }}
          restore-keys: nuget-${{ runner.os }}-
      - name: Restore
        run: dotnet restore PolskiStreamerSimulator.slnx
      - name: Build
        run: dotnet build PolskiStreamerSimulator.slnx -c Release --no-restore
      - name: Verify formatting
        run: dotnet format PolskiStreamerSimulator.slnx --verify-no-changes --no-restore
      - name: Test with coverage gate
        run: >-
          dotnet test PolskiStreamerSimulator.slnx -c Release --no-build
          --results-directory "$RUNNER_TEMP/test-results"
          --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total
      - name: Coverage summary
        if: always()
        run: |
          report=$(ls "$RUNNER_TEMP"/test-results/coverage.cobertura.*.xml 2>/dev/null | head -n 1)
          if [ -z "$report" ]; then
            echo "Backend coverage report was not produced." >> "$GITHUB_STEP_SUMMARY"
            exit 0
          fi
          rate=$(grep -o 'line-rate="[0-9.]*"' "$report" | head -n 1 | cut -d '"' -f 2)
          awk -v r="$rate" 'BEGIN { printf "Backend line coverage: %.2f%% (gate: 80%%)\n", r * 100 }' >> "$GITHUB_STEP_SUMMARY"
      - uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        if: always()
        with:
          name: backend-coverage
          path: ${{ runner.temp }}/test-results/coverage.cobertura.*.xml
          if-no-files-found: warn

  sonar:
    name: sonar
    runs-on: ubuntu-latest
    timeout-minutes: 15
    defaults:
      run:
        working-directory: source/backend
    steps:
      - name: Require SonarQube Cloud token
        working-directory: .
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: |
          if [ -z "$SONAR_TOKEN" ]; then
            echo "::error::The SONAR_TOKEN repository secret is not available to this run. Add it as described in docs/development-and-delivery.md (SonarQube Cloud quality gate)."
            exit 1
          fi
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: source/backend/global.json
      - uses: actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('source/backend/Directory.Packages.props', 'source/backend/**/*.csproj', 'source/backend/.config/dotnet-tools.json') }}
          restore-keys: nuget-${{ runner.os }}-
      - name: Restore tools
        run: dotnet tool restore
      - name: Begin analysis
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: >-
          dotnet sonarscanner begin
          /k:"$SONAR_PROJECT_KEY"
          /o:"$SONAR_ORGANIZATION"
          /d:sonar.host.url="https://sonarcloud.io"
          /d:sonar.token="$SONAR_TOKEN"
          /d:sonar.cs.opencover.reportsPaths="$RUNNER_TEMP/sonar-results/coverage.opencover.*.xml"
          /d:sonar.coverage.exclusions="**/Tools/BatchSimulator/Program.cs,**/*.g.cs"
          /d:sonar.exclusions="**/Tests/Golden/**"
          /d:sonar.qualitygate.wait=true
      - name: Build
        run: dotnet build PolskiStreamerSimulator.slnx -c Release
      - name: Test with OpenCover coverage
        run: dotnet test PolskiStreamerSimulator.slnx -c Release --no-build --results-directory "$RUNNER_TEMP/sonar-results" --coverlet
      - name: Require OpenCover report
        run: |
          if ! ls "$RUNNER_TEMP"/sonar-results/coverage.opencover.*.xml >/dev/null 2>&1; then
            echo "::error::No OpenCover report in $RUNNER_TEMP/sonar-results; SonarQube Cloud would see 0% coverage."
            exit 1
          fi
      - name: End analysis and wait for the quality gate
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"

  pr-policy:
    name: pr-policy
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          sparse-checkout: .github/scripts
      - name: Self-test the policy script
        run: bash .github/scripts/check-pr-source.test.sh
      - name: Check the PR source branch
        env:
          BASE_REF: ${{ github.base_ref }}
          HEAD_REF: ${{ github.head_ref }}
        run: bash .github/scripts/check-pr-source.sh
```

- [ ] **Step 3: Lint the workflow.**

```bash
actionlint .github/workflows/ci.yml; echo "exit=$?"
```

Expected: no findings and `exit=0`. Fix any finding in the YAML; do not suppress it.

- [ ] **Step 4: Run the `backend` job's commands locally** from `source/backend`, using a scratch results directory (`$TEMP` replaces `$RUNNER_TEMP`):

```bash
dotnet restore PolskiStreamerSimulator.slnx && dotnet build PolskiStreamerSimulator.slnx -c Release --no-restore && dotnet format PolskiStreamerSimulator.slnx --verify-no-changes --no-restore && dotnet test PolskiStreamerSimulator.slnx -c Release --no-build --results-directory "$TEMP/m2a-ci" --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total; echo "exit=$?"
report=$(ls "$TEMP"/m2a-ci/coverage.cobertura.*.xml | head -n 1); grep -o 'line-rate="[0-9.]*"' "$report" | head -n 1
```

Expected: `exit=0` and a `line-rate` of at least `0.8`.

- [ ] **Step 5: Commit.**

```bash
git rm -q .github/workflows/.gitkeep
git add .github/workflows/ci.yml
git commit -m "ci: add backend, SonarQube Cloud and PR-policy checks" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Lefthook pre-commit hook

**Files:**
- Create: `package.json`, `package-lock.json` (generated), `lefthook.yml`
- Modify: `.gitignore` (append the tooling section)

**Interfaces:**
- Consumes: the trait convention `Category=Integration` (no tests carry it yet).
- Produces: running `npm install` in the repository root installs the `pre-commit` hook into `.git/hooks/`.

- [ ] **Step 1: Write `package.json`:**

```json
{
  "name": "polski-streamer-simulator-tooling",
  "private": true,
  "description": "Repository tooling only (Git hooks through Lefthook). The React app keeps its own package.json in source/frontend.",
  "devDependencies": {
    "lefthook": "2.2.1"
  }
}
```

- [ ] **Step 2: Append to the root `.gitignore`:**

```gitignore

# Root tooling dependencies (package.json for Lefthook)
/node_modules/
```

- [ ] **Step 3: Write `lefthook.yml`:**

```yaml
# Git hooks (TD-88). Install once with `npm install` in the repository root.
# A hook that changes tracked files rejects the commit; review and stage the fixes yourself.
pre-commit:
  piped: true
  fail_on_changes: always
  jobs:
    - name: backend-format
      root: "source/backend/"
      glob: "source/backend/**/*.cs"
      run: dotnet format PolskiStreamerSimulator.slnx --include {staged_files}
    - name: backend-tests
      glob:
        - "source/backend/*"
        - "source/backend/**/*"
        - "content/*"
        - "content/**/*"
      run: cd source/backend && dotnet test PolskiStreamerSimulator.slnx --filter-not-trait "Category=Integration"
```

`backend-tests` deliberately has no `root`, because `root` also filters the staged-file list and would skip content-only commits.

- [ ] **Step 4: Install and verify the hook is registered.**

```bash
npm install; ls .git/hooks/pre-commit && head -n 3 .git/hooks/pre-commit; git status --short
```

Expected:
- `package-lock.json` is created and `.git/hooks/pre-commit` exists;
- `git status` lists `package.json`, `package-lock.json`, `lefthook.yml` and `.gitignore`, but no `node_modules/`.

If the hook file is missing, run `npx lefthook install`. In that case README Step (Task 6) uses `npm install && npx lefthook install`; record a ruling.

- [ ] **Step 5: Verify that the test filter keeps every current test.**

```bash
cd source/backend && dotnet test PolskiStreamerSimulator.slnx --filter-not-trait "Category=Integration" 2>&1 | grep -E "total:|failed:"; cd ../..
```

Expected: `total: 256` and `failed: 0`.

- [ ] **Step 6: Watch the hook reject a formatting violation.** Introduce a whitespace violation, stage it with the tooling files, and commit:

```bash
sed -i 's/^public static class AssemblyMarker$/public  static class AssemblyMarker/' source/backend/Domain/AssemblyMarker.cs
git add package.json package-lock.json lefthook.yml .gitignore source/backend/Domain/AssemblyMarker.cs
git commit -m "probe: formatting violation"; echo "exit=$?"
git status --short source/backend/Domain/AssemblyMarker.cs; git diff source/backend/Domain/AssemblyMarker.cs
```

Expected:
- the output shows `backend-format`, then `Error: files were modified by a hook, and fail_on_changes is enabled`, and `exit=1`;
- the file status is `MM`: the staged violation plus the unstaged fix;
- `git diff` shows the fix restoring a single space.

If the commit is accepted, or the file is unchanged, `--include` did not match the files. In that case:
1. Remove `root` from `backend-format`.
2. Change its `run` to `dotnet format source/backend/PolskiStreamerSimulator.slnx --include {staged_files}`.
3. Repeat this step and record a ruling.

- [ ] **Step 7: Undo the probe and commit the tooling with the hook active.**

```bash
git restore --staged source/backend/Domain/AssemblyMarker.cs && git checkout -- source/backend/Domain/AssemblyMarker.cs
git status --short
git commit -m "build: add Lefthook pre-commit hook for backend format and tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"; echo "exit=$?"
```

Expected:
- only the four tooling files are staged;
- the hook skips `backend-format` and `backend-tests`, because no files under `source/backend/` or `content/` are staged;
- `exit=0`.

- [ ] **Step 8: Watch the hook pass on a clean backend change.** Add a comment-only line to a C# file, commit it, then revert it in a second commit:

```bash
sed -i 's|^/// <summary>Anchor type for locating the Domain assembly.</summary>$|/// <summary>Anchor type for locating the Domain assembly (used by architecture tests).</summary>|' source/backend/Domain/AssemblyMarker.cs
git add source/backend/Domain/AssemblyMarker.cs
git commit -m "docs(domain): clarify AssemblyMarker purpose" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"; echo "exit=$?"
```

Expected: `backend-format` and `backend-tests` both run and pass (256 tests), and `exit=0`. Keep this commit; it is a real, harmless doc-comment improvement that proves the hook path.

---

### Task 5: Versioned rulesets

**Files:**
- Create: `.github/rulesets/main.json`, `.github/rulesets/develop.json`

**Interfaces:**
- Consumes: the check names `backend`, `sonar` and `pr-policy` from Task 3.
- Produces: the JSON bodies posted in Task 10 with `gh api --method POST repos/adamcharusta/PolskiStreamerSimulator/rulesets --input <file>`.

- [ ] **Step 1: Write `.github/rulesets/main.json`:**

```json
{
  "name": "main",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/heads/main"],
      "exclude": []
    }
  },
  "bypass_actors": [
    {
      "actor_id": 5,
      "actor_type": "RepositoryRole",
      "bypass_mode": "pull_request"
    }
  ],
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": true,
        "do_not_enforce_on_create": false,
        "required_status_checks": [
          { "context": "backend", "integration_id": 15368 },
          { "context": "sonar", "integration_id": 15368 },
          { "context": "pr-policy", "integration_id": 15368 }
        ]
      }
    }
  ]
}
```

`actor_id` 5 is the built-in Repository admin role. `integration_id` 15368 is the GitHub Actions app, which binds each check to Actions so another app cannot satisfy it.

- [ ] **Step 2: Write `.github/rulesets/develop.json`.** It is identical to `main.json` except for `"name": "develop"`, `"include": ["refs/heads/develop"]` and `"strict_required_status_checks_policy": false`:

```json
{
  "name": "develop",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/heads/develop"],
      "exclude": []
    }
  },
  "bypass_actors": [
    {
      "actor_id": 5,
      "actor_type": "RepositoryRole",
      "bypass_mode": "pull_request"
    }
  ],
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": false,
        "do_not_enforce_on_create": false,
        "required_status_checks": [
          { "context": "backend", "integration_id": 15368 },
          { "context": "sonar", "integration_id": 15368 },
          { "context": "pr-policy", "integration_id": 15368 }
        ]
      }
    }
  ]
}
```

- [ ] **Step 3: Validate both files against the spec** with an independent check (Python ships with Git for Windows images and Ubuntu runners; locally use `python3`):

```bash
python3 - <<'PY'
import json
for name, strict in (("main", True), ("develop", False)):
    r = json.load(open(f".github/rulesets/{name}.json", encoding="utf-8"))
    assert r["conditions"]["ref_name"]["include"] == [f"refs/heads/{name}"], name
    assert r["bypass_actors"] == [{"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "pull_request"}], name
    rules = {x["type"]: x.get("parameters") for x in r["rules"]}
    assert set(rules) == {"deletion", "non_fast_forward", "pull_request", "required_status_checks"}, name
    assert rules["pull_request"]["required_approving_review_count"] == 0, name
    assert rules["pull_request"]["allowed_merge_methods"] == ["merge"], name
    checks = rules["required_status_checks"]
    assert checks["strict_required_status_checks_policy"] is strict, name
    assert [c["context"] for c in checks["required_status_checks"]] == ["backend", "sonar", "pr-policy"], name
print("rulesets OK")
PY
```

Expected: `rulesets OK`.

- [ ] **Step 4: Commit** (a docs-only change for the hook; no backend jobs run):

```bash
git add .github/rulesets/main.json .github/rulesets/develop.json
git commit -m "ci: version the main and develop repository rulesets" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Decision records and documentation

**Files:**
- Modify: `docs/technical-decisions.md`, `docs/development-and-delivery.md`, `docs/repository-standards.md`, `README.md`, `docs/requirements-traceability.md`

**Interfaces:**
- Consumes: the file names and settings from Tasks 1–5.

- [ ] **Step 1: Append TD-85…TD-88** to the Confirmed Decisions table in `docs/technical-decisions.md`, directly after the TD-84 row:

```markdown
| TD-85 | Split milestone M2 into three sub-projects with their own spec, plan and implementation: A delivery foundation (#14 CI, Lefthook and SonarQube Cloud; #15), B backend slice (#9, #10, #13 server side) and C frontend slice (#8, #11, #12, #13 UI; #14 Docker Compose and smoke test), delivered in the order A → B → C. | Owner decision, 2026-10-13 ([M2-A design](superpowers/specs/2026-10-13-m2a-delivery-foundation-design.md)). Previous plan: M2 issues without an internal order beyond their listed dependencies. Reason: the issues form independent subsystems, and delivering CI and branch rules first lets B and C reach `develop` through gated PRs (TD-72) instead of local merges (TD-84). Impact: #14 is delivered in two parts (A and C). |
| TD-86 | Run CI as one workflow, `.github/workflows/ci.yml`, with three required jobs. `backend`: Release build without warnings, `dotnet format --verify-no-changes`, and tests with an 80% total backend line-coverage gate through coverlet.MTP. `sonar`: CI-based SonarQube Cloud analysis with SonarScanner for .NET, OpenCover coverage and `sonar.qualitygate.wait=true`, the default Sonar way gate, and `develop` analysed as a long-lived branch. `pr-policy`: only `release/*` and `hotfix/*` may open PRs into `main`. No path filters; third-party actions pinned to commit SHAs. | Owner decision, 2026-10-13 (workflow layout; SonarQube Cloud from the start). SHA pinning and the coverlet threshold are agent proposals accepted with the design. Previous plan: TD-73 and development-and-delivery described the checks without a workflow layout, and TD-81 deferred CI until after M1. Reason: required checks must always report, and a SonarQube Cloud outage must not hide the test result. Impact: B adds `backend-integration` and C adds `frontend` as further required jobs. SHA-pinned actions are updated manually until Dependabot is introduced. |
| TD-87 | Protect `main` and `develop` with repository rulesets versioned in `.github/rulesets/`. Rules: no deletion or force push; changes only by PR with 0 required approvals; required checks `backend`, `sonar` and `pr-policy`, with an up-to-date branch required on `main` only; merge commits only; Repository admins may bypass only through a PR. Disable squash and rebase merging and delete merged branches automatically. Reconcile a release or hotfix by a PR from `main` into `develop` (and into an active `release/*`). | Owner decision, 2026-10-13. Previous plan: development-and-delivery left the exact ruleset and emergency bypass policy open. Reason: the owner is the sole maintainer and cannot approve their own PR; a PR-only bypass keeps direct pushes blocked while allowing a deliberate merge during a CI or SonarQube Cloud outage. Impact: every change reaches `develop` and `main` through a PR, and GitHub records each bypass. |
| TD-88 | Install Lefthook from a root `package.json` (exact version, with `package-lock.json`) by running `npm install`. The `pre-commit` hook runs `dotnet format` on staged backend C# files and rejects the commit when it changes anything, leaving the fixes unstaged. When backend or `content/` files are staged, it then runs the fast backend tests, excluding the `Category=Integration` trait. | Agent proposal accepted by the owner, 2026-10-13. Previous plan: TD-71 chose Lefthook without an installation method, and repository-standards listed `lefthook.yml` as planned. Reason: the same pinned version on every machine; Node is required for the frontend anyway. Impact: adds a root `package.json` and a `/node_modules/` entry in the root `.gitignore`. Integration tests added in B must carry the trait, and frontend lint joins the hook in C. |
```

Then append to the end of TD-81's Impact cell, before its closing ` |`, the sentence ` Delivered by TD-86 and TD-88 (2026-10-13).`

- [ ] **Step 2: Update `docs/development-and-delivery.md`.**

(a) In the opening status paragraph, replace `The repository currently has `main` and `origin/main`; `develop` has not yet been created.` with `` `develop` exists, and repository rulesets protect `main` and `develop` (TD-87).``

(b) After the paragraph that begins `Protect `main` against force pushes`, insert:

```markdown
**Implemented rules (TD-87).** Repository rulesets, versioned in `.github/rulesets/main.json` and `.github/rulesets/develop.json`, block deletion and force pushes on `main` and `develop` and accept changes only through PRs. The sole maintainer cannot approve their own PR, so no approval is required; instead the checks `backend`, `sonar` and `pr-policy` are required, and `main` also requires the branch to be up to date. Only merge commits are allowed. Squash and rebase merging are disabled so that release and hotfix commits keep the same identity on `main` and `develop`. Repository admins may bypass the rules only through a PR, for example during a CI or SonarQube Cloud outage; direct pushes stay blocked. `pr-policy` rejects PRs into `main` from branches other than `release/*` and `hotfix/*`. Merged branches are deleted automatically. To change a ruleset, edit its JSON file in a PR, then apply it with `gh api --method PUT repos/adamcharusta/PolskiStreamerSimulator/rulesets/<id> --input <file>`.

**Reconciling a release or hotfix.** After a `release/*` or `hotfix/*` PR merges into `main`, its branch is deleted. Open a PR from `main` into `develop`, and into any active `release/*`. Resolve conflicts there and merge it once its checks pass. This brings the exact released tree back into integration.
```

(c) At the end of the `## Local pre-commit checks` first paragraph, append:

```markdown
Install the hooks once with `npm install` in the repository root; this installs the pinned Lefthook version from the root `package.json` (TD-88). The hook rejects a commit when `dotnet format` changes a staged file and leaves the fixes unstaged for review. Backend tests in the hook skip tests marked with the trait `Category=Integration`; CI runs them.
```

(d) After the numbered list in `## GitHub Actions stages`, insert:

```markdown
**Current state (M2-A, TD-86).** `.github/workflows/ci.yml` implements the backend part of *Validate*, with the required jobs `backend`, `sonar` and `pr-policy`. It runs on PRs into `develop` and `main` and on pushes to them. Sub-project B adds a SQL Server integration job and sub-project C adds the frontend job. Packaging, infrastructure checks and deployment arrive with M6 ([#42](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/42)); until then a push to `develop` runs CI only and deploys nothing.
```

(e) At the end of `### SonarQube Cloud quality gate`'s first paragraph, append:

```markdown
The SonarQube Cloud organization is `adamcharusta` and the project key `adamcharusta_PolskiStreamerSimulator`. Automatic Analysis is disabled in favour of CI-based analysis. `main`, `develop` and `release/*` are long-lived branches, and the quality gate is Sonar way. The analysis token is the repository secret `SONAR_TOKEN`; only the owner sets or rotates it (`gh secret set SONAR_TOKEN`).
```

(f) In `## Repository setup and remaining inputs`, replace `Create `develop` from the intended `main` baseline, configure PR/status-check rules, add release-candidate environment isolation, then enable deployment workflows after secrets and hosts are ready.` with ``develop` exists and the PR and status-check rules are configured (TD-87). Next, add release-candidate environment isolation, then enable deployment workflows after secrets and hosts are ready.``

- [ ] **Step 3: Update `docs/repository-standards.md`.**

(a) Replace the row that begins `| `lefthook.yml` (planned with application scaffolding) |` with:

```markdown
| `package.json` and `package-lock.json` (root) | Repository tooling only: the exact Lefthook version installed by `npm install` in the root (TD-88). The React app keeps its own `package.json` in `source/frontend`. |
| `lefthook.yml` | `pre-commit` hook. Runs `dotnet format` on staged backend C# files and rejects the commit if anything changes, then fast backend tests without `Category=Integration`. Frontend lint is added with the React app (TD-88). |
| `.github/workflows/ci.yml` | Required CI jobs `backend`, `sonar` and `pr-policy`; third-party actions pinned to commit SHAs (TD-86). |
| `.github/scripts/` | Bash helpers used by CI, each with a self-test (`check-pr-source.sh`, `check-pr-source.test.sh`). |
| `.github/rulesets/` | Versioned definitions of the `main` and `develop` repository rulesets (TD-87). |
| `source/backend/.config/dotnet-tools.json` | Local .NET tool manifest pinning SonarScanner for .NET (TD-86). |
```

(b) In the `.gitignore` row, replace `and local deployment credentials;` with `local deployment credentials and the root tooling `node_modules/`;`.

(c) In the `source/backend/Tests/testconfig.json` row, replace `and Cobertura output (TD-82)` with `and Cobertura (CI artifact) plus OpenCover (SonarQube Cloud) output (TD-82, TD-86)`.

- [ ] **Step 4: Update `README.md`.**

(a) In the layout block, replace the line `.github/workflows/         GitHub Actions (later milestone)` with:

```text
.github/
  workflows/               CI (backend, SonarQube Cloud, PR policy)
  rulesets/                main and develop branch rules
  scripts/                 CI helper scripts
```

(b) Append to `## Build and test`:

````markdown
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

Changes reach `develop` and `main` only through pull requests; see [development and delivery](docs/development-and-delivery.md).
````

- [ ] **Step 5: Update `docs/requirements-traceability.md`.**

(a) In the status legend sentence, replace ``Status values: `Planned` (not started), `In progress (M1)` (part of the requirement is being delivered by an M1 issue; the remainder stays with the named later milestone), `Implemented (M1)` (the M1 part has passing tests).`` with ``Status values: `Planned` (not started), `In progress (Mn)` (part of the requirement is being delivered in milestone n; the remainder stays with the named later milestone), `Implemented (Mn)` (the milestone-n part has passing tests).``

(b) Replace the REQ-26 row's last two cells with:

```markdown
| [#2](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/2)–[#6](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/6) (TDD, backend coverage measured); M2 ([#14](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/14): backend CI gate in M2-A, frontend gate in M2-C); M6 (deployment gates) | In progress (M2) |
```

(c) Replace the REQ-27 row's last two cells with:

```markdown
| M2 ([#14](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/14), [#15](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/15)); M6 | In progress (M2) |
```

- [ ] **Step 6: Verify links and commit.** Run the link check from the repository root:

```bash
rc=0; for l in $(grep -o '](docs/[^)]*)' README.md | sed 's/](//;s/)//;s/#.*//'); do test -f "$l" || { echo "MISSING README: $l"; rc=1; }; done
for f in docs/*.md docs/reports/*.md docs/superpowers/specs/*.md docs/superpowers/plans/*.md; do d=$(dirname "$f"); for l in $(grep -o '](\([^)h#][^)]*\))' "$f" | sed 's/](//;s/)//;s/#.*//' | sort -u); do test -e "$d/$l" || { echo "MISSING $f: $l"; rc=1; }; done; done; echo "links rc=$rc"
git add docs/technical-decisions.md docs/development-and-delivery.md docs/repository-standards.md README.md docs/requirements-traceability.md
git commit -m "docs: record M2-A delivery decisions TD-85 to TD-88" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Expected: `links rc=0`; the hook runs nothing (documentation only); the commit succeeds.

---

### Task 7: [external] Publish `develop` and open the M2-A pull request

**Interfaces:**
- Produces: `origin/develop`, `origin/feature/m2a-delivery-foundation`, and PR number `<A>` (recorded in the ledger for Tasks 8–11).

- [ ] **Step 1: Ask the owner:** "Push `develop` and `feature/m2a-delivery-foundation` to GitHub and open the M2-A PR into `develop`?" Wait for an explicit yes.

- [ ] **Step 2: Push both branches.**

```bash
git push -u origin develop
git push -u origin feature/m2a-delivery-foundation
```

Expected: both pushes succeed. No CI runs for the `develop` push, because `develop` does not contain `ci.yml` yet.

- [ ] **Step 3: Open the PR.** Write the body to the session scratchpad as `pr-a.md`:

```markdown
## Summary
- One CI workflow with required jobs `backend` (Release build, format check, tests, 80% backend line-coverage gate), `sonar` (SonarQube Cloud quality gate) and `pr-policy` (only `release/*` and `hotfix/*` may target `main`).
- Versioned `main` and `develop` rulesets (applied after the checks report), plus a Lefthook pre-commit hook installed by `npm install`.
- Decision records TD-85 to TD-88 and updated delivery and repository-standards docs.

Spec: `docs/superpowers/specs/2026-10-13-m2a-delivery-foundation-design.md`
Plan: `docs/superpowers/plans/2026-10-13-m2a-delivery-foundation.md`

Refs #14, #15

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

```bash
gh pr create --base develop --head feature/m2a-delivery-foundation --title "M2-A: delivery foundation (CI, SonarQube Cloud, rulesets, Lefthook)" --body-file "<scratchpad>/pr-a.md"
```

Expected: a PR URL is printed. Record the number as `<A>`.

- [ ] **Step 4: Watch the first CI run.**

```bash
gh pr checks <A> --watch --fail-fast=false
```

Expected:
- `backend` passes, and its job summary shows `Backend line coverage: 9x.xx% (gate: 80%)`;
- `pr-policy` passes, and its log shows `All check-pr-source cases passed` and `OK: any branch may open a pull request into 'develop'.`;
- `sonar` **fails** at `Require SonarQube Cloud token` with the actionable `SONAR_TOKEN` message. This is expected until Task 8 and confirms Review Focus line 5.

---

### Task 8: [owner + external] Connect SonarQube Cloud and get a green `sonar`

**Interfaces:**
- Consumes: PR `<A>`.
- Produces: a green `sonar` check on `<A>`, and confirmed organization and project key constants.

- [ ] **Step 1: Give the owner these steps** and wait until they report done:
  1. Sign in at https://sonarcloud.io with GitHub.
  2. Choose **+ → Analyze new project → Import an organization from GitHub**, install the SonarQube Cloud app on the account `adamcharusta` with access to **only** `PolskiStreamerSimulator`, and choose the free plan.
  3. Select `PolskiStreamerSimulator`, set new code to **Previous version**, and create the project.
  4. In the project, under **Administration → Analysis Method**, turn **off** Automatic Analysis.
  5. Under **Administration → Branches and Pull Requests**, set the long-lived branches pattern to `(main|develop|release/.*)`.
  6. Under **My Account → Security**, generate a token, then in this session run `! gh secret set SONAR_TOKEN` and paste it when prompted. The token must not be pasted into the chat.
  7. Report the organization key and project key shown on the project's **Information** page.

- [ ] **Step 2: Confirm the keys and the secret without reading the token.**

```bash
gh secret list | grep SONAR_TOKEN
curl -s "https://sonarcloud.io/api/components/show?component=adamcharusta_PolskiStreamerSimulator" | head -c 300
```

Expected:
- `SONAR_TOKEN` is listed;
- the API returns a component whose `key` is `adamcharusta_PolskiStreamerSimulator`, and whose organization is `adamcharusta`.

If the owner reports different keys:
1. Update `SONAR_ORGANIZATION` and `SONAR_PROJECT_KEY` in `ci.yml` and the TD-86 / development-and-delivery text.
2. Commit with `ci: use the SonarQube Cloud keys assigned at import`.
3. Push the branch; this re-runs CI.
4. Record a ruling.

- [ ] **Step 3: Ask the owner** "Re-run the failed CI jobs on PR `<A>`?". After the yes, re-run and watch:

```bash
run=$(gh run list --branch feature/m2a-delivery-foundation --workflow CI --limit 1 --json databaseId --jq '.[0].databaseId'); gh run rerun "$run" --failed; gh pr checks <A> --watch --fail-fast=false
```

Expected: all three checks pass. The `sonar` log shows the OpenCover report found, `QUALITY GATE STATUS: PASSED`, and the PR analysis using `sonar.pullrequest.key=<A>` (auto-detected).

- [ ] **Step 4: Confirm SonarQube Cloud imported coverage.**

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=adamcharusta_PolskiStreamerSimulator&pullRequest=<A>&metricKeys=coverage,new_coverage,ncloc" | head -c 400
```

Expected: the response lists `ncloc` > 0. The `coverage` field must not be absent. An absent coverage metric with C# in scope means the report path did not match; fix `sonar.cs.opencover.reportsPaths` before continuing.

The first `develop` branch analysis happens on the push that merges this PR (Task 11), not here. `workflow_dispatch` works only once `ci.yml` exists on the default branch `main`.

---

### Task 9: [external] Probe PRs prove the gates block

**Interfaces:**
- Consumes: the green PR `<A>` (its branch is the probes' base, so they carry `ci.yml`).
- Produces: three red, closed probe PRs `<P1>`, `<P2>`, `<P3>`, recorded in the ledger as evidence.

- [ ] **Step 1: Ask the owner:** "Open three probe PRs (format violation, coverage drop, feature branch into main) that will be closed unmerged and their branches deleted?" Wait for an explicit yes.

- [ ] **Step 2: Probe 1, formatting.**

```bash
git checkout -b probe/format-violation feature/m2a-delivery-foundation
sed -i 's/^public static class AssemblyMarker$/public  static class AssemblyMarker/' source/backend/Domain/AssemblyMarker.cs
git commit --no-verify -am "probe: formatting violation (do not merge)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin probe/format-violation
gh pr create --base develop --head probe/format-violation --title "Probe: formatting violation (do not merge)" --body "Gate probe for M2-A; expected red \`backend\`. Will be closed unmerged."
```

Expected: `backend` fails at `Verify formatting`, and `gh pr view <P1> --json mergeStateStatus --jq .mergeStateStatus` is not `CLEAN`.

- [ ] **Step 3: Probe 2, coverage.** Remove the domain and batch-simulator tests so that coverage drops while the remaining tests still pass:

```bash
git checkout -b probe/coverage-drop feature/m2a-delivery-foundation
git rm -r -q source/backend/Tests/Domain source/backend/Tests/BatchSimulator
git commit --no-verify -m "probe: drop tests below the coverage gate (do not merge)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin probe/coverage-drop
gh pr create --base develop --head probe/coverage-drop --title "Probe: coverage below 80% (do not merge)" --body "Gate probe for M2-A; expected red \`backend\` at the coverage gate. Will be closed unmerged."
```

Expected: `backend` fails at `Test with coverage gate`. The tests that ran passed, the log reports total line coverage below 80%, and the run exits non-zero.

- [ ] **Step 4: Probe 3, PR source policy.**

```bash
git checkout -b feature/probe-main-target feature/m2a-delivery-foundation
git commit --allow-empty --no-verify -m "probe: feature branch targeting main (do not merge)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -u origin feature/probe-main-target
gh pr create --base main --head feature/probe-main-target --title "Probe: feature into main (do not merge)" --body "Gate probe for M2-A; expected red \`pr-policy\`. Will be closed unmerged."
```

Expected: `pr-policy` fails with `Only release/* and hotfix/* branches may open pull requests into main`.

- [ ] **Step 5: Record the evidence and clean up.**

```bash
for p in <P1> <P2> <P3>; do gh pr checks "$p" | cat; gh pr close "$p" --delete-branch; done
git checkout feature/m2a-delivery-foundation && git branch -D probe/format-violation probe/coverage-drop feature/probe-main-target
```

Expected: each listing shows the expected failing job. All three PRs are closed and their remote and local branches deleted.

---

### Task 10: [external] Apply the rulesets and repository settings

**Interfaces:**
- Consumes: `.github/rulesets/main.json`, `.github/rulesets/develop.json`, and the green PR `<A>`.
- Produces: active rulesets on GitHub. Their ids are recorded in the ledger for future `PUT` updates.

- [ ] **Step 1: Ask the owner:** "Create the `main` and `develop` rulesets and switch the repository to merge-commit-only with automatic branch deletion?" Wait for an explicit yes.

- [ ] **Step 2: Apply.**

```bash
gh api --method POST repos/adamcharusta/PolskiStreamerSimulator/rulesets --input .github/rulesets/main.json --jq '{id, name, enforcement}'
gh api --method POST repos/adamcharusta/PolskiStreamerSimulator/rulesets --input .github/rulesets/develop.json --jq '{id, name, enforcement}'
gh api --method PATCH repos/adamcharusta/PolskiStreamerSimulator -F allow_squash_merge=false -F allow_rebase_merge=false -F allow_merge_commit=true -F delete_branch_on_merge=true --jq '{allow_squash_merge, allow_rebase_merge, allow_merge_commit, delete_branch_on_merge}'
```

Expected: two rulesets with `"enforcement": "active"`, and the settings print `false, false, true, true`.

- [ ] **Step 3: Verify the effective rules.**

```bash
for b in main develop; do echo "$b:"; gh api repos/adamcharusta/PolskiStreamerSimulator/rules/branches/$b --jq '[.[].type] | sort'; done
gh api repos/adamcharusta/PolskiStreamerSimulator/rules/branches/main --jq '.[] | select(.type=="required_status_checks") | .parameters | {strict: .strict_required_status_checks_policy, checks: [.required_status_checks[].context]}'
```

Expected:
- both branches list `["deletion","non_fast_forward","pull_request","required_status_checks"]`;
- `main` shows `strict: true` and checks `["backend","sonar","pr-policy"]`.

- [ ] **Step 4: Verify the required checks match the job names** (Review Focus line 4):

```bash
gh pr view <A> --json mergeStateStatus,statusCheckRollup --jq '{state: .mergeStateStatus, checks: [.statusCheckRollup[] | {name, conclusion}]}'
```

Expected: `state` is `CLEAN`, and the three checks are `SUCCESS`. A `BLOCKED` state with all checks green means a context-name mismatch. In that case, fix the JSON, apply it with `gh api --method PUT …/rulesets/<id> --input <file>`, and record a ruling.

---

### Task 11: [external] Finish the records and merge M2-A through the ruleset

**Interfaces:**
- Consumes: PR `<A>` with the rulesets active.

- [ ] **Step 1: Record the delivered state.** In `docs/requirements-traceability.md`, change the REQ-27 cell `M2 ([#14](…), [#15](…)); M6` to:

```markdown
M2 ([#14](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/14): pre-commit format and fast tests, CI tests, backend coverage gate and SonarQube Cloud delivered in M2-A; frontend lint, Prettier, Testcontainers and Playwright follow in M2-B/C; [#15](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/15): protected `main`/`develop` delivered in M2-A); M6
```

Keep the status `In progress (M2)`. Commit with `docs: record delivered M2-A checks in the traceability matrix`.

- [ ] **Step 2: Ask the owner** "Push the traceability update to PR `<A>`?" After the yes, push and watch until all checks pass:

```bash
git push && gh pr checks <A> --watch --fail-fast=false
```

- [ ] **Step 3: Propose the #14 wording.** Show the owner this section to append to issue #14:

```markdown
## Delivery split (TD-85)

- **M2-A (delivery foundation):** Lefthook pre-commit (`dotnet format`, fast backend tests), GitHub Actions CI with tests and the backend 80% line-coverage gate, and SonarQube Cloud PR checks.
- **M2-C (frontend slice):** Docker Compose with SQL Server, API and Vite started by one command, frontend lint/Vitest and its 80% coverage gate, Prettier check, and the local smoke test (career start, choice, reload, test admin login).
```

Ask "Append this to issue #14?". After the yes:

```bash
gh issue view 14 --json body --jq .body > "<scratchpad>/issue14.md" && printf '\n%s\n' "<the section above>" >> "<scratchpad>/issue14.md" && gh issue edit 14 --body-file "<scratchpad>/issue14.md"
```

Expected: `gh issue view 14` shows the original body followed by the new section.

- [ ] **Step 4: Ask the owner** "Merge PR `<A>` into `develop` (merge commit, through the ruleset)?". After the yes:

```bash
gh pr merge <A> --merge
git checkout develop && git pull --ff-only && git log --oneline -1
gh run list --branch develop --workflow CI --limit 1
```

Expected:
- the merge succeeds without bypass, because the checks are green and no approval is required;
- the remote branch is deleted automatically, and local `develop` fast-forwards to the merge commit;
- a CI run starts for the `develop` push. Watch it with `gh run watch <id>` until `backend` and `sonar` pass; `pr-policy` is skipped on push. This is the first SonarQube Cloud analysis of `develop`.

- [ ] **Step 5: Clean up locally.**

```bash
git branch -d feature/m2a-delivery-foundation
```

Expected: the branch is deleted because it is merged.
