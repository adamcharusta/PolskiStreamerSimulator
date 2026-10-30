import { execFileSync } from 'node:child_process';
import { createHmac } from 'node:crypto';
import { fileURLToPath } from 'node:url';

import AxeBuilder from '@axe-core/playwright';
import { expect, type Page, test } from '@playwright/test';

import pl from '../src/locales/pl/translation.json' with { type: 'json' };

const themes = ['overlay', 'win98', 'web2005'] as const;

const fill = (text: string, values: Record<string, string | number>): string =>
  text.replaceAll(/\{\{(\w+)\}\}/gu, (_, name: string) => String(values[name]));

/** WCAG 2.2 AA in every theme (spec §5): any axe violation on a key screen fails the journey. */
async function expectAccessible(page: Page, screen: string): Promise<void> {
  for (const theme of themes) {
    await page.evaluate((name) => {
      document.documentElement.dataset.theme = name;
    }, theme);
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
      .analyze();
    expect(results.violations, `${screen} (${theme})`).toEqual([]);
  }

  await page.evaluate(() => {
    document.documentElement.dataset.theme = 'overlay';
  });
}

test('a new career plays a manual week and a 13-week batch, then reloads to the same state', async ({
  page,
}) => {
  await page.goto('/');
  await expect(page.getByRole('alertdialog', { name: pl.gate.title })).toBeVisible();
  await expectAccessible(page, 'gate');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();

  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill('Tester E2E');
  await expectAccessible(page, 'character creation');
  await page.getByRole('button', { name: pl.create.submit }).click();

  await expect(page.getByRole('status', { name: pl.recovery.keyTitle })).toContainText('pss1.');
  await expectAccessible(page, 'recovery key');
  await page.getByRole('link', { name: pl.recovery.saved }).click();

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
  await expect(
    page.getByRole('heading', { name: fill(pl.report.title, { week: 1 }) }),
  ).toBeVisible();
  await expectAccessible(page, 'weekly report');
  await page.getByRole('button', { name: pl.report.next }).click();

  await expect(page.getByText(pl.event.decision)).toBeVisible();
  await expectAccessible(page, 'mandatory decision');
  await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
  await page.getByRole('button', { name: pl.event.choose }).click();
  await page.getByRole('button', { name: pl.roll.next }).click();
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();

  await page.getByRole('button', { name: pl.batch.launch }).click();
  await page.getByRole('button', { name: fill(pl.batch.confirm_many, { count: 13 }) }).click();
  await expect(page.getByRole('heading', { name: pl.batch.title })).toBeVisible({
    timeout: 60_000,
  });
  // Every career has a random seed, so the batch may end the career early; any legitimate stop passes, and the week
  // after it follows from the weeks the ledger lists. The stop reason is also in the screen reader announcement, so
  // the visible paragraph is the one checked.
  const stops = [
    pl.batch.stop.completed,
    pl.batch.stop.pausedForDecision,
    pl.batch.stop.careerEnded,
  ];
  const stop = page.locator('p.topbar').first();
  await expect(stop).toBeVisible();
  expect(stops).toContain(await stop.textContent());
  const weeks = await page
    .getByRole('list', { name: pl.batch.ledger })
    .locator(':scope > li')
    .count();
  expect(weeks).toBeGreaterThan(0);
  await expectAccessible(page, 'batch result');
  await page.getByRole('button', { name: pl.batch.next }).click();

  const desk = page.getByRole('region', { name: pl.career.summaryLabel });
  await expect(desk).toContainText(fill(pl.career.week, { week: 2 + weeks }));
  // A batch that paused for a decision lands on that decision first; answer it to reach the plan.
  const planOrEnd = page.getByRole('heading', {
    name: new RegExp(`^(${pl.plan.title}|${pl.career.endedTitle})$`, 'u'),
  });
  const decision = page.getByRole('button', { name: pl.event.choose });
  await expect(planOrEnd.or(decision)).toBeVisible();
  while (await decision.isVisible()) {
    await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
    await decision.click();
    await page.getByRole('button', { name: pl.roll.next }).click();
    await expect(planOrEnd.or(decision)).toBeVisible();
  }

  const next = page.getByRole('heading', {
    name: new RegExp(`^(${pl.plan.title}|${pl.career.endedTitle})$`, 'u'),
  });
  await expect(next).toBeVisible();
  const step = await next.textContent();
  const before = (await desk.textContent()) ?? '';
  await page.reload();
  await expect(desk).toHaveText(before);
  await expect(page.getByRole('heading', { name: step ?? '' })).toBeVisible();
});

