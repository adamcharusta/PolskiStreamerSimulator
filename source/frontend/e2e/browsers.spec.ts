import type { Locator, Page } from '@playwright/test';

import pl from '../src/locales/pl/translation.json' with { type: 'json' };
import { expect, expectAccessible, fill, test } from './support';

// M5-C (#37): the core journey in every supported engine and on emulated phones (playwright.config.ts projects),
// plus keyboard-only play, reduced motion and the secondary screens at 320 CSS pixels.

/** No horizontal page scroll at the current viewport width. */
async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const { scroll, width } = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    width: window.innerWidth,
  }));
  const wide = await page.evaluate(() =>
    [...document.querySelectorAll('body *')]
      .filter((element) => element.getBoundingClientRect().right > window.innerWidth + 0.5)
      .slice(0, 8)
      .map(
        (element) =>
          `${element.tagName}.${element.className} ${Math.round(element.getBoundingClientRect().right)} ${element.textContent.slice(0, 40)}`,
      ),
  );
  expect(scroll, wide.join(' | ')).toBeLessThanOrEqual(width);
}

async function enterGame(page: Page): Promise<void> {
  await page.goto('/');
  await expect(page.getByRole('alertdialog', { name: pl.gate.title })).toBeVisible();
  await page.getByRole('button', { name: pl.gate.confirm }).click();
  await page.getByRole('button', { name: pl.notice.dismiss }).click();
}

async function createCareer(page: Page, nickname: string): Promise<void> {
  await page.getByRole('link', { name: pl.home.new }).click();
  await page.getByLabel(pl.create.nickname).fill(nickname);
  await page.getByRole('button', { name: pl.create.submit }).click();
  await expect(page.getByRole('status', { name: pl.recovery.keyTitle })).toContainText('pss1.');
  await page.getByRole('link', { name: pl.recovery.saved }).click();
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();
}

/** Plays a rest week through its live events to the weekly report. */
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
  await expect(
    page.getByRole('heading', { name: fill(pl.report.title, { week: 1 }) }),
  ).toBeVisible();
}

test('the first week plays in this browser without horizontal scrolling', async ({ page }) => {
  await enterGame(page);
  await expectNoHorizontalScroll(page);
  await createCareer(page, 'Przeglądarka');
  await expectNoHorizontalScroll(page);
  await expectAccessible(page, 'week plan');

  await playRestWeek(page);
  await expectNoHorizontalScroll(page);
  await page.getByRole('button', { name: pl.report.next }).click();
  await expect(page.getByText(pl.event.decision)).toBeVisible();

  await page.reload();
  await expect(page.getByText(pl.event.decision)).toBeVisible();
});

/** Presses Tab until the target has focus; fails if it is not reachable within the limit. */
async function tabTo(page: Page, target: Locator, limit = 80): Promise<void> {
  const seen: string[] = [];
  for (let presses = 0; presses < limit; presses += 1) {
    if (await target.evaluate((element) => element === document.activeElement)) {
      return;
    }

    await page.keyboard.press('Tab');
    seen.push(
      await page.evaluate(() => {
        const active = document.activeElement;
        return `${active?.tagName ?? ''} ${(active?.textContent ?? '').trim().slice(0, 24)}`;
      }),
    );
  }

  throw new Error(
    `The control cannot be reached with the Tab key; focus went to: ${seen.slice(0, 20).join(' | ')}`,
  );
}

/** WCAG 2.4.7: the focused control shows a visible focus indicator. */
async function expectVisibleFocus(target: Locator): Promise<void> {
  const style = await target.evaluate((element) => {
    const computed = getComputedStyle(element);
    return {
      outline: computed.outlineStyle,
      width: computed.outlineWidth,
      shadow: computed.boxShadow,
    };
  });
  const outlined = style.outline !== 'none' && style.width !== '0px';
  expect(outlined || style.shadow !== 'none', JSON.stringify(style)).toBe(true);
}

