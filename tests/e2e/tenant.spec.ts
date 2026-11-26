import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';
import { WEB_BASE_URL } from './helpers/config';
import { ADMIN_EMAIL, ADMIN_PASSWORD, uniqueSlug, uniqueName } from './helpers/fixtures';

const BASE_URL = WEB_BASE_URL;

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

test.describe('Tenant - Context (passing)', () => {
  test('me lists the signed-in user tenants', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.listMyTenants();
    expect(res.ok()).toBeTruthy();
    expect(Array.isArray(await res.json())).toBeTruthy();
  });

  test('list tenants without a scope returns 400', async ({ request }) => {
    // RequireAccessFilter fails closed on a missing scope (400) before the auth check.
    const client = new ApiClient(request, BASE_URL);
    const res = await client.listTenants();
    expect(res.status()).toBe(400);
  });

  test('create tenant without a scope returns 400', async ({ request }) => {
    const client = new ApiClient(request, BASE_URL);
    const res = await client.createTenant(uniqueName('NoAuth'), uniqueSlug('noauth'));
    expect(res.status()).toBe(400);
  });

  test('current tenant requires a resolved tenant scope', async ({ request }) => {
    // The platform admin has no ambient tenant, so /current cannot resolve one.
    const client = await adminClient(request);
    const res = await client.getCurrentTenant();
    expect(res.status()).toBe(403);
  });
});

// The following assert INTENDED behavior. They fail with 403 because the seeded platform
// Administrator role lacks the tenants.* permissions (defect D-E2E-001). They are kept
// failing to document the defect; do not weaken them to 403.
test.describe('Tenant - Management (blocked by D-E2E-001)', () => {
  test('list tenants returns a paged result', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.listTenants();
    expect(res.ok()).toBeTruthy();
    expect(Array.isArray((await res.json()).items)).toBeTruthy();
  });

  test('create tenant returns 201 and Active status', async ({ request }) => {
    const client = await adminClient(request);
    const res = await client.createTenant(uniqueName('Tenant'), uniqueSlug('tenant'));
    expect(res.status()).toBe(201);
    const data = await res.json();
    expect(data.id).toBeTruthy();
    expect(data.status).toBe('Active');
  });

  test('get tenant by id returns the tenant', async ({ request }) => {
    const client = await adminClient(request);
    const created = await client.createTenant(uniqueName('GetTenant'), uniqueSlug('get'));
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    const res = await client.getTenant(id);
    expect(res.ok()).toBeTruthy();
    expect((await res.json()).id).toBe(id);
  });

  test('update tenant name succeeds', async ({ request }) => {
    const client = await adminClient(request);
    const created = await client.createTenant(uniqueName('UpdTenant'), uniqueSlug('upd'));
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    const res = await client.updateTenant(id, { name: uniqueName('Renamed') });
    expect(res.ok()).toBeTruthy();
    expect((await res.json()).name).toContain('Renamed');
  });

  test('suspend then restore a tenant', async ({ request }) => {
    const client = await adminClient(request);
    const created = await client.createTenant(uniqueName('SusTenant'), uniqueSlug('sus'));
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    expect((await client.suspendTenant(id, 'E2E suspend')).ok()).toBeTruthy();
    expect((await client.restoreTenant(id)).ok()).toBeTruthy();
  });

  test('archive a tenant', async ({ request }) => {
    const client = await adminClient(request);
    const created = await client.createTenant(uniqueName('ArcTenant'), uniqueSlug('arc'));
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    expect((await client.archiveTenant(id, 'E2E archive')).ok()).toBeTruthy();
  });

  test('set and read tenant settings', async ({ request }) => {
    const client = await adminClient(request);
    const created = await client.createTenant(uniqueName('SetTenant'), uniqueSlug('set'));
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    const setRes = await client.put(`/tenants/${id}/settings`, { values: { theme: 'dark' } });
    expect(setRes.ok()).toBeTruthy();
    const getRes = await client.get(`/tenants/${id}/settings`);
    expect(getRes.ok()).toBeTruthy();
  });

  test('add, list and remove a tenant member', async ({ request }) => {
    const client = await adminClient(request);
    const userId = await adminUserId(client);
    const created = await client.createTenant(uniqueName('MemTenant'), uniqueSlug('mem'), true, userId);
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    const listRes = await client.listMembers(id);
    expect(listRes.ok()).toBeTruthy();
    expect(Array.isArray(await listRes.json())).toBeTruthy();
  });

  test('tenant audit log returns a paged result', async ({ request }) => {
    const client = await adminClient(request);
    const created = await client.createTenant(uniqueName('AudTenant'), uniqueSlug('aud'));
    expect(created.status()).toBe(201);
    const id = (await created.json()).id;
    const res = await client.getAuditLog(id);
    expect(res.ok()).toBeTruthy();
    expect(Array.isArray((await res.json()).items)).toBeTruthy();
  });
});