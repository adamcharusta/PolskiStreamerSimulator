import { afterEach, describe, expect, it } from 'vitest';

import css from './index.css?raw';

type Declarations = Record<string, string>;

const themes = ['overlay', 'win98', 'web2005'] as const;
type Theme = (typeof themes)[number];

/** The custom properties each selector of the stylesheet declares, in source order. */
function declarationsBySelector(): Map<string, Declarations> {
  const blocks = new Map<string, Declarations>();
  const source = css.replaceAll(/\/\*[\s\S]*?\*\//gu, '');
  for (const [, selectors = '', body = ''] of source.matchAll(/([^{}]+)\{([^{}]*)\}/gu)) {
    const declarations: Declarations = {};
    for (const [, name = '', value = ''] of body.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/gu)) {
      declarations[name] = value.trim();
    }
    for (const selector of selectors.split(',')) {
      const key = selector.trim();
      blocks.set(key, { ...blocks.get(key), ...declarations });
    }
  }
  return blocks;
}

const blocks = declarationsBySelector();

function tokens(theme: Theme, context?: string): Declarations {
  return {
    ...blocks.get(':root'),
    ...blocks.get(`[data-theme='${theme}']`),
    ...(context === undefined ? {} : blocks.get(context)),
  };
}

function colour(scope: Declarations, name: string): string {
  const value = scope[name];
  if (value === undefined) {
    throw new Error(`The token ${name} is missing.`);
  }
  const reference = /^var\((--[\w-]+)\)$/u.exec(value);
  return reference?.[1] === undefined ? value : colour(scope, reference[1]);
}

function luminance(hex: string): number {
  const match = /^#([\da-f]{2})([\da-f]{2})([\da-f]{2})$/iu.exec(hex);
  if (!match) {
    throw new Error(`${hex} is not a #rrggbb colour.`);
  }
  const [r = 0, g = 0, b = 0] = match.slice(1).map((part) => {
    const channel = Number.parseInt(part, 16) / 255;
    return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** The WCAG 2.2 contrast ratio of two colours. */
function contrast(first: string, second: string): number {
  const [light, dark] = [luminance(first), luminance(second)].toSorted((a, b) => b - a);
  return ((light ?? 0) + 0.05) / ((dark ?? 0) + 0.05);
}

describe.each(themes)('the %s theme (WCAG 2.2 AA)', (theme) => {
  it.each([
    ['--text', '--surface'],
    ['--page-text', '--bg'],
    ['--muted', '--surface'],
    ['--bar-text', '--bar'],
    ['--accent-text', '--accent'],
    ['--good', '--surface'],
    ['--bad', '--surface'],
    ['--field-text', '--field-bg'],
  ])('keeps the text %s on %s at 4.5:1 or more', (text, background) => {
    const scope = tokens(theme);
    expect(contrast(colour(scope, text), colour(scope, background))).toBeGreaterThanOrEqual(4.5);
  });

  it.each([
    ['--focus', '--bg'],
    ['--focus', '--surface'],
    ['--field-border', '--surface'],
  ])('keeps the indicator %s on %s at 3:1 or more', (indicator, background) => {
    const scope = tokens(theme);
    expect(contrast(colour(scope, indicator), colour(scope, background))).toBeGreaterThanOrEqual(3);
  });

  it('keeps the focus ring inside the top bar at 3:1 or more', () => {
    const scope = tokens(theme, '.topbar');
    expect(contrast(colour(scope, '--focus'), colour(scope, '--bar'))).toBeGreaterThanOrEqual(3);
  });
});

/** A fieldset with the given classes, styled by the whole stylesheet in jsdom. */
function styled(className: string): CSSStyleDeclaration {
  const style = document.createElement('style');
  style.textContent = css.replaceAll(/@import[^;]+;/gu, '');
  document.head.append(style);
  const fieldset = document.createElement('fieldset');
  fieldset.className = className;
  document.body.append(fieldset);
  return getComputedStyle(fieldset);
}

describe('the stylesheet cascade', () => {
  afterEach(() => {
    document.head.innerHTML = '';
    document.body.innerHTML = '';
  });

  it('keeps the panel padding on a choice group drawn as a panel', () => {
    // The panel's 0.75rem at jsdom's 16px root font size.
    expect(styled('choices panel').paddingTop).toBe('12px');
  });

  it('keeps a bare choice group without a frame', () => {
    expect(styled('choices').paddingTop).toMatch(/^0(px)?$/u);
  });
});