// Chromium and Firefox only (grepInvert in playwright.config.ts): phones have no Tab key, and WebKit keeps links out
// of the Tab order by default (Safari's "Press Tab to highlight each item"), with no Playwright key that adds them.
test('a career starts and plays a week with the keyboard alone', async ({ page }) => {
  await page.goto('/');
  const confirm = page.getByRole('button', { name: pl.gate.confirm });
  await tabTo(page, confirm);
  await expectVisibleFocus(confirm);
  await page.keyboard.press('Enter');
  const dismiss = page.getByRole('button', { name: pl.notice.dismiss });
  await tabTo(page, dismiss);
  await page.keyboard.press('Enter');

  const newCareer = page.getByRole('link', { name: pl.home.new });
  await tabTo(page, newCareer);
  await expectVisibleFocus(newCareer);
  await page.keyboard.press('Enter');
  const nickname = page.getByLabel(pl.create.nickname);
  await tabTo(page, nickname);
  await page.keyboard.type('Klawiatura');
  const submit = page.getByRole('button', { name: pl.create.submit });
  await tabTo(page, submit);
  await page.keyboard.press('Enter');

  const saved = page.getByRole('link', { name: pl.recovery.saved });
  await tabTo(page, saved);
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: pl.plan.title })).toBeVisible();

  const rest = page.getByRole('radio', { name: pl.action.rest });
  await tabTo(page, rest);
  await expectVisibleFocus(rest);
  await page.keyboard.press('Space');
  await expect(rest).toBeChecked();
  const start = page.getByRole('button', { name: pl.plan.start });
  await tabTo(page, start);
  await page.keyboard.press('Enter');

  const choose = page.getByRole('button', { name: pl.event.choose });
  const settle = page.getByRole('button', { name: pl.settle.action });
  for (;;) {
    await expect(choose.or(settle)).toBeVisible();
    if (await settle.isVisible()) {
      break;
    }

    const first = page.getByRole('group', { name: pl.event.choices }).getByRole('radio').first();
    await tabTo(page, first);
    await page.keyboard.press('Space');
    await tabTo(page, choose);
    await page.keyboard.press('Enter');
    const next = page.getByRole('button', { name: pl.roll.next });
    await tabTo(page, next);
    await page.keyboard.press('Enter');
  }

  await tabTo(page, settle);
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('heading', { name: fill(pl.report.title, { week: 1 }) }),
  ).toBeVisible();
});

test('reduced motion removes the tilted buttons', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await enterGame(page);
  const button = page.getByRole('link', { name: pl.home.new });
  await expect(button).toBeVisible();
  const transforms = await page
    .locator('.btn-primary, .btn')
    .evaluateAll((elements) => elements.map((element) => getComputedStyle(element).transform));
  for (const transform of transforms) {
    expect(transform).toBe('none');
  }
});

// Desktop engines only (grepInvert in playwright.config.ts): the phones run the first test at their own width.
test('the secondary screens fit 320 pixels and pass axe', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await enterGame(page);
  await expectNoHorizontalScroll(page);
  await expectAccessible(page, 'home');

  await page.getByRole('button', { name: pl.app.settings }).click();
  await expect(page.getByRole('dialog', { name: pl.app.settings })).toBeVisible();
  await expectNoHorizontalScroll(page);
  await expectAccessible(page, 'settings');
  await page.keyboard.press('Escape');

  await page.getByRole('link', { name: pl.home.leaderboard }).click();
  await expect(page.getByRole('heading', { name: pl.leaderboard.title, level: 1 })).toBeVisible();
  await expectNoHorizontalScroll(page);
  await expectAccessible(page, 'leaderboard');

  await page.goto('/odzyskaj');
  await expect(page.getByRole('heading', { name: pl.recover.title })).toBeVisible();
  await expectNoHorizontalScroll(page);
  await expectAccessible(page, 'recovery');

  await page.goto('/');
  await createCareer(page, 'Wąski Ekran');
  await page.getByRole('link', { name: pl.dashboard.history }).click();
  await expect(page.getByRole('heading', { name: pl.history.title, level: 1 })).toBeVisible();
  await expectNoHorizontalScroll(page);
  await expectAccessible(page, 'history');
});
