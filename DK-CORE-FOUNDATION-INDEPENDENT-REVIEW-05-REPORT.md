# DK-CORE-FOUNDATION-INDEPENDENT-REVIEW-05

## Complete Application Architecture, Security and Next-Module Readiness Audit

**Role:** Independent Senior Software Architect, .NET Security Reviewer and Application Auditor  
**Repository:** `waqas-dba/Dastarkhwan`  
**Branch:** `master`  
**Review type:** Independent, read-only, evidence-based audit  
**Review date:** 2026-11-28  
**Status:** Final report (no implementation performed; read-only review)

---

## A. Executive Verdict

**CONDITIONAL GO — Only limited, isolated business-module work may start while specified non-blocking issues remain.**

### Decision rationale

The foundation (Identity, Access, Tenant, Integration, Web) is substantially implemented and demonstrates genuine security engineering: atomic refresh-token rotation with replay detection and family revocation, concurrent-safe lockout, fail-closed authorization, JWT `secver` validation, tenant suspended-status enforcement, email immutability, and outbox-based cross-module event delivery. The code shows real attention to atomicity (single `SaveChanges`, row guards, conditional updates).

However, the foundation is NOT yet ready for unrestricted business-module implementation. There are **P0 boundary violations** (Identity referencing Access.Contracts), a **stale documentation baseline** (current-state.md does not reflect the actual HEAD commit), **unimplemented accepted decisions** (D-021 removal of CoreKit.Access.Contracts), **migration disorganization** (dual migration folders, "PendingFix" migrations), and **missing public self-registration**. These must be corrected or explicitly accepted as technical debt before the first business module (Business, Store, Catalog/Product, Subscription) builds on top.

Business-module work may begin in isolated, read-only-adjacent slices (e.g., Store/Catalog domain model behind Tenant-owned contracts) **only after** the P0 boundary violation and documentation-staleness issues are resolved. The boundary violation means Identity currently depends on Access, so any module that touches Identity transitively pulls in Access — violating the "Standalone Identity must not require Tenant/Access infrastructure" constraint.

---

## B. Repository and Review Baseline

| Item | Value |
|---|---|
| Repository | `waqas-dba/Dastarkhwan` (local working copy) |
| Branch | `master` |
| Baseline SHA (local HEAD) | `fc4a08976b737d47464365477944e9c5cd328b42` |
| Baseline commit date | 2026-11-28 21:28:56 +0500 |
| Baseline commit message | `chore: update ports to 5000/5001 in launchSettings and Postman files` |
| Remote `origin` HEAD SHA | `fc4a08976b737d47464365477944e9c5cd328b42` (matches local) |
| Working tree | Clean (`git status --short` empty) |
| Review projects inspected | 14 projects: CoreKit.Identity, CoreKit.Identity.Contracts, CoreKit.Access, CoreKit.Access.Contracts, CoreKit.Tenant, CoreKit.Tenant.Contracts, Dastarkhwan.Integration, Dastarkhwan.Web, CoreKit.Identity.Tests, CoreKit.Access.Tests, CoreKit.Tenant.Tests, Dastarkhwan.Integration.Tests, CoreKit.Architecture.Tests, HashGen |
| Access limitation | Read-only only. No builds, tests, migrations, or database operations executed. Runtime behavior (EF Core transaction enlistment, PostgreSQL RLS, migration application) verified statically only — flagged as "Requires runtime verification" where relevant. |
| Documentation reliability | **current-state.md is STALE** — references HEAD `3c562cf` / `d641921`, not actual `fc4a0897`. All current-state.md claims re-verified against actual source. |

---

## C. Module Completion Matrix

