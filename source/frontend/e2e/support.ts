import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';

const themes = ['overlay', 'win98', 'web2005'] as const;

export const fill = (text: string, values: Record<string, string | number>): string =>
  text.replaceAll(/\{\{(\w+)\}\}/gu, (_, name: string) => String(values[name]));

/** WCAG 2.2 AA in every theme (spec §5): any axe violation on a key screen fails the journey. */
export async function expectAccessible(page: Page, screen: string): Promise<void> {
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

/** Spec §4: every action fits a 320 CSS pixel viewport without horizontal scrolling. */
export async function expectFitsNarrowScreen(page: Page): Promise<void> {
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
  await page.setViewportSize({ width: 1280, height: 800 });
}
