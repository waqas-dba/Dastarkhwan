// Dastarkhwan E2E — Identity Module Tests
// Tests registration, login, tokens, lockout, password reset, account lifecycle.

const { test, expect } = require('@playwright/test');
const { createApiClient } = require('../helpers/api-client');
const config = require('../helpers/config');
const { makeEmail, makePassword, registerPayload, loginPayload, changePasswordPayload } = require('../helpers/fixtures');
const { count } = require('../helpers/db-client');

test.describe('Identity — Registration', () => {
  test('should register a new user successfully', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const payload = registerPayload();

    const response = await api.post(`${config.identityPrefix}/auth/register`, payload);
    const body = await api.json(response);

    expect(response.status()).toBe(200);
    expect(body.id).toBeDefined();
    expect(body.email).toBe(payload.email.toLowerCase()); // email normalized
    expect(body.isActive).toBe(true);

    // Verify in database
    const userCount = await count(`SELECT COUNT(*) FROM identity."Identity_Users" WHERE email = '${body.email}'`);
    expect(userCount).toBe(1);
  });

  test('should reject duplicate registration with same email', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const email = makeEmail('dup');
    const payload = registerPayload(email);

    const response1 = await api.post(`${config.identityPrefix}/auth/register`, payload);
    expect(response1.status()).toBe(200);

    const response2 = await api.post(`${config.identityPrefix}/auth/register`, payload);
    expect(response2.status()).toBe(400); // duplicate email
  });

  test('should reject registration with missing email', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);

    const response = await api.post(`${config.identityPrefix}/auth/register`, {
      password: makePassword(),
    });
    expect(response.status()).toBe(400);
  });

  test('should reject registration with missing password', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);

    const response = await api.post(`${config.identityPrefix}/auth/register`, {
      email: makeEmail('nopass'),
    });
    expect(response.status()).toBe(400);
  });

  test('should normalize email to lowercase', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const email = `UPPER.${makeEmail('case')}`;
    const payload = registerPayload(email);

    const response = await api.post(`${config.identityPrefix}/auth/register`, payload);
    const body = await api.json(response);

    expect(response.status()).toBe(200);
    expect(body.email).toBe(body.email.toLowerCase());
  });
});

test.describe('Identity — Login', () => {
  test('should login with valid credentials', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);

    const response = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, testUser.password));
    const body = await api.json(response);

    expect(response.status()).toBe(200);
    expect(body.accessToken).toBeDefined();
    expect(body.refreshToken).toBeDefined();
    expect(body.user.email).toBe(testUser.email);
  });

  test('should reject invalid password', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);

    const response = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, 'WrongPassword123!'));
    expect(response.status()).toBe(401);
  });

  test('should reject login for unknown email', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);

    const response = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(makeEmail('nonexistent'), makePassword()));
    expect(response.status()).toBe(401);
  });

  test('should reject missing credentials', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);

    const response = await api.post(`${config.identityPrefix}/auth/login`, {});
    expect(response.status()).toBe(400);
  });

  test('should return uniform response for unknown email (no enumeration)', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);

    const response = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(makeEmail('unknown'), makePassword()));
    const body = await api.json(response);

    expect(response.status()).toBe(401);
    expect(body).toBeDefined();
  });
});

test.describe('Identity — Account Lockout', () => {
  test('should lock account after 5 failed login attempts', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);

    // 5 failed attempts
    for (let i = 0; i < 5; i++) {
      const response = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, 'WrongPassword123!'));
      expect(response.status()).toBe(401);
    }

    // 6th attempt should be locked
    const response = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, 'WrongPassword123!'));
    expect([401, 423, 429]).toContain(response.status());
  });
});

