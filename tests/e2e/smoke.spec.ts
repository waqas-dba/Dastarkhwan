import { test, expect } from '@playwright/test';

test('health endpoint responds', async ({ request }) => {
  const res = await request.get('/health/live');
  expect(res.ok()).toBeTruthy();
});
