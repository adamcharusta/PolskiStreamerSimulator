# M2-C Frontend Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the M2 vertical slice playable in a browser — 18+ gate, character creation, one manual week, a 13-week batch, reload to the same server-confirmed state — with three selectable retro themes, started locally by one `docker compose up` and proven by a Playwright journey in CI.

**Architecture:** A React 19 + Vite client in `source/frontend` talks to the M2-B API only through an Orval-generated TanStack Query/Axios client. The game screen is server-driven: one route renders the stage for the API's `next.kind`; every confirmed step carries a `commandId` that retries reuse. Themes are CSS token sets switched by `data-theme`. The backend gains `GET /v1/career-options`, a polished OpenAPI document and a multi-stage `Dockerfile`; Compose runs SQL Server, the API and Vite.

**Tech Stack:** Node 24.22.0 LTS, React 19.3.1, TypeScript 6.0.3, Vite 8.3.4, TanStack Router 1.170.44 and Query 5.104.2, Axios 1.20.0, Orval 8.43.0, Zustand 5.0.15, React Hook Form 7.90.0 + Zod 4.6.7, Tailwind CSS 4.3.3, `radix-ui` 1.8.1 (owned shadcn/ui-style components), i18next 26.4.5, Vitest 5.0.4 + Testing Library + MSW 3.0.3, Playwright 1.64.1 + `@axe-core/playwright` 4.13.0, ESLint 9.39.5; backend .NET 10 as in M2-B.

**Spec:** [`docs/superpowers/specs/2026-10-15-m2c-frontend-slice-design.md`](../specs/2026-10-15-m2c-frontend-slice-design.md)

## Global Constraints

- Run `dotnet` commands from `source/backend` and `npm` commands from `source/frontend` ([AGENTS.md](../../../AGENTS.md)); Compose commands from the repository root.
- Exact frontend versions (pinned in `source/frontend/package.json`, no ranges): `react`/`react-dom` 19.3.1, `@types/react`/`@types/react-dom` 19.3.0, `@types/node` 24.19.2, `typescript` 6.0.3, `vite` 8.3.4, `@vitejs/plugin-react` 6.1.2, `@tanstack/react-router` 1.170.44, `@tanstack/react-query` 5.104.2, `axios` 1.20.0, `orval` 8.43.0, `zustand` 5.0.15, `react-hook-form` 7.90.0, `zod` 4.6.7, `@hookform/resolvers` 5.9.1, `tailwindcss`/`@tailwindcss/vite` 4.3.3, `radix-ui` 1.8.1, `class-variance-authority` 0.7.1, `clsx` 2.1.1, `tailwind-merge` 3.7.0, `i18next` 26.4.5, `react-i18next` 17.0.16, `@fontsource/anton`/`@fontsource/comic-relief` 5.3.0 (`@fontsource/comic-neue` superseded by TD-107: it has no Polish letters), `vitest`/`@vitest/coverage-v8` 5.0.4, `jsdom` 30.1.2, `@testing-library/react` 16.3.3, `@testing-library/dom` 10.4.2, `@testing-library/user-event` 14.6.8, `@testing-library/jest-dom` 7.0.1, `msw` 3.0.3, `@playwright/test` 1.64.1, `@axe-core/playwright` 4.13.0, `eslint`/`@eslint/js` 9.39.5, `typescript-eslint` 8.71.2, `globals` 17.13.0, `eslint-plugin-react` 7.37.5, `eslint-plugin-react-hooks` 7.1.1, `eslint-plugin-react-refresh` 0.5.7, `eslint-plugin-jsx-a11y` 6.10.2, `eslint-plugin-import-x` 4.17.1, `eslint-import-resolver-typescript` 4.4.5, `eslint-plugin-unused-imports` 4.4.1, `eslint-plugin-simple-import-sort` 14.0.0, `eslint-plugin-unicorn` 65.0.1, `@vitest/eslint-plugin` 1.6.27, `eslint-config-prettier` 10.1.8, `prettier` 3.9.10. ESLint stays on 9.39.5 because `eslint-plugin-react` and `eslint-plugin-jsx-a11y` from the owner's package set do not support ESLint 10; TypeScript stays on 6.0.3 because `typescript-eslint` supports `<6.1.0` (TD-98).
- Node `24.22.0` (`source/frontend/.nvmrc`; `engines.node` `>=24.15.0 <25`); Docker images `node:24.22.0-alpine`, `mcr.microsoft.com/dotnet/sdk:10.0.402-noble`, `mcr.microsoft.com/dotnet/aspnet:10.0.13-noble`, `mcr.microsoft.com/mssql/server:2025-RTM-ubuntu-22.04`.
- Routes: `/`, `/nowa-kariera`, `/kariera/$careerId`. API calls only through `src/api/generated` (never edited by hand) and the mutator `src/api/http.ts`.
- Local storage keys: `pss.ageGate.v1` (`"confirmed"`), `pss.onlineNotice.v1` (`"dismissed"`), `pss.theme` (`overlay` | `win98` | `web2005`, default `overlay`), `pss.careers.v1` (JSON array of `{ careerId, nickname, secret, createdAt }`).
- Every confirmed step or batch gets `commandId = crypto.randomUUID()` once; a retry sends the identical request. Automatic retries only for network errors and timeouts, at most 2. `POST /v1/careers` is an exception: it has no idempotency key, so Task 9 disables its automatic retries (TD-106).
- All player-visible copy is Polish and comes from `src/locales/pl/translation.json` or from API content (`textPl`, `labelPl`); no hard-coded UI strings in components. Documentation stays English.
- WCAG 2.2 AA in every theme; 320 CSS pixels without horizontal scrolling; gains/losses always carry a sign and text; nicknames render as text.
- Test-driven development ([TD-58](../../technical-decisions.md)): every production change starts with a failing test. Frontend line coverage ≥ 80% on its own; SonarQube Cloud zero open issues (TD-96).
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR bodies end with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`. Pushes, PRs, live ruleset changes, issue edits and merges each need the owner's explicit yes immediately before.

## Review Focus

- **Corrupted or foreign local storage.** A malformed `pss.careers.v1`, an unknown `pss.theme` or a missing secret must not crash the app: bad entries read as absent, the theme falls back to `overlay`, a career without a secret shows "not found" without a request (Task 7 `ignores malformed known-careers storage`, `shows the empty state for malformed storage instead of failing`; Task 6 `falls back to the overlay theme for an unknown stored value`; Task 10 `treats a career without a stored secret as not found and asks nothing`).
- **Unavailable browser storage (TD-103).** A denied read uses the `overlay` theme and shows the 18+ gate; a denied write still applies an explicit theme or gate choice for the current page. Tasks 6 and 8 add regression tests for these cases.
- **Career options request failure (TD-104).** Task 9 shows a localized error and retry action when `GET /v1/career-options` fails, instead of leaving the creation screen in a loading state.
- **Unsaved career secret (TD-105).** Task 9 checks that browser storage can save the secret before sending `POST /v1/careers`; if it cannot, the form shows a localized error and no career is created.
- **Lost response after a confirmed step.** A network error after the server committed must not roll again: retries reuse the `commandId` (Task 10 `retries a network failure with the same command id`, `keeps the intent for a manual retry once the automatic retries fail`; Task 12 `offers a retry with the same command id after a lost connection`).
- **Double activation.** A fast double click on a primary action sends one request (Task 10 `ignores a second run while the first is pending`; Task 12 `starts the week once on a double click`; Task 13 `confirms a choice once on a double click`; Task 15 `runs one batch on a double click and says it is running`).
- **Another tab moved the career.** `409 stale-version` refetches and shows the fresh step with the reason instead of a dead end (Task 10 `refetches the career after a stale version`; Task 14 `refetches the career after a stale version and says why`).
- **Gate bypass.** Escape, an outside click, browser back and a deep link to `/kariera/...` never show game content or send a request before confirmation (Task 8 `does not open the game on Escape, an outside click or navigating back`, `shows the gate first on a deep link and makes no request`).

## Delivery

Four PRs into `develop`, each cut from the updated `develop` after the previous merge, reviewed by one fresh reviewer before its merge, merged only with zero SonarQube Cloud issues on the PR and green required checks:

| PR | Branch | Tasks | Refers to |
| --- | --- | --- | --- |
| 1 | `feature/m2c-08-contract` (holds the spec) | 1–5 | #8, part of #14 |
| 2 | `feature/m2c-11-entry` | 6–9 | #11 |
| 3 | `feature/m2c-12-week` | 10–14 | #12 |
| 4 | `feature/m2c-13-14-batch-stack` | 15–18, then the milestone release | client side of #13, #14 |

Issues stay open until the milestone release reaches `main` (TD-96); PR bodies say `Refs #n`.

Common commands:

- Backend unit run with the gate (from `source/backend`): `dotnet test PolskiStreamerSimulator.slnx -c Release --filter-not-trait "Category=Integration" --coverlet --coverlet-threshold 80 --coverlet-threshold-type line --coverlet-threshold-stat total`
- Backend integration run (Docker Desktop running): `dotnet test PolskiStreamerSimulator.slnx -c Release --filter-trait "Category=Integration"`
- OpenAPI regeneration (from `source/backend`): `dotnet build PolskiStreamerSimulator.slnx -c Release && dotnet run --project WebApp -c Release --no-build -- openapi --document v1 --output openapi/v1.json`
- Frontend checks (from `source/frontend`): `npm run typecheck && npm run lint && npm run format:check && npm run test:coverage`
- One frontend test file: `npx vitest run src/path/File.test.tsx`
- Doc links: `bash .superpowers/sdd/2026-10-13-m2a-delivery-foundation/check-links.sh` → `links OK`

---

# PR 1 — Contract and scaffold (#8, part of #14)

The branch `feature/m2c-08-contract` already exists and holds the spec commit.

### Task 1: Career options endpoint

**Files:**
- Create: `source/backend/Application/Careers/CareerOptions.cs`
- Modify: `source/backend/Application/Careers/CareerViewBuilder.cs` (make the category and equipment view helpers public)
- Modify: `source/backend/WebApp/Careers/CareerEndpoints.cs`
- Test: `source/backend/Tests/Application/Careers/GetCareerOptionsTests.cs`, `source/backend/Tests/WebApp/CareerApiTests.cs`, `source/backend/Tests/WebApp/OpenApiDocumentTests.cs`

**Interfaces:**
- Consumes: `ICurrentContent.Snapshot` (`ContentSnapshot` with `Profiles: IReadOnlyDictionary<Background, StartingProfile>` and `Balance.DifficultyMultiplier`), `Decimals.Of(Fixed)`.
- Produces: `BackgroundOptionView(Background Background, long CashPln, long Followers, decimal Drama, decimal Credibility, decimal Energy, CategoryValuesView Skill, EquipmentView Equipment, decimal ScoreMultiplier)`, `DifficultyOptionView(Difficulty Difficulty, decimal ScoreMultiplier)`, `CareerOptionsView(IReadOnlyList<BackgroundOptionView> Backgrounds, IReadOnlyList<DifficultyOptionView> Difficulties, IReadOnlyList<PronounSet> Pronouns)`, message `GetCareerOptions`, `GetCareerOptionsHandler(ICurrentContent)`; `CareerViewBuilder.Categories(CategoryValues<Fixed>)` and `CareerViewBuilder.Equipment(IReadOnlyDictionary<EquipmentSlot, int>)` become public; HTTP `GET /v1/career-options` with operation ID `getCareerOptions`.

- [ ] **Step 1: Write the failing tests**

`Tests/Application/Careers/GetCareerOptionsTests.cs`:

```csharp
using PolskiStreamerSimulator.Application.Careers;
using PolskiStreamerSimulator.Domain.Careers;
using PolskiStreamerSimulator.Tests.Support;

namespace PolskiStreamerSimulator.Tests.Application.Careers;

public sealed class GetCareerOptionsTests
{
    private static CareerOptionsView Options() => new GetCareerOptionsHandler(new AppTestKit().Current).Handle(new GetCareerOptions());

    [Fact]
    public void Every_background_difficulty_and_pronoun_set_is_offered_in_enum_order()
    {
        var options = Options();

        Assert.Equal(Enum.GetValues<Background>(), options.Backgrounds.Select(b => b.Background));
        Assert.Equal(Enum.GetValues<Difficulty>(), options.Difficulties.Select(d => d.Difficulty));
        Assert.Equal(Enum.GetValues<PronounSet>(), options.Pronouns);
    }

    [Fact]
    public void A_background_shows_its_starting_profile_from_the_current_content()
    {
        var dorm = Options().Backgrounds.Single(b => b.Background == Background.DormRoomNewcomer);

        Assert.Equal((2_000L, 0L, 18m, 0, 1.0m), (dorm.CashPln, dorm.Followers, dorm.Skill.Fps, dorm.Equipment.Microphone, dorm.ScoreMultiplier));
    }

    [Fact]
    public void A_difficulty_shows_its_score_multiplier()
        => Assert.Equal([1.0m, 1.1m], Options().Difficulties.Select(d => d.ScoreMultiplier));
}
```

Add to `Tests/WebApp/CareerApiTests.cs` (the class already has `client` and `Ct`):

```csharp
    [Fact]
    public async Task Career_options_need_no_secret()
    {
        var response = await client.GetAsync("/v1/career-options", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("dormRoomNewcomer", body.GetProperty("backgrounds")[0].GetProperty("background").GetString());
        Assert.Equal(1.1m, body.GetProperty("difficulties")[1].GetProperty("scoreMultiplier").GetDecimal());
        Assert.Equal("nonbinary", body.GetProperty("pronouns")[2].GetString());
    }
```

In `Tests/WebApp/OpenApiDocumentTests.cs`, `The_committed_document_describes_the_career_api`, add `Assert.True(paths.TryGetProperty("/v1/career-options", out _));`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*GetCareerOptionsTests"` → build error (`GetCareerOptionsHandler` missing).

- [ ] **Step 3: Implement**

In `Application/Careers/CareerViewBuilder.cs`, change the two private helpers used by `Stats` to public static methods (the equipment block moves into `Equipment`):

```csharp
    public static CategoryValuesView Categories(CategoryValues<Fixed> v) => new(
        Decimals.Of(v[Category.JustChatting]),
        Decimals.Of(v[Category.Fps]),
        Decimals.Of(v[Category.Moba]),
        Decimals.Of(v[Category.Chess]),
        Decimals.Of(v[Category.Gambling]));

    public static EquipmentView Equipment(IReadOnlyDictionary<EquipmentSlot, int> tiers) => new(
        tiers[EquipmentSlot.Microphone],
        tiers[EquipmentSlot.Camera],
        tiers[EquipmentSlot.Computer],
        tiers[EquipmentSlot.Connection]);
```

and in `Stats` replace the inline `new EquipmentView(...)` with `Equipment(s.Equipment)`.

`Application/Careers/CareerOptions.cs`:

```csharp
using PolskiStreamerSimulator.Application.Content;
using PolskiStreamerSimulator.Domain.Careers;

namespace PolskiStreamerSimulator.Application.Careers;

public sealed record BackgroundOptionView(
    Background Background,
    long CashPln,
    long Followers,
    decimal Drama,
    decimal Credibility,
    decimal Energy,
    CategoryValuesView Skill,
    EquipmentView Equipment,
    decimal ScoreMultiplier);

public sealed record DifficultyOptionView(Difficulty Difficulty, decimal ScoreMultiplier);

/// <summary>What character creation may choose, with the numbers of the content new careers start on.</summary>
public sealed record CareerOptionsView(
    IReadOnlyList<BackgroundOptionView> Backgrounds,
    IReadOnlyList<DifficultyOptionView> Difficulties,
    IReadOnlyList<PronounSet> Pronouns);

public sealed record GetCareerOptions;

public sealed class GetCareerOptionsHandler(ICurrentContent current)
{
    public CareerOptionsView Handle(GetCareerOptions query)
    {
        var content = current.Snapshot;
        var backgrounds = Enum.GetValues<Background>()
            .Select(background => Option(background, content.Profiles[background]))
            .ToList();
        var difficulties = Enum.GetValues<Difficulty>()
            .Select(difficulty => new DifficultyOptionView(difficulty, Decimals.Of(content.Balance.DifficultyMultiplier[difficulty])))
            .ToList();
        return new CareerOptionsView(backgrounds, difficulties, Enum.GetValues<PronounSet>());
    }

    private static BackgroundOptionView Option(Background background, StartingProfile p) => new(
        background,
        p.CashPln,
        p.Followers,
        Decimals.Of(p.Drama),
        Decimals.Of(p.Credibility),
        Decimals.Of(p.Energy),
        CareerViewBuilder.Categories(p.Skill),
        CareerViewBuilder.Equipment(p.Equipment),
        Decimals.Of(p.ScoreMultiplier));
}
```

In `WebApp/Careers/CareerEndpoints.cs`, add:

```csharp
    [WolverineGet("/v1/career-options", OperationId = "getCareerOptions")]
    public static async Task<Ok<CareerOptionsView>> GetOptions(IMessageBus bus, CancellationToken cancellationToken)
        => TypedResults.Ok(await bus.InvokeAsync<CareerOptionsView>(new GetCareerOptions(), cancellationToken));
```

- [ ] **Step 4: Run, regenerate the document and commit**

Run: `--filter-class "*GetCareerOptionsTests"` → 3 passed; `--filter-class "*CareerApiTests"` → PASS. Regenerate the OpenAPI document (common commands), then `--filter-class "*OpenApiDocumentTests"` → PASS. Unit run with the gate → PASS.

```bash
git add source/backend
git commit -m "feat(api): list the starting profiles and difficulties for character creation" -m "Refs #8." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: OpenAPI contract polish and camelCase validation keys

**Files:**
- Create: `source/backend/WebApp/OpenApiContract.cs`
- Modify: `source/backend/WebApp/Program.cs`, `source/backend/WebApp/openapi/v1.json` (regenerated)
- Test: `source/backend/Tests/WebApp/OpenApiDocumentTests.cs`, `source/backend/Tests/WebApp/CareerApiTests.cs`

**Interfaces:**
- Produces: security scheme `careerSecret` (HTTP bearer) on `getCareer`, `submitAction`, `runBatch`; every operation tagged `careers` with a readable `summary` and a `500` response; enum schemas `"type": "string"` without `null`; `ProblemDetails` schema with required `code` and optional `errors`; validation error keys in camelCase (`nickname`, `commandId`); `/health` excluded from the document.

- [ ] **Step 1: Write the failing tests**

Add to `Tests/WebApp/OpenApiDocumentTests.cs`:

```csharp
    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(DocumentPath));

    [Fact]
    public void Career_operations_require_the_bearer_secret_and_every_operation_lists_a_500()
    {
        using var document = Load();
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("careerSecret").GetProperty("scheme").GetString());
        Assert.True(paths.GetProperty("/v1/careers/{id}").GetProperty("get").GetProperty("security")[0].TryGetProperty("careerSecret", out _));
        Assert.False(paths.GetProperty("/v1/careers").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(paths.TryGetProperty("/health", out _));
        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                var op = operation.Value;
                Assert.True(op.GetProperty("responses").TryGetProperty("500", out _), $"{operation.Name} {path.Name} has no 500");
                Assert.Equal("careers", op.GetProperty("tags")[0].GetString());
                Assert.DoesNotContain('_', op.GetProperty("summary").GetString());
            }
        }
    }

    [Fact]
    public void Enums_are_strings_without_null_and_problems_carry_a_code()
    {
        using var document = Load();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var ending = schemas.GetProperty("EndingReason");
        Assert.Equal("string", ending.GetProperty("type").GetString());
        Assert.DoesNotContain(ending.GetProperty("enum").EnumerateArray(), v => v.ValueKind == JsonValueKind.Null);
        var problem = schemas.GetProperty("ProblemDetails");
        Assert.True(problem.GetProperty("properties").TryGetProperty("code", out _));
        Assert.True(problem.GetProperty("properties").TryGetProperty("errors", out _));
        Assert.Contains("code", problem.GetProperty("required").EnumerateArray().Select(v => v.GetString()));
    }
```

Add to `Tests/WebApp/CareerApiTests.cs`:

```csharp
    [Fact]
    public async Task Validation_errors_name_fields_in_camel_case()
    {
        var response = await client.PostAsJsonAsync("/v1/careers", new { nickname = "x", background = "dormRoomNewcomer", difficulty = "normal", pronouns = "woman" }, Ct);

        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("nickname", out _), errors.GetRawText());
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test PolskiStreamerSimulator.slnx --filter-class "*OpenApiDocumentTests" --filter-class "*CareerApiTests"` → the two document tests and the camelCase test fail.

- [ ] **Step 3: Implement**

`WebApp/OpenApiContract.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PolskiStreamerSimulator.WebApp;

/// <summary>
/// Shapes the OpenAPI document for the generated client: the bearer career secret, string enums, Problem Details with
/// their code, a documented 500 and readable operation names (TD-101).
/// </summary>
public static class OpenApiContract
{
    public const string SecurityScheme = "careerSecret";

    private const string Tag = "careers";

    private static readonly Dictionary<string, (string Summary, bool Secured)> Operations = new(StringComparer.Ordinal)
    {
        ["createCareer"] = ("Create a career and receive its secret once", false),
        ["getCareerOptions"] = ("List the starting profiles and difficulties", false),
        ["getCareer"] = ("Read a career and its next step", true),
        ["submitAction"] = ("Settle one manual step", true),
        ["runBatch"] = ("Play up to 13 weeks automatically", true),
    };

    public static void Configure(OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo { Title = "Polski Streamer Simulator API", Version = "v1" };
            document.Tags = new HashSet<OpenApiTag> { new() { Name = Tag, Description = "Anonymous careers: creation, reading, steps and batches." } };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SecurityScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "The career secret returned once by createCareer, sent as Authorization: Bearer <secret>.",
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var id = operation.OperationId ?? string.Empty;
            var (summary, secured) = Operations.TryGetValue(id, out var known) ? known : (id, false);
            operation.Summary = summary;
            operation.Description = null;
            operation.Tags = new HashSet<OpenApiTagReference> { new(Tag, context.Document) };
            if (secured)
            {
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SecurityScheme, context.Document)] = [] }];
            }

            operation.Responses ??= new OpenApiResponses();
            operation.Responses["500"] = new OpenApiResponse
            {
                Description = "Internal Server Error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(nameof(ProblemDetails), context.Document) },
                },
            };
            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, context, _) =>
        {
            var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (type.IsEnum)
            {
                // Nullability lives on the referencing property (oneOf with null), never in the shared enum.
                schema.Type = JsonSchemaType.String;
                schema.Enum = schema.Enum?.Where(value => value is not null).ToList();
            }

            if (type == typeof(ProblemDetails))
            {
                schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                schema.Properties["code"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable error code, for example stale-version." };
                schema.Properties["errors"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Description = "Field errors of validation-failed, keyed by camelCase field name.",
                    AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
                };
                schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                schema.Required.Add("code");
            }

            return Task.CompletedTask;
        });
    }
}
```

In `WebApp/Program.cs`:
- replace `builder.Services.AddOpenApi();` with `builder.Services.AddOpenApi(OpenApiContract.Configure);`;
- before `var builder = ...`, add `ValidatorOptions.Global.PropertyNameResolver = (_, member, _) => member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);` with `using System.Text.Json;` and `using FluentValidation;` and the comment `// Field errors use the contract's camelCase names, so clients map them onto their fields.`;
- change `app.MapGet("/health", () => "ok");` to `app.MapGet("/health", () => "ok").ExcludeFromDescription();`.

If a Microsoft.OpenApi member name differs in the restored package (for example `OpenApiTagReference` or `Document` on the transformer context), keep the behaviour and adjust only the member name; the tests above are the contract.

- [ ] **Step 4: Regenerate, run and commit**

Regenerate the OpenAPI document (common commands). Run: `--filter-class "*OpenApiDocumentTests" --filter-class "*CareerApiTests" --filter-class "*ApiProblemsTests"` → PASS; unit run with the gate → PASS; `dotnet format PolskiStreamerSimulator.slnx --verify-no-changes` → exit 0.

```bash
git add source/backend
git commit -m "feat(api): shape the OpenAPI document for the generated client" -m "Bearer career secret, string enums without null, Problem Details with code and errors, a documented 500, readable summaries and camelCase validation keys. Refs #8." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 3: Frontend scaffold with lint, format and component tests

**Files:**
- Create: `source/frontend/package.json`, `source/frontend/package-lock.json` (by `npm install`), `source/frontend/.nvmrc`, `source/frontend/tsconfig.json`, `source/frontend/tsconfig.node.json`, `source/frontend/vite.config.ts`, `source/frontend/vitest.config.ts`, `source/frontend/eslint.config.mjs`, `source/frontend/index.html`, `source/frontend/src/main.tsx`, `source/frontend/src/App.tsx`, `source/frontend/src/styles/index.css`, `source/frontend/test/setup.ts`
- Modify: `source/frontend/.prettierignore`
- Test: `source/frontend/src/App.test.tsx`

**Interfaces:**
- Produces: npm scripts `dev`, `build`, `typecheck`, `lint`, `format`, `format:check`, `test`, `test:coverage`, `api:generate`, `e2e`; the `@/` import alias for `src/`; `test/setup.ts` (jest-dom matchers, cleanup, `localStorage.clear()` after each test). Task 4 adds MSW to the setup.

- [ ] **Step 1: Create the project files**

`source/frontend/.nvmrc`:

```text
24.22.0
```

`source/frontend/package.json`:

```json
{
  "name": "polski-streamer-simulator-frontend",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "engines": {
    "node": ">=24.15.0 <25"
  },
  "scripts": {
    "dev": "vite",
    "build": "npm run typecheck && vite build",
    "typecheck": "tsc -p tsconfig.json && tsc -p tsconfig.node.json",
    "lint": "eslint . --max-warnings 0",
    "format": "prettier --write .",
    "format:check": "prettier --check .",
    "test": "vitest run",
    "test:coverage": "vitest run --coverage",
    "api:generate": "orval --config orval.config.ts",
    "e2e": "playwright test"
  },
  "dependencies": {
    "@fontsource/anton": "5.3.0",
    "@fontsource/comic-neue": "5.3.0",
    "@hookform/resolvers": "5.9.1",
    "@tanstack/react-query": "5.104.2",
    "@tanstack/react-router": "1.170.44",
    "axios": "1.20.0",
    "class-variance-authority": "0.7.1",
    "clsx": "2.1.1",
    "i18next": "26.4.5",
    "radix-ui": "1.8.1",
    "react": "19.3.1",
    "react-dom": "19.3.1",
    "react-hook-form": "7.90.0",
    "react-i18next": "17.0.16",
    "tailwind-merge": "3.7.0",
    "zod": "4.6.7",
    "zustand": "5.0.15"
  },
  "devDependencies": {
    "@axe-core/playwright": "4.13.0",
    "@eslint/js": "9.39.5",
    "@playwright/test": "1.64.1",
    "@tailwindcss/vite": "4.3.3",
    "@testing-library/dom": "10.4.2",
    "@testing-library/jest-dom": "7.0.1",
    "@testing-library/react": "16.3.3",
    "@testing-library/user-event": "14.6.8",
    "@types/node": "24.19.2",
    "@types/react": "19.3.0",
    "@types/react-dom": "19.3.0",
    "@vitejs/plugin-react": "6.1.2",
    "@vitest/coverage-v8": "5.0.4",
    "@vitest/eslint-plugin": "1.6.27",
    "eslint": "9.39.5",
    "eslint-config-prettier": "10.1.8",
    "eslint-import-resolver-typescript": "4.4.5",
    "eslint-plugin-import-x": "4.17.1",
    "eslint-plugin-jsx-a11y": "6.10.2",
    "eslint-plugin-react": "7.37.5",
    "eslint-plugin-react-hooks": "7.1.1",
    "eslint-plugin-react-refresh": "0.5.7",
    "eslint-plugin-simple-import-sort": "14.0.0",
    "eslint-plugin-unicorn": "65.0.1",
    "eslint-plugin-unused-imports": "4.4.1",
    "globals": "17.13.0",
    "jsdom": "30.1.2",
    "msw": "3.0.3",
    "orval": "8.43.0",
    "prettier": "3.9.10",
    "tailwindcss": "4.3.3",
    "typescript": "6.0.3",
    "typescript-eslint": "8.71.2",
    "vite": "8.3.4",
    "vitest": "5.0.4"
  }
}
```

`source/frontend/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2023",
    "lib": ["ES2023", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "noFallthroughCasesInSwitch": true,
    "verbatimModuleSyntax": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["vite/client"],
    "paths": { "@/*": ["./src/*"] }
  },
  "include": ["src", "test"]
}
```

`source/frontend/tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "target": "ES2023",
    "lib": ["ES2023"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "verbatimModuleSyntax": true,
    "resolveJsonModule": true,
    "skipLibCheck": true,
    "noEmit": true,
    "types": ["node"]
  },
  "include": ["vite.config.ts", "vitest.config.ts", "orval.config.ts", "playwright.config.ts", "e2e"]
}
```

`source/frontend/vite.config.ts`:

```ts
import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Compose sets API_PROXY_TARGET to the api service and VITE_POLLING for Docker Desktop on Windows (TD-99).
const apiTarget = process.env.API_PROXY_TARGET ?? 'http://localhost:8080';
const polling = process.env.VITE_POLLING === 'true';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('src', import.meta.url)) } },
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/v1': { target: apiTarget } },
    watch: polling ? { usePolling: true, interval: 300 } : undefined,
  },
});
```

`source/frontend/vitest.config.ts`:

```ts
import { defineConfig, mergeConfig } from 'vitest/config';

