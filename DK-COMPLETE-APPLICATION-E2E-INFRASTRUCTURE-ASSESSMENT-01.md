# DK-COMPLETE-APPLICATION-E2E — Infrastructure Assessment (DK-COMPLETE-APPLICATION-E2E-INFRASTRUCTURE-ASSESSMENT-01)

**Baseline:** `da45a14`. Read-only. No installs, builds, tests, or DB ops performed.

## 1. Layout
```
tests/e2e/
├── package.json            # placeholder test script (defect)
├── package-lock.json
├── playwright.config.ts    # baseURL protocol/port mismatch (defect)
├── helpers/apiClient.ts    # bearer attached on all verbs (OK)
├── smoke.spec.ts           # 1 test
├── identity.spec.ts        # 2 tests
├── tenant.spec.ts          # 13 tests
├── access.spec.ts          # 15 tests
├── cross-tenant.spec.ts    # 17 tests (invalid .or matcher — defect)
├── admin.spec.ts           # 4 tests (wrong host — defect)
├── playwright-report/      # generated (should be gitignored)
└── test-results/           # generated, .last-run.json = failed (should be gitignored)
```

## 2. Playwright config (`playwright.config.ts`)
- `baseURL: process.env.BASE_URL || 'http://localhost:53584'` — **defect**: Web host HTTPS is `https://localhost:53584`; HTTP is `53585`. Protocol/port mismatch; hosts enforce HTTPS redirect (307).
- No `ignoreHTTPSErrors` — self-signed dev cert will fail.
- `reporter: [['html', { open: 'never' }]]` — **defect**: no CI-friendly reporter (junit/json/line).
- No `timeout`, `retries`, `workers`, `fullyParallel`, `use.trace`, `use.screenshot`, `expect.timeout` — reliability/diagnostics gaps.

## 3. API client (`helpers/apiClient.ts`)
- Bearer token attached via `headers()` for **get/post/put/delete** (lines 29–65) — **OK** (F-1 from `27d122c` is applied; suspected defect resolved).
- `login()` retries on 429/“rate limit” (3 attempts). Auth helpers for refresh/logout/changePassword/forgot/reset present.
- Covers Identity, Access, Tenant, Admin methods. `adminHealthReady()`/`addPlatformMemberAdmin()` target `/admin/*`.
- No `patch` method (no PATCH endpoints exist — OK).
- No built-in tenant-header helper (`X-Tenant-Id`) — tests rely on `tenant_id` JWT claim; cross-tenant header-manipulation scenarios need a new helper.

## 4. Existing specs (52 tests)
| File | describe | tests | Coverage |
|---|---|---|---|
| smoke | 0 | 1 | health reachable (200–499) |
| identity | 1 | 2 | invalid login 401; /me no token 401 |
| tenant | 1 | 13 | list/create/get/update/suspend/archive/restore; me; settings; create+owner; add/remove/setOwner; audit |
| access | 1 | 15 | permissions/roles CRUD; role perms; user-roles; platform members; audit |
| cross-tenant | 1 | 17 | cross-tenant 403/404; same-tenant allowed; unauth 401; API-level isolation |
| admin | 1 | 4 | admin health; unauth 401; add platform member; non-admin 403 |

## 5. Confirmed harness defects (7)
| # | Defect | Location | Evidence |
|---|---|---|---|
| H1 | `npm test` is a placeholder that exits 1 | `tests/e2e/package.json:7` | `echo "Error: no test specified" && exit 1` |
| H2 | baseURL protocol/port mismatch (http vs https 53584) | `playwright.config.ts:6` | host listens https 53584 / http 53585; 307 redirect |
| H3 | No `ignoreHTTPSErrors` for self-signed dev cert | `playwright.config.ts` | absent |
| H4 | Invalid matcher `.toBe(403).or.toBe(404)` | `cross-tenant.spec.ts:79,84,89,94,99,104,109,114,119` | `.or` not a Playwright numeric matcher → runtime error |
| H5 | Admin endpoints tested against Web host URL | `admin.spec.ts:4` (+ cross-tenant/tenant use 53584) | `/admin/*` only on Admin host 53586 |
| H6 | No CI reporters; HTML only | `playwright.config.ts:9` | no junit/json/line |
| H7 | No test-data cleanup (data accumulates) | all specs | no afterEach/afterAll; fixed emails re-created each run → 409 on rerun |

## 6. Suspected issues (need Phase-1 verification)
| # | Issue | Rationale |
|---|---|---|
| S1 | Cross-tenant "DB isolation" test does not query DB | `cross-tenant.spec.ts:140-152` uses API list responses, not DB before/after — fails briefing §9 DB-proof requirement |
| S2 | Hardcoded seed users (`testuser@example.com`, `staff-a@example.com`) | depend on Identity seeding; may not exist / not platform admin |
| S3 | Admin health test expects 200 but endpoint is auth-protected and user may lack platform role | `admin.spec.ts:21-26` |
| S4 | HTTPS redirect (307) may break APIRequestContext on http baseURL | hosts call `UseHttpsRedirection` |
| S5 | No root `package.json`; invocation path for CI unclear | only `tests/e2e/package.json` exists |

## 7. `.gitignore` assessment
- Excludes: .NET artifacts, `TestResults/`, coverage, secrets (`appsettings.Development.json`, `*.env`, `secrets*.json`), OS files. Good.
- **Missing (defect-adjacent):** `node_modules/`, `tests/e2e/node_modules/`, `tests/e2e/playwright-report/`, `tests/e2e/test-results/`. Generated `playwright-report/` and `test-results/` are currently present in tree and not ignored.

## 8. Test-data cleanup strategy
- **None.** No teardown; fixed emails cause duplicate-registration 409 on reruns. Phase 1 must add unique-per-run identifiers (prefix/uuid) and/or afterAll cleanup of created records only.

## 9. Existing reports/diagnostics
- `tests/e2e/playwright-report/index.html` (generated).
- `tests/e2e/test-results/.last-run.json` → `status: failed`, 2 failed tests (identity invalid-login, /me no-token) with `error-context.md` showing `ECONNRESET`/socket hang up (server not running / wrong protocol).
- Repo-root E2E docs: `DK-API-E2E-INDEPENDENT-REVIEW-02-ADDENDUM.md`, `DK-FULL-APPLICATION-E2E-AND-PRODUCTION-READINESS-AUDIT-01-COMPREHENSIVE-REPORT.md`, `AUDIT-EVIDENCE-DK-FULL-E2E-01/{logs,screenshots}`.

## 10. Phase-1 harness fix plan (in approved scope, no app changes)
1. `package.json`: real scripts (`test`, `test:headed`, `test:report`).
2. `playwright.config.ts`: dual baseURL (Web https 53584, Admin https 53586 via env), `ignoreHTTPSErrors: true`, `reporter: [['list'],['html',{open:'never'}],['junit',{outputFile:'results.xml'}]]`, timeouts/retries/workers/trace/screenshot.
3. Fix `.toBe(403).or.toBe(404)` → `expect([403,404]).toContain(res.status())`.
4. Admin spec → Admin baseURL; add platform-admin login fixture or skip-with-reason if seed absent (no silent skip).
5. Fixtures: unique test prefix (uuid), bearer helper for all verbs (already OK), `X-Tenant-Id` helper for header scenarios, afterAll cleanup of created records.
6. `.gitignore`: add node_modules + playwright-report + test-results.
7. Supply Admin `Identity__Jwt__SigningKey` via env (no secret committed).
8. Verify one authenticated request per host (Web + Admin).