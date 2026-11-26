import { test, expect } from '@playwright/test';

test('debug: check server connectivity', async ({ request }) => {
  const res = await request.get('http://localhost:53584/admin/health/ready');
  console.log('Status:', res.status());
  console.log('Body:', await res.text());
});

test('debug: try login endpoint', async ({ request }) => {
  const res = await request.post('http://localhost:53584/Identity/auth/login', {
    data: { email: 'testuser@example.com', password: 'TestPass123!' },
    headers: { 'Content-Type': 'application/json' },
  });
  console.log('Login status:', res.status());
  console.log('Login body:', await res.text());
});