import viteConfig from './vite.config';

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      setupFiles: ['./test/setup.ts'],
      include: ['src/**/*.test.{ts,tsx}'],
      restoreMocks: true,
      coverage: {
        provider: 'v8',
        // LCOV paths relative to the repository root, SonarQube Cloud's base directory (TD-100).
        reporter: ['text-summary', ['lcov', { projectRoot: '../..' }]],
        include: ['src/**/*.{ts,tsx}'],
        exclude: ['src/api/generated/**', 'src/main.tsx', 'src/**/*.test.{ts,tsx}'],
        thresholds: { lines: 80 },
      },
    },
  }),
);
```

`source/frontend/eslint.config.mjs`:

```js
import js from '@eslint/js';
import vitest from '@vitest/eslint-plugin';
import prettier from 'eslint-config-prettier';
import importX from 'eslint-plugin-import-x';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import react from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import simpleImportSort from 'eslint-plugin-simple-import-sort';
import unicorn from 'eslint-plugin-unicorn';
import { createTypeScriptImportResolver } from 'eslint-import-resolver-typescript';
import unusedImports from 'eslint-plugin-unused-imports';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// Owner package set (repository standards). Orval output is excluded: regeneration drift checks it instead.
export default tseslint.config(
  {
    ignores: ['dist/', 'coverage/', 'playwright-report/', 'test-results/', 'src/api/generated/', 'node_modules/'],
  },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      parserOptions: { projectService: { allowDefaultProject: ['eslint.config.mjs'] }, tsconfigRootDir: import.meta.dirname },
    },
    settings: {
      'import-x/resolver-next': [createTypeScriptImportResolver({ project: ['./tsconfig.json', './tsconfig.node.json'] })],
    },
  },
  importX.flatConfigs.recommended,
  importX.flatConfigs.typescript,
  unicorn.configs.recommended,
  {
    plugins: { 'simple-import-sort': simpleImportSort, 'unused-imports': unusedImports },
    rules: {
      'simple-import-sort/imports': 'error',
      'simple-import-sort/exports': 'error',
      'import-x/order': 'off',
      'no-unused-vars': 'off',
      '@typescript-eslint/no-unused-vars': 'off',
      'unused-imports/no-unused-imports': 'error',
      'unused-imports/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      'unicorn/prevent-abbreviations': 'off',
      'unicorn/filename-case': 'off',
      'unicorn/no-null': 'off',
    },
  },
  {
    files: ['src/**/*.{ts,tsx}', 'test/**/*.{ts,tsx}'],
    ...react.configs.flat.recommended,
    ...react.configs.flat['jsx-runtime'],
    languageOptions: { ...react.configs.flat.recommended.languageOptions, globals: globals.browser },
    settings: { react: { version: 'detect' } },
  },
  // TypeScript types replace prop-types.
  { files: ['src/**/*.{ts,tsx}', 'test/**/*.{ts,tsx}'], rules: { 'react/prop-types': 'off' } },
  { files: ['src/**/*.{ts,tsx}'], ...jsxA11y.flatConfigs.recommended },
  { files: ['src/**/*.{ts,tsx}'], ...reactHooks.configs.flat.recommended },
  { files: ['src/**/*.tsx'], ...reactRefresh.configs.vite },
  {
    files: ['*.config.{ts,mjs}', 'e2e/**/*.ts'],
    languageOptions: { globals: globals.node },
  },
  {
    files: ['src/**/*.test.{ts,tsx}', 'test/**/*.{ts,tsx}'],
    ...vitest.configs.recommended,
  },
  prettier,
);
```

Replace `source/frontend/.prettierignore` with:

```text
node_modules/
dist/
build/
coverage/
playwright-report/
test-results/
src/api/generated/
package-lock.json
```

`source/frontend/index.html`:

```html
<!doctype html>
<html lang="pl" data-theme="overlay">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Polski Streamer Simulator</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`source/frontend/src/styles/index.css`:

```css
@import 'tailwindcss';
```

- [ ] **Step 2: Install and write the failing test**

Run: `npm install` (creates `package-lock.json`; no version may change).

`source/frontend/test/setup.ts`:

```ts
import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

afterEach(() => {
  cleanup();
  localStorage.clear();
});
```

`source/frontend/src/App.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';

import { App } from './App';

it('shows the game title', () => {
  render(<App />);

  expect(screen.getByRole('heading', { level: 1, name: 'Polski Streamer Simulator' })).toBeInTheDocument();
});
```

Run: `npx vitest run src/App.test.tsx` → FAIL (`./App` not found).

- [ ] **Step 3: Implement**

`source/frontend/src/App.tsx`:

```tsx
export function App() {
  return <h1>Polski Streamer Simulator</h1>;
}
```

`source/frontend/src/main.tsx`:

```tsx
import './styles/index.css';

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from './App';

const root = document.querySelector('#root');
if (!root) {
  throw new Error('The #root element is missing.');
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

(Task 7 replaces the title with i18n copy and the router.)

- [ ] **Step 4: Run every check and commit**

Run: `npx vitest run` → 1 passed; `npm run typecheck` → exit 0; `npm run lint` → exit 0; `npm run format:check` → exit 0 (run `npm run format` first if Prettier reformats a new file); `npm run build` → `dist/` written.

```bash
git add source/frontend
git commit -m "build(frontend): scaffold React 19, Vite, TypeScript, ESLint, Prettier and Vitest" -m "Refs #8, #14." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 4: Generated API client and the HTTP mutator

**Files:**
- Create: `source/frontend/orval.config.ts`, `source/frontend/src/api/http.ts`, `source/frontend/test/server.ts`, `source/frontend/src/api/generated/**` (by `npm run api:generate`)
- Modify: `source/frontend/test/setup.ts`
- Test: `source/frontend/src/api/http.test.ts`

**Interfaces:**
- Consumes: `source/backend/WebApp/openapi/v1.json` (Task 2).
- Produces: `apiRequest<T>(config, options?)` (Orval mutator; merges headers; throws `ApiRequestError`); `class ApiRequestError extends Error { status: number | null; code: string; detail: string | null; errors: Record<string, string[]>; get isNetwork(): boolean }`; `type ErrorType<_E> = ApiRequestError`; `isApiError(e): e is ApiRequestError`; generated `src/api/generated/game.ts` (functions `createCareer`, `getCareer`, `getCareerOptions`, `submitAction`, `runBatch`, hooks `useGetCareer`, `useGetCareerOptions`, `useCreateCareer`, query keys `getGetCareerQueryKey(id)`) and `src/api/generated/game.schemas.ts` (types `CareerView`, `ActionRequest`, `ActionOutcome`, `BatchRequest`, `BatchOutcome`, `CareerOptionsView`, `WeekReportView`, `RollView`, `ReasonView`, enums as `const` objects such as `Category`, `MainAction`, `NextStepKind`, `BatchStopReason`); `test/server.ts` exporting `server` (MSW `setupServer()` with no default handlers).

- [ ] **Step 1: Generate the client**

`source/frontend/orval.config.ts`:

```ts
import { defineConfig } from 'orval';

// Generated from the committed API contract; CI regenerates it and fails on any difference (TD-92, TD-100).
export default defineConfig({
  game: {
    input: { target: '../backend/WebApp/openapi/v1.json' },
    output: {
      mode: 'split',
      target: 'src/api/generated/game.ts',
      client: 'react-query',
      httpClient: 'axios',
      clean: true,
      override: { mutator: { path: 'src/api/http.ts', name: 'apiRequest' } },
    },
  },
});
```

Write the mutator first (Step 3 code) as a stub that only exports `apiRequest` and `ErrorType`, then run `npm run api:generate`. Expected: `src/api/generated/game.ts` and `src/api/generated/game.schemas.ts`. Inspect `game.schemas.ts`: `ProblemDetails` has `code: string`, enums have no `null`, `CareerOptionsView` exists.

- [ ] **Step 2: Write the failing tests**

`source/frontend/test/server.ts`:

```ts
import { setupServer } from 'msw/node';

/** HTTP-level API double for component tests; each test adds its handlers with `server.use`. */
export const server = setupServer();
```

Replace `source/frontend/test/setup.ts` with:

```ts
import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';

import { server } from './server';

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' });
});

afterEach(() => {
  server.resetHandlers();
  cleanup();
  localStorage.clear();
});

afterAll(() => {
  server.close();
});
```

`source/frontend/src/api/http.test.ts`:

```ts
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { server } from '../../test/server';
import { ApiRequestError, apiRequest, isApiError } from './http';

/** The rejection of a request that must fail. */
const failure = (request: Promise<unknown>): Promise<unknown> => request.then(() => null, (error: unknown) => error);

describe('apiRequest', () => {
  it('returns the body and keeps the content type when a career adds its secret', async () => {
    let seen: { auth: string | null; type: string | null } = { auth: null, type: null };
    server.use(
      http.post('/v1/careers/abc/actions', ({ request }) => {
        seen = { auth: request.headers.get('authorization'), type: request.headers.get('content-type') };
        return HttpResponse.json({ ok: true });
      }),
    );

    const body = await apiRequest<{ ok: boolean }>(
      { url: '/v1/careers/abc/actions', method: 'POST', headers: { 'Content-Type': 'application/json' }, data: {} },
      { headers: { Authorization: 'Bearer s3cret' } },
    );

    expect(body).toEqual({ ok: true });
    expect(seen).toEqual({ auth: 'Bearer s3cret', type: 'application/json' });
  });

  it('turns Problem Details into a coded error with field errors', async () => {
    server.use(
      http.post('/v1/careers', () =>
        HttpResponse.json(
          { status: 400, code: 'validation-failed', detail: null, errors: { nickname: ['too short'] } },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await failure(apiRequest({ url: '/v1/careers', method: 'POST', data: {} }));

    expect(isApiError(error)).toBe(true);
    expect(error).toMatchObject({ status: 400, code: 'validation-failed', errors: { nickname: ['too short'] }, isNetwork: false });
  });

  it('gives a body without a code a code from its status', async () => {
    server.use(http.get('/v1/careers/x', () => new HttpResponse('nope', { status: 502 })));

    const error = (await failure(apiRequest({ url: '/v1/careers/x', method: 'GET' }))) as ApiRequestError;

    expect(error.code).toBe('http-502');
  });

  it('marks a failed connection as a network error', async () => {
    server.use(http.get('/v1/careers/x', () => HttpResponse.error()));

    const error = (await failure(apiRequest({ url: '/v1/careers/x', method: 'GET' }))) as ApiRequestError;

    expect(error).toMatchObject({ status: null, code: 'network-error', isNetwork: true });
  });
});
```

Run: `npx vitest run src/api/http.test.ts` → FAIL (stub has no `ApiRequestError`).

- [ ] **Step 3: Implement the mutator**

`source/frontend/src/api/http.ts`:

```ts
import axios, { type AxiosRequestConfig } from 'axios';

/** One HTTP client for the API: relative URLs (Vite proxies /v1 locally; one origin in production). */
export const http = axios.create({ timeout: 30_000 });

/** An API failure: `status` is null when the request never got a response (offline, timeout). */
export class ApiRequestError extends Error {
  constructor(
    readonly status: number | null,
    readonly code: string,
    readonly detail: string | null,
    readonly errors: Record<string, string[]>,
  ) {
    super(`${code}${status === null ? '' : ` (${status})`}`);
    this.name = 'ApiRequestError';
  }

  get isNetwork(): boolean {
    return this.status === null;
  }
}

/** Orval types every hook error with this alias; the mutator only ever rejects with ApiRequestError. */
export type ErrorType<_E> = ApiRequestError;

export function isApiError(error: unknown): error is ApiRequestError {
  return error instanceof ApiRequestError;
}

interface ProblemBody {
  code?: unknown;
  detail?: unknown;
  errors?: unknown;
}

function toApiError(error: unknown): ApiRequestError {
  if (!axios.isAxiosError(error) || !error.response) {
    return new ApiRequestError(null, 'network-error', null, {});
  }

  const status = error.response.status;
  const body = (typeof error.response.data === 'object' && error.response.data !== null ? error.response.data : {}) as ProblemBody;
  const code = typeof body.code === 'string' ? body.code : `http-${status}`;
  const detail = typeof body.detail === 'string' ? body.detail : null;
  const errors = typeof body.errors === 'object' && body.errors !== null ? (body.errors as Record<string, string[]>) : {};
  return new ApiRequestError(status, code, detail, errors);
}

/** Orval mutator: per-call options (a career's Authorization header) merge into the generated request. */
export async function apiRequest<T>(config: AxiosRequestConfig, options?: AxiosRequestConfig): Promise<T> {
  try {
    const response = await http.request<T>({ ...config, ...options, headers: { ...config.headers, ...options?.headers } });
    return response.data;
  } catch (error) {
    throw toApiError(error);
  }
}
```

