# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: tenant.spec.ts >> Tenant - Lifecycle and Membership >> should set owner
- Location: tenant.spec.ts:152:3

# Error details

```
Error: expect(received).toBeTruthy()

Received: false
```

# Test source

```ts
  54  |     const createData = await createRes.json();
  55  |     const tenantId = createData.id;
  56  | 
  57  |     const updateRes = await client.updateTenant(tenantId, { name: 'Updated Tenant Name' });
  58  |     expect(updateRes.ok()).toBeTruthy();
  59  |     const updated = await updateRes.json();
  60  |     expect(updated.name).toBe('Updated Tenant Name');
  61  |   });
  62  | 
  63  |   test('should suspend and restore tenant', async () => {
  64  |     const createRes = await client.createTenant('Suspendable Tenant', 'suspendable-tenant', true);
  65  |     expect(createRes.ok()).toBeTruthy();
  66  |     const createData = await createRes.json();
  67  |     const tenantId = createData.id;
  68  | 
  69  |     const suspendRes = await client.suspendTenant(tenantId, 'Testing suspension');
  70  |     expect(suspendRes.ok()).toBeTruthy();
  71  | 
  72  |     const restoreRes = await client.restoreTenant(tenantId);
  73  |     expect(restoreRes.ok()).toBeTruthy();
  74  |   });
  75  | 
  76  |   test('should archive tenant', async () => {
  77  |     const createRes = await client.createTenant('Archivable Tenant', 'archivable-tenant', true);
  78  |     expect(createRes.ok()).toBeTruthy();
  79  |     const createData = await createRes.json();
  80  |     const tenantId = createData.id;
  81  | 
  82  |     const archiveRes = await client.archiveTenant(tenantId, 'Testing archive');
  83  |     expect(archiveRes.ok()).toBeTruthy();
  84  |   });
  85  | 
  86  |   test('should list my tenants', async () => {
  87  |     const res = await client.listMyTenants();
  88  |     expect(res.ok()).toBeTruthy();
  89  |     const data = await res.json();
  90  |     expect(Array.isArray(data)).toBeTruthy();
  91  |   });
  92  | 
  93  |   test('should get tenant settings', async () => {
  94  |     const listRes = await client.listTenants();
  95  |     const data = await listRes.json();
  96  |     if (data.items.length === 0) {
  97  |       test.skip();
  98  |       return;
  99  |     }
  100 |     const tenantId = data.items[0].id;
  101 |     const res = await client.get(`/tenants/${tenantId}/settings`);
  102 |     expect(res.ok()).toBeTruthy();
  103 |   });
  104 | 
  105 |   test('should create tenant with owner', async () => {
  106 |     const userRes = await client.createUser(
  107 |       'tenant-owner-test@example.com',
  108 |       'OwnerTestPass123!',
  109 |       '+15551000060',
  110 |       'Tenant Owner Test',
  111 |       true
  112 |     );
  113 |     expect(userRes.ok()).toBeTruthy();
  114 |     const userData = await userRes.json();
  115 |     const ownerId = userData.id;
  116 | 
  117 |     const tenantRes = await client.createTenant('Owner Tenant', 'owner-tenant', true, ownerId);
  118 |     expect(tenantRes.ok()).toBeTruthy();
  119 |     const tenantData = await tenantRes.json();
  120 |     expect(tenantData.id).toBeTruthy();
  121 |   });
  122 | 
  123 |   test('should add and remove member', async () => {
  124 |     const createRes = await client.createTenant('Member Test Tenant', 'member-test-tenant', true);
  125 |     expect(createRes.ok()).toBeTruthy();
  126 |     const tenantData = await createRes.json();
  127 |     const tenantId = tenantData.id;
  128 | 
  129 |     const userRes = await client.createUser(
  130 |       'member-test@example.com',
  131 |       'MemberTestPass123!',
  132 |       '+15551000070',
  133 |       'Member Test User',
  134 |       true
  135 |     );
  136 |     expect(userRes.ok()).toBeTruthy();
  137 |     const userData = await userRes.json();
  138 |     const userId = userData.id;
  139 | 
  140 |     const addRes = await client.addMember(tenantId, userId, false);
  141 |     expect(addRes.ok()).toBeTruthy();
  142 | 
  143 |     const listRes = await client.listMembers(tenantId);
  144 |     expect(listRes.ok()).toBeTruthy();
  145 |     const membersData = await listRes.json();
  146 |     expect(membersData.some((m: any) => m.userId === userId)).toBeTruthy();
  147 | 
  148 |     const removeRes = await client.removeMember(tenantId, userId);
  149 |     expect(removeRes.ok()).toBeTruthy();
  150 |   });
  151 | 
  152 |   test('should set owner', async () => {
  153 |     const createRes = await client.createTenant('Owner Test Tenant', 'owner-test-tenant', true);
> 154 |     expect(createRes.ok()).toBeTruthy();
      |                            ^ Error: expect(received).toBeTruthy()
  155 |     const tenantData = await createRes.json();
  156 |     const tenantId = tenantData.id;
  157 | 
  158 |     const userRes = await client.createUser(
  159 |       'new-owner-test@example.com',
  160 |       'NewOwnerTestPass123!',
  161 |       '+15551000080',
  162 |       'New Owner Test',
  163 |       true
  164 |     );
  165 |     expect(userRes.ok()).toBeTruthy();
  166 |     const userData = await userRes.json();
  167 |     const userId = userData.id;
  168 | 
  169 |     await client.addMember(tenantId, userId, false);
  170 | 
  171 |     const setOwnerRes = await client.setOwner(tenantId, userId, true);
  172 |     expect(setOwnerRes.ok()).toBeTruthy();
  173 |   });
  174 | 
  175 |   test('should get tenant audit log', async () => {
  176 |     const listRes = await client.listTenants();
  177 |     const data = await listRes.json();
  178 |     if (data.items.length === 0) {
  179 |       test.skip();
  180 |       return;
  181 |     }
  182 |     const tenantId = data.items[0].id;
  183 |     const res = await client.getAuditLog(tenantId);
  184 |     expect(res.ok()).toBeTruthy();
  185 |     const auditData = await res.json();
  186 |     expect(Array.isArray(auditData.items)).toBeTruthy();
  187 |   });
  188 | });
```