# DK-COMPLETE-APPLICATION-E2E-CROSS-TENANT-SECURITY-01

Cross-tenant isolation suite — status: **blocked** (cannot execute until D-E2E-001 is fixed).

## Why blocked
`cross-tenant.spec.ts` builds its fixtures in `beforeEach`: it logs in as the platform admin, then
creates two tenants and two users and adds each user to its tenant. Every create call returns **403**
because the Administrator role lacks `tenants.*` and `identity.users.*` (D-E2E-001). All 13 tests
therefore fail in setup, not in their isolation assertions.

## Coverage (ready to run once unblocked)
The suite asserts that a tenant-A user cannot reach tenant-B data, and the positive own-tenant path:
- read tenant B by id → denied
- list tenant B members → denied
- add member to tenant B → denied
- remove member from tenant B → denied
- set owner on tenant B → denied
- get tenant B audit log → denied
- update tenant B → denied
- delete tenant B → denied
- cross-tenant membership manipulation → denied
- own-tenant access (positive) → allowed
- own-tenant member list (positive) → allowed
- unauthenticated access to tenant endpoints → denied
- DB-state isolation check (reads the database to confirm no cross-tenant leakage)

## Harness fix applied
- Added the missing `uniqueName` import (the spec used it in `beforeEach` without importing it). This
  is test-only and makes the suite runnable once D-E2E-001 is resolved.

## Exit criteria
- After D-E2E-001: all 13 should pass. If any isolation assertion fails, that is a genuine
  cross-tenant security defect and must be reported separately with DB before/after evidence
  (per the briefing's cross-tenant write rule).