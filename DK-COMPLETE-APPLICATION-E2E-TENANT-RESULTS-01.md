# DK-COMPLETE-APPLICATION-E2E-TENANT-RESULTS-01

Phase 4 — Tenant module end-to-end results.

## Baseline
- Commit: `6b6fd2a` (Phase 2/3 changes still uncommitted; this phase adds to the working tree).
- Host: Web `https://localhost:53584` (Development), real PostgreSQL `dastarkhwan_e2e_audit`.
- Test admin: `admin@e2e-audit.local` (platform Administrator).

## Scope
22 Tenant endpoints under `/tenants` (TenantEndpointExtensions.cs), all guarded by
`RequireAccessPermission(TenantPermissionNames.*)` except `/me` and `/current`.

## Result: 4 passed, 9 failed (intended-behavior tests document D-E2E-001)

### Passing (4) — actual, correct behavior
- `GET /tenants/me` → 200 (any signed-in user; empty list for the admin).
- `GET /tenants` without a scope → **400** (RequireAccessFilter fails closed on a missing
  scope before the authentication check — not 401).
- `POST /tenants` without a scope → **400** (same reason).
- `GET /tenants/current` for the platform admin (no ambient tenant) → **403**.

### Failing (9) — assert intended success, blocked by D-E2E-001 (403)
- list tenants → 200 (paged)
- create tenant → 201 Active
- get tenant by id → 200
- update tenant name → 200
- suspend then restore → 200
- archive → 200
- set and read tenant settings → 200
- add/list/remove tenant member → 200
- tenant audit log → 200 (paged)

Per Owner decision, these remain failing to document the defect; they are not weakened to 403.

## Defect reference: D-E2E-001 (Administrator role under-provisioned)
The platform Administrator role (AccessBootstrap.cs:47-59) is granted only six `access.*`
permissions. It is **not** granted the `tenants.*` permissions that every Tenant management
route requires (`tenants.platform`, `tenants.create`, `tenants.read`, `tenants.update`,
`tenants.manage_status`, `tenants.delete`, `tenants.settings.*`, `tenants.members.*`,
`tenants.audit.read`). The Admin host even declares `tenants.platform` in its catalog
(Dastarkhwan.Admin/Program.cs:120) but no role ever receives it. Result: the platform admin
gets 403 on all tenant management. This is the same root cause as the Identity user-management
block, extended to Tenant.

## Harness changes (test-only)
- `tenant.spec.ts`: fully rewritten (13 scenarios). Removed the prior version's `test.skip()`
  calls and its dependency on `createUser`/listing (both blocked by D-E2E-001). Uses unique
  names/slugs and the existing admin user id.
- No application code changed.

## Notes / follow-ups
- The no-token tenant expectation is **400 (scope required)**, not 401, because the access
  filter checks scope before authentication. Documented in the passing tests.
- Cross-tenant isolation tests (`cross-tenant.spec.ts`) are Phase 6; they currently fail in
  their setup because they create tenants/users (blocked by D-E2E-001). Not in scope here.
- After D-E2E-001 is fixed (grant `tenants.*` to the Administrator role), all 9 failing tests
  should pass unchanged; re-run to confirm.