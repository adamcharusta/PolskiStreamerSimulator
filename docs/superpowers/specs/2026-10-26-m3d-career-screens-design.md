# M3-D Career Screens — Design

Date: 2026-10-26
Status: **Agent proposal.** The owner asked the agent to continue with sub-project D ("tak", 2026-10-26, answering "merge #88 and #89 and start M3-D?") without a design interview, as for B and C. Every screen, field and wording below is an agent proposal for owner review.
Milestone: [M3 — Complete gameplay systems](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/3), sub-project D, delivering [#24](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/24), [#76](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/76) and [#82](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/82) ([TD-109](../../technical-decisions.md)).
Builds on: [M3-C automation and endings](2026-10-26-m3c-automation-and-endings-design.md) (ending summary, score view, achievements).

## 1. Intent

A whole career can be followed in the browser: a dashboard of where the career stands, a searchable week-by-week history with yearly reviews, reports and rolls that explain themselves in the authored Polish text, and an ending that can be kept as a PDF diploma.

Success means (#24, #76, #82):

- The career screen shows the dashboard of content §3.4 (week, age, platform, followers, viewers, cash, drama, credibility, energy, burnout, skills, obligations, next milestone).
- A history screen lists every settled week, can be filtered by week range, kind of incident and text, opens any week's full report, and summarises each 52-week year.
- Every roll (live event, decision, networking, sponsor pitch, ban move) in the roll screen, the week report and the batch ledger shows the event title, the choice label and the authored outcome text for the career's pronouns (#82); the ledger shows each roll against its threshold (#76).
- The plan screen shows a hint per category (#82); a short-stream or recovery week has its own report line (#82).
- An ended career can download a PDF diploma generated in the browser with `pdf-lib` from the server's final career view (TD-44).

## 2. Scope

**In scope**

- `Domain`: the report line `week:short-stream` for a short stream (no state change, so no golden hash moves).
- `Application`/`WebApp`: `RollView` gains `EventTitle`, `ChoiceLabel` and `OutcomeText` (nullable, from the career's pinned snapshot); `GET /v1/careers/{id}/history` (week summaries) and `GET /v1/careers/{id}/history/{week}` (one week's report); the store lists stored reports; OpenAPI regenerated.
- Frontend: dashboard card, history route `/kariera/$careerId/historia` with filters and yearly review, roll and report text, ledger rolls, punctuation moved into translations (#76), the most relevant blocked reason (#76), category hints, the short-stream line, the PDF diploma.

**Out of scope**

- Faux chat (content §3.6) and Streamer Friendly masking (content §2): M5 content and polish.
- Ranked nickname entry and leaderboard result on the ending (#27, M4).

## 3. Roll text (#82)

`RollView(…, string? EventTitle = null, string? ChoiceLabel = null, string? OutcomeText = null)`: optional members with null defaults, so stored responses written before D still load (TD-91) and the server fills them for new responses only.

- Live events and decisions: `Title` (M3 ICU) or null for an M1 event (the client keeps its translation key), the choice's `LabelPl`, and `SuccessText` for success, critical success and guaranteed outcomes, `FailureText` (falling back to `SuccessText`) for failures.
- Networking and the sponsor pitch: no event title (the client names the action), the balance choice's label and outcome texts.
- The ruleset-built `platform-ban` decision: `platformBan.title` and the move label; no outcome text.
- A batch ledger choice carries the same roll view.

## 4. History

- `GET /v1/careers/{id}/history` → `HistoryView(IReadOnlyList<WeekSummaryView> Weeks)` in week order, one per settled week (manual or automatic): `Week`, `Plan`, `LiveViewers`, `FollowersNet`, `CashDelta`, `Automatic`, and incident tags (`viral`, `strike`, `ban`, `migration`, `wager`, `sponsorPaid`, `sponsorPenalty`, `decision`, `shortStream`, `ending`).
- `GET /v1/careers/{id}/history/{week}` → the stored `WeekReportView` of that week, `404 week-not-found` when it was never settled.
- Both use the bearer secret like `GET /v1/careers/{id}` and read the command log (`report_json`), so the history is exactly what the player was shown. A career has at most 1,040 reports; the summary list is computed per request (no new table).
- The yearly review is computed in the browser from the summaries: per year (52 weeks) the followers and cash change, streams per category, incidents and the best week.

## 5. Screens

- **Dashboard** (top of the career screen, every step): week of 1,040, age (18 + whole years played), platform, followers, last viewers, cash, drama, credibility, energy, burnout, the deals due this week and the next follower milestone (the next of 1k / 10k / 100k / 1m / 10m / 100m); links to the history.
- **History** (`/kariera/$careerId/historia`): yearly review cards, filters (week from–to, incident kind, text that matches the action, category or incident names), a list of week rows; a row opens the full week report fetched from the server.
- **Reports and rolls**: the roll result and every report roll line show the title, the label and the formatted outcome text; decision rolls name the decision; the batch ledger shows each choice with its roll against the threshold and the outcome text (#76, #82); the `week:short-stream` line reads "Krótszy stream po kryzysie".
- **Category hints** on the plan screen, one short Polish line per category in the translations, drawn from the category profiles of gameplay §11.
- **Blocked action reason** (#76): when every option of an action is blocked, the reason shown is the most relevant one: `rest-only`, then energy, cash and the other reasons, in a fixed priority list.
- **Diploma**: on the ending view, "Pobierz dyplom (PDF)" builds an A4 landscape PDF in the browser with `pdf-lib` and `@pdf-lib/fontkit` and an embedded font with Polish letters: the nickname, background, difficulty, weeks played, ending, final score with F/W/A, followers and cash at the end, earned achievements, and the date. Read only from the ended career view.

## 6. Rulings

1. Roll texts are optional contract members with null defaults instead of required members with `StoredContract` defaults, because they appear in five nested places (outcome roll, report rolls, decision rolls, action roll, ledger choices). Cost: the generated client types them as optional.
2. History is read from the command log on each request; no projection table. Cost: about 3 MB read for a full career's summary; acceptable until M4 load tests.
3. The yearly review is client-side; it needs only the summaries. Cost: no server test of the aggregation (frontend tests cover it).

## 7. Delivery

Two PRs: **1 Contract and history** (`feature/m3d-1-history`: Domain line, roll texts, history endpoints, store, OpenAPI) and **2 Screens** (`feature/m3d-2-screens`: dashboard, history route, texts, #76, hints, diploma, Playwright, docs). Issues #24, #76 and #82 close with the M3 release into `main`.
