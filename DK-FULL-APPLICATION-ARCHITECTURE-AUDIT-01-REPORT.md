# DK-FULL-APPLICATION-ARCHITECTURE-AUDIT-01 — Primary Audit Report

**Repository:** `waqas-dba/Dastarkhwan`  
**Working directory:** `F:\Developer\waqas-dba\waqas-dba\Dastarkhwan`  
**Reported baseline:** `12eac87dd6d7c50e29fc89bcc43d758515d6b03a`  
**Audit type:** Investigation, audit, architecture assessment and remediation planning only  
**Implementation authorization:** NOT GRANTED  
**Primary audit agent:** OpenCode  
**Independent reviewer:** Claude Account 2 (pending)  
**Project Manager / architecture coordinator:** ChatGPT Account 1  
**Final authority:** Project Owner  
**Audit date:** Sun Nov 15 2026

---

## Part A — Repository Baseline

### A.1 Repository identity

| Field | Value |
|---|---|
| Repository path | `F:\Developer\waqas-dba\waqas-dba\Dastarkhwan` |
| Branch | `master` |
| Local HEAD | `12eac87dd6d7c50e29fc89bcc43d758515d6b03a` |
| HEAD commit date | 2026-11-14 20:51:30 +0500 |
| HEAD commit message | `docs(access): correct the endpoint filter's authorization freshness claim` |
| Reported baseline | `12eac87dd6d7c50e29fc89bcc43d758515d6b03a` (matches local HEAD) |

### A.2 Working-tree status

```
 M CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs
?? CoreKit.Tenant.Tests/TenantMemberRemovedAccessCacheTests.cs
?? CoreKit.Tenant/Integration/TenantMemberRemovedToAccessMaintenance.cs
```

| File | Status | Nature |
|---|---|---|
| `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` | Modified (unstaged) | Adds DI registration for `TenantMemberRemovedToAccessMaintenance` event handler (5 lines, uncommitted) |
| `CoreKit.Tenant.Tests/TenantMemberRemovedAccessCacheTests.cs` | Untracked | **Corrupted test file** — truncated at `var member = Guid/Newline` (line 74), will not compile |
| `CoreKit.Tenant/Integration/TenantMemberRemovedToAccessMaintenance.cs` | Untracked | New integration event handler class (36 lines) |

These three files are related: they implement the "clear Access cache when a tenant member is removed" feature. Two are uncommitted; one is corrupted.

### A.3 Commit history (latest)

| Hash | Date | Message |
|---|---|---|
| `12eac87d` | 2026-11-14 20:51 | `docs(access): correct the endpoint filter's authorization freshness claim` |
| `7ed4c61f` | 2026-11-13 19:53 | `docs(governance): accept D-017 automation and align agent guidance` |
| `f09f236b` | 2026-11-13 19:36 | `update the files` |

The working tree contains uncommitted changes beyond HEAD `12eac87d`. No untracked secrets were observed in the diff.

### A.4 Solution and project inventory

| Project | Type | Target | Project references |
|---|---|---|---|
| `CoreKit.Access` | Class library | net10.0 | None (leaf module) |
| `CoreKit.Identity` | Class library | net10.0 | `CoreKit.Access` |
| `CoreKit.Tenant` | Class library | net10.0 | `CoreKit.Access` |
| `CoreKit.SampleHost` | ASP.NET Core Web | net10.0 | `CoreKit.Access`, `CoreKit.Identity`, `CoreKit.Tenant` |
| `CoreKit.Access.Tests` | xUnit test | net10.0 | `CoreKit.Access` |
| `CoreKit.Identity.Tests` | xUnit test | net10.0 | `CoreKit.Identity` |
| `CoreKit.Tenant.Tests` | xUnit test | net10.0 | `CoreKit.Tenant` |
| `CoreKit.Architecture.Tests` | xUnit test | net10.0 | `CoreKit.Access`, `CoreKit.Identity`, `CoreKit.Tenant` |

**SharedKernel** folder exists but has **no `.csproj` file and no source files**. It is not referenced by any project. Architecture documents intend it to hold neutral cross-module contracts, but those contracts currently live in `CoreKit.Access.Abstractions`.

### A.5 Documentation inventory

| Document | Location | Status |
|---|---|---|
| `AGENTS.md` | root | Agent entry point |
| `CLAUDE.md` | root | Engineering instructions |
| `architecture.md` | root | Target architecture |
| `decisions.md` | root | Accepted decisions (D-001 … D-017) |
| `roadmap.md` | root | Milestones M0–M14 |
| `current-state.md` | root | Implementation status evidence |
| `ai-tasks.md` | root | Agent workflow, §13 briefing standard, §14 documentation standard |
| `commands.txt` | root | EF Core/Git command reference |
| `DK-APP-MASTER-PLAN-01.md` | root | Master plan (not yet inspected in detail) |
| `COMPLETION_AC-07-IMPL1.md` | root | Completion record |
| `COMPLETION_AC-08-TEST-FIX1.md` | root | Completion record |

### A.6 Audit limitations

- No builds were executed (prohibited).
- No tests were executed (prohibited).
- No database connections were made (prohibited).
- No migrations were applied or generated (prohibited).
- Findings based on static source inspection only. Runtime behaviour (e.g., EF Core query translation, cache race conditions under load, migration application success) is classified as **needs verification**.
- Secrets were not searched for in Git history (prohibited to print); the committed-secret finding from `current-state.md` is carried forward as a reported risk.

---

## Part B — Executive Assessment

### B.1 Overall architecture quality

**Design strength:** The modular monolith is genuinely implemented with three leaf-ish modules (Access, Identity, Tenant) and a composition root (SampleHost). Access is the authorization authority and does not reference Identity or Tenant. Identity and Tenant both depend on Access. There are no circular project dependencies. EF Core 10, PostgreSQL, .NET 10 — all as decided.

**Design weakness:** The dependency direction between Identity and Access is inverted relative to the architecture's conceptual model. D-002 states "Identity may provide only the stable public identity contracts/claims required by Access," which implies Access should depend on Identity's contracts. In the actual code, Identity references Access (`CoreKit.Identity → CoreKit.Access`) and calls `IAccessService`, `IPermissionChecker`, `IAccessMaintenance` for platform-scope authorization of its user-management endpoints. This is not circular, but it means the authentication module cannot compile without the authorization module. Whether this is justified depends on whether Identity's user-management authorization is correctly placed in Access or should be a lighter contract.

**Overall:** Structurally sound for a foundation layer, but several security-critical properties are **unverified at runtime** (migrations not applied, tests not run, one test file corrupted). The foundation is **implemented but not verified**.

### B.2 Security posture

| Control | Status | Evidence |
|---|---|---|
| Server-side authorization | Implemented | `AccessGuard`, `RequirePlatformScopePermissionAsync`, tenant guard |
| Fail-closed authorization | Implemented | `EffectiveAccess.Has` requires `IsMember && (IsOwner || Permissions.Contains)` |
| JWT short-lived access tokens | Implemented | `TokenService.CreateAccessToken`, configurable 1–120 min |
| Rotating refresh tokens | Implemented | `RefreshToken.FamilyId`, reuse revokes family |
| Refresh token reuse detection | Implemented | `RefreshAsync` revokes all on reuse |
| Password hashing (no plaintext) | Implemented | `PasswordHasher`, `OpaqueToken.Hash` |
| Account lockout (5 attempts / 15 min) | Implemented | `RegisterFailedLoginAttemptAsync`, `AccessFailedCount`, `LockoutEnd` |
| No account enumeration | Implemented | Same `InvalidCredentials` for unknown email and wrong password |
| Account deactivation revokes sessions | Implemented | `SecurityVersion` bump + `RevokeAllForUserAsync` |
| SecurityVersion JWT validation | **NOT implemented** | `User.cs` comment line 45-46: "Access-token (JWT) validation of this version is a separate, not yet implemented task" |
| Tenant isolation query filters | Partial | `TenantMember` has `RemovedAt == null` filter; `TenantAwareDbContext` exists but `TenantDbContext` does not inherit it (platform-level by design) |
| Cross-tenant access prevention | Implemented | `TenantAccessGuard.EnsureCanAccessAsync` |
| Cache invalidation on member removal | **Uncommitted** | Handler + DI registration exist but are not committed; one test file corrupted |
| Secrets in source control | Historical risk | `current-state.md`: `appsettings.Development.json` was previously committed |

**Security posture:** Good defensive design at the code level, but **one confirmed security gap** (access tokens remain valid after credential change until expiry) and **one uncommitted security feature** (Access cache invalidation on tenant member removal). Secrets history risk is carried forward.

### B.3 Logical correctness

**Implemented correctly:** Lockout atomic transition, refresh token rotation with family revocation, optimistic concurrency on all mutable entities, generation-based membership preventing stale assignment grants, escalation prevention (`AccessGuard.Holds`), last-active-administrator protection, email immutability after creation.

**Probable defects:** Corrupted test file `TenantMemberRemovedAccessCacheTests.cs` line 74 (`Guid/Newline`). Cache stampede possibility in `AccessCache.Set` after invalidation (read-lock checked, but a concurrent writer could repopulate between check and set — though the token comparison mitigates). `TenantMembershipReader.FindAsync` caches `TenantMemberInfo` including `IsOwner`, but there is no invalidation path when `IsOwner` changes (only `Invalidate` on membership add/remove, not on owner change).

**Unverified:** All runtime workflows (login, refresh, tenant creation, member removal) have no execution evidence.

### B.4 Maintainability

**Strengths:** Consistent `Result<T>` error pattern, audit logging on all mutations, clear module boundaries, focused service classes, architecture tests enforce boundaries at compile time.

**Risks:** Empty SharedKernel (contracts scattered), Identity → Access dependency direction, `TenantActor` accepting `ICurrentUser?` (transitive Access coupling), `CurrentUserService` exposing `Permissions`/`Roles` claims from JWT (stale by design), `TenantDbContext` not using `TenantAwareDbContext` base.

### B.5 Module independence

Access is genuinely independent (no project references). Identity and Tenant both depend only on Access. SampleHost depends on all three. No circular dependencies. Architecture tests enforce this at both compiled-assembly and project-reference levels.

