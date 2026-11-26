import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';
import { ADMIN_BASE_URL } from './helpers/config';
import { ADMIN_EMAIL, ADMIN_PASSWORD, uniqueEmail, TEST_PASSWORD } from './helpers/fixtures';

const BASE_URL = ADMIN_BASE_URL;

test.describe('Admin - Platform Administration', () => {
  let client: ApiClient;

  test.beforeEach(async ({ request }) => {
    client = new ApiClient(request, BASE_URL);
    await client.login(ADMIN_EMAIL, ADMIN_PASSWORD);
  });

  test('should access admin health endpoint', async () => {
    const res = await client.adminHealthReady();
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data.status).toBe('ready');
  });

  test('should reject unauthenticated admin access', async ({ request }) => {
    const unauthClient = new ApiClient(request, BASE_URL);
    const res = await unauthClient.adminHealthReady();
    expect(res.status()).toBe(401);
  });

  test('should add platform member', async () => {
    const userRes = await client.createUser(
      uniqueEmail('platform-member'),
      TEST_PASSWORD,
      '+15551000090',
      'Platform Member Test',
      true
    );
    expect(userRes.ok()).toBeTruthy();
    const userData = await userRes.json();
    const userId = userData.id;

    const res = await client.addPlatformMemberAdmin(userId);
    expect(res.ok()).toBeTruthy();
  });

  test('should reject non-admin platform member addition', async ({ request }) => {
    // Create a fresh non-platform-admin user, then attempt an admin-only operation.
    const adminClient = new ApiClient(request, BASE_URL);
    await adminClient.login(ADMIN_EMAIL, ADMIN_PASSWORD);
    const email = uniqueEmail('nonadmin');
    const createRes = await adminClient.createUser(email, TEST_PASSWORD, '+15551000091', 'Non Admin', true);
    expect(createRes.ok()).toBeTruthy();

    const staffClient = new ApiClient(request, BASE_URL);
    await staffClient.login(email, TEST_PASSWORD);
    const res = await staffClient.addPlatformMemberAdmin('00000000-0000-0000-0000-000000000000');
    expect(res.status()).toBe(403);
  });
});