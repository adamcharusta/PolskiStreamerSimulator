import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';

const severity = (entry: unknown): unknown => (Array.isArray(entry) ? entry[0] : entry);

describe('the ESLint configuration (repository standards)', () => {
  it('applies the React, hooks and accessibility rules to components', async () => {
    const config = (await new ESLint().calculateConfigForFile('src/App.tsx')) as {
      rules: Record<string, unknown>;
    };

    for (const rule of [
      'react/jsx-key',
      'react/no-unescaped-entities',
      'react/jsx-no-target-blank',
      'react-hooks/rules-of-hooks',
      'jsx-a11y/alt-text',
    ]) {
      expect([2, 'error']).toContain(severity(config.rules[rule]));
    }

    expect(
      severity(config.rules['react/react-in-jsx-scope']),
      'the JSX runtime needs no React import',
    ).toBe(0);
  });
});