Run `npm run api:generate` again (the mutator's exports did not change, so the output stays the same).

- [ ] **Step 4: Run every check and commit**

Run: `npx vitest run` → all passed; `npm run typecheck`, `npm run lint`, `npm run format:check` → exit 0; `npm run api:generate && git status --porcelain -- src/api/generated` → empty after the first commit of the folder.

```bash
git add source/frontend
git commit -m "feat(frontend): generate the API client with Orval behind one Axios mutator" -m "Refs #8." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 5: CI frontend job, SonarQube Cloud for TypeScript, Lefthook, decisions and PR 1

**Files:**
- Modify: `.github/workflows/ci.yml`, `.github/rulesets/main.json`, `.github/rulesets/develop.json`, `lefthook.yml`
- Modify: `docs/technical-decisions.md`, `docs/game-design-document.md`, `docs/validation-and-delivery.md`, `docs/technology-stack.md`, `docs/repository-standards.md`, `docs/development-and-delivery.md`, `docs/technical-architecture.md`, `docs/requirements-traceability.md`, `README.md`

- [ ] **Step 1: Add the `frontend` job**

In `.github/workflows/ci.yml`, after the `backend-integration` job, add:

```yaml
  frontend:
    name: frontend
    runs-on: ubuntu-latest
    timeout-minutes: 15
    defaults:
      run:
        working-directory: source/frontend
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-node@949feb2413d6458794dcd2491c4babbbce0c15c1 # v7.1.0
        with:
          node-version-file: source/frontend/.nvmrc
          cache: npm
          cache-dependency-path: source/frontend/package-lock.json
      - name: Install
        run: npm ci --ignore-scripts
      # The client must match the committed contract; untracked output counts as drift too.
      - name: Verify generated API client
        run: |
          npm run api:generate
          if [ -n "$(git status --porcelain -- src/api/generated)" ]; then
            git status --porcelain -- src/api/generated
            echo "::error::src/api/generated differs from the OpenAPI document. Run npm run api:generate and commit the result."
            exit 1
          fi
      - name: Typecheck
        run: npm run typecheck
      - name: Lint
        run: npm run lint
      - name: Check formatting
        run: npm run format:check
      - name: Test with coverage gate
        run: npm run test:coverage
      - uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        if: always()
        with:
          name: frontend-coverage
          path: source/frontend/coverage/lcov.info
          if-no-files-found: warn
```

- [ ] **Step 2: Analyse TypeScript in the `sonar` job**

In the `sonar` job:
1. After `actions/setup-dotnet`, add the same `actions/setup-node` step as above, then:

```yaml
      - name: Install frontend
        working-directory: source/frontend
        run: npm ci --ignore-scripts
```

2. In `Begin analysis`, add these lines to the `dotnet sonarscanner begin` arguments (the base directory moves to the repository root so the TypeScript source is in scope):

```text
          /d:sonar.projectBaseDir="$GITHUB_WORKSPACE"
          /d:sonar.javascript.lcov.reportPaths="$GITHUB_WORKSPACE/source/frontend/coverage/lcov.info"
```

and extend the two existing properties to:

```text
          /d:sonar.coverage.exclusions="**/Tools/BatchSimulator/Program.cs,**/*.g.cs,**/Persistence/Migrations/**,source/frontend/src/main.tsx,source/frontend/src/api/generated/**"
          /d:sonar.exclusions="**/Tests/Golden/**,**/Persistence/Migrations/**,source/frontend/src/api/generated/**,source/frontend/**/*.test.ts,source/frontend/**/*.test.tsx,source/frontend/test/**,source/frontend/e2e/**,**/node_modules/**,**/coverage/**,**/dist/**"
```

3. After `Require OpenCover report`, add:

```yaml
      - name: Test frontend with LCOV coverage
        working-directory: source/frontend
        run: npm run test:coverage
```

- [ ] **Step 3: Require the check, add Lefthook commands**

In `.github/rulesets/main.json` and `.github/rulesets/develop.json`, add `{ "context": "frontend" }` to `required_status_checks` next to `backend-integration` (keep the existing entries and their `integration_id` fields as they are).

In `lefthook.yml`, append to `pre-commit.jobs`:

```yaml
    - name: frontend-lint
      root: "source/frontend/"
      glob: "source/frontend/**/*.{ts,tsx,mjs}"
      # Same rule as backend-format: a fix the developer has not reviewed fails the commit.
      run: npx eslint --fix --max-warnings 0 --no-warn-ignored {staged_files} && git diff --exit-code -- {staged_files}
    - name: frontend-tests
      glob:
        - "source/frontend/*"
        - "source/frontend/**/*"
      run: cd source/frontend && npm test
```

Validate: `actionlint .github/workflows/ci.yml` (the M2-A helper path `/c/Users/charu/AppData/Local/Microsoft/WinGet/Packages/rhysd.actionlint_Microsoft.Winget.Source_8wekyb3d8bbwe/actionlint.exe`) → no output; `python .superpowers/sdd/2026-10-13-m2a-delivery-foundation/validate-rulesets.py` → OK.

- [ ] **Step 4: Record the decisions and update the documentation**

`docs/technical-decisions.md`, after TD-97:

- **TD-98** — Decision: the frontend is a feature-based React 19.3.1 + TypeScript 6.0.3 + Vite 8.3.4 app with code-based TanStack Router routes, TanStack Query, an Orval 8.43.0 client in `split` mode behind one Axios mutator, Zustand only for transient UI state, React Hook Form + Zod, i18next (`pl`), Tailwind CSS 4 with owned `radix-ui` components, self-hosted OFL fonts and MSW 3 in component tests; ESLint stays on 9.39.5 and TypeScript on 6.0.3 for plugin compatibility. Source: Agent proposal accepted by the owner, 2026-10-15 (M2-C design). Previous plan: the technology stack named the libraries without versions, test doubles or routing style. Reason: one verified compatible version set; MSW exercises the real client; ESLint 10 breaks the owner's React and accessibility plugins. Impact: revisit ESLint 10 when `eslint-plugin-react` and `eslint-plugin-jsx-a11y` support it (owner choice of packages); React 19.3.1 is a patch above the owner's 19.3.0.
- **TD-99** — Decision: a root `compose.yaml` for local development starts SQL Server (the image the tests pin), the API built from the multi-stage `source/backend/WebApp/Dockerfile` in Development mode (migrations at startup) and Vite in a Node container with the frontend source mounted, file-watch polling and a `/v1` proxy; the SA password comes from an ignored root `.env` documented by `.env.example`, and Compose fails without it. Source: Owner decision, 2026-10-15 (M2-C design). Previous plan: TD-50 required Compose without a layout; the M2-B note ran the API against a hand-started SQL Server. Reason: one command for the whole slice; M6 reuses the `Dockerfile`. Impact: delivered in PR 4; Docker Desktop on Windows needs polling and about 2 GB of memory for SQL Server.
- **TD-100** — Decision: add the required CI job `frontend` (Orval drift, typecheck, ESLint, Prettier, Vitest with an 80% line gate), analyse `source/frontend/src` in the existing SonarQube Cloud project through SonarScanner for .NET with the repository root as base directory and Vitest LCOV coverage, add Lefthook frontend lint and tests, and deliver M2-C as four PRs; PR 4 adds the required `frontend-e2e` job. Source: Owner decision, 2026-10-15 (M2-C design; the four-PR split is an agent proposal accepted with it). Previous plan: TD-86 listed a future frontend job; TD-73 planned one SonarQube Cloud project for both trees. Reason: the frontend needs the same gates as the backend. Impact: `frontend` becomes a required check on `main` and `develop`.
- **TD-101** — Decision: add `GET /v1/career-options` (starting profiles and difficulty multipliers of the current content, no secret) and shape the OpenAPI document for the generated client (bearer `careerSecret`, string enums without `null`, Problem Details with `code` and `errors`, a documented `500`, readable summaries, `/health` excluded); validation error keys are camelCase. Source: Agent proposal accepted by the owner, 2026-10-15. Previous plan: the creation screen had no data source for profile numbers; the review of #55 and #61 listed the document gaps (checklist on #8). Reason: no duplicated content numbers in the client and a client generated without patches. Impact: the OpenAPI document changes; existing endpoints behave the same except the camelCase error keys.

`docs/game-design-document.md` decision log, two rows dated 2026-10-15:
- "Offer three selectable visual themes — a cheap OBS overlay from old streams (default), Windows 98 shareware and a 2005 fan site — with a deliberately amateur look and sound usability; the theme changes presentation only." Source "Owner decision (M2-C brainstorming)".
- "Keep the M2 slice on the M1 content; the representative events for each adult theme and the tone check with players move to M3, where the full content schema arrives." Source "Owner decision (M2-C brainstorming)".

`docs/validation-and-delivery.md` stage 2 (*Vertical slice*): replace "and one representative event from each adult theme. Validate clarity and tone with players." with "The representative events for each adult theme and the tone check with players move to stage 3 (owner decision, 2026-10-15)." and add to stage 3 "one representative event from each adult theme, with a clarity and tone check with players". Update the smoke-test acceptance row so the test admin login belongs to M4.

`docs/technology-stack.md`: add a *Frontend versions (M2-C, TD-98)* table with the Global Constraints versions; in *OpenAPI and client generation* state the Orval command `npm run api:generate`, the `split` output in `src/api/generated/` and the `careerSecret` scheme (TD-101); in *Verification* name MSW 3 for component tests.

`docs/repository-standards.md`: rows for `source/frontend/package.json` and `package-lock.json` (exact versions, `npm ci`), `.nvmrc`, `tsconfig.json`/`tsconfig.node.json`, `vite.config.ts`/`vitest.config.ts`, `eslint.config.mjs` (the executable config of the owner's set), `orval.config.ts`, `src/api/generated/` (generated, committed, never edited by hand); remove the sentence that the executable ESLint config and lockfile will be created later.

`docs/development-and-delivery.md`: in *Current state*, add `frontend` to the required jobs and the TypeScript scope of `sonar` (TD-100); in *Local pre-commit checks*, the frontend lint and test commands (run `npm ci` in `source/frontend` once); until PR 4, local frontend runs with the API from M2-B: `npm run dev` in `source/frontend` proxies `/v1` to `http://localhost:8080`.

`docs/technical-architecture.md` §3 API table: a row for `GET /v1/career-options` (implemented in M2, TD-101).

`docs/requirements-traceability.md`: rows touched by #8 and #14 name the M2-C delivery (`In progress (M2)`); the vertical-slice row records the adult-theme move to M3.

`README.md`: the workflows line becomes `CI (backend, backend integration, frontend, SonarQube Cloud, PR policy)`; the frontend layout line drops "(later milestone)".

Check the links.

- [ ] **Step 5: Verify everything and commit**

Backend: unit run with the gate → PASS; integration run → PASS; OpenAPI regeneration → no diff; `dotnet format --verify-no-changes` → exit 0. Frontend: the frontend checks → PASS with ≥ 80% lines; `npm run api:generate` → no change. Links OK.

```bash
git add .github lefthook.yml docs README.md
git commit -m "ci: check the frontend and analyse TypeScript in SonarQube Cloud" -m "Records TD-98 to TD-101 and the owner's M2-C product decisions. Refs #8, #14." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: [external, owner yes] Review, push, open PR 1, require the check, merge**

1. One fresh reviewer (most capable model) reviews `develop..feature/m2c-08-contract` against the spec and this plan; fix Important findings with a failing test first.
2. With the owner's yes: `git push -u origin feature/m2c-08-contract`; `gh pr create --base develop --title "M2-C contract and frontend scaffold (#8)" --body "<summary, test plan, Refs #8, #14, 🤖 line>"`.
3. Wait for `backend`, `backend-integration`, `frontend`, `sonar`, `pr-policy`. Confirm SonarQube Cloud lists TypeScript files for the PR (`curl -s "https://sonarcloud.io/api/components/tree?component=adamcharusta_PolskiStreamerSimulator&pullRequest=<n>&qualifiers=FIL&q=http.ts"` returns `source/frontend/src/api/http.ts`) and 0 open issues.
4. With the owner's yes, apply both ruleset files (`gh api --method PUT repos/adamcharusta/PolskiStreamerSimulator/rulesets/24968586 --input .github/rulesets/main.json` and `.../24968589 --input .github/rulesets/develop.json`) and confirm `frontend` is required.
5. With the owner's yes, `gh pr merge --merge`; sync `develop`; watch its push run; confirm SonarQube Cloud `develop` has 0 open issues.
6. With the owner's yes, comment on #8 that the OpenAPI checklist is done, linking the PR.

---

# PR 2 — Entry: themes, 18+ gate, home and character creation (#11)

Start from the updated `develop`: `git checkout develop && git pull --ff-only && git checkout -b feature/m2c-11-entry`.

### Task 6: Polish copy, formatting, themes and base UI

**Files:**
- Create: `source/frontend/src/locales/pl/translation.json`, `source/frontend/src/i18n.ts`, `source/frontend/src/format.ts`, `source/frontend/src/theme/theme.ts`, `source/frontend/src/theme/useTheme.ts`, `source/frontend/src/components/ui/cn.ts`, `source/frontend/src/components/ui/Button.tsx`, `source/frontend/src/components/ui/Panel.tsx`, `source/frontend/src/components/ui/StepHeading.tsx`, `source/frontend/src/features/settings/SettingsDialog.tsx`
- Modify: `source/frontend/src/styles/index.css`, `source/frontend/index.html`, `source/frontend/src/main.tsx`, `source/frontend/test/setup.ts`
- Test: `source/frontend/src/format.test.ts`, `source/frontend/src/theme/theme.test.ts`, `source/frontend/src/features/settings/SettingsDialog.test.tsx`

**Interfaces:**
- Produces: the full Polish copy (every key later tasks use); `format.ts`: `pln(v)`, `count(v)`, `stat(v)`, `percent(p)`, `multiplier(v)`, `date(iso)`, `signed(v, format = count)` (U+2212 for negatives); `theme.ts`: `themes`, `type Theme`, `isTheme`, `storedTheme()`, `applyTheme(t)`, `saveTheme(t)`; `useTheme` (Zustand: `theme`, `setTheme`); `cn(...)`; `Button` (`variant: 'primary' | 'secondary'`, `pending`); `Panel` (`title`, `children`, `className`); `StepHeading` (an `h2` that takes focus when it mounts); `SettingsDialog`.

- [ ] **Step 1: Write the copy file**

`source/frontend/src/locales/pl/translation.json`:

```json
{
  "app": { "title": "Polski Streamer Simulator", "settings": "Ustawienia", "skip": "Przejdź do treści" },
  "theme": {
    "legend": "Wygląd",
    "overlay": "Overlay z OBS-a",
    "win98": "Shareware z Windowsa 98",
    "web2005": "Strona fanowska 2005",
    "close": "Zamknij"
  },
  "gate": {
    "title": "Gra tylko dla osób pełnoletnich (18+)",
    "body": "W tej grze pojawiają się tematy dla dorosłych:",
    "themes": {
      "gambling": "hazard za fikcyjną walutę gry",
      "adult": "praca twórcy treści dla dorosłych (bez treści erotycznych)",
      "privacy": "wycieki prywatności",
      "language": "mocny język"
    },
    "note": "Potwierdzenie zapisze się w tej przeglądarce. To nie jest weryfikacja wieku.",
    "confirm": "Mam 18 lat lub więcej — wchodzę",
    "leave": "Wychodzę",
    "exitTitle": "Do zobaczenia",
    "exitBody": "Ta gra jest przeznaczona tylko dla osób pełnoletnich. Możesz zamknąć tę kartę."
  },
  "notice": {
    "online": "Gra wymaga połączenia z internetem. Kariera zapisuje się na serwerze po każdym kroku.",
    "dismiss": "Rozumiem"
  },
  "home": {
    "careers": "Twoje kariery",
    "empty": "W tej przeglądarce nie masz jeszcze żadnej kariery.",
    "started": "Start: {{date}}",
    "continue": "Kontynuuj",
    "continueNamed": "Kontynuuj karierę {{nickname}}",
    "new": "Nowa kariera",
    "accessNotice": "Dostęp do kariery jest zapisany tylko w tej przeglądarce. Wyczyszczenie danych strony odbiera dostęp. Klucze odzyskiwania pojawią się w kolejnej wersji."
  },
  "create": {
    "title": "Nowa kariera",
    "loading": "Wczytuję opcje…",
    "nickname": "Nick",
    "nicknameHint": "3–20 znaków: litery, cyfry, spacja, _ - .",
    "background": "Pochodzenie",
    "difficulty": "Poziom trudności",
    "pronouns": "Zaimki",
    "summary": "Podsumowanie",
    "submit": "Zaczynam karierę",
    "cash": "Kasa: {{value}}",
    "followers": "Obserwujący: {{value}}",
    "strongest": "Mocna strona: {{value}}",
    "equipment": "Sprzęt: poziom {{value}}",
    "multiplier": "Mnożnik punktów: ×{{value}}",
    "errors": {
      "nickname": "Nick musi mieć 3–20 znaków: litery, cyfry, spacja, _ - .",
      "field": "Popraw to pole.",
      "generic": "Nie udało się założyć kariery. Spróbuj ponownie."
    }
  },
  "background": {
    "dormRoomNewcomer": "Świeżak z akademika",
    "localMemeCreator": "Lokalny memiarz",
    "chessClubRegular": "Bywalec klubu szachowego",
    "industryInsider": "Człowiek z branży"
  },
  "difficulty": { "easy": "Łatwy", "normal": "Normalny" },
  "pronouns": { "woman": "ona/jej", "man": "on/jego", "nonbinary": "forma neutralna" },
  "category": { "justChatting": "Just Chatting", "fps": "FPS", "moba": "MOBA", "chess": "Szachy", "gambling": "Hazard" },
  "action": {
    "rest": "Odpoczynek",
    "trainCategory": "Trening",
    "improveProduction": "Ulepszenie sprzętu",
    "manageCommunity": "Ogarnianie społeczności",
    "noAction": "Zwykły stream"
  },
  "slot": { "microphone": "Mikrofon", "camera": "Kamera", "computer": "Komputer", "connection": "Łącze" },
  "stat": {
    "energy": "Energia",
    "burnout": "Wypalenie",
    "drama": "Drama",
    "credibility": "Wiarygodność",
    "skill": "Umiejętności",
    "boredom": "Znudzenie",
    "equipment": "Sprzęt",
    "tier": "poziom {{value}}"
  },
  "modifier": { "credibility": "Wiarygodność", "primary-skill": "Umiejętność w kategorii", "drama": "Drama" },
  "career": {
    "summaryLabel": "Stan kariery",
    "loading": "Wczytuję karierę…",
    "week": "Tydzień {{week}}",
    "cash": "Kasa",
    "followers": "Obserwujący",
    "viewers": "Widzowie ostatniego streamu",
    "stats": "Statystyki",
    "saved": "Zapisano",
    "saving": "Zapisuję…",
    "offline": "Brak połączenia",
    "notFound": "Nie znaleziono kariery",
    "notFoundBody": "Ta przeglądarka nie ma dostępu do tej kariery albo kariera nie istnieje.",
    "forget": "Usuń z listy",
    "backHome": "Wróć na start",
    "loadFailed": "Nie udało się wczytać kariery.",
    "reload": "Odśwież",
    "retry": "Spróbuj ponownie",
    "networkFailed": "Wybór nie został zapisany — spróbuj ponownie.",
    "endedTitle": "Koniec kariery",
    "ending": {
      "completed": "Kariera ukończona — dwadzieścia lat streamowania za tobą.",
      "insolvency": "Bankructwo — kasa skończyła się na dobre.",
      "earlyRetirement": "Wcześniejsza emerytura."
    },
    "errors": {
      "stale-version": "Kariera zmieniła się w innej karcie. Pokazuję aktualny stan.",
      "career-ended": "Ta kariera już się zakończyła.",
      "week-in-progress": "Tydzień już trwa.",
      "illegal-action": "Ta akcja jest teraz niedostępna.",
      "unknown-choice": "Tej odpowiedzi już nie ma.",
      "events-unresolved": "Najpierw rozegraj zdarzenia tego tygodnia.",
      "no-pending-week": "Nie ma trwającego tygodnia.",
      "generic": "Coś poszło nie tak. Odśwież stronę."
    }
  },
  "plan": {
    "title": "Plan tygodnia",
    "action": "Akcja",
    "category": "Kategoria",
    "slot": "Sprzęt do ulepszenia",
    "start": "Zaczynam stream",
    "lastReport": "Ostatni raport (tydzień {{week}})"
  },
  "disabled": {
    "forced-rest": "Musisz odpocząć",
    "unaffordable": "Nie stać cię",
    "max-tier": "Maksymalny poziom",
    "missing-slot": "Wybierz sprzęt",
    "negative-cash": "Masz debet",
    "career-ended": "Kariera zakończona",
    "week-in-progress": "Tydzień już trwa",
    "unknown": "Niedostępne"
  },
  "event": {
    "live": "Na żywo",
    "choices": "Odpowiedzi",
    "chance": "Szansa: {{value}}",
    "certain": "pewne",
    "critical": "Możliwy wynik krytyczny",
    "modifiers": "Modyfikatory",
    "points": "{{value}} pp",
    "choose": "Wybieram",
    "untitled": "Zdarzenie na streamie",
    "chat-raid": {
      "title": "Rajd na czat",
      "welcome": "Przywitaj gości i odpal dla nich szybkie show",
      "ignore": "Graj dalej, jakby nic się nie stało"
    },
    "tech-failure": {
      "title": "Awaria sprzętu",
      "fix-live": "Napraw to na żywo i komentuj każdy krok",
      "restart": "Zrestartuj wszystko i przeproś za przerwę"
    },
    "troll-wave": {
      "title": "Fala trolli",
      "banter": "Odpowiedz żartem i zrób z nich część contentu",
      "moderate": "Włącz tryb tylko dla obserwujących i banuj bez dyskusji"
    },
    "donation-goal": {
      "title": "Cel donejtów",
      "push": "Podbijaj licznik i rozkręcaj czat do końca streamu",
      "skip": "Podziękuj i wróć do normalnego streamu"
    },
    "collab-ping": {
      "title": "Zaproszenie do kolaboracji",
      "accept": "Zaproś na stream bez przygotowania",
      "decline": "Grzecznie odmów i trzymaj się planu"
    }
  },
  "roll": {
    "title": "Wynik: {{outcome}}",
    "rolled": "Rzut {{roll}} przy progu {{threshold}}",
    "noRoll": "Pewny wybór — bez rzutu",
    "outcome": {
      "success": "Sukces",
      "failure": "Porażka",
      "criticalSuccess": "Krytyczny sukces",
      "criticalFailure": "Krytyczna porażka"
    },
    "next": "Dalej"
  },
  "settle": {
    "title": "Koniec streamu",
    "body": "Wszystkie zdarzenia tego tygodnia za tobą.",
    "action": "Kończę tydzień"
  },
  "report": {
    "title": "Raport — tydzień {{week}}",
    "viewers": "Widzowie",
    "followersGained": "Nowi obserwujący",
    "followersLost": "Odeszło",
    "followersNet": "Bilans obserwujących",
    "income": "Przychody",
    "costs": "Koszty",
    "cashDelta": "Zmiana kasy",
    "statChanges": "Zmiany statystyk",
    "none": "Brak",
    "next": "Następny tydzień",
    "line": {
      "income": { "subscriptions": "Suby", "donations": "Donejty", "ads": "Reklamy", "event": "Zdarzenie" },
      "cost": { "living": "Życie", "equipment-upkeep": "Utrzymanie sprzętu", "equipment": "Zakup sprzętu", "event": "Zdarzenie" }
    },
    "cause": { "action": "akcja tygodnia", "stream": "stream", "practice": "praktyka" }
  },
  "batch": {
    "launch": "Auto: 13 tygodni",
    "confirmTitle": "Streamować automatycznie 13 tygodni?",
    "confirmBody": "Grę poprowadzi strategia „Zbalansowana”. Każdy tydzień zapisuje się osobno, a gra zatrzyma się przed decyzją, która należy do ciebie.",
    "confirm": "Streamuj 13 tygodni",
    "cancel": "Anuluj",
    "running": "Streamujesz 13 tygodni…",
    "title": "Wynik automatycznych tygodni",
    "weeksCommitted": "Rozegrane tygodnie: {{value}}",
    "stop": {
      "completed": "Wszystkie 13 tygodni rozegrane.",
      "pausedForDecision": "Gra czeka na twoją decyzję.",
      "careerEnded": "Kariera zakończyła się w trakcie.",
      "interrupted": "Kariera zmieniła się w innej karcie — automat się zatrzymał."
    },
    "summary": "Podsumowanie",
    "start": "Na starcie",
    "end": "Na końcu",
    "ledger": "Tydzień po tygodniu",
    "week": "Tydzień {{week}}",
    "plan": "{{action}} · {{category}}",
    "choice": "{{event}}: {{choice}}",
    "next": "Dalej"
  },
  "reason": {
    "forced-rest": "Brak energii albo pełne wypalenie — tylko odpoczynek.",
    "train-every-week": "Trening co tydzień.",
    "rest-low-energy": "Energia {{energy}} poniżej {{threshold}} — odpoczynek.",
    "rest-high-burnout": "Wypalenie {{burnout}} powyżej {{limit}} — odpoczynek.",
    "upgrade-equipment": "{{slot}} za {{pricePln}} przy kasie {{cashPln}} — ulepszenie.",
    "train-scheduled": "Co {{everyWeeks}} tygodnie trening.",
    "manage-community-drama": "Drama {{drama}} powyżej {{limit}} — ogarnianie społeczności.",
    "no-action-default": "Nic pilnego — zwykły stream.",
    "fallback-rest": "„{{preferred}}” niedostępne — odpoczynek.",
    "gamble-best-odds": "Szansa {{probability}} (próg {{threshold}}) — ryzyk-fizyk.",
    "take-guaranteed": "Najlepsza szansa {{bestProbability}} to za mało — pewny wybór.",
    "take-guaranteed-only": "Do wyboru tylko pewna opcja.",
    "only-gambles": "Same ryzykowne opcje — szansa {{probability}}.",
    "unknown": "Decyzja automatu.",
    "category": "Kategoria {{category}}: {{rule}}.",
    "rule": { "bonus": "mocna strona", "least-bored": "najmniej nudna", "rotation": "rotacja" }
  }
}
```

- [ ] **Step 2: Write the failing tests**

`source/frontend/src/format.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { count, multiplier, percent, pln, signed, stat } from './format';

describe('format', () => {
  it('writes negatives with a real minus sign and positives with a plus', () => {
    expect(signed(5)).toBe('+5');
    expect(signed(-3)).toBe('−3');
    expect(signed(0)).toBe('0');
    expect(signed(-60, pln)).toMatch(/^−60\szł$/u);
  });

  it('formats money, counts, percentages, stats and multipliers in Polish', () => {
    expect(pln(2000)).toMatch(/^2000\szł$/u);
    expect(count(12_345)).toMatch(/^12\s345$/u);
    expect(percent(0.58)).toBe('58%');
    expect(stat(18.82)).toBe('18,8');
    expect(multiplier(0.96)).toBe('0,96');
  });
});
```

`source/frontend/src/theme/theme.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { saveTheme, storedTheme } from './theme';

describe('theme', () => {
  it('falls back to the overlay theme for an unknown stored value', () => {
    localStorage.setItem('pss.theme', 'neon');

    expect(storedTheme()).toBe('overlay');
  });

  it('keeps a known stored theme', () => {
    localStorage.setItem('pss.theme', 'web2005');

    expect(storedTheme()).toBe('web2005');
  });

  it('applies and stores a chosen theme', () => {
    saveTheme('win98');

    expect(document.documentElement.dataset.theme).toBe('win98');
    expect(localStorage.getItem('pss.theme')).toBe('win98');
  });
});
```

`source/frontend/src/features/settings/SettingsDialog.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it } from 'vitest';

import { SettingsDialog } from './SettingsDialog';

it('switches the theme from the settings dialog and closes it', async () => {
  const user = userEvent.setup();
  render(<SettingsDialog />);

  await user.click(screen.getByRole('button', { name: 'Ustawienia' }));
  await user.click(screen.getByRole('radio', { name: 'Shareware z Windowsa 98' }));

  expect(document.documentElement.dataset.theme).toBe('win98');
  expect(localStorage.getItem('pss.theme')).toBe('win98');
  await user.click(screen.getByRole('button', { name: 'Zamknij' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});
```

Add `import '../src/i18n';` as the first import of `source/frontend/test/setup.ts`.

Run: `npx vitest run src/format.test.ts src/theme src/features/settings` → FAIL (modules missing).

- [ ] **Step 3: Implement**

`source/frontend/src/i18n.ts`:

```ts
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import translation from './locales/pl/translation.json';

// Polish is the only M2 locale; keys stay stable for later languages (content and experience §5).
// Synchronous init: the resources are bundled, so the first render already has its copy.
void i18n.use(initReactI18next).init({
  lng: 'pl',
  fallbackLng: 'pl',
  resources: { pl: { translation } },
  initAsync: false,
  nsSeparator: false,
  interpolation: { escapeValue: false },
});

export default i18n;
```

`source/frontend/src/format.ts`:

```ts
const locale = 'pl-PL';
const MINUS = '−';

const plnFormat = new Intl.NumberFormat(locale, { style: 'currency', currency: 'PLN', maximumFractionDigits: 0 });
const countFormat = new Intl.NumberFormat(locale, { maximumFractionDigits: 0 });
const statFormat = new Intl.NumberFormat(locale, { maximumFractionDigits: 1 });
const percentFormat = new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 0 });
const multiplierFormat = new Intl.NumberFormat(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const dateFormat = new Intl.DateTimeFormat(locale, { dateStyle: 'medium' });

export const pln = (value: number): string => plnFormat.format(value);
export const count = (value: number): string => countFormat.format(value);
export const stat = (value: number): string => statFormat.format(value);
export const percent = (probability: number): string => percentFormat.format(probability);
export const multiplier = (value: number): string => multiplierFormat.format(value);
export const date = (iso: string): string => dateFormat.format(new Date(iso));

/** A change with its sign, never colour alone (content and experience §4). */
export function signed(value: number, format: (value: number) => string = count): string {
  if (value > 0) {
    return `+${format(value)}`;
  }

  return value < 0 ? `${MINUS}${format(-value)}` : format(0);
}
```

`source/frontend/src/theme/theme.ts`:

```ts
export const themes = ['overlay', 'win98', 'web2005'] as const;

export type Theme = (typeof themes)[number];

const storageKey = 'pss.theme';

export function isTheme(value: unknown): value is Theme {
  return typeof value === 'string' && (themes as readonly string[]).includes(value);
}

/** The stored theme, or the overlay default for a missing or unknown value. */
export function storedTheme(): Theme {
  const value = localStorage.getItem(storageKey);
  return isTheme(value) ? value : 'overlay';
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
}

/** Presentation only: the theme never reaches the server or the rules. */
export function saveTheme(theme: Theme): void {
  localStorage.setItem(storageKey, theme);
  applyTheme(theme);
}
```

`source/frontend/src/theme/useTheme.ts`:

```ts
import { create } from 'zustand';

import { saveTheme, storedTheme, type Theme } from './theme';

interface ThemeState {
  readonly theme: Theme;
  readonly setTheme: (theme: Theme) => void;
}

export const useTheme = create<ThemeState>()((set) => ({
  theme: storedTheme(),
  setTheme: (theme) => {
    saveTheme(theme);
    set({ theme });
  },
}));
```

`source/frontend/src/components/ui/cn.ts`:

```ts
import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...values: ClassValue[]): string {
  return twMerge(clsx(values));
}
```

`source/frontend/src/components/ui/Button.tsx`:

```tsx
import { cva, type VariantProps } from 'class-variance-authority';
import type { ButtonHTMLAttributes } from 'react';

import { cn } from './cn';

const button = cva('btn', {
  variants: { variant: { primary: 'btn-primary', secondary: 'btn-secondary' } },
  defaultVariants: { variant: 'secondary' },
});

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof button> {
  /** A request for this action is running: the button stays visible but cannot be activated again. */
  readonly pending?: boolean;
}

export function Button({ className, variant, pending = false, type = 'button', disabled = false, ...props }: ButtonProps) {
  return (
    <button
      type={type === 'submit' ? 'submit' : 'button'}
      className={cn(button({ variant }), className)}
      disabled={disabled || pending}
      aria-busy={pending || undefined}
      {...props}
    />
  );
}
```

`source/frontend/src/components/ui/Panel.tsx`:

```tsx
import { type ReactNode, useId } from 'react';

import { cn } from './cn';

export interface PanelProps {
  readonly title: ReactNode;
  readonly children: ReactNode;
  readonly className?: string;
}

/** A framed section with its heading; the frame is what each theme restyles. */
export function Panel({ title, children, className }: PanelProps) {
  const id = useId();
  return (
    <section className={cn('panel', className)} aria-labelledby={id}>
      <h2 id={id} className="panel-title">
        {title}
      </h2>
      <div className="panel-body">{children}</div>
    </section>
  );
}
```

`source/frontend/src/components/ui/StepHeading.tsx`:

```tsx
import { type ReactNode, useEffect, useRef } from 'react';

/** The heading of the current game step; it takes focus when the step appears (content and experience §4). */
export function StepHeading({ children }: { readonly children: ReactNode }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return (
    <h2 ref={ref} tabIndex={-1} className="step-heading">
      {children}
    </h2>
  );
}
```

`source/frontend/src/features/settings/SettingsDialog.tsx`:

```tsx
import { Dialog } from 'radix-ui';
import { useTranslation } from 'react-i18next';

import { Button } from '@/components/ui/Button';
import { themes } from '@/theme/theme';
import { useTheme } from '@/theme/useTheme';

export function SettingsDialog() {
  const { t } = useTranslation();
  const theme = useTheme((state) => state.theme);
  const setTheme = useTheme((state) => state.setTheme);
  return (
    <Dialog.Root>
      <Dialog.Trigger asChild>
        <Button>{t('app.settings')}</Button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content className="dialog panel" aria-describedby={undefined}>
          <Dialog.Title className="panel-title">{t('app.settings')}</Dialog.Title>
          <fieldset className="choices">
            <legend>{t('theme.legend')}</legend>
            {themes.map((name) => (
              <label key={name} className="choice">
                <input
                  type="radio"
                  name="theme"
                  value={name}
                  checked={theme === name}
                  onChange={() => {
                    setTheme(name);
                  }}
                />
                {t(`theme.${name}`)}
              </label>
            ))}
          </fieldset>
          <Dialog.Close asChild>
            <Button>{t('theme.close')}</Button>
          </Dialog.Close>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
```

Replace `source/frontend/src/styles/index.css` with the theme tokens and component classes:

```css
@import 'tailwindcss';

/* Theme tokens (TD-98): every theme meets WCAG 2.2 AA for text and focus; neon and gradients stay on frames. */
:root,
[data-theme='overlay'] {
  --bg: #111111;
  --page-text: #ffffff;
  --surface: #1b1b1b;
  --text: #ffffff;
  --muted: #c9c9c9;
  --frame: #ff2bd6;
  --frame-2: #00e5ff;
  --bar: #00b140;
  --bar-text: #000000;
  --accent: #ffe600;
  --accent-text: #000000;
  --live: #c8102e;
  --good: #3ddc84;
  --bad: #ff8a8a;
  --focus: #00e5ff;
  --border: 3px solid var(--frame);
  --radius: 0;
  --tilt: -0.6deg;
  --font-heading: 'Anton', Impact, 'Arial Narrow', sans-serif;
  --font-body: system-ui, 'Segoe UI', Arial, sans-serif;
}

[data-theme='win98'] {
  --bg: #008080;
  --page-text: #ffffff;
  --surface: #c0c0c0;
  --text: #000000;
  --muted: #404040;
  --frame: #404040;
  --frame-2: #ffffff;
  --bar: #000080;
  --bar-text: #ffffff;
  --accent: #c0c0c0;
  --accent-text: #000000;
  --live: #8b0000;
  --good: #005000;
  --bad: #8b0000;
  --focus: #000000;
  --border: 2px outset #dfdfdf;
  --radius: 0;
  --tilt: 0deg;
  --font-heading: Tahoma, Verdana, sans-serif;
  --font-body: Tahoma, Verdana, sans-serif;
}

[data-theme='web2005'] {
  --bg: #e9eef7;
  --page-text: #1b1b1b;
  --surface: #ffffff;
  --text: #1b1b1b;
  --muted: #4a4a4a;
  --frame: #2f6fd0;
  --frame-2: #7a9cd6;
  --bar: #2f6fd0;
  --bar-text: #ffffff;
  --accent: #dbe8ff;
  --accent-text: #0a2a66;
  --live: #c00000;
  --good: #1d6b1d;
  --bad: #c00000;
  --focus: #c00000;
  --border: 1px dashed var(--frame-2);
  --radius: 10px;
  --tilt: 0deg;
  --font-heading: 'Comic Neue', 'Comic Sans MS', Verdana, sans-serif;
  --font-body: Verdana, Tahoma, sans-serif;
}

/* Text on the page background (win98: white on teal) differs from text on panels. */
body {
  margin: 0;
  background: var(--bg);
  color: var(--page-text);
  font: 16px/1.45 var(--font-body);
}

:focus-visible {
  outline: 3px solid var(--focus);
  outline-offset: 2px;
}

.heading,
.panel-title,
.step-heading {
  font-family: var(--font-heading);
  letter-spacing: 0.02em;
  margin: 0 0 0.5rem;
}

.shell {
  max-width: 72rem;
  margin: 0 auto;
  padding: 0.75rem;
}

.panel {
  background: var(--surface);
  color: var(--text);
  border: var(--border);
  border-radius: var(--radius);
  padding: 0.75rem;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  min-height: 2.75rem;
  padding: 0.4rem 0.9rem;
  border: var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-weight: 700;
  text-decoration: none;
  cursor: pointer;
}

.btn-primary {
  background: var(--accent);
  color: var(--accent-text);
  transform: rotate(var(--tilt));
}

.btn:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}

.choices {
  display: grid;
  gap: 0.5rem;
  border: 0;
  margin: 0 0 0.75rem;
  padding: 0;
}

.choice {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: baseline;
  padding: 0.5rem;
  border: var(--border);
  border-radius: var(--radius);
}

.muted {
  color: var(--muted);
}

.badge-live {
  background: var(--live);
  color: #ffffff;
  padding: 0 0.4rem;
  font-weight: 700;
}

.good {
  color: var(--good);
}

.bad {
  color: var(--bad);
}

.topbar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem 1rem;
  align-items: center;
  background: var(--bar);
  color: var(--bar-text);
  padding: 0.4rem 0.75rem;
  font-family: var(--font-heading);
}

.desk {
  display: grid;
  gap: 0.75rem;
  grid-template-columns: minmax(0, 1fr);
}

.rail {
  display: none;
}

.rail.open {
  display: block;
}

@media (min-width: 48rem) {
  .desk {
    grid-template-columns: minmax(0, 3fr) minmax(0, 1.2fr);
  }

  .rail {
    display: block;
  }

  .rail-toggle {
    display: none;
  }
}

.dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgb(0 0 0 / 70%);
}

