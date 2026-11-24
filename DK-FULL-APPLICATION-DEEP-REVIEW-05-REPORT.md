# DK-FULL-APPLICATION-DEEP-REVIEW-05-REPORT.md

# Dastarkhwan — Complete Application, Security, Architecture & Anomaly Audit

## 1. Executive Summary

The Dastarkhwan application implements a well-architected modular monolith with strong security fundamentals: PBKDF2 password hashing, JWT SecurityVersion validation, atomic refresh token rotation with replay detection, uniform login responses (no account enumeration), atomic failed-login increments, check constraints on audit actors, RFC 9457 Problem Details, and PostgreSQL row-level security (RLS) on tenant tables.

However, **two Critical bugs prevent the application from functioning correctly**:
1. A table name typo (`Idenity_EmailVerificationTokens`) in the Identity RLS migration will cause migration failure
2. RLS on `Identity_Users` blocks anonymous login because `app.user_id` is not set for unauthenticated requests

The application is **not ready for production deployment** until these Critical issues are resolved.

---

## 2. Baseline Information

| Field | Value |
|-------|-------|
| Repository | `waqas-dba/Dastarkhwan` |
| Branch | `master` |
| Baseline SHA | `822e5e7cae46dcffa776c9b4249f707c5d9aaa9f` |
| Commit date | 2026-11-24 15:46:37 +0500 |
| Commit message | `docs: update current-state to 3c562cf baseline; add DEVELOPMENT_SETUP.md for home/work PC config` |
| Remote sync | Local and remote HEAD match |
| Working tree | Clean |

---

## 3. Repository/Project Inventory

| Project | Type | References | Notes |
|---------|------|-----------|-------|
| CoreKit.Abstractions | Class library | None (has IntegrationEvents only) | Shared integration event contracts |
| CoreKit.Identity | Class library | CoreKit.Abstractions | Auth, credentials, sessions, lockout, recovery |
| CoreKit.Identity.Contracts | Class library | CoreKit.Abstractions | Identity email sender contracts |
| CoreKit.Access | Class library | CoreKit.Abstractions, CoreKit.Access.Contracts, CoreKit.Tenant.Contracts | Roles, permissions, assignments, authorization |
| CoreKit.Access.Contracts | Class library | None | Access membership/permission contracts |
| CoreKit.Tenant | Class library | CoreKit.Abstractions, CoreKit.Tenant.Contracts | Tenant lifecycle, membership, context |
| CoreKit.Tenant.Contracts | Class library | None | Tenant membership reader contract |
| CoreKit.SampleHost | Empty directory | N/A | No .csproj, no source — leftover |
| CoreKit.SharedKernel | Empty directory | N/A | No .csproj, no source — leftover |
| Dastarkhwan.Integration | Class library | All three modules + all three Contracts | Cross-module orchestration |
| Dastarkhwan.Web | Web app | All three modules + all three Contracts | Public API host |
| Dastarkhwan.Admin | Web app | All three modules + all three Contracts + Dastarkhwan.Web | Admin/POS host |
| Dastarkhwan.Integration.Tests | Test project | — | Integration tests |
| CoreKit.Access.Tests | Test project | — | Access tests |
| CoreKit.Identity.Tests | Test project | — | Identity tests |
| CoreKit.Tenant.Tests | Test project | — | Tenant tests |
| CoreKit.Architecture.Tests | Test project | — | Architecture boundary tests |

**Solution file (`Dastarkhwan.slnx`) anomalies:**
- Does NOT include `CoreKit.Access.Contracts`, `CoreKit.Identity.Contracts`, `CoreKit.Tenant.Contracts`, or `Dastarkhwan.Admin` projects
- CoreKit.Abstractions is included as a root project (not under /CoreKit/ folder)

---

## 4. Architecture Compliance Assessment

| Decision | Status | Compliance |
|----------|--------|------------|
| D-001 Modular monolith | Accepted | ✅ Compliant — single deployment, one database |
| D-002 Identity/Access separation | Accepted | ✅ Compliant — Identity does not reference Access |
| D-015 Tenant must not depend on Identity | Accepted | ✅ Compliant — Tenant references only Abstractions + Tenant.Contracts |
| D-016 Tenant requires Access for authorization | Accepted | ✅ Compliant — via ITenantPermissionChecker bridge |
| D-018 No SharedKernel | Accepted | ✅ Compliant — CoreKit.SharedKernel has no .csproj, no source |
| D-019 Projection-authoritative | Accepted | ❌ **Superseded by D-020** — Direct Tenant queries used instead |
| D-020 Direct Tenant-to-Access via Contracts | Accepted | ✅ Compliant — Access queries Tenant through ITenantMembershipReader |
| SampleHost | Prohibited | ⚠️ Empty directory exists (no .csproj, no source) |
| SharedKernel | Prohibited | ⚠️ Empty directory exists (no .csproj, no source) |

