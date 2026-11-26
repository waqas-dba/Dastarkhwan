# DK-COMPLETE-APPLICATION-E2E-EXECUTION-REPORT-01

Authoritative execution record for the Web-host E2E suite.

## Environment
- Date: 2026-11-26.
- Host: Web `https://localhost:53584` (Development), .NET 10, real PostgreSQL `dastarkhwan_e2e_audit`.
- Admin host (`:53586`) not started this run; `admin.spec.ts` excluded (see Remaining Issues #5).
- Command: `npx playwright test smoke identity access tenant cross-tenant --reporter=line`
  (workers=1, HTTPS, ignoreHTTPSErrors).

## Totals
- **42 passed, 30 failed, 0 flaky** (72 tests).
- All 30 failures share one root cause: D-E2E-001 (Administrator role under-provisioned).

## Per-spec
| Spec | Passed | Failed | Cause of failures |
|------|-------:|-------:|-------------------|
| smoke.spec.ts | 2 | 0 | — |
| identity.spec.ts | 16 | 8 | D-E2E-001 (user-management 403) |
| access.spec.ts | 20 | 0 | — |
| tenant.spec.ts | 4 | 9 | D-E2E-001 (tenant-management 403) |
| cross-tenant.spec.ts | 0 | 13 | D-E2E-001 (fixtures cannot create tenants/users) |
| **Total** | **42** | **30** | |

## Notes
- The 8 Identity + 9 Tenant failures assert intended success (201/200/transitions) and are kept
  failing to document D-E2E-001; they are not weakened to 403.
- The 13 cross-tenant failures occur in fixture setup (create 2 tenants + 2 users), not in the
  isolation assertions themselves.
- No application code was changed during the campaign; all changes are test-only (specs, helpers)
  plus a gitignored Development rate-limit override.
- Re-run after D-E2E-001 is fixed: expect 72/72 (plus the Admin spec once that host is started).