test('an Industry Insider accepts the lead sponsor offer and plays the deal week', async ({
  page,
}) => {
  await page.goto('/');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill('Sponsor E2E');
  await page.getByRole('radio', { name: pl.background.industryInsider }).check();
  await page.getByRole('button', { name: pl.create.submit }).click();
  await page.getByRole('link', { name: pl.recovery.saved }).click();

  const offers = page.getByRole('region', { name: pl.offer.title });
  await expect(offers).toBeVisible();
  await expectAccessible(page, 'sponsor offers');
  await offers.getByRole('button', { name: pl.offer.accept }).click();
  await expect(offers.getByRole('list', { name: pl.offer.contracts })).toBeVisible();
  await expect(offers.getByText(pl.offer.dueNow)).toBeVisible();

  await page.getByRole('radio', { name: pl.action.noAction }).check();
  await page.getByRole('button', { name: pl.plan.start }).click();
  const choose = page.getByRole('button', { name: pl.event.choose });
  const settle = page.getByRole('button', { name: pl.settle.action });
  for (;;) {
    await expect(choose.or(settle)).toBeVisible();
    if (await settle.isVisible()) {
      break;
    }

    await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
    await choose.click();
    await page.getByRole('button', { name: pl.roll.next }).click();
  }

  await settle.click();
  await expect(
    page.getByRole('heading', { name: fill(pl.report.title, { week: 1 }) }),
  ).toBeVisible();
  await expect(
    page
      .getByText(pl.report.line.income.sponsor, { exact: false })
      .or(page.getByText(pl.report.line.cost['sponsor-penalty'], { exact: false }))
      .first(),
  ).toBeVisible();
  await page.getByRole('button', { name: pl.report.next }).click();
  await expect(
    page
      .getByRole('heading', { name: pl.plan.title })
      .or(page.getByRole('button', { name: pl.event.choose })),
  ).toBeVisible();
});