.dialog {
  position: fixed;
  top: 50%;
  left: 50%;
  width: min(32rem, calc(100vw - 1.5rem));
  max-height: calc(100vh - 1.5rem);
  overflow: auto;
  transform: translate(-50%, -50%);
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

/* The skip link must be visible when it has focus. */
.sr-only:focus-visible {
  position: static;
  width: auto;
  height: auto;
  clip-path: none;
}

@media (prefers-reduced-motion: reduce) {
  .btn-primary {
    transform: none;
  }
}
```

In `source/frontend/index.html`, add before the module script (it applies the stored theme before the first paint; it must match `theme.ts`):

```html
    <script>
      try {
        var stored = localStorage.getItem('pss.theme');
        if (stored === 'win98' || stored === 'web2005') document.documentElement.dataset.theme = stored;
      } catch (error) {}
    </script>
```

In `source/frontend/src/main.tsx`, add after the CSS import:

```ts
import '@fontsource/anton/400.css';
import '@fontsource/comic-neue/400.css';
import '@fontsource/comic-neue/700.css';
import './i18n';
```

Check the Polish glyphs: `grep -c "U+0100-02BA\|U+0100-024F" node_modules/@fontsource/anton/400.css node_modules/@fontsource/comic-neue/400.css` → each file contains a latin-ext range covering `ą ć ę ł ń ó ś ź ż` (U+0104–U+017C). If a font lacks it, keep its family in the stack and let the next family render the heading; record the gap in the PR. **Superseded (TD-107, 2026-10-15):** Comic Neue failed this check (Latin-1 only), so `web2005` uses Comic Relief, and `src/styles/fonts.test.ts` now fails for any heading family without every Polish letter.

- [ ] **Step 4: Run and commit**

Run: `npx vitest run` → PASS; `npm run typecheck`, `npm run lint`, `npm run format:check` → exit 0.

```bash
git add source/frontend
git commit -m "feat(frontend): add the Polish copy, number formats, three retro themes and base UI" -m "Refs #11." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 7: App shell, router, known careers and home

**Files:**
- Create: `source/frontend/src/app/queryClient.ts`, `source/frontend/src/app/router.ts`, `source/frontend/src/app/RootLayout.tsx`, `source/frontend/src/careers/knownCareers.ts`, `source/frontend/src/features/home/HomeScreen.tsx`, `source/frontend/src/features/create-career/CreateCareerScreen.tsx` (replaced in Task 9), `source/frontend/src/features/career/CareerScreen.tsx` (replaced in Task 14), `source/frontend/test/render.tsx`
- Modify: `source/frontend/src/App.tsx`
- Delete: `source/frontend/src/App.test.tsx`
- Test: `source/frontend/src/careers/knownCareers.test.ts`, `source/frontend/src/features/home/HomeScreen.test.tsx`

**Interfaces:**
- Produces: `createQueryClient({ retryDelay? })` and `shouldRetry(failureCount, error)`; `createAppRouter(history?)`, `type AppRouter`; route ids `/`, `/nowa-kariera`, `/kariera/$careerId`; `App({ router?, queryClient? })`; `knownCareers.ts`: `interface KnownCareer { careerId; nickname; secret; createdAt }`, `knownCareers()`, `rememberCareer(c)`, `forgetCareer(id)`, `careerSecret(id): string | null`, `careerAuth(secret): AxiosRequestConfig`; `test/render.tsx`: `renderApp(path, { confirmed = true })` → `{ router, queryClient, user, history }`, `renderWithClient(ui)` → `{ queryClient, user }`, `rememberTestCareer(overrides?)` → `KnownCareer`.

- [ ] **Step 1: Write the failing tests**

`source/frontend/test/render.tsx`:

```tsx
import { QueryClientProvider } from '@tanstack/react-query';
import { createMemoryHistory } from '@tanstack/react-router';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';

import { App } from '@/App';
import { createQueryClient } from '@/app/queryClient';
import { createAppRouter } from '@/app/router';
import { type KnownCareer, rememberCareer } from '@/careers/knownCareers';

export const TEST_CAREER_ID = '0199e3a0-0000-7000-8000-000000000001';

/** The whole app at `path`; by default the 18+ gate and the online notice were already answered. */
export function renderApp(path = '/', { confirmed = true }: { confirmed?: boolean } = {}) {
  if (confirmed) {
    localStorage.setItem('pss.ageGate.v1', 'confirmed');
    localStorage.setItem('pss.onlineNotice.v1', 'dismissed');
  }

  const history = createMemoryHistory({ initialEntries: [path] });
  const router = createAppRouter(history);
  const queryClient = createQueryClient({ retryDelay: 0 });
  const user = userEvent.setup();
  render(<App router={router} queryClient={queryClient} />);
  return { router, queryClient, user, history };
}

/** One component with a query client and no retry delay. */
export function renderWithClient(ui: ReactElement) {
  const queryClient = createQueryClient({ retryDelay: 0 });
  const user = userEvent.setup();
  render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
  return { queryClient, user };
}

export function rememberTestCareer(overrides: Partial<KnownCareer> = {}): KnownCareer {
  const career: KnownCareer = {
    careerId: TEST_CAREER_ID,
    nickname: 'Streamer',
    secret: 'secret-1',
    createdAt: '2026-10-15T12:00:00.000Z',
    ...overrides,
  };
  rememberCareer(career);
  return career;
}
```

`source/frontend/src/careers/knownCareers.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { careerAuth, careerSecret, forgetCareer, knownCareers, rememberCareer } from './knownCareers';

const career = (careerId: string) => ({ careerId, nickname: `Nick ${careerId}`, secret: `secret-${careerId}`, createdAt: '2026-10-15T12:00:00.000Z' });

describe('known careers', () => {
  it('remembers the newest career first and only once', () => {
    rememberCareer(career('a'));
    rememberCareer(career('b'));
    rememberCareer(career('a'));

    expect(knownCareers().map((c) => c.careerId)).toEqual(['a', 'b']);
  });

  it('forgets a career and its secret', () => {
    rememberCareer(career('a'));

    forgetCareer('a');

    expect(knownCareers()).toEqual([]);
    expect(careerSecret('a')).toBeNull();
  });

  it('ignores malformed known-careers storage', () => {
    localStorage.setItem('pss.careers.v1', 'not json');
    expect(knownCareers()).toEqual([]);

    localStorage.setItem('pss.careers.v1', JSON.stringify([{ careerId: 'x' }, career('b'), 42]));
    expect(knownCareers()).toEqual([career('b')]);
  });

  it('sends a career secret as a bearer token', () => {
    expect(careerAuth('s3cret')).toEqual({ headers: { Authorization: 'Bearer s3cret' } });
  });
});
```

`source/frontend/src/features/home/HomeScreen.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { rememberTestCareer, renderApp } from '../../../test/render';

describe('home', () => {
  it('lists the careers known to this browser', async () => {
    rememberTestCareer({ careerId: 'b', nickname: 'Drugi' });
    rememberTestCareer({ careerId: 'a', nickname: 'Pierwszy' });

    renderApp('/');

    const links = await screen.findAllByRole('link', { name: /Kontynuuj karierę/u });
    expect(links.map((l) => l.getAttribute('href'))).toEqual(['/kariera/a', '/kariera/b']);
    expect(screen.getByText('Pierwszy')).toBeInTheDocument();
  });

  it('shows the empty state, the access notice and the new-career link', async () => {
    renderApp('/');

    expect(await screen.findByText('W tej przeglądarce nie masz jeszcze żadnej kariery.')).toBeInTheDocument();
    expect(screen.getByText(/tylko w tej przeglądarce/u)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nowa kariera' })).toHaveAttribute('href', '/nowa-kariera');
  });

  it('shows the empty state for malformed storage instead of failing', async () => {
    localStorage.setItem('pss.careers.v1', '{"oops":');

    renderApp('/');

    expect(await screen.findByText('W tej przeglądarce nie masz jeszcze żadnej kariery.')).toBeInTheDocument();
  });
});
```

Delete `source/frontend/src/App.test.tsx` (the home tests cover the title).

Run: `npx vitest run src/careers src/features/home` → FAIL (modules missing).

- [ ] **Step 2: Implement**

`source/frontend/src/app/queryClient.ts`:

```ts
import { QueryClient } from '@tanstack/react-query';

import { isApiError } from '@/api/http';

/** Only a lost connection is retried, at most twice; a coded API answer is final. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  return isApiError(error) && error.isNetwork && failureCount < 2;
}

const backoff = (attempt: number): number => Math.min(1000 * 2 ** attempt, 4000);

export function createQueryClient({ retryDelay = backoff }: { retryDelay?: number | ((attempt: number) => number) } = {}) {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: shouldRetry, retryDelay, refetchOnWindowFocus: false },
      mutations: { retry: shouldRetry, retryDelay },
    },
  });
}
```

`source/frontend/src/careers/knownCareers.ts`:

```ts
import type { AxiosRequestConfig } from 'axios';

/** A career this browser can open: its secret is the only access (no recovery key until M4). */
export interface KnownCareer {
  readonly careerId: string;
  readonly nickname: string;
  readonly secret: string;
  readonly createdAt: string;
}

const storageKey = 'pss.careers.v1';

function isKnownCareer(value: unknown): value is KnownCareer {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const c = value as Record<string, unknown>;
  return [c.careerId, c.nickname, c.secret, c.createdAt].every((field) => typeof field === 'string' && field.length > 0);
}

/** Every well-formed stored career, newest first; malformed storage reads as no careers. */
export function knownCareers(): KnownCareer[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((entry) => isKnownCareer(entry)) : [];
  } catch {
    return [];
  }
}

function save(careers: KnownCareer[]): void {
  localStorage.setItem(storageKey, JSON.stringify(careers));
}

export function rememberCareer(career: KnownCareer): void {
  save([career, ...knownCareers().filter((known) => known.careerId !== career.careerId)]);
}

export function forgetCareer(careerId: string): void {
  save(knownCareers().filter((known) => known.careerId !== careerId));
}

export function careerSecret(careerId: string): string | null {
  return knownCareers().find((known) => known.careerId === careerId)?.secret ?? null;
}

/** Request options for one career's calls; the generated client merges them into its request. */
export function careerAuth(secret: string): AxiosRequestConfig {
  return { headers: { Authorization: `Bearer ${secret}` } };
}
```

`source/frontend/src/app/RootLayout.tsx`:

```tsx
import { Link, Outlet } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { SettingsDialog } from '@/features/settings/SettingsDialog';

export function RootLayout() {
  const { t } = useTranslation();
  return (
    <div className="shell">
      <a className="sr-only" href="#main">
        {t('app.skip')}
      </a>
      <header className="flex flex-wrap items-center justify-between gap-2 py-2">
        <Link to="/" className="heading text-xl text-inherit no-underline">
          {t('app.title')}
        </Link>
        <SettingsDialog />
      </header>
      <main id="main">
        <Outlet />
      </main>
    </div>
  );
}
```

`source/frontend/src/features/home/HomeScreen.tsx`:

```tsx
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { knownCareers } from '@/careers/knownCareers';
import { Panel } from '@/components/ui/Panel';
import { date } from '@/format';

export function HomeScreen() {
  const { t } = useTranslation();
  const careers = knownCareers();
  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('app.title')}</h1>
      <Panel title={t('home.careers')}>
        {careers.length === 0 ? (
          <p>{t('home.empty')}</p>
        ) : (
          <ul className="grid list-none gap-2 p-0">
            {careers.map((career) => (
              <li key={career.careerId} className="choice justify-between">
                <span className="font-bold">{career.nickname}</span>
                <span className="muted">{t('home.started', { date: date(career.createdAt) })}</span>
                <Link
                  className="btn btn-secondary"
                  to="/kariera/$careerId"
                  params={{ careerId: career.careerId }}
                  aria-label={t('home.continueNamed', { nickname: career.nickname })}
                >
                  {t('home.continue')}
                </Link>
              </li>
            ))}
          </ul>
        )}
        <p className="my-3">
          <Link className="btn btn-primary" to="/nowa-kariera">
            {t('home.new')}
          </Link>
        </p>
        <p className="muted">{t('home.accessNotice')}</p>
      </Panel>
    </div>
  );
}
```

`source/frontend/src/features/create-career/CreateCareerScreen.tsx` (Task 9 replaces it):

```tsx
import { useTranslation } from 'react-i18next';

export function CreateCareerScreen() {
  const { t } = useTranslation();
  return <h1 className="heading text-3xl">{t('create.title')}</h1>;
}
```

`source/frontend/src/features/career/CareerScreen.tsx` (Task 14 replaces it):

```tsx
import { useTranslation } from 'react-i18next';

export function CareerScreen() {
  const { t } = useTranslation();
  return <p role="status">{t('career.loading')}</p>;
}
```

`source/frontend/src/app/router.ts`:

```ts
import { createRootRoute, createRoute, createRouter, type RouterHistory } from '@tanstack/react-router';

import { CareerScreen } from '@/features/career/CareerScreen';
import { CreateCareerScreen } from '@/features/create-career/CreateCareerScreen';
import { HomeScreen } from '@/features/home/HomeScreen';

import { RootLayout } from './RootLayout';

const rootRoute = createRootRoute({ component: RootLayout });
const homeRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: HomeScreen });
const createCareerRoute = createRoute({ getParentRoute: () => rootRoute, path: '/nowa-kariera', component: CreateCareerScreen });
const careerRoute = createRoute({ getParentRoute: () => rootRoute, path: '/kariera/$careerId', component: CareerScreen });

const routeTree = rootRoute.addChildren([homeRoute, createCareerRoute, careerRoute]);

/** One game route per career: its stage follows the server's next step, not the URL (TD-98). */
export function createAppRouter(history?: RouterHistory) {
  return createRouter({ routeTree, history });
}

export type AppRouter = ReturnType<typeof createAppRouter>;

declare module '@tanstack/react-router' {
  interface Register {
    router: AppRouter;
  }
}
```

Replace `source/frontend/src/App.tsx`:

```tsx
import { type QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { useState } from 'react';

import { createQueryClient } from './app/queryClient';
import { type AppRouter, createAppRouter } from './app/router';

export interface AppProps {
  readonly router?: AppRouter;
  readonly queryClient?: QueryClient;
}

export function App(props: AppProps) {
  const [router] = useState(() => props.router ?? createAppRouter());
  const [queryClient] = useState(() => props.queryClient ?? createQueryClient());
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
```

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; typecheck, lint, format → exit 0. Start `npm run dev` and open `http://localhost:5173` once to see the home screen in the overlay theme; switch themes in `Ustawienia`.

```bash
git add -A source/frontend
git commit -m "feat(frontend): add the router, the careers known to this browser and the home screen" -m "Refs #11." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 8: The 18+ gate and the online notice

**Files:**
- Create: `source/frontend/src/features/age-gate/AgeGate.tsx`
- Modify: `source/frontend/src/App.tsx`
- Test: `source/frontend/src/features/age-gate/AgeGate.test.tsx`

**Interfaces:**
- Consumes: `renderApp` (Task 7), `Button` (Task 6).
- Produces: `AgeGate({ children })`; storage keys `pss.ageGate.v1` and `pss.onlineNotice.v1`.

- [ ] **Step 1: Write the failing tests**

`source/frontend/src/features/age-gate/AgeGate.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { server } from '../../../test/server';
import { renderApp, TEST_CAREER_ID } from '../../../test/render';

const gate = () => screen.findByRole('alertdialog', { name: 'Gra tylko dla osób pełnoletnich (18+)' });

describe('the 18+ gate', () => {
  it('shows only the gate and its themes before confirmation', async () => {
    renderApp('/', { confirmed: false });

    expect(await gate()).toHaveTextContent('hazard za fikcyjną walutę gry');
    expect(screen.queryByText('Twoje kariery')).not.toBeInTheDocument();
  });

  it('does not open the game on Escape, an outside click or navigating back', async () => {
    const { user, history } = renderApp('/', { confirmed: false });
    await gate();

    await user.keyboard('{Escape}');
    await user.click(document.body);
    history.push('/nowa-kariera');
    history.back();

    expect(await gate()).toBeInTheDocument();
    expect(localStorage.getItem('pss.ageGate.v1')).toBeNull();
  });

  it('shows a neutral exit when the player leaves', async () => {
    const { user } = renderApp('/', { confirmed: false });

    await user.click(await screen.findByRole('button', { name: 'Wychodzę' }));

    expect(screen.getByRole('heading', { name: 'Do zobaczenia' })).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(screen.queryByText('Twoje kariery')).not.toBeInTheDocument();
    expect(localStorage.getItem('pss.ageGate.v1')).toBeNull();
  });

  it('opens the game after confirmation, remembers it and shows the online notice once', async () => {
    const { user } = renderApp('/', { confirmed: false });

    await user.click(await screen.findByRole('button', { name: 'Mam 18 lat lub więcej — wchodzę' }));

    expect(await screen.findByText('Twoje kariery')).toBeInTheDocument();
    expect(localStorage.getItem('pss.ageGate.v1')).toBe('confirmed');
    await user.click(screen.getByRole('button', { name: 'Rozumiem' }));
    expect(screen.queryByText(/Gra wymaga połączenia/u)).not.toBeInTheDocument();
    expect(localStorage.getItem('pss.onlineNotice.v1')).toBe('dismissed');
  });

  it('shows the gate first on a deep link and makes no request', async () => {
    let requests = 0;
    server.use(
      http.all('*', () => {
        requests += 1;
        return HttpResponse.error();
      }),
    );

    renderApp(`/kariera/${TEST_CAREER_ID}`, { confirmed: false });

    expect(await gate()).toBeInTheDocument();
    expect(requests).toBe(0);
  });

  it('skips the gate once it was confirmed', async () => {
    renderApp('/');

    expect(await screen.findByText('Twoje kariery')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });
});
```

Run: `npx vitest run src/features/age-gate` → FAIL (`AgeGate` missing).

- [ ] **Step 2: Implement**

`source/frontend/src/features/age-gate/AgeGate.tsx`:

```tsx
import { AlertDialog } from 'radix-ui';
import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/components/ui/Button';

const gateKey = 'pss.ageGate.v1';
const noticeKey = 'pss.onlineNotice.v1';
const gameThemes = ['gambling', 'adult', 'privacy', 'language'] as const;

type GateState = 'asking' | 'confirmed' | 'declined';

/**
 * A presentation gate, not age verification (content and experience §2): nothing of the game — routes, copy or
 * requests — renders before an explicit confirmation. Escape and outside clicks never close it.
 */
export function AgeGate({ children }: { readonly children: ReactNode }) {
  const { t } = useTranslation();
  const [state, setState] = useState<GateState>(() => (localStorage.getItem(gateKey) === 'confirmed' ? 'confirmed' : 'asking'));
  const [noticeOpen, setNoticeOpen] = useState(() => localStorage.getItem(noticeKey) !== 'dismissed');

  if (state === 'declined') {
    return (
      <main className="shell grid min-h-screen place-content-center gap-2 text-center">
        <h1 className="heading text-3xl">{t('gate.exitTitle')}</h1>
        <p>{t('gate.exitBody')}</p>
      </main>
    );
  }

  if (state === 'confirmed') {
    return (
      <>
        {noticeOpen && (
          <div role="status" className="topbar justify-between">
            <span>{t('notice.online')}</span>
            <Button
              onClick={() => {
                localStorage.setItem(noticeKey, 'dismissed');
                setNoticeOpen(false);
              }}
            >
              {t('notice.dismiss')}
            </Button>
          </div>
        )}
        {children}
      </>
    );
  }

  return (
    <AlertDialog.Root open>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content
          className="dialog panel"
          onEscapeKeyDown={(event) => {
            event.preventDefault();
          }}
        >
          <AlertDialog.Title className="panel-title text-2xl">{t('gate.title')}</AlertDialog.Title>
          <AlertDialog.Description>{t('gate.body')}</AlertDialog.Description>
          <ul>
            {gameThemes.map((name) => (
              <li key={name}>{t(`gate.themes.${name}`)}</li>
            ))}
          </ul>
          <p className="muted">{t('gate.note')}</p>
          <div className="flex flex-wrap gap-2">
            <AlertDialog.Action asChild>
              <Button
                variant="primary"
                onClick={() => {
                  localStorage.setItem(gateKey, 'confirmed');
                  setState('confirmed');
                }}
              >
                {t('gate.confirm')}
              </Button>
            </AlertDialog.Action>
            <AlertDialog.Cancel asChild>
              <Button
                onClick={() => {
                  setState('declined');
                }}
              >
                {t('gate.leave')}
              </Button>
            </AlertDialog.Cancel>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
```

In `source/frontend/src/App.tsx`, import `AgeGate` from `./features/age-gate/AgeGate` and wrap the router: `<AgeGate><RouterProvider router={router} /></AgeGate>` inside the `QueryClientProvider`.

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; typecheck, lint, format → exit 0.

```bash
git add source/frontend
git commit -m "feat(frontend): block the game behind an 18+ gate that only an explicit choice opens" -m "Refs #11." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 9: Character creation, PR 2 documentation and delivery

**Files:**
- Create: `source/frontend/src/features/create-career/schema.ts`, `source/frontend/test/fixtures.ts`, `source/frontend/test/api.ts`
- Modify: `source/frontend/src/features/create-career/CreateCareerScreen.tsx` (replace)
- Test: `source/frontend/src/features/create-career/CreateCareerScreen.test.tsx`, `source/frontend/src/features/create-career/schema.test.ts`
- Modify: `docs/content-and-experience.md`, `docs/requirements-traceability.md`

**Interfaces:**
- Consumes: generated `getCareerOptions`, `useGetCareerOptions`, `createCareer`, `getGetCareerQueryKey`, types `CareerOptionsView`, `CreatedCareer`, `CareerView`, `CreateCareerRequest`, const enums `Background`, `Difficulty`, `PronounSet`, `Category`, `MainAction`, `EquipmentSlot`, `NextStepKind`; `rememberCareer` (Task 7).
- Produces: `createCareerSchema` and `type CreateCareerForm`; `test/fixtures.ts`: `careerOptions()`, `stats(overrides?)`, `planNext(disabled?)`, `eventNext()`, `settleNext()`, `endedNext()`, `roll(overrides?)`, `weekReport(week?, overrides?)`, `careerView(overrides?)`, `actionOutcome(career, extras?)`; `test/api.ts`: `problem(status, code, extra?)`, `serveCareer(...views)` (answers `GET /v1/careers/:id` with the views in order, repeating the last), `captureActions(respond)` and `captureBatches(respond)` (return the received bodies and headers).

- [ ] **Step 1: Write the fixtures and API helpers**

`source/frontend/test/fixtures.ts`:

```ts
import {
  Background,
  type CareerOptionsView,
  type CareerStatsView,
  type CareerView,
  Category,
  Difficulty,
  EquipmentSlot,
  MainAction,
  type NextStepView,
  NextStepKind,
  type PlanOptionView,
  type PlanView,
  PronounSet,
  type RollView,
  type WeekReportView,
} from '@/api/generated/game.schemas';

import { TEST_CAREER_ID } from './render';

export function careerOptions(): CareerOptionsView {
  const skill = (strong: keyof typeof Category, value = 18) => ({ justChatting: 10, fps: 10, moba: 10, chess: 10, gambling: 10, [Category[strong]]: value });
  const tiers = (tier: number) => ({ microphone: tier, camera: tier, computer: tier, connection: tier });
  return {
    backgrounds: [
      { background: Background.dormRoomNewcomer, cashPln: 2000, followers: 0, drama: 0, credibility: 50, energy: 70, skill: skill('fps'), equipment: tiers(0), scoreMultiplier: 1 },
      { background: Background.localMemeCreator, cashPln: 1000, followers: 600, drama: 20, credibility: 42, energy: 70, skill: skill('justChatting'), equipment: tiers(1), scoreMultiplier: 0.96 },
      { background: Background.chessClubRegular, cashPln: 500, followers: 0, drama: 0, credibility: 58, energy: 70, skill: skill('chess', 25), equipment: tiers(1), scoreMultiplier: 0.98 },
      { background: Background.industryInsider, cashPln: 5000, followers: 1500, drama: 15, credibility: 50, energy: 70, skill: skill('fps', 10), equipment: tiers(2), scoreMultiplier: 0.9 },
    ],
    difficulties: [
      { difficulty: Difficulty.easy, scoreMultiplier: 1 },
      { difficulty: Difficulty.normal, scoreMultiplier: 1.1 },
    ],
    pronouns: [PronounSet.woman, PronounSet.man, PronounSet.nonbinary],
  };
}

export function stats(overrides: Partial<CareerStatsView> = {}): CareerStatsView {
  return {
    cashPln: 2000,
    followers: 0,
    lastLiveViewers: 0,
    drama: 0,
    credibility: 50,
    energy: 70,
    burnout: 0,
    skill: { justChatting: 10, fps: 18, moba: 10, chess: 10, gambling: 10 },
    boredom: { justChatting: 0, fps: 0, moba: 0, chess: 0, gambling: 0 },
    equipment: { microphone: 0, camera: 0, computer: 0, connection: 0 },
    ...overrides,
  };
}

/** Every plan of a week, like the API lists them; `disabled` decides each option's reason. */
export function planNext(disabled: (plan: PlanView) => string | null = () => null): NextStepView {
  const plans: PlanOptionView[] = [];
  for (const category of Object.values(Category)) {
    for (const mainAction of Object.values(MainAction)) {
      const slots = mainAction === MainAction.improveProduction ? Object.values(EquipmentSlot) : [null];
      for (const upgradeSlot of slots) {
        const plan: PlanView = { mainAction, category, trainTarget: null, upgradeSlot };
        plans.push({ plan, disabledReason: disabled(plan) });
      }
    }
  }

  return { kind: NextStepKind.planWeek, plans, event: null };
}

export function eventNext(): NextStepView {
  return {
    kind: NextStepKind.resolveEvent,
    plans: [],
    event: {
      eventId: 'chat-raid',
      titleKey: 'event.chat-raid.title',
      textPl: 'Inny streamer kończy transmisję i wysyła do ciebie cały swój czat.',
      choices: [
        {
          choiceId: 'welcome',
          labelKey: 'event.chat-raid.welcome',
          labelPl: 'Przywitaj gości i odpal dla nich szybkie show',
          guaranteed: false,
          probability: 0.58,
          threshold: 58,
          modifiers: [{ labelKey: 'modifier.credibility', value: 0.03 }],
          allowsCritical: true,
        },
        {
          choiceId: 'ignore',
          labelKey: 'event.chat-raid.ignore',
          labelPl: 'Graj dalej, jakby nic się nie stało',
          guaranteed: true,
          probability: 1,
          threshold: 100,
          modifiers: [],
          allowsCritical: false,
        },
      ],
    },
  };
}

export const settleNext = (): NextStepView => ({ kind: NextStepKind.settleWeek, plans: [], event: null });

export const endedNext = (): NextStepView => ({ kind: NextStepKind.ended, plans: [], event: null });

export function roll(overrides: Partial<RollView> = {}): RollView {
  return { eventId: 'chat-raid', choiceId: 'welcome', roll: 37, threshold: 58, modifiers: [], outcome: 'success', ...overrides };
}

export function weekReport(week = 1, overrides: Partial<WeekReportView> = {}): WeekReportView {
  return {
    week,
    plan: { mainAction: MainAction.rest, category: Category.fps, trainTarget: null, upgradeSlot: null },
    rolls: [roll()],
    liveViewers: 115,
    followersGained: 8,
    followersLost: 2,
    followersNet: 6,
    income: [
      { code: 'income:subscriptions', amountPln: 23, delta: null, detail: null },
      { code: 'income:donations', amountPln: 17, delta: null, detail: null },
    ],
    costs: [{ code: 'cost:living', amountPln: 100, delta: null, detail: null }],
    cashDelta: -60,
    statChanges: [
      { code: 'stat:Energy', amountPln: null, delta: 25, detail: 'action' },
      { code: 'stat:Skill.Fps', amountPln: null, delta: 0.82, detail: 'practice' },
      { code: 'stat:Credibility', amountPln: null, delta: 2, detail: 'chat-raid' },
    ],
    ending: null,
    ...overrides,
  };
}

export function careerView(overrides: Partial<CareerView> = {}): CareerView {
  return {
    careerId: TEST_CAREER_ID,
    nickname: 'Streamer',
    version: 1,
    status: 'active',
    ending: null,
    background: Background.dormRoomNewcomer,
    difficulty: Difficulty.normal,
    pronouns: PronounSet.woman,
    week: 1,
    lastSettledWeek: 0,
    stats: stats(),
    next: planNext(),
    lastReport: null,
    ...overrides,
  };
}

export function actionOutcome(career: CareerView, extras: { roll?: RollView; report?: WeekReportView } = {}) {
  return { career, roll: extras.roll ?? null, report: extras.report ?? null };
}
```

`source/frontend/test/api.ts`:

```ts
import { http, HttpResponse } from 'msw';

import type { ActionRequest, BatchRequest, CareerView } from '@/api/generated/game.schemas';

import { server } from './server';

export function problem(status: number, code: string, extra: Record<string, unknown> = {}) {
  return HttpResponse.json({ status, code, detail: null, ...extra }, { status, headers: { 'Content-Type': 'application/problem+json' } });
}

/** `GET /v1/careers/:id` answers with each view in turn and then keeps answering with the last one. */
export function serveCareer(...views: CareerView[]) {
  let call = 0;
  const seen: (string | null)[] = [];
  server.use(
    http.get('/v1/careers/:id', ({ request }) => {
      seen.push(request.headers.get('authorization'));
      const view = views[Math.min(call, views.length - 1)];
      call += 1;
      return HttpResponse.json(view);
    }),
  );
  return seen;
}

interface Received<T> {
  readonly body: T;
  readonly authorization: string | null;
}

function capture<T>(path: string, respond: (body: T, call: number) => Response | Promise<Response>) {
  const received: Received<T>[] = [];
  server.use(
    http.post(path, async ({ request }) => {
      const body = (await request.json()) as T;
      received.push({ body, authorization: request.headers.get('authorization') });
      return respond(body, received.length);
    }),
  );
  return received;
}

export const captureActions = (respond: (body: ActionRequest, call: number) => Response | Promise<Response>) =>
  capture<ActionRequest>('/v1/careers/:id/actions', respond);

export const captureBatches = (respond: (body: BatchRequest, call: number) => Response | Promise<Response>) =>
  capture<BatchRequest>('/v1/careers/:id/batches', respond);
```

If Orval writes the schema module under another name, import the same types from the file it wrote; keep the names.

- [ ] **Step 2: Write the failing tests**

`source/frontend/src/features/create-career/schema.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { createCareerSchema } from './schema';

const form = (nickname: string) => ({ nickname, background: 'dormRoomNewcomer', difficulty: 'normal', pronouns: 'woman' });

describe('the nickname rule mirrors the server (TD-92)', () => {
  it.each(['Streamer', 'Zażółć Gęślą', 'a_b-c.d', '  Trim Me  ', 'abc', 'x'.repeat(20)])('accepts %j', (nickname) => {
    expect(createCareerSchema.safeParse(form(nickname)).success).toBe(true);
  });

  it.each(['ab', 'x'.repeat(21), 'emoji 😀', 'a<b>', '   '])('rejects %j', (nickname) => {
    expect(createCareerSchema.safeParse(form(nickname)).success).toBe(false);
  });

  it('stores the nickname trimmed', () => {
    expect(createCareerSchema.parse(form('  Trim Me  ')).nickname).toBe('Trim Me');
  });
});
```

`source/frontend/src/features/create-career/CreateCareerScreen.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import type { CreateCareerRequest } from '@/api/generated/game.schemas';
import { knownCareers } from '@/careers/knownCareers';
import { pln } from '@/format';

import { problem } from '../../../test/api';
import { careerOptions, careerView } from '../../../test/fixtures';
import { renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';

function serveOptions() {
  server.use(http.get('/v1/career-options', () => HttpResponse.json(careerOptions())));
}

function captureCreation(respond: () => Response) {
  const bodies: CreateCareerRequest[] = [];
  server.use(
    http.post('/v1/careers', async ({ request }) => {
      bodies.push((await request.json()) as CreateCareerRequest);
      return respond();
    }),
  );
  return bodies;
}

describe('character creation', () => {
  it('shows each background with its starting numbers from the API', async () => {
    serveOptions();
    renderApp('/nowa-kariera');

    const dorm = await screen.findByRole('radio', { name: /Świeżak z akademika/u });
    expect(dorm).toBeChecked();
    expect(dorm.closest('label')).toHaveTextContent(`Kasa: ${pln(2000)}`);
    expect(dorm.closest('label')).toHaveTextContent('Mocna strona: FPS');
    expect(screen.getByRole('radio', { name: /Człowiek z branży/u }).closest('label')).toHaveTextContent('×0,90');
  });

  it('rejects an invalid nickname before sending anything', async () => {
    serveOptions();
    const bodies = captureCreation(() => HttpResponse.json({}));
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'ab');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(await screen.findByText('Nick musi mieć 3–20 znaków: litery, cyfry, spacja, _ - .')).toBeInTheDocument();
    expect(screen.getByLabelText('Nick')).toHaveAttribute('aria-invalid', 'true');
    expect(bodies).toEqual([]);
  });

  it('sends the chosen profile, remembers the career and opens it', async () => {
    serveOptions();
    const bodies = captureCreation(() => HttpResponse.json({ careerId: TEST_CAREER_ID, secret: 'fresh-secret', career: careerView({ nickname: 'Zażółć 1' }) }, { status: 201 }));
    const { user, router } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), '  Zażółć 1 ');
    await user.click(screen.getByRole('radio', { name: /Lokalny memiarz/u }));
    await user.click(screen.getByRole('radio', { name: /Łatwy/u }));
    await user.click(screen.getByRole('radio', { name: 'forma neutralna' }));
    expect(screen.getByRole('region', { name: 'Podsumowanie' })).toHaveTextContent('Lokalny memiarz');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/kariera/${TEST_CAREER_ID}`);
    });
    expect(bodies).toEqual([{ nickname: 'Zażółć 1', background: 'localMemeCreator', difficulty: 'easy', pronouns: 'nonbinary' }]);
    expect(knownCareers()[0]).toMatchObject({ careerId: TEST_CAREER_ID, nickname: 'Zażółć 1', secret: 'fresh-secret' });
  });

  it('shows the server field error on the nickname', async () => {
    serveOptions();
    captureCreation(() => problem(400, 'validation-failed', { errors: { nickname: ['Nickname is invalid.'] } }));
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(await screen.findByText('Nick musi mieć 3–20 znaków: litery, cyfry, spacja, _ - .')).toBeInTheDocument();
  });

  it('shows a generic error when the server fails', async () => {
    serveOptions();
    captureCreation(() => problem(500, 'internal-error'));
    const { user } = renderApp('/nowa-kariera');

    await user.type(await screen.findByLabelText('Nick'), 'Valid Name');
    await user.click(screen.getByRole('button', { name: 'Zaczynam karierę' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się założyć kariery. Spróbuj ponownie.');
  });
});
```

Run: `npx vitest run src/features/create-career` → FAIL.

- [ ] **Step 3: Implement**

`source/frontend/src/features/create-career/schema.ts`:

```ts
import { z } from 'zod';

import { Background, Difficulty, PronounSet } from '@/api/generated/game.schemas';

// The server is the authority (TD-92); this mirror only saves a round trip. Letters of any script, digits, space, _ - .
const nicknamePattern = /^[\p{L}\p{Nd} _.-]{3,20}$/u;

export const createCareerSchema = z.object({
  nickname: z.string().trim().regex(nicknamePattern, { error: 'create.errors.nickname' }),
  background: z.enum(Background),
  difficulty: z.enum(Difficulty),
  pronouns: z.enum(PronounSet),
});

export type CreateCareerForm = z.input<typeof createCareerSchema>;
```

`source/frontend/src/features/create-career/CreateCareerScreen.tsx`:

```tsx
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { createCareer, getGetCareerQueryKey, useGetCareerOptions } from '@/api/generated/game';
import type { BackgroundOptionView, CareerOptionsView, CreatedCareer, CreateCareerRequest } from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import { rememberCareer } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';
import { Panel } from '@/components/ui/Panel';
import { count, multiplier, pln } from '@/format';

import { type CreateCareerForm, createCareerSchema } from './schema';

const fields = ['nickname', 'background', 'difficulty', 'pronouns'] as const;

function strongest(option: BackgroundOptionView): string {
  const [best] = Object.entries(option.skill).toSorted((a, b) => b[1] - a[1]);
  return best?.[0] ?? 'justChatting';
}

export function CreateCareerScreen() {
  const { t } = useTranslation();
  const options = useGetCareerOptions();
  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('create.title')}</h1>
      {options.data ? <CreateCareerForm options={options.data} /> : <p role="status">{t('create.loading')}</p>}
    </div>
  );
}

