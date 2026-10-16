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

  await page.getByRole('button', { name: pl.batch.launch }).click();
  await page.getByRole('button', { name: pl.batch.confirm }).click();
  await expect(page.getByRole('heading', { name: pl.batch.title })).toBeVisible({
    timeout: 60_000,
  });
  // The stop reason is also in the screen reader announcement; the visible paragraph is the one to check.
  await expect(
    page.getByRole('paragraph').filter({ hasText: pl.batch.stop.completed }),
  ).toBeVisible();
  await expectAccessible(page, 'batch result');
  await page.getByRole('button', { name: pl.batch.next }).click();

  const desk = page.getByRole('region', { name: pl.career.summaryLabel });
  await expect(desk).toContainText(fill(pl.career.week, { week: 15 }));
  const before = (await desk.textContent()) ?? '';
  await page.reload();
  await expect(desk).toHaveText(before);
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
});
