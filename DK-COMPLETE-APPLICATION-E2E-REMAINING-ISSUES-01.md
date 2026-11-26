# DK-COMPLETE-APPLICATION-E2E-REMAINING-ISSUES-01

Open issues and follow-ups after the E2E campaign. Ordered by impact.

## 1. D-E2E-001 — Administrator role under-provisioned (High, blocking)
Blocks Identity user management, Tenant management, and Cross-tenant provisioning (30 E2E failures).
Fix: grant `identity.users.*` and `tenants.*` to the platform Administrator role in
`AccessBootstrap.BootstrapInitialPlatformAdminAsync` (authz change — Owner approval + review required).
See APPLICATION-DEFECTS-01.md.

## 2. D-E2E-002 — Tenant outbox publisher not registered (High or deferred)
Tenant integration events are enqueued but never dispatched. Confirm with Owner whether this is
deferred to the "projection cutover"; if not, call `AddTenantOutboxPublisher` in both hosts.
See APPLICATION-DEFECTS-01.md.

## 3. Cross-tenant security suite not executable (blocked by #1)
`cross-tenant.spec.ts` (13 tests) cannot run: its fixtures create two tenants and two users, which
returns 403 under D-E2E-001. Re-run after #1 is fixed. The suite already asserts isolation for
read/list/member-mutation/owner/audit/update/delete plus positive own-tenant access and DB-state
isolation.

## 4. Functional outbox E2E not yet written (blocked by #1)
Add an Outbox spec after #1: create tenant → assert `TenantCreated` row delivered and default Access
roles provisioned; create+delete user → assert cleanup handlers ran. See OUTBOX-RESULTS-01.md.

## 5. Admin host E2E not run this campaign
`admin.spec.ts` targets the Admin host (`https://localhost:53586`), which was not started during this
campaign (the Admin host shares the same DB and the same D-E2E-001 limitation). Run it once the Admin
host is started and #1 is resolved.

## 6. Rate-limit configuration is environment-specific
- Identity limit raised to 1000/60s in the gitignored Web `appsettings.Development.json` (production
  config untouched) to stop inter-test 429 cascades.
- Access (20/min) and Tenant (20/min) write limits are hardcoded and not configurable; the harness
  retries write verbs on 429 (`writeWithRetry`). Reliability-phase work should add a configurable
  limit or a dedicated 429 scenario with the production values restored.

## 7. Documentation drift (pre-existing, not caused by this campaign)
`current-state.md` and `roadmap.md` are stale relative to the code (e.g., D-03/D-18 listed open but
implemented). Update during a documentation pass.

## 8. Dev-only inbox endpoints
`/dev/verification-token` and `/dev/password-reset-token` are Development-only and were used for
recovery/verification tests. Ensure they remain excluded from production builds (verified: guarded by
`IsDevelopment()`).

## Status summary
- No application code was modified during Phases 0–7.
- All remaining issues trace to #1 (and #2 for outbox); the test harness and all non-blocked
  scenarios are green.