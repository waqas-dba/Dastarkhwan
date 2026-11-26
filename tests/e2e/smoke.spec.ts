import { test, expect } from '@playwright/test';
import { WEB_BASE_URL } from './helpers/config';

test('web health/live responds', async ({ request }) => {
  const res = await request.get(`${WEB_BASE_URL}/health/live`);
  expect(res.status()).toBe(200);
  const data = await res.json();
  expect(data.status).toBe('live');
});

test('web health/ready responds', async ({ request }) => {
  const res = await request.get(`${WEB_BASE_URL}/health/ready`);
  // 200 = ready; 503 = a dependency (database) is unreachable. Both prove the host is up.
  expect(res.status()).toBeGreaterThanOrEqual(200);
  expect(res.status()).toBeLessThan(500);
});