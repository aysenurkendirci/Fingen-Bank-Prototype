import { defineConfig, devices } from '@playwright/test';

// End-to-end checks of the whole app in a real browser, against a running API. The API is started separately
// (CI starts it in its own step); the dev server below proxies /api to it.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : 'list',
  timeout: 60_000,
  use: {
    baseURL: 'http://localhost:4200',
    locale: 'tr-TR',
    trace: 'retain-on-failure',
    // Locally the installed Chrome is used, so nothing has to be downloaded; CI installs Chromium.
    ...(process.env['CI'] ? {} : { channel: 'chrome' }),
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } }],
  webServer: {
    command: 'npm start -- --port 4200',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  },
});