test('a 4-week batch under a custom strategy, then early retirement shows the score', async ({
  page,
}) => {
  await page.goto('/');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill('Strategia E2E');
  await page.getByRole('button', { name: pl.create.submit }).click();
  await page.getByRole('link', { name: pl.recovery.saved }).click();
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();

  await page.getByRole('button', { name: pl.batch.launch }).click();
  await page.getByRole('radio', { name: fill(pl.batch.weeks_few, { count: 4 }) }).check();
  await page.getByText(pl.strategy.title, { exact: true }).first().click();
  await page.getByLabel(pl.strategy.risk).selectOption('safe');
  await expectAccessible(page, 'batch strategy');
  await page.getByRole('button', { name: fill(pl.batch.confirm_few, { count: 4 }) }).click();
  await expect(page.getByRole('heading', { name: pl.batch.title })).toBeVisible({
    timeout: 30_000,
  });
  await page.getByRole('button', { name: pl.batch.next }).click();

  const decision = page.getByRole('button', { name: pl.event.choose });
  const retire = page.getByRole('button', { name: pl.retire.action });
  for (;;) {
    await expect(decision.or(retire)).toBeVisible();
    if (await retire.isVisible()) {
      break;
    }

    await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
    await decision.click();
    await page.getByRole('button', { name: pl.roll.next }).click();
  }

  await page.getByRole('link', { name: pl.dashboard.history }).click();
  await expect(page.getByRole('heading', { name: pl.history.title, level: 1 })).toBeVisible();
  await expect(
    page.getByRole('article', { name: fill(pl.history.year, { year: 1 }) }),
  ).toBeVisible();
  await page.getByRole('button', { name: pl.history.show }).first().click();
  await expect(
    page.getByRole('heading', { name: fill(pl.report.title, { week: 1 }) }),
  ).toBeVisible();
  await expectAccessible(page, 'history');
  await page.getByRole('link', { name: pl.history.back }).click();

  await page.getByRole('button', { name: pl.retire.action }).click();
  await page.getByRole('button', { name: pl.retire.confirm }).click();
  await expect(page.getByRole('table', { name: pl.ending.score.title })).toBeVisible();
  await expect(page.getByText(pl.career.ending.earlyRetirement)).toBeVisible();
  await expectAccessible(page, 'ending');
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: pl.diploma.download }).click();
  const file = await download;
  expect(file.suggestedFilename()).toBe('dyplom-Strategia E2E.pdf');

  // The career was ranked (the default): its server-computed score goes to the season table.
  const ranking = page.getByRole('region', { name: pl.leaderboard.panel.title });
  await ranking.getByLabel(pl.leaderboard.panel.nickname).fill('e2e');
  await ranking.getByRole('button', { name: pl.leaderboard.panel.submit }).click();
  await expect(ranking.getByText(/^E2E — .* miejsce w sezonie \d+\.$/u)).toBeVisible();
  await ranking.getByRole('link', { name: pl.leaderboard.panel.show }).click();
  await expect(page.getByRole('heading', { name: pl.leaderboard.title, level: 1 })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: 'E2E' }).first()).toBeVisible();
  await expectAccessible(page, 'leaderboard');
});

test('a career recovered with its key in a browser that lost its data continues where it was', async ({
  page,
}) => {
  await page.goto('/');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill('Odzysk E2E');
  await page.getByRole('button', { name: pl.create.submit }).click();
  const key = (await page.getByRole('status', { name: pl.recovery.keyTitle }).textContent()) ?? '';
  expect(key).toMatch(/^pss1\./u);
  const careerPath = new URL(page.url()).pathname.replace(/\/klucz$/u, '');

  await page.evaluate(() => {
    localStorage.clear();
  });
  await page.goto('/odzyskaj');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByLabel(pl.recover.keyLabel).fill(key);
  await expectAccessible(page, 'recovery');
  await page.getByRole('button', { name: pl.recover.submit }).click();

  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
  expect(new URL(page.url()).pathname).toBe(careerPath);
});

/** Plays the manual week from the plan to its report, choosing the first option of every event. */
async function playRestWeek(page: Page): Promise<void> {
  await page.getByRole('radio', { name: pl.action.rest }).check();
  await page.getByRole('button', { name: pl.plan.start }).click();
  const choose = page.getByRole('button', { name: pl.event.choose });
  const settle = page.getByRole('button', { name: pl.settle.action });
  for (;;) {
    await expect(choose.or(settle)).toBeVisible();
    if (await settle.isVisible()) {
      break;
    }

    await page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first().check();
    await choose.click();
    await page.getByRole('button', { name: pl.roll.next }).click();
  }

  await settle.click();
  await page.getByRole('button', { name: pl.report.next }).click();
}

