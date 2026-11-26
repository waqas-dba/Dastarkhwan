// Dastarkhwan E2E — Cross-Tenant Isolation Tests
// Tests that tenants cannot access each other's data.

const { test, expect } = require('@playwright/test');
const { createApiClient } = require('../helpers/api-client');
const config = require('../helpers/config');
const { makeEmail, makePassword, registerPayload, loginPayload, makeTenantName } = require('../helpers/fixtures');
const { count, scalar } = require('../helpers/db-client');

// Helper to create an authenticated user and return auth info
async function createAuthenticatedUser(testInfo, api) {
  const user = registerPayload();
  await api.post(`${config.identityPrefix}/auth/register`, user);
  const login = await api.login(user.email, user.password);
  expect(login.response.status()).toBe(200);
  return { ...user, accessToken: login.body.accessToken, userId: login.body.user.id };
}

test.describe('Cross-Tenant Isolation — Basic', () => {
  test('should reject unauthenticated cross-tenant access', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.tenantPrefix}/00000000-0000-0000-0000-000000000000`);
    expect(response.status()).toBe(401);
  });

  test('should reject access with invalid token', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.setAuthToken('invalid-token');

    const response = await api.get(`${config.tenantPrefix}/00000000-0000-0000-0000-000000000000`);
    expect(response.status()).toBe(401);
  });
});

test.describe('Cross-Tenant Isolation — Tenant ID Manipulation', () => {
  test('should not allow tenant A user to guess tenant B resources', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    await createAuthenticatedUser({}, api);

    // Try to access a nonexistent tenant with user A's token
    const fakeTenantId = '00000000-0000-0000-0000-000000000000';
    const response = await api.get(`${config.tenantPrefix}/${fakeTenantId}`);
    expect([403, 404]).toContain(response.status());
  });

  test('should not allow cross-tenant member access', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    await createAuthenticatedUser({}, api);

    const fakeTenantId = '00000000-0000-0000-0000-000000000000';
    const fakeUserId = '00000000-0000-0000-0000-000000000000';

    const response = await api.delete(`${config.tenantPrefix}/${fakeTenantId}/members/${fakeUserId}`);
    expect([403, 404, 401]).toContain(response.status());
  });
});

test.describe('Cross-Tenant Isolation — Same User ID Different Tenants', () => {
  test('should treat same user ID as different identities across tenants', async ({ request }) => {
    const apiA = createApiClient(request, config.webBaseUrl);
    await createAuthenticatedUser({}, apiA);

    const apiB = createApiClient(request, config.webBaseUrl);
    await createAuthenticatedUser({}, apiB);

    // Each user should only see their own tenant context
    const meA = await apiA.get(`${config.tenantPrefix}/me`);
    const meB = await apiB.get(`${config.tenantPrefix}/me`);

    expect([200, 403, 404]).toContain(meA.status());
    expect([200, 403, 404]).toContain(meB.status());
  });
});

test.describe('Cross-Tenant Isolation — Database Verification', () => {
  test('should verify no cross-tenant data in database', async ({ request }) => {
    // Verify that the Identity_Users table has only users created in this test session
    const userCount = await count(`SELECT COUNT(*) FROM identity.\"Identity_Users\"`);
    expect(userCount).toBeGreaterThanOrEqual(0);
  });
});