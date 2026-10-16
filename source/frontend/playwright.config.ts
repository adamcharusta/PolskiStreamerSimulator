import { defineConfig, devices } from '@playwright/test';

// One journey against the Compose stack (TD-100); no retries, so a flaky step is fixed rather than hidden.
export default defineConfig({
  testDir: 'e2e',
  timeout: 180_000,
  expect: { timeout: 15_000 },
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