test('an unranked career restores a slot, then its export imports as a new career', async ({
  page,
}) => {
  await page.goto('/');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill('Zapis E2E');
  await page.getByRole('radio', { name: new RegExp(`^${pl.create.unranked}`, 'u') }).check();
  await page.getByRole('button', { name: pl.create.submit }).click();
  await page.getByRole('link', { name: pl.recovery.saved }).click();
  const desk = page.getByRole('region', { name: pl.career.summaryLabel });
  await expect(desk).toContainText(fill(pl.career.week, { week: 1 }));

  await page.getByRole('link', { name: pl.dashboard.saves }).click();
  await page.getByRole('button', { name: fill(pl.saves.saveHere, { slot: 1 }) }).click();
  await expect(page.getByText(fill(pl.saves.saved, { slot: 1 }))).toBeVisible();
  await expectAccessible(page, 'saves');
  await page.getByRole('link', { name: pl.saves.back }).click();

  await playRestWeek(page);
  await expect(desk).toContainText(fill(pl.career.week, { week: 2 }));

  await page.getByRole('link', { name: pl.dashboard.saves }).click();
  await page.getByRole('button', { name: fill(pl.saves.restore, { slot: 1 }) }).click();
  await page.getByRole('button', { name: pl.saves.load }).click();
  await expect(page.getByText(fill(pl.saves.restored, { slot: 1, week: 1 }))).toBeVisible();
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: pl.saves.export }).click();
  const downloaded = await download;
  const file = await downloaded.path();
  await page.getByRole('link', { name: pl.saves.back }).click();
  await expect(desk).toContainText(fill(pl.career.week, { week: 1 }));
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();

  await page.goto('/importuj');
  await page.getByLabel(pl.import.fileLabel).setInputFiles(file);
  await expectAccessible(page, 'import');
  await page.getByRole('button', { name: pl.import.submit }).click();
  await expect(page.getByRole('status', { name: pl.recovery.keyTitle })).toContainText('pss1.');
  await page.getByRole('link', { name: pl.recovery.saved }).click();
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
  await expect(desk).toContainText('Zapis E2E');
  await expect(desk).toContainText(fill(pl.career.week, { week: 1 }));
});

/** RFC 6238 code for a Base32 key, as an authenticator app shows it. */
function totp(base32: string, at = Date.now()): string {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const char of base32.replaceAll('=', '').toUpperCase()) {
    bits += alphabet.indexOf(char).toString(2).padStart(5, '0');
  }

  const key = Buffer.from(bits.match(/.{8}/gu)?.map((byte) => Number.parseInt(byte, 2)) ?? []);
  const counter = Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(at / 30_000)));
  const hash = createHmac('sha1', key).update(counter).digest();
  const offset = (hash.at(-1) ?? 0) & 0x0f;
  return String((hash.readUInt32BE(offset) & 0x7f_ff_ff_ff) % 1_000_000).padStart(6, '0');
}

/** Runs `owner-create` inside the API container, resetting the owner when an earlier run created it. */
function ownerCredentials(): { username: string; password: string; key: string } {
  const repository = fileURLToPath(new URL('../../..', import.meta.url));
  const run = (...args: string[]) =>
    execFileSync(
      'docker',
      [
        'compose',
        'exec',
        '-T',
        'api',
        'dotnet',
        'PolskiStreamerSimulator.WebApp.dll',
        'owner-create',
        ...args,
      ],
      { cwd: repository, encoding: 'utf8' },
    );
  let output: string;
  try {
    output = run('--username', 'e2e-owner');
  } catch {
    output = run('--reset');
  }

  const field = (name: string) =>
    output
      .split(/\r?\n/u)
      .find((line) => line.startsWith(`${name}: `))
      ?.slice(name.length + 2)
      .trim() ?? '';
  return { username: field('Owner'), password: field('Password'), key: field('Authenticator key') };
}

