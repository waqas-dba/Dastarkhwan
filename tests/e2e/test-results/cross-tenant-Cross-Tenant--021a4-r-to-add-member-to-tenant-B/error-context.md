# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: cross-tenant.spec.ts >> Cross-Tenant Security >> should not allow tenant A user to add member to tenant B
- Location: cross-tenant.spec.ts:87:3

# Error details

```
Error: expect(received).toBeTruthy()

Received: false
```

# Test source

```ts
  1   | import { test, expect } from '@playwright/test';
  2   | import { ApiClient } from './helpers/apiClient';
  3   | 
  4   | const BASE_URL = 'http://localhost:53584';
  5   | 
  6   | test.describe('Cross-Tenant Security', () => {
  7   |   let clientA: ApiClient;
  8   |   let clientB: ApiClient;
  9   |   let adminClient: ApiClient;
  10  |   let tenantAId: string;
  11  |   let tenantBId: string;
  12  |   let userAId: string;
  13  |   let userBId: string;
  14  | 
  15  |   test.beforeEach(async ({ request }) => {
  16  |     clientA = new ApiClient(request, BASE_URL);
  17  |     clientB = new ApiClient(request, BASE_URL);
  18  |     adminClient = new ApiClient(request, BASE_URL);
  19  | 
  20  |     // Login as testuser
  21  |     try {
  22  |       await adminClient.login('testuser@example.com', 'TestPass123!');
  23  |     } catch {
  24  |       const auditRes = await adminClient.login('testuser@example.com', 'TestPass123!');
  25  |       if (!auditRes.accessToken) {
  26  |         throw new Error('Cannot login as admin');
  27  |       }
  28  |     }
  29  | 
  30  |     // Create tenant A
  31  |     const tenantARes = await adminClient.createTenant('Tenant A', 'tenant-a-cross', true);
> 32  |     expect(tenantARes.ok()).toBeTruthy();
      |                             ^ Error: expect(received).toBeTruthy()
  33  |     const tenantAData = await tenantARes.json();
  34  |     tenantAId = tenantAData.id;
  35  | 
  36  |     // Create tenant B
  37  |     const tenantBRes = await adminClient.createTenant('Tenant B', 'tenant-b-cross', true);
  38  |     expect(tenantBRes.ok()).toBeTruthy();
  39  |     const tenantBData = await tenantBRes.json();
  40  |     tenantBId = tenantBData.id;
  41  | 
  42  |     // Create user A and add to tenant A
  43  |     const userARes = await adminClient.createUser(
  44  |       'cross-tenant-a@example.com',
  45  |       'TestPass123!',
  46  |       '+15551000090',
  47  |       'User A',
  48  |       true
  49  |     );
  50  |     expect(userARes.ok()).toBeTruthy();
  51  |     const userAData = await userARes.json();
  52  |     userAId = userAData.id;
  53  | 
  54  |     await adminClient.addMember(tenantAId, userAId, true);
  55  | 
  56  |     // Create user B and add to tenant B
  57  |     const userBRes = await adminClient.createUser(
  58  |       'cross-tenant-b@example.com',
  59  |       'TestPass123!',
  60  |       '+15551000091',
  61  |       'User B',
  62  |       true
  63  |     );
  64  |     expect(userBRes.ok()).toBeTruthy();
  65  |     const userBData = await userBRes.json();
  66  |     userBId = userBData.id;
  67  | 
  68  |     await adminClient.addMember(tenantBId, userBId, true);
  69  | 
  70  |     // Login as tenant A user
  71  |     await clientA.login('cross-tenant-a@example.com', 'TestPass123!');
  72  | 
  73  |     // Login as tenant B user
  74  |     await clientB.login('cross-tenant-b@example.com', 'TestPass123!');
  75  |   });
  76  | 
  77  |   test('should not allow tenant A user to access tenant B data', async () => {
  78  |     const res = await clientA.getTenant(tenantBId);
  79  |     expect(res.status()).toBeIn([403, 404]);
  80  |   });
  81  | 
  82  |   test('should not allow tenant A user to list tenant B members', async () => {
  83  |     const res = await clientA.listMembers(tenantBId);
  84  |     expect(res.status()).toBeIn([403, 404]);
  85  |   });
  86  | 
  87  |   test('should not allow tenant A user to add member to tenant B', async () => {
  88  |     const res = await clientA.addMember(tenantBId, userAId, false);
  89  |     expect(res.status()).toBeIn([403, 404]);
  90  |   });
  91  | 
  92  |   test('should not allow tenant A user to remove member from tenant B', async () => {
  93  |     const res = await clientA.removeMember(tenantBId, userBId);
  94  |     expect(res.status()).toBeIn([403, 404]);
  95  |   });
  96  | 
  97  |   test('should not allow tenant A user to set owner on tenant B', async () => {
  98  |     const res = await clientA.setOwner(tenantBId, userBId, true);
  99  |     expect(res.status()).toBeIn([403, 404]);
  100 |   });
  101 | 
  102 |   test('should not allow tenant A user to get tenant B audit log', async () => {
  103 |     const res = await clientA.getAuditLog(tenantBId);
  104 |     expect(res.status()).toBeIn([403, 404]);
  105 |   });
  106 | 
  107 |   test('should not allow tenant A user to update tenant B', async () => {
  108 |     const res = await clientA.updateTenant(tenantBId, { name: 'Hacked Tenant' });
  109 |     expect(res.status()).toBeIn([403, 404]);
  110 |   });
  111 | 
  112 |   test('should not allow tenant A user to delete tenant B', async () => {
  113 |     const res = await clientA.deleteTenant(tenantBId);
  114 |     expect(res.status()).toBeIn([403, 404]);
  115 |   });
  116 | 
  117 |   test('should not allow cross-tenant membership manipulation', async () => {
  118 |     const res = await clientA.addMember(tenantBId, userAId, false);
  119 |     expect(res.status()).toBeIn([403, 404]);
  120 |   });
  121 | 
  122 |   test('should allow tenant A user to access their own tenant data', async () => {
  123 |     const res = await clientA.getTenant(tenantAId);
  124 |     expect(res.ok()).toBeTruthy();
  125 |     const data = await res.json();
  126 |     expect(data.id).toBe(tenantAId);
  127 |   });
  128 | 
  129 |   test('should allow tenant A user to list their own tenant members', async () => {
  130 |     const res = await clientA.listMembers(tenantAId);
  131 |     expect(res.ok()).toBeTruthy();
  132 |   });
```