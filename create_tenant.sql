INSERT INTO tenant."TENANT_Tenants" ("Id", "Name", "Slug", "Status", "CreatedAt", "UpdatedAt", "StatusChangedAt", "ConcurrencyStamp", "LastEventSequence") 
VALUES (gen_random_uuid(), 'E2E Audit Tenant', 'e2e-audit-tenant', 'Active', NOW(), NOW(), NOW(), gen_random_uuid(), 0) 
RETURNING "Id";