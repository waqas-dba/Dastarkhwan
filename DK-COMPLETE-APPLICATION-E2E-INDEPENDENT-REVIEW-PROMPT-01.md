# Independent Review Prompt — DK-COMPLETE-APPLICATION-E2E (for OpenCode)

You are OpenCode, acting as an **independent, read-only reviewer** of a completed end-to-end (E2E)
test campaign for the Dastarkhwan modular monolith. Do not modify application code, tests, schema, or
configuration to make anything pass. Your job is to independently judge, with evidence, whether each
failing test is caused by an **application defect** or a **test defect**, what the **correct behavior**
should be, and whether the application is free of bugs in the areas under review.

## Ground rules
- Read the authoritative project docs first: `CLAUDE.md`, `ai-tasks.md`, `architecture.md`,
  `decisions.md`, `current-state.md`.
- Baseline commit: `6b6fd2a`. Working tree contains test-only changes (specs/helpers) plus new
  `DK-COMPLETE-APPLICATION-E2E-*.md` reports. Do not assume the tree is clean; verify with
  `git status --short` and `git log --oneline -5`.
- Do not change application business logic, auth/authz, schema, or production config. If you believe a
  fix is required, describe it precisely (file/line, change, risk) and label it as requiring Owner
  approval + independent review.
- Do not expose secrets. Refer to credentials by name only.
- Report findings with severity and `file:line` references. Distinguish facts you verified from
  inferences.

## What was done (context)
A Playwright E2E suite runs against the real Web host (`https://localhost:53584`, Development) and a
real PostgreSQL DB (`dastarkhwan_e2e_audit`). Latest full run: **42 passed, 30 failed, 0 flaky (72
tests)**. The campaign produced these reports (read them):
- `DK-COMPLETE-APPLICATION-E2E-EXECUTION-REPORT-01.md` — totals and per-spec results.
- `DK-COMPLETE-APPLICATION-E2E-APPLICATION-DEFECTS-01.md` — D-E2E-001, D-E2E-002.
- `DK-COMPLETE-APPLICATION-E2E-IDENTITY-RESULTS-01.md`
- `DK-COMPLETE-APPLICATION-E2E-ACCESS-RESULTS-01.md`
- `DK-COMPLETE-APPLICATION-E2E-TENANT-RESULTS-01.md`
- `DK-COMPLETE-APPLICATION-E2E-OUTBOX-RESULTS-01.md`
- `DK-COMPLETE-APPLICATION-E2E-CROSS-TENANT-SECURITY-01.md`
- `DK-COMPLETE-APPLICATION-E2E-POSTGRESQL-RESULTS-01.md`
- `DK-COMPLETE-APPLICATION-E2E-REMAINING-ISSUES-01.md`
- `DK-COMPLETE-APPLICATION-E2E-TEST-ARCHITECTURE-01.md`
- `DK-COMPLETE-APPLICATION-E2E-IMPLEMENTATION-REPORT-01.md`

## The failing tests (30) — verify each independently
All 30 are claimed to share one root cause (D-E2E-001). Confirm or refute this claim.

Identity (`tests/e2e/identity.spec.ts`, 8 failing):
1. admin creates, reads, updates and deletes a user — create returns 403
2. duplicate email returns 409 — create returns 403
3. invalid email returns 400 — create returns 403
4. short password returns 400 — create returns 403
5. email is immutable on update — create 403 then update 404
6. non-admin cannot list users — non-admin user cannot be created (403), login 401
7. forgot then reset password — cannot create target user (403)
8. send and verify email — cannot create target user (403)

Tenant (`tests/e2e/tenant.spec.ts`, 9 failing):
9. list tenants → 200 (paged)
10. create tenant → 201 Active
11. get tenant by id → 200
12. update tenant name → 200
13. suspend then restore → 200
14. archive → 200
15. set and read tenant settings → 200
16. add/list/remove tenant member → 200
17. tenant audit log → 200 (paged)
(All observed as 403.)

Cross-tenant (`tests/e2e/cross-tenant.spec.ts`, 13 failing):
18–30. All fail in `beforeEach` fixture setup (create 2 tenants + 2 users → 403). The isolation
assertions themselves were not reached.

## Claimed root cause to verify (D-E2E-001)
The platform **Administrator** role is seeded by
`CoreKit.Access/Services/AccessBootstrap.cs:47-59` with only six `access.*` /
`platform.members.manage` permissions. The `identity.users.*` and `tenants.*` permissions are declared
in the catalog (`Dastarkhwan.Web/Program.cs:153-156`, `Dastarkhwan.Admin/Program.cs:116-120`,
`Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs`) but granted to no role. Therefore
`PermissionBasedIdentityAdminAuthorizer` (Web `Program.cs:281-303`) and `RequireAccessFilter`
(`CoreKit.Access/Endpoints/RequireAccess.cs`) fail closed → 403.

Independently verify:
- The DB state in `dastarkhwan_e2e_audit` (`access.ACCESS_Roles`, `ACCESS_RolePermissions`,
  `ACCESS_Assignments`, `ACCESS_PlatformMembers`) matches the claim (one Administrator role, six
  permissions, no identity/tenants grants).
- Whether any other code path grants these permissions (search the whole repo).
- Whether the 403 is correct-by-design (deny) or a provisioning gap.

## Second claimed defect to verify (D-E2E-002)
`AddTenant` registers `ITenantOutbox` but not the `TenantOutboxPublisherService` hosted service; neither
host calls `AddTenantOutboxPublisher` (`Dastarkhwan.Web/Program.cs:121`, `Dastarkhwan.Admin/Program.cs:104`
are empty placeholders). Confirm via code and runtime logs that the Identity outbox publisher polls but
the Tenant one does not, and judge whether this is a defect or an intentionally deferred cutover.

## Your deliverable (write to `DK-COMPLETE-APPLICATION-E2E-INDEPENDENT-REVIEW-01.md`)
For each failing test (grouped is fine if the cause is identical), state:
- **Classification:** Application defect | Test defect | Correct behavior (test wrong) | Blocked.
- **Evidence:** `file:line` and/or DB/log/command output you actually observed.
- **Correct expected behavior** and the exact status code/contract that should hold.
- **Required action:** app fix (with approval note) or test fix, described precisely.

Then answer explicitly:
1. Are the 30 failures application defects or test defects? (Agree/disagree with D-E2E-001, with proof.)
2. Is the application bug-free in Identity, Access, Tenant, and Outbox? List any additional defects you
   find beyond D-E2E-001/002 (e.g., status-code ordering, validation gaps, security issues).
3. Are the passing 42 tests genuinely correct, or are any asserting the wrong thing (false positives)?
4. Is the test architecture sound (isolation, repeatability, no unexplained skips, no weakened
   assertions)? Flag any test that was weakened to hide a defect.
5. Overall verdict: is the suite a faithful, bug-revealing E2E suite? What must change before the
   application can be called bug-free in the covered areas?

Do not edit files. Return findings only. If you cannot verify something (e.g., a host is not running),
say so explicitly rather than assuming.