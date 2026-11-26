# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: admin.spec.ts >> Admin - Platform Administration >> should add platform member
- Location: admin.spec.ts:34:3

# Error details

```
Error: expect(received).toBeTruthy()

Received: false
```

# Test source

```ts
  1  | import { test, expect } from '@playwright/test';
  2  | import { ApiClient } from './helpers/apiClient';
  3  | 
  4  | const BASE_URL = 'http://localhost:53584';
  5  | 
  6  | test.describe('Admin - Platform Administration', () => {
  7  |   let client: ApiClient;
  8  | 
  9  |   test.beforeEach(async ({ request }) => {
  10 |     client = new ApiClient(request, BASE_URL);
  11 |     try {
  12 |       await client.login('testuser@example.com', 'TestPass123!');
  13 |     } catch {
  14 |       const auditRes = await client.login('testuser@example.com', 'TestPass123!');
  15 |       if (!auditRes.accessToken) {
  16 |         throw new Error('Cannot login as admin');
  17 |       }
  18 |     }
  19 |   });
  20 | 
  21 |   test('should access admin health endpoint', async () => {
  22 |     const res = await client.adminHealthReady();
  23 |     expect(res.ok()).toBeTruthy();
  24 |     const data = await res.json();
  25 |     expect(data.status).toBe('ready');
  26 |   });
  27 | 
  28 |   test('should reject unauthenticated admin access', async ({ request }) => {
  29 |     const unauthClient = new ApiClient(request, BASE_URL);
  30 |     const res = await unauthClient.adminHealthReady();
  31 |     expect(res.status()).toBe(401);
  32 |   });
  33 | 
  34 |   test('should add platform member', async () => {
  35 |     const userRes = await client.createUser(
  36 |       'platform-member-test@example.com',
  37 |       'PlatformMemberPass123!',
  38 |       '+15551000090',
  39 |       'Platform Member Test',
  40 |       true
  41 |     );
> 42 |     expect(userRes.ok()).toBeTruthy();
     |                          ^ Error: expect(received).toBeTruthy()
  43 |     const userData = await userRes.json();
  44 |     const userId = userData.id;
  45 | 
  46 |     const res = await client.addPlatformMemberAdmin(userId);
  47 |     expect(res.ok()).toBeTruthy();
  48 |   });
  49 | 
  50 |   test('should reject non-admin platform member addition', async ({ request }) => {
  51 |     const staffClient = new ApiClient(request, BASE_URL);
  52 |     await staffClient.login('staff-a@example.com', 'TestPass123!');
  53 |     const res = await staffClient.addPlatformMemberAdmin('00000000-0000-0000-0000-000000000000');
  54 |     expect(res.status()).toBe(403);
  55 |   });
  56 | });
```