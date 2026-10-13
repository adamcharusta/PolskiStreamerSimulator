# Repository Configuration and Code Quality

Status: **owner tooling choices and initial configuration**, 2026-10-12. The repository is still at the documentation stage; the React and .NET application projects have not been scaffolded. Files listed below now establish the layout and style rules. ESLint package versions and executable config will be pinned and verified when `source/frontend/package.json` and the React app are created.

## Configuration placement

| File | Scope and role |
| --- | --- |
| `.gitignore` | Ignore Codex/Claude local artifacts, OS files, Visual Studio, VS Code, JetBrains files local deployment credentials and the root tooling `node_modules/`; keep the root `AGENTS.md` and `CLAUDE.md` tracked. |
| `AGENTS.md` and `CLAUDE.md` | Versioned agent guides with a documentation map and English decision-record policy. `AGENTS.md` is canonical; `CLAUDE.md` points to it. |
| `source/frontend/.gitignore` | Ignore Node dependencies, Vite/TypeScript output, test reports and local frontend environment files. |
| `source/backend/.gitignore` | Ignore .NET build/test output, local settings, certificates and local SQL data/backup files. |
| `.gitattributes` | Normalize source, docs, workflow, Ansible and Kubernetes text to LF; retain CRLF for Windows batch files and preserve binaries. |
| `.editorconfig` | Root UTF-8/LF/two-space conventions for GitHub Actions, YAML, JSON, shell, PowerShell, Terraform and other repository scripts. |
| `docs/.editorconfig` | English Markdown files in UTF-8/LF; preserve intentional Markdown trailing spaces. |
| `source/backend/.editorconfig` | Four-space C# style, file-scoped namespaces, braces and unused-import diagnostics; two-space project XML. |
| `source/backend/Directory.Build.props` | Common `net10.0`, nullable, implicit usings, build analysis, deterministic output and CI settings for all .NET projects; `PolskiStreamerSimulator.<Project>` assembly names and root namespaces; XML documentation generation (needed for the IDE0005 unused-using check on build) with CS1591 suppressed (TD-82). |
| `source/backend/Directory.Packages.props` | NuGet Central Package Management for every backend project. Add exact package versions as projects are scaffolded; project `PackageReference` elements omit versions. |
| `source/backend/global.json` | Minimum .NET SDK 10.0.100, rolling to the latest installed .NET 10 feature band/patch; prerelease SDKs excluded. Selects Microsoft.Testing.Platform as the `dotnet test` runner (TD-82). |
| `source/backend/Tests/testconfig.json` | Microsoft.Testing.Platform settings, including coverlet.MTP coverage filters (tests and the batch tool entry point excluded, generated `*.g.cs` and `[GeneratedCode]`/`[CompilerGenerated]` code excluded) and Cobertura (CI artifact) plus OpenCover (SonarQube Cloud) output (TD-82, TD-86). |
| `source/frontend/.prettierrc.json` | Initial two-space, LF, single-quote, semicolon and 100-column formatting defaults; review with the first React code. |
| `source/frontend/.prettierignore` | Keep generated Orval client and build/test output out of formatting checks. |
| `package.json` and `package-lock.json` (root) | Repository tooling only: the exact Lefthook version installed by `npm install` in the root, with Lefthook's install script approved under npm `allowScripts` (TD-88). The React app keeps its own `package.json` in `source/frontend`. |
| `lefthook.yml` | `pre-commit` hook. Runs `dotnet format` on staged backend C# files and rejects the commit if anything changes, then fast backend tests without `Category=Integration`. Frontend lint is added with the React app (TD-88). |
| `.github/workflows/ci.yml` | Required CI jobs `backend`, `sonar` and `pr-policy`; third-party actions pinned to commit SHAs (TD-86). |
| `.github/scripts/` | Bash helpers used by CI, each with a self-test (`check-pr-source.sh`, `check-pr-source.test.sh`). |
| `.github/rulesets/` | Versioned definitions of the `main` and `develop` repository rulesets (TD-87). |
| `source/backend/.config/dotnet-tools.json` | Local .NET tool manifest pinning SonarScanner for .NET (TD-86). |

