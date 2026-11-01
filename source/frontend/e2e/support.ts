import AxeBuilder from '@axe-core/playwright';
import { expect, type Page, test as base } from '@playwright/test';

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

/** Records every content security policy violation on the page (M5-D, #38), from the first navigation on. */
async function watchCsp(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const seen: string[] = [];
    Object.assign(globalThis, { cspViolations: seen });
    document.addEventListener('securitypolicyviolation', (event) => {
      seen.push(`${event.violatedDirective} ${event.blockedURI} ${event.sourceFile}`);
    });
  });
}

async function expectNoCspViolations(page: Page): Promise<void> {
  const violations = await page.evaluate(
    () => (globalThis as unknown as { cspViolations?: string[] }).cspViolations ?? [],
  );
  expect(violations).toEqual([]);
}

/** Every journey runs under the development content security policy and fails on any violation (M5-D, #38). */
// An automatic fixture: tests never read its value.
export const test = base.extend<{ csp: true }>({
  csp: [
    async ({ page }, use) => {
      await watchCsp(page);
      await use(true);
      await expectNoCspViolations(page);
    },
    { auto: true },
  ],
});

export { expect } from '@playwright/test';
