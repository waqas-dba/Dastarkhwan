import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';
import { WEB_BASE_URL } from './helpers/config';
import { ADMIN_EMAIL, ADMIN_PASSWORD, uniqueName } from './helpers/fixtures';

const BASE_URL = WEB_URL();

function WEB_URL() {
  return WEB_BASE_URL;
}

// Permissions the seeded platform Administrator actually holds (and that are in the catalog).
const HELD_PERMISSION = 'access.roles.read';
// A permission that is in the catalog but NOT held by the admin (escalation guard).
const UNHELD_PERMISSION = 'identity.users.create';

async function adminClient(request: any): Promise<ApiClient> {
  const client = new ApiClient(request, BASE_URL);
  await client.login(ADMIN_EMAIL, ADMIN_PASSWORD);
  return client;
}

async function adminUserId(client: ApiClient): Promise<string> {
  const res = await client.getCurrentUser();
  expect(res.ok()).toBeTruthy();
  const me = await res.json();
  expect(me.id).toBeTruthy();
  return me.id;
}

test.describe('Access - Context and catalog', () => {
  test('me returns the signed-in access context', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.getMyAccess();
    expect(res.ok()).toBeTruthy();
  });

  test('me without token returns 401', async ({ request }) => {
    const client = new ApiClient(request, BASE_URL);
    const res = await client.getMyAccess();
    expect(res.status()).toBe(401);
  });

  test('permissions lists the declared catalog', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.listPermissions();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data)).toBeTruthy();
    expect(data.length).toBeGreaterThan(0);
  });

  test('roles list includes the Administrator role', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.listRoles();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data)).toBeTruthy();
    expect(data.some((r: any) => r.name === 'Administrator')).toBeTruthy();
  });
});

test.describe('Access - Role lifecycle', () => {
  test('create, get, update, set-permissions and delete a role', async ({ request }) => {
    const client = await adminClient(request);
    const name = uniqueName('role');

    const createRes = await client.createRole(name, 'E2E role', [HELD_PERMISSION]);
    expect(createRes.status()).toBe(201);
    const created = await createRes.json();
    const id = created.id;
    expect(id).toBeTruthy();
    expect(created.name).toBe(name);
    expect(created.concurrencyStamp).toBeTruthy();

    const getRes = await client.getRole(id);
    expect(getRes.ok()).toBeTruthy();
    expect((await getRes.json()).id).toBe(id);

    const updateRes = await client.updateRole(id, {
      name: uniqueName('role-renamed'),
      description: 'updated',
      concurrencyStamp: created.concurrencyStamp,
    });
    expect(updateRes.ok()).toBeTruthy();
    const updated = await updateRes.json();

    const permRes = await client.setRolePermissions(id, [HELD_PERMISSION], updated.concurrencyStamp);
    expect(permRes.ok()).toBeTruthy();

    const delRes = await client.deleteRole(id);
    expect(delRes.ok()).toBeTruthy();

    const getAfter = await client.getRole(id);
    expect(getAfter.status()).toBe(404);
  });

  test('duplicate role name returns 409', async ({ request }) => {
    const client = await adminClient(request);
    const name = uniqueName('dup-role');
    const first = await client.createRole(name, 'first', []);
    expect(first.status()).toBe(201);
    const firstId = (await first.json()).id;

    const dup = await client.createRole(name, 'second', []);
    expect(dup.status()).toBe(409);

    await client.deleteRole(firstId);
  });

  test('empty role name returns 400', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.createRole('   ', 'blank name', []);
    expect(res.status()).toBe(400);
  });

  test('unknown permission returns 400', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.createRole(uniqueName('bad-perm'), 'bad', ['not.a.real.permission']);
    expect(res.status()).toBe(400);
  });

  test('granting a permission the admin does not hold is rejected (escalation)', async ({ request }) => {
    const client = await adminClient(request);
    // identity.users.create is in the catalog but not held by the admin -> escalation guard.
    const res = await client.createRole(uniqueName('escalate'), 'escalation', [UNHELD_PERMISSION]);
    expect(res.status()).toBe(403);
  });

  test('update with a stale concurrency stamp returns 409', async ({ request }) => {
    const client = await adminClient(request);
    const createRes = await client.createRole(uniqueName('stale'), 'stale', []);
    expect(createRes.status()).toBe(201);
    const created = await createRes.json();
    const id = created.id;

    const res = await client.updateRole(id, {
      name: uniqueName('stale-renamed'),
      concurrencyStamp: '11111111-1111-1111-1111-111111111111',
    });
    expect(res.status()).toBe(409);

    await client.deleteRole(id);
  });

  test('get unknown role returns 404', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.getRole('00000000-0000-0000-0000-0000000000ff');
    expect(res.status()).toBe(404);
  });

  test('list roles without token returns 401', async ({ request }) => {
    const client = new ApiClient(request, BASE_URL);
    const res = await client.listRoles();
    expect(res.status()).toBe(401);
  });
});

test.describe('Access - Assignments', () => {
  test('read the admin user roles', async ({ request }) => {
    const client = await adminClient(request);
    const userId = await adminUserId(client);
    const res = await client.getUserRoles(userId);
    expect(res.ok()).toBeTruthy();
  });

  test('assigning roles to a non-member user returns 400', async ({ request }) => {
    const client = await adminClient(request);
    // A random user id is not a platform member -> validation failure, and it does not mutate the admin.
    const res = await client.assignUserRoles('00000000-0000-0000-0000-0000000000fe', []);
    expect(res.status()).toBe(400);
  });

  test('get user roles without token returns 401', async ({ request }) => {
    const client = new ApiClient(request, BASE_URL);
    const res = await client.getUserRoles('00000000-0000-0000-0000-0000000000fd');
    expect(res.status()).toBe(401);
  });
});

test.describe('Access - Platform membership', () => {
  test('admin is a platform member', async ({ request }) => {
    const client = await adminClient(request);
    const userId = await adminUserId(client);
    const res = await client.getPlatformMember(userId);
    expect(res.ok()).toBeTruthy();
    expect((await res.json()).isMember).toBe(true);
  });

  test('adding the admin as a member is idempotent (no error)', async ({ request }) => {
    const client = await adminClient(request);
    const userId = await adminUserId(client);
    const res = await client.addPlatformMember(userId);
    // Already a member: the service treats re-add as success/no-op.
    expect([200, 201, 409]).toContain(res.status());
  });

  test('platform membership check without token returns 401', async ({ request }) => {
    const client = new ApiClient(request, BASE_URL);
    const res = await client.getPlatformMember('00000000-0000-0000-0000-0000000000fc');
    expect(res.status()).toBe(401);
  });
});

test.describe('Access - Audit', () => {
  test('audit log returns a paged result', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.getAccessAudit();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data.items)).toBeTruthy();
  });

  test('audit log without token returns 401', async ({ request }) => {
    const client = new ApiClient(request, BASE_URL);
    const res = await client.getAccessAudit();
    expect(res.status()).toBe(401);
  });
});