There are exactly **three `.gitignore` files**, each with a distinct responsibility. The root ignore covers editor/agent/OS and deployment-local files without hiding source or documentation; the root agent guides are explicit exceptions. Frontend and backend files cover their own generated output. Root `.editorconfig` and `.gitattributes` also cover `.github/workflows/`, `k8s/` and optional `scripts/`; infrastructure assets do not get a fourth ignore file. Verify `git status --ignored` and `git check-ignore -v` when a new tool writes files, especially before adding broad patterns. `.gitattributes` is the canonical line-ending policy; `.editorconfig` is the editor policy. GitHub Actions must validate formatting rather than silently rewrite committed files.

The .NET SDK selection uses `global.json` with `rollForward: latestFeature`, which stays within .NET 10 while accepting later installed feature bands. Central Package Management uses `ManagePackageVersionsCentrally`. [Microsoft SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [NuGet Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/Central-Package-Management), [MSBuild directory customization](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=visualstudio).

## React lint and formatting stack

Install and configure these owner-selected development dependencies in `source/frontend` when the React app is scaffolded:

| Purpose | Packages |
| --- | --- |
| ESLint core, TypeScript and environment globals | `eslint`, `@eslint/js`, `typescript-eslint`, `globals` |
| React and accessibility | `eslint-plugin-react`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`, `eslint-plugin-jsx-a11y` |
| Import validity, unused code and ordering | `eslint-plugin-import-x`, `eslint-plugin-unused-imports`, `eslint-plugin-simple-import-sort` |
| Additional code-quality rules | `eslint-plugin-unicorn` |
| Test-specific rules | `@vitest/eslint-plugin` |
| Formatting | `eslint-config-prettier`, `prettier` |

Use a flat `eslint.config.mjs`. Apply React/JSX rules to app components, browser globals to client files, Node globals to Vite/ESLint config files, and Vitest rules only to test files. `eslint-plugin-import-x` checks import validity; `eslint-plugin-simple-import-sort` owns import order, so disable overlapping ordering rules. `eslint-plugin-unused-imports` owns unused-import diagnostics; disable overlapping base/TypeScript unused-variable rules where necessary. Put `eslint-config-prettier` last and run Prettier as a separate format check. The generated Orval folder is excluded from lint/format checks; its output is validated by a regeneration-drift check. Select a mutually compatible stable version set before enabling the config, especially for ESLint, `typescript-eslint` and Unicorn. [typescript-eslint flat-config guide](https://typescript-eslint.io/getting-started/), [React ESLint plugin](https://github.com/jsx-eslint/eslint-plugin-react), [Unicorn plugin](https://github.com/sindresorhus/eslint-plugin-unicorn).

## CI gates

The `develop`, `release/*`, `hotfix/*` and `main` workflows should run the relevant checks for their stage: `dotnet format --verify-no-changes` and `dotnet build` for the backend; ESLint, TypeScript typecheck and `prettier --check` for the frontend; YAML/Ansible/Helm validation for deployment files; and the existing unit, integration and browser tests. Require **at least 80% line coverage for the backend and at least 80% for the frontend as separate CI gates**, with explicit reviewed exclusions for tests, generated code, build output and migrations. Fail on coverage, formatting or generated-client drift rather than mutating files during CI. Keep local editor formatting and CI output consistent with the committed `.editorconfig` and `.gitattributes`.

When code projects exist, install the root Lefthook pre-commit hook as part of local setup. It runs `dotnet format` from `source/backend` for staged C# changes, `npm run lint -- --fix` from `source/frontend` for staged frontend changes, and fast xUnit/Vitest tests. Fixes must be reviewed and staged intentionally before commit. Prettier is checked in CI, alongside integration tests, Playwright, full coverage reports and SonarQube Cloud. Require a passing SonarQube Cloud quality-gate GitHub Actions job on PRs into `develop` and `main`; the direct coverage gates remain separate from Sonar's quality gate. See [Development and Delivery](development-and-delivery.md).