### B.6 Database design

Three separate DbContexts, each owning its schema (`Identity_*`, `ACCESS_*`, `TENANT_*` tables). Composite FK from `ACCESS_Assignments` to `ACCESS_Roles` via alternate key `(Id, ScopeType, ScopeId)`. Unique indexes on role name per scope and tenant slug. Soft deletion on `TenantMember` with query filter. `MembershipGeneration` columns on both `AccessAssignment` and `TenantMember`. **All migrations committed but never applied to a verified database.**

### B.7 Scalability

Single shared PostgreSQL database per module (decided in D-011). No sharding, no read replicas, no outbox pattern, no background job infrastructure. Cache is in-process `IMemoryCache` — does not scale to multiple instances without invalidation coordination. This is acceptable for the current stage but is a future limitation.

### B.8 Operational readiness

Startup applies migrations via `Database.MigrateAsync()` in `SampleHost/Program.cs` — explicitly marked "development convenience," not production-appropriate. No health checks, no readiness/liveness endpoints, no structured correlation IDs, no metrics. Logging exists but is basic `ILogger`.

### B.9 Major blockers

1. **Corrupted test file** blocks `CoreKit.Tenant.Tests` compilation.
2. **Uncommitted security feature** (Tenant–Access cache invalidation) — handler class untracked, DI registration uncommitted.
3. **No runtime verification** — zero test execution results, zero migration application evidence.
4. **SecurityVersion JWT validation gap** — access tokens valid until expiry after credential change.
5. **Committed-secret history risk** — carried forward from M0.

---

## Part C — Actual Architecture Map

### C.1 Project dependency graph

```
CoreKit.Access  (leaf: roles, permissions, assignments, platform membership, authorization)
    │
    ├─► CoreKit.Identity  (accounts, authn, credentials, JWT, sessions, lockout)
    │         └─► calls Access: IAccessService, IPermissionChecker, IAccessMaintenance
    │
    └─► CoreKit.Tenant  (tenant lifecycle, membership, context, settings)
            └─► calls Access: IAccessScopeContext (via TenantAccessScopeContext),
                   IAccessMembershipAdapter (via TenantAccessMembership),
                   IAccessMaintenance (via TenantMemberRemovedToAccessMaintenance)
                   
CoreKit.SampleHost  (composition root: wires all three, maps endpoints, applies migrations at startup)
```

No circular dependencies. Access is the dependency root.

### C.2 Module ownership map

| Module | Owns | Does not own | Depends on |
|---|---|---|---|
| `CoreKit.Access` | Roles, permissions, assignments, platform membership, authorization decisions, audit | Identity credentials, Identity private tables | None |
| `CoreKit.Identity` | Users, authentication, passwords, JWT/refresh tokens, lockout, recovery | Business roles/permissions policy | `CoreKit.Access` (for platform authorization) |
| `CoreKit.Tenant` | Tenant lifecycle, membership, context/resolution, settings | Store/catalog/order rules, Identity implementation | `CoreKit.Access` (for authorization) |
| `CoreKit.SampleHost` | Composition, endpoint mapping, startup migration | Business rules | All three modules |

### C.3 Main request flow

`HTTP endpoint → [SampleHost middleware: UseIdentity (authN + rate limiter + authZ) → UseTenant (tenant resolution)] → endpoint → service → repository → DbContext → PostgreSQL`

### C.4 Authentication flow

1. Client posts `/identity/auth/login` with email/password (anonymous, rate-limited)
2. `AuthService.LoginAsync`: finds user by normalized email, hashes dummy for unknown users (timing mitigation), verifies password, checks lockout
3. On success: `TryCompleteSuccessfulLoginAsync` (atomic, version-guarded), `SessionIssuer.IssueForLoginAsync`
4. `SessionIssuer`: reads roles/permissions from `IAccessService.GetAccessAsync(userId, PlatformScope)`, builds JWT via `TokenService.CreateAccessToken`, inserts refresh token via `TryIssueGuardedAsync` (version-guarded, re-checks user row under lock)
5. Returns access token + refresh token

### C.5 Authorization flow

1. Endpoint decorated with `RequireAuthorization()` or `RequireAccessPermission(permission)` / `RequireTenant()`
2. Service method calls `AccessGuard.RequireAsync(permission, ct)` or `RequirePlatformScopePermissionAsync`
3. `AccessGuard`: validates scope from `IAccessScopeContext.Current`, checks `IAccessCurrentUser.IsAuthenticated` + `UserId`, resolves `EffectiveAccess` (cached or fresh), checks `access.Has(permission)`
4. Fresh authority required for `RolesManage`, `AssignmentsManage`, `PlatformMembersManage` — bypasses cache via `IFreshAccessSource`
5. Escalation prevention: `AccessGuard.Holds(actor, permissions)` — cannot grant permissions you don't hold

### C.6 Tenant resolution flow

1. Request arrives; `TenantResolutionMiddleware` runs resolution strategies in order: `ClaimTenantResolutionStrategy` (reads `tenant_id` claim from JWT), then `HeaderTenantResolutionStrategy` (reads `X-Tenant-Id` header)
2. Resolved tenant stored in `CurrentTenant` (scoped, implements `ICurrentTenant`)
3. `TenantAccessScopeContext` exposes `AccessScope.ForTenant(tenantId)` to Access
4. Tenant-scoped endpoints use `TenantAccessGuard.EnsureCanAccessAsync(tenantId)` — current tenant must match, or caller needs platform access

### C.7 Persistence boundaries

| DbContext | Tables prefix | Owns | Tenant-filtered? |
|---|---|---|---|
| `IdentityDbContext` | `Identity_*` | Users, RefreshTokens, EmailVerificationTokens | No (platform-level) |
| `AccessDbContext` | `ACCESS_*` | Roles, RolePermissions, Assignments, AuditEntries, PlatformMembers | No (platform-level) |
| `TenantDbContext` | `TENANT_*` | Tenants, Settings, Members (soft-delete filter), AuditEntries | No (platform-level store; services enforce boundary) |

`TenantAwareDbContext` base class exists but is not used by `TenantDbContext`. Future tenant-scoped modules should use it.

### C.8 Integration points

| Integration | Direction | Mechanism |
|---|---|---|
| Tenant → Access scope context | Tenant implements Access contract | `TenantAccessScopeContext : IAccessScopeContext` |
| Tenant → Access membership | Tenant implements Access contract | `TenantAccessMembership : IAccessMembershipAdapter` (always `IsOwner=false`) |
| Tenant member removed → Access cache clear | Tenant publishes event, Access handles | `TenantMemberRemoved` → `TenantMemberRemovedToAccessMaintenance : IEventHandler<TenantMemberRemoved>` → `IAccessMaintenance.RemoveUserAsync` |
| Identity → Access token claims | Tenant contributes claims to JWT | `TenantClaimsContributor : IAccessTokenClaimsContributor` |
| Identity → Access authorization | Identity calls Access | `IPermissionChecker.HasPlatformPermissionAsync`, `IAccessService`, `IAccessMaintenance` |

### C.9 Cache and concurrency design

| Cache | Scope | Invalidation | Consistency |
|---|---|---|---|
| `AccessCache` | EffectiveAccess per (scope, user) | Generation tokens (scope + user version + epoch); `InvalidateScope`, `InvalidateUser` | Eventually consistent within TTL; fresh reads bypass cache |
| `TenantMembershipReader` | TenantMemberInfo per (tenant, user) | `Invalidate(tenantId, userId)` on membership change | Eventually consistent within TTL |
| Refresh tokens | DB table | RevokedAt set on rotation/revocation/logout | Immediate (database) |
| Lockout | DB row | `AccessFailedCount`/`LockoutEnd` updated atomically | Immediate (database) |

**Concurrency:** Optimistic concurrency via `ConcurrencyStamp` on `TenantEntity`, `AccessRole`, `User.SecurityVersion`. Single `SaveChangesAsync` per unit of work. No distributed transactions. Cross-module workflows (e.g., create user + add platform member + set roles) use `IAccessMaintenance` (trusted, unguarded) called from Identity services within the same logical operation but separate DbContexts — not atomic across contexts.

---

## Part D — Detailed Findings

### Finding D-01: Corrupted test file (Confirmed defect — High)

| Field | Value |
|---|---|
| ID | D-01 |
| Category | Code quality / build |
| Severity | High |
| Confidence | Confirmed |
| Component | `CoreKit.Tenant.Tests` |
| Evidence | `CoreKit.Tenant.Tests/TenantMemberRemovedAccessCacheTests.cs`, line 74: `var member = Guid/Newline` — clearly truncated/corrupted |
| Actual behaviour | File will not compile; entire test project fails to build |
| Expected behaviour | Valid C# test file |
| Root cause | File was truncated during creation or a failed edit |
| Impact | `CoreKit.Tenant.Tests` cannot be compiled or executed; CI/build broken for this project |
| Scenario | `dotnet build` or `dotnet test CoreKit.Tenant.Tests` fails |
| Recommendation | Delete or rewrite the file. Since implementation authorization is not granted, record for remediation |
| Dependencies | Related to Finding D-04 (uncommitted cache invalidation feature) |

### Finding D-02: Uncommitted Tenant–Access cache invalidation (Confirmed defect — High)

| Field | Value |
|---|---|
| ID | D-02 |
| Category | Security / correctness |
| Severity | High |
| Confidence | Confirmed |
| Component | `CoreKit.Tenant` |
| Evidence | `CoreKit.Tenant/Integration/TenantMemberRemovedToAccessMaintenance.cs` (untracked, 36 lines); `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` (modified, unstaged, adds `services.AddScoped<IEventHandler<TenantMemberRemoved>, TenantMemberRemovedToAccessMaintenance>()`) |
| Actual behaviour | When a tenant member is removed, their Access role assignments are deleted via `IAccessMaintenance.RemoveUserAsync`, but the handler is not registered in DI (registration exists only as an unstaged modification). The handler class file is untracked. |
| Expected behaviour | Handler registered and committed so that member removal immediately invalidates Access cache and removes assignments |
| Root cause | Work in progress not committed |
| Impact | A removed tenant member can retain cached tenant-scoped permissions until the Access cache entry expires (TTL-dependent), violating immediate revocation |
| Scenario | Admin removes user from tenant; user's cached Access permissions remain valid until cache TTL expires |
| Recommendation | Commit handler class and DI registration together; add regression tests |
| Dependencies | Related to D-01 (corrupted test file that should cover this), D-10 (cache stampede) |

