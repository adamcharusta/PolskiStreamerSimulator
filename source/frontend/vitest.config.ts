import { defineConfig, mergeConfig } from 'vitest/config';

import viteConfig from './vite.config';

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      setupFiles: ['./test/setup.ts'],
      include: ['src/**/*.test.{ts,tsx}'],
      restoreMocks: true,
      // Stylesheets stay empty in tests, except raw imports that check theme tokens and the cascade.
      css: { include: [/\.css\?raw$/u] },
      coverage: {
        provider: 'v8',
        // LCOV paths relative to the repository root, SonarQube Cloud's base directory (TD-100).
        reporter: ['text-summary', ['lcov', { projectRoot: '../..' }]],
        include: ['src/**/*.{ts,tsx}'],
        exclude: ['src/api/generated/**', 'src/main.tsx', 'src/**/*.test.{ts,tsx}'],
        thresholds: { lines: 80 },
      },
    },
  }),
);