| Module | Status | Evidence |
|---|---|---|
| Identity | **Reported complete (not independently verified)** | Registration (admin-only), login, refresh rotation, lockout, JWT secver validation, outbox, email verification, password recovery implemented. No public self-registration endpoint. Build not executed by reviewer. |
| Access | **Reported complete (not independently verified)** | Roles, permissions, assignments, authorization, platform membership, caching, fail-closed. Build not executed. |
| Tenant | **Reported complete (not independently verified)** | Tenant lifecycle, context resolution, middleware, membership, outbox, suspended-status enforcement. Build not executed. |
| Contracts | **In progress** | Module-owned Contracts assemblies exist, but CoreKit.Access.Contracts should be removed per D-021 (not done). Duplicate contract types between Identity/Interfaces and Identity.Contracts. |
| Integration | **In progress** | Event handlers registered for tenant lifecycle and identity deletion cleanup. But duplicates `TenantClaimsContributor`/`PlatformAdminClaimsContributor` registrations. References Tenant.Persistence (TenantDbContext) — boundary risk. |
| Web | **Completed (verified via source inspection)** | Single host, middleware order, JWT auth, tenant resolution, problem details, health checks, dev-only endpoints. Not executed at runtime. |
| Database and persistence | **Under review** | Three DbContexts, migrations committed, schema per module. Migration application to PostgreSQL not verified. Dual Identity migration folders. RLS interceptors removed (per P1A). |
| Background processing | **In progress** | Identity outbox dispatcher + hosted services, Tenant outbox dispatcher. Lease/renewal and poison-message handling not verified at runtime. |
| Architecture boundaries | **In progress** | Mostly correct at project-reference level, but Identity → Access.Contracts violates D-002. CoreKit.Access.Contracts should not exist per D-021. |
| Security | **Under review** | Strong token/lockout/tenant enforcement, but secver cache has 30s stale window. No public registration. Runtime verification of JWT validation not performed. |
| Performance | **Under review** | In-memory caches with TTL, bounded pagination, no N+1 observed in inspected code. Not measured. |
| Testing coverage | **In progress** | Tests exist but many fail due to SQLite/PostgreSQL incompatibility (pg_advisory_xact_lock, chr). Integration tests minimal (1 file). Architecture tests 14/53 failing. |

---

## D. Architecture Dependency Matrix

### Actual project references

```
CORE CONTRACTS (leaf, no project refs):
  CoreKit.Identity.Contracts
  CoreKit.Access.Contracts
  CoreKit.Tenant.Contracts

MODULES:
  CoreKit.Identity          → CoreKit.Access.Contracts  [VIOLATION: D-002]
                            → CoreKit.Identity.Contracts
  CoreKit.Access            → CoreKit.Access.Contracts  [D-021 says remove]
                            → CoreKit.Tenant.Contracts  [D-020 compliant]
  CoreKit.Tenant            → CoreKit.Tenant.Contracts

COMPOSITION / HOST:
  Dastarkhwan.Integration   → CoreKit.Identity
                            → CoreKit.Access
                            → CoreKit.Tenant
                            → CoreKit.Identity.Contracts
                            → CoreKit.Access.Contracts  [D-021 says remove]
                            → CoreKit.Tenant.Contracts
                            → CoreKit.Tenant.Persistence [boundary risk: direct Tenant DbContext access]
  Dastarkhwan.Web           → CoreKit.Access
                            → CoreKit.Identity
                            → CoreKit.Tenant
                            → Dastarkhwan.Integration
                            → CoreKit.Identity.Contracts
                            → CoreKit.Access.Contracts  [D-021 says remove]
                            → CoreKit.Tenant.Contracts

TESTS:
  CoreKit.Identity.Tests    → CoreKit.Identity
  CoreKit.Access.Tests      → CoreKit.Access
  CoreKit.Tenant.Tests      → CoreKit.Tenant, CoreKit.Access, Dastarkhwan.Integration
  Dastarkhwan.Integration.Tests → Dastarkhwan.Integration, CoreKit.Access, CoreKit.Tenant
  CoreKit.Architecture.Tests → CoreKit.Access, CoreKit.Identity, CoreKit.Tenant, Dastarkhwan.Integration

TOOLS:
  HashGen                   → CoreKit.Identity
```

