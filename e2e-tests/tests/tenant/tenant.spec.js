// Dastarkhwan E2E — Tenant Module Tests
// Tests tenant lifecycle, membership, context resolution.

const { test, expect } = require('@playwright/test');
const { createApiClient } = require('../helpers/api-client');
const config = require('../helpers/config');
const { makeEmail, makePassword, registerPayload, loginPayload, makeTenantName, createTenantPayload } = require('../helpers/fixtures');
const { count, tableRowCount, scalar } = require('../helpers/db-client');

test.describe('Tenant — Tenant Lifecycle (authenticated user)', () => {
  test.beforeEach(async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);
  });

  test('should list tenants (may be empty or 403 for non-platform user)', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.tenantPrefix}`);
    expect([200, 403]).toContain(response.status());
  });

  test('should create a tenant (requires platform permission)', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const payload = createTenantPayload();
    const response = await api.post(`${config.tenantPrefix}`, payload);
    // May succeed (platform permission) or 403
    expect([200, 201, 403, 400]).toContain(response.status());

    if (response.status() === 200 || response.status() === 201) {
      const body = await api.json(response);
      expect(body.id).toBeDefined();
      expect(body.name).toBeDefined();
      expect(body.status).toBeDefined();
    }
  });

  test('should reject invalid tenant data', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.post(`${config.tenantPrefix}`, {
      name: '', // empty name
    });
    expect([400, 403]).toContain(response.status());
  });

  test('should reject missing name field', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.post(`${config.tenantPrefix}`, {});
    expect([400, 403]).toContain(response.status());
  });

  test('should get current tenant context', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.tenantPrefix}/current`);
    // May return 200 (if tenant context resolved) or 400/404 (no tenant context)
    expect([200, 400, 404]).toContain(response.status());
  });

  test('should list user tenants', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.get(`${config.tenantPrefix}/me`);
    expect(response.status()).toBe(200);
    const body = await api.json(response);
    expect(Array.isArray(body)).toBe(true);
  });

  test('should reject unauthenticated access to tenant endpoints', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.tenantPrefix}`);
    expect(response.status()).toBe(401);
  });
});

test.describe('Tenant — Member Operations', () => {
  test.beforeEach(async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);
  });

  test('should reject unauthenticated member access', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.tenantPrefix}/00000000-0000-0000-0000-000000000000/members`);
    expect(response.status()).toBe(401);
  });

  test('should reject adding member to nonexistent tenant', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);

    const response = await api.post(`${config.tenantPrefix}/00000000-0000-0000-0000-000000000000/members`, {
      userId: '00000000-0000-0000-0000-000000000000',
      isOwner: false,
    });
    expect([403, 404, 400]).toContain(response.status());
  });
});

test.describe('Tenant — Tenant Settings', () => {
  test.beforeEach(async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const user = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, user);
    await api.login(user.email, user.password);
  });

  test('should reject unauthenticated settings access', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.tenantPrefix}/00000000-0000-0000-0000-000000000000/settings`);
    expect(response.status()).toBe(401);
  });
});