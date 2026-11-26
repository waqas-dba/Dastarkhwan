import { test, expect } from '@playwright/test';

test.describe('Identity API', () => {
  test('login invalid credentials returns 401', async ({ request }) => {
    const res = await request.post('/Identity/auth/login', {
      data: { email: 'nonexistent@example.com', password: 'wrong' },
    });
    expect(res.status()).toBe(401);
  });

  test('me without token returns 401', async ({ request }) => {
    const res = await request.get('/Identity/auth/me');
    expect(res.status()).toBe(401);
  });
});
