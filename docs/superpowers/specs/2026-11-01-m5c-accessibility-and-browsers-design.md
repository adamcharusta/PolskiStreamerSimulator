# M5-C Accessibility and Browsers — Design

Date: 2026-11-01
Status: **Agent proposal (2026-11-01).** Delivered without a design interview at the owner's request ("ok kontynyuj zrób cały millestone 5", 2026-11-01). Every rule below is an agent proposal for owner review.
Milestone: [M5 — Content, balance, and quality](https://github.com/adamcharusta/PolskiStreamerSimulator/milestone/5), sub-project C, delivering the automated part of [#37](https://github.com/adamcharusta/PolskiStreamerSimulator/issues/37) (TD-129). The M5 decomposition is in the [M5-A design §0](2026-11-01-m5a-ui-polish-design.md).

## 1. Intent

Prove the WCAG 2.2 AA and browser-target requirements (REQ-21, REQ-24) with automated browser tests wherever a machine can judge them, fix what they find, and name what still needs a person.

## 2. Rulings

1. **Browser matrix.** Playwright runs five projects: `chromium` (every journey), and `firefox`, `webkit` (the Safari engine), `mobile-chrome` (Pixel 7) and `mobile-safari` (iPhone 14), which run `e2e/browsers.spec.ts`. Edge shares Chromium's engine and is covered by `chromium`. CI installs Chromium, Firefox and WebKit.
2. **Cross-browser spec.** `e2e/browsers.spec.ts` plays the first week with no horizontal scroll and reloads to the same step in every project; plays it again with the keyboard alone (Tab, Space, Enter) in Chromium and Firefox and checks a visible focus indicator; checks that reduced motion leaves no tilted button; and checks the home, settings, leaderboard, recovery and history screens at 320 CSS pixels with axe in all three themes. Shared helpers moved to `e2e/support.ts`.
3. **WebKit keyboard.** WebKit keeps links out of the Tab order by default (Safari's "Press Tab to highlight each item"), and Playwright's WebKit has no key that adds them, so the `webkit` project leaves out the keyboard journey. Phones have no Tab key; the phone projects leave out the keyboard journey and the secondary-screen check (`grepInvert` in `playwright.config.ts`).
4. **Fixes found by the matrix.** A file picker's built-in width pushed the recovery form to 336 pixels on a 320-pixel screen; file inputs now take the full width and may shrink. The leaderboard's horizontally scrolling table wrapper could not be reached by keyboard (axe `scrollable-region-focusable`); it is now a labelled, focusable `section`.
5. **Reduced motion.** The game has no animation or transition; the only motion, the tilted buttons, is undone under `prefers-reduced-motion`, and the browser test checks every button. Any later effect must add its own reduced-motion rule (a global `!important` override was dropped: SonarQube rule css:S9423).
6. **Test stack.** Playwright uses two workers (as a CI runner does): more parallel browsers stalled requests through the development stack's Vite proxy. Compose reads the creation rate limit from `CREATE_PERMITS` (default 20, the API default); the `frontend-e2e` job sets 200 because the matrix creates more careers from one address than a 10-minute window allows. The job timeout rises from 30 to 45 minutes.

## 3. Left for people

Real-device checks on current Safari for iOS and Chrome for Android, a screen-reader pass (NVDA with Firefox, VoiceOver with Safari) through creation, a manual week, a batch and the ending, and a review of the reading order at 200 % zoom. #37 stays open until the owner records those results.
