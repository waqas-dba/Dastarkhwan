import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';

test('debug: test ApiClient login with JSON body', async ({ request }) => {
  const client = new ApiClient(request, 'http://localhost:53584');
  // Try posting with JSON directly
  const res = await request.post('http://localhost:53584/Identity/auth/login', {
    data: { email: 'testuser@example.com', password: 'TestPass123!' },
  });
  console.log('Status:', res.status());
  console.log('Body:', await res.text());
  if (res.ok()) {
    const data = await res.json();
    console.log('Has accessToken:', !!data.accessToken);
  }
});