import { ApiClient } from '../helpers/apiClient';

export interface TestUser {
  id: string;
  email: string;
  password: string;
  mobile: string;
  displayName: string;
}

export interface TestTenant {
  id: string;
  name: string;
  slug: string;
  ownerUserId: string;
}

export interface TestContext {
  client: ApiClient;
  adminUser: TestUser;
  tenantA: TestTenant;
  tenantB: TestTenant;
  tenantAOwner: TestUser;
  tenantAStaff: TestUser;
  tenantBOwner: TestUser;
  tenantBStaff: TestUser;
}

export const TEST_PASSWORD = 'TestPass123!';

export async function createTestUser(
  client: ApiClient,
  email: string,
  displayName: string,
  mobile: string
): Promise<TestUser> {
  const res = await client.createUser(email, TEST_PASSWORD, mobile, displayName, true);
  if (!res.ok()) {
    throw new Error(`Failed to create user ${email}: ${res.status()} ${await res.text()}`);
  }
  const data = await res.json();
  return {
    id: data.id,
    email,
    password: TEST_PASSWORD,
    mobile,
    displayName,
  };
}

export async function loginAs(
  client: ApiClient,
  user: TestUser
): Promise<void> {
  await client.login(user.email, user.password);
}

export async function createTestTenant(
  client: ApiClient,
  name: string,
  slug: string,
  ownerUserId: string
): Promise<TestTenant> {
  const res = await client.createTenant(name, slug, true, ownerUserId);
  if (!res.ok()) {
    throw new Error(`Failed to create tenant ${name}: ${res.status()} ${await res.text()}`);
  }
  const data = await res.json();
  return {
    id: data.id,
    name,
    slug,
    ownerUserId,
  };
}

export async function addMemberToTenant(
  client: ApiClient,
  tenantId: string,
  userId: string,
  isOwner = false
): Promise<void> {
  const res = await client.addMember(tenantId, userId, isOwner);
  if (!res.ok()) {
    throw new Error(`Failed to add member to tenant ${tenantId}: ${res.status()} ${await res.text()}`);
  }
}

export async function setupTestContext(
  client: ApiClient
): Promise<TestContext> {
  // Login as admin (seeded during host startup)
  const adminRes = await client.login('testuser@example.com', 'TestPass123!');
  if (!adminRes.ok()) {
    // Admin might not exist, try the audit-admin user
    const auditRes = await client.login('testuser@example.com', 'TestPass123!');
    if (!auditRes.ok()) {
      throw new Error('Cannot login as admin or audit-admin');
    }
    adminUser: {
      id: auditRes.user.id,
      email: 'audit-admin@example.com',
      password: TEST_PASSWORD,
      mobile: '+15551000000',
      displayName: 'Audit Admin',
    }
  }

  // For now, use the logged-in user as admin
  const adminUser: TestUser = {
    id: adminRes.user.id,
    email: 'admin@example.com',
    password: 'a-long-test-password',
    mobile: '+15551000000',
    displayName: 'Admin User',
  };

  // Create tenant A
  const tenantA = await createTestTenant(client, 'Tenant A', 'tenant-a', adminUser.id);
  await addMemberToTenant(client, tenantA.id, adminUser.id, true);

  // Create tenant A staff
  const tenantAStaff = await createTestUser(
    client,
    'staff-a@example.com',
    'Staff A',
    '+15551000010'
  );
  await addMemberToTenant(client, tenantA.id, tenantAStaff.id, false);

  // Create tenant B
  const tenantB = await createTestTenant(client, 'Tenant B', 'tenant-b', adminUser.id);
  await addMemberToTenant(client, tenantB.id, adminUser.id, true);

  // Create tenant B staff
  const tenantBStaff = await createTestUser(
    client,
    'staff-b@example.com',
    'Staff B',
    '+15551000011'
  );
  await addMemberToTenant(client, tenantB.id, tenantBStaff.id, false);

  return {
    client,
    adminUser,
    tenantA,
    tenantB,
    tenantAOwner: adminUser,
    tenantAStaff,
    tenantBOwner: adminUser,
    tenantBStaff,
  };
}