### Dependency violations

| # | Project | References | Violates | Explanation |
|---|---|---|---|---|
| V1 | `CoreKit.Identity` | `CoreKit.Access.Contracts` | **D-002** (Identity and Access separate) | Identity must not depend on Access at all. `SessionIssuer.cs` uses `IAccessTokenClaimsContributor` from Access.Contracts. This means Identity cannot be used standalone without Access contracts. |
| V2 | `CoreKit.Identity` | `CoreKit.Access.Contracts` | **D-018/D-019** (zero direct CoreKit references between modules) | Transitively, Identity depends on Access (through Access.Contracts). Standalone Identity requirement broken. |
| V3 | `CoreKit.Access` | `CoreKit.Access.Contracts` | **D-021** (remove Access.Contracts assembly) | Accepted decision D-021 (2026-11-23) says remove CoreKit.Access.Contracts entirely. It still exists and is referenced. |
| V4 | `Dastarkhwan.Integration` | `CoreKit.Access.Contracts` | **D-021** | Same as V3. |
| V5 | `Dastarkhwan.Web` | `CoreKit.Access.Contracts` | **D-021** | Same as V3. |
| V6 | `Dastarkhwan.Integration` | `CoreKit.Tenant.Persistence` | **Architecture §3** (no private DbContext access) | Integration references Tenant implementation project, gaining access to TenantDbContext. While not clearly used in inspected code, the reference enables boundary bypass. |
| V7 | `Dastarkhwan.Web` | `CoreKit.Access`, `CoreKit.Identity`, `CoreKit.Tenant` (impl) | **Architecture §7** (Web must use Integration for composition) | Web references module implementation projects directly, bypassing Integration as the composition layer. |
| V8 | `CoreKit.Tenant.Tests` | `CoreKit.Access`, `Dastarkhwan.Integration` | **Standalone module testability** | Tenant tests are coupled to Access and Integration; Tenant cannot be tested in isolation. |

### Dependency diagram

```
                          ┌─────────────────────────┐
                          │  CoreKit.Identity.Contracts│ (leaf)
                          └───────────┬─────────────┘
                                      │
                          ┌───────────▼─────────────┐
                          │   CoreKit.Identity        │───┐
                          │  (references Access.Contracts)│
                          └───────────┬─────────────┘  │
                                      │                  │
                          ┌───────────▼─────────────┐  │
                          │  CoreKit.Access.Contracts │◄──┘ [D-021: REMOVE]
                          └───────────┬─────────────┘
                                      │
                          ┌───────────▼─────────────┐
                          │   CoreKit.Access          │
                          │  (references Tenant.Contracts)│
                          └───────────┬─────────────┘
                                      │
                          ┌───────────▼─────────────┐
                          │  CoreKit.Tenant.Contracts│ (leaf)
                          └───────────┬─────────────┘
                                      │
                          ┌───────────▼─────────────┐
                          │   CoreKit.Tenant          │
                          └───────────┬─────────────┘
                                      │
              ┌───────────────────────┼───────────────────────┐
              │                       │                       │
┌─────────────▼─────────────┐ ┌───────▼────────┐ ┌───────────▼────────┐
│  Dastarkhwan.Integration  │ │Dastarkhwan.Web │ │   Test projects   │
│  (references all impl +   │ │ (references all│ │ (coupled to impl  │
│   all contracts)          │ │   impl + Int)  │ │   projects)      │
└───────────────────────────┘ └────────────────┘ └────────────────────┘
```

---

## E. Security Assessment

### Confirmed vulnerabilities

None at Critical severity. The code demonstrates genuine security engineering.

### Suspected weaknesses

