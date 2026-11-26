import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';

const BASE_URL = 'http://localhost:53584';

test.describe('Tenant - Lifecycle and Membership', () => {
  let client: ApiClient;

  test.beforeEach(async ({ request }) => {
    client = new ApiClient(request, BASE_URL);
    try {
      await client.login('testuser@example.com', 'TestPass123!');
    } catch {
      const auditRes = await client.login('testuser@example.com', 'TestPass123!');
      if (!auditRes.accessToken) {
        throw new Error('Cannot login as admin');
      }
    }
  });

  test('should list tenants', async () => {
    const res = await client.listTenants();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data.items)).toBeTruthy();
  });

  test('should create a tenant', async () => {
    const res = await client.createTenant('E2E Test Tenant', 'e2e-test-tenant', true);
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data.id).toBeTruthy();
    expect(data.name).toBe('E2E Test Tenant');
    expect(data.status).toBe('Active');
  });

  test('should get tenant by id', async () => {
    const listRes = await client.listTenants();
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const tenantId = data.items[0].id;
    const res = await client.getTenant(tenantId);
    expect(res.ok()).toBeTruthy();
    const tenantData = await res.json();
    expect(tenantData.id).toBe(tenantId);
  });

  test('should update tenant name', async () => {
    const createRes = await client.createTenant('Updatable Tenant', 'updatable-tenant', true);
    expect(createRes.ok()).toBeTruthy();
    const createData = await createRes.json();
    const tenantId = createData.id;

    const updateRes = await client.updateTenant(tenantId, { name: 'Updated Tenant Name' });
    expect(updateRes.ok()).toBeTruthy();
    const updated = await updateRes.json();
    expect(updated.name).toBe('Updated Tenant Name');
  });

  test('should suspend and restore tenant', async () => {
    const createRes = await client.createTenant('Suspendable Tenant', 'suspendable-tenant', true);
    expect(createRes.ok()).toBeTruthy();
    const createData = await createRes.json();
    const tenantId = createData.id;

    const suspendRes = await client.suspendTenant(tenantId, 'Testing suspension');
    expect(suspendRes.ok()).toBeTruthy();

    const restoreRes = await client.restoreTenant(tenantId);
    expect(restoreRes.ok()).toBeTruthy();
  });

  test('should archive tenant', async () => {
    const createRes = await client.createTenant('Archivable Tenant', 'archivable-tenant', true);
    expect(createRes.ok()).toBeTruthy();
    const createData = await createRes.json();
    const tenantId = createData.id;

    const archiveRes = await client.archiveTenant(tenantId, 'Testing archive');
    expect(archiveRes.ok()).toBeTruthy();
  });

  test('should list my tenants', async () => {
    const res = await client.listMyTenants();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data)).toBeTruthy();
  });

  test('should get tenant settings', async () => {
    const listRes = await client.listTenants();
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const tenantId = data.items[0].id;
    const res = await client.get(`/tenants/${tenantId}/settings`);
    expect(res.ok()).toBeTruthy();
  });

  test('should create tenant with owner', async () => {
    const userRes = await client.createUser(
      'tenant-owner-test@example.com',
      'OwnerTestPass123!',
      '+15551000060',
      'Tenant Owner Test',
      true
    );
    expect(userRes.ok()).toBeTruthy();
    const userData = await userRes.json();
    const ownerId = userData.id;

    const tenantRes = await client.createTenant('Owner Tenant', 'owner-tenant', true, ownerId);
    expect(tenantRes.ok()).toBeTruthy();
    const tenantData = await tenantRes.json();
    expect(tenantData.id).toBeTruthy();
  });

  test('should add and remove member', async () => {
    const createRes = await client.createTenant('Member Test Tenant', 'member-test-tenant', true);
    expect(createRes.ok()).toBeTruthy();
    const tenantData = await createRes.json();
    const tenantId = tenantData.id;

    const userRes = await client.createUser(
      'member-test@example.com',
      'MemberTestPass123!',
      '+15551000070',
      'Member Test User',
      true
    );
    expect(userRes.ok()).toBeTruthy();
    const userData = await userRes.json();
    const userId = userData.id;

    const addRes = await client.addMember(tenantId, userId, false);
    expect(addRes.ok()).toBeTruthy();

    const listRes = await client.listMembers(tenantId);
    expect(listRes.ok()).toBeTruthy();
    const membersData = await listRes.json();
    expect(membersData.some((m: any) => m.userId === userId)).toBeTruthy();

    const removeRes = await client.removeMember(tenantId, userId);
    expect(removeRes.ok()).toBeTruthy();
  });

  test('should set owner', async () => {
    const createRes = await client.createTenant('Owner Test Tenant', 'owner-test-tenant', true);
    expect(createRes.ok()).toBeTruthy();
    const tenantData = await createRes.json();
    const tenantId = tenantData.id;

    const userRes = await client.createUser(
      'new-owner-test@example.com',
      'NewOwnerTestPass123!',
      '+15551000080',
      'New Owner Test',
      true
    );
    expect(userRes.ok()).toBeTruthy();
    const userData = await userRes.json();
    const userId = userData.id;

    await client.addMember(tenantId, userId, false);

    const setOwnerRes = await client.setOwner(tenantId, userId, true);
    expect(setOwnerRes.ok()).toBeTruthy();
  });

  test('should get tenant audit log', async () => {
    const listRes = await client.listTenants();
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const tenantId = data.items[0].id;
    const res = await client.getAuditLog(tenantId);
    expect(res.ok()).toBeTruthy();
    const auditData = await res.json();
    expect(Array.isArray(auditData.items)).toBeTruthy();
  });
});