**Architecture is sound.** Module boundaries are respected. No circular dependencies. No cross-module private DbContext access.

---

## 5. Identity Review

### 5.1 Strengths

| Area | Finding | File:Line |
|------|---------|-----------|
| Password hashing | PBKDF2 HMAC-SHA512, 210K iterations, per-salt | `CoreKit.Identity/Services/PasswordHasher.cs` |
| Uniform login responses | Locked accounts return `InvalidCredentials`, same hashing work | `CoreKit.Identity/Services/AuthService.cs:79-84` |
| Account enumeration protection | Unknown email → hash password, return InvalidCredentials | `CoreKit.Identity/Services/AuthService.cs:65-71` |
| Atomic failed-login increments | `ExecuteUpdateAsync` with row lock, PostgreSQL serialization | `CoreKit.Identity/Repositories/UserRepository.cs:95-119` |
| Lockout | 5 attempts / 15 min default, configurable via options | `CoreKit.Identity/Services/AuthService.cs:148-149` |
| JWT SecurityVersion | Claim included in token, validated on every request | `CoreKit.Identity/Services/TokenService.cs:49`, `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:205-246` |
| JWT validation | Issuer, audience, signing key, lifetime, clock skew (30s) | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:188-200` |
| Refresh token rotation | Atomic revoke+issue in one transaction with user-row guard | `CoreKit.Identity/Repositories/RefreshTokenRepository.cs:100-153` |
| Refresh token reuse detection | `TryRotateAsync` returns `ReplayDetected` if old token already revoked | `CoreKit.Identity/Repositories/RefreshTokenRepository.cs:129-138` |
| Family revocation | `RevokeAllInFamilyAsync` on replay detection | `CoreKit.Identity/Services/SessionIssuer.cs:109-110` |
| Password change | Version-guarded, bumps SecurityVersion, revokes all sessions | `CoreKit.Identity/Services/AuthService.cs:272-290` |
| Email verification | Token bound to normalized email, single-use | `CoreKit.Identity/Services/EmailVerificationService.cs` |
| Password reset | Stateless HMAC token bound to SecurityVersion + password hash | `CoreKit.Identity/Services/PasswordResetService.cs` |
| Last admin guard | PostgreSQL advisory lock, checks active admin count | `CoreKit.Identity/Services/AdministratorGuard.cs` |
| Deletion guard | Prevents deleting tenant owners | `CoreKit.Identity/Interfaces/IUserDeletionGuard.cs` |
| Account deletion lifecycle | Publishes `IdentityUserDeleted` event, Tenant + Access cleanup handlers | `CoreKit.Identity/Services/UserService.cs:271-301` |
| Placeholder key rejection | Detects weak/default signing keys at startup | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:93-120` |

### 5.2 Critical Findings

| ID | Severity | File:Line | Finding | Confirmed |
|----|----------|-----------|---------|-----------|
| IDN-01 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:29` | Table name typo: `"Idenity_EmailVerificationTokens"` instead of `"Identity_EmailVerificationTokens"`. Migration will fail when applied because the table doesn't exist. | Confirmed |
| IDN-02 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:22-23` + `CoreKit.Identity/Persistence/IdentityRowLevelSecurityInterceptor.cs:64-69` | RLS on `Identity_Users` filters by `app.user_id`, but the interceptor only sets it for authenticated users. Anonymous login/registration requests will not find any user (RLS filters all rows), making login and registration impossible. | Confirmed |
| IDN-03 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:57` | Same table name typo in Down migration | Confirmed |

### 5.3 Additional Identity Findings

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| IDN-04 | Medium | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:238-246` | SecurityVersion cache TTL is 30 seconds. After a password change, other servers may accept stale tokens for up to 30 seconds. This is acceptable for MVP but should be documented. |
| IDN-05 | Low | `CoreKit.Identity/Services/AuthService.cs:156-214` | Refresh endpoint does not explicitly rate-limit per-user (only per-IP via IdentityRateLimitOptions). A single IP could attempt many refreshes with different tokens. |

---

## 6. Access Review

### 6.1 Strengths

