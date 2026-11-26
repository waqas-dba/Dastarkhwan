// Dastarkhwan E2E Test Configuration
// API testing via Playwright APIRequestContext against local Web and Admin hosts.
// No browser automation is required for API-level E2E tests.

const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './tests',
  timeout: 30000,
  expect: {
    timeout: 10000,
  },
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5000',
    extraHTTPHeaders: {
      'Accept': 'application/json',
      'Content-Type': 'application/json',
    },
    // No browser needed for API tests
    launchOptions: {
      args: [],
    },
  },
  projects: [
    {
      name: 'api',
      testMatch: /.*\.spec\.js$/,
    },
  ],
});
