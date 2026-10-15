import { describe, expect, it } from 'vitest';

import css from './index.css?raw';

// Vite needs the glob options written out as literals in each call.
const sheets = import.meta.glob<string>('/node_modules/@fontsource/*/*.css', {
  query: '?raw',
  import: 'default',
  eager: true,
});
const main = Object.values(
  import.meta.glob<string>('../main.tsx', { query: '?raw', import: 'default', eager: true }),
).join('');

const polishLetters = [
  'ą',
  'ć',
  'ę',
  'ł',
  'ń',
  'ó',
  'ś',
  'ź',
  'ż',
  'Ą',
  'Ć',
  'Ę',
  'Ł',
  'Ń',
  'Ó',
  'Ś',
  'Ź',
  'Ż',
];

/** The self-hosted heading families: the quoted first family of each theme's heading font. */
const headingFamilies = [
  ...new Set([...css.matchAll(/--font-heading:\s*'([^']+)'/gu)].map(([, family = '']) => family)),
];

/** The font faces the app ships: every Fontsource stylesheet that main.tsx imports. */
const shippedFaces = [...main.matchAll(/import '(@fontsource\/[^']+\.css)';/gu)].flatMap(
  ([, path = '']) => [
    ...(sheets[`/node_modules/${path}`] ?? '').matchAll(/@font-face\s*\{([^}]*)\}/gu),
  ],
);

function ranges(family: string): [number, number][] {
  return shippedFaces
    .map(([, body = '']) => body)
    .filter((body) => body.includes(`font-family: '${family}'`))
    .flatMap((body) => /unicode-range:\s*([^;]+);/u.exec(body)?.[1]?.split(',') ?? [])
    .map((range) => {
      const [start = '0', end = start] = range.trim().replace('U+', '').split('-');
      return [Number.parseInt(start, 16), Number.parseInt(end, 16)];
    });
}

describe('the self-hosted heading fonts (spec §5)', () => {
  it('finds the heading families in the stylesheet', () => {
    expect(headingFamilies.length).toBeGreaterThan(0);
  });

  it.each(headingFamilies)('ships %s with every Polish letter', (family) => {
    const covered = ranges(family);
    const missing = polishLetters.filter((letter) => {
      const code = letter.codePointAt(0) ?? 0;
      return !covered.some(([start, end]) => code >= start && code <= end);
    });
    expect(missing).toEqual([]);
  });
});