| ID | Severity | Status | Location | Description |
|---|---|---|---|---|
| S-01 | High | Confirmed | `CoreKit.Identity/Services/SessionIssuer.cs:2`, `CoreKit.Identity.csproj:12` | Identity references `CoreKit.Access.Contracts` and uses `IAccessTokenClaimsContributor`. Identity depends on Access, violating D-002. Standalone Identity broken. |
| S-02 | Medium | Confirmed | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:248` | JWT `secver` (SecurityVersion) validation cache TTL is 30 seconds. After a password change/deactivation, a stolen access token remains valid for up to 30 seconds. Documented trade-off but a real window. |
| S-03 | Medium | Suspected | `Dastarkhwan.Web/Program.cs:218-219` | Middleware order: `UseIdentity()` (auth) then `UseTenant()` (tenant resolution). Access endpoints under `/access` are NOT in `ExcludedPathPrefixes` (`/identity`, `/tenants/me`, `/health`, `/favicon.ico`, `/.well-known`). A platform admin with a `tenant_id` JWT claim but not a member of that tenant gets 403 from `TenantResolutionMiddleware` before Access authorization runs. |
| S-04 | Medium | Confirmed | `CoreKit.Identity/Services/UserService.cs:291-294` | `IdentityOutboxPublisher.PublishAsync` calls `SaveChangesAsync()` separately. In `UserService.DeleteAsync`, the outbox message and user deletion are within an explicit transaction, but EF Core `SaveChangesAsync` may not auto-enlist in `Database.CurrentTransaction` — requires runtime verification to confirm atomicity. |
| S-05 | Low | Confirmed | `Dastarkhwan.Web/Program.cs:45-47` | CORS in Development allows any origin when no `Cors:AllowedOrigins` configured. Acceptable for dev but must be locked down in production. |
| S-06 | Low | Confirmed | `CoreKit.Identity/Models/JwtOptions.cs:10` | Default `AccessTokenMinutes = 15`, `RefreshTokenDays = 14`. Reasonable but should be confirmed against security policy. |
| S-07 | Low | Suspected | `CoreKit.Access/Services/AccessCache.cs:75` | Access cache TTL defaults to `CacheSeconds` (config-driven, default unknown). Stale permission data possible until TTL expiry even after `InvalidateUser`/`InvalidateScope` bumps generation (old cache entry unreachable by key but occupies memory until expiry). |

### No hard-coded secrets

Confirmed: No connection strings, signing keys, API keys, or credentials in tracked source code. Configuration uses `${VAR_NAME}` expansion from environment variables (`Dastarkhwan.Web/Configuration/EnvironmentVariableExpander.cs`). Test files contain test-only passwords ("wrong-password", "super-secret-value") which is acceptable.

### Runtime verification required

| Item | Location | Why runtime verification needed |
|---|---|---|
| JWT `secver` validation actually rejects stale tokens | `ServiceCollectionExtensions.cs:215-256` | Not executed; cannot confirm JwtBearer events fire correctly |
| Outbox atomicity (outbox + user deletion in one transaction) | `UserService.cs:283-297`, `IdentityOutboxPublisher.cs:40-41` | EF Core `SaveChangesAsync` enlistment in ambient `DbContextTransaction` unconfirmed |
| PostgreSQL advisory lock for `TryRotateAsync` / `TryIssueGuardedAsync` | `RefreshTokenRepository.cs:34-67, 119-169` | Row-lock behavior on PostgreSQL vs SQLite differs; tests run on SQLite |
| Migration application to PostgreSQL | All migration folders | Committed migrations not applied/verified by reviewer |
| RLS (removed interceptors) | `ServiceCollectionExtensions.cs:49`, `TenantServiceCollectionExtensions.cs:53` | RLS interceptors removed per P1A; no RLS in code. Needs confirmation whether RLS is needed at all |

---

## F. Future-Module Readiness Matrix

| Module | Supported by foundation | Missing | Blocker? | Owner |
|---|---|---|---|---|
| Business | Tenant existence, tenant context, tenant status (Active/Suspended/Deleted) | Business entity ownership, business lifecycle | No | Business module |
| Store | Tenant context, tenant membership authorization | Store entity, store ownership, store lifecycle | No | Store module |
| Branch | Tenant isolation, tenant-scoped data | Branch entity, branch-level authorization | No | Branch module |
| Catalog | Module contracts pattern, tenant-scoped data access | Catalog entities, product ownership, catalog authorization | No | Catalog module |
| Product | Ownership patterns, validation patterns | Product entity, pricing, authorization | No | Product module |
| Subscription | Tenant lifecycle, tenant status | Subscription plans, entitlements, usage limits, billing | No | Subscription module (M6) |
| Orders | Transaction boundaries, outbox pattern | Order entity, idempotency keys, payment state | No | Orders module |
| Payments | Background job infrastructure, outbox | Payment provider adapter, callback validation, reconciliation | No | Payments module |
| Delivery | Tenant-scoped operations, permissions | Delivery entity, assignment, fulfillment state | No | Delivery module |
| Inventory | Transactional consistency, concurrency guards | Inventory entity, stock deduction, low-stock alerts | No | Inventory module |
| Notifications | Outbox pattern, background hosted services | Notification templates, preferences, delivery providers | No | Notifications module |
| Reporting | Tenant isolation, read model patterns | Report queries, exports, scheduled reports | No | Reporting module |
| Customer | Account ownership (Identity), tenant-specific relationships | Customer entity, address book, order history linkage | No | Customer module |

### Critical ownership distinction (confirmed in code)
- **Access** answers "Is this actor authorized?" — `AccessService.HasPermissionAsync`, `RequireAccessFilter`, `IPermissionChecker`. Implemented.
- **Subscription** answers "Does this tenant's subscription allow this?" — NOT implemented (M6, Pending). Correctly separated; no subscription entitlement logic in Access.
- Business modules must NOT depend on Identity/Access/Tenant implementation projects — they should consume Contracts. Foundation supports this via `*.Contracts` assemblies.

### Speculative contracts
None found. No contracts created for not-yet-designed modules. Good.

---

## G. Remediation Backlog

### P0 — Must fix before business-module implementation

| Task ID | Short name | Scope / files | Why needed | Acceptance criteria | Dependencies | Owner |
|---|---|---|---|---|---|---|
| P0-01 | Fix Identity→Access boundary violation | `CoreKit.Identity.csproj` (remove `CoreKit.Access.Contracts` reference); `CoreKit.Identity/Services/SessionIssuer.cs` (replace `IAccessTokenClaimsContributor` with Identity-owned contract or inline claims logic) | Identity depends on Access, violating D-002/D-018/D-019. Standalone Identity broken. | `CoreKit.Identity.csproj` has no Access reference; `SessionIssuer` compiles without Access.Contracts | None | Implementation agent |
| P0-02 | Remove CoreKit.Access.Contracts (D-021) | Delete `CoreKit.Access.Contracts/` project; remove references from Identity, Access, Integration, Web csproj files; migrate `IPermissionChecker`, `IAccessService`, `IAccessMembership` etc. to appropriate homes or inline | Accepted decision D-021 (2026-11-23) not implemented. Access.Contracts is a SharedKernel-like dumping ground. | `CoreKit.Access.Contracts` no longer exists; no project references it | P0-01 (Identity must stop referencing it first) | Implementation agent |
| P0-03 | Fix stale documentation baseline | `current-state.md`, `roadmap.md`, `architecture.md` — update HEAD references from `3c562cf`/`d641921` to `fc4a0897`; reconcile D-019/D-020 contradictions in architecture.md §6 | Reviewer cannot trust documentation; decisions vs code mismatches (architecture.md describes superseded D-019 projection design) | Documentation reflects actual HEAD `fc4a0897`; architecture.md consistent with decisions.md D-020/D-021 | None | Project Coordinator |
| P0-04 | Add public self-registration endpoint | `CoreKit.Identity/Endpoints/IdentityEndpointExtensions.cs` (add `POST /identity/auth/register` AllowAnonymous); `CoreKit.Identity/Services/AuthService.cs` or `UserService.cs` (registration flow) | Identity "owns registration" per AGENTS.md but no public registration endpoint exists. Business modules (Customer, Store) need user registration. | Anonymous POST /register creates user with EmailConfirmed=false, returns success without account enumeration | None | Implementation agent |

### P1 — Must fix before foundation completion

| Task ID | Short name | Scope / files | Why needed | Acceptance criteria | Dependencies | Owner |
|---|---|---|---|---|---|---|
| P1-01 | Fix `/access` tenant middleware exclusion | `CoreKit.Tenant/Models/TenantOptions.cs:57-64` (add `/access` to `ExcludedPathPrefixes`) or configure per-endpoint bypass | Platform admin Access endpoints may be blocked by tenant membership check for non-member admins | `/access` excluded from tenant resolution OR platform scope bypasses tenant membership check | None | Implementation agent |
| P1-02 | Consolidate Identity migration folders | `CoreKit.Identity/Migrations/` and `CoreKit.Identity/Persistence/Migrations/` — unify into one folder, regenerate `IdentityDbContextModelSnapshot` | Two migration folders risk snapshot/model drift; EF Core may generate conflicting migrations | Single migration folder; snapshot matches latest migration | None | Implementation agent |
| P1-03 | Squash "PendingFix" migrations | `CoreKit.Identity/Persistence/Migrations/20261127163403_PendingFix.cs`, `CoreKit.Access/Migrations/20261127164524_PendingFixAccess.cs` | "PendingFix" indicates incomplete/temporary migrations; typo-fix migrations should be squashed into originals | No migrations named "PendingFix"; clean linear migration history | P1-02 | Implementation agent |
| P1-04 | Remove duplicate Integration registrations | `Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs:66, 95` (duplicate `TenantClaimsContributor`), lines `69, 98` (duplicate `PlatformAdminClaimsContributor`) | `TryAddEnumerable` makes duplicates harmless but confusing; indicates copy-paste error | Single registration of each `IAccessTokenClaimsContributor` | None | Implementation agent |
| P1-05 | Investigate Integration → Tenant.Persistence reference | `Dastarkhwan.Integration.csproj:11` (references CoreKit.Tenant) and `using CoreKit.Tenant.Persistence` in IntegrationServiceCollectionExtensions.cs | Direct Tenant DbContext access enables boundary bypass | Integration uses only Tenant public contracts; Tenant.Persistence reference removed or justified | None | Implementation agent |

### P2 — Important, can be scheduled

| Task ID | Short name | Scope | Why | Acceptance |
|---|---|---|---|---|
| P2-01 | Fix architecture test failures | `CoreKit.Architecture.Tests/` — update solution inventory, resolve duplicate contract types | 14/53 architecture tests fail; boundary enforcement not trustworthy | 53/53 architecture tests pass |
| P2-02 | Add integration tests for cross-module events | `Dastarkhwan.Integration.Tests/` — expand beyond 1 file | Event delivery reliability is critical for tenant→Access integration | Tests cover tenant-created→Access-provision, identity-deleted→tenant/Access cleanup |
| P2-03 | Confirm secver cache TTL security impact | `ServiceCollectionExtensions.cs:248` (30s TTL) | 30-second stale token window after credential change | Documented/accepted or TTL reduced |
| P2-04 | Run tests on PostgreSQL | All test projects | Current failures due to SQLite lacking `pg_advisory_xact_lock`, `chr()` | Tests pass against PostgreSQL |

### P3 — Future scalability / optimization

| Task ID | Short name | Scope | Why | Acceptance |
|---|---|---|---|---|
| P3-01 | Distributed cache for Access | `AccessCache` is in-memory singleton | Multi-instance hosts need shared cache | Redis or distributed cache optional |
| P3-02 | RLS implementation | No RLS in code (interceptors removed) | Defense-in-depth for tenant isolation | RLS policies if required by security policy |
| P3-03 | Outbox lease/renewal | Identity/Tenant outbox dispatchers | Multi-instance outbox processing | Lease-based claiming |

---

## H. Final Recommendation

### 11 explicit answers

1. **Is Identity complete?** No — missing public self-registration endpoint; has boundary violation (references Access.Contracts). Auth/login/refresh/lockout/token/secver are well implemented.

2. **Is Access complete?** Substantially yes — roles, permissions, assignments, authorization, fail-closed, caching, platform membership. But `CoreKit.Access.Contracts` should be removed per D-021 (not done).

3. **Is Tenant complete?** Substantially yes — lifecycle, context resolution, middleware, suspended-status enforcement, membership, outbox. Cache invalidation implemented.

4. **Are module Contracts correctly implemented?** Partially — Identity.Contracts and Tenant.Contracts exist and are used correctly. Access.Contracts should not exist (D-021). Duplicate contract types between Identity/Interfaces and Identity.Contracts.

5. **Is Integration reliable?** Partially — event handlers registered for tenant lifecycle and identity deletion cleanup. But duplicate registrations, references Tenant.Persistence, no runtime verification of event delivery.

6. **Is Web composition correct?** Yes — single host, correct middleware order, auth before tenant resolution, health checks, problem details, dev-only endpoints gated.

7. **Are the core security boundaries enforced?** Mostly — JWT secver validation (with 30s cache), refresh rotation with replay detection, atomic lockout, tenant suspended enforcement, fail-closed authorization. But Identity→Access dependency breaks the Identity/Access boundary.

8. **Is the database/persistence foundation reliable?** Structurally yes — three DbContexts, schema per module, migrations committed. But migration application to PostgreSQL not verified; dual Identity migration folders; "PendingFix" migrations.

9. **Is the foundation ready for future business modules?** Conditionally — contracts pattern is in place, but P0 boundary violations must be fixed first.

10. **Can Business, Store, Catalog/Product and Subscription development start?** Not yet for code that touches Identity or Access. Business-module domain models can be designed behind Tenant.Contracts. First business module implementation should wait for P0-01/P0-02.

11. **What must be fixed before the first business module?** P0-01 (Identity→Access boundary), P0-02 (remove Access.Contracts), P0-03 (docs baseline), P0-04 (public registration).

12. **What can safely be deferred?** P1-P3 items, RLS, distributed cache, multi-instance outbox, Subscription module (M6).

13. **Recommended next implementation task:** P0-01 — Fix Identity→Access boundary violation (remove `CoreKit.Access.Contracts` reference from `CoreKit.Identity.csproj`, replace `IAccessTokenClaimsContributor` usage in `SessionIssuer.cs`).

### Overall decision

**CONDITIONAL GO.** The foundation is genuinely well-engineered with real security controls. The P0 issues are correctable without architectural redesign. Once P0-01 through P0-04 are resolved, the foundation can support the first business module (Store or Catalog/Product) behind Tenant.Contracts and Access contracts.

---

## Review Limitations (read-only, static inspection)

- No builds executed (build logs from prior sessions exist but reviewer did not run them)
- No tests executed (test failures documented in current-state.md are from prior runs)
- No migrations applied to any database
- No PostgreSQL runtime verification (RLS, advisory locks, transaction enlistment)
- No network/external dependency testing (email providers, captcha)
- current-state.md is stale relative to actual HEAD; all documentation claims re-verified against source where possible

---

## End of report

Review performed independently, read-only, against baseline `fc4a08976b737d47464365477944e9c5cd328b42` (2026-11-28). No repository modifications made.