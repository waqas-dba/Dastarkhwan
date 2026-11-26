import { defineConfig } from '@playwright/test';
import { WEB_BASE_URL } from './helpers/config';

/**
 * E2E configuration.
 *
 * - baseURL defaults to the Web host (HTTPS 53584). Admin-host specs construct an
 *   ApiClient with ADMIN_BASE_URL explicitly.
 * - ignoreHTTPSErrors is required because the dev hosts use a self-signed certificate.
 * - CI-friendly reporters (list + junit) plus the HTML report.
 * - Trace + screenshot on failure for diagnostics.
 */
export default defineConfig({
  testDir: '.',
  fullyParallel: false,
  workers: 1,
  timeout: 30_000,
  expect: { timeout: 5_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ['list'],
    ['junit', { outputFile: 'test-results/junit.xml' }],
    ['html', { open: 'never' }],
  ],
  use: {
    baseURL: process.env.BASE_URL || WEB_BASE_URL,
    ignoreHTTPSErrors: true,
    extraHTTPHeaders: { Accept: 'application/json' },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});