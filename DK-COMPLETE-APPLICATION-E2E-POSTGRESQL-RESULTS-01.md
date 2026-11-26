# DK-COMPLETE-APPLICATION-E2E-POSTGRESQL-RESULTS-01

Database-level verification performed during the E2E campaign.

## Environment
- PostgreSQL 18.3 at `127.0.0.1:5432`.
- The running Web host connects to database **`dastarkhwan_e2e_audit`** (resolved from
  `pg_stat_activity`; the host process overrides `ConnectionStrings__{Access,Identity,Tenant}` via
  environment variables). The gitignored `appsettings.Development.json` points at
  `dastarkhwan_runtime_audit`, but the env vars win for the connection string.
- Queries run as `postgres` (superuser) for inspection; the app role is `dastarkhwan_e2e_audit`.

## Schema layout (non-system schemas)
- `identity`: `Identity_Users`, `Identity_RefreshTokens`, `Idenity_EmailVerificationTokens`,
  `Identity_OutboxMessages`, `__IdentityMigrationsHistory`.
- `access`: `ACCESS_Roles`, `ACCESS_RolePermissions`, `ACCESS_Assignments`, `ACCESS_PlatformMembers`,
  `ACCESS_AuditEntries`, `__AccessMigrationsHistory`.
- `tenant`: `TENANT_Tenants`, `TENANT_Members`, `TENANT_Settings`, `TENANT_AuditEntries`,
  `TENANT_OutboxMessages`, `__TenantMigrationsHistory`.
- All three migration history tables present (migrations applied). Identifiers are case-sensitive
  (mixed case) and require quoting in SQL.

## Seed / authorization state (evidence for D-E2E-001)
- `ACCESS_Roles`: exactly one role — `Administrator`, `ScopeType=platform`,
  `ScopeId=00000000-0000-0000-0000-000000000001`.
- `ACCESS_RolePermissions` for that role: the six `access.*` / `platform.members.manage` permissions
  only. **No `identity.users.*` and no `tenants.*` grants.**
- `ACCESS_Assignments`: the admin user (`453207bb-...`) → Administrator at platform scope.
- `ACCESS_PlatformMembers`: admin is an active platform member (RevokedAt null).
- `Identity_Users`: admin `admin@e2e-audit.local` present, active, email confirmed.

## Outbox state (evidence for D-E2E-002)
- `identity.Identity_OutboxMessages`: 0 rows; publisher polling observed in host logs.
- `tenant.TENANT_OutboxMessages`: 0 rows; **no publisher polling observed** (publisher not started).

## Cross-tenant DB isolation
- Not exercised this campaign: seeding two tenants + two users is blocked by D-E2E-001. The
  cross-tenant spec includes a DB-state isolation check to run once unblocked.

## Integrity notes
- No production database was touched. All inspection was read-only against the development audit DB.
- No schema changes were made during the campaign.