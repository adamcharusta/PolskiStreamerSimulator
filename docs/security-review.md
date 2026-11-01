# Security, Privacy and 18+ Review

Status: **agent review, 2026-11-01** (M5-D, [#38](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/38), TD-130). This is an engineering threat model of the code on `develop` plus the M5 PRs, written by the agent. It is **not a legal opinion and claims no compliance**: §5 lists the questions a qualified reviewer must answer before the public launch (M6). The owner approves or changes the findings; #38 stays open until the legal questions are answered and recorded.

## 1. Scope and method

In scope: the public career API (`/v1/careers…`, `/v1/leaderboard`, `/v1/seasons`, `/v1/telemetry`), the owner API (`/v1/admin…`), the React app and the content pipeline. Out of scope until M6 deployment exists: the k3s ingress, TLS, Vault, SQL Server hosting and backups ([infrastructure](infrastructure-and-operations.md)); §4 lists what M6 must configure. Method: read the access, storage and request-handling code, list assets and trust boundaries, check each threat against a control with its evidence, and add a test for every control changed here.

## 2. Assets and trust boundaries

| Asset | Where | Who may use it |
| --- | --- | --- |
| Career secret (bearer credential, one career) | Browser storage; SHA-256 hash in `game.career_secrets` | The browser that created or recovered the career |
| Recovery key `pss1.<id>.<32 random bytes>` | Shown once, kept by the player; SHA-256 hash stored | Whoever holds it (recovery rotates the secret) |
| Owner account (password, TOTP key, 10 recovery codes) | ASP.NET Core Identity in schema `owner` | The owner only |
| Leaderboard integrity | `game.leaderboard_entries`, server-computed scores | Server only; players publish a nickname |
| Content and balance | Repository files and `game.content_publications` | Owner via `/admin`; deployments |
| Player data | Nickname, career state and log; analytics daily counts | Server; no account, e-mail or IP is stored |

Boundaries: browser ↔ public API (anonymous, bearer secret per career); browser ↔ owner API (cookie session after password + second factor); API ↔ SQL Server; repository ↔ deployed content.

## 3. Threats and controls

| Threat | Control | Evidence |
| --- | --- | --- |
| Guessing a career secret or recovery key | 256-bit random values, stored as SHA-256 and compared in constant time; recovery failures are one `404 recovery-failed`; 10 recoveries per 10 minutes per address | `Infrastructure/Security/Sha256SecretHasher.cs`, `WebApp/CareerRateLimits.cs`, `AccessApiTests` |
| A stolen secret used forever | Recovery issues a new secret and invalidates the old one; the key can be rotated | TD-121, `AccessApiTests.Recovery_returns_a_new_secret_and_the_old_one_stops_working` |
| Forged score or ranking | The server simulates every week from its own seeded state; clients send only choices; ranked careers have no saves or imports; publication is idempotent and the first nickname is final | TD-122, TD-123, `Domain` golden tests |
| Tampered save import | Imports only create new unranked careers; the file is validated, size-limited (64 KiB) and replayed by the server | TD-123, `SaveApiTests` |
| Owner account takeover | Password + TOTP or single-use recovery code; lockout after 5 failures for 15 minutes; 10 sign-ins per 10 minutes per address; `HttpOnly`, `SameSite=Strict`, `Secure` cookies with a fixed session length; no registration endpoint | `WebApp/Admin/OwnerAuthentication.cs`, TD-124 |
| Cross-site request forgery on `/admin` | Anti-forgery token on every mutating cookie request, plus `SameSite=Strict` | `UseOwnerAntiforgery`, `AdminApiTests` |
| Cross-site scripting | React renders nicknames, content and imports as text (no `dangerouslySetInnerHTML`); event text is ICU with tags ignored; **new:** a content security policy forbids inline script and eval in production | `src/content/icu.ts`, `securityHeaders.ts`, §3.1 |
| Clickjacking | **New:** `frame-ancestors 'none'` and `X-Frame-Options: DENY` | `WebApp/SecurityHeaders.cs`, `securityHeaders.ts` |
| SQL injection | EF Core with parameters only | `Infrastructure/Persistence` |
| Oversized bodies (memory and CPU exhaustion) | Saves 64 KiB, telemetry 16 KiB; **new:** every other endpoint 256 KiB instead of Kestrel's 30 MB | `WebApp/RequestSizeLimits.cs`, `SecurityHeadersTests` |
| Creation flooding | 20 creations per 10 minutes per address; repeat-safe creation IDs | TD-121 |
| One shared rate limit behind the ingress, or a spoofed address | **New:** forwarded headers are trusted only from the configured proxy networks (`ForwardedHeaders:KnownNetworks`); without them they are ignored | `WebApp/TrustedProxies.cs`, `AccessApiTests` |
| Cached private responses | **New:** `Cache-Control: no-store` on every response that does not set its own (only the public leaderboard is cacheable) | `SecurityHeadersTests` |
| Content that breaks careers | Validation, simulation and immutable published versions; running careers stay pinned | TD-112, TD-125 |
| Analytics leaking players | Off until opt-in; seven allowlisted events; only daily totals, no IDs | TD-126 |

### 3.1 Content security policy

`source/frontend/securityHeaders.ts` defines the page policy: `default-src 'self'`; `script-src 'self'`; `style-src 'self' 'unsafe-inline'` (Radix positions dialogs with style attributes); `img-src 'self' data: blob:`; `font-src 'self'`; `connect-src 'self'`; `object-src 'none'`; `base-uri 'self'`; `form-action 'self'`; `frame-ancestors 'none'`, plus `nosniff`, `no-referrer` and a `Permissions-Policy` that turns off camera, microphone and geolocation. The development server sends the same policy with inline scripts allowed for Vite's preamble, and every browser journey fails on a `securitypolicyviolation`. `vite preview` sends the production policy, and the `Production build journey` CI step plays the first week on the built app in Chromium, Firefox and WebKit. To meet it, the theme boot script moved from `index.html` to `public/theme-boot.js`, and Zod runs `jitless`, because its eval probe would otherwise be reported. The API answers with `default-src 'none'; frame-ancestors 'none'`.

## 4. Must be done at deployment (M6)

- Serve the built app with the headers of `securityHeaders('production')` (the same module) and HSTS at the TLS-terminating ingress.
- Set `ForwardedHeaders:KnownNetworks` to the ingress pod network, or every player shares one rate-limit bucket.
- Keep the owner account, SQL and data-protection keys in Vault; never in the image or repository.
- Encrypt backups before they leave `ovh` (already planned in [infrastructure](infrastructure-and-operations.md)).
- Run a dependency and container image scan in the release workflow.

## 5. Questions for a qualified reviewer (not answered here)

1. **Age presentation.** Is a self-declared 18+ gate enough in Poland and the EU for a free game with satirical adult themes, or is stronger age assurance expected?
2. **Simulated gambling.** Does a gambling category with fictional currency only, no purchase, payout or real-money link, fall outside Polish gambling law and EU consumer rules, and is the current warning wording adequate?
3. **Adult-business theme.** Is the non-explicit side-business storyline acceptable under platform and regional rules for an 18+ game, and is the Sensitive content filter a sufficient option?
4. **Privacy notice.** Which GDPR notices does the game need for career state, nicknames, the in-memory per-address rate limits and the opt-in daily analytics; who is the controller; and is browser storage for settings, consent and career secrets strictly necessary under the ePrivacy rules?
5. **Retention.** Are six months for unfinished careers, twelve months after an ending and indefinite public leaderboard entries proportionate, and what must the notice say?
6. **Nicknames and moderation.** What is the duty to remove an offensive or personal-data nickname, and in what time?
7. **Satire and real persons.** Does the content review (fictional names only) need a written policy for complaints about resemblance to real creators?

## 6. Residual risks accepted for now

- A career secret in browser storage can be read by malware or anyone using the same browser profile; the recovery key rotates it.
- Rate limits are per process; with several API instances an attacker gets the limit once per instance.
- `style-src 'unsafe-inline'` remains for Radix; scripts stay strict.