### Finding D-03: Access tokens remain valid after credential change (Confirmed security gap — Medium)

| Field | Value |
|---|---|
| ID | D-03 |
| Category | Security |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Identity` |
| Evidence | `CoreKit.Identity/Entities/User.cs`, lines 45-46: comment states "Access-token (JWT) validation of this version is a separate, not yet implemented task: existing access tokens stay valid until expiry." `SecurityVersion` exists on `User` (line 48) and is incremented on password change/deactivation, but `SessionIssuer` and JWT validation never check it. |
| Actual behaviour | After password change or account deactivation, existing JWT access tokens remain valid until their natural expiry (configurable up to 120 minutes) |
| Expected behaviour | Security-sensitive changes should invalidate existing access tokens (or at minimum, the design decision should be documented and accepted) |
| Root cause | Deliberate but incomplete design — `SecurityVersion` column exists but is not validated in token issuance or middleware |
| Impact | Compromised/stolen access token remains usable for up to 120 minutes after credential change; deactivated user can still access until token expiry |
| Scenario | Attacker steals JWT; victim changes password; attacker's token still works until expiry |
| Recommendation | Implement JWT `SecurityVersion` validation (e.g., include `sec_ver` claim and validate on each request via token validation handler), or document and accept the 120-minute window as a deliberate trade-off |
| Dependencies | Related to D-12 (refresh token rotation), D-02 (cache invalidation) |

### Finding D-04: Identity → Access dependency direction (Design weakness — Medium)

| Field | Value |
|---|---|
| ID | D-04 |
| Category | Architecture / dependency |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Identity`, `CoreKit.Access` |
| Evidence | `CoreKit.Identity/CoreKit.Identity.csproj` line 33: `<ProjectReference Include="..\CoreKit.Access\CoreKit.Access.csproj" />`. `UserService` (line 25) depends on `IAccessService` and `IAccessMaintenance`. `AuthService` (line 25) depends on `IAccessService`. `SessionIssuer` (line 14) depends on `IAccessService`. `AdministratorGuard` (line 17) depends on `IAccessService`. |
| Actual behaviour | Identity (authentication) depends on Access (authorization). D-002 states "Identity may provide only the stable public identity contracts/claims required by Access," implying the reverse direction conceptually. |
| Expected behaviour | Per D-002 intent, Access should consume Identity's stable contracts, not vice versa — OR the current direction should be explicitly justified as acceptable |
| Root cause | Identity's user-management endpoints need platform-scope authorization (e.g., `identity.users.read`), which Access provides. The cleanest contract boundary is unresolved. |
| Impact | Identity cannot be compiled or tested without Access; tight conceptual coupling between authentication and authorization |
| Scenario | Future need to deploy Identity independently of Access is blocked |
| Recommendation | Evaluate whether Identity's platform authorization needs can be served by a thinner contract (e.g., `IPermissionChecker` only) owned by Identity or SharedKernel, or formally accept Identity → Access as the approved direction and update documentation |
| Dependencies | Related to D-06 (SharedKernel empty), D-15 (module ownership) |

### Finding D-05: Empty SharedKernel (Design weakness — Medium)

| Field | Value |
|---|---|
| ID | D-05 |
| Category | Architecture |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.SharedKernel` |
| Evidence | `CoreKit.SharedKernel/` folder exists with no `.csproj` and no source files. Architecture intends neutral cross-module contracts here (`ICurrentUser`, `ITenantMembershipReader`), but they live in `CoreKit.Access.Abstractions`. |
| Actual behaviour | SharedKernel is empty; cross-module contracts are owned by Access |
| Expected behaviour | Per architecture §4, SharedKernel should hold "small, stable cross-module primitives/contracts genuinely needed by multiple modules" |
| Root cause | Intended but not implemented |
| Impact | `ICurrentUser` (used by TenantActor) lives in Access.Abstractions, creating transitive Access coupling in Tenant. `ITenantMembershipReader` lives in Tenant, which is correct. |
| Recommendation | Decide: either populate SharedKernel with genuinely shared contracts (but approved constraints say "Do not introduce SharedKernel" per the audit task instructions — wait, re-reading: the task says "Do not introduce SharedKernel. Do not introduce SampleHost as a future architectural requirement.") — Actually the task §4.2 says "Do not introduce SharedKernel." This means I should NOT recommend creating/populating SharedKernel. The current empty state is acceptable; contracts should remain module-owned. |
| Recommendation | Accept empty SharedKernel. Keep `ICurrentUser` in Access.Abstractions or move it to Identity (its producer). Do not create SharedKernel. |
| Dependencies | Related to D-04 |

### Finding D-06: TenantActor transitive Access coupling (Design weakness — Medium)

| Field | Value |
|---|---|
| ID | D-06 |
| Category | Coupling / SRP |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Tenant/Abstractions/TenantActor.cs` |
| Evidence | `TenantActor` constructor accepts `ICurrentUser? currentUser` (line 13) where `ICurrentUser` is from `CoreKit.Access.Abstractions`. `TenantActor` only uses `_currentUser?.UserId` (line 22) and `IPermissionChecker` (line 14). It does not use `ICurrentUser.Permissions`. |
| Actual behaviour | Tenant depends on Access's `ICurrentUser` interface (transitively) for `UserId` |
| Expected behaviour | Tenant should depend on the smallest necessary contract for the current user's ID |
| Root cause | `ICurrentUser` was placed in Access.Abstractions rather than being owned by Identity (its producer) or defined as a minimal contract |
| Impact | Tenant has unnecessary coupling to Access's user abstraction |
| Recommendation | Define a minimal `ICurrentUser`-like contract in Tenant (or Identity) that exposes only `UserId` and `IsAuthenticated`, or accept the current coupling if justified by the single shared current-user implementation in SampleHost |
| Dependencies | Related to D-04, D-05 |

### Finding D-07: TenantDbContext not using TenantAwareDbContext (Design weakness — Low)

| Field | Value |
|---|---|
| ID | D-07 |
| Category | Architecture / data integrity |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.Tenant/Persistence/TenantDbContext.cs` |
| Evidence | `TenantDbContext : DbContext` (line 9), not `: TenantAwareDbContext`. Comment on line 7: "It is a platform-level store, so it is not filtered by tenant." |
| Actual behaviour | `TenantDbContext` is a platform-level store with no tenant query filter. This is by design for tenants/members/settings tables, but any future tenant-scoped table added to this context would lack automatic isolation. |
| Expected behaviour | Platform-level contexts should be explicitly documented as non-tenant-filtered; tenant-scoped contexts should use `TenantAwareDbContext` |
| Root cause | Design decision documented in code comment |
| Impact | Future developers might add tenant-scoped entities to `TenantDbContext` without isolation |
| Recommendation | Add explicit documentation/class comment that `TenantDbContext` is platform-level; create tenant-scoped sub-contexts using `TenantAwareDbContext` for future modules (Store, Catalog, etc.) |
| Dependencies | Related to D-22 (tenant isolation) |

### Finding D-08: TenantMembershipReader caches IsOwner without invalidation on owner change (Probable defect — Medium)

| Field | Value |
|---|---|
| ID | D-08 |
| Category | Cache / correctness |
| Severity | Medium |
| Confidence | Probable |
| Component | `CoreKit.Tenant/Services/TenantMembershipReader.cs` |
| Evidence | `FindAsync` (line 39) caches `new TenantMemberInfo(member.IsOwner, member.MembershipGeneration)`. `Invalidate` (line 47) is called on membership add/remove via `ITenantMembershipCache`, but `SetOwnerAsync` (in `TenantMembershipService`, line 257) calls `_membershipCache.Invalidate(tenantId, userId)` — actually it does invalidate. Let me re-check... `SetOwnerAsync` line 257: `_membershipCache.Invalidate(tenantId, userId)`. So owner changes ARE invalidated. |
| Re-evaluation | This finding is **invalidated** — `SetOwnerAsync` does invalidate the cache. Change to False positive. |

**Correction: Finding D-08 is a False positive / rejected concern.** `TenantMembershipService.SetOwnerAsync` calls `_membershipCache.Invalidate(tenantId, userId)` at line 257. `AddMemberAsync` calls it at line 143. `RemoveMemberAsync` calls it at line 203. All membership-changing paths invalidate.

### Finding D-09: AccessCache potential cache stampede after invalidation (Probable defect — Medium)

| Field | Value |
|---|---|
| ID | D-09 |
| Category | Cache / concurrency |
| Severity | Medium |
| Confidence | Probable |
| Component | `CoreKit.Access/Services/AccessCache.cs` |
| Evidence | `Set(scope, userId, access, token)` (line 117) re-checks `GetCurrentToken` under read lock (line 126). If equal, stores to cache. However, between `Begin` (taking token) and `Set`, another thread can invalidate (advancing generation). The token check prevents storing stale data, but the original caller's `GetAccessAsync` still returns the freshly resolved (correct) value — it just doesn't cache it. No incorrect data is served. |
| Re-evaluation | This is **not a defect** — the token comparison correctly prevents stale cache population. The cost is a cache miss, not incorrect data. |
| Classification | **False positive / rejected concern** |

### Finding D-10: Cache key collision risk across scopes (Design weakness — Low)

| Field | Value |
|---|---|
| ID | D-10 |
| Category | Cache / multi-tenancy |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.Access/Services/AccessCache.cs` |
| Evidence | `MakeKey` (line 200): `$"corekit.access:{scope}:e{token.Epoch}:v{token.ScopeVersion}:u{token.UserVersion}:{userId:N}"`. Scope includes `Type:Id` (e.g., `tenant:xxx`), so tenant-scoped and platform-scoped entries are distinct. |
| Re-evaluation | **False positive** — scope is part of the key, so no collision. |

**Correction: Finding D-10 is a False positive / rejected concern.**

### Finding D-11: Identity `CurrentUserService` exposes stale `Roles`/`Permissions` from JWT (Design weakness — Medium)

