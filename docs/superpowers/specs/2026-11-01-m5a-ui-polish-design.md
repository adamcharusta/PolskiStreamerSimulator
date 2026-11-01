# M5-A Interface and Test Polish — Design

Date: 2026-11-01
Status: **Agent proposal (2026-11-01).** The owner asked the agent to deliver the whole of M5 ("ok kontynyuj zrób cały millestone 5", 2026-11-01) after the M4 pattern of sub-projects without a design interview. Every rule below is an agent proposal for owner review.
Milestone: [M5 — Content, balance, and quality](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/5), sub-project A, delivering [#79](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/79), [#78](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/78) and [#77](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/77) (TD-127).

## 0. M5 decomposition

M5 is split into sub-projects, each with its own design and PR, delivered in this order:

| Sub-project | Issues | Content |
| --- | --- | --- |
| Sample content | [#114](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/114) | 20 sample events in `content/m3` (owner decision, 2026-11-01). |
| A interface and test polish | #79, #78, #77 | Deferred M2-C review findings: form defaults, drawer state, journey coverage, CI diagnostics, announcements, accessible names, headings. |
| B localization and text filters | #35 | Missing keys, pronoun forms, the `Sensitive content` wording filter and Streamer Friendly masking in the UI, announcements and the PDF. |
| C accessibility and browsers | #37 | WCAG 2.2 AA audit of the journeys, 320 px layouts, reduced motion, and the browser matrix that can be automated. |
| D security and privacy review | #38 | Threat model, validation, rate limits and CSP; legal findings recorded as open questions for a qualified reviewer. |
| E balance | #36 | 10,000-career report: the runaway economy, sponsor caps and bold-strategy bans; season-one rules frozen. |

Outside the agent's reach and left open in M5: the owner's authoring-time measurement and launch floor (#7), launch content to that floor (#32–#34, written by the owner once CI/CD is in place), moderated 60–120-minute playtests (#36), screen-reader and real-device checks (#37) and the legal review (#38).

## 1. Rulings

1. **Creation defaults (#79).** Each preselected choice is one the server lists in `GET /v1/career-options`. The form keeps today's preferences (the dorm-room newcomer, normal difficulty, woman) when the server offers them and otherwise preselects the first value the server lists. Taking the first value always would have changed the default difficulty to easy, because the server lists `Easy` first.
2. **Statistics drawer (#79).** The drawer closes when the career screen unmounts or the career ID changes.
3. **Journey coverage (#78).** The first browser journey runs axe on the open batch confirmation dialog in all three themes and checks the batch result at 320 CSS pixels without horizontal scrolling; the 320-pixel check is one helper shared with the creation step.
4. **CI diagnostics (#78).** `frontend-e2e` collects the stack logs and uploads its report on `failure() || cancelled()`, so a timeout also leaves diagnostics.
5. **No-request test (#78).** The `useCareer` test waits 100 ms after the first render before counting requests, so a request sent later by an effect, retry or refetch fails it.
6. **Stage announcements (#77).** The focused step heading names each result; the stage's polite live region adds only what the heading leaves out: the roll line after a roll, the stop reason after a batch, nothing after a week report. Each reveal renders a new keyed node, so an identical result is announced again.
7. **Save status (#77).** The top bar's save status stays visible but is no longer a live region (`<output>` became a `<span>`); it changed with every command and talked over the stage. Connection loss is still announced by the connection banner.
8. **Disabled reasons and tiers (#77).** A disabled reason and an equipment slot's tier stay inside the radio's label for sighted players but are `aria-hidden` there; the radio's `aria-describedby` reads them once as its description. A slot radio's name is the slot alone.
9. **Server nickname error (#77).** A nickname the server rejects is set with `shouldFocus`, so focus moves to the field.
10. **Ledger headings (#77).** `WeekReport` takes `headingLevel` 3 (default) or 5; the batch ledger passes 5, so its folded reports sit under the `h4` week headings.
