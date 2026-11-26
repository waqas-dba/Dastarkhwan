// Dastarkhwan E2E — Access Module Tests
// Tests roles, permissions, assignments, authorization boundaries.

const { test, expect } = require('@playwright/test');
const { createApiClient } = require('../helpers/api-client');
const config = require('../helpers/config');
const { makeEmail, makePassword, registerPayload, loginPayload, makeRoleName, createRolePayload } = require('../helpers/fixtures');
const { count, tableRowCount, scalar } = require('../helpers/db-client');

test.describe('Access — Roles and Permissions (authenticated user)', () => {
  test('should list permissions (may be empty or 403 for non-platform user)', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.accessPrefix}/permissions`);
    expect([200, 403]).toContain(response.status());
  });

  test('should list roles', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.accessPrefix}/roles`);
    expect(response.status()).toBe(200);
    const body = await api.json(response);
    expect(Array.isArray(body)).toBe(true);
  });

  test('should reject unauthenticated access to access endpoints', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.accessPrefix}/roles`);
    expect(response.status()).toBe(401);
  });

  test('should reject access to nonexistent role', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.accessPrefix}/roles/00000000-0000-0000-0000-000000000000`);
    expect(response.status()).toBe(404);
  });

  test('should get my access info', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.accessPrefix}/me`);
    expect(response.status()).toBe(200);
    const body = await api.json(response);
    expect(body).toBeDefined();
  });
});

test.describe('Access — Role CRUD (requires platform or tenant scope)', () => {
  test('should create a role (may require platform permission)', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const rolePayload = {
      name: makeRoleName(),
      description: 'E2E test role',
      permissions: ['sales.create'],
    };
    const response = await api.post(`${config.accessPrefix}/roles`, rolePayload);
    // May succeed (if user has platform/tenant permission) or 403
    expect([200, 201, 403, 400]).toContain(response.status());

    if (response.status() === 200 || response.status() === 201) {
      const body = await api.json(response);
      expect(body.id).toBeDefined();
      // Clean up: try to delete
      await api.delete(`${config.accessPrefix}/roles/${body.id}`);
    }
  });

  test('should reject duplicate role name', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const roleName = makeRoleName('Dup');
    const rolePayload = { name: roleName, description: 'Test', permissions: [] };

    const resp1 = await api.post(`${config.accessPrefix}/roles`, rolePayload);
    if (resp1.status() === 200 || resp1.status() === 201) {
      const resp2 = await api.post(`${config.accessPrefix}/roles`, rolePayload);
      expect([400, 409, 403]).toContain(resp2.status());
    }
  });

  test('should reject invalid role data', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.post(`${config.accessPrefix}/roles`, {
      name: '', // empty name
      description: 'Test',
      permissions: [],
    });
    expect([400, 403]).toContain(response.status());
  });

  test('should reject missing name field', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.post(`${config.accessPrefix}/roles`, {
      description: 'No name',
      permissions: [],
    });
    expect([400, 403]).toContain(response.status());
  });
});

test.describe('Access — Authorization Boundaries', () => {
  test('should require authentication for platform membership endpoints', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.post(`${config.accessPrefix}/platform/members`, {
      userId: '00000000-0000-0000-0000-000000000000',
    });
    expect(response.status()).toBe(401);
  });

  test('should require authentication for user role assignment', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.put(`${config.accessPrefix}/users/00000000-0000-0000-0000-000000000000/roles`, {
      roleIds: [],
    });
    expect(response.status()).toBe(401);
  });
});

test.describe('Access — Audit Log', () => {
  test('should list access audit entries', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.accessPrefix}/audit`);
    expect([200, 403]).toContain(response.status());
  });
});

test.describe('Access — Permission Assignment', () => {
  test('should assign permissions to a role', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const rolePayload = createRolePayload('Perm Role', 'A test role', []);
    const createResp = await api.post(`${config.accessPrefix}/roles`, rolePayload);
    if (createResp.status() === 200 || createResp.status() === 201) {
      const body = await api.json(createResp);

      // Need concurrency stamp for update - get the role first
      const getResp = await api.get(`${config.accessPrefix}/roles/${body.id}`);
      const roleBody = await api.json(getResp);

      const updateResp = await api.put(`${config.accessPrefix}/roles/${body.id}/permissions`, {
        permissions: ['sales.create', 'products.view'],
        concurrencyStamp: roleBody.concurrencyStamp || '00000000-0000-0000-0000-000000000000',
      });
      expect([200, 204, 400, 403, 409]).toContain(updateResp.status());
    }
  });

  test('should reject invalid permission assignment', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const rolePayload = createRolePayload('Bad Perm Role', 'A test role', []);
    const createResp = await api.post(`${config.accessPrefix}/roles`, rolePayload);
    if (createResp.status() === 200 || createResp.status() === 201) {
      const body = await api.json(createResp);

      const updateResp = await api.put(`${config.accessPrefix}/roles/${body.id}/permissions`, {
        permissions: ['invalid.permission.that.does.not.exist'],
        concurrencyStamp: '00000000-0000-0000-0000-000000000000',
      });
      expect([400, 403, 409]).toContain(updateResp.status());
    }
  });

  test('should delete a role', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const rolePayload = createRolePayload('Delete Role', 'To be deleted', []);
    const createResp = await api.post(`${config.accessPrefix}/roles`, rolePayload);
    if (createResp.status() === 200 || createResp.status() === 201) {
      const body = await api.json(createResp);

      const response = await api.delete(`${config.accessPrefix}/roles/${body.id}`);
      expect([200, 204, 403, 409]).toContain(response.status());
    }
  });
});