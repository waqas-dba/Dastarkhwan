# DK-COMPLETE-APPLICATION-E2E-TEST-ARCHITECTURE-01

How the end-to-end test suite is structured and why.

## Location and tooling
- `tests/e2e/` — Playwright (TypeScript), run via `npx playwright test`.
- Real HTTP against the Development hosts (Web `:53584`, Admin `:53586`) and a real PostgreSQL
  database. No in-memory fakes, no mocked services.

## Layout
- `playwright.config.ts` — HTTPS base URLs, `ignoreHTTPSErrors`, list/junit/html reporters,
  30s timeout, trace + screenshot on failure, `workers: 1` (sequential; the suite shares one DB and
  one admin identity, so parallel workers would collide on unique constraints and rate limits).
- `helpers/config.ts` — `WEB_BASE_URL` / `ADMIN_BASE_URL` (env-overridable).
- `helpers/fixtures.ts` — per-run unique data (`runId`, `uniqueEmail`, `uniqueSlug`, `uniqueName`),
  `TEST_PASSWORD`, admin credentials.
- `helpers/apiClient.ts` — thin typed HTTP client: bearer + `X-Tenant-Id` headers, verb helpers,
  `login`/`refresh`/`logout` (with 429 retry), Identity/Access/Tenant/Admin operations, and
  `writeWithRetry` (retries write verbs on 429 for the hardcoded 20/min Access/Tenant limits).
- Specs: `smoke`, `identity`, `access`, `tenant`, `cross-tenant`, `admin`.

## Principles
- **Repeatable:** unique emails/slugs/names per run; created roles/tenants are deleted where possible
  so `MaxRolesPerScope`/name-uniqueness do not accumulate. The lockout test leaves one throwaway
  locked user (documented).
- **No unexplained skips:** the prior `test.skip()` calls were removed; blocked scenarios assert
  intended behavior and fail (documenting defects) rather than skipping.
- **Defect fidelity:** tests assert intended success; where the app returns 403 due to D-E2E-001 the
  test fails and is recorded, never rewritten to expect 403.
- **Scope ordering awareness:** the Access filter fails closed on a missing scope (400) before the
  auth check (401) before the permission check (403); specs assert the actual ordering.
- **Test-only changes:** no application/business-logic/authz/schema changes were made to pass tests.

## Data and isolation
- One shared development DB and one platform admin identity; isolation between runs is by unique
  identifiers, not separate databases.
- Cross-tenant isolation (Phase 6) is the one suite that needs two tenants + two users; it is blocked
  by D-E2E-001 and will run once that is fixed.

## Out of scope / deferred
- Production configuration and secrets (never touched).
- Load/performance and chaos testing (reliability phase).
- Admin-host spec execution (host not started this campaign).