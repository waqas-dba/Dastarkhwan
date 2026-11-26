// Dastarkhwan E2E — Database and Persistence Tests
// Tests constraints, RLS, and data integrity using actual PostgreSQL.

const { test, expect } = require('@playwright/test');
const { createApiClient } = require('../helpers/api-client');
const config = require('../helpers/config');
const { query, count, tableRowCount, scalar } = require('../helpers/db-client');
const { makeEmail, makePassword, registerPayload, loginPayload } = require('../helpers/fixtures');

test.describe('Database — Schema and Constraints', () => {
  test('should verify Identity_Users table exists and is accessible', async ({ request }) => {
    const countResult = await count(`SELECT COUNT(*) FROM identity."Identity_Users"`);
    expect(typeof countResult).toBe('number');
    expect(countResult).toBeGreaterThanOrEqual(0);
  });

  test('should verify Access tables exist', async ({ request }) => {
    const rolesCount = await count(`SELECT COUNT(*) FROM access."ACCESS_Roles"`);
    expect(typeof rolesCount).toBe('number');
    expect(rolesCount).toBeGreaterThanOrEqual(0);
  });

  test('should verify Tenant tables exist', async ({ request }) => {
    const tenantsCount = await count(`SELECT COUNT(*) FROM tenant."TENANT_Tenants"`);
    expect(typeof tenantsCount).toBe('number');
    expect(tenantsCount).toBeGreaterThanOrEqual(0);
  });

  test('should verify no RLS is enabled on Identity tables', async ({ request }) => {
    const rlsEnabled = await scalar(`SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'identity' AND rowsecurity = true`);
    expect(parseInt(rlsEnabled, 10)).toBe(0);
  });

  test('should verify no RLS is enabled on Tenant tables', async ({ request }) => {
    const rlsEnabled = await scalar(`SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'tenant' AND rowsecurity = true`);
    expect(parseInt(rlsEnabled, 10)).toBe(0);
  });

  test('should verify no RLS is enabled on Access tables', async ({ request }) => {
    const rlsEnabled = await scalar(`SELECT COUNT(*) FROM pg_tables WHERE schemaname = 'access' AND rowsecurity = true`);
    expect(parseInt(rlsEnabled, 10)).toBe(0);
  });

  test('should verify no RLS policies exist', async ({ request }) => {
    const policyCount = await scalar(`SELECT COUNT(*) FROM pg_policy`);
    expect(parseInt(policyCount, 10)).toBe(0);
  });
});

test.describe('Database — Data Integrity', () => {
  test('should verify email uniqueness constraint via API', async ({ request }) => {
    const api = createApiClient(request, config.webBaseUrl);
    const email = makeEmail('unique');
    
    const response1 = await api.post(`${config.identityPrefix}/auth/register`, registerPayload(email));
    expect(response1.status()).toBe(200);
    
    const response2 = await api.post(`${config.identityPrefix}/auth/register`, registerPayload(email));
    expect(response2.status()).toBe(400);
  });

  test('should verify Identity_OutboxMessages table exists', async ({ request }) => {
    const countResult = await count(`SELECT COUNT(*) FROM identity."Identity_OutboxMessages"`);
    expect(typeof countResult).toBe('number');
    expect(countResult).toBeGreaterThanOrEqual(0);
  });

  test('should verify Tenant_OutboxMessages table exists', async ({ request }) => {
    const countResult = await count(`SELECT COUNT(*) FROM tenant."TENANT_OutboxMessages"`);
    expect(typeof countResult).toBe('number');
    expect(countResult).toBeGreaterThanOrEqual(0);
  });
});

test.describe('Database — Migration History', () => {
  test('should verify Identity migrations have been applied', async ({ request }) => {
    const migrationCount = await count(`SELECT COUNT(*) FROM identity."__IdentityMigrationsHistory"`);
    expect(migrationCount).toBeGreaterThan(0);
  });

  test('should verify Access migrations have been applied', async ({ request }) => {
    const migrationCount = await count(`SELECT COUNT(*) FROM access."__AccessMigrationsHistory"`);
    expect(migrationCount).toBeGreaterThan(0);
  });

  test('should verify Tenant migrations have been applied', async ({ request }) => {
    const migrationCount = await count(`SELECT COUNT(*) FROM tenant."__TenantMigrationsHistory"`);
    expect(migrationCount).toBeGreaterThan(0);
  });

  test('should verify Identity RLS migration was NOT applied (typo migration)', async ({ request }) => {
    const rlsMigration = await scalar(`SELECT COUNT(*) FROM identity."__IdentityMigrationsHistory" WHERE "MigrationId" LIKE '%IdentityRls%'`);
    expect(parseInt(rlsMigration, 10)).toBe(0);
  });
});