function CreateCareerForm({ options }: { readonly options: CareerOptionsView }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const form = useForm<CreateCareerForm, unknown, CreateCareerRequest>({
    resolver: zodResolver(createCareerSchema),
    defaultValues: { nickname: '', background: 'dormRoomNewcomer', difficulty: 'normal', pronouns: 'woman' },
  });
  const creation = useMutation<CreatedCareer, ApiRequestError, CreateCareerRequest>({
    mutationFn: (request) => createCareer(request),
    onSuccess: async (created) => {
      rememberCareer({ careerId: created.careerId, nickname: created.career.nickname, secret: created.secret, createdAt: new Date().toISOString() });
      queryClient.setQueryData(getGetCareerQueryKey(created.careerId), created.career);
      await navigate({ to: '/kariera/$careerId', params: { careerId: created.careerId } });
    },
    onError: (error) => {
      for (const field of fields) {
        if (field in error.errors) {
          form.setError(field, { message: field === 'nickname' ? 'create.errors.nickname' : 'create.errors.field' });
        }
      }
    },
  });
  // useWatch, not watch(): the React Compiler lint rules reject watch() (eslint-plugin-react-hooks 7).
  const watched = useWatch({ control: form.control });
  const nickname = watched.nickname ?? '';
  const background = watched.background ?? 'dormRoomNewcomer';
  const difficulty = watched.difficulty ?? 'normal';
  const pronouns = watched.pronouns ?? 'woman';
  const nicknameError = form.formState.errors.nickname?.message;

  return (
    <form
      noValidate
      className="grid gap-3"
      onSubmit={(event) => {
        void form.handleSubmit((request) => {
          creation.mutate(request);
        })(event);
      }}
    >
      <div className="panel">
        <label htmlFor="nickname" className="block font-bold">
          {t('create.nickname')}
        </label>
        <input
          id="nickname"
          className="choice w-full"
          autoComplete="off"
          aria-describedby="nickname-hint"
          aria-invalid={nicknameError ? true : undefined}
          {...form.register('nickname')}
        />
        <p id="nickname-hint" className="muted">
          {nicknameError ? <span className="bad">{t(nicknameError)}</span> : t('create.nicknameHint')}
        </p>
      </div>

      <fieldset className="choices panel">
        <legend className="panel-title">{t('create.background')}</legend>
        {options.backgrounds.map((option) => (
          <label key={option.background} className="choice">
            <input type="radio" value={option.background} {...form.register('background')} />
            <span className="font-bold">{t(`background.${option.background}`)}</span>
            <span>{t('create.cash', { value: pln(option.cashPln) })}</span>
            <span>{t('create.followers', { value: count(option.followers) })}</span>
            <span>{t('create.strongest', { value: t(`category.${strongest(option)}`) })}</span>
            <span>{t('create.equipment', { value: option.equipment.microphone })}</span>
            <span>{t('create.multiplier', { value: multiplier(option.scoreMultiplier) })}</span>
          </label>
        ))}
      </fieldset>

      <fieldset className="choices panel">
        <legend className="panel-title">{t('create.difficulty')}</legend>
        {options.difficulties.map((option) => (
          <label key={option.difficulty} className="choice">
            <input type="radio" value={option.difficulty} {...form.register('difficulty')} />
            <span className="font-bold">{t(`difficulty.${option.difficulty}`)}</span>
            <span>{t('create.multiplier', { value: multiplier(option.scoreMultiplier) })}</span>
          </label>
        ))}
      </fieldset>

      <fieldset className="choices panel">
        <legend className="panel-title">{t('create.pronouns')}</legend>
        {options.pronouns.map((pronouns) => (
          <label key={pronouns} className="choice">
            <input type="radio" value={pronouns} {...form.register('pronouns')} />
            {t(`pronouns.${pronouns}`)}
          </label>
        ))}
      </fieldset>

      <Panel title={t('create.summary')}>
        <p>
          {nickname.trim() || '—'} · {t(`background.${background}`)} · {t(`difficulty.${difficulty}`)} · {t(`pronouns.${pronouns}`)}
        </p>
        {creation.error !== null && creation.error.code !== 'validation-failed' && (
          <p role="alert" className="bad">
            {t('create.errors.generic')}
          </p>
        )}
        <Button type="submit" variant="primary" pending={creation.isPending}>
          {t('create.submit')}
        </Button>
      </Panel>
    </form>
  );
}
```

The summary `Panel` is a `section` with `aria-labelledby`, so `getByRole('region', { name: 'Podsumowanie' })` finds it.

- [ ] **Step 4: Run, document and commit**

Run: `npx vitest run` → PASS; frontend checks → PASS with ≥ 80% lines.

`docs/content-and-experience.md`: in §3, item 2 *Home*, add "**M2 (TD-98):** the home screen lists the careers this browser knows and starts a new one; slots, imports, recovery, the leaderboard and the expiry notice follow in M4." Item 3 *Character creation*: "**M2:** the ranked/unranked choice is absent (every M2 career is unranked, TD-90); the background and difficulty numbers come from `GET /v1/career-options` (TD-101)." In §4, add a paragraph "**Visual themes (owner decision, 2026-10-15).** The game looks deliberately cheap and amateur — a cheap OBS overlay from old streams by default, with Windows 98 shareware and a 2005 fan site as alternatives in settings — while usability, contrast and focus meet the requirements below in every theme. A theme changes presentation only."

`docs/requirements-traceability.md`: the 18+ gate and character-option rows reference #11 with `In progress (M2)` or `Implemented (M2)` as their remaining scope dictates (the gate itself is complete; the Sensitive content setting is M3–M5).

Check the links.

```bash
git add source/frontend docs
git commit -m "feat(frontend): create a career from the server's starting profiles" -m "Refs #11." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: [external, owner yes] Review, push, open PR 2, merge**

One fresh reviewer over `develop..feature/m2c-11-entry`; fix Important findings test-first. With the owner's yes: push, `gh pr create --base develop --title "M2-C entry: themes, 18+ gate, home and character creation (#11)" --body "<summary, test plan, Refs #11, 🤖 line>"`; wait for every required check and 0 SonarQube Cloud issues on the PR; with the owner's yes merge, sync `develop`, watch its run, confirm 0 issues on `develop`.

---

# PR 3 — The game screen and one manual week (#12)

Start from the updated `develop`: `git checkout develop && git pull --ff-only && git checkout -b feature/m2c-12-week`.

### Task 10: Career data and repeat-safe commands

**Files:**
- Create: `source/frontend/src/features/career/useCareer.ts`, `source/frontend/src/features/career/useCareerCommand.ts`, `source/frontend/src/features/career/RetryBanner.tsx`, `source/frontend/src/features/career/CommandNotice.tsx`
- Test: `source/frontend/src/features/career/useCareer.test.tsx`, `source/frontend/src/features/career/useCareerCommand.test.tsx`

**Interfaces:**
- Consumes: generated `useGetCareer`, `getGetCareerQueryKey`, `submitAction`, `ActionType`, types `ActionRequest`, `ActionOutcome`, `CareerView`; `careerSecret`, `careerAuth` (Task 7); `ApiRequestError` (Task 4); test helpers (Tasks 7, 9).
- Produces: `useCareer(careerId)` → `{ secret: string | null; query }` (no request without a secret); `careerMutationKey(careerId)`; `interface CareerCommand<TBody> { run(body); retry(); pending; failure }`; `useCareerCommand(careerId, send, careerOf, onDone?)`; `type ActionInput = Omit<ActionRequest, 'commandId'>`; `useActionCommand(careerId, secret, onDone?)`; `useLatestCommand(careerId)` → `{ status: 'idle' | 'pending' | 'success' | 'error'; error: ApiRequestError | null }`; `RetryBanner({ command })` (shown only for a network failure); `CommandNotice({ careerId })` (the localized reason of the latest coded failure).

- [ ] **Step 1: Write the failing tests**

`source/frontend/src/features/career/useCareer.test.tsx`:

```tsx
import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import { createQueryClient } from '@/app/queryClient';

import { serveCareer } from '../../../test/api';
import { careerView } from '../../../test/fixtures';
import { rememberTestCareer, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';
import { useCareer } from './useCareer';

function Wrapper({ children }: { readonly children: ReactNode }) {
  return <QueryClientProvider client={createQueryClient({ retryDelay: 0 })}>{children}</QueryClientProvider>;
}

describe('useCareer', () => {
  it('treats a career without a stored secret as not found and asks nothing', () => {
    let requests = 0;
    server.use(
      http.all('*', () => {
        requests += 1;
        return HttpResponse.error();
      }),
    );

    const { result } = renderHook(() => useCareer(TEST_CAREER_ID), { wrapper: Wrapper });

    expect(result.current.secret).toBeNull();
    expect(result.current.query.fetchStatus).toBe('idle');
    expect(requests).toBe(0);
  });

  it('reads the career with its stored secret', async () => {
    rememberTestCareer();
    const seen = serveCareer(careerView({ version: 3 }));

    const { result } = renderHook(() => useCareer(TEST_CAREER_ID), { wrapper: Wrapper });

    await waitFor(() => {
      expect(result.current.query.data?.version).toBe(3);
    });
    expect(seen).toEqual(['Bearer secret-1']);
  });
});
```

`source/frontend/src/features/career/useCareerCommand.test.tsx`:

```tsx
import { type QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import { getGetCareerQueryKey } from '@/api/generated/game';
import { ActionType } from '@/api/generated/game.schemas';
import { createQueryClient } from '@/app/queryClient';

import { captureActions, problem, serveCareer } from '../../../test/api';
import { actionOutcome, careerView } from '../../../test/fixtures';
import { rememberTestCareer, TEST_CAREER_ID } from '../../../test/render';
import { useCareer } from './useCareer';
import { useActionCommand, useLatestCommand } from './useCareerCommand';

const settle = { expectedVersion: 1, type: ActionType.settleWeek };

function setup() {
  const queryClient: QueryClient = createQueryClient({ retryDelay: 0 });
  function Wrapper({ children }: { readonly children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return { queryClient, Wrapper };
}

describe('career commands', () => {
  it('sends the step with the career secret and stores the confirmed career', async () => {
    const received = captureActions(() => HttpResponse.json(actionOutcome(careerView({ version: 2 }))));
    const { queryClient, Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), { wrapper: Wrapper });

    act(() => {
      result.current.run(settle);
    });

    await waitFor(() => {
      expect(queryClient.getQueryData(getGetCareerQueryKey(TEST_CAREER_ID))).toMatchObject({ version: 2 });
    });
    expect(received[0]).toMatchObject({ body: settle, authorization: 'Bearer secret-1' });
    expect(received[0]?.body.commandId).toMatch(/^[\da-f-]{36}$/u);
  });

  it('retries a network failure with the same command id', async () => {
    const received = captureActions((_, call) => (call === 1 ? HttpResponse.error() : HttpResponse.json(actionOutcome(careerView({ version: 2 })))));
    const { Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), { wrapper: Wrapper });

    act(() => {
      result.current.run(settle);
    });

    await waitFor(() => {
      expect(result.current.pending).toBe(false);
    });
    expect(received).toHaveLength(2);
    expect(received[1]?.body.commandId).toBe(received[0]?.body.commandId);
    expect(result.current.failure).toBeNull();
  });

  it('keeps the intent for a manual retry once the automatic retries fail', async () => {
    const received = captureActions((_, call) => (call <= 3 ? HttpResponse.error() : HttpResponse.json(actionOutcome(careerView({ version: 2 })))));
    const { Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), { wrapper: Wrapper });

    act(() => {
      result.current.run(settle);
    });
    await waitFor(() => {
      expect(result.current.failure?.isNetwork).toBe(true);
    });
    act(() => {
      result.current.retry();
    });

    await waitFor(() => {
      expect(result.current.failure).toBeNull();
    });
    expect(received).toHaveLength(4);
    expect(new Set(received.map((r) => r.body.commandId)).size).toBe(1);
  });

  it('gives every new intent a new command id', async () => {
    const received = captureActions(() => HttpResponse.json(actionOutcome(careerView({ version: 2 }))));
    const { Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), { wrapper: Wrapper });

    act(() => {
      result.current.run(settle);
    });
    await waitFor(() => {
      expect(received).toHaveLength(1);
    });
    await waitFor(() => {
      expect(result.current.pending).toBe(false);
    });
    act(() => {
      result.current.run(settle);
    });

    await waitFor(() => {
      expect(received).toHaveLength(2);
    });
    expect(received[1]?.body.commandId).not.toBe(received[0]?.body.commandId);
  });

  it('ignores a second run while the first is pending', async () => {
    const received = captureActions(async () => {
      await delay(30);
      return HttpResponse.json(actionOutcome(careerView({ version: 2 })));
    });
    const { Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), { wrapper: Wrapper });

    act(() => {
      result.current.run(settle);
      result.current.run(settle);
    });

    await waitFor(() => {
      expect(result.current.pending).toBe(false);
    });
    expect(received).toHaveLength(1);
  });

  it('refetches the career after a stale version', async () => {
    rememberTestCareer();
    serveCareer(careerView({ version: 1 }), careerView({ version: 7 }));
    captureActions(() => problem(409, 'stale-version'));
    const { Wrapper } = setup();
    const { result } = renderHook(
      () => ({ career: useCareer(TEST_CAREER_ID), command: useActionCommand(TEST_CAREER_ID, 'secret-1'), latest: useLatestCommand(TEST_CAREER_ID) }),
      { wrapper: Wrapper },
    );
    await waitFor(() => {
      expect(result.current.career.query.data?.version).toBe(1);
    });

    act(() => {
      result.current.command.run(settle);
    });

    await waitFor(() => {
      expect(result.current.career.query.data?.version).toBe(7);
    });
    expect(result.current.command.failure?.code).toBe('stale-version');
    expect(result.current.latest).toMatchObject({ status: 'error', error: { code: 'stale-version' } });
  });
});
```

Run: `npx vitest run src/features/career` → FAIL (modules missing).

- [ ] **Step 2: Implement**

`source/frontend/src/features/career/useCareer.ts`:

```ts
import { useGetCareer } from '@/api/generated/game';
import { careerAuth, careerSecret } from '@/careers/knownCareers';

/** The server-confirmed career. Without a stored secret this browser cannot open it, so nothing is requested. */
export function useCareer(careerId: string) {
  const secret = careerSecret(careerId);
  const query = useGetCareer(careerId, {
    query: { enabled: secret !== null },
    request: secret === null ? undefined : careerAuth(secret),
  });
  return { secret, query };
}
```

`source/frontend/src/features/career/useCareerCommand.ts`:

```ts
import { useMutation, useMutationState, useQueryClient } from '@tanstack/react-query';
import { useRef } from 'react';

import { getGetCareerQueryKey, submitAction } from '@/api/generated/game';
import type { ActionOutcome, ActionRequest, CareerView } from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import { careerAuth } from '@/careers/knownCareers';

export const careerMutationKey = (careerId: string) => ['career', careerId] as const;

type WithCommandId<T> = T & { readonly commandId: string };

export interface CareerCommand<TBody> {
  /** Starts a new intent with a fresh command ID; ignored while a request of this command runs. */
  readonly run: (body: TBody) => void;
  /** Sends the last unanswered intent again with its command ID, so the server answers it instead of rolling again. */
  readonly retry: () => void;
  readonly pending: boolean;
  readonly failure: ApiRequestError | null;
}

/**
 * One kind of career step. The response's career view replaces the cached career at once; a coded failure refetches
 * the career (another tab may have moved it); a lost connection keeps the intent for a retry with the same ID.
 */
export function useCareerCommand<TBody, TResult>(
  careerId: string,
  send: (request: WithCommandId<TBody>) => Promise<TResult>,
  careerOf: (result: TResult) => CareerView,
  onDone?: (result: TResult) => void,
): CareerCommand<TBody> {
  const queryClient = useQueryClient();
  const inFlight = useRef(false);
  const last = useRef<WithCommandId<TBody> | null>(null);
  const mutation = useMutation<TResult, ApiRequestError, WithCommandId<TBody>>({
    mutationKey: careerMutationKey(careerId),
    mutationFn: send,
    onSuccess: (result) => {
      last.current = null;
      queryClient.setQueryData(getGetCareerQueryKey(careerId), careerOf(result));
      onDone?.(result);
    },
    onError: async (error) => {
      if (!error.isNetwork) {
        last.current = null;
        await queryClient.invalidateQueries({ queryKey: getGetCareerQueryKey(careerId) });
      }
    },
    onSettled: () => {
      inFlight.current = false;
    },
  });

  // A ref, not isPending: two clicks in one tick both see the same render.
  const submit = (request: WithCommandId<TBody>) => {
    if (inFlight.current) {
      return;
    }

    inFlight.current = true;
    last.current = request;
    mutation.mutate(request);
  };

  return {
    run: (body) => {
      submit({ ...body, commandId: crypto.randomUUID() });
    },
    retry: () => {
      if (last.current !== null) {
        submit(last.current);
      }
    },
    pending: mutation.isPending,
    failure: mutation.error,
  };
}

export type ActionInput = Omit<ActionRequest, 'commandId'>;

export function useActionCommand(careerId: string, secret: string, onDone?: (outcome: ActionOutcome) => void) {
  return useCareerCommand<ActionInput, ActionOutcome>(
    careerId,
    (request) => submitAction(careerId, request, careerAuth(secret)),
    (outcome) => outcome.career,
    onDone,
  );
}

export interface LatestCommand {
  readonly status: 'idle' | 'pending' | 'success' | 'error';
  readonly error: ApiRequestError | null;
}

/** The newest step of this career, wherever it was started: the save status and the failure notice read it. */
export function useLatestCommand(careerId: string): LatestCommand {
  const states = useMutationState({ filters: { mutationKey: careerMutationKey(careerId) }, select: (mutation) => mutation.state });
  const latest = states.at(-1);
  return { status: latest?.status ?? 'idle', error: (latest?.error as ApiRequestError | null | undefined) ?? null };
}
```

