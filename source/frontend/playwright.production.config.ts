import { defineConfig, devices } from '@playwright/test';

// M5-D (#38): the built app under the production content security policy (no inline script, no eval), served by
// `vite preview` with the same headers the production web server sends; the API is the Compose stack's.
export default defineConfig({
  testDir: 'e2e',
  testMatch: /browsers\.spec\.ts/u,
  grep: /first week/u,
  timeout: 180_000,
  expect: { timeout: 15_000 },
  retries: 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-production' }]],
  use: { baseURL: 'http://localhost:4173', trace: 'retain-on-failure' },
  webServer: {
    command: 'npx vite preview --port 4173 --strictPort',
    url: 'http://localhost:4173',
    reuseExistingServer: false,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
});
