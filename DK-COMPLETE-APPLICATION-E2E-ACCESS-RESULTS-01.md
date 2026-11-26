# DK-COMPLETE-APPLICATION-E2E-ACCESS-RESULTS-01

Phase 3 — Access module end-to-end results.

## Baseline
- Commit: `6b6fd2a` (Phase 2 changes still uncommitted; this phase adds to the working tree).
- Host: Web `https://localhost:53584` (Development), real PostgreSQL `dastarkhwan_e2e_audit`.
- Test admin: `admin@e2e-audit.local` (platform Administrator) — holds the six Access
  permissions (`access.roles.read/manage`, `access.assignments.read/manage`,
  `access.audit.read`, `platform.members.manage`).

## Scope
14 Access endpoints under `/access` (AccessEndpointExtensions.cs):
- `GET /access/me`, `GET /access/permissions`
- `GET /access/roles`, `GET /access/roles/{id}`, `POST /access/roles`,
  `PUT /access/roles/{id}`, `PUT /access/roles/{id}/permissions`, `DELETE /access/roles/{id}`
- `GET /access/users/{userId}/roles`, `PUT /access/users/{userId}/roles`
- `GET /access/audit`
- `POST /access/platform/members`, `GET /access/platform/members/{userId}`,
  `DELETE /access/platform/members/{userId}`

## Harness changes (test-only)
- `helpers/apiClient.ts`: added `getRole`, `updateRole`, `setRolePermissions`, `deleteRole`,
  `getUserRoles`, `getAccessAudit`, `getPlatformMember`; added retry-on-429 for write verbs
  (`writeWithRetry`) to absorb the hardcoded Access write limit (20/min fixed window).
- `access.spec.ts`: fully rewritten (20 scenarios). Removed the prior version's defects:
  it granted `tenants.*` permissions the admin does not hold (escalation 403), used
  `test.skip()` (unexplained skips), and depended on `/Identity/users` listing (blocked by
  D-E2E-001). The new suite uses only held permissions, the existing admin user, unique
  role names, and deletes created roles to stay under `MaxRolesPerScope` (50).

## Result: 20 passed, 0 failed
- me returns access context; me without token → 401
- permissions lists catalog (non-empty)
- roles list includes Administrator
- role create/get/update/set-permissions/delete lifecycle (201 → 200 → 200 → 200 → 404)
- duplicate role name → 409
- empty role name → 400
- unknown permission → 400
- granting an unheld permission (escalation) → 403
- stale (non-empty) concurrency stamp → 409
- get unknown role → 404
- list roles without token → 401
- read admin user roles → 200
- assign roles to a non-member user → 400 (no mutation of the admin)
- get user roles without token → 401
- admin is a platform member → 200 isMember=true
- re-adding the admin as member is idempotent (200/201/409 accepted)
- platform membership check without token → 401
- audit log returns paged result (items array)
- audit log without token → 401

## Notes / decisions
- **No application defect found in Access.** The 403s seen in Phase 2 are an Identity
  permission-seeding gap (D-E2E-001), not an Access fault; Access authorization works as designed.
- **Role grants are constrained by `AccessGuard.Holds`** (AccessRoleService.cs:102): an actor
  may only grant permissions it holds. Tests therefore grant `access.roles.read`. The
  escalation test confirms granting `identity.users.create` (in catalog, not held) → 403.
- **Concurrency:** an all-zeros stamp is "stamp required" → 400; a non-empty wrong stamp → 409.
  The test uses a non-empty wrong stamp to exercise the 409 path.
- **Access write rate limit is hardcoded 20/min** (AccessEndpointExtensions.cs:103), not
  configurable. A single suite run stays under it; `writeWithRetry` absorbs boundary 429s so
  back-to-back runs do not fail. No production behavior changed.
- **Platform-member revoke and user-role mutation of the real admin were intentionally not
  exercised** (would risk locking out the only platform admin). Revoke needs a second user,
  which cannot be created until D-E2E-001 is fixed; revisit then.
- Combined run (access + identity): 36 passed, 8 failed (the 8 are the D-E2E-001 Identity
  cascade, unchanged).