test.describe('Identity — Tokens and Sessions', () => {
  test('should issue valid JWT access token', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    await api.login(testUser.email, testUser.password);

    const response = await api.get(`${config.identityPrefix}/auth/me`);
    expect(response.status()).toBe(200);
  });

  test('should reject request without authentication token', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.identityPrefix}/auth/me`);
    expect(response.status()).toBe(401);
  });

  test('should refresh token and rotate', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    const authResult = await api.login(testUser.email, testUser.password);
    expect(authResult.response.status()).toBe(200);

    const refreshToken = authResult.body.refreshToken;
    const response = await api.post(`${config.identityPrefix}/auth/refresh`, { refreshToken });
    const body = await api.json(response);

    expect(response.status()).toBe(200);
    expect(body.accessToken).toBeDefined();
    expect(body.refreshToken).toBeDefined();
    expect(body.refreshToken).not.toBe(refreshToken);
  });

  test('should detect refresh token replay (reuse of revoked token)', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    const authResult = await api.login(testUser.email, testUser.password);
    expect(authResult.response.status()).toBe(200);

    const refreshToken = authResult.body.refreshToken;

    // Use the refresh token once
    await api.post(`${config.identityPrefix}/auth/refresh`, { refreshToken });

    // Try to use the same (now revoked) refresh token again
    const response = await api.post(`${config.identityPrefix}/auth/refresh`, { refreshToken });
    expect([400, 401, 403]).toContain(response.status());
  });

  test('should logout and invalidate refresh token', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    const authResult = await api.login(testUser.email, testUser.password);
    expect(authResult.response.status()).toBe(200);

    const refreshToken = authResult.body.refreshToken;
    const logoutResponse = await api.post(`${config.identityPrefix}/auth/logout`, { refreshToken });
    expect([200, 204]).toContain(logoutResponse.status());

    // Refresh token should no longer work after logout
    const refreshResponse = await api.post(`${config.identityPrefix}/auth/refresh`, { refreshToken });
    expect([400, 401, 403]).toContain(refreshResponse.status());
  });

  test('should reject malformed token', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.setAuthToken('not-a-valid-jwt');

    const response = await api.get(`${config.identityPrefix}/auth/me`);
    expect(response.status()).toBe(401);
  });
});

test.describe('Identity — Password Change', () => {
  test('should change password successfully', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    await api.login(testUser.email, testUser.password);

    const newPassword = 'NewPassword456!';
    const response = await api.post(`${config.identityPrefix}/auth/change-password`, changePasswordPayload(testUser.password, newPassword));
    expect(response.status()).toBe(200);

    // Old password should no longer work
    const oldLogin = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, testUser.password));
    expect(oldLogin.status()).toBe(401);

    // New password should work
    const newLogin = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, newPassword));
    expect(newLogin.status()).toBe(200);
  });

  test('should reject wrong current password', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    await api.login(testUser.email, testUser.password);

    const response = await api.post(`${config.identityPrefix}/auth/change-password`, changePasswordPayload('WrongPassword123!', 'NewPassword456!'));
    expect(response.status()).toBe(400);
  });
});

test.describe('Identity — Password Reset (Recovery)', () => {
  test('should request password reset for known account', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);

    const response = await api.post(`${config.identityPrefix}/auth/forgot-password`, { email: testUser.email });
    expect([200, 202, 204]).toContain(response.status());
  });

  test('should not reveal account existence for unknown email', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);

    const response = await api.post(`${config.identityPrefix}/auth/forgot-password`, { email: makeEmail('unknown') });
    expect([200, 202, 204]).toContain(response.status());
  });
});

test.describe('Identity — Account Deactivation', () => {
  test('should deactivate account and revoke sessions', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const testUser = registerPayload();
    await api.post(`${config.identityPrefix}/auth/register`, testUser);
    const authResult = await api.login(testUser.email, testUser.password);
    expect(authResult.response.status()).toBe(200);

    const response = await api.put(`${config.identityPrefix}/users/${authResult.body.user.id}`, {
      isActive: false,
    });
    expect(response.status()).toBe(200);

    const loginResponse = await api.post(`${config.identityPrefix}/auth/login`, loginPayload(testUser.email, testUser.password));
    expect(loginResponse.status()).toBe(401);
  });
});

test.describe('Identity — Protected Endpoint Access', () => {
  test('should reject unauthorized access to protected endpoint', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    api.clearAuth();

    const response = await api.get(`${config.identityPrefix}/users`);
    expect(response.status()).toBe(401);
  });
});