`source/frontend/src/features/career/RetryBanner.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { ApiRequestError } from '@/api/http';
import { Button } from '@/components/ui/Button';

export interface RetryableCommand {
  readonly failure: ApiRequestError | null;
  readonly pending: boolean;
  readonly retry: () => void;
}

/** After a lost connection: no result is shown, and the retry sends the same command again. */
export function RetryBanner({ command }: { readonly command: RetryableCommand }) {
  const { t } = useTranslation();
  if (command.failure?.isNetwork !== true) {
    return null;
  }

  return (
    <div role="alert" className="panel">
      <p className="bad">{t('career.networkFailed')}</p>
      <Button onClick={command.retry} pending={command.pending}>
        {t('career.retry')}
      </Button>
    </div>
  );
}
```

`source/frontend/src/features/career/CommandNotice.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import { useLatestCommand } from './useCareerCommand';

/** Why the latest step was refused (for example another tab moved the career); the stage already shows the fresh step. */
export function CommandNotice({ careerId }: { readonly careerId: string }) {
  const { t } = useTranslation();
  const latest = useLatestCommand(careerId);
  if (latest.status !== 'error' || latest.error === null || latest.error.isNetwork) {
    return null;
  }

  return (
    <p role="status" className="panel">
      {t(`career.errors.${latest.error.code}`, { defaultValue: t('career.errors.generic') })}
    </p>
  );
}
```

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; typecheck, lint, format → exit 0.

```bash
git add source/frontend
git commit -m "feat(frontend): read careers and send steps that retry with the same command id" -m "Refs #12." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 11: Weekly report and settlement

**Files:**
- Create: `source/frontend/src/features/career/reportLabels.ts`, `source/frontend/src/features/career/stage/WeekReport.tsx`, `source/frontend/src/features/career/stage/SettleWeek.tsx`
- Test: `source/frontend/src/features/career/stage/WeekReport.test.tsx`, `source/frontend/src/features/career/stage/SettleWeek.test.tsx`

**Interfaces:**
- Consumes: `useActionCommand`, `RetryBanner` (Task 10); `StepHeading`, `Button` (Task 6); `count`, `pln`, `signed`, `stat` (Task 6).
- Produces: `lineLabel(t, code)`, `statLabel(t, code)`, `causeLabel(t, detail)`; `WeekReport({ report, onNext? })` (a step with focus and `Następny tydzień` when `onNext` is given, otherwise a plain section); `SettleWeek({ career, secret, onSettled(report) })`.

- [ ] **Step 1: Write the failing tests**

`source/frontend/src/features/career/stage/WeekReport.test.tsx`:

```tsx
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { pln, signed } from '@/format';

import { weekReport } from '../../../../test/fixtures';
import { WeekReport } from './WeekReport';

const items = (name: string) => within(screen.getByRole('list', { name })).getAllByRole('listitem').map((item) => item.textContent);

