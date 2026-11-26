# DK-COMPLETE-APPLICATION-E2E-APPLICATION-DEFECTS-01

Consolidated application defects found during the end-to-end test campaign.
These are app-side issues, not test defects. Per Owner decision, affected tests are kept
failing to document them; no application code was changed to make tests pass.

## D-E2E-001 — Platform Administrator role is under-provisioned (High)
**Blocks:** all Identity user management and all Tenant management.

### Evidence
- `CoreKit.Access/Services/AccessBootstrap.cs:47-59` seeds the platform **Administrator** role with
  exactly six permissions: `access.roles.read`, `access.roles.manage`, `access.assignments.read`,
  `access.assignments.manage`, `access.audit.read`, `platform.members.manage`.
- The `identity.users.read/create/update/delete` permissions are declared in the catalog
  (`Dastarkhwan.Web/Program.cs:153-156`, `Dastarkhwan.Admin/Program.cs:116-119`) but granted to no role.
- The `tenants.*` permissions (`tenants.platform`, `tenants.create`, `tenants.read`,
  `tenants.update`, `tenants.manage_status`, `tenants.delete`, `tenants.settings.*`,
  `tenants.members.*`, `tenants.audit.read`) are likewise declared (Admin `Program.cs:120`,
  Integration `AddDastarkhwanIntegration`) but granted to no role.
- Live DB `dastarkhwan_e2e_audit`: `ACCESS_Roles` has one role (Administrator) with exactly the six
  `access.*` permissions; no `identity.*` or `tenants.*` grants exist. This reflects the seed, not a
  stale DB.

### Effect
- `PermissionBasedIdentityAdminAuthorizer` (Web `Program.cs:281-303`) → `HasPlatformPermissionAsync`
  fails closed → **403** on every `/identity/users` call (8 Identity E2E failures).
- `RequireAccessFilter` → **403** on every tenant-management route (9 Tenant E2E failures), and the
  cross-tenant suite cannot provision its fixtures (13 failures).
- Net: 30 of 72 Web-host E2E tests fail, all from this single root cause.

### Required fix (authz change — needs Owner approval + independent review)
Grant `identity.users.read/create/update/delete` and the `tenants.*` set to the platform
Administrator role in `BootstrapInitialPlatformAdminAsync` (keep in step with the catalog), or add a
dedicated Identity/Tenant admin role template carrying them. After the fix, re-run; the 30 failing
tests should pass unchanged.

## D-E2E-002 — Tenant outbox publisher is never started (High if tenant provisioning is expected now; otherwise a documented limitation)
**Blocks:** Tenant→Access provisioning and membership-removal cache maintenance.

### Evidence
- `AddTenant` (`CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs`) registers
  `ITenantOutbox` only; it does not call `AddTenantOutboxPublisher`, the method that registers the
  `TenantOutboxPublisherService` hosted service (`CoreKit.Tenant/Outbox/TenantOutboxServiceCollectionExtensions.cs:35`).
- Neither host calls it: `Dastarkhwan.Web/Program.cs:121` and `Dastarkhwan.Admin/Program.cs:104`
  carry a "Register outbox publisher" comment followed by nothing.
- Runtime logs show the **Identity** outbox publisher polling continuously
  (`SELECT ... FROM identity."Identity_OutboxMessages" WHERE "Status" = 1 ...`) but **no** equivalent
  poll of `tenant."TENANT_OutboxMessages"`.
- `TenantEventPublisher` enqueues `TenantCreated` / `TenantMemberRemoved`, so messages would accumulate
  unprocessed.

### Effect
- `TenantCreatedToAccessProvision` and `TenantMemberRemovedToAccessMaintenance` handlers never run.
  A newly created tenant would not get its default Access roles; membership-removal cache maintenance
  would not fire.

### Note
The Integration extension comment states the publisher "arrives with the projection cutover," so this
may be intentionally deferred. **Owner to confirm.** If deferred, record as a known limitation; if
tenant provisioning is expected now, treat as High and register `AddTenantOutboxPublisher` in both hosts.

## Not defects (verified correct)
- Access authorization, role lifecycle, escalation guard, concurrency (Access 20/20 pass).
- Identity authentication, token rotation/replay, lockout, password recovery (16/24 pass; the 8
  failures are D-E2E-001, not auth logic).
- Tenant scope resolution and fail-closed 400/403 ordering (context tests pass).