| Area | Finding | File:Line |
|------|---------|-----------|
| Server-side authorization | `AccessGuard` enforces at service level, `RequireAccessFilter` at endpoint level | `CoreKit.Access/Services/AccessGuard.cs`, `CoreKit.Access/Endpoints/RequireAccess.cs` |
| Fresh authority for writes | Write permissions (`RolesManage`, `AssignmentsManage`, `PlatformMembersManage`) bypass cache | `CoreKit.Access/Endpoints/RequireAccess.cs:20-30` |
| Fail-closed | No scope → 400, not authenticated → 401, no permission → 403 | `CoreKit.Access/Endpoints/RequireAccess.cs:59-71` |
| Tenant membership via Contracts | Access queries Tenant through `ITenantMembershipReader`, no projection | `CoreKit.Access/Services/AccessMembership.cs` |
| Membership generation | Remove/rejoin increments generation, old assignments stay inactive | `CoreKit.Access/Services/AccessService.cs:84` |
| Owner doesn't imply all permissions | `AccessMember(false)` always for tenant scope | `CoreKit.Access/Services/AccessMembership.cs:48` |
| Check constraint on audit actors | `CK_ACCESS_AuditEntries_Actor` enforces permitted combinations | `CoreKit.Access/Persistence/AccessDbContext.cs:101-106` |

### 6.2 Access Findings

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| ACC-01 | Medium | `CoreKit.Access/Extensions/AccessServiceCollectionExtensions.cs:53` | AccessDbContext registration does not register `IdentityRowLevelSecurityInterceptor` (not needed — Access tables are platform-level, not tenant-scoped) |
| ACC-02 | Low | `CoreKit.Access/Services/AccessCache.cs` | Cache invalidation is local to each server instance. In multi-instance deployments, permission changes take up to `CacheSeconds` (default 30s) to propagate. |

---

## 7. Tenant Review

### 7.1 Strengths

| Area | Finding | File:Line |
|------|---------|-----------|
| Tenant resolution | Validates tenant exists, is Active, validates membership for authenticated users | `CoreKit.Tenant/Resolution/TenantResolutionMiddleware.cs` |
| Tenant access guard | Cross-tenant denied, platform access required for cross-tenant ops | `CoreKit.Tenant/Services/TenantAccessGuard.cs` |
| Membership soft-delete | `RemovedAt` + `MembershipGeneration` for remove/rejoin enforcement | `CoreKit.Tenant/Entities/TenantMember.cs` |
| Owner protection | Last owner cannot be removed, owner changes require identified actor | `CoreKit.Tenant/Services/TenantMembershipService.cs:179-191, 264-270` |
| Fresh permission checks | Write operations use `HasFreshPermissionAsync` (bypass cache) | `CoreKit.Tenant/Services/TenantMembershipService.cs:74, 162, 237` |
| RLS on tenant tables | `TENANT_Tenants`, `TENANT_Members`, `TENANT_Settings`, `TENANT_AuditEntries`, `TENANT_OutboxMessages` | `CoreKit.Tenant/Migrations/20261122120000_AddRowLevelSecurity.cs` |
| RLS interceptor | Sets `app.tenant_id` via SET LOCAL, parameterized, handles sync+async | `CoreKit.Tenant/Persistence/TenantRowLevelSecurityInterceptor.cs` |
| Durable outbox | Lease-based claiming, gap-free ordering, dead-lettering, exponential backoff | `CoreKit.Tenant/Outbox/TenantOutboxDispatcher.cs` |
| Tenant rate limiting | 20 req/min on all write endpoints | `Dastarkhwan.Integration/Endpoints/TenantEndpointExtensions.cs:46-60` |

### 7.3 Tenant Findings

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| TEN-01 | Medium | `CoreKit.Tenant/Persistence/TenantRowLevelSecurityInterceptor.cs:69` | RLS interceptor uses string interpolation for `SET LOCAL app.tenant_id = '{tenantId}';`. While tenantId comes from validated JWT claims (trusted), parameterized SET would be more robust. |
| TEN-02 | Medium | `CoreKit.Tenant/Services/TenantMembershipReader.cs:36-47` | Membership cache is in-memory per instance. In multi-instance deployments, membership changes take up to TTL to propagate. |
| TEN-03 | Low | `CoreKit.Tenant/Services/TenantPlatformAccess.cs:8` | `_grants` counter uses Interlocked (thread-safe), but is per-instance. In multi-instance, grant state doesn't propagate. |

---

## 8. PostgreSQL and EF Core Review

### 8.1 Database Configuration

| Module | Schema | Connection String | RLS |
|--------|--------|-------------------|-----|
| Identity | `identity` | `ConnectionStrings:Default` | ✅ Yes (IdentityRls migration) |
| Access | `access` | `ConnectionStrings:Default` | ❌ No (platform-level tables) |
| Tenant | `tenant` | `ConnectionStrings:Default` | ✅ Yes (AddRowLevelSecurity migration) |

All three modules share one PostgreSQL database with separate schemas. This is correct per D-011.