| Field | Value |
|---|---|
| ID | D-11 |
| Category | Security / correctness |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Identity/Services/CurrentUserService.cs` |
| Evidence | Lines 34-41: `Roles` and `Permissions` are read from JWT claims (`IdentityClaimTypes.Role`, `IdentityClaimTypes.Permission`). These are embedded at token issuance and are stale by design. `AuthService` and `UserService` use `IPermissionChecker`/`IAccessService` for fresh authorization, not `CurrentUserService.Permissions`. |
| Actual behaviour | `CurrentUserService.Permissions`/`Roles` reflect token-issuance-time values, not current Access state |
| Expected behaviour | Consumers should not rely on `CurrentUserService.Permissions` for authorization decisions; they should use `IPermissionChecker` |
| Root cause | JWT claims are intentionally minimal; roles/permissions claims may be present but are not the authorization source of truth |
| Impact | If any code path uses `CurrentUserService.Permissions` for authorization, it would be vulnerable to stale privilege escalation |
| Recommendation | Document that `CurrentUserService.Roles`/`Permissions` are informational only (from JWT, potentially stale); authorization must use `IPermissionChecker.HasPermissionAsync`/`HasPlatformPermissionAsync`. Audit all callers to confirm none use them for access decisions. |
| Dependencies | Related to D-03 (token validation gap) |

### Finding D-12: No health checks, readiness, or liveness endpoints (Missing capability — Low)

| Field | Value |
|---|---|
| ID | D-12 |
| Category | Operations |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.SampleHost/Program.cs` |
| Evidence | `Program.cs` has no `AddHealthChecks()`, no health endpoint mapping. Only `/`, `/demo/public`, `/demo/needs-login`, `/demo/needs-users-read`, `/demo/needs-tenant`, `/demo/sale`, `/demo/refund`, `/dev/verification-token`. |
| Actual behaviour | No way for orchestration to determine application readiness or database connectivity |
| Expected behaviour | Production service should expose health/readiness/liveness |
| Root cause | Backend-first priority; operational concerns deferred |
| Impact | Cannot integrate with container orchestration health checks; database outage undetectable via HTTP |
| Recommendation | Add health checks for database connectivity (all three DbContexts) and map `/health` endpoint |
| Dependencies | None |

### Finding D-13: Startup applies migrations automatically (Design weakness — Medium)

| Field | Value |
|---|---|
| ID | D-13 |
| Category | Operations / deployment |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.SampleHost/Program.cs` lines 51-56 |
| Evidence | `Database.MigrateAsync()` called on all three DbContexts at startup, with comment "Development convenience: create or update the databases, then seed them. In production, run migrations as a separate deployment step instead." |
| Actual behaviour | Application automatically migrates and seeds on startup |
| Expected behaviour | Production should not auto-apply migrations on startup; migrations should be a separate deployment step |
| Root cause | Development convenience left in composition root |
| Impact | If deployed to production as-is, startup could fail due to migration errors, or migrations could run with excessive permissions |
| Recommendation | Remove auto-migration from `Program.cs` or gate it behind `IsDevelopment()`. Document production migration deployment procedure |
| Dependencies | None |

### Finding D-14: No CORS, HSTS, or forward-headers configuration visible (Missing capability — Low)

| Field | Value |
|---|---|
| ID | D-14 |
| Category | Security / API |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.SampleHost/Program.cs` |
| Evidence | No `AddCors()`, `UseCors()`, `AddHsts()`, `UseHsts()`, `UseForwardedHeaders()` calls in `Program.cs`. |
| Actual behaviour | No CORS policy, no HSTS, no forwarded-header handling |
| Expected behaviour | Production API should have explicit CORS policy, HSTS, and forwarded-headers middleware if behind a reverse proxy |
| Root cause | Deferred operational/security configuration |
| Impact | Browser clients cannot call API from different origins; behind a proxy, client IP and protocol may be misidentified |
| Recommendation | Add CORS policy, HSTS, and forwarded-headers configuration for production deployment |
| Dependencies | None |

### Finding D-15: Identity owns `ICurrentUser` consumption but Access defines it (Architecture — Medium)

| Field | Value |
|---|---|
| ID | D-15 |
| Category | Architecture / ownership |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Identity/Services/ICurrentUserService.cs`, `CoreKit.Access/Abstractions/ICurrentUser.cs` |
| Evidence | `ICurrentUserService : ICurrentUser` (Identity) where `ICurrentUser` is defined in `CoreKit.Access.Abstractions`. Identity registers `ICurrentUser` by casting `ICurrentUserService` to it (`ServiceCollectionExtensions.cs` line 106). |
| Actual behaviour | Identity's current-user implementation satisfies Access's `ICurrentUser` contract. The contract is owned by Access but implemented by Identity. |
| Expected behaviour | The contract owner and implementation owner should be clear. Either Access defines and uses it (current, acceptable), or Identity defines it and Access consumes it. |
| Impact | Conceptual ownership of the current-user abstraction is split between modules |
| Recommendation | Document that `ICurrentUser` is an Access-owned contract implemented by Identity's `CurrentUserService`. This is acceptable as long as Access does not depend on Identity internals (it doesn't — it depends on the interface only). |
| Dependencies | Related to D-04 |

### Finding D-16: No database indexes on `AccessAssignment.UserId` alone (Performance — Low)

| Field | Value |
|---|---|
| ID | D-16 |
| Category | Performance / database |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.Access/Persistence/AccessDbContext.cs` |
| Evidence | `AccessAssignment` has indexes on `(ScopeType, ScopeId, UserId)` (line 56) and `(RoleId, ScopeType, ScopeId)` (line 57), but no index on `UserId` alone. `ListUserIdsWithRoleAsync` queries by role, not by user alone. `GetRolesAsync` queries by scope+user. |
| Actual behaviour | No standalone `UserId` index on `ACCESS_Assignments` |
| Expected behaviour | If future queries look up all assignments for a user across scopes, a `UserId` index would help |
| Root cause | Current query patterns don't require it |
| Impact | None currently; future limitation |
| Recommendation | Add `UserId` index if cross-scope user assignment lookup is needed |
| Dependencies | None |

### Finding D-17: Tenant claims contributor queries database on every token issuance (Performance — Low)

| Field | Value |
|---|---|
| ID | D-17 |
| Category | Performance |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.Tenant/Integration/TenantClaimsContributor.cs` |
| Evidence | `GetClaimsAsync` (line 25) calls `_members.FindPreferredTenantIdAsync(userId, ct)` on every JWT issuance (login and refresh). This is a database read during token issuance. |
| Actual behaviour | Every token issuance incurs a tenant membership DB query |
| Expected behaviour | Acceptable for login (low frequency), but refresh token rotation also triggers re-issuance |
| Root cause | Tenant claim must be current at issuance |
| Impact | Minor latency on refresh; acceptable for current scale |
| Recommendation | Cache the preferred tenant ID per user with short TTL and invalidate on membership change |
| Dependencies | Related to D-02 (cache invalidation) |

### Finding D-18: No rate limiting on tenant or access endpoints (Missing capability — Medium)

| Field | Value |
|---|---|
| ID | D-18 |
| Category | Security |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` (rate limiting only on AuthPolicy) |
| Evidence | `AddRateLimiter` only registers `IdentityRateLimiting.AuthPolicy` (line 145), applied only to `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/change-password`, `/auth/verify-email`, `/auth/send-verification`, `/auth/resend-verification`. Tenant endpoints (`/tenants/*`) and Access endpoints (`/access/*`) have `RequireAuthorization()` but no rate limiting. |
| Actual behaviour | Only identity auth endpoints are rate-limited; tenant and access management endpoints are not |
| Expected behaviour | Write endpoints (tenant create/update/delete, member add/remove, role create/update/delete, assignment changes) should be rate-limited to prevent abuse |
| Root cause | Rate limiting configured only for identity auth |
| Impact | Brute-force or abuse on tenant/access write endpoints |
| Recommendation | Add rate limiting policies for tenant and access write endpoints |
| Dependencies | None |

### Finding D-19: `AccessGuard.RequireAsync` does not explicitly check `IsAuthenticated` before `UserId` null check (Design observation — Low)

| Field | Value |
|---|---|
| ID | D-19 |
| Category | Security / authorization |
| Severity | Low |
| Confidence | Confirmed |
| Component | `CoreKit.Access/Services/AccessGuard.cs` line 29 |
| Evidence | `if (!_actor.IsAuthenticated || _actor.UserId is not { } userId || userId == Guid.Empty) return AccessErrors.NotAuthenticated;` — the check is compound but correct. The earlier review finding (`current-state.md` line 95) noted "guard authorization path did not explicitly check IsAuthenticated" — but the code does check it. |
| Re-evaluation | **False positive** — the code correctly checks `!_actor.IsAuthenticated` first. |
| Classification | False positive / rejected concern |

### Finding D-20: `PlatformMembershipService` error-code propagation for unauthenticated callers (Confirmed defect — Medium, from prior review)

