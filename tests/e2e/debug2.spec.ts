import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';

test('debug: test ApiClient login', async ({ request }) => {
  const client = new ApiClient(request, 'http://localhost:53584');
  const res = await client.login('testuser@example.com', 'TestPass123!');
  console.log('Login status:', res.accessToken ? 'SUCCESS' : 'FAILED');
  expect(res.accessToken).toBeTruthy();
});