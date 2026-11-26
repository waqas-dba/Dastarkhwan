# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: access.spec.ts >> Access - Roles and Permissions >> should assign user roles
- Location: access.spec.ts:107:3

# Error details

```
TypeError: Cannot read properties of undefined (reading 'length')
```

# Test source

```ts
  10  |     client = new ApiClient(request, BASE_URL);
  11  |     try {
  12  |       await client.login('testuser@example.com', 'TestPass123!');
  13  |     } catch {
  14  |       const auditRes = await client.login('testuser@example.com', 'TestPass123!');
  15  |       if (!auditRes.accessToken) {
  16  |         throw new Error('Cannot login as admin');
  17  |       }
  18  |     }
  19  |   });
  20  | 
  21  |   test('should list permissions', async () => {
  22  |     const res = await client.listPermissions();
  23  |     expect(res.ok()).toBeTruthy();
  24  |     const data = await res.json();
  25  |     expect(Array.isArray(data)).toBeTruthy();
  26  |     expect(data.length).toBeGreaterThan(0);
  27  |   });
  28  | 
  29  |   test('should list roles', async () => {
  30  |     const res = await client.listRoles();
  31  |     expect(res.ok()).toBeTruthy();
  32  |     const data = await res.json();
  33  |     expect(Array.isArray(data)).toBeTruthy();
  34  |   });
  35  | 
  36  |   test('should create a role', async () => {
  37  |     const res = await client.createRole('Test Role', 'A test role', ['tenants.read', 'tenants.settings.read']);
  38  |     expect(res.ok()).toBeTruthy();
  39  |     const data = await res.json();
  40  |     expect(data.id).toBeTruthy();
  41  |     expect(data.name).toBe('Test Role');
  42  |   });
  43  | 
  44  |   test('should get role by id', async () => {
  45  |     const listRes = await client.listRoles();
  46  |     const data = await listRes.json();
  47  |     if (data.length === 0) {
  48  |       test.skip();
  49  |       return;
  50  |     }
  51  |     const roleId = data[0].id;
  52  |     const res = await client.get(`/access/roles/${roleId}`);
  53  |     expect(res.ok()).toBeTruthy();
  54  |     const roleData = await res.json();
  55  |     expect(roleData.id).toBe(roleId);
  56  |   });
  57  | 
  58  |   test('should update role', async () => {
  59  |     const createRes = await client.createRole('Updatable Role', 'To be updated', ['tenants.read']);
  60  |     expect(createRes.ok()).toBeTruthy();
  61  |     const createData = await createRes.json();
  62  |     const roleId = createData.id;
  63  | 
  64  |     const updateRes = await client.put(`/access/roles/${roleId}`, {
  65  |       name: 'Updated Role',
  66  |       description: 'Updated description',
  67  |       concurrencyStamp: createData.concurrencyStamp,
  68  |     });
  69  |     expect(updateRes.ok()).toBeTruthy();
  70  |   });
  71  | 
  72  |   test('should set role permissions', async () => {
  73  |     const createRes = await client.createRole('Perm Role', 'For permissions test', ['tenants.read']);
  74  |     expect(createRes.ok()).toBeTruthy();
  75  |     const createData = await createRes.json();
  76  |     const roleId = createData.id;
  77  | 
  78  |     const permRes = await client.put(`/access/roles/${roleId}/permissions`, {
  79  |       permissions: ['tenants.read', 'tenants.settings.read'],
  80  |       concurrencyStamp: createData.concurrencyStamp,
  81  |     });
  82  |     expect(permRes.ok()).toBeTruthy();
  83  |   });
  84  | 
  85  |   test('should delete role', async () => {
  86  |     const createRes = await client.createRole('Deletable Role', 'To be deleted', []);
  87  |     expect(createRes.ok()).toBeTruthy();
  88  |     const createData = await createRes.json();
  89  |     const roleId = createData.id;
  90  | 
  91  |     const deleteRes = await client.delete(`/access/roles/${roleId}`);
  92  |     expect(deleteRes.ok()).toBeTruthy();
  93  |   });
  94  | 
  95  |   test('should get user roles', async () => {
  96  |     const listRes = await client.get('/Identity/users');
  97  |     const data = await listRes.json();
  98  |     if (data.items.length === 0) {
  99  |       test.skip();
  100 |       return;
  101 |     }
  102 |     const userId = data.items[0].id;
  103 |     const res = await client.get(`/access/users/${userId}/roles`);
  104 |     expect(res.ok()).toBeTruthy();
  105 |   });
  106 | 
  107 |   test('should assign user roles', async () => {
  108 |     const listRes = await client.get('/Identity/users');
  109 |     const data = await listRes.json();
> 110 |     if (data.items.length === 0) {
      |                    ^ TypeError: Cannot read properties of undefined (reading 'length')
  111 |       test.skip();
  112 |       return;
  113 |     }
  114 |     const userId = data.items[0].id;
  115 |     const rolesRes = await client.listRoles();
  116 |     const rolesData = await rolesRes.json();
  117 |     if (rolesData.length === 0) {
  118 |       test.skip();
  119 |       return;
  120 |     }
  121 |     const roleId = rolesData[0].id;
  122 | 
  123 |     const assignRes = await client.assignUserRoles(userId, [roleId]);
  124 |     expect(assignRes.ok()).toBeTruthy();
  125 |   });
  126 | 
  127 |   test('should add platform member', async () => {
  128 |     const listRes = await client.get('/Identity/users');
  129 |     const data = await listRes.json();
  130 |     if (data.items.length === 0) {
  131 |       test.skip();
  132 |       return;
  133 |     }
  134 |     const userId = data.items[0].id;
  135 |     const res = await client.addPlatformMember(userId);
  136 |     expect(res.ok()).toBeTruthy();
  137 |   });
  138 | 
  139 |   test('should check platform membership', async () => {
  140 |     const listRes = await client.get('/Identity/users');
  141 |     const data = await listRes.json();
  142 |     if (data.items.length === 0) {
  143 |       test.skip();
  144 |       return;
  145 |     }
  146 |     const userId = data.items[0].id;
  147 |     const res = await client.get(`/access/platform/members/${userId}`);
  148 |     expect(res.ok()).toBeTruthy();
  149 |   });
  150 | 
  151 |   test('should revoke platform member', async () => {
  152 |     const listRes = await client.get('/Identity/users');
  153 |     const data = await listRes.json();
  154 |     if (data.items.length === 0) {
  155 |       test.skip();
  156 |       return;
  157 |     }
  158 |     const userId = data.items[0].id;
  159 |     await client.addPlatformMember(userId);
  160 |     const res = await client.revokePlatformMember(userId);
  161 |     expect(res.ok()).toBeTruthy();
  162 |   });
  163 | 
  164 |   test('should get audit log', async () => {
  165 |     const res = await client.get('/access/audit');
  166 |     expect(res.ok()).toBeTruthy();
  167 |     const data = await res.json();
  168 |     expect(Array.isArray(data.items)).toBeTruthy();
  169 |   });
  170 | });
```