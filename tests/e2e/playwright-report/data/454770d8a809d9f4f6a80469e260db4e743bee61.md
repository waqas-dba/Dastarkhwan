# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: smoke.spec.ts >> health endpoint responds
- Location: smoke.spec.ts:3:1

# Error details

```
Error: apiRequestContext.get: socket hang up (ECONNRESET)
Call log:
  - → GET http://localhost:53584/health/live
    - user-agent: Playwright/1.65.1 (x64; windows 10.0) node/24.14
    - accept: application/json
    - accept-encoding: gzip,deflate,br

```

# Test source

```ts
  1 | import { test, expect } from '@playwright/test';
  2 | 
  3 | test('health endpoint responds', async ({ request }) => {
> 4 |   const res = await request.get('/health/live');
    |                             ^ Error: apiRequestContext.get: socket hang up (ECONNRESET)
  5 |   expect(res.ok()).toBeTruthy();
  6 | });
  7 | 
```