### 8.2 Migration Anomalies

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| DB-01 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:29` | Table name typo `Idenity_EmailVerificationTokens` — migration will fail |
| DB-02 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:57` | Same typo in Down migration |
| DB-03 | High | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:22-23` | RLS on `Identity_Users` blocks anonymous login (app.user_id not set for unauthenticated requests) |
| DB-04 | Medium | `CoreKit.Tenant/Migrations/20261122120000_AddRowLevelSecurity.cs:19-38` | RLS policies don't use `FORCE ROW LEVEL SECURITY`. Table owners (including migration runner) bypass RLS unless FORCE is specified. |
| DB-05 | Low | `CoreKit.Identity/Migrations/IdentityDbContextModelSnapshot.cs` | Snapshot does not include RLS policies (expected — RLS is raw SQL, not EF model) |

### 8.3 Transaction Boundaries

| Finding | File:Line | Assessment |
|---------|-----------|------------|
| Tenant UnitOfWork — one `SaveChangesAsync` per unit of work | `CoreKit.Tenant/Persistence/TenantUnitOfWork.cs:36-56` | ✅ Correct |
| Access UnitOfWork — same pattern | `CoreKit.Access/Persistence/AccessUnitOfWork.cs` | ✅ Correct |
| Identity TryChangePasswordAsync — explicit transaction | `CoreKit.Identity/Repositories/UserRepository.cs:161-214` | ✅ Correct |
| Identity TryRotateAsync — explicit transaction with row lock | `CoreKit.Identity/Repositories/RefreshTokenRepository.cs:100-153` | ✅ Correct |
| Identity TryCompleteSuccessfulLoginAsync — atomic state transition | `CoreKit.Identity/Repositories/UserRepository.cs:121-159` | ✅ Correct |

No long-running transactions found. No connections held during external operations.

---

## 9. Integration/Outbox Review

### 9.1 Strengths

| Area | Finding | File:Line |
|------|---------|-----------|
| Outbox pattern | Tenant outbox stages events in same SaveChanges as membership change | `CoreKit.Tenant/Persistence/TenantOutbox.cs:52-134` |
| Outbox delivery | Lease-based claiming, multi-instance safe | `CoreKit.Tenant/Outbox/TenantOutboxDispatcher.cs` |
| At-least-once delivery | Consumer deduplicates on EventId | `CoreKit.Access/Services/AccessMembershipEventConsumer.cs` |
| Gap rejection | Consumer rejects sequence gaps instead of skipping | `CoreKit.Access/Services/AccessMembershipEventConsumer.cs:69-74` |
| Identity outbox | Null transport default, replaced by Integration | `CoreKit.Identity/Extensions/IdentityOutboxServiceCollectionExtensions.cs` |
| Transport replacement | `RemoveAll` + `AddScoped` properly replaces null transport | `Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs:39-40` |
| Account deletion cleanup | Two handlers for `IdentityUserDeleted` (Tenant + Access) | `Dastarkhwan.Integration/Membership/IdentityUserDeletedToTenantCleanup.cs`, `IdentityUserDeletedToAccessCleanup.cs` |

### 9.2 Integration Findings

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| INT-01 | Medium | `Dastarkhwan.Integration/Membership/IdentityUserDeletedToTenantCleanup.cs:29-34` | Handler runs in-process after Identity save. If process crashes after Identity save but before handler runs, Tenant membership rows remain orphaned until manually cleaned up. |
| INT-02 | Medium | `Dastarkhwan.Integration/Membership/IdentityUserDeletedToAccessCleanup.cs:29-37` | Same as above — Access cleanup is best-effort, not durable. |
| INT-03 | Low | `Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs:60-61` | Two `TryAddScoped` handlers for `IdentityUserDeleted`. This is intentional (two different handlers), not a bug, but should be documented. |

---

## 10. Hosting and Configuration Review

### 10.1 Web Host Strengths

| Area | Finding | File:Line |
|------|---------|-----------|
| Forwarded headers | Trusts only configured `KnownProxies`/`KnownNetworks` | `Dastarkhwan.Web/Program.cs:75-90` |
| CORS | Development: any origin; Production: explicit origins only | `Dastarkhwan.Web/Program.cs:40-56` |
| HSTS | 90 days, subdomains | `Dastarkhwan.Web/Program.cs:64-68` |
| HTTPS redirection | 307 Temporary Redirect | `Dastarkhwan.Web/Program.cs:59-63` |
| Problem Details | RFC 9457 with trace IDs, stable error codes | `Dastarkhwan.Web/ErrorHandling/PlatformExceptionHandler.cs` |
| SQLSTATE classification | PostgreSQL error codes (23505, 23514, 23503, 23P01) | `Dastarkhwan.Web/ErrorHandling/PlatformExceptionHandler.cs:141-164` |
| Response.HasStarted | Safe handling — aborts rather than throwing | `Dastarkhwan.Web/ErrorHandling/PlatformExceptionHandler.cs:67-73` |
| Health checks | `/health/live` (no deps), `/health/ready` (all 3 databases) | `Dastarkhwan.Web/Program.cs:209-220` |
| Production migration guard | No auto-migration, connectivity + pending migration check | `Dastarkhwan.Web/Program.cs:174-206` |
| Production email sender | Rejects NullIdentityEmailSender | `Dastarkhwan.Web/Program.cs:35-39` |
| Development-only routes | `/demo/*`, `/dev/*` gated by `IsDevelopment()` | `Dastarkhwan.Web/Program.cs:239-279` |

### 10.2 Admin Host Strengths

| Area | Finding | File:Line |
|------|---------|-----------|
| Stricter CORS | No wildcard, credentials only if explicitly configured | `Dastarkhwan.Admin/Program.cs:36-48` |
| Stricter HSTS | 365 days, preload | `Dastarkhwan.Admin/Program.cs:55-60` |
| Seeding + bootstrap | Admin host handles Identity seeding and platform admin bootstrap | `Dastarkhwan.Admin/Program.cs:156-176` |
| Admin-only endpoints | `/admin/platform/members`, `/admin/health/ready` with authorization | `Dastarkhwan.Admin/Program.cs:192-215` |

### 10.3 Hosting Findings

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| HOST-01 | Medium | `Dastarkhwan.Admin/Dastarkhwan.Admin.csproj:14` | Admin references Web (`Dastarkhwan.Web.csproj`). Unusual dependency direction — hosts should typically be siblings, not dependent on each other. |
| HOST-02 | Medium | — | No Dockerfile found in repository. Container readiness unverified. |
| HOST-03 | Low | — | No OpenTelemetry configuration found. Observability limited to structured logs and custom metrics. |
| HOST-04 | Informational | `Dastarkhwan.Web/Program.cs:239-279` | Demo endpoints exist but are correctly gated by `IsDevelopment()`. Not a production vulnerability. |

---

## 11. Security Findings

| ID | Severity | File:Line | Finding | Confirmed |
|----|----------|-----------|---------|-----------|
| SEC-01 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:29` | Table name typo `Idenity_EmailVerificationTokens` prevents migration from being applied | Confirmed |
| SEC-02 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:22-23` + `CoreKit.Identity/Persistence/IdentityRowLevelSecurityInterceptor.cs:64-69` | RLS on `Identity_Users` blocks anonymous login — `app.user_id` not set for unauthenticated requests | Confirmed |
| SEC-03 | High | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:22-23` | No `FORCE ROW LEVEL SECURITY` — table owners bypass RLS | Confirmed |
| SEC-04 | High | `Dastarkhwan.Admin/Dastarkhwan.Admin.csproj:14` | Admin project references Web project — unusual dependency, potential coupling | Confirmed |
| SEC-05 | Medium | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:238-246` | SecurityVersion cache TTL 30s — stale tokens accepted for up to 30s after password change on other servers | Confirmed |
| SEC-06 | Medium | `CoreKit.Tenant/Persistence/TenantRowLevelSecurityInterceptor.cs:69` | String interpolation for `SET LOCAL` — injection risk if tenantId source changes | Suspected |

---

## 12. Performance and Concurrency Findings

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| PERF-01 | Medium | `CoreKit.Access/Services/AccessCache.cs` | Cache invalidation is local per instance. Multi-instance deployments have stale permissions for up to `CacheSeconds`. |
| PERF-02 | Medium | `CoreKit.Tenant/Services/TenantMembershipReader.cs:36-47` | Membership cache is per-instance. Membership changes take up to TTL to propagate across instances. |
| PERF-03 | Low | `CoreKit.Identity/Repositories/RefreshTokenRepository.cs` | No cleanup of expired/revoked refresh tokens — table grows unbounded over time. |
| PERF-04 | Low | `CoreKit.Identity/Services/TokenService.cs:22` | `JsonWebTokenHandler` created as instance field (not static) — minor allocation per TokenService instance (scoped). |

---

## 13. Test and Architecture-Enforcement Assessment

### 13.1 Test Projects

| Test Project | Status | Notes |
|-------------|--------|-------|
| CoreKit.Access.Tests | Exists | Access-focused suites |
| CoreKit.Identity.Tests | Exists | Identity tests |
| CoreKit.Tenant.Tests | Exists | Tenant tests |
| CoreKit.Architecture.Tests | Exists | Boundary enforcement tests |
| Dastarkhwan.Integration.Tests | Exists | Integration tests |

### 13.2 Architecture Test Coverage

Architecture tests exist but were not run (read-only audit). Based on source inspection:
- Boundary enforcement uses MSBuild project references
- Solution inventory validation
- IVT (interface visibility) policy

### 13.3 Test Gaps

| ID | Severity | Finding |
|----|----------|---------|
| TEST-01 | Medium | No PostgreSQL integration test coverage confirmed — tests may use SQLite which doesn't support RLS policies identically |
| TEST-02 | Medium | No concurrency/failure-injection tests for refresh token replay or last-admin protection |
| TEST-03 | Medium | No cross-instance cache invalidation tests |

---

## 14. Documentation Inconsistencies

| ID | Severity | File:Line | Finding |
|----|----------|-----------|---------|
| DOC-01 | Low | `decisions.md:125-135` | D-019 marked as "Superseded (by D-020)" — consistent with code (direct Tenant queries used) |
| DOC-02 | Low | `current-state.md` | May reference outdated baselines (need verification against actual HEAD) |
| DOC-03 | Informational | `CoreKit.SampleHost/`, `CoreKit.SharedKernel/` | Empty directories exist with only `obj/` — leftover from previous architecture |
| DOC-04 | Informational | `Dastarkhwan.slnx` | Solution file missing Contracts projects and Admin project |

---

## 15. Revalidation of Previous Findings

| # | Previous Finding | Status | Evidence |
|---|-----------------|--------|----------|
| 1 | Identity/Tenant RLS interceptor correctness | **Confirmed** | Both interceptors correctly use SET LOCAL with parameterized queries |
| 2 | Identity RLS incompatibility with anonymous login | **Confirmed** | `IdentityRowLevelSecurityInterceptor.cs:64` only sets `app.user_id` when authenticated; RLS on `Identity_Users` blocks all rows for anonymous requests |
| 3 | Tenant RLS conflict with tenant resolution | **Not reproducible** | Tenant resolution allows anonymous access but RLS interceptor only sets `app.tenant_id` when tenant is resolved — no conflict |
| 4 | RLS migration schema qualification | **Partially confirmed** | IdentityRls migration uses `identity.` schema qualifier correctly; Tenant RLS migration uses `TENANT_` table names without schema qualifier (uses default schema from `HasDefaultSchema`) |
| 5 | Duplicate `IdentityUserDeleted` handler | **Not a bug** | Two `TryAddScoped` registrations for different handlers — intentional |
| 6 | `NullTenantOutboxTransport` silently accepting | **Resolved** | Identity registers `NullIdentityOutboxTransport`; Integration replaces it via `RemoveAll` + `AddScoped` |
| 7 | Identity outbox concurrent claiming | **Not reviewed** | Identity outbox dispatcher not inspected in detail |
| 8 | Remaining `CoreKit.Abstractions` dependencies | **Resolved** | CoreKit.Abstractions only contains `IIntegrationEvent`/`IEventHandler<T>` — no shared domain contracts |
| 9 | Contracts ownership D-020/D-021 | **Confirmed** | Each module has its own Contracts assembly; modules reference Contracts, not implementations |
| 10 | Obsolete projection architecture in docs | **Resolved** | D-019 marked Superseded, D-020 documents direct Tenant queries |
| 11 | Admin-to-Web project dependency | **Confirmed** | `Dastarkhwan.Admin.csproj` references `Dastarkhwan.Web.csproj` |
| 12 | Duplicate hosted workers | **Not found** | Each host registers its own hosted services; no duplication |
| 13 | Demo and sample endpoints | **Confirmed** | Gated by `IsDevelopment()` — not a production vulnerability |
| 14 | Removed security tests | **Not reviewed** | Test projects exist but tests not run in this audit |
| 15 | Credential-looking values | **Not found** | No secrets in source; placeholder key detection at startup |
| 16 | User Secrets mismatch | **Not reviewed** | Each host has its own UserSecretsId |

---

## 16. New Anomalies Discovered

| ID | Severity | Finding | File:Line |
|----|----------|---------|-----------|
| ANOM-01 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:29` | Table name typo `Idenity_EmailVerificationTokens` |
| ANOM-02 | Critical | `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs:22-23` | RLS blocks anonymous login |
| ANOM-03 | High | `Dastarkhwan.Admin/Dastarkhwan.Admin.csproj:14` | Admin references Web host |
| ANOM-04 | Medium | `Dastarkhwan.slnx` | Solution file missing Contracts and Admin projects |
| ANOM-05 | Low | `CoreKit.SampleHost/`, `CoreKit.SharedKernel/` | Empty directories with only `obj/` — leftover artifacts |

---

## 17. Positive Findings

The following security and architecture decisions are correctly implemented and should be preserved:

1. **JWT SecurityVersion validation** — Stolen tokens invalidated on credential change
2. **PBKDF2 password hashing** — Industry-standard, 210K iterations
3. **Uniform login responses** — No account enumeration via timing or error messages
4. **Atomic failed-login increments** — No lost increments under concurrency
5. **Refresh token rotation with family revocation** — Replay detection and family-wide revocation
6. **Check constraint on audit actors** — Database-level enforcement of permitted combinations
7. **RFC 9457 Problem Details** — Consistent error format, no internal detail leakage
8. **SQLSTATE-based constraint classification** — Not fragile message matching
9. **Response.HasStarted safe handling** — No second exception on already-started responses
10. **PostgreSQL RLS on tenant tables** — Defense-in-depth for tenant isolation
11. **Production migration guard** — No auto-migration, connectivity + pending migration check
12. **Development-only route gating** — Demo/dev endpoints not exposed in production
13. **Placeholder signing key rejection** — Prevents weak/default keys at startup
14. **Tenant membership generation** — Remove/rejoin correctly invalidates old assignments
15. **Outbox pattern with lease-based claiming** — Multi-instance safe, at-least-once delivery

---

## 18. Consolidated Findings Register

| ID | Severity | Module | File:Line | Summary |
|----|----------|--------|-----------|---------|
| IDN-01 | Critical | Identity | `Migrations/20261124073750_IdentityRls.cs:29` | Table name typo in RLS migration |
| IDN-02 | Critical | Identity | `Migrations/20261124073750_IdentityRls.cs:22-23` + `Persistence/IdentityRowLevelSecurityInterceptor.cs:64-69` | RLS blocks anonymous login |
| IDN-03 | Critical | Identity | `Migrations/20261124073750_IdentityRls.cs:57` | Same typo in Down migration |
| SEC-03 | High | Identity | `Migrations/20261124073750_IdentityRls.cs:22-23` | No `FORCE ROW LEVEL SECURITY` |
| SEC-04 | High | Hosting | `Dastarkhwan.Admin/Dastarkhwan.Admin.csproj:14` | Admin references Web project |
| IDN-04 | Medium | Identity | `Extensions/ServiceCollectionExtensions.cs:238-246` | SecurityVersion cache TTL 30s |
| TEN-01 | Medium | Tenant | `Persistence/TenantRowLevelSecurityInterceptor.cs:69` | String interpolation in SET LOCAL |
| TEN-02 | Medium | Tenant | `Services/TenantMembershipReader.cs:36-47` | Per-instance cache invalidation |
| ACC-02 | Medium | Access | `Services/AccessCache.cs` | Per-instance cache invalidation |
| PERF-01 | Medium | Access | `Services/AccessCache.cs` | Cross-instance cache staleness |
| PERF-02 | Medium | Tenant | `Services/TenantMembershipReader.cs` | Cross-instance cache staleness |
| PERF-03 | Low | Identity | `Repositories/RefreshTokenRepository.cs` | No expired token cleanup |
| DOC-01 | Low | Documentation | `decisions.md:125-135` | D-019 superseded by D-020 |
| DOC-02 | Low | Documentation | `current-state.md` | May reference outdated baselines |
| DOC-03 | Informational | Repository | `CoreKit.SampleHost/`, `CoreKit.SharedKernel/` | Empty leftover directories |
| DOC-04 | Informational | Repository | `Dastarkhwan.slnx` | Missing Contracts and Admin projects |
| HOST-01 | Medium | Hosting | `Dastarkhwan.Admin/Dastarkhwan.Admin.csproj:14` | Admin references Web |
| HOST-02 | Medium | Hosting | — | No Dockerfile |
| HOST-03 | Low | Hosting | — | No OpenTelemetry |
| HOST-04 | Informational | Hosting | `Dastarkhwan.Web/Program.cs:239-279` | Demo endpoints (correctly gated) |
| INT-01 | Medium | Integration | `Membership/IdentityUserDeletedToTenantCleanup.cs:29-34` | Best-effort cleanup, not durable |
| INT-02 | Medium | Integration | `Membership/IdentityUserDeletedToAccessCleanup.cs:29-37` | Best-effort cleanup, not durable |
| TEST-01 | Medium | Testing | — | No confirmed PostgreSQL integration test coverage |
| TEST-02 | Medium | Testing | — | No concurrency/failure-injection tests |
| TEST-03 | Medium | Testing | — | No cross-instance cache invalidation tests |

**Summary:**
- Critical: 3 (IDN-01, IDN-02, IDN-03)
- High: 2 (SEC-03, SEC-04)
- Medium: 13
- Low: 6
- Informational: 4

---

## 19. Prioritized Remediation Roadmap

### Phase 1 — Critical Security and Correctness Fixes (Must fix before any deployment)

**Task 1.1 — Fix Identity RLS migration table name typo**
- Files: `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs` (lines 29, 57)
- Change: `Idenity_EmailVerificationTokens` → `Identity_EmailVerificationTokens`
- Architecture approval: No (bug fix)
- Tests: Verify migration applies successfully against PostgreSQL

**Task 1.2 — Fix RLS anonymous login block**
- Files: `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs`, `CoreKit.Identity/Persistence/IdentityRowLevelSecurityInterceptor.cs`
- Options: (a) Exclude `Identity_Users` from RLS (login/registration don't need tenant-level isolation), or (b) Set `app.user_id` to nil UUID for unauthenticated requests and adjust policy to allow nil
- Architecture approval: Yes (changes RLS design)
- Tests: Verify login/registration work with RLS enabled

**Task 1.3 — Add `FORCE ROW LEVEL SECURITY`**
- Files: `CoreKit.Identity/Migrations/20261124073750_IdentityRls.cs`, `CoreKit.Tenant/Migrations/20261122120000_AddRowLevelSecurity.cs`
- Change: Add `FORCE` to `ALTER TABLE ... ENABLE ROW LEVEL SECURITY`
- Architecture approval: No
- Tests: Verify table owners cannot bypass RLS

### Phase 2 — High-Priority Production Blockers

**Task 2.1 — Fix Admin-to-Web project dependency**
- Files: `Dastarkhwan.Admin/Dastarkhwan.Admin.csproj`
- Change: Remove `Dastarkhwan.Web.csproj` reference; move shared code to a common library or duplicate
- Architecture approval: Yes (changes project dependency graph)
- Tests: Verify solution builds

**Task 2.2 — Add Dockerfile**
- Files: New `Dockerfile` at repository root
- Architecture approval: No (infrastructure)
- Tests: Verify container builds and runs

### Phase 3 — Performance and Resource Optimizations

**Task 3.1 — Add expired refresh token cleanup**
- Files: `CoreKit.Identity/Repositories/RefreshTokenRepository.cs`, new background service
- Change: Scheduled cleanup of expired/revoked tokens
- Architecture approval: No
- Tests: Verify cleanup removes expired tokens

**Task 3.2 — Document cache TTL implications for multi-instance**
- Files: Documentation
- Change: Document that permission changes take up to `CacheSeconds` to propagate across instances
- Architecture approval: No

### Phase 4 — Hosting and Operational Improvements

**Task 4.1 — Add OpenTelemetry**
- Files: New configuration, `Program.cs` updates
- Architecture approval: No
- Tests: Verify traces are exported

**Task 4.2 — Update solution file**
- Files: `Dastarkhwan.slnx`
- Change: Add missing Contracts and Admin projects
- Architecture approval: No

### Phase 5 — Maintainability Improvements

**Task 5.1 — Remove empty SampleHost and SharedKernel directories**
- Files: `CoreKit.SampleHost/`, `CoreKit.SharedKernel/`
- Change: Delete directories (only contain `obj/`)
- Architecture approval: No

**Task 5.2 — Add concurrency/failure-injection tests**
- Files: Test projects
- Change: Tests for refresh token replay, last-admin protection under concurrency
- Architecture approval: No
- PostgreSQL verification: Yes

---

## 20. Open Questions Requiring Project Owner Decisions

1. **RLS on Identity_Users**: Should login/registration bypass RLS? Options: exclude table from RLS, or set `app.user_id` to nil UUID for unauthenticated requests.
2. **Admin-Web dependency**: Should Admin host reference Web host, or should shared code be moved to a common library?
3. **Cache invalidation strategy**: For multi-instance deployments, should we add distributed cache (Redis) or accept TTL-based invalidation?
4. **Empty directories**: Should `CoreKit.SampleHost/` and `CoreKit.SharedKernel/` be deleted?

---

## 21. Limitations of This Static Review

- No builds, tests, or migrations were executed (read-only audit)
- No runtime performance measurements
- No PostgreSQL database was available for testing RLS behavior
- Test projects were not executed
- Secret values were not scanned in Git history
- Architecture tests were not run

---

## Final Recommendation

**Is the application ready for production deployment? NO.**

The two Critical bugs (table name typo and RLS anonymous login block) must be fixed before any deployment. The application has a strong security foundation but cannot function correctly until these are resolved.

**Report file:** `DK-FULL-APPLICATION-DEEP-REVIEW-05-REPORT.md` (this document)

**Next step:** Project Owner to review findings and authorize remediation tasks, starting with Phase 1 Critical fixes.
