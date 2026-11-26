import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';
import { WEB_BASE_URL } from './helpers/config';
import { ADMIN_EMAIL, ADMIN_PASSWORD } from './helpers/fixtures';

const BASE_URL = WEB_BASE_URL;

test.describe('Access - Roles and Permissions', () => {
  let client: ApiClient;

  test.beforeEach(async ({ request }) => {
    client = new ApiClient(request, BASE_URL);
    await client.login(ADMIN_EMAIL, ADMIN_PASSWORD);
  });

  test('should list permissions', async () => {
    const res = await client.listPermissions();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data)).toBeTruthy();
    expect(data.length).toBeGreaterThan(0);
  });

  test('should list roles', async () => {
    const res = await client.listRoles();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data)).toBeTruthy();
  });

  test('should create a role', async () => {
    const res = await client.createRole('Test Role', 'A test role', ['tenants.read', 'tenants.settings.read']);
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data.id).toBeTruthy();
    expect(data.name).toBe('Test Role');
  });

  test('should get role by id', async () => {
    const listRes = await client.listRoles();
    const data = await listRes.json();
    if (data.length === 0) {
      test.skip();
      return;
    }
    const roleId = data[0].id;
    const res = await client.get(`/access/roles/${roleId}`);
    expect(res.ok()).toBeTruthy();
    const roleData = await res.json();
    expect(roleData.id).toBe(roleId);
  });

  test('should update role', async () => {
    const createRes = await client.createRole('Updatable Role', 'To be updated', ['tenants.read']);
    expect(createRes.ok()).toBeTruthy();
    const createData = await createRes.json();
    const roleId = createData.id;

    const updateRes = await client.put(`/access/roles/${roleId}`, {
      name: 'Updated Role',
      description: 'Updated description',
      concurrencyStamp: createData.concurrencyStamp,
    });
    expect(updateRes.ok()).toBeTruthy();
  });

  test('should set role permissions', async () => {
    const createRes = await client.createRole('Perm Role', 'For permissions test', ['tenants.read']);
    expect(createRes.ok()).toBeTruthy();
    const createData = await createRes.json();
    const roleId = createData.id;

    const permRes = await client.put(`/access/roles/${roleId}/permissions`, {
      permissions: ['tenants.read', 'tenants.settings.read'],
      concurrencyStamp: createData.concurrencyStamp,
    });
    expect(permRes.ok()).toBeTruthy();
  });

  test('should delete role', async () => {
    const createRes = await client.createRole('Deletable Role', 'To be deleted', []);
    expect(createRes.ok()).toBeTruthy();
    const createData = await createRes.json();
    const roleId = createData.id;

    const deleteRes = await client.delete(`/access/roles/${roleId}`);
    expect(deleteRes.ok()).toBeTruthy();
  });

  test('should get user roles', async () => {
    const listRes = await client.get('/Identity/users');
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const userId = data.items[0].id;
    const res = await client.get(`/access/users/${userId}/roles`);
    expect(res.ok()).toBeTruthy();
  });

  test('should assign user roles', async () => {
    const listRes = await client.get('/Identity/users');
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const userId = data.items[0].id;
    const rolesRes = await client.listRoles();
    const rolesData = await rolesRes.json();
    if (rolesData.length === 0) {
      test.skip();
      return;
    }
    const roleId = rolesData[0].id;

    const assignRes = await client.assignUserRoles(userId, [roleId]);
    expect(assignRes.ok()).toBeTruthy();
  });

  test('should add platform member', async () => {
    const listRes = await client.get('/Identity/users');
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const userId = data.items[0].id;
    const res = await client.addPlatformMember(userId);
    expect(res.ok()).toBeTruthy();
  });

  test('should check platform membership', async () => {
    const listRes = await client.get('/Identity/users');
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const userId = data.items[0].id;
    const res = await client.get(`/access/platform/members/${userId}`);
    expect(res.ok()).toBeTruthy();
  });

  test('should revoke platform member', async () => {
    const listRes = await client.get('/Identity/users');
    const data = await listRes.json();
    if (data.items.length === 0) {
      test.skip();
      return;
    }
    const userId = data.items[0].id;
    await client.addPlatformMember(userId);
    const res = await client.revokePlatformMember(userId);
    expect(res.ok()).toBeTruthy();
  });

  test('should get audit log', async () => {
    const res = await client.get('/access/audit');
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(Array.isArray(data.items)).toBeTruthy();
  });
});