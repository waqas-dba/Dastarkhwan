import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:53584',
    extraHTTPHeaders: { 'Accept': 'application/json' },
  },
  reporter: [['html', { open: 'never' }]],
});
