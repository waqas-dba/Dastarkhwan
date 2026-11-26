import { test, expect } from '@playwright/test';
import { ApiClient } from './helpers/apiClient';

const BASE_URL = 'http://localhost:53584';

test.describe('Cross-Tenant Security', () => {
  let clientA: ApiClient;
  let clientB: ApiClient;
  let adminClient: ApiClient;
  let tenantAId: string;
  let tenantBId: string;
  let userAId: string;
  let userBId: string;

  test.beforeEach(async ({ request }) => {
    clientA = new ApiClient(request, BASE_URL);
    clientB = new ApiClient(request, BASE_URL);
    adminClient = new ApiClient(request, BASE_URL);

    // Login as testuser
    try {
      await adminClient.login('testuser@example.com', 'TestPass123!');
    } catch {
      const auditRes = await adminClient.login('testuser@example.com', 'TestPass123!');
      if (!auditRes.accessToken) {
        throw new Error('Cannot login as admin');
      }
    }

    // Create tenant A
    const tenantARes = await adminClient.createTenant('Tenant A', 'tenant-a-cross', true);
    expect(tenantARes.ok()).toBeTruthy();
    const tenantAData = await tenantARes.json();
    tenantAId = tenantAData.id;

    // Create tenant B
    const tenantBRes = await adminClient.createTenant('Tenant B', 'tenant-b-cross', true);
    expect(tenantBRes.ok()).toBeTruthy();
    const tenantBData = await tenantBRes.json();
    tenantBId = tenantBData.id;

    // Create user A and add to tenant A
    const userARes = await adminClient.createUser(
      'cross-tenant-a@example.com',
      'TestPass123!',
      '+15551000090',
      'User A',
      true
    );
    expect(userARes.ok()).toBeTruthy();
    const userAData = await userARes.json();
    userAId = userAData.id;

    await adminClient.addMember(tenantAId, userAId, true);

    // Create user B and add to tenant B
    const userBRes = await adminClient.createUser(
      'cross-tenant-b@example.com',
      'TestPass123!',
      '+15551000091',
      'User B',
      true
    );
    expect(userBRes.ok()).toBeTruthy();
    const userBData = await userBRes.json();
    userBId = userBData.id;

    await adminClient.addMember(tenantBId, userBId, true);

    // Login as tenant A user
    await clientA.login('cross-tenant-a@example.com', 'TestPass123!');

    // Login as tenant B user
    await clientB.login('cross-tenant-b@example.com', 'TestPass123!');
  });

  test('should not allow tenant A user to access tenant B data', async () => {
    const res = await clientA.getTenant(tenantBId);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to list tenant B members', async () => {
    const res = await clientA.listMembers(tenantBId);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to add member to tenant B', async () => {
    const res = await clientA.addMember(tenantBId, userAId, false);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to remove member from tenant B', async () => {
    const res = await clientA.removeMember(tenantBId, userBId);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to set owner on tenant B', async () => {
    const res = await clientA.setOwner(tenantBId, userBId, true);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to get tenant B audit log', async () => {
    const res = await clientA.getAuditLog(tenantBId);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to update tenant B', async () => {
    const res = await clientA.updateTenant(tenantBId, { name: 'Hacked Tenant' });
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow tenant A user to delete tenant B', async () => {
    const res = await clientA.deleteTenant(tenantBId);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should not allow cross-tenant membership manipulation', async () => {
    const res = await clientA.addMember(tenantBId, userAId, false);
    expect(res.status()).toBeIn([403, 404]);
  });

  test('should allow tenant A user to access their own tenant data', async () => {
    const res = await clientA.getTenant(tenantAId);
    expect(res.ok()).toBeTruthy();
    const data = await res.json();
    expect(data.id).toBe(tenantAId);
  });

  test('should allow tenant A user to list their own tenant members', async () => {
    const res = await clientA.listMembers(tenantAId);
    expect(res.ok()).toBeTruthy();
  });

  test('should not allow unauthenticated access to tenant endpoints', async ({ request }) => {
    const unauthClient = new ApiClient(request, BASE_URL);
    const res = await unauthClient.getTenant(tenantAId);
    expect(res.status()).toBe(401);
  });

  test('should verify database state shows correct tenant isolation', async () => {
    const resA = await clientA.listMembers(tenantAId);
    expect(resA.ok()).toBeTruthy();
    const membersA = await resA.json();
    const memberIdsA = membersA.map((m: any) => m.userId);
    expect(memberIdsA).not.toContain(userBId);

    const resB = await clientB.listMembers(tenantBId);
    expect(resB.ok()).toBeTruthy();
    const membersB = await resB.json();
    const memberIdsB = membersB.map((m: any) => m.userId);
    expect(memberIdsB).not.toContain(userAId);
  });
});