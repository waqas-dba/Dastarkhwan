# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: identity.spec.ts >> Identity API >> me without token returns 401
- Location: identity.spec.ts:11:3

# Error details

```
Error: apiRequestContext.get: socket hang up (ECONNRESET)
Call log:
  - → GET http://localhost:53584/Identity/auth/me
    - user-agent: Playwright/1.65.1 (x64; windows 10.0) node/24.14
    - accept: application/json
    - accept-encoding: gzip,deflate,br

```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | 
  3  | test.describe('Identity API', () => {
  4  |   test('login invalid credentials returns 401', async ({ request }) => {
  5  |     const res = await request.post('/Identity/auth/login', {
  6  |       data: { email: 'nonexistent@example.com', password: 'wrong' },
  7  |     });
  8  |     expect(res.status()).toBe(401);
  9  |   });
  10 | 
  11 |   test('me without token returns 401', async ({ request }) => {
> 12 |     const res = await request.get('/Identity/auth/me');
     |                               ^ Error: apiRequestContext.get: socket hang up (ECONNRESET)
  13 |     expect(res.status()).toBe(401);
  14 |   });
  15 | });
  16 | 
```