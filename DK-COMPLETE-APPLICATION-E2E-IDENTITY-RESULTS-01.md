# DK-COMPLETE-APPLICATION-E2E-IDENTITY-RESULTS-01

Phase 2 — Identity module end-to-end results.

## Baseline
- Commit: `6b6fd2a` (pushed).
- Host: Web `https://localhost:53584` (Development), real PostgreSQL.
- Database (resolved from live host connection): `dastarkhwan_e2e_audit`
  (the host process overrides `ConnectionStrings__{Access,Identity,Tenant}` via env vars;
  the gitignored `appsettings.Development.json` rate-limit override still applies because
  no env var overrides `Identity:RateLimit`).
- Test admin: `admin@e2e-audit.local` / `ResetAdminPass123!`
  (platform Administrator, user id `453207bb-525b-49e4-be96-ec2088d1e7ac`).

## Harness changes (test-only, no app logic touched)
- `helpers/apiClient.ts`: added `sendVerification`, `resendVerification`, `verifyEmail`,
  and dev-inbox getters `getVerificationToken`, `getPasswordResetToken`.
- `identity.spec.ts`: expanded to 24 scenarios across auth, tokens/sessions, user
  management, password recovery, lockout, and email verification.
- `Dastarkhwan.Web/appsettings.Development.json` (gitignored): raised
  `Identity:RateLimit:PermitLimit` to 1000/60s for the audit environment only.
  Production config untouched. This eliminated 429 cascades between tests.

## Result: 16 passed, 8 failed
All 8 failures share a single root cause (see Defect D-E2E-001). No test was weakened
to hide the defect; per Owner decision the affected tests remain failing to document it.

### Passed (16)
- login invalid credentials → 401
- me without token → 401
- valid login returns tokens and user
- login wrong password → 401
- login unknown email → 401
- login missing password → 401 (corrected: empty password is invalid-credentials, not 400)
- me with token returns current user
- me with malformed token → 401
- refresh rotates tokens
- reusing rotated refresh token rejected (replay) → 401
- logout then reusing refresh token rejected
- refresh invalid token → 401
- list users without token → 401
- reset with invalid token → 400
- forgot password unknown account → 204 (corrected: NoContent, not 200)
- five failed attempts lock the account → 401

### Failed (8) — all caused by D-E2E-001
- admin creates, reads, updates and deletes a user — create returns 403
- duplicate email returns 409 — create returns 403 (never reaches duplicate check)
- invalid email returns 400 — create returns 403 (authz precedes validation)
- short password returns 400 — create returns 403
- email is immutable on update — create returns 403, update then 404
- non-admin cannot list users — non-admin user cannot be created (403), login 401
- forgot then reset password — cannot create the target user (403)
- send and verify email — cannot create the target user (403)

## Defect D-E2E-001 (application, authorization)
**Severity: High (blocks all Identity user management).**
- The platform **Administrator** role is seeded by
  `CoreKit.Access/Services/AccessBootstrap.cs:47-59` with only six Access permissions
  (`access.roles.read/manage`, `access.assignments.read/manage`, `access.audit.read`,
  `platform.members.manage`).
- The `identity.users.read/create/update/delete` permissions are declared in the catalog
  (`Dastarkhwan.Web/Program.cs:153-156`, `Dastarkhwan.Admin/Program.cs:116-119`) but are
  **not granted to any role** anywhere in the bootstrap/seeding path.
- Effect: `PermissionBasedIdentityAdminAuthorizer` (Web Program.cs:281-303) checks
  `HasPlatformPermissionAsync("identity.users.create")`, which fails closed → **403** on
  every `/identity/users` write, and read paths likewise.
- Not a test defect and not a stale-DB issue: the access schema in `dastarkhwan_e2e_audit`
  correctly reflects the seed (1 role, 6 permissions, no identity.* grants).

**Required fix (needs Owner approval + independent review — authz change):**
grant `identity.users.read/create/update/delete` to the platform Administrator role in
`AccessBootstrap.BootstrapInitialPlatformAdminAsync` (and keep the catalog in step), or
introduce a dedicated Identity-admin role template that carries them. Re-run this suite
after the fix; all 8 should pass without test changes.

## Notes / follow-ups
- Lockout test leaves one locked throwaway user (unique email); acceptable, no shared state.
- Dev-inbox endpoints (`/dev/verification-token`, `/dev/password-reset-token`) are
  Development-only and were used for recovery/verification token retrieval.
- Rate-limit behavior (429) is out of scope for Phase 2; revisit in the reliability phase
  with the production limit restored.