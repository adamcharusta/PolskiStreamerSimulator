# M4-E Content Administration — Design

Date: 2026-10-29
Status: **Agent proposal; API in PR 1 ([#104](https://github.com/adamcharusta/PolskiStreamerSimulator/pull/104)), `/admin` screens in PR 2 (2026-10-30).** The owner asked the agent to deliver every M4 sub-project in order without a design interview ("tak zrób je po kolei wszystkie", 2026-10-29; "zmergowane" after M4-D merged). Every rule, field and wording below is an agent proposal for owner review.
Milestone: [M4 — Online play and administration](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/4), sub-project E, delivering [#29](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/29) (TD-125). The M4 decomposition is in the [M4-A design §0](2026-10-29-m4a-career-access-and-retention-design.md).

## 1. Intent

The owner can change the events and balance constants of new careers without a deployment. The workflow:

1. Edit a draft in `/admin`.
2. Check the draft: the server validates it, shows what changed and replays a deterministic batch of careers on it.
3. Publish it as an immutable snapshot.

Careers already running keep the snapshot they started with. A change to the mechanics opens a new leaderboard season. A text-only correction does not open a new season. The owner can undo a publication by publishing an earlier snapshot again as a new publication; no snapshot ever changes after publication (TD-70, TD-112).

Success means (#29, REQ-20):

- Existing careers keep their pinned ruleset and content digest after any publication.
- Any change to mechanics (availability, eligibility, odds, effects or numeric balance) starts a new season. A change to text alone keeps the season.
- Each publication shows its diff and digest and has an audit record. A reversal creates a new publication.
- The server refuses an invalid draft, including a changed event that keeps its published version, and refuses a draft whose simulated careers break an invariant.

## 2. Scope

**In scope**

- **`Application`**:
  - the draft, its check (validation, diff, mechanical or text-only), simulation, publication, reversal, history and export;
  - the mechanics fingerprint;
  - the content new careers pin, now taken from the newest publication.
- **`Infrastructure`**:
  - the `game.content_drafts` and `game.content_publications` tables and `seasons.mechanics_hash`, in one migration;
  - the stores;
  - publishing the repository content as a release at startup;
  - a refresher that picks up publications made on another API instance.
- **`WebApp`**: the admin content endpoints, and the anti-forgery check extended to `PUT` and `DELETE`.
- **Frontend**: a „Treść” section in `/admin` with:
  - the draft status;
  - an event list with a JSON editor, a preview in the three pronoun forms, adding and removing events;
  - the numeric balance constants;
  - the check result and the simulation;
  - publication with a reason;
  - the history with reversal and export.

**Out of scope**

- **A form for every field of an event.** Events are edited as their JSON object, which the server validates against the same schema as the repository files. A new kind of effect or rule still needs a C# change and a release (TD-70).
- **Several drafts at once, or several editors.** There is one owner and one draft.
- **Notices on the home screen about a new season.** These belong to M4-F.

## 3. Published content and releases

- **`game.content_publications`** stores every content version that new careers have pinned. Each row has:
  - `number` (identity), `digest` (foreign key to `content_snapshots`), `mechanics_hash` and `kind`:
    - `release`: the repository content shipped by a deployment;
    - `publish`: a draft;
    - `revert`: a copy of an earlier publication;
  - `source_number` (the publication a revert copies), `actor`, `reason` and `published_at`.
- **What new careers pin.** New careers pin the **newest publication**.
- **Startup.** The API stores the repository content and publishes it as a `release` unless an earlier release already has the same digest. So:
  - a deployment that ships new content files supersedes the owner's publications;
  - an unchanged redeployment keeps the owner's newest publication.

  The release is audited with actor `deployment`.
- **Other API instances.** An instance re-reads the newest publication every 30 seconds and after its own publications, so new careers on every instance switch to the new content within half a minute.

## 4. Mechanics fingerprint and seasons

- **The fingerprint (`mechanics_hash`)** is the SHA-256 of both content files in canonical form (properties sorted, compact) after removing what players only read:
  - in events and sponsors: the string values of `title`, `text`, `label`, `successText`, `failureText`, `pitch`, `name` and `description`, and every `version`;
  - in the balance file: the same text properties, the top-level `version`, and the platform choice labels under `platformBan.choices`.
- **Seasons pin the fingerprint, not the digest.** M4-B pinned the digest. Now:
  - `Season.Pins(ruleset, mechanicsHash)` decides whether a season matches;
  - a text-only publication keeps the open season, even though careers created after it pin a new digest;
  - any other change closes the season and opens the next one, as at startup (M4-B §4).
- **Migration.** `seasons.mechanics_hash` is nullable. An open season without one adopts the current fingerprint when its digest equals the current digest.

## 5. The draft

- **Storage.** There is one draft, in `game.content_drafts`. It holds:
  - the two documents as JSON text;
  - the publication it was copied from (`base_number`);
  - a `revision` for optimistic concurrency;
  - the time and the actor of the last change.
- **Creating.** A draft is created from the newest publication, or from an earlier publication given by number. It is deleted when it is discarded or published.
- **Stale requests.** Every edit sends the `revision` it read. A mismatch answers `409 draft-stale`.
- **Edits:**
  - replace or add one event, by its `id`; an added event goes to the end;
  - remove one event from the draft: the event is archived from future publications, while snapshots already published keep it;
  - replace the balance document.

  Every edit, discard and draft creation writes an audit record. Its target is the event ID or `balance`, and its old and new values are the JSON before and after.
- **The check** runs on every read of the draft. It reports:
  - **Problems**: every message of the content parser, the `ContentValidator` violations, an unregistered ruleset, and `event-version-changed:<id>@<version>` for an event that differs from a published event with the same version.
  - **Diff against the newest publication**: events added, changed or removed, each flagged mechanical or text-only, with the event version before and after, and balance values changed, by JSON path.
  - Whether the draft as a whole is **mechanical**, **text-only** or **unchanged**.
- **Saving.** A draft with problems can still be saved, so a large edit can be done in steps. Publishing it is refused.

## 6. Simulation, publication and reversal

- **Simulation** (`POST .../draft/simulation`) replays 40 careers on the draft through `CareerRunner`, with seeds `SplitMix64(42 + i)`, the ruleset's built-in policies and every background and difficulty in turn. It then replays the same 40 on the newest publication. For each it returns:
  - the number of careers;
  - the endings;
  - the 10th, 50th and 90th percentile of final score and of weeks played;
  - invariant violations;
  - the careers that threw an error.
- **Publication** (`POST .../publish`, body `{ revision, reason }`) is refused with:
  - `422 content-invalid` while the check reports a problem;
  - `409 content-unchanged` when the draft equals the newest publication;
  - `422 simulation-failed` when a simulated career breaks an invariant or throws.

  Otherwise:
  1. The snapshot is stored.
  2. The publication row, its audit record (action `content.publish`, with the diff summary and digest as the new value) and the deletion of the draft commit in one transaction.
  3. This instance switches to the new content, and the open season is checked against the new fingerprint.
- **Reversal** (`POST .../publications/{number}/revert`, body `{ reason }`) publishes the digest of that publication again, as a new `revert` publication. It is audited as `content.revert`. The season follows the fingerprint: reverting a mechanical change opens a new season, because results played under the reverted rules stay in their own season. A revert to the content that is already current is refused (`409 content-unchanged`).
- **Export** (`GET .../publications/{number}/export`) returns `{ number, digest, mechanicsHash, balance, events }`, with the two documents exactly as stored, for recovery and for reproducing an audit.

## 7. API

Every endpoint is under `/v1/admin/content` and requires the owner cookie (M4-D). Every `/v1/admin` response is `Cache-Control: no-store`. `POST`, `PUT` and `DELETE` also require the `X-CSRF-TOKEN` header.

| Method and path | Purpose |
| --- | --- |
| `GET /` | The newest publication and the draft with its check, or `draft: null` |
| `POST /draft` | Create the draft. Body `{ fromPublication? }`. `409 draft-exists` |
| `DELETE /draft?revision=` | Discard the draft |
| `PUT /draft/events/{id}` | Replace or add an event. Body `{ revision, event }`; the body's `id` must equal the path |
| `DELETE /draft/events/{id}?revision=` | Remove an event from the draft. `404 event-not-found` |
| `PUT /draft/balance` | Replace the balance document. Body `{ revision, balance }` |
| `POST /draft/simulation` | Simulate the draft and the current content |
| `POST /publish` | Publish the draft. Body `{ revision, reason }` |
| `GET /publications` | The publication history, newest first |
| `POST /publications/{number}/revert` | Publish an earlier snapshot again. Body `{ reason }` |
| `GET /publications/{number}/export` | Both files of a publication |

Other errors are `404 draft-not-found` and `404 publication-not-found`. Without a draft, the edit endpoints answer `404 draft-not-found`.

## 8. Screens (PR 2)

The panel gains „Treść”:

- **Without a draft**, „Utwórz szkic” creates one.
- **The draft status** shows its base publication and revision, a warning when a newer publication exists, and „Odrzuć szkic”.
- **„Wydarzenia”** is a filtered list of events, each with its ID, version, phase, title and diff badge (nowe, zmienione, mechanika or tekst). Selecting an event opens:
  - its JSON in a textarea;
  - a preview of the title, text and choice labels in the female, male and neutral forms;
  - „Zapisz wydarzenie” and „Usuń ze szkicu”.

  „Nowe wydarzenie” starts from a template.
- **„Stałe balansu”** lists every numeric value of the balance file by path, with its current value and a field. „Zapisz stałe” saves them.
- **„Sprawdzenie”** shows the problems, the diff and whether the publication will open a new season.
- **„Symulacja”** compares the draft with the current content.
- **„Publikacja”** takes a reason and is disabled while there are problems.
- **„Historia publikacji”** lists the publications with „Przywróć” (which needs a reason) and „Eksportuj”.

## 9. Rulings

1. One owner draft, edited per event as JSON and per numeric constant, with optimistic revisions. Every edit is audited.
2. New careers pin the newest publication. The repository content is published as a `release` at startup when its digest is new.
3. Seasons pin a fingerprint of the content without its display texts and versions, so a text-only publication keeps the season.
4. Publishing needs a clean check, a real change, a 40-career simulation without violations and a reason. It stores the snapshot, the publication and its audit record, and switches new careers.
5. Reversal republishes an earlier digest as a new publication; snapshots never change.
6. Other instances pick up a publication within 30 seconds.

## 10. Testing

- **Unit:**
  - the fingerprint ignores each text field and version, and changes with any number, ID, flag or effect;
  - the diff classifies changes;
  - the draft edits and their audit records;
  - stale revisions;
  - publish refusals (invalid, unchanged, simulation failure);
  - text-only publication keeps the season, a mechanical one rolls it over;
  - existing careers keep their digest and keep playing;
  - revert creates a new publication and season;
  - export round-trips the digest.
- **Store contract (in-memory and SQL Server):**
  - the publication, its audit record and the draft deletion commit together;
  - the release at startup is idempotent;
  - the open season adopts the fingerprint.
- **API:**
  - every content endpoint answers `401` without the owner and `400` without the anti-forgery token on `PUT` and `DELETE`;
  - an end-to-end text fix and a mechanical change through HTTP, with a ranked career created before and after each.
- **Frontend:** the draft lifecycle, the event editor and preview, constants, check, simulation, publication and revert.
- **Playwright:** the owner fixes an event title, sees „tekst” and no new season, publishes, and the history shows the publication.
