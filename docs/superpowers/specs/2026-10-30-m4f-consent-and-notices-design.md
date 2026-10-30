# M4-F Consent and Notices — Design

Date: 2026-10-30
Status: **Agent proposal; one PR (2026-10-30).** The home screen's `/v1/seasons` read is a notice: it fails silently. The owner asked the agent to deliver every M4 sub-project in order without a design interview ("tak zrób je po kolei wszystkie", 2026-10-29; "zmergowane" after M4-E merged). Every rule, field and wording below is an agent proposal for owner review.
Milestone: [M4 — Online play and administration](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4), sub-project F, delivering [#31](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/31) (TD-126). The M4 decomposition is in the [M4-A design §0](2026-10-29-m4a-career-access-and-retention-design.md).

## 1. Intent

Analytics are off unless the player turns them on, and the player can turn them off again at any time (TD-15). Even when they are on, nothing identifies a player, a career or a save. The player also learns two things the game cannot hide:

- Progress lives on the server: during an outage the game pauses and nothing can be played offline (TD-28).
- After a severe database failure, up to about a day of progress can be lost (the recovery-point target of the architecture §4).

Success means (#31, REQ-25):

- No analytics request leaves the browser before the player opts in, after the player opts out, or while nothing was decided.
- Opting out deletes the events still waiting in the browser.
- Events carry only an allowlisted name and allowlisted enum or bucketed values. The server stores only daily counts, with no address, career, nickname, secret or time of day.
- While the API is unreachable, a banner says that the game is paused. Every gameplay button keeps waiting for the server, so nothing settles offline.
- The online notice states the day-of-progress risk. The home screen announces closed seasons whose deadline was extended.

## 2. Analytics

### Consent

- **Stored where.** `pss.analytics.v1` in `localStorage`, with value `granted` or `denied`. Absent means undecided, and undecided counts as **off**.
- **Asked when.** After the 18+ gate, a non-blocking panel asks „Pomóż ulepszyć grę?” with two equal buttons: „Zgadzam się” and „Nie, dziękuję”. It explains what is collected, in one short paragraph.
- **Changed where.** Settings gain an „Analityka” checkbox that changes the choice at any time.
- **Revocation.** Clearing the checkbox or choosing „Nie” deletes the buffer at once.
- **Storage blocked.** If storage throws, the choice applies only to the current page, and the buffer lives in memory only.

### Client buffer

`analytics.ts`:

- **`track(name, props)`**
  - Does nothing unless consent is `granted`.
  - Accepts only the events in the table below. Each prop must be one of that event's allowed values; an unknown prop or value drops the whole event.
- **Buffer.**
  - Kept in `pss.analytics.buffer.v1`, at most 100 events.
  - When full, the oldest event is dropped.
- **Flushing.**
  - Every 30 seconds, and on `pagehide`, the buffer is sent as `POST /v1/telemetry`, at most 50 events per request.
  - Sending uses `fetch` with `keepalive` and `credentials: 'omit'`, and carries no cookies or headers beyond `Content-Type`.
  - The sent events are removed only after a `2xx` answer. A failure keeps them for the next flush.
  - Before each send the client checks consent again.

### Events

| Event | Props |
| --- | --- |
| `career_created` | `background`, `difficulty`, `ranked` (`true`/`false`) |
| `week_settled` | `stage` (`1-4`, `5-13`, `14-52`, `53-260`, `261+`) |
| `batch_run` | `weeks` (`4`, `13`, `52`) |
| `career_ended` | `ending`, `ranked` |
| `result_published` | — |
| `save_restored` | — |
| `save_imported` | — |

### Server

`POST /v1/telemetry`:

- **Request.**
  - Body `{ events: [{ name, props }] }`, up to 50 events, a 16 KiB limit, and its own rate limit of 30 per minute per address.
  - Unauthenticated, and no cookies are read.
- **Validation.** Each event is checked against the same allowlist. Invalid events are skipped; the batch is not rejected.
- **Answer.** `202` with `{ accepted }`.
- **Storage.** `game.telemetry_daily` holds `(day, event, props_key, count)`:
  - `props_key` is the props sorted as `k=v` pairs and joined with `;`;
  - each valid event adds 1 to its row for the UTC day;
  - nothing else is stored: no address, user agent or exact time.
- **Logging.** The endpoint logs nothing beyond the framework's request line.

The owner reads the counts in SQL for now. An analytics view in `/admin` is not part of M4.

## 3. Notices

- **Online notice.** The text of the online notice after the gate becomes:

  > „Gra wymaga połączenia z internetem: kariera zapisuje się na serwerze po każdym kroku i bez połączenia nie da się grać. Po poważnej awarii bazy danych możesz stracić do około jednego dnia postępu.”

  The home screen keeps the same sentence about the risk in its access notice.
- **Outage banner.** `ConnectionBanner` in the layout:
  - **Detection.** It watches `navigator.onLine` and the outcome of API requests, through the query cache and the mutation cache: a network error or a 5xx marks the server unreachable, and any successful response clears it. While unreachable, it checks `GET /health` every 15 seconds.
  - **Message.** While unreachable it shows a polite live region (`<output>`, so the screen's own failure alert keeps focus) with „Brak połączenia z serwerem. Gra jest wstrzymana — widzisz ostatni potwierdzony stan. Wrócimy, gdy serwer odpowie.”
  - **No offline play.** No step is queued offline. The existing retry banner and the pending buttons stay as they are.
- **Extended deadlines.** The home screen lists every closed season with `extended: true` whose deadline has not passed: „Termin publikacji wyników z sezonu N przedłużono do <data>.” (data from `GET /v1/seasons`).

## 4. Rulings

1. Consent is undecided (off), granted or denied, stored per browser, asked after the gate and changeable in settings. Revocation deletes the buffer.
2. A fixed allowlist of seven events with enum or bucketed props. The client and the server both enforce it.
3. A first-party endpoint stores only daily counts. No third-party analytics script or cookie is used.
4. The online notice states the day-of-progress risk. The outage banner pauses play visibly. Extended deadlines are announced on the home screen.

## 5. Testing

- **Unit (server):**
  - The allowlist accepts the listed events and skips unknown names, props and values.
  - Daily counting groups by day and props.
  - The batch limit is enforced.
- **API:**
  - `202` with counts in SQL.
  - An oversized body gets `413`.
  - The rate limit applies.
  - No cookie is read or set.
- **Migration:** the table exists and is empty.
- **Frontend:**
  - No request before consent or after denial.
  - Events are buffered and flushed after consent.
  - Revocation clears the buffer.
  - Invalid props are dropped.
  - The settings toggle works.
  - The consent panel appears once.
  - The outage banner appears on a network error and leaves after a success.
  - Extended deadlines show on the home screen.
- **Playwright:** the journeys run with consent undecided. A new check fails if any `/v1/telemetry` request happens before consent, then opts in in the settings and sees one telemetry request after creating a career.