test('the owner signs in with an authenticator code and hides a published entry', async ({
  page,
  request,
}) => {
  const created = await request.post('/v1/careers', {
    data: {
      nickname: 'Moderacja E2E',
      background: 'dormRoomNewcomer',
      difficulty: 'normal',
      pronouns: 'woman',
      ranked: true,
    },
  });
  const career = (await created.json()) as {
    careerId: string;
    secret: string;
    career: { version: number };
  };
  const auth = { Authorization: `Bearer ${career.secret}` };
  await request.post(`/v1/careers/${career.careerId}/actions`, {
    headers: auth,
    data: {
      commandId: crypto.randomUUID(),
      expectedVersion: career.career.version,
      type: 'retire',
    },
  });
  const published = await request.post(`/v1/careers/${career.careerId}/leaderboard-entry`, {
    headers: auth,
    data: { nickname: 'MOD' },
  });
  expect(published.ok()).toBe(true);
  const owner = ownerCredentials();

  await page.goto('/admin');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByLabel(pl.admin.signIn.username).fill(owner.username);
  await page.getByLabel(pl.admin.signIn.password).fill(owner.password);
  await expectAccessible(page, 'owner sign-in');
  await page.getByRole('button', { name: pl.admin.signIn.next }).click();
  await page.getByLabel(pl.admin.signIn.code).fill(totp(owner.key));
  await page.getByRole('button', { name: pl.admin.signIn.submit }).click();
  await expect(
    page.getByText(fill(pl.admin.signedInAs, { username: owner.username })),
  ).toBeVisible();

  const list = page.getByRole('list', { name: pl.admin.moderation.list });
  await list
    .getByRole('button', { name: fill(pl.admin.moderation.hide, { nickname: 'MOD' }) })
    .first()
    .click();
  await list.getByLabel(pl.admin.moderation.reason).fill('Test moderacji E2E');
  await list
    .getByRole('button', { name: fill(pl.admin.moderation.confirmHide, { nickname: 'MOD' }) })
    .click();
  await expect(
    list.getByText(fill(pl.admin.moderation.hidden, { reason: 'Test moderacji E2E' })).first(),
  ).toBeVisible();
  await expect(page.getByRole('region', { name: pl.admin.audit.title })).toContainText(
    'Test moderacji E2E',
  );
  await expectAccessible(page, 'owner panel');

  await page.goto('/ranking');
  await expect(page.getByRole('heading', { name: pl.leaderboard.title, level: 1 })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: 'MOD' })).toHaveCount(0);
});

test('the owner publishes a title fix that keeps the season', async ({ page }) => {
  const owner = ownerCredentials();
  await page.goto('/admin');
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
  await page.getByLabel(pl.admin.signIn.username).fill(owner.username);
  await page.getByLabel(pl.admin.signIn.password).fill(owner.password);
  await page.getByRole('button', { name: pl.admin.signIn.next }).click();
  await page.getByLabel(pl.admin.signIn.code).fill(totp(owner.key));
  await page.getByRole('button', { name: pl.admin.signIn.submit }).click();

  const content = page.getByRole('region', { name: pl.admin.content.title });
  const create = content.getByRole('button', { name: pl.admin.content.create });
  const discard = content.getByRole('button', { name: pl.admin.content.discard });
  await expect(create.or(discard)).toBeVisible();
  if (await discard.isVisible()) {
    await discard.click();
  }

  await create.click();
  const events = content.getByRole('region', { name: pl.admin.content.events.title });
  await events.getByLabel(pl.admin.content.events.filter).fill('jc-hot-take');
  await events.getByRole('button', { name: /^jc-hot-take v/u }).click();
  const editor = events.getByLabel(fill(pl.admin.content.events.json, { id: 'jc-hot-take' }));
  const item = JSON.parse(await editor.inputValue()) as { title: string; version: number };
  item.title = `Gorąca teza (${String(Date.now())})`;
  item.version += 1;
  await editor.fill(JSON.stringify(item, null, 2));
  await events.getByRole('button', { name: pl.admin.content.events.save }).click();

  await expect(content.getByText(pl.admin.content.sameSeason)).toBeVisible();
  await expect(content.getByText(pl.admin.content.noProblems)).toBeVisible();
  await expectAccessible(page, 'content draft');
  const publish = content.getByRole('form', { name: pl.admin.content.publishTitle });
  await publish.getByLabel(pl.admin.moderation.reason).fill('Poprawka tytułu E2E');
  await publish.getByRole('button', { name: pl.admin.content.publish }).click();
  await expect(content.getByText(/^Opublikowano jako #\d+\./u)).toBeVisible({ timeout: 60_000 });

  const history = content.getByRole('region', { name: pl.admin.content.history.title });
  await expect(history).toContainText('Poprawka tytułu E2E');
  await expect(content.getByRole('button', { name: pl.admin.content.create })).toBeVisible();
});