| Field | Value |
|---|---|
| ID | D-20 |
| Category | Security / error handling |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Access/Services/PlatformMembershipService.cs` |
| Evidence | `AddMemberAsync` (line 46-48): `guardResult` failure returns `guardResult.Error.Code`. For unauthenticated callers, `RequirePlatformScopePermissionAsync` returns `AccessErrors.NotAuthenticated` (code `access.not_authenticated`). But `current-state.md` line 95 reports that guard failures "collapse to access.denied." Looking at `AccessGuard.RequirePlatformScopePermissionAsync` line 61-62: unauthenticated returns `AccessErrors.NotAuthenticated`. So the code appears correct. However, the prior review flagged this. Need to check `AccessErrors.NotAuthenticated` code value. |
| Re-evaluation | The code returns `NotAuthenticated` for unauthenticated. The prior review finding may have been about a different version. Current code at HEAD `12eac87d` appears correct for this specific path. However, `AddMemberAsync`/`RevokeMemberAsync` return `guardResult.Error.Code` which would be `access.not_authenticated` for unauthenticated — this is correct. |
| Classification | Needs verification — prior review flagged it, but current code appears to handle it correctly. The `AccessErrors` class was not fully inspected. |

**Correction: Finding D-20 needs verification.** The `AccessErrors` class (`CoreKit.Access/Common/AccessErrors.cs`) was not read in full. The guard returns `AccessErrors.NotAuthenticated` which should map to `access.not_authenticated`. Without reading `AccessErrors.cs`, cannot confirm the error code string. Mark as needs verification.

### Finding D-21: No raw SQL, all parameterized via EF Core (Confirmed good practice — Informational)

| Field | Value |
|---|---|
| ID | D-21 |
| Category | Security |
| Severity | Informational |
| Confidence | Confirmed |
| Component | All DbContexts |
| Evidence | All queries use LINQ/EF Core. `ExecuteUpdateAsync` used for bulk revocation (`RefreshTokenRevocation.cs` line 14-16) which is parameterized. No raw SQL strings found. |
| Actual behaviour | No SQL injection risk from raw SQL |
| Recommendation | Maintain this practice |
| Dependencies | None |

### Finding D-22: Tenant isolation enforced at service layer, not DbContext layer for TenantDbContext (Design observation — Informational)

| Field | Value |
|---|---|
| ID | D-22 |
| Category | Architecture |
| Severity | Informational |
| Confidence | Confirmed |
| Component | `CoreKit.Tenant/Services/TenantAccessGuard.cs` |
| Evidence | `TenantAccessGuard.EnsureCanAccessAsync` enforces tenant boundary at the service level. `TenantDbContext` itself has no query filter (it's platform-level). |
| Actual behaviour | Tenant isolation is service-enforced, not context-enforced |
| Expected behaviour | This is acceptable for a platform-level store. Future tenant-scoped modules should use `TenantAwareDbContext` |
| Recommendation | Document the boundary clearly |
| Dependencies | Related to D-07 |

### Finding D-23: `UserService` calls `IAccessMaintenance` (unguarded) from trusted service context (Architecture — Informational)

| Field | Value |
|---|---|
| ID | D-23 |
| Category | Architecture |
| Severity | Informational |
| Confidence | Confirmed |
| Component | `CoreKit.Identity/Services/UserService.cs` |
| Evidence | `UserService` calls `_maintenance.AddPlatformMemberAsync`, `_maintenance.RemovePlatformMemberAsync`, `_maintenance.SetPlatformUserRolesAsync` — all on `IAccessMaintenance` which has no permission checks (by design, documented as "trusted code"). |
| Actual behaviour | Identity services (which are already authorized via `HasPlatformPermissionAsync`) call unguarded maintenance methods |
| Expected behaviour | Correct — the service layer has already authorized; maintenance is the trusted execution path |
| Recommendation | Maintain; document the trust boundary |
| Dependencies | None |

### Finding D-24: No migration history verification evidence (Missing verification — Medium)

| Field | Value |
|---|---|
| ID | D-24 |
| Category | Verification / database |
| Severity | Medium |
| Confidence | Confirmed |
| Component | All migrations |
| Evidence | 14 migration files committed across Access (4), Identity (7), Tenant (3). No evidence any have been applied to a database. `__EFMigrationsHistory` state unknown. |
| Actual behaviour | Migrations exist in code; database state unknown |
| Expected behaviour | Migrations should be applied to a positively identified dev database and verified |
| Root cause | No migration application has been performed or recorded |
| Impact | Schema correctness, compatibility, and migration ordering are unverified |
| Recommendation | Apply migrations to positively identified dev database; verify `__EFMigrationsHistory` and schema |
| Dependencies | Requires Project Owner approval (DB operation) |

### Finding D-25: Architecture tests exist but not executed (Missing verification — Medium)

| Field | Value |
|---|---|
| ID | D-25 |
| Category | Testing |
| Severity | Medium |
| Confidence | Confirmed |
| Component | `CoreKit.Architecture.Tests` |
| Evidence | 7 test files exist (`ModuleBoundaryTests`, `ProjectReferenceBoundaryTests`, `MsBuildProjectEvaluator`, `SolutionInventory`, `IvtMechanismScanner`, etc.). `current-state.md` line 86: "Tests were not run as part of this inspection." |
| Actual behaviour | Architecture boundary tests exist but have not been executed |
| Expected behaviour | Boundary tests should pass to enforce module independence |
| Root cause | Test execution not yet performed |
| Impact | Module boundary violations would not be caught |
| Recommendation | Execute architecture tests; fix any failures |
| Dependencies | Requires build execution (currently prohibited in audit) |

---

## Part E — Security Report

### E.1 Threat model

| Threat actor | Capabilities | Goals |
|---|---|---|
| Anonymous user | Network access to endpoints | Enumerate accounts, brute-force login, access protected data |
| Authenticated tenant member | Valid JWT, tenant scope | Access other tenants' data, escalate privileges |
| Tenant owner | Valid JWT, owner flag in tenant | Assign roles, change ownership, modify tenant |
| Platform administrator | Platform membership + roles | Manage all users, roles, platform membership |
| Malicious insider | Valid credentials, legitimate access | Exceed authorized permissions, exfiltrate data |
| Compromised session | Stolen JWT or refresh token | Access resources as victim, maintain persistence |

### E.2 Security findings summary

| ID | Severity | Type | Component | Summary |
|---|---|---|---|---|
| D-02 | High | Confirmed defect | Tenant | Uncommitted cache invalidation on member removal — stale permissions persist |
| D-03 | Medium | Confirmed security gap | Identity | JWT access tokens valid until expiry after credential change (`SecurityVersion` not validated) |
| D-04 | Medium | Design weakness | Identity/Access | Identity → Access dependency direction inverted vs D-002 intent |
| D-18 | Medium | Missing capability | All modules | No rate limiting on tenant/access write endpoints |
| D-20 | Medium | Needs verification | Access | Platform membership error-code propagation for unauthenticated callers |
| D-24 | Medium | Missing verification | Database | Migrations committed but never applied/verified |
| D-25 | Medium | Missing verification | Tests | Architecture tests not executed |
| D-08 | — | False positive | Tenant | Cache invalidation on owner change — actually implemented |
| D-09 | — | False positive | Access | Cache stampede — token comparison prevents stale data |
| D-10 | — | False positive | Access | Cache key collision — scope is in key |
| D-19 | — | False positive | Access | `IsAuthenticated` check — actually present |

### E.3 Detailed security scenarios

**S-1: Stale permissions after tenant member removal (D-02)**
1. User is a tenant member with cached Access permissions (TTL = e.g. 300 seconds).
2. Admin removes user from tenant via `RemoveMemberAsync`.
3. `TenantMemberRemoved` event published; handler `TenantMemberRemovedToAccessMaintenance` should call `IAccessMaintenance.RemoveUserAsync` to delete assignments and invalidate Access cache.
4. **But** the handler is not registered in committed code (unstaged modification only) and the handler class file is untracked.
5. Result: User's cached Access permissions remain valid until cache TTL expires. User can still perform tenant-scoped actions after removal.
6. Severity: High. Immediate revocation is a security requirement for membership termination.

**S-2: Stale access token after password change (D-03)**
1. User's JWT access token is stolen (e.g., via XSS or log leak).
2. User changes password; `SecurityVersion` is incremented; all refresh tokens are revoked.
3. **But** JWT contains no `SecurityVersion` claim, and token validation middleware does not check it.
4. Result: Attacker's JWT remains valid until expiry (up to 120 minutes).
5. Severity: Medium. Refresh token rotation mitigates long-term persistence, but short-term access remains.

**S-3: Account enumeration (mitigated)**
1. Attacker attempts login with unknown email.
2. `AuthService` hashes dummy password (constant-time mitigation) and returns `InvalidCredentials`.
3. Same response as wrong password for known email.
4. Status: **Mitigated** — no enumeration vector from timing or error messages.

**S-4: Privilege escalation via role assignment (mitigated)**
1. User with `assignments.manage` tries to assign a role with `roles.manage` permission to another user.
2. `AccessAssignmentService.SetRolesAsync` calls `AccessGuard.Holds(access, changedRoles.SelectMany(r => r.Permissions))`.
3. If actor doesn't hold all permissions of the target roles, returns `AccessErrors.Escalation`.
4. Status: **Mitigated** — escalation prevention is implemented.

**S-5: Last administrator lockout (mitigated)**
1. Admin attempts to deactivate/delete the last active administrator.
2. `AdministratorGuard.IsLastActiveAdministratorAsync` checks `ListUserIdsWithRoleAsync(PlatformScope, Administrator)`.
3. Returns conflict if count == 1 and the target is that user.
4. Status: **Mitigated** — prevents total lockout.

**S-6: Cross-tenant access (mitigated)**
1. User resolved to Tenant A attempts to access Tenant B's data.
2. `TenantAccessGuard.EnsureCanAccessAsync(B)` returns `CrossTenantAccess` because `_currentTenant.Id != B` and caller lacks platform access.
3. Status: **Mitigated**.

**S-7: Refresh token reuse (mitigated)**
1. Attacker reuses a refresh token after it was already used.
2. `RefreshAsync` finds `RevokedAt is not null` and revokes all tokens for the user's family.
3. Status: **Mitigated** — family-wide revocation on reuse.

**S-8: Rate limit abuse on tenant/access writes (D-18)**
1. Attacker repeatedly calls `POST /tenants/{id}/members` or `PUT /access/roles/{id}/permissions`.
2. No rate limiting on these endpoints.
3. Result: Potential abuse, brute-force on role permissions, or resource exhaustion.
4. Severity: Medium.

**S-9: Committed secrets history (carried forward)**
1. `current-state.md` reports `appsettings.Development.json` was previously committed and may contain credentials.
2. Removing from tree does not remove from Git history.
3. Severity: High (historical). Rotate affected secrets.

### E.4 Security recommendations (priority order)

1. **Commit and register** `TenantMemberRemovedToAccessMaintenance` handler (D-02) — immediate security gap.
2. **Implement JWT `SecurityVersion` validation** or document the accepted 120-minute stale-token window (D-03).
3. **Add rate limiting** to tenant and access write endpoints (D-18).
4. **Rotate secrets** that may have been committed in Git history (carried forward from M0).
5. **Verify error-code propagation** for unauthenticated platform membership calls (D-20) by reading `AccessErrors.cs`.

---

## Part F — Code Quality Report

### F.1 SRP violations and responsibility concentration

| Class | Responsibilities | Assessment |
|---|---|---|
| `TenantService` | Tenant CRUD, status transitions, slug generation, audit, events, cache invalidation | Large but cohesive — all tenant lifecycle. Acceptable. |
| `UserService` | User CRUD, role assignment, platform membership delegation, administrator guard, email immutability | Large but cohesive — all identity user management. Acceptable. |
| `AuthService` | Login, refresh, logout, change-password, email verification coordination | Coordinates auth workflows. Acceptable. |
| `AccessGuard` | Scope validation, authentication check, fresh/cached access resolution, escalation check | Single responsibility: authorization gate. Well-factored. |
| `PlatformMembershipService` | Add/revoke platform members, concurrency handling, cache invalidation, audit | Cohesive. Well-factored. |
| `AccessCache` | Cache storage, generation tracking, invalidation, capacity management | Cohesive caching concern. Well-factored. |
| `TenantMembershipService` | Member add/remove/set-owner, concurrency, default tenant, cache invalidation, events | Large but cohesive — all membership operations. Acceptable. |

No clear SRP violations. Service classes are large but domain-cohesive.

### F.2 Tight coupling and unnecessary dependencies

| Coupling | Direction | Assessment |
|---|---|---|
| Identity → Access | Project reference | Conceptually questionable per D-002, but not circular. Justified by platform authorization needs. |
| Tenant → Access | Project reference | Correct per D-016. Tenant uses Access contracts only. |
| TenantActor → `ICurrentUser` (Access.Abstractions) | Interface dependency | Transitive Access coupling in Tenant. `TenantActor` only uses `UserId`. Could use a thinner contract. |
| Identity → `ICurrentUser` (Access.Abstractions) | Interface dependency | Identity implements Access's contract. Acceptable — Access doesn't depend on Identity internals. |
| `TenantClaimsContributor` → `ITenantMemberRepository` | Repository dependency | Tenant contributes token claims. Correct — Tenant owns tenant data. |

### F.3 Poor abstractions and complexity

| Abstraction | Issue |
|---|---|
| `ICurrentUser` in Access.Abstractions | Contains `Permissions` property which is semantically odd — current user shouldn't carry permissions (they come from Access). `TenantActor` ignores it. Could be split into `ICurrentUser` (UserId, IsAuthenticated) and a separate permission contract. |
| `EffectiveAccess` | Well-designed record. No issues. |
| `AccessScope` | Well-designed value type. No issues. |
| `AccessCache` | Complex but justified — generation-based invalidation with epoch overflow. Well-documented. |

### F.4 Harmful duplication

| Duplication | Location | Assessment |
|---|---|---|
| `AccessResult<T>` and `IdentityResult<T>` | Access.Common vs Identity.Common | Separate result types per module. Intentional — modules own their error contracts. Not harmful duplication. |
| `TenantResult<T>` | Tenant.Common | Same pattern. Intentional. |
| Permission declaration | `AddAccessPermissions` called by Identity and SampleHost | Each module declares its own permissions in the Access catalog. Intentional — Access owns the catalog, modules contribute. |
| Validation logic | `AccessValidation`, `TenantValidation`, `Identity.Validation.*` | Separate validators per module. Intentional — different rules, different ownership. |

No harmful duplication identified. Repetition is intentional and module-owned.

### F.5 Naming problems

| Current name | Issue | Severity |
|---|---|---|
| `TenantEntity` | Named to avoid namespace/class ambiguity with `CoreKit.Tenant.Tenant` | Acceptable, documented in class comment |
| `ICurrentUser.Permissions` | Semantically odd — current user doesn't hold permissions; Access does | Low — could confuse consumers |
| `AccessMember` vs `TenantMemberInfo` | Two similar record types for membership in different modules | Acceptable — different ownership, different concerns |
| `RequireAccess` endpoint filter | Not inspected in full; name suggests access permission filter | Low |
| `PlatformAccessMembership` vs `TenantAccessMembership` | Both implement `IAccessMembershipAdapter`, clearly named | Good |

No critical naming problems.

### F.6 Maintainability risks

1. **Empty SharedKernel** — contracts are scattered; future shared primitives have no home (but task constraints say "Do not introduce SharedKernel").
2. **Identity → Access dependency** — Identity cannot evolve independently of Access.
3. **Uncommitted security feature** — cache invalidation handler exists but isn't in committed code; risk of being lost or forgotten.
4. **Corrupted test file** — blocks test project compilation; indicates possible process gaps in file creation.
5. **No runtime verification** — zero test execution, zero migration application evidence.

---

## Part G — Database and Performance Report

### G.1 Schema design

| Table | Module | Key | Indexes | Notes |
|---|---|---|---|---|
| `Identity_Users` | Identity | `Id` | Email unique (via config), `AccessFailedCount`, `LockoutEnd` | Lockout columns added by migration |
| `Identity_RefreshTokens` | Identity | `Id` | UserId, FamilyId, TokenHash | Refresh token storage |
| `Identity_EmailVerificationTokens` | Identity | `Id` | UserId, NormalizedEmail, ExpiresAt | Email verification |
| `ACCESS_Roles` | Access | `Id` | `(ScopeType, ScopeId, NormalizedName)` unique | Role per scope |
| `ACCESS_RolePermissions` | Access | `(RoleId, Permission)` | — | Permission links |
| `ACCESS_Assignments` | Access | `(RoleId, UserId)` | `(ScopeType, ScopeId, UserId)`, `(RoleId, ScopeType, ScopeId)` | Composite FK to Roles via alternate key |
| `ACCESS_PlatformMembers` | Access | `UserId` | `(PlatformId, UserId)` | Platform membership |
| `ACCESS_AuditEntries` | Access | `Id` | `(ScopeType, ScopeId, OccurredAt)` | Audit trail |
| `TENANT_Tenants` | Tenant | `Id` | `Slug` unique, `Status` | Tenant registry |
| `TENANT_Settings` | Tenant | `(TenantId, Key)` | — | Tenant settings |
| `TENANT_Members` | Tenant | `(TenantId, UserId)` | `UserId`, query filter `RemovedAt == null` | Soft delete, generation |
| `TENANT_AuditEntries` | Tenant | `Id` | `(TenantId, OccurredAt)` | Audit trail |

### G.2 Transaction design

- Each unit of work (`AccessUnitOfWork`, `TenantUnitOfWork`, `Identity` repositories) performs exactly one `SaveChangesAsync`. EF Core wraps in implicit transaction.
- **Cross-module operations are NOT atomic**: Identity `CreateAsync` calls `_userRepository.CreateAsync` (saves), then `_maintenance.AddPlatformMemberAsync` and `_maintenance.SetPlatformUserRolesAsync` (Access DbContext saves). These are two separate DbContexts/transactions. If the Access call fails after Identity save, the user exists without platform membership. This is a **partial failure risk**.
- `RefreshTokenRevocation.RevokeAllForUserAsync` uses `ExecuteUpdateAsync` — single statement, atomic.
- `AuthService.LoginAsync` uses `TryCompleteSuccessfulLoginAsync` (atomic, version-guarded) then `SessionIssuer.IssueForLoginAsync` (guarded refresh token insert). Two steps but guarded.

### G.3 Concurrency handling

| Entity | Concurrency mechanism | Assessment |
|---|---|---|
| `User` | `SecurityVersion` incremented on credential change; version-guarded updates | Good |
| `TenantEntity` | `ConcurrencyStamp` on update/delete | Good |
| `AccessRole` | `ConcurrencyStamp` on update/delete | Good |
| `AccessAssignment` | `MembershipGeneration` on reassignment | Good |
| `TenantMember` | `MembershipGeneration` on remove/rejoin | Good |
| `AccessPlatformMember` | `MembershipGeneration` on revoke/rejoin | Good |

### G.4 Performance observations

- `ListRolesForUserAsync` uses `.Include(r => r.Permissions)` with a correlated subquery on `Assignments` — could be N-ish but acceptable for typical role counts.
- `AccessCache` uses in-memory `IMemoryCache` — does not scale to multiple instances without invalidation coordination.
- `TenantClaimsContributor` queries DB on every token issuance — minor latency.
- No read replicas, no sharding, no outbox pattern — appropriate for current stage.
- Pagination: `PageSize` clamped to 1–100 (Access) and 1–100/20 (Tenant/Identity) — bounded. Good.

### G.5 Database security

- All queries via EF Core (parameterized). No raw SQL.
- `ExecuteUpdateAsync` for bulk revocation — parameterized.
- Unique constraints prevent duplicate roles per scope and duplicate tenant slugs.
- Soft-delete query filter on `TenantMember` — but services also check `RemovedAt` explicitly in some paths. Defense in depth.
- No row-level security or tenant-level DB schemas — single shared database per module (D-011). Tenant isolation enforced at application level.

### G.6 Missing indexes

- `ACCESS_Assignments.UserId` standalone index — not currently needed but may be for future cross-scope user lookup.
- No covering indexes identified as missing for current query patterns.

---

## Part H — Testing Assessment

### H.1 Existing test coverage

| Test project | Test files | Approximate test classes | Status |
|---|---|---|---|
| `CoreKit.Access.Tests` | 9 | `PlatformMembershipTests` (30+ tests), `RoleServiceTests`, `AssignmentServiceTests`, `AccessAuditLogTests`, `AuthorityAndCacheTests`, `DefaultsAndHostingTests`, `MaintenanceAndAuditTests`, `MigrationSnapshotTests`, `RequireAccessFilterTests`, `RoleAssignmentScopeConsistencyTests` | Not executed |
| `CoreKit.Identity.Tests` | ~9 | `UserServiceTests`, `CredentialVersionTests`, `MobileValidatorTests`, `PasswordHasherTests`, `PasswordPolicyTests`, `PostgresLoginTransitionTests` | Not executed |
| `CoreKit.Tenant.Tests` | ~13 | `TenantServiceTests`, `IsolationTests`, `AuditEventsAndInfoTests`, `ContextAndGuardTests`, `IntegrationEventsTests`, `IntegrationTests`, `ResolutionTests`, `RulesTests`, `SettingsAndMembershipTests`, `TenantAccessAuthorizationTests`, `TenantAccessIntegrationTests`, `TenantMemberRemovedAccessCacheTests` (CORRUPTED) | Not executed; 1 file corrupted |
| `CoreKit.Architecture.Tests` | 7 | `ModuleBoundaryTests`, `ProjectReferenceBoundaryTests`, `IvtMechanismScannerTests` | Not executed |

### H.2 Coverage gaps

| Area | Coverage | Gap |
|---|---|---|
| Login/lockout workflow | `PostgresLoginTransitionTests` (Identity) | Not executed; lockout end-to-end not verified |
| Refresh token rotation | Partial (Identity.Tests) | Not executed |
| Tenant member removal + Access cache invalidation | `TenantMemberRemovedAccessCacheTests` (corrupted) | **Corrupted file** — this critical security test cannot run |
| Platform membership | `PlatformMembershipTests` (Access) | Comprehensive; not executed |
| Role escalation prevention | `RoleServiceTests`, `RoleAssignmentScopeConsistencyTests` | Not executed |
| Cross-tenant access denial | `ContextAndGuardTests`, `IsolationTests` | Not executed |
| Architecture boundaries | `ProjectReferenceBoundaryTests`, `ModuleBoundaryTests` | Not executed |
| Concurrency/race conditions | `ConcurrentAddMemberIsIdempotent` (Access) | Not executed |

### H.3 Test quality observations

- `PlatformMembershipTests` is comprehensive — covers generation refresh, concurrent add, unauthenticated denial, stale cache, fixed platform scope. Excellent test design.
- `TenantMemberRemovedAccessCacheTests` is corrupted — critical gap for the security feature in D-02.
- No test execution results exist for any project.
- Architecture tests enforce boundaries at compile time but haven't been run.
- No PostgreSQL integration tests identified (tests use SQLite in-memory via `UseSqlite` in some test setups).

### H.4 Required regression tests

1. Fix/rewrite `TenantMemberRemovedAccessCacheTests` — verify member removal invalidates Access cache.
2. Add test: access token remains valid after `SecurityVersion` change (documenting current behavior) or test new validation (if implemented).
3. Add test: unauthenticated caller to `AddMemberAsync`/`RevokeMemberAsync` returns `access.not_authenticated` (D-20 verification).
4. Add test: rate limiting on tenant/access write endpoints (if implemented).
5. Execute architecture tests and fix failures.

---

## Part I — Documentation Assessment

### I.1 Documentation vs code consistency

| Document | Claim | Code reality | Status |
|---|---|---|---|
| `architecture.md` §6 | Tenant–Access integration via `IAccessScopeContext`, `IAccessMembership`, `IAccessService`, `IPermissionChecker` | Implemented via adapters | **Consistent** |
| `architecture.md` §9 | Lock 15 min after 5 failed attempts | Implemented (`RegisterFailedLoginAttemptAsync` threshold 5, 15 min) | **Consistent** |
| `architecture.md` §10 | One `SaveChangesAsync` per unit of work | Implemented | **Consistent** |
| `architecture.md` §5 | Tenant must not depend directly on Identity | No direct project reference; `TenantActor` uses `ICurrentUser` from Access.Abstractions | **Mostly consistent** (transitive coupling) |
| `current-state.md` | `DK-ACCESS-PLATFORM-SCOPE-01` Under review / Request Changes | Commit `70d757f` exists with fix; not accepted | **Consistent** |
| `current-state.md` | `RemoveIdentityRbacTables` migration committed | Migration exists at `CoreKit.Identity/Migrations/20261114113050_` | **Consistent** |
| `current-state.md` | Committed-secret risk from M0 | `appsettings.Development.json` previously committed | **Consistent (carried forward)** |
| `decisions.md` D-017 | Standing automation approved | Accepted 2026-11-13 | **Consistent** |

### I.2 Outdated or missing documentation

| Issue | Detail |
|---|---|
| No API documentation | Endpoints documented only in code comments; no OpenAPI/Swagger or API spec |
| No deployment documentation | Production migration procedure not documented; auto-migration in `Program.cs` marked "development convenience" but no alternative documented |
| No security documentation | Threat model, token lifetimes, rotation policy not documented outside code comments |
| `SharedKernel` referenced in `architecture.md` but empty | Contradiction: architecture says SharedKernel holds contracts; it's empty |
| `D-002` says "Identity may provide only the stable public identity contracts/claims required by Access" | Code has Identity → Access dependency, direction not explicitly addressed in decisions |

### I.3 Documentation recommendations

- Document the production deployment procedure (migrations as separate step, not auto-applied).
- Document the trust boundary for `IAccessMaintenance` (trusted system code, never exposed to callers).
- Document that `CurrentUserService.Roles`/`Permissions` are JWT-derived and potentially stale.
- Document the accepted access-token staleness window (or plan to fix it).
- Resolve the `SharedKernel` contradiction — either populate it or update `architecture.md` to reflect that contracts live in module-owned `Abstractions` namespaces.

---

## Part J — Architecture Recommendations

### J.1 Keep unchanged

| Item | Reason |
|---|---|
| Modular monolith with Access as authorization authority | Correctly implemented, no circular dependencies |
| Access project references (leaf module) | Access correctly has no dependencies on Identity/Tenant |
| `EffectiveAccess`, `AccessScope`, `AccessCache` designs | Well-factored, generation-based, fail-closed |
| Optimistic concurrency (`ConcurrencyStamp`, `SecurityVersion`, `MembershipGeneration`) | Correctly implemented across all mutable entities |
| Refresh token rotation with family revocation | Correct security implementation |
| Lockout atomic transition (5 attempts / 15 min) | Correct implementation |
| `Result<T>` error pattern | Consistent across modules |
| Audit logging on all mutations | Good practice |
| Architecture tests enforcing boundaries | Correct enforcement mechanism |

### J.2 Small improvements (no architectural change)

| Item | Effort | Benefit |
|---|---|---|
| Fix/rewrite corrupted `TenantMemberRemovedAccessCacheTests.cs` | Small | Unblocks test project; covers critical security feature |
| Commit `TenantMemberRemovedToAccessMaintenance` handler + DI registration | Small | Completes D-016 cache invalidation security feature |
| Add rate limiting to tenant/access write endpoints | Medium | Abuse protection |
| Add health checks (`/health`) | Medium | Operational readiness |
| Remove/gate auto-migration in `Program.cs` | Small | Production deployment safety |
| Document `CurrentUserService.Permissions` as stale/informational | Small | Prevents misuse |
| Add `UserId` index on `ACCESS_Assignments` (if needed) | Small | Future query performance |

### J.3 Required corrections

| Item | Finding | Approval required? |
|---|---|---|
| Implement JWT `SecurityVersion` validation or document accepted staleness window | D-03 | Yes (security behavior change) |
| Verify error-code propagation for unauthenticated platform membership calls | D-20 | No (code inspection) |
| Apply migrations to dev database and verify schema | D-24 | Yes (DB operation) |
| Execute architecture tests and fix failures | D-25 | No (test execution) |

### J.4 Proposed architectural changes

| Item | Current | Proposed | Benefit | Risk | Approval |
|---|---|---|---|---|---|
| Identity → Access dependency direction | Identity references Access | Evaluate whether a thinner contract can decouple them, or formally accept current direction | Clearer ownership boundary | Medium (refactoring Identity services) | Yes (architecture change) |
| `ICurrentUser.Permissions` | Current user carries permissions claim | Remove `Permissions` from `ICurrentUser`; consumers use `IPermissionChecker` | Clearer semantics | Low (interface change) | Yes (API change) |
| SharedKernel | Empty | Leave empty per task constraint ("Do not introduce SharedKernel") | N/A | N/A | N/A |

### J.5 Future considerations

| Item | Why deferred |
|---|---|
| Distributed cache (Redis) for multi-instance cache invalidation | Not needed until multiple instances |
| Outbox pattern for cross-module event reliability | Current in-process events acceptable |
| Read replicas / sharding | Single database sufficient for current stage |
| API versioning | Not yet needed |
| CORS/HSTS/forwarded-headers | Production deployment concern |

---

## Part K — Overall Maturity Assessment

| Area | Current status | Evidence | Remaining gap |
|---|---|---|---|
| Identity | In progress | Lockout implemented, RBAC tables removed, JWT/refresh tokens implemented | `SecurityVersion` JWT validation not implemented; tests not run; migrations not applied |
| Access | In progress | Roles/permissions/assignments/platform membership implemented; cache with generation invalidation | Tests not run; migrations not applied; rate limiting missing |
| Tenant | In progress | Lifecycle, membership, context, Access integration foundation implemented | Cache invalidation handler uncommitted; test file corrupted; per-operation permission enforcement pending |
| Security | Partial | Strong design (fail-closed, rotation, lockout, no enumeration) | Stale access tokens after credential change; uncommitted cache invalidation; no rate limiting on writes; committed-secret history |
| Data integrity | In progress | Optimistic concurrency, soft delete, unique constraints, generation-based membership | Cross-module transactions not atomic; migrations not verified |
| API design | Implemented | Consistent `Result<T>`, RESTful endpoints, permission-based authorization | No versioning, no health checks, no OpenAPI spec |
| Testing | In progress | Test projects exist with good test design (`PlatformMembershipTests`) | Zero tests executed; one corrupted file; no execution evidence |
| Architecture | Implemented | Modular monolith, clear boundaries, no circular deps | Identity → Access direction; empty SharedKernel; `ICurrentUser.Permissions` semantic issue |
| Performance | Adequate | Bounded pagination, in-memory cache, EF Core queries | Multi-instance cache invalidation not addressed; no distributed cache |
| Operations | Minimal | Startup auto-migration (dev only) | No health checks, no HSTS/CORS, no production deployment docs |
| Documentation | In progress | Architecture, decisions, roadmap, current-state all present | Contradictions (SharedKernel); no API spec; no deployment docs |

**Overall maturity:** Foundation layer implemented but **not verified**. The code demonstrates sound design patterns, but zero runtime verification exists (no builds run, no tests executed, no migrations applied). There are two concrete defects (corrupted test file, uncommitted security handler) and one security gap (stale access tokens).

---

## Part L — Remediation and Development Roadmap

### L.1 Task classification

| Priority | Definition |
|---|---|
| 1. Must fix before further feature development | Build blockers, security gaps, corrupted files |
| 2. Must fix before production | Rate limiting, health checks, migration verification, secrets rotation |
| 3. Required for next milestone | Per-operation permission enforcement, platform authorization correction |
| 4. Important maintainability improvement | Documentation, dependency direction review, index additions |
| 5. Future scalability improvement | Distributed cache, read replicas, outbox pattern |
| 6. Optional / not recommended | None identified |

### L.2 Remediation tasks

| Task ID | Title | Problem | Priority | Dependencies | Approval |
|---|---|---|---|---|---|
| R-01 | Fix or remove corrupted test file `TenantMemberRemovedAccessCacheTests.cs` | D-01 | 1 | None | No (test file) |
| R-02 | Commit `TenantMemberRemovedToAccessMaintenance` handler + DI registration | D-02 | 1 | R-01 | No (within approved task scope if assigned) |
| R-03 | Implement JWT `SecurityVersion` validation or document accepted staleness window | D-03 | 1 | None | Yes (security behavior change) |
| R-04 | Add rate limiting to tenant and access write endpoints | D-18 | 2 | None | No |
| R-05 | Add health checks (`/health`) for all three DbContexts | D-12 | 2 | None | No |
| R-06 | Remove/gate auto-migration in `Program.cs` (dev-only) | D-13 | 2 | None | No |
| R-07 | Apply migrations to positively identified dev database; verify schema | D-24 | 2 | None | Yes (DB operation) |
| R-08 | Execute architecture tests; fix any boundary violations | D-25 | 2 | R-01 | No |
| R-09 | Verify error-code propagation for unauthenticated platform membership calls | D-20 | 2 | None | No (code inspection) |
| R-10 | Document production deployment procedure | Documentation | 4 | R-07 | No |
| R-11 | Document `CurrentUserService.Permissions` as stale/informational | D-11 | 4 | None | No |
| R-12 | Resolve `SharedKernel` contradiction (update `architecture.md` or populate) | D-05 | 4 | None | Yes (documentation/architecture) |
| R-13 | Evaluate Identity → Access dependency direction | D-04 | 4 | None | Yes (architecture decision) |
| R-14 | Add `UserId` index on `ACCESS_Assignments` if cross-scope lookup needed | D-16 | 5 | None | No |
| R-15 | Implement distributed cache invalidation for multi-instance | Future | 5 | None | No |

### L.3 Roadmap ordering rationale

1. **R-01, R-02 first**: Build blocker and uncommitted security feature. Must be resolved before any further development on Tenant or Access.
2. **R-03**: Security gap — stale access tokens after credential change. Should be decided before production.
3. **R-04, R-05, R-06**: Production readiness — rate limiting, health checks, migration gating.
4. **R-07**: Database verification — requires Project Owner approval and positive DB identification.
5. **R-08**: Architecture test execution — validates module boundaries.
6. **R-09**: Security verification — code inspection only.
7. **R-10 through R-13**: Documentation and architecture decisions — can proceed in parallel with R-07.
8. **R-14, R-15**: Future scalability — deferred.

### L.4 Independent and dependent work

| Independent (can proceed in parallel) | Dependent (must wait) |
|---|---|
| R-01 (fix test file) | R-08 (depends on R-01 for build) |
| R-09 (code inspection) | R-07 (requires DB approval) |
| R-10, R-11 (documentation) | R-13 (architecture decision) |
| R-14, R-15 (future) | R-03 (security decision) |

---

## Part M — Final Inventory

### M.A. Already implemented and verified

**Nothing is verified.** No builds, tests, migrations, or database operations have been executed against the current state. The following are **implemented but not verified**:

- Identity lockout (5 attempts / 15 min) — code exists, not runtime-tested
- Refresh token rotation with family revocation — code exists, not runtime-tested
- Access roles/permissions/assignments with escalation prevention — code exists, not runtime-tested
- Platform membership with generation-based invalidation — code exists, not runtime-tested
- Tenant lifecycle, membership, context resolution — code exists, not runtime-tested
- Tenant–Access integration adapters — code exists, handler uncommitted
- Architecture boundary tests — code exists, not executed

### M.B. Implemented but incomplete, defective, or unverified

| Item | Finding(s) | Status | Limitations |
|---|---|---|---|
| Tenant–Access cache invalidation on member removal | D-02 | Handler + DI registration uncommitted; handler file untracked | Not in committed code; not tested |
| JWT `SecurityVersion` validation | D-03 | Not implemented; access tokens valid until expiry after credential change | Stale token window up to 120 min |
| Rate limiting on tenant/access writes | D-18 | Not implemented | Abuse vulnerability |
| Migrations (Access, Identity, Tenant) | D-24 | Committed, never applied to verified DB | Schema correctness unknown |
| Architecture tests | D-25 | Exist, not executed | Boundary violations undetected |
| Test project `CoreKit.Tenant.Tests` | D-01 | Corrupted file blocks compilation | Cannot execute any tenant tests |
| Health checks | D-12 | Not implemented | No operational readiness |
| Auto-migration in `Program.cs` | D-13 | Implemented as dev convenience | Not production-safe |

### M.C. Not implemented

| Item | Roadmap reference | Status |
|---|---|---|
| Store module | M8 | Pending |
| Catalog module | M8 | Pending |
| Cart & Checkout | M9 | Pending |
| Orders & Payments | M10 | Pending |
| Fulfillment | M11 | Pending |
| API & Website | M12 | Pending |
| Marketplace | M13 | Pending |
| SaaS Subscription (`CoreKit.Subscription`) | M6 | Pending |
| Domain routing | M7 | Pending |
| Per-operation Tenant permission enforcement | M5 S4.4 | Pending |
| Platform authorization (corrected) | M5 S4.3 | Under review / Request Changes |
| Identity role/permission ownership transfer completion | M3 D-002 | Schema removed; ownership in Access |
| DK-IDENTITY-BOUNDARY-01 | Blocked | Baseline mismatch; requires Project Owner approval |

---

## Part N — Decision Summary for Project Owner

### 1. Is the current foundation safe enough to continue development?

**Conditionally yes.** The foundation code demonstrates sound security and architecture patterns, but it is **unverified at runtime**. Development can continue on new features only after:
- R-01 (fix corrupted test file) and R-02 (commit cache invalidation handler) are complete — these unblock the test project and close a security gap.
- R-03 (stale access token decision) is decided — this is a security behavior question.

Do not proceed to product modules (Store, Catalog, etc.) until these are resolved.

### 2. What must be corrected immediately?

| Correction | Finding | Why immediate |
|---|---|---|
| Fix/remove `TenantMemberRemovedAccessCacheTests.cs` | D-01 | Blocks test project compilation |
| Commit `TenantMemberRemovedToAccessMaintenance` handler + DI registration | D-02 | Security gap: removed members retain cached permissions |
| Decide on JWT `SecurityVersion` validation | D-03 | Security: stolen tokens valid until expiry after credential change |

### 3. What should remain unchanged?

- Modular monolith structure with Access as authorization leaf module
- `EffectiveAccess`, `AccessScope`, `AccessCache` designs
- Refresh token rotation, lockout, optimistic concurrency patterns
- `Result<T>` error pattern, audit logging
- No SharedKernel (per task constraint)
- No SampleHost as future architectural requirement (existing SampleHost is composition code only)

### 4. Which architectural decisions require Project Owner approval?

| Decision | Finding | Approval type |
|---|---|---|
| JWT `SecurityVersion` validation vs. accepted staleness window | D-03 | Security behavior change |
| Identity → Access dependency direction (accept or refactor) | D-04 | Architecture decision |
| `SharedKernel` contradiction resolution | D-05 | Architecture/documentation |
| Migration application to dev database | D-24 | Database operation |

### 5. What work is already complete?

- Identity: users, authentication, JWT, refresh tokens, lockout, email verification, RBAC table removal, administrator guard
- Access: roles, permissions, assignments, platform membership, authorization guard, cache with generation invalidation, audit
- Tenant: lifecycle, membership, context resolution, settings, Access integration foundation
- Architecture: module boundaries, dependency graph, architecture tests (written, not executed)
- Migrations: all written and committed (not applied)

### 6. What work remains?

- Verify runtime behavior (tests, builds, migration application)
- Fix corrupted test file and commit uncommitted security handler
- Close security gaps (stale tokens, rate limiting)
- Add operational readiness (health checks, production deployment docs)
- Implement per-operation Tenant permission enforcement (S4.4)
- Correct platform authorization (S4.3, under review)
- Build product modules (Store, Catalog, Cart, Orders, etc.)

### 7. What should be the next implementation task?

**R-01 + R-02 combined**: Fix or remove the corrupted `TenantMemberRemovedAccessCacheTests.cs`, then commit the `TenantMemberRemovedToAccessMaintenance` handler class and its DI registration together. This unblocks the Tenant test project and closes the cache-invalidated-on-member-removal security gap. Small, bounded, single-concern task.

### 8. Which work can be deferred?

- Product modules (Store, Catalog, Cart, Orders, Payments, Fulfillment, Website, Marketplace) — M6–M14
- Distributed cache, read replicas, outbox pattern — future scalability
- `UserId` index on `ACCESS_Assignments` — only if cross-scope user lookup is needed
- API versioning, OpenAPI spec — not yet needed

### 9. Principal risks if we continue without corrections

| Risk | Impact | Likelihood |
|---|---|---|
| Removed tenant members retain cached Access permissions until TTL expiry | Unauthorized post-removal access | High (if feature used in production) |
| Stolen access tokens valid up to 120 min after password change | Extended attacker window | Medium |
| Corrupted test file blocks CI | Development velocity blocked | High (immediate) |
| Migrations never verified against database | Schema/runtime mismatch in production | High (before any deployment) |
| Committed secrets in Git history | Credential exposure | High (historical, requires rotation) |

---

**End of primary audit report.**  
This report is for independent review by Claude Account 2 and final decision by the Project Owner. No implementation has been performed. No code, test, documentation, migration, database, or Git changes were made during this audit.
