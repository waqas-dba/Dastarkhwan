import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';

const BASE_URL = 'http://localhost:53584';

test.describe('Admin - Platform Administration', () => {
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
      'platform-member-test@example.com',
      'PlatformMemberPass123!',
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
    const staffClient = new ApiClient(request, BASE_URL);
    await staffClient.login('staff-a@example.com', 'TestPass123!');
    const res = await staffClient.addPlatformMemberAdmin('00000000-0000-0000-0000-000000000000');
    expect(res.status()).toBe(403);
  });
});