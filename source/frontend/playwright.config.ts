import { defineConfig, devices } from '@playwright/test';

// The journeys against the Compose stack (TD-100); no retries, so a flaky step is fixed rather than hidden.
export default defineConfig({
  testDir: 'e2e',
  timeout: 180_000,
  expect: { timeout: 15_000 },
  retries: 0,
  // Two workers, as on a CI runner: more parallel browsers stall requests through the development stack's Vite proxy.
  workers: 2,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: { baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173', trace: 'retain-on-failure' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    // M5-C (#37): the browser matrix runs the shorter cross-browser spec in the other engines and on emulated phones.
    { name: 'firefox', testMatch: /browsers\.spec\.ts/u, use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', testMatch: /browsers\.spec\.ts/u, use: { ...devices['Desktop Safari'] } },
    { name: 'mobile-chrome', testMatch: /browsers\.spec\.ts/u, use: { ...devices['Pixel 7'] } },
    { name: 'mobile-safari', testMatch: /browsers\.spec\.ts/u, use: { ...devices['iPhone 14'] } },
  ],
});
