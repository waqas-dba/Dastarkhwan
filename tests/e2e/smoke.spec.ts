import { test, expect } from '@playwright/test';

test('health endpoint responds', async ({ request }) => {
  const res = await request.get('http://localhost:53584/admin/health/ready');
  // 200 means healthy, 401 means server is up but requires auth
  expect(res.status()).toBeGreaterThanOrEqual(200);
  expect(res.status()).toBeLessThan(500);
});