describe('weekly report', () => {
  it('lists income, costs, cash change and stat changes with signs and causes', async () => {
    const onNext = vi.fn();
    render(<WeekReport report={weekReport(1)} onNext={onNext} />);

    expect(screen.getByRole('heading', { name: 'Raport — tydzień 1' })).toHaveFocus();
    expect(items('Przychody')).toEqual([`Suby ${signed(23, pln)}`, `Donejty ${signed(17, pln)}`]);
    expect(items('Koszty')).toEqual([`Życie ${signed(-100, pln)}`]);
    expect(items('Zmiany statystyk')).toEqual(['Energia +25 (akcja tygodnia)', 'Umiejętności · FPS +0,8 (praktyka)', 'Wiarygodność +2 (Rajd na czat)']);
    expect(screen.getByText(signed(-60, pln))).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Następny tydzień' }));
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('shows an unknown line by its code and the ending of the career', () => {
    render(<WeekReport report={weekReport(9, { income: [{ code: 'income:new-thing', amountPln: 5, delta: null, detail: null }], ending: 'insolvency' })} />);

    expect(items('Przychody')).toEqual([`income:new-thing ${signed(5, pln)}`]);
    expect(screen.getByText('Bankructwo — kasa skończyła się na dobre.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
```

`source/frontend/src/features/career/stage/SettleWeek.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { expect, it, vi } from 'vitest';

import { captureActions } from '../../../../test/api';
import { actionOutcome, careerView, settleNext, weekReport } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { SettleWeek } from './SettleWeek';

it('settles the week and reveals its report', async () => {
  const received = captureActions(() => HttpResponse.json(actionOutcome(careerView({ version: 2, week: 2, lastSettledWeek: 1 }), { report: weekReport(1) })));
  const onSettled = vi.fn();
  const { user } = renderWithClient(<SettleWeek career={careerView({ next: settleNext() })} secret="secret-1" onSettled={onSettled} />);

  expect(screen.getByRole('heading', { name: 'Koniec streamu' })).toHaveFocus();
  await user.click(screen.getByRole('button', { name: 'Kończę tydzień' }));

  await waitFor(() => {
    expect(onSettled).toHaveBeenCalledWith(weekReport(1));
  });
  expect(received[0]?.body).toMatchObject({ expectedVersion: 1, type: 'settleWeek' });
});
```

Run: `npx vitest run src/features/career/stage` → FAIL.

- [ ] **Step 2: Implement**

`source/frontend/src/features/career/reportLabels.ts`:

```ts
import type { TFunction } from 'i18next';

import i18n from '@/i18n';

const camel = (pascal: string): string => pascal.slice(0, 1).toLowerCase() + pascal.slice(1);

/** `income:subscriptions` → "Suby"; an unknown code shows as itself rather than disappearing. */
export function lineLabel(t: TFunction, code: string): string {
  const key = `report.line.${code.replace(':', '.')}`;
  return i18n.exists(key) ? t(key) : code;
}

/** `stat:Energy` → "Energia"; `stat:Skill.Fps` → "Umiejętności · FPS". */
export function statLabel(t: TFunction, code: string): string {
  const [name = '', category] = code.replace(/^stat:/u, '').split('.');
  const key = `stat.${camel(name)}`;
  if (!i18n.exists(key)) {
    return code;
  }

  return category === undefined ? t(key) : `${t(key)} · ${t(`category.${camel(category)}`)}`;
}

/** Why a stat changed: the week's action, the stream, practice, or an event by its title. */
export function causeLabel(t: TFunction, detail: string | null): string | null {
  if (detail === null) {
    return null;
  }

  if (i18n.exists(`report.cause.${detail}`)) {
    return t(`report.cause.${detail}`);
  }

  return i18n.exists(`event.${detail}.title`) ? t(`event.${detail}.title`) : detail;
}
```

`source/frontend/src/features/career/stage/WeekReport.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { ReportLineView, WeekReportView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { count, pln, signed, stat } from '@/format';

import { causeLabel, lineLabel, statLabel } from '../reportLabels';

export interface WeekReportProps {
  readonly report: WeekReportView;
  /** Given when the report is the current step; folded reports (last report, batch ledger) have no button. */
  readonly onNext?: () => void;
}

export function WeekReport({ report, onNext }: WeekReportProps) {
  const { t } = useTranslation();
  const title = t('report.title', { week: report.week });
  return (
    <div className="grid gap-2">
      {onNext ? <StepHeading>{title}</StepHeading> : <h3 className="panel-title">{title}</h3>}
      <dl className="grid grid-cols-[auto_1fr] gap-x-3">
        <dt>{t('report.viewers')}</dt>
        <dd className="m-0">{count(report.liveViewers)}</dd>
        <dt>{t('report.followersGained')}</dt>
        <dd className="good m-0">{signed(report.followersGained)}</dd>
        <dt>{t('report.followersLost')}</dt>
        <dd className="bad m-0">{signed(-report.followersLost)}</dd>
        <dt>{t('report.followersNet')}</dt>
        <dd className="m-0">{signed(report.followersNet)}</dd>
        <dt>{t('report.cashDelta')}</dt>
        <dd className="m-0">{signed(report.cashDelta, pln)}</dd>
      </dl>
      <MoneyLines title={t('report.income')} lines={report.income} sign={1} />
      <MoneyLines title={t('report.costs')} lines={report.costs} sign={-1} />
      <h3 className="font-bold">{t('report.statChanges')}</h3>
      {report.statChanges.length === 0 ? (
        <p>{t('report.none')}</p>
      ) : (
        <ul aria-label={t('report.statChanges')}>
          {report.statChanges.map((line, index) => {
            const cause = causeLabel(t, line.detail);
            return (
              <li key={`${line.code}-${String(index)}`}>
                {statLabel(t, line.code)} {signed(line.delta ?? 0, stat)}
                {cause === null ? '' : ` (${cause})`}
              </li>
            );
          })}
        </ul>
      )}
      {report.ending !== null && <p className="font-bold">{t(`career.ending.${report.ending}`)}</p>}
      {onNext && (
        <Button variant="primary" onClick={onNext}>
          {t('report.next')}
        </Button>
      )}
    </div>
  );
}

function MoneyLines({ title, lines, sign }: { readonly title: string; readonly lines: readonly ReportLineView[]; readonly sign: 1 | -1 }) {
  const { t } = useTranslation();
  return (
    <>
      <h3 className="font-bold">{title}</h3>
      {lines.length === 0 ? (
        <p>{t('report.none')}</p>
      ) : (
        <ul aria-label={title}>
          {lines.map((line, index) => (
            <li key={`${line.code}-${String(index)}`}>
              {lineLabel(t, line.code)} {signed(sign * (line.amountPln ?? 0), pln)}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
```

`source/frontend/src/features/career/stage/SettleWeek.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { CareerView, WeekReportView } from '@/api/generated/game.schemas';
import { ActionType } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';

export interface SettleWeekProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onSettled: (report: WeekReportView) => void;
}

export function SettleWeek({ career, secret, onSettled }: SettleWeekProps) {
  const { t } = useTranslation();
  const command = useActionCommand(career.careerId, secret, (outcome) => {
    if (outcome.report !== null) {
      onSettled(outcome.report);
    }
  });
  return (
    <div className="grid gap-3">
      <StepHeading>{t('settle.title')}</StepHeading>
      <p>{t('settle.body')}</p>
      <RetryBanner command={command} />
      <Button
        variant="primary"
        pending={command.pending}
        onClick={() => {
          command.run({ expectedVersion: career.version, type: ActionType.settleWeek });
        }}
      >
        {t('settle.action')}
      </Button>
    </div>
  );
}
```

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; typecheck, lint, format → exit 0.

```bash
git add source/frontend
git commit -m "feat(frontend): settle a week and show its report line by line" -m "Refs #12." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 12: Week plan

**Files:**
- Create: `source/frontend/src/features/career/plans.ts`, `source/frontend/src/features/career/stage/PlanWeek.tsx`
- Test: `source/frontend/src/features/career/plans.test.ts`, `source/frontend/src/features/career/stage/PlanWeek.test.tsx`

**Interfaces:**
- Consumes: `useActionCommand`, `RetryBanner` (Task 10); `WeekReport` (Task 11); generated `MainAction`, `Category`, `EquipmentSlot`, `ActionType`, `PlanOptionView`, `CareerStatsView`.
- Produces: `optionFor(plans, action, category, slot)`, `actionBlock(plans, action, category): string | null`, `strongestCategory(stats)`; `PlanWeek({ career, secret, children? })` — `children` sits next to the start button (Task 15 puts the batch launcher there).

- [ ] **Step 1: Write the failing tests**

`source/frontend/src/features/career/plans.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { planNext, stats } from '../../../test/fixtures';
import { actionBlock, optionFor, strongestCategory } from './plans';

describe('plans', () => {
  it('finds the option of an action, category and slot', () => {
    const { plans } = planNext((plan) => (plan.upgradeSlot === 'camera' ? 'unaffordable' : null));

    expect(optionFor(plans, 'improveProduction', 'chess', 'camera')).toMatchObject({ disabledReason: 'unaffordable' });
    expect(optionFor(plans, 'rest', 'chess', null)).toMatchObject({ plan: { mainAction: 'rest', category: 'chess' } });
  });

  it('blocks an action only when none of its options is legal', () => {
    const { plans } = planNext((plan) => (plan.mainAction === 'rest' || plan.upgradeSlot === 'camera' ? null : 'forced-rest'));

    expect(actionBlock(plans, 'rest', 'fps')).toBeNull();
    expect(actionBlock(plans, 'improveProduction', 'fps')).toBeNull();
    expect(actionBlock(plans, 'trainCategory', 'fps')).toBe('forced-rest');
  });

  it('starts on the strongest category', () => {
    expect(strongestCategory(stats())).toBe('fps');
  });
});
```

`source/frontend/src/features/career/stage/PlanWeek.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { captureActions } from '../../../../test/api';
import { actionOutcome, careerView, eventNext, planNext, weekReport } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { PlanWeek } from './PlanWeek';

const answer = () => HttpResponse.json(actionOutcome(careerView({ version: 2, next: eventNext() })));

describe('week plan', () => {
  it('starts the chosen week with the version it was shown', async () => {
    const received = captureActions(answer);
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    expect(screen.getByRole('heading', { name: 'Plan tygodnia' })).toHaveFocus();
    expect(screen.getByRole('radio', { name: 'FPS' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Zaczynam stream' })).toBeDisabled();
    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(received).toHaveLength(1);
    });
    expect(received[0]?.body).toMatchObject({ expectedVersion: 1, type: 'startWeek', mainAction: 'rest', category: 'fps', upgradeSlot: null });
  });

  it('explains why an action is disabled', () => {
    renderWithClient(<PlanWeek career={careerView({ next: planNext((plan) => (plan.mainAction === 'rest' ? null : 'forced-rest')) })} secret="secret-1" />);

    const train = screen.getByRole('radio', { name: /Trening/u });
    expect(train).toBeDisabled();
    expect(train).toHaveAccessibleDescription('Musisz odpocząć');
  });

  it('asks for a slot when improving production and blocks an unaffordable one', async () => {
    const received = captureActions(answer);
    const { user } = renderWithClient(
      <PlanWeek career={careerView({ next: planNext((plan) => (plan.upgradeSlot === 'camera' ? 'unaffordable' : null)) })} secret="secret-1" />,
    );

    await user.click(screen.getByRole('radio', { name: 'Ulepszenie sprzętu' }));
    expect(screen.getByRole('button', { name: 'Zaczynam stream' })).toBeDisabled();
    expect(screen.getByRole('radio', { name: /Kamera/u })).toHaveAccessibleDescription('Nie stać cię');
    await user.click(screen.getByRole('radio', { name: /Mikrofon/u }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({ mainAction: 'improveProduction', upgradeSlot: 'microphone' });
    });
  });

  it('starts the week once on a double click', async () => {
    const received = captureActions(async () => {
      await delay(50);
      return answer();
    });
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.dblClick(screen.getByRole('button', { name: 'Zaczynam stream' }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Zaczynam stream' })).not.toHaveAttribute('aria-busy');
    });
    expect(received).toHaveLength(1);
  });

  it('offers a retry with the same command id after a lost connection', async () => {
    const received = captureActions((_, call) => (call <= 3 ? HttpResponse.error() : answer()));
    const { user } = renderWithClient(<PlanWeek career={careerView()} secret="secret-1" />);

    await user.click(screen.getByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Wybór nie został zapisany — spróbuj ponownie.');
    await user.click(screen.getByRole('button', { name: 'Spróbuj ponownie' }));

    await waitFor(() => {
      expect(received).toHaveLength(4);
    });
    expect(new Set(received.map((r) => r.body.commandId)).size).toBe(1);
  });

  it('keeps the last report folded away', () => {
    renderWithClient(<PlanWeek career={careerView({ week: 4, lastSettledWeek: 3, lastReport: weekReport(3) })} secret="secret-1" />);

    const summary = screen.getByText('Ostatni raport (tydzień 3)');
    expect(summary.closest('details')).not.toHaveAttribute('open');
  });
});
```

Run: `npx vitest run src/features/career` → FAIL.

- [ ] **Step 2: Implement**

`source/frontend/src/features/career/plans.ts`:

```ts
import { type CareerStatsView, Category, type EquipmentSlot, MainAction, type PlanOptionView } from '@/api/generated/game.schemas';

/** The option the player is about to choose; the slot matters only for an upgrade. */
export function optionFor(
  plans: readonly PlanOptionView[],
  action: MainAction,
  category: Category,
  slot: EquipmentSlot | null,
): PlanOptionView | undefined {
  return plans.find(
    (option) =>
      option.plan.mainAction === action && option.plan.category === category && (action !== MainAction.improveProduction || option.plan.upgradeSlot === slot),
  );
}

/** Why an action cannot be chosen in a category, or null when at least one of its options is legal. */
export function actionBlock(plans: readonly PlanOptionView[], action: MainAction, category: Category): string | null {
  const options = plans.filter((option) => option.plan.mainAction === action && option.plan.category === category);
  if (options.some((option) => option.disabledReason === null)) {
    return null;
  }

  return options[0]?.disabledReason ?? 'unknown';
}

export function strongestCategory(stats: CareerStatsView): Category {
  const [first] = Object.values(Category).toSorted((a, b) => stats.skill[b] - stats.skill[a]);
  return first ?? Category.justChatting;
}
```

`source/frontend/src/features/career/stage/PlanWeek.tsx`:

```tsx
import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionType, type CareerView, Category, EquipmentSlot, MainAction } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

import { actionBlock, optionFor, strongestCategory } from '../plans';
import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';
import { WeekReport } from './WeekReport';

export interface PlanWeekProps {
  readonly career: CareerView;
  readonly secret: string;
  /** Placed next to the start button (the batch launcher). */
  readonly children?: ReactNode;
}

export function PlanWeek({ career, secret, children }: PlanWeekProps) {
  const { t } = useTranslation();
  const plans = career.next.plans;
  const command = useActionCommand(career.careerId, secret);
  const [action, setAction] = useState<MainAction | null>(null);
  const [category, setCategory] = useState<Category>(() => strongestCategory(career.stats));
  const [slot, setSlot] = useState<EquipmentSlot | null>(null);
  const chosen = action === null ? undefined : optionFor(plans, action, category, slot);
  const ready = chosen?.disabledReason === null;
  const reason = (code: string) => t(`disabled.${code}`, { defaultValue: t('disabled.unknown') });

  return (
    <div className="grid gap-3">
      <StepHeading>{t('plan.title')}</StepHeading>

      <fieldset className="choices">
        <legend className="font-bold">{t('plan.action')}</legend>
        {Object.values(MainAction).map((option) => {
          const block = actionBlock(plans, option, category);
          const hint = `plan-action-${option}`;
          return (
            <label key={option} className="choice">
              <input
                type="radio"
                name="action"
                value={option}
                checked={action === option}
                disabled={block !== null}
                aria-describedby={block === null ? undefined : hint}
                onChange={() => {
                  setAction(option);
                  setSlot(null);
                }}
              />
              <span className="font-bold">{t(`action.${option}`)}</span>
              {block !== null && (
                <span id={hint} className="muted">
                  {reason(block)}
                </span>
              )}
            </label>
          );
        })}
      </fieldset>

      <fieldset className="choices">
        <legend className="font-bold">{t('plan.category')}</legend>
        {Object.values(Category).map((option) => (
          <label key={option} className="choice">
            <input
              type="radio"
              name="category"
              value={option}
              checked={category === option}
              onChange={() => {
                setCategory(option);
              }}
            />
            {t(`category.${option}`)}
          </label>
        ))}
      </fieldset>

      {action === MainAction.improveProduction && (
        <fieldset className="choices">
          <legend className="font-bold">{t('plan.slot')}</legend>
          {Object.values(EquipmentSlot).map((option) => {
            const block = optionFor(plans, action, category, option)?.disabledReason ?? null;
            const hint = `plan-slot-${option}`;
            return (
              <label key={option} className="choice">
                <input
                  type="radio"
                  name="slot"
                  value={option}
                  checked={slot === option}
                  disabled={block !== null}
                  aria-describedby={block === null ? undefined : hint}
                  onChange={() => {
                    setSlot(option);
                  }}
                />
                <span className="font-bold">{t(`slot.${option}`)}</span>
                <span className="muted">{t('stat.tier', { value: career.stats.equipment[option] })}</span>
                {block !== null && (
                  <span id={hint} className="muted">
                    {reason(block)}
                  </span>
                )}
              </label>
            );
          })}
        </fieldset>
      )}

      <RetryBanner command={command} />
      <div className="flex flex-wrap gap-2">
        <Button
          variant="primary"
          disabled={!ready}
          pending={command.pending}
          onClick={() => {
            if (action !== null) {
              command.run({
                expectedVersion: career.version,
                type: ActionType.startWeek,
                mainAction: action,
                category,
                upgradeSlot: action === MainAction.improveProduction ? slot : null,
              });
            }
          }}
        >
          {t('plan.start')}
        </Button>
        {children}
      </div>

      {career.lastReport !== null && (
        <details className="panel">
          <summary>{t('plan.lastReport', { week: career.lastReport.week })}</summary>
          <WeekReport report={career.lastReport} />
        </details>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; typecheck, lint, format → exit 0.

```bash
git add source/frontend
git commit -m "feat(frontend): plan a week with the server's legal options and their reasons" -m "Refs #12." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 13: Live events and rolls

**Files:**
- Create: `source/frontend/src/features/career/stage/LiveEvent.tsx`, `source/frontend/src/features/career/stage/RollResult.tsx`
- Test: `source/frontend/src/features/career/stage/LiveEvent.test.tsx`, `source/frontend/src/features/career/stage/RollResult.test.tsx`

**Interfaces:**
- Consumes: `useActionCommand`, `RetryBanner` (Task 10); `percent`, `signed`, `stat` (Task 6).
- Produces: `LiveEvent({ career, secret, onRolled(roll) })`; `RollResult({ roll, onNext })`.

- [ ] **Step 1: Write the failing tests**

`source/frontend/src/features/career/stage/LiveEvent.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { captureActions } from '../../../../test/api';
import { actionOutcome, careerView, eventNext, roll, settleNext } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { LiveEvent } from './LiveEvent';

const answer = () => HttpResponse.json(actionOutcome(careerView({ version: 3, next: settleNext() }), { roll: roll() }));

describe('live event', () => {
  it('shows the odds, modifiers and critical chance of each choice', () => {
    renderWithClient(<LiveEvent career={careerView({ next: eventNext() })} secret="secret-1" onRolled={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Rajd na czat' })).toHaveFocus();
    const welcome = screen.getByRole('radio', { name: /Przywitaj gości/u }).closest('label');
    expect(welcome).toHaveTextContent('Szansa: 58%');
    expect(welcome).toHaveTextContent('Wiarygodność +3 pp');
    expect(welcome).toHaveTextContent('Możliwy wynik krytyczny');
    expect(screen.getByRole('radio', { name: /Graj dalej/u }).closest('label')).toHaveTextContent('Szansa: pewne');
    expect(screen.getByRole('button', { name: 'Wybieram' })).toBeDisabled();
  });

  it('resolves the chosen response and reveals the roll', async () => {
    const received = captureActions(answer);
    const onRolled = vi.fn();
    const { user } = renderWithClient(<LiveEvent career={careerView({ version: 2, next: eventNext() })} secret="secret-1" onRolled={onRolled} />);

    await user.click(screen.getByRole('radio', { name: /Przywitaj gości/u }));
    await user.click(screen.getByRole('button', { name: 'Wybieram' }));

    await waitFor(() => {
      expect(onRolled).toHaveBeenCalledWith(roll());
    });
    expect(received[0]?.body).toMatchObject({ expectedVersion: 2, type: 'resolveEvent', choiceId: 'welcome' });
  });

  it('confirms a choice once on a double click', async () => {
    const received = captureActions(async () => {
      await delay(50);
      return answer();
    });
    const onRolled = vi.fn();
    const { user } = renderWithClient(<LiveEvent career={careerView({ next: eventNext() })} secret="secret-1" onRolled={onRolled} />);

    await user.click(screen.getByRole('radio', { name: /Graj dalej/u }));
    await user.dblClick(screen.getByRole('button', { name: 'Wybieram' }));

    await waitFor(() => {
      expect(onRolled).toHaveBeenCalledOnce();
    });
    expect(received).toHaveLength(1);
  });
});
```

`source/frontend/src/features/career/stage/RollResult.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { roll } from '../../../../test/fixtures';
import { RollResult } from './RollResult';

describe('roll result', () => {
  it('shows the roll against its threshold and the outcome in words', () => {
    render(<RollResult roll={roll()} onNext={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Wynik: Sukces' })).toHaveFocus();
    expect(screen.getByText('Rzut 37 przy progu 58')).toBeInTheDocument();
  });

  it('says a guaranteed choice needed no roll and moves on', async () => {
    const onNext = vi.fn();
    render(<RollResult roll={roll({ choiceId: 'ignore', roll: null, threshold: 100 })} onNext={onNext} />);

    expect(screen.getByText('Pewny wybór — bez rzutu')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Dalej' }));
    expect(onNext).toHaveBeenCalledOnce();
  });

  it('marks a failure in words, not only by colour', () => {
    render(<RollResult roll={roll({ roll: 91, outcome: 'criticalFailure' })} onNext={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Wynik: Krytyczna porażka' })).toBeInTheDocument();
  });
});
```

Run: `npx vitest run src/features/career/stage` → FAIL.

- [ ] **Step 2: Implement**

`source/frontend/src/features/career/stage/LiveEvent.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionType, type CareerView, type RollView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { percent, signed, stat } from '@/format';

import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';

export interface LiveEventProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onRolled: (roll: RollView) => void;
}

/** One live situation without a timer: every response shows its chance and modifiers before the player commits. */
export function LiveEvent({ career, secret, onRolled }: LiveEventProps) {
  const { t } = useTranslation();
  const [choiceId, setChoiceId] = useState<string | null>(null);
  const command = useActionCommand(career.careerId, secret, (outcome) => {
    if (outcome.roll !== null) {
      onRolled(outcome.roll);
    }
  });
  const event = career.next.event;
  if (event === null) {
    return null;
  }

  return (
    <div className="grid gap-3">
      <p>
        <span className="badge-live">● {t('event.live')}</span>
      </p>
      <StepHeading>{t(event.titleKey, { defaultValue: t('event.untitled') })}</StepHeading>
      <p>{event.textPl}</p>
      <fieldset className="choices">
        <legend className="font-bold">{t('event.choices')}</legend>
        {event.choices.map((choice) => (
          <label key={choice.choiceId} className="choice">
            <input
              type="radio"
              name="choice"
              value={choice.choiceId}
              checked={choiceId === choice.choiceId}
              onChange={() => {
                setChoiceId(choice.choiceId);
              }}
            />
            <span className="font-bold">{choice.labelPl}</span>
            <span>{t('event.chance', { value: choice.guaranteed ? t('event.certain') : percent(choice.probability) })}</span>
            {choice.modifiers.length > 0 && (
              <span className="muted">
                {t('event.modifiers')}:{' '}
                {choice.modifiers
                  .map((modifier) => `${t(modifier.labelKey, { defaultValue: modifier.labelKey })} ${t('event.points', { value: signed(modifier.value * 100, stat) })}`)
                  .join(', ')}
              </span>
            )}
            {choice.allowsCritical && <span className="muted">{t('event.critical')}</span>}
          </label>
        ))}
      </fieldset>
      <RetryBanner command={command} />
      <Button
        variant="primary"
        disabled={choiceId === null}
        pending={command.pending}
        onClick={() => {
          if (choiceId !== null) {
            command.run({ expectedVersion: career.version, type: ActionType.resolveEvent, choiceId });
          }
        }}
      >
        {t('event.choose')}
      </Button>
    </div>
  );
}
```

`source/frontend/src/features/career/stage/RollResult.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { RollView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

const gains = new Set(['success', 'criticalSuccess']);

export function RollResult({ roll, onNext }: { readonly roll: RollView; readonly onNext: () => void }) {
  const { t } = useTranslation();
  const outcome = t(`roll.outcome.${roll.outcome}`);
  const good = gains.has(roll.outcome);
  return (
    <div className="grid gap-3">
      <StepHeading>{t('roll.title', { outcome })}</StepHeading>
      <p>{roll.roll === null ? t('roll.noRoll') : t('roll.rolled', { roll: roll.roll, threshold: roll.threshold })}</p>
      <p className={good ? 'good font-bold' : 'bad font-bold'}>
        <span aria-hidden="true">{good ? '✔ ' : '✖ '}</span>
        {outcome}
      </p>
      <Button variant="primary" onClick={onNext}>
        {t('roll.next')}
      </Button>
    </div>
  );
}
```

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; typecheck, lint, format → exit 0.

```bash
git add source/frontend
git commit -m "feat(frontend): play live events with visible odds and show each roll" -m "Refs #12." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 14: Game screen, PR 3 documentation and delivery

**Files:**
- Create: `source/frontend/src/features/career/deskStore.ts`, `source/frontend/src/features/career/TopBar.tsx`, `source/frontend/src/features/career/StatsRail.tsx`, `source/frontend/src/features/career/Stage.tsx`, `source/frontend/src/features/career/CareerDesk.tsx`, `source/frontend/src/features/career/stage/EndedView.tsx`
- Modify: `source/frontend/src/features/career/CareerScreen.tsx` (replace the Task 7 placeholder)
- Test: `source/frontend/src/features/career/CareerScreen.test.tsx`
- Modify: `docs/requirements-traceability.md`
- Modify: `source/frontend/src/styles/index.css` and the components that carry a decoration (theme decorations, below)

**Theme decorations (added 2026-10-15 after the PR 2 review).** Spec §5 names decorations that no earlier task builds: `win98` blue title bars on panels, a dotted focus ring and block progress bars; `web2005` glossy gradients on frames and buttons, a visitor counter and red `NOWE!` labels. This task builds them with the game screen, where the bars and panels they decorate appear. Decorations stay presentation only: the visitor counter and labels are `aria-hidden`, and the token contrast tests in `src/styles/index.test.ts` cover any new colour pair.

**Interfaces:**
- Consumes: everything from Tasks 10–13; `forgetCareer` (Task 7).
- Produces: `useDesk` (Zustand: `statsOpen`, `toggleStats`); `type Revealed` (`roll` | `report`; Task 15 adds `batch`); `Stage({ career, secret })`; `CareerDesk({ career, secret })`; `CareerScreen` (route `/kariera/$careerId`).

- [ ] **Step 1: Write the failing tests**

`source/frontend/src/features/career/CareerScreen.test.tsx`:

```tsx
import { screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { knownCareers } from '@/careers/knownCareers';
import { pln } from '@/format';

import { captureActions, problem, serveCareer } from '../../../test/api';
import { actionOutcome, careerView, endedNext, eventNext, roll, settleNext, stats, weekReport } from '../../../test/fixtures';
import { rememberTestCareer, renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';

const path = `/kariera/${TEST_CAREER_ID}`;
const desk = () => screen.getByRole('region', { name: 'Stan kariery' });

describe('game screen', () => {
  it('plays a full manual week through the stage', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    captureActions((body) => {
      switch (body.type) {
        case 'startWeek': {
          return HttpResponse.json(actionOutcome(careerView({ version: 2, next: eventNext() })));
        }
        case 'resolveEvent': {
          return HttpResponse.json(actionOutcome(careerView({ version: 3, next: settleNext() }), { roll: roll() }));
        }
        default: {
          return HttpResponse.json(actionOutcome(careerView({ version: 4, week: 2, lastSettledWeek: 1, lastReport: weekReport(1) }), { report: weekReport(1) }));
        }
      }
    });
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));
    expect(await screen.findByRole('heading', { name: 'Rajd na czat' })).toHaveFocus();
    await user.click(screen.getByRole('radio', { name: /Przywitaj gości/u }));
    await user.click(screen.getByRole('button', { name: 'Wybieram' }));
    expect(await screen.findByRole('heading', { name: 'Wynik: Sukces' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Dalej' }));
    await user.click(await screen.findByRole('button', { name: 'Kończę tydzień' }));
    expect(await screen.findByRole('heading', { name: 'Raport — tydzień 1' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Następny tydzień' }));

    expect(await screen.findByRole('heading', { name: 'Plan tygodnia' })).toBeInTheDocument();
    expect(desk()).toHaveTextContent('Tydzień 2');
    expect(desk()).toHaveTextContent('Zapisano');
  });

  it('shows the server-confirmed state and the last report after a reload', async () => {
    rememberTestCareer();
    const seen = serveCareer(careerView({ week: 4, lastSettledWeek: 3, lastReport: weekReport(3), stats: stats({ cashPln: 2500 }) }));

    renderApp(path);

    expect(await screen.findByText('Ostatni raport (tydzień 3)')).toBeInTheDocument();
    expect(desk()).toHaveTextContent('Tydzień 4');
    expect(desk()).toHaveTextContent(pln(2500));
    expect(seen[0]).toBe('Bearer secret-1');
  });

  it('refetches the career after a stale version and says why', async () => {
    rememberTestCareer();
    serveCareer(careerView(), careerView({ version: 5, next: eventNext() }));
    captureActions(() => problem(409, 'stale-version'));
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    expect(await screen.findByRole('heading', { name: 'Rajd na czat' })).toBeInTheDocument();
    expect(screen.getByText('Kariera zmieniła się w innej karcie. Pokazuję aktualny stan.')).toBeInTheDocument();
  });

  it('shows the saving state while a step is pending', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    captureActions(async () => {
      await delay(50);
      return HttpResponse.json(actionOutcome(careerView({ version: 2, next: eventNext() })));
    });
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('radio', { name: 'Odpoczynek' }));
    await user.click(screen.getByRole('button', { name: 'Zaczynam stream' }));

    expect(within(desk()).getByRole('status')).toHaveTextContent('Zapisuję…');
    await waitFor(() => {
      expect(within(desk()).getByRole('status')).toHaveTextContent('Zapisano');
    });
  });

  it('can forget a career the server does not find', async () => {
    rememberTestCareer();
    server.use(http.get('/v1/careers/:id', () => problem(404, 'career-not-found')));
    const { user, router } = renderApp(path);

    await user.click(await screen.findByRole('button', { name: 'Usuń z listy' }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(knownCareers()).toEqual([]);
  });

  it('shows not found when this browser has no secret for the career', async () => {
    renderApp(path);

    expect(await screen.findByRole('heading', { name: 'Nie znaleziono kariery' })).toBeInTheDocument();
  });

  it('offers a reload when the career cannot be loaded', async () => {
    rememberTestCareer();
    let call = 0;
    server.use(
      http.get('/v1/careers/:id', () => {
        call += 1;
        return call === 1 ? problem(500, 'internal-error') : HttpResponse.json(careerView());
      }),
    );
    const { user } = renderApp(path);

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się wczytać kariery.');
    await user.click(screen.getByRole('button', { name: 'Odśwież' }));

    expect(await screen.findByRole('heading', { name: 'Plan tygodnia' })).toBeInTheDocument();
  });

  it('opens and closes the statistics drawer', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    const { user } = renderApp(path);

    const toggle = await screen.findByRole('button', { name: 'Statystyki' });
    const rail = screen.getByRole('complementary', { name: 'Statystyki' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(rail).toHaveClass('open');
    await user.click(toggle);
    expect(rail).not.toHaveClass('open');
  });

  it('shows the ending of a finished career', async () => {
    rememberTestCareer();
    serveCareer(careerView({ status: 'ended', ending: 'completed', next: endedNext() }));

    renderApp(path);

    expect(await screen.findByText('Kariera ukończona — dwadzieścia lat streamowania za tobą.')).toBeInTheDocument();
  });
});
```

Run: `npx vitest run src/features/career/CareerScreen.test.tsx` → FAIL (placeholder screen).

- [ ] **Step 2: Implement**

`source/frontend/src/features/career/deskStore.ts`:

```ts
import { create } from 'zustand';

interface DeskState {
  readonly statsOpen: boolean;
  readonly toggleStats: () => void;
}

/** Transient UI state only; the career itself lives in the query cache and on the server. */
export const useDesk = create<DeskState>()((set) => ({
  statsOpen: false,
  toggleStats: () => {
    set((state) => ({ statsOpen: !state.statsOpen }));
  },
}));
```

`source/frontend/src/features/career/TopBar.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { count, pln } from '@/format';

import { useDesk } from './deskStore';
import { type LatestCommand, useLatestCommand } from './useCareerCommand';

function saveStatus(latest: LatestCommand): 'saved' | 'saving' | 'offline' {
  if (latest.status === 'pending') {
    return 'saving';
  }

  return latest.error?.isNetwork === true ? 'offline' : 'saved';
}

export function TopBar({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  const latest = useLatestCommand(career.careerId);
  const statsOpen = useDesk((state) => state.statsOpen);
  const toggleStats = useDesk((state) => state.toggleStats);
  return (
    <section className="topbar" aria-label={t('career.summaryLabel')}>
      <span className="font-bold">{career.nickname}</span>
      <span>{t('career.week', { week: career.week })}</span>
      <span>
        {t('career.cash')}: {pln(career.stats.cashPln)}
      </span>
      <span>
        {t('career.followers')}: {count(career.stats.followers)}
      </span>
      <span>
        {t('career.viewers')}: {count(career.stats.lastLiveViewers)}
      </span>
      <span role="status">{t(`career.${saveStatus(latest)}`)}</span>
      <Button className="rail-toggle" aria-expanded={statsOpen} aria-controls="stats-rail" onClick={toggleStats}>
        {t('career.stats')}
      </Button>
    </section>
  );
}
```

`source/frontend/src/features/career/StatsRail.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import { type CareerView, Category, EquipmentSlot } from '@/api/generated/game.schemas';
import { stat } from '@/format';

import { useDesk } from './deskStore';

const bars = ['energy', 'burnout', 'drama', 'credibility'] as const;

export function StatsRail({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  const open = useDesk((state) => state.statsOpen);
  const stats = career.stats;
  return (
    <aside id="stats-rail" className={open ? 'rail panel open' : 'rail panel'} aria-label={t('career.stats')}>
      <h2 className="panel-title">{t('career.stats')}</h2>
      {bars.map((bar) => {
        const id = `stat-${bar}`;
        return (
          <div key={bar} className="grid grid-cols-[1fr_auto] items-center gap-x-2">
            <span id={id}>{t(`stat.${bar}`)}</span>
            <span>{stat(stats[bar])}</span>
            <meter className="col-span-2 w-full" min={0} max={100} value={stats[bar]} aria-labelledby={id} />
          </div>
        );
      })}
      <table className="mt-2 w-full">
        <caption className="text-left font-bold">{t('stat.skill')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('plan.category')}</th>
            <th scope="col">{t('stat.skill')}</th>
            <th scope="col">{t('stat.boredom')}</th>
          </tr>
        </thead>
        <tbody>
          {Object.values(Category).map((category) => (
            <tr key={category}>
              <th scope="row" className="text-left">
                {t(`category.${category}`)}
              </th>
              <td>{stat(stats.skill[category])}</td>
              <td>{stat(stats.boredom[category])}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <h3 className="font-bold">{t('stat.equipment')}</h3>
      <ul>
        {Object.values(EquipmentSlot).map((slot) => (
          <li key={slot}>
            {t(`slot.${slot}`)}: {t('stat.tier', { value: stats.equipment[slot] })}
          </li>
        ))}
      </ul>
    </aside>
  );
}
```

`source/frontend/src/features/career/stage/EndedView.tsx`:

```tsx
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';
import { StepHeading } from '@/components/ui/StepHeading';

/** The reason the career ended; the full ending screen with score and diploma arrives in M3. */
export function EndedView({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-3">
      <StepHeading>{t('career.endedTitle')}</StepHeading>
      {career.ending !== null && <p>{t(`career.ending.${career.ending}`)}</p>}
      <p>
        <Link className="btn btn-secondary" to="/">
          {t('career.backHome')}
        </Link>
      </p>
    </div>
  );
}
```

`source/frontend/src/features/career/Stage.tsx`:

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { type CareerView, NextStepKind, type RollView, type WeekReportView } from '@/api/generated/game.schemas';

import { CommandNotice } from './CommandNotice';
import { EndedView } from './stage/EndedView';
import { LiveEvent } from './stage/LiveEvent';
import { PlanWeek } from './stage/PlanWeek';
import { RollResult } from './stage/RollResult';
import { SettleWeek } from './stage/SettleWeek';
import { WeekReport } from './stage/WeekReport';

/** A result the player reads before the stage moves to the server's next step. */
export type Revealed = { readonly kind: 'roll'; readonly roll: RollView } | { readonly kind: 'report'; readonly report: WeekReportView };

interface StageViewProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly revealed: Revealed | null;
  readonly onReveal: (revealed: Revealed, announcement: string) => void;
  readonly onClose: () => void;
}

/** Server-driven (TD-98): the stage renders the career's next step; only revealed results are local. */
export function Stage({ career, secret }: { readonly career: CareerView; readonly secret: string }) {
  const [revealed, setRevealed] = useState<Revealed | null>(null);
  const [announcement, setAnnouncement] = useState('');
  return (
    <div className="panel grid gap-3">
      <div aria-live="polite" className="sr-only">
        {announcement}
      </div>
      <CommandNotice careerId={career.careerId} />
      <StageView
        career={career}
        secret={secret}
        revealed={revealed}
        onReveal={(next, text) => {
          setRevealed(next);
          setAnnouncement(text);
        }}
        onClose={() => {
          setRevealed(null);
        }}
      />
    </div>
  );
}

function StageView({ career, secret, revealed, onReveal, onClose }: StageViewProps) {
  const { t } = useTranslation();
  if (revealed?.kind === 'roll') {
    return <RollResult roll={revealed.roll} onNext={onClose} />;
  }

  if (revealed?.kind === 'report') {
    return <WeekReport report={revealed.report} onNext={onClose} />;
  }

  switch (career.next.kind) {
    case NextStepKind.planWeek: {
      return <PlanWeek career={career} secret={secret} />;
    }
    case NextStepKind.resolveEvent: {
      return (
        <LiveEvent
          key={career.version}
          career={career}
          secret={secret}
          onRolled={(roll) => {
            onReveal({ kind: 'roll', roll }, t('roll.title', { outcome: t(`roll.outcome.${roll.outcome}`) }));
          }}
        />
      );
    }
    case NextStepKind.settleWeek: {
      return (
        <SettleWeek
          career={career}
          secret={secret}
          onSettled={(report) => {
            onReveal({ kind: 'report', report }, t('report.title', { week: report.week }));
          }}
        />
      );
    }
    case NextStepKind.ended: {
      return <EndedView career={career} />;
    }
  }
}
```

`source/frontend/src/features/career/CareerDesk.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';

import { Stage } from './Stage';
import { StatsRail } from './StatsRail';
import { TopBar } from './TopBar';

/** Layout A: the top bar, the wide stage and the statistics rail (a drawer on narrow screens). */
export function CareerDesk({ career, secret }: { readonly career: CareerView; readonly secret: string }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-3">
      <h1 className="sr-only">
        {t('career.summaryLabel')}: {career.nickname}
      </h1>
      <TopBar career={career} />
      <div className="desk">
        <Stage key={career.careerId} career={career} secret={secret} />
        <StatsRail career={career} />
      </div>
    </div>
  );
}
```

Replace `source/frontend/src/features/career/CareerScreen.tsx`:

```tsx
import { Link, useNavigate, useParams } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { forgetCareer } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';

import { CareerDesk } from './CareerDesk';
import { useCareer } from './useCareer';

export function CareerScreen() {
  const { careerId } = useParams({ from: '/kariera/$careerId' });
  const { t } = useTranslation();
  const { secret, query } = useCareer(careerId);

  if (secret === null || query.error?.code === 'career-not-found') {
    return <CareerNotFound careerId={careerId} />;
  }

  if (query.data !== undefined) {
    return <CareerDesk career={query.data} secret={secret} />;
  }

  if (query.isError) {
    return (
      <div role="alert" className="panel">
        <p>{t('career.loadFailed')}</p>
        <Button
          onClick={() => {
            void query.refetch();
          }}
        >
          {t('career.reload')}
        </Button>
      </div>
    );
  }

  return <p role="status">{t('career.loading')}</p>;
}

function CareerNotFound({ careerId }: { readonly careerId: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <div className="panel grid gap-2">
      <h1 className="heading text-2xl">{t('career.notFound')}</h1>
      <p>{t('career.notFoundBody')}</p>
      <div className="flex flex-wrap gap-2">
        <Button
          onClick={() => {
            forgetCareer(careerId);
            void navigate({ to: '/' });
          }}
        >
          {t('career.forget')}
        </Button>
        <Link className="btn btn-secondary" to="/">
          {t('career.backHome')}
        </Link>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Run, document and commit**

Run: `npx vitest run` → PASS; frontend checks → PASS with ≥ 80% lines. With the API from M2-B on `http://localhost:8080` (`dotnet run --project WebApp` from `source/backend`, `ConnectionStrings__Game` set), run `npm run dev`, create a career and play one week by hand, then reload the page.

`docs/requirements-traceability.md`: the rows #12 delivers (one complete manual week in the browser, visible odds and modifiers before a choice, autosave visible after reload, no double settlement from the UI) name #12 with `In progress (M2)` until the milestone release, or `Implemented (M2)` where no later milestone owns part of the row. Check the links.

```bash
git add source/frontend docs
git commit -m "feat(frontend): put the week on a streamer desk driven by the server's next step" -m "Refs #12." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: [external, owner yes] Review, push, open PR 3, merge**

One fresh reviewer over `develop..feature/m2c-12-week`; fix Important findings test-first. With the owner's yes: push, `gh pr create --base develop --title "M2-C week: the game screen and one manual week (#12)" --body "<summary, test plan, Refs #12, 🤖 line>"`; wait for every required check and 0 SonarQube Cloud issues on the PR; with the owner's yes merge, sync `develop`, watch its run, confirm 0 issues on `develop`.

---

# PR 4 — The 13-week batch UI, the local stack and the browser journey (#13 client side, #14)

Start from the updated `develop`: `git checkout develop && git pull --ff-only && git checkout -b feature/m2c-13-14-batch-stack`.

### Task 15: The 13-week batch in the browser

**Files:**
- Create: `source/frontend/src/features/career/batch/reasons.ts`, `source/frontend/src/features/career/batch/BatchLauncher.tsx`, `source/frontend/src/features/career/batch/BatchResult.tsx`
- Modify: `source/frontend/src/features/career/useCareerCommand.ts`, `source/frontend/src/features/career/Stage.tsx`, `source/frontend/test/fixtures.ts`
- Test: `source/frontend/src/features/career/batch/reasons.test.ts`, `source/frontend/src/features/career/batch/BatchLauncher.test.tsx`, `source/frontend/src/features/career/batch/BatchResult.test.tsx`, `source/frontend/src/features/career/CareerScreen.test.tsx`

**Interfaces:**
- Consumes: generated `runBatch`, types `BatchOutcome`, `BatchRequest`, `ReasonView`, `AutoWeekView`, `BatchStopReason`; `useCareerCommand` (Task 10); `PlanWeek` `children` slot (Task 12); `WeekReport` (Task 11).
- Produces: `type BatchInput = Omit<BatchRequest, 'commandId'>`; `useBatchCommand(careerId, secret, onDone?)`; `reasonText(t, reason)`; `BatchLauncher({ career, secret, onDone(outcome) })`; `BatchResult({ outcome, onNext })`; `Revealed` gains `{ kind: 'batch'; outcome: BatchOutcome }`; fixture `batchOutcome(stopReason?, weeks?)`.

- [ ] **Step 1: Write the failing tests**

Add to `source/frontend/test/fixtures.ts` (extend the type import with `type AutoWeekView`, `type BatchOutcome`, `BatchStopReason`):

```ts
export function batchOutcome(stopReason: BatchStopReason = BatchStopReason.completed, weeks = 2): BatchOutcome {
  const ledger: AutoWeekView[] = Array.from({ length: weeks }, (_, index) => ({
    week: index + 2,
    plan: { mainAction: MainAction.rest, category: Category.fps, trainTarget: null, upgradeSlot: null },
    planReason: { code: 'rest-low-energy', args: { energy: '20.000000', threshold: '30', category: 'fps', categoryRule: 'bonus' } },
    choices: [{ eventId: 'chat-raid', choiceId: 'welcome', reason: { code: 'gamble-best-odds', args: { probability: '0.600000', threshold: '0.500000' } }, roll: roll() }],
    report: weekReport(index + 2),
  }));
  return {
    weeksCommitted: weeks,
    stopReason,
    weeks: ledger,
    summary: { weeksCommitted: weeks, followersNet: 12, cashDelta: -120, incomePln: 80, costsPln: 200, start: stats(), end: stats({ cashPln: 1880, followers: 12 }) },
    career: careerView({ version: 30, week: 2 + weeks, lastSettledWeek: 1 + weeks }),
  };
}
```

`source/frontend/src/features/career/batch/reasons.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { pln } from '@/format';
import i18n from '@/i18n';

import { reasonText } from './reasons';

const t = i18n.t.bind(i18n);
const plan = { category: 'fps', categoryRule: 'bonus' };

describe('policy reasons in Polish', () => {
  it.each([
    [{ code: 'rest-low-energy', args: { energy: '20.000000', threshold: '30', ...plan } }, 'Energia 20 poniżej 30 — odpoczynek. Kategoria FPS: mocna strona.'],
    [{ code: 'rest-high-burnout', args: { burnout: '75.000000', limit: '70.000000', ...plan } }, 'Wypalenie 75 powyżej 70 — odpoczynek. Kategoria FPS: mocna strona.'],
    [{ code: 'train-scheduled', args: { everyWeeks: '4', category: 'chess', categoryRule: 'rotation' } }, 'Co 4 tygodnie trening. Kategoria Szachy: rotacja.'],
    [{ code: 'fallback-rest', args: { preferred: 'improveProduction', category: 'moba', categoryRule: 'least-bored' } }, '„Ulepszenie sprzętu” niedostępne — odpoczynek. Kategoria MOBA: najmniej nudna.'],
    [{ code: 'gamble-best-odds', args: { probability: '0.600000', threshold: '0.500000' } }, 'Szansa 60% (próg 50%) — ryzyk-fizyk.'],
    [{ code: 'take-guaranteed', args: { bestProbability: '0.400000' } }, 'Najlepsza szansa 40% to za mało — pewny wybór.'],
    [{ code: 'take-guaranteed', args: {} }, 'Do wyboru tylko pewna opcja.'],
    [{ code: 'only-gambles', args: { probability: '0.400000' } }, 'Same ryzykowne opcje — szansa 40%.'],
    [{ code: 'brand-new-reason', args: {} }, 'Decyzja automatu.'],
  ])('explains %j', (reason, text) => {
    expect(reasonText(t, reason)).toBe(text);
  });

  it('formats money and equipment in an upgrade reason', () => {
    expect(reasonText(t, { code: 'upgrade-equipment', args: { slot: 'microphone', pricePln: '600', cashPln: '2000', ...plan } })).toBe(
      `Mikrofon za ${pln(600)} przy kasie ${pln(2000)} — ulepszenie. Kategoria FPS: mocna strona.`,
    );
  });
});
```

`source/frontend/src/features/career/batch/BatchLauncher.test.tsx`:

```tsx
import { screen, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { captureBatches } from '../../../../test/api';
import { batchOutcome, careerView } from '../../../../test/fixtures';
import { renderWithClient } from '../../../../test/render';
import { BatchLauncher } from './BatchLauncher';

describe('batch launcher', () => {
  it('asks before running and sends 13 weeks at the shown version', async () => {
    const received = captureBatches(() => HttpResponse.json(batchOutcome()));
    const onDone = vi.fn();
    const { user } = renderWithClient(<BatchLauncher career={careerView({ version: 4 })} secret="secret-1" onDone={onDone} />);

    await user.click(screen.getByRole('button', { name: 'Auto: 13 tygodni' }));
    expect(screen.getByRole('alertdialog', { name: 'Streamować automatycznie 13 tygodni?' })).toBeInTheDocument();
    expect(received).toEqual([]);
    await user.click(screen.getByRole('button', { name: 'Streamuj 13 tygodni' }));

    await waitFor(() => {
      expect(onDone).toHaveBeenCalledWith(batchOutcome());
    });
    expect(received[0]).toMatchObject({ body: { expectedVersion: 4, weeks: 13 }, authorization: 'Bearer secret-1' });
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('runs one batch on a double click and says it is running', async () => {
    const received = captureBatches(async () => {
      await delay(50);
      return HttpResponse.json(batchOutcome());
    });
    const onDone = vi.fn();
    const { user } = renderWithClient(<BatchLauncher career={careerView()} secret="secret-1" onDone={onDone} />);

    await user.click(screen.getByRole('button', { name: 'Auto: 13 tygodni' }));
    await user.dblClick(screen.getByRole('button', { name: 'Streamuj 13 tygodni' }));

    expect(screen.getByRole('status')).toHaveTextContent('Streamujesz 13 tygodni…');
    await waitFor(() => {
      expect(onDone).toHaveBeenCalledOnce();
    });
    expect(received).toHaveLength(1);
  });

  it('cancels without sending anything', async () => {
    const received = captureBatches(() => HttpResponse.json(batchOutcome()));
    const { user } = renderWithClient(<BatchLauncher career={careerView()} secret="secret-1" onDone={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Auto: 13 tygodni' }));
    await user.click(screen.getByRole('button', { name: 'Anuluj' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(received).toEqual([]);
  });
});
```

`source/frontend/src/features/career/batch/BatchResult.test.tsx`:

```tsx
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { BatchStopReason } from '@/api/generated/game.schemas';
import { pln } from '@/format';

import { batchOutcome } from '../../../../test/fixtures';
import { BatchResult } from './BatchResult';

describe('batch result', () => {
  it.each([
    [BatchStopReason.completed, 'Wszystkie 13 tygodni rozegrane.'],
    [BatchStopReason.pausedForDecision, 'Gra czeka na twoją decyzję.'],
    [BatchStopReason.careerEnded, 'Kariera zakończyła się w trakcie.'],
    [BatchStopReason.interrupted, 'Kariera zmieniła się w innej karcie — automat się zatrzymał.'],
  ])('explains why it stopped: %s', (stopReason, text) => {
    render(<BatchResult outcome={batchOutcome(stopReason)} onNext={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Wynik automatycznych tygodni' })).toHaveFocus();
    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('summarises the batch and lists every week with its reasons', async () => {
    const onNext = vi.fn();
    render(<BatchResult outcome={batchOutcome()} onNext={onNext} />);

    const cash = within(screen.getByRole('table', { name: 'Podsumowanie' })).getByRole('row', { name: /Kasa/u });
    expect(cash).toHaveTextContent(pln(2000));
    expect(cash).toHaveTextContent(pln(1880));
    const weeks = within(screen.getByRole('list', { name: 'Tydzień po tygodniu' })).getAllByRole('listitem', { name: /Tydzień/u });
    expect(weeks).toHaveLength(2);
    expect(weeks[0]).toHaveTextContent('Odpoczynek · FPS');
    expect(weeks[0]).toHaveTextContent('Energia 20 poniżej 30 — odpoczynek. Kategoria FPS: mocna strona.');
    expect(weeks[0]).toHaveTextContent('Rajd na czat: Przywitaj gości i odpal dla nich szybkie show — Sukces.');
    expect(weeks[0]).toHaveTextContent('Szansa 60% (próg 50%) — ryzyk-fizyk.');
    await userEvent.click(screen.getByRole('button', { name: 'Dalej' }));
    expect(onNext).toHaveBeenCalledOnce();
  });
});
```

Add to `source/frontend/src/features/career/CareerScreen.test.tsx` (import `captureBatches` and `batchOutcome`):

```tsx
  it('runs a 13-week batch from the week plan and returns to the next plan', async () => {
    rememberTestCareer();
    serveCareer(careerView());
    captureBatches(() => HttpResponse.json(batchOutcome()));
    const { user } = renderApp(path);

    await user.click(await screen.findByRole('button', { name: 'Auto: 13 tygodni' }));
    await user.click(screen.getByRole('button', { name: 'Streamuj 13 tygodni' }));
    expect(await screen.findByRole('heading', { name: 'Wynik automatycznych tygodni' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Dalej' }));

    expect(await screen.findByRole('heading', { name: 'Plan tygodnia' })).toBeInTheDocument();
    expect(desk()).toHaveTextContent('Tydzień 4');
  });
```

Run: `npx vitest run src/features/career` → FAIL.

- [ ] **Step 2: Implement**

Append to `source/frontend/src/features/career/useCareerCommand.ts` (add `runBatch` to the generated import and `BatchOutcome`, `BatchRequest` to the type import):

```ts
export type BatchInput = Omit<BatchRequest, 'commandId'>;

export function useBatchCommand(careerId: string, secret: string, onDone?: (outcome: BatchOutcome) => void) {
  return useCareerCommand<BatchInput, BatchOutcome>(
    careerId,
    (request) => runBatch(careerId, request, careerAuth(secret)),
    (outcome) => outcome.career,
    onDone,
  );
}
```

`source/frontend/src/features/career/batch/reasons.ts`:

```ts
import type { TFunction } from 'i18next';

import type { ReasonView } from '@/api/generated/game.schemas';
import { percent, pln, stat } from '@/format';

const numbers = new Set(['energy', 'burnout', 'drama', 'limit', 'threshold', 'everyWeeks']);
const probabilities = new Set(['probability', 'bestProbability']);
const money = new Set(['pricePln', 'cashPln']);
const labels: Record<string, string> = { slot: 'slot', category: 'category', preferred: 'action', categoryRule: 'reason.rule' };

/** Policy values arrive as invariant strings (`20.000000`, `0.600000`, `microphone`); show them the Polish way. */
function formatArg(t: TFunction, code: string, name: string, value: string): string {
  if (probabilities.has(name) || (name === 'threshold' && code === 'gamble-best-odds')) {
    return percent(Number(value));
  }

  if (numbers.has(name)) {
    return stat(Number(value));
  }

  if (money.has(name)) {
    return pln(Number(value));
  }

  const prefix = labels[name];
  return prefix === undefined ? value : t(`${prefix}.${value}`, { defaultValue: value });
}

/** Why the automatic strategy decided (spec §7, gameplay §8): the decision, then the category rule when there is one. */
export function reasonText(t: TFunction, reason: ReasonView): string {
  const args = Object.fromEntries(Object.entries(reason.args).map(([name, value]) => [name, formatArg(t, reason.code, name, value)]));
  const key = reason.code === 'take-guaranteed' && !('bestProbability' in reason.args) ? 'take-guaranteed-only' : reason.code;
  const decision = t(`reason.${key}`, { ...args, defaultValue: t('reason.unknown') });
  const { category, categoryRule } = args;
  return category === undefined ? decision : `${decision} ${t('reason.category', { category, rule: categoryRule ?? '' })}`;
}
```

`source/frontend/src/features/career/batch/BatchLauncher.tsx`:

```tsx
import { AlertDialog } from 'radix-ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { BatchOutcome, CareerView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';

import { RetryBanner } from '../RetryBanner';
import { useBatchCommand } from '../useCareerCommand';

export interface BatchLauncherProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onDone: (outcome: BatchOutcome) => void;
}

/** One request plays up to 13 weeks; each week is already saved, so the dialog only closes when it is done. */
export function BatchLauncher({ career, secret, onDone }: BatchLauncherProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const command = useBatchCommand(career.careerId, secret, (outcome) => {
    setOpen(false);
    onDone(outcome);
  });
  return (
    <AlertDialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!command.pending) {
          setOpen(next);
        }
      }}
    >
      <AlertDialog.Trigger asChild>
        <Button>{t('batch.launch')}</Button>
      </AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content className="dialog panel">
          <AlertDialog.Title className="panel-title">{t('batch.confirmTitle')}</AlertDialog.Title>
          <AlertDialog.Description>{t('batch.confirmBody')}</AlertDialog.Description>
          {command.pending && <p role="status">{t('batch.running')}</p>}
          <RetryBanner command={command} />
          <div className="mt-3 flex flex-wrap gap-2">
            <Button
              variant="primary"
              pending={command.pending}
              onClick={() => {
                command.run({ expectedVersion: career.version, weeks: 13 });
              }}
            >
              {t('batch.confirm')}
            </Button>
            <AlertDialog.Cancel asChild>
              <Button disabled={command.pending}>{t('batch.cancel')}</Button>
            </AlertDialog.Cancel>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
```

`source/frontend/src/features/career/batch/BatchResult.tsx`:

```tsx
import { useTranslation } from 'react-i18next';

import type { BatchOutcome, CareerStatsView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { count, pln, signed, stat } from '@/format';

import { WeekReport } from '../stage/WeekReport';
import { reasonText } from './reasons';

type SummaryKey = keyof Pick<CareerStatsView, 'cashPln' | 'followers' | 'energy' | 'burnout' | 'drama' | 'credibility'>;

const summaryRows: readonly { label: string; key: SummaryKey; format: (value: number) => string }[] = [
  { label: 'career.cash', key: 'cashPln', format: pln },
  { label: 'career.followers', key: 'followers', format: count },
  { label: 'stat.energy', key: 'energy', format: stat },
  { label: 'stat.burnout', key: 'burnout', format: stat },
  { label: 'stat.drama', key: 'drama', format: stat },
  { label: 'stat.credibility', key: 'credibility', format: stat },
];

export function BatchResult({ outcome, onNext }: { readonly outcome: BatchOutcome; readonly onNext: () => void }) {
  const { t } = useTranslation();
  const { summary } = outcome;
  return (
    <div className="grid gap-3">
      <StepHeading>{t('batch.title')}</StepHeading>
      <p className="topbar">{t(`batch.stop.${outcome.stopReason}`)}</p>
      <p>{t('batch.weeksCommitted', { value: outcome.weeksCommitted })}</p>

      <table className="w-full">
        <caption className="text-left font-bold">{t('batch.summary')}</caption>
        <thead>
          <tr>
            <td />
            <th scope="col">{t('batch.start')}</th>
            <th scope="col">{t('batch.end')}</th>
          </tr>
        </thead>
        <tbody>
          {summaryRows.map((row) => (
            <tr key={row.key}>
              <th scope="row" className="text-left">
                {t(row.label)}
              </th>
              <td>{row.format(summary.start[row.key])}</td>
              <td>{row.format(summary.end[row.key])}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3">
        <dt>{t('report.followersNet')}</dt>
        <dd className="m-0">{signed(summary.followersNet)}</dd>
        <dt>{t('report.cashDelta')}</dt>
        <dd className="m-0">{signed(summary.cashDelta, pln)}</dd>
        <dt>{t('report.income')}</dt>
        <dd className="m-0">{pln(summary.incomePln)}</dd>
        <dt>{t('report.costs')}</dt>
        <dd className="m-0">{pln(summary.costsPln)}</dd>
      </dl>

      <h3 className="font-bold">{t('batch.ledger')}</h3>
      <ol aria-label={t('batch.ledger')} className="grid list-none gap-2 p-0 md:grid-cols-2">
        {outcome.weeks.map((week) => {
          const heading = `batch-week-${String(week.week)}`;
          return (
            <li key={week.week} className="panel" aria-labelledby={heading}>
              <h4 id={heading} className="font-bold">
                {t('batch.week', { week: week.week })}
              </h4>
              <p>{t('batch.plan', { action: t(`action.${week.plan.mainAction}`), category: t(`category.${week.plan.category}`) })}</p>
              <p className="muted">{reasonText(t, week.planReason)}</p>
              {week.choices.length > 0 && (
                <ul>
                  {week.choices.map((choice, index) => (
                    <li key={`${choice.eventId}-${String(index)}`}>
                      {t('batch.choice', {
                        event: t(`event.${choice.eventId}.title`, { defaultValue: choice.eventId }),
                        choice: t(`event.${choice.eventId}.${choice.choiceId}`, { defaultValue: choice.choiceId }),
                      })}{' '}
                      — {t(`roll.outcome.${choice.roll.outcome}`)}. <span className="muted">{reasonText(t, choice.reason)}</span>
                    </li>
                  ))}
                </ul>
              )}
              <details>
                <summary>{t('report.title', { week: week.report.week })}</summary>
                <WeekReport report={week.report} />
              </details>
            </li>
          );
        })}
      </ol>
      <Button variant="primary" onClick={onNext}>
        {t('batch.next')}
      </Button>
    </div>
  );
}
```

In `source/frontend/src/features/career/Stage.tsx`:
- extend `Revealed` with `| { readonly kind: 'batch'; readonly outcome: BatchOutcome }` (import `BatchOutcome`);
- in `StageView`, before the `switch`, add:

```tsx
  if (revealed?.kind === 'batch') {
    return <BatchResult outcome={revealed.outcome} onNext={onClose} />;
  }
```

- change the plan case to:

```tsx
    case NextStepKind.planWeek: {
      return (
        <PlanWeek career={career} secret={secret}>
          <BatchLauncher
            career={career}
            secret={secret}
            onDone={(outcome) => {
              onReveal({ kind: 'batch', outcome }, t(`batch.stop.${outcome.stopReason}`));
            }}
          />
        </PlanWeek>
      );
    }
```

(import `BatchLauncher` and `BatchResult` from `./batch/...`).

- [ ] **Step 3: Run and commit**

Run: `npx vitest run` → PASS; frontend checks → PASS with ≥ 80% lines.

```bash
git add source/frontend
git commit -m "feat(frontend): run a 13-week batch and show every week with the strategy's reasons" -m "Refs #13." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 16: The local stack with one command

**Files:**
- Create: `source/backend/WebApp/Dockerfile`, `.dockerignore`, `compose.yaml`, `.env.example`

**Interfaces:**
- Produces: `docker compose up` starts `sql` (healthy on `SELECT 1`), `api` (port 8080, Development, migrations at startup) and `web` (port 5173, healthy when `/v1/career-options` answers through the Vite proxy); `docker compose up -d --build --wait` returns once all are healthy.

This task adds configuration, not behaviour; its test is the running stack (Step 3) and the Playwright journey of Task 17.

- [ ] **Step 1: Write the files**

`source/backend/WebApp/Dockerfile`:

```dockerfile
# Multi-stage build of the API (TD-99). The build context is the repository root, so content/m1 ships with the app.
# M6 reuses this file for the production image.
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /repo
COPY .editorconfig ./
COPY source/backend/global.json source/backend/Directory.Build.props source/backend/Directory.Packages.props source/backend/.editorconfig source/backend/
COPY source/backend/Domain/Domain.csproj source/backend/Domain/
COPY source/backend/Application/Application.csproj source/backend/Application/
COPY source/backend/Infrastructure/Infrastructure.csproj source/backend/Infrastructure/
COPY source/backend/WebApp/WebApp.csproj source/backend/WebApp/
WORKDIR /repo/source/backend
RUN dotnet restore WebApp/WebApp.csproj
COPY source/backend/ /repo/source/backend/
COPY content/ /repo/content/
RUN dotnet publish WebApp/WebApp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PolskiStreamerSimulator.WebApp.dll"]
```

`.dockerignore`:

```text
.git/
.claude/
.superpowers/
docs/
node_modules/
**/bin/
**/obj/
**/TestResults/
**/node_modules/
source/frontend/
**/.env
```

`compose.yaml`:

```yaml
# Local development only (TD-50, TD-99). Copy .env.example to .env, set the password, then `docker compose up`
# and open http://localhost:5173. Never point this file at staging or production secrets.
name: polski-streamer-simulator

services:
  sql:
    image: mcr.microsoft.com/mssql/server:2025-RTM-ubuntu-22.04
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD in .env (copy .env.example)}
    volumes:
      - sql-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -Q 'SELECT 1' -b -o /dev/null"]
      interval: 5s
      timeout: 5s
      retries: 30
      start_period: 20s

  api:
    build:
      context: .
      dockerfile: source/backend/WebApp/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Game: Server=sql,1433;Database=PolskiStreamerSimulator;User Id=sa;Password=${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD in .env (copy .env.example)};TrustServerCertificate=True;Encrypt=True
    depends_on:
      sql:
        condition: service_healthy
    ports:
      - "8080:8080"

  web:
    image: node:24.22.0-alpine
    working_dir: /app
    command: sh -c "npm ci --no-audit --no-fund && npm run dev -- --host 0.0.0.0"
    environment:
      API_PROXY_TARGET: http://api:8080
      # Docker Desktop on Windows does not forward file events into the container.
      VITE_POLLING: "true"
    volumes:
      - ./source/frontend:/app
      - web-node-modules:/app/node_modules
    depends_on:
      api:
        condition: service_started
    ports:
      - "5173:5173"
    healthcheck:
      # Through the Vite proxy: healthy only when Vite and the API both answer.
      test: ["CMD-SHELL", "wget -q -O /dev/null http://127.0.0.1:5173/v1/career-options"]
      interval: 5s
      timeout: 5s
      retries: 60
      start_period: 180s

volumes:
  sql-data:
  web-node-modules:
```

`.env.example`:

```text
# Copy to .env (git ignores it) and set a local-only password. SQL Server needs at least 8 characters from three of:
# upper case, lower case, digits, symbols. Do not use ';' (it ends the connection string). Never reuse a real secret.
MSSQL_SA_PASSWORD=
```

- [ ] **Step 2: Check the configuration**

From the repository root: `docker compose config --quiet` without `.env` → fails with `Set MSSQL_SA_PASSWORD in .env (copy .env.example)`. `cp .env.example .env`, set `MSSQL_SA_PASSWORD=Local-Dev-Only-1`, then `docker compose config --quiet` → exit 0. `git status --short` must not list `.env`.

- [ ] **Step 3: Start the stack and play**

Run: `docker compose up -d --build --wait --wait-timeout 600` → `sql`, `api` and `web` running, `sql` and `web` healthy. `curl -fsS http://localhost:5173/v1/career-options` → JSON with four `backgrounds`. Open `http://localhost:5173`, pass the gate, create a career, play one week and run one batch; edit `src/locales/pl/translation.json` (any string) and see the page update without a restart; revert the edit. `docker compose restart api`, reload the page → the same career and week. `docker compose down` (add `-v` to reset the database).

- [ ] **Step 4: Commit**

```bash
git add .dockerignore .env.example compose.yaml source/backend/WebApp/Dockerfile
git commit -m "build: start SQL Server, the API and Vite with one docker compose up" -m "Refs #14." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 17: The browser journey and the `frontend-e2e` job

**Files:**
- Create: `source/frontend/playwright.config.ts`, `source/frontend/e2e/journey.spec.ts`
- Modify: `.github/workflows/ci.yml`, `.github/rulesets/main.json`, `.github/rulesets/develop.json`

**Interfaces:**
- Consumes: the Compose stack (Task 16); the Polish copy (Task 6); every screen of PRs 2–4.
- Produces: `npm run e2e` (Playwright, Chromium, base URL `E2E_BASE_URL` or `http://localhost:5173`); required check `frontend-e2e`.

- [ ] **Step 1: Write the journey**

`source/frontend/playwright.config.ts`:

```ts
import { defineConfig, devices } from '@playwright/test';

// One journey against the Compose stack (TD-100); no retries, so a flaky step is fixed rather than hidden.
export default defineConfig({
  testDir: 'e2e',
  timeout: 180_000,
  expect: { timeout: 15_000 },
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
```

`source/frontend/e2e/journey.spec.ts`:

```ts
import AxeBuilder from '@axe-core/playwright';
import { expect, type Page, test } from '@playwright/test';

import pl from '../src/locales/pl/translation.json';

const themes = ['overlay', 'win98', 'web2005'] as const;

const fill = (text: string, values: Record<string, string | number>): string =>
  text.replaceAll(/\{\{(\w+)\}\}/gu, (_, name: string) => String(values[name]));

/** WCAG 2.2 AA in every theme (spec §5): any axe violation on a key screen fails the journey. */
async function expectAccessible(page: Page, screen: string): Promise<void> {
  for (const theme of themes) {
    await page.evaluate((name) => {
      document.documentElement.dataset.theme = name;
    }, theme);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']).analyze();
    expect(results.violations, `${screen} (${theme})`).toEqual([]);
  }

  await page.evaluate(() => {
    document.documentElement.dataset.theme = 'overlay';
  });
}

test('a new career plays a manual week and a 13-week batch, then reloads to the same state', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('alertdialog', { name: pl.gate.title })).toBeVisible();
  await expectAccessible(page, 'gate');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();

  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill('Tester E2E');
  await expectAccessible(page, 'character creation');
  await page.getByRole('button', { name: pl.create.submit }).click();

  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
  await expectAccessible(page, 'week plan');
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
  await page.setViewportSize({ width: 1280, height: 800 });

  await page.getByRole('radio', { name: pl.action.rest }).check();
  await page.getByRole('button', { name: pl.plan.start }).click();

  const choose = page.getByRole('button', { name: pl.event.choose });
  const settle = page.getByRole('button', { name: pl.settle.action });
  let firstEvent = true;
  for (;;) {
    await expect(choose.or(settle)).toBeVisible();
    if (await settle.isVisible()) {
      break;
    }

    await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
    if (firstEvent) {
      await expectAccessible(page, 'live event');
      firstEvent = false;
    }

    await choose.click();
    await page.getByRole('button', { name: pl.roll.next }).click();
  }

  await settle.click();
  await expect(page.getByRole('heading', { name: fill(pl.report.title, { week: 1 }) })).toBeVisible();
  await expectAccessible(page, 'weekly report');
  await page.getByRole('button', { name: pl.report.next }).click();

  await page.getByRole('button', { name: pl.batch.launch }).click();
  await page.getByRole('button', { name: pl.batch.confirm }).click();
  await expect(page.getByRole('heading', { name: pl.batch.title })).toBeVisible({ timeout: 60_000 });
  await expect(page.getByText(pl.batch.stop.completed)).toBeVisible();
  await expectAccessible(page, 'batch result');
  await page.getByRole('button', { name: pl.batch.next }).click();

  const desk = page.getByRole('region', { name: pl.career.summaryLabel });
  await expect(desk).toContainText(fill(pl.career.week, { week: 15 }));
  const before = await desk.innerText();
  await page.reload();
  await expect(desk).toHaveText(before);
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
});
```

- [ ] **Step 2: Run it against the stack**

With the stack from Task 16 running: `npx playwright install chromium`, then `npm run e2e` → 1 passed. If axe reports a contrast violation in a theme, fix the theme tokens in `src/styles/index.css` (never suppress the rule); if a step fails, fix the code, not the journey.

- [ ] **Step 3: Add the CI job and require it**

In `.github/workflows/ci.yml`, after the `frontend` job:

```yaml
  frontend-e2e:
    name: frontend-e2e
    runs-on: ubuntu-latest
    timeout-minutes: 30
    env:
      # A throwaway password for this run's database only (TD-99); it is not a secret.
      MSSQL_SA_PASSWORD: Ci-${{ github.run_id }}-Sql!
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-node@949feb2413d6458794dcd2491c4babbbce0c15c1 # v7.1.0
        with:
          node-version-file: source/frontend/.nvmrc
          cache: npm
          cache-dependency-path: source/frontend/package-lock.json
      - name: Install
        working-directory: source/frontend
        run: npm ci --ignore-scripts
      - name: Install Chromium
        working-directory: source/frontend
        run: npx playwright install --with-deps chromium
      - name: Start the local stack
        run: docker compose up -d --build --wait --wait-timeout 600
      - name: Browser journey
        working-directory: source/frontend
        run: npm run e2e
      - name: Collect stack logs
        if: failure()
        run: docker compose logs --no-color > "$RUNNER_TEMP/compose.log"
      - uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
        if: failure()
        with:
          name: frontend-e2e-report
          path: |
            source/frontend/playwright-report
            source/frontend/test-results
            ${{ runner.temp }}/compose.log
          if-no-files-found: warn
      - name: Stop the stack
        if: always()
        run: docker compose down -v
```

Add `{ "context": "frontend-e2e" }` to `required_status_checks` in `.github/rulesets/main.json` and `.github/rulesets/develop.json`. Validate with actionlint and `validate-rulesets.py` as in Task 5.

- [ ] **Step 4: Commit**

```bash
git add source/frontend/playwright.config.ts source/frontend/e2e .github
git commit -m "test: play the whole slice in Chromium with axe in every theme, in CI too" -m "Refs #14." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 18: Documentation and PR 4

**Files:**
- Modify: `docs/development-and-delivery.md`, `docs/technology-stack.md`, `docs/repository-standards.md`, `docs/requirements-traceability.md`, `docs/validation-and-delivery.md`, `README.md`

- [ ] **Step 1: Update the documentation**

- `docs/development-and-delivery.md` *Local Docker Compose*: replace the M2-B "Current state" paragraph with the M2-C run — copy `.env.example` to `.env` and set `MSSQL_SA_PASSWORD`; `docker compose up` (first start downloads images and runs `npm ci`, a few minutes); open `http://localhost:5173`; `docker compose up --build api` after backend changes; `docker compose down -v` resets the database; Docker Desktop needs about 2 GB of memory for SQL Server; `npm run dev` on the host with the stack's API stays possible for heavy UI work; `npm run e2e` runs the journey against the running stack (TD-99). In *Current state* of the CI section, list `frontend-e2e` as a required job (TD-100).
- `docs/technology-stack.md` *Verification*: one Playwright journey in Chromium against the Compose stack, `@axe-core/playwright` on the gate, creation, plan, live event, report and batch result in all three themes (TD-100).
- `docs/repository-standards.md`: rows for `compose.yaml` (development only), `.env.example` (documents variables, never real values; `.env` is ignored), `.dockerignore`, `source/backend/WebApp/Dockerfile` (multi-stage, context = repository root), `source/frontend/playwright.config.ts` and `source/frontend/e2e/`.
- `docs/requirements-traceability.md`: #13 (client side: batch UI, cumulative report, pause message) and #14 (one-command stack, frontend gates, journey) rows; the smoke-test admin login points to M4.
- `docs/validation-and-delivery.md`: the local smoke-test acceptance row names the Playwright journey; the admin login moves to M4.
- `README.md`: a *Run locally* section with the three Compose steps and the link to the development guide.

Check the links.

- [ ] **Step 2: Verify everything and commit**

Backend: unit run with the gate, integration run, OpenAPI regeneration, `dotnet format` → PASS. Frontend: frontend checks → PASS with ≥ 80% lines; `npm run api:generate` → no change. Stack: `docker compose up -d --build --wait` and `npm run e2e` → PASS; `docker compose down -v`.

```bash
git add docs README.md
git commit -m "docs: record the M2-C local stack and browser journey" -m "Refs #13, #14." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: [external, owner yes] Review, push, open PR 4, update the issues, require the check, merge**

1. One fresh reviewer over `develop..feature/m2c-13-14-batch-stack`; fix Important findings test-first.
2. With the owner's yes: push; `gh pr create --base develop --title "M2-C batch UI, local stack and browser journey (#13, #14)" --body "<summary, test plan, Refs #13, #14, 🤖 line>"`.
3. With the owner's yes, update the issue texts: #14's *Delivery split* marks the M2-C part delivered and moves the test admin login to M4; #13's *Delivery split* marks the client side delivered.
4. Wait for `backend`, `backend-integration`, `frontend`, `frontend-e2e`, `sonar`, `pr-policy` and 0 SonarQube Cloud issues on the PR. With the owner's yes, apply both ruleset files and confirm `frontend-e2e` is required.
5. With the owner's yes merge; sync `develop`; watch its run; confirm 0 SonarQube Cloud issues on `develop`.

---

# Milestone release (TD-96)

After PR 4, every M1 and M2 issue is delivered on `develop`. Each step needs the owner's yes.

1. List the issues to close: `gh issue list --milestone "M1 — Simulation prototype" --state open` and the same for `M2 — Vertical slice`, plus the bug #58. Confirm each is delivered on `develop` (its PR merged).
2. `git checkout develop && git pull --ff-only && git checkout -b release/m2`; push; open a PR into `main` titled `Release M2: simulation prototype and vertical slice` whose body has one `Closes #n` line per issue from step 1.
3. The `sonar` check cannot pass on a PR into `main` until M6 (TD-89); with the owner's yes, merge through the admin PR-only bypass once every other required check is green.
4. After the merge, check that the listed issues closed; close any that did not. Close the M1 and M2 milestones (`gh api --method PATCH repos/adamcharusta/PolskiStreamerSimulator/milestones/<n> -f state=closed`).
5. Reconcile: `gh pr create --base develop --head main --title "Reconcile main into develop after the M2 release"`; merge with the owner's yes once its checks pass. No version tag yet: tags mark successful publications, which start with M6.

---

## Final review

Before each PR's merge, one fresh reviewer (most capable model) reviews that branch against the spec and this plan. Important findings are fixed on the branch before the merge, each with a failing test first; every ruling is reported to the owner. SonarQube Cloud must show zero open issues on the PR and, after the merge, on `develop` (TD-96).
