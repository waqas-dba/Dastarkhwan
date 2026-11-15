# DK-TENANT-ACCESS-INTEGRATION-DESIGN-04 — Revised Architecture Design

**Repository:** `waqas-dba/Dastarkhwan`  
**Working directory:** `F:\Developer\waqas-dba\waqas-dba\Dastarkhwan`  
**Baseline verified:** `73932a6125a89f42553732c313a6dda496a42656` (2026-11-15 16:14:29 +0500)  
**Remote HEAD:** `73932a6125a89f42553732c313a6dda496a42656` — exact match  
**Working tree:** Clean (no modifications made)  
**Task type:** Architecture investigation and preparation (design only, read-only)  
**Implementation authorization:** NOT GRANTED  
**Design owner:** OpenCode  
**Independent reviewer:** Claude Account 2  
**Project Manager:** ChatGPT  
**Final authority:** Project Owner  

---

## 1. Baseline Verification

| Field | Value |
|---|---|
| Repository path | `F:\Developer\waqas-dba\waqas-dba\Dastarkhwan` |
| Branch | `master` |
| Local HEAD | `73932a6125a89f42553732c313a6dda496a42656` |
| Local HEAD date | 2026-11-15 16:14:29 +0500 |
| Remote HEAD (`origin/master`) | `73932a6125a89f42553732c313a6dda496a42656` — exact match |
| Working tree | Clean (`git status --short` returned empty; only untracked design/review report files) |
| Required baseline | `73932a6125a89f42553732c313a6dda496a42656` — matches |

**Baseline gate: PASSED.** All source inspection below is against baseline `73932a6125a89f42553732c313a6dda496a42656`.

---

## 2. Repository Evidence and Source Inventory

### 2.1 Project References (Dependency Direction)

| Project | Project References | Runtime Dependencies |
|---|---|---|
| `CoreKit.Access` | None (leaf module) | Called at runtime by Identity, Tenant, SampleHost via DI |
| `CoreKit.Identity` | `CoreKit.Access` | Calls `IAccessService`, `IPermissionChecker`, `IAccessMaintenance` |
| `CoreKit.Tenant` | `CoreKit.Access` | Implements `IAccessScopeContext`, `IAccessMembershipAdapter`, `IAccessTokenClaimsContributor`; calls `IAccessMaintenance` |
| `CoreKit.SampleHost` | Access, Identity, Tenant | Composition root; wires all three modules |

**Confirmed:** Access is a leaf module at the project-reference level. However, at runtime, Access's `CompositeAccessMembership` calls `TenantAccessMembership` (in Tenant's assembly) via DI composition in SampleHost. This is a **runtime dependency from Access to Tenant** that is invisible at the project-reference level but violates the spirit of constraint 7 ("Access remains a leaf module").

### 2.2 Source Inventory — All Cross-Module Integration Points

#### Tenant → Access integrations (current state):

| # | Integration Point | Source Path | Lines | Tenant Implements Access-Owned? | Violates D-018? |
|---|---|---|---|---|---|
| T-1 | `TenantAccessScopeContext : IAccessScopeContext` | `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs` | 10 | Yes | Yes |
| T-2 | `TenantAccessMembership : IAccessMembershipAdapter` | `CoreKit.Tenant/Integration/TenantAccessMembership.cs` | 10 | Yes | Yes |
| T-3 | `TenantClaimsContributor : IAccessTokenClaimsContributor` | `CoreKit.Tenant/Integration/TenantClaimsContributor.cs` | 13 | Yes | Yes |
| T-4 | `TenantMemberRemovedToAccessMaintenance : IEventHandler<TenantMemberRemoved>` calls `IAccessMaintenance.RemoveUserAsync` | `CoreKit.Tenant/Integration/TenantMemberRemovedToAccessMaintenance.cs` | 19-34 | No (consumes unguarded maintenance) | No — but calls unguarded Access interface |
| T-5 | `TenantActor` constructor accepts `ICurrentUser?` from `CoreKit.Access.Abstractions` | `CoreKit.Tenant/Abstractions/TenantActor.cs` | 13-16 | No (consumes, doesn't implement) | Semantic coupling only |
| T-6 | DI: `services.AddScoped<IAccessScopeContext, TenantAccessScopeContext>()` | `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` | 109 | Yes | Yes |
| T-7 | DI: `services.AddScoped<IAccessMembershipAdapter, TenantAccessMembership>()` | `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` | 110 | Yes | Yes |

#### Identity → Access integrations (current state):

| # | Integration Point | Source Path | Lines | Identity Implements Access-Owned? | Violates D-018? |
|---|---|---|---|---|---|
| I-1 | `ICurrentUserService : ICurrentUser` | `CoreKit.Identity/Services/ICurrentUserService.cs` | 10 | Yes | Yes |
| I-2 | DI: `services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<ICurrentUserService>())` | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` | 106 | Yes | Yes |
| I-3 | `AuthService.GetCurrentUserAsync` calls `_access.GetAccessAsync(userId, PlatformScope)` | `CoreKit.Identity/Services/AuthService.cs` | 231 | No (consumes) | No |
| I-4 | `SessionIssuer.IssueCoreAsync` calls `_access.GetAccessAsync(user.Id, PlatformScope)` | `CoreKit.Identity/Services/SessionIssuer.cs` | 51 | No (consumes) | No |
| I-5 | `UserService` calls `_maintenance.AddPlatformMemberAsync`, `SetPlatformUserRolesAsync`, `RemovePlatformMemberAsync` | `CoreKit.Identity/Services/UserService.cs` | 160-163, 247, 286 | No (consumes unguarded `IAccessMaintenance`) | No — trusted system code |

#### Access consumption of Tenant (current state):

| # | Integration Point | Source Path | Lines | Access Calls Tenant? |
|---|---|---|---|---|
| A-1 | `AccessService.ResolveAsync` calls `_membership.FindAsync(scope, userId)` → `CompositeAccessMembership` → `TenantAccessMembership.FindAsync` → `ITenantMembershipReader.FindAsync` | `CoreKit.Access/Services/AccessService.cs` line 72; `CoreKit.Access/Abstractions/CompositeAccessMembership.cs` lines 19-29 | Yes — Access calls Tenant's adapter at runtime |
| A-2 | `AccessGuard.RequireAsync` reads `_scopeContext.Current` → `TenantAccessScopeContext.Current` | `CoreKit.Access/Services/AccessGuard.cs` line 26; `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs` lines 16-18 | Yes — Access reads Tenant's scope context at runtime |

#### Identity internal (no cross-module interface implementation):

| # | Integration Point | Source Path | Lines |
|---|---|---|---|
| I-6 | `ICurrentUserService` provides `Email`, `Roles`, `Permissions` from JWT claims | `CoreKit.Identity/Services/CurrentUserService.cs` lines 34-41 | Identity-owned, not Access-owned |
| I-7 | `TokenService.CreateAccessToken` issues JWT with `sub`, `jti`, `email`, `iss`, `aud`, `iat`, `nbf`, `exp` | `CoreKit.Identity/Services/TokenService.cs` lines 38-48 | Identity-owned |
| I-8 | `SessionIssuer` reads roles/permissions from `IAccessService.GetAccessAsync(PlatformScope)` for response DTO only (not embedded in JWT) | `CoreKit.Identity/Services/SessionIssuer.cs` lines 49-55, 88 | Access is read for response body only |

---

## 3. Previous OpenCode Findings Reconciled with Claude Review

### OpenCode's findings (from `DK-FULL-APPLICATION-ARCHITECTURE-AUDIT-01-REPORT.md`):

| Finding | OpenCode's Claim | Claude's Independent Result | Reconciliation |
|---|---|---|---|
| D-01 | Corrupted test file | **Confirmed** — file was truncated, now rewritten at `7115b14` but not build-verified | OpenCode correct; file exists but untested |
| D-02 | Uncommitted cache invalidation handler | **Confirmed** — handler committed at `1c832de`, DI registered, but cross-instance/runtime verification pending | OpenCode correct; implementation exists but untested |
| D-03 | Stale JWT after credential change | **Confirmed** — `SecurityVersion` exists on `User` but JWT never includes or validates it | OpenCode correct; verified in source |
| D-04 | Identity→Access dependency direction | **Confirmed as justified** — Identity depends on Access for authorization, not circular | OpenCode's "design weakness" overstated; this is correct per D-002 |
| D-05 | Empty SharedKernel | **Rejected per task constraint** — task says "Do not introduce SharedKernel" | OpenCode correct to not recommend it |
| D-06 | TenantActor transitive Access coupling | **Confirmed** — `TenantActor` accepts `ICurrentUser?` from Access.Abstractions but only uses `UserId` | OpenCode correct |
| D-18 | No rate limiting on tenant/access writes | **Confirmed** — only Identity auth endpoints are rate-limited | OpenCode correct |

### Claude's additional findings (from `DK-IAM-TENANT-FOUNDATION-REVIEW-01`):

| Finding | Claude's Claim | My Independent Verification |
|---|---|---|
| T-01 | Tenant implements `IAccessScopeContext`/`IAccessMembershipAdapter` | **Confirmed** — source lines 10 of both files |
| T-02 | `TenantActor` depends on `ICurrentUser` from Access.Abstractions | **Confirmed** — line 13, only uses `UserId` |
| T-03 | Identity implements `ICurrentUser` via `CurrentUserService` | **Confirmed** — `ServiceCollectionExtensions.cs:106-108` |
| T-04 | `CompositeAccessMembership` composes adapters via `IEnumerable<IAccessMembershipAdapter>` | **Confirmed** — lines 10-29 |
| T-05 | `TenantAccessMembership` always returns `IsOwner=false` | **Confirmed** — line 25 |
| T-06 | `ICurrentTenant` and `ITenantMembershipReader` are Tenant-owned | **Confirmed** — Tenant abstractions |
| D-20 | Platform membership error-code propagation | **Not confirmed** — current code correctly returns `access.not_authenticated` for unauthenticated callers |
| D-19 | `IsAuthenticated` check in `AccessGuard` | **False positive** — code correctly checks `!_actor.IsAuthenticated` at line 29 |

### Claude's additional areas (not covered by OpenCode):

| Area | Claude's Finding | My Independent Verification |
|---|---|---|
| Identity registration/login | Implemented with lockout, no account enumeration | **Confirmed** — `AuthService.cs` lines 55-142 |
| Refresh token rotation/reuse | Implemented with family revocation | **Confirmed** — `AuthService.cs` lines 144-206, `RefreshToken.cs:22-25` |
| Rate limiting on tenant/access writes | **Missing** — only Identity auth endpoints rate-limited | **Confirmed** — `ServiceCollectionExtensions.cs:145-166`, endpoints have `RequireAuthorization()` but no rate limiter |
| Access cache cross-instance | In-memory only, no cross-instance invalidation | **Confirmed** — `AccessCache.cs` uses `IMemoryCache` |
| Tenant membership cache | In-memory with TTL, invalidated on change | **Confirmed** — `TenantMembershipReader.cs` |
| Cross-DbContext atomicity | Tenant save + Access maintenance not atomic | **Confirmed** — separate DbContexts, separate `SaveAsync` calls |

---

## 4. Current Integration Defects

### Defect ID: INT-01 — Tenant implements Access-owned interfaces (architectural violation)

| Field | Value |
|---|---|
| ID | INT-01 |
| Category | Architecture / module boundary |
| Severity | High |
| Confidence | Confirmed |
| Evidence | `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs:10` (`: IAccessScopeContext`); `CoreKit.Tenant/Integration/TenantAccessMembership.cs:10` (`: IAccessMembershipAdapter`); `CoreKit.Tenant/Integration/TenantClaimsContributor.cs:13` (`: IAccessTokenClaimsContributor`); `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs:109-110` (DI registration overriding Access defaults) |
| Actual behaviour | Tenant implements Access-owned contracts `IAccessScopeContext`, `IAccessMembershipAdapter`, and `IAccessTokenClaimsContributor`. Access's `CompositeAccessMembership` calls these adapters at runtime. |
| Expected behaviour | Per D-018 and constraint 11, no module should implement interfaces owned by another module. Access should own its membership and scope contracts. |
| Root cause | D-016 directed Tenant to "integrate through Access public contracts" which was interpreted as Tenant implementing Access's interfaces. D-018 later prohibited this pattern but the implementation was not removed. |
| Impact | Violates D-018's prohibition on cross-module adopter patterns. Creates runtime dependency from Access to Tenant despite Access being a leaf module at project-reference level. |
| Scenario | Any tenant-scoped authorization call (`AccessGuard.RequireAsync`) flows through `AccessService.ResolveAsync` → `CompositeAccessMembership.FindAsync` → `TenantAccessMembership.FindAsync` → `ITenantMembershipReader.FindAsync` (Tenant's database). Access depends on Tenant at runtime. |
| Recommendation | Redesign so Access owns its `IAccessMembership` implementation and reads from its own data or from Tenant-pushed events. |
| Status | Under review — redesign and Owner approval outstanding |

### Defect ID: INT-02 — Cross-DbContext non-atomicity

| Field | Value |
|---|---|
| ID | INT-02 |
| Category | Data integrity / transaction |
| Severity | High |
| Confidence | Confirmed |
| Evidence | `CoreKit.Tenant/Services/TenantMembershipService.RemoveMemberAsync` lines 200-204: `SaveAsync` commits Tenant change, then publishes `TenantMemberRemovedEvent`, then `TenantMemberRemovedToAccessMaintenance.HandleAsync` calls `IAccessMaintenance.RemoveUserAsync` which calls `AccessUnitOfWork.SaveAsync`. Two separate DbContexts, two separate transactions. |
| Actual behaviour | If `AccessUnitOfWork.SaveAsync` fails after Tenant's `SaveAsync` succeeds, the member is removed from Tenant but Access role assignments are not deleted. The member retains Access permissions until cache TTL expires. |
| Expected behaviour | Member removal and Access assignment deletion should be atomic — either both succeed or neither. |
| Root cause | Two separate DbContexts (`TenantDbContext` and `AccessDbContext`) with no distributed transaction or outbox pattern. |
| Impact | A removed tenant member can retain Access permissions indefinitely (until cache TTL). Cross-tenant authorization bypass possible. |
| Scenario | Admin removes user from tenant. Tenant's `SaveAsync` commits. Access's `SaveAsync` fails (DB connection issue). User's Access role assignments remain. User can still perform tenant-scoped operations. |
| Recommendation | Implement transactional outbox or durable event delivery with reconciliation. |
| Status | New finding — requires Owner decision on approach |

### Defect ID: INT-03 — Stale JWT after credential change (D-03)

| Field | Value |
|---|---|
| ID | INT-03 |
| Category | Security |
| Severity | Medium |
| Confidence | Confirmed |
| Evidence | `CoreKit.Identity/Entities/User.cs:45-46` documents: "Access-token (JWT) validation of this version is a separate, not yet implemented task: existing access tokens stay valid until expiry." `CoreKit.Identity/Services/TokenService.cs:38-48` — JWT contains no `SecurityVersion` claim. `CoreKit.Identity/Services/SessionIssuer.cs:51` — `GetAccessAsync` called for response body only, not embedded in token. |
| Actual behaviour | `SecurityVersion` is incremented on password change/deactivation but never embedded in or validated against JWTs. |
| Expected behaviour | Either JWTs should include `SecurityVersion` and be validated on each request, or the accepted staleness window should be documented as a deliberate trade-off. |
| Root cause | Deliberate but incomplete design — `SecurityVersion` protects refresh-token issuance (guarded insert) but not access-token validation. |
| Impact | Stolen access token remains valid until expiry (up to 120 minutes via `JwtOptions.AccessTokenMinutes`) after password change or account deactivation. |
| Scenario | Attacker steals JWT. Victim changes password. Attacker's JWT remains valid until expiry. |
| Recommendation | Project Owner must decide: implement JWT `SecurityVersion` validation, or document and accept the staleness window. |
| Status | Open Owner decision (D-03) |

---

## 5. Alternative Architecture Analysis

### Option A — Current Access-owned interface adapters (baseline)

**Description:** Tenant implements `IAccessScopeContext`, `IAccessMembershipAdapter`, `IAccessTokenClaimsContributor` (Access-owned interfaces). Identity implements `ICurrentUser` (Access-owned). Access's `CompositeAccessMembership` composites adapters at runtime.

| Evaluation Criterion | Assessment |
|---|---|
| Module ownership | Access defines contracts, Tenant/Identity implement them — inverted ownership |
| No cross-module implementation pattern | ❌ Violated — Tenant and Identity implement Access-owned interfaces |
| No SharedKernel | ✅ Satisfied |
| Standalone Access | ❌ Violated — Access's `CompositeAccessMembership` depends on Tenant's adapter at runtime |
| Authorization correctness | ✅ Works correctly at runtime |
| Membership freshness | ✅ Real-time (calls Tenant's reader synchronously) |
| Revocation | ⚠️ In-process event handler; cross-instance staleness until TTL |
| Multi-instance | ❌ In-memory cache per instance; no cross-instance invalidation |
| Failure recovery | ⚠️ Event handler failure doesn't undo Tenant change |
| Performance | ⚠️ Synchronous cross-module call per authorization check |
| Complexity | Medium — adapter pattern adds indirection |
| Testability | ⚠️ Requires DI composition of both modules |

**Verdict:** Violates D-018 constraint 11. Cannot be the recommended architecture.

---

### Option B — Explicit membership evidence (DTO-based)

**Description:** Tenant validates membership and creates a signed evidence DTO containing `(userId, tenantId, generation, timestamp, signature)`. Access receives this evidence with each authorization request and validates the signature before using it.

**How Access obtains evidence:** The host or middleware would call Tenant's membership service, get evidence, and pass it to Access. But Access's `IPermissionChecker.HasPermissionAsync(string permission)` only accepts a permission string — no membership evidence parameter. Access's `IAccessScopeContext.Current` provides the scope, and `IAccessCurrentUser` provides the user. There's no parameter for membership evidence.

**Access authenticating evidence origin:** Access would need to verify the signature. But Access doesn't have Tenant's signing key. Tenant would need to share it, creating a shared secret — which is effectively a SharedKernel-like concern.

**Freshness:** Evidence would need a timestamp and short expiry. Access would need to check it on every call.

**Replay prevention:** Evidence would need a nonce or jti, checked against a store — Access doesn't have such a store for membership evidence.

**Membership generation:** Evidence would include generation. Access would check it against stored assignment generation.

**Multi-instance:** Each instance would need to verify signatures independently — requires shared signing key.

**Verdict:** Cannot satisfy constraints. Access's `IPermissionChecker` interface doesn't accept membership evidence, and there's no secure way for Access to validate evidence from Tenant without shared secrets or Access depending on Tenant.

---

### Option C — Durable membership projection (event-driven)

**Description:** Tenant publishes `TenantMemberAdded` and `TenantMemberRemoved` integration events. Access maintains its own authorization-relevant membership projection cache. Access's `IAccessMembership` implementation reads from its own local cache (populated by events), not from Tenant's database.

**Dependencies:**
- Tenant → Access (project reference, existing) — Tenant publishes events
- Access subscribes to `TenantMemberAdded` and `TenantMemberRemoved` via `IEventHandler<>`
- Access maintains its own `IMemoryCache` of `(tenantId, userId, membershipGeneration, isActive)`
- Access's `IAccessMembership.FindAsync` reads from its own local cache

**Event durability:** Currently in-process only. Would need transactional outbox for reliability.

**Delivery retries:** Not currently implemented. Would need outbox with retry.

**Idempotency:** Events contain `tenantId`, `userId`, `generation`. Access's cache update would be idempotent (set generation = max(existing, new)).

**Event ordering:** Events are published sequentially per tenant within a single DbContext transaction. Order preserved within instance.

**Duplicate events:** Idempotent cache update handles duplicates.

**Missed events:** Without durable outbox, missed events = stale cache until TTL.

**Reconciliation:** Would need periodic reconciliation or consistency check.

**Multi-instance:** Each instance has its own cache. Events are in-process only. Other instances' caches stale until TTL or until they process the event.

**Stale-positive authorization:** Possible until event processed + TTL expires.

**Cache invalidation:** Access invalidates its own cache entry on event receipt.

**Operational complexity:** High — requires outbox, retry, reconciliation, monitoring.

**Verdict:** Technically feasible but introduces significant operational complexity. Eventual consistency means stale authorization windows are unavoidable without distributed cache invalidation. **Selected as the basis for the recommended design, but with mandatory durable outbox and reconciliation additions.**

---

### Option D — Host-orchestrated authorization

**Description:** The host (SampleHost or future host) coordinates Tenant membership validation and Access authorization. Host calls Tenant's `ITenantMembershipReader.FindAsync`, gets membership info, then calls Access's `IPermissionChecker`.

**How host obtains authenticated actor:** From JWT claims (`sub` claim) via `ICurrentUserService` (Identity).

**How host establishes tenant context:** From JWT `tenant_id` claim via `TenantResolutionMiddleware` (already exists).

**How host invokes Tenant's membership service:** Host calls `ITenantMembershipReader.FindAsync(tenantId, userId)` → gets `(isMember, generation)`.

**How host invokes Access's authorization service:** Host calls `IPermissionChecker.HasPermissionAsync(permission)`. But Access's interface only accepts a permission string — no membership evidence parameter. Access uses `IAccessScopeContext.Current` (set by middleware) and `IAccessCurrentUser` (from JWT).

**Problem:** Host gets membership info from Tenant, but has no way to pass it to Access. Access's `IPermissionChecker` doesn't accept tenant membership context. Access gets scope from `IAccessScopeContext.Current` (which would be set by `TenantAccessScopeContext` — but that's Tenant implementing Access's interface, which we're trying to eliminate).

**If host sets `IAccessScopeContext.Current`:** The host would implement `IAccessScopeContext` (Access-owned interface). Constraint 11 prohibits modules implementing Access-owned interfaces, but the host isn't Tenant or Identity — it's the composition root. However, this still creates an "adopter pattern" where a non-Access module implements Access's interface.

**Preventing bypass:** Application services call `IPermissionChecker` directly, bypassing the host. Host orchestration only works for HTTP endpoints that go through the host. Background jobs, direct service calls would bypass.

**Standalone Access:** Without Tenant, host can't provide membership evidence. Access's `IAccessMembership` returns null for all lookups → no one is a member → fail-closed. This works for standalone.

**Verdict:** Relocates the adapter pattern into the host but doesn't solve the core problem. Access's `IPermissionChecker` interface doesn't accept membership evidence. Host orchestration adds latency (extra hop to Tenant) and doesn't cover background jobs or direct service calls.

---

### Option E — Access-owned membership projection with event-driven synchronization (RECOMMENDED)

**Description:** Access owns and implements `IAccessMembership`. Access maintains its own local membership cache populated by Tenant-published integration events. Access's `IAccessMembership.FindAsync` reads from its own local cache — no call to Tenant's database.

**How it works:**

1. **Tenant publishes events** (`TenantMemberAdded`, `TenantMemberRemoved`) — integration events carry ids only
2. **Access subscribes** to `TenantMemberAdded` and `TenantMemberRemoved` via `IEventHandler<>`
3. **Access's `IAccessMembership` implementation reads from its own local cache** — no call to Tenant's database
4. **Access's `AccessService.ResolveAsync`** checks assignments against the generation from Access's own cache — no call to Tenant's database

**Dependencies:**
- Tenant → Access (project reference, existing) — Tenant publishes events
- Access → None (leaf module preserved at runtime too)
- Access's `IAccessMembership` implementation is in Access, reading Access's own cache

**Request-time data flow (tenant HTTP request):**
1. JWT validation (Identity)
2. `TenantResolutionMiddleware` resolves tenant from `X-Tenant-Id` header, validates user membership via `ITenantMembershipReader` (Tenant's own database)
3. `TenantResolutionMiddleware` sets `ICurrentTenant` (Tenant-owned) AND `HttpContext.Items["tenant_id"]`
4. `AccessGuard.RequireAsync` reads `IAccessScopeContext.Current` (Access's `ClaimsAccessScopeContext` reads `tenant_id` from `HttpContext.Items["tenant_id"]`)
5. `AccessService.GetAccessAsync` calls `IAccessMembership.FindAsync` (Access's own cache)
6. Authorization decision

**Dependencies:**
- Tenant → Access (project reference, existing) — Tenant publishes events
- Access → None (leaf module preserved at runtime too)
- Access's `IAccessMembership` implementation is in Access, reading Access's own cache

**Verdict:** **RECOMMENDED.** Access owns and implements `IAccessMembership`. Access maintains its own local membership cache populated by Tenant-published integration events. Tenant publishes `TenantMemberAdded` and `TenantMemberRemoved` events. Access's `IAccessMembership.FindAsync` reads from Access's own local cache — no call to Tenant's database.

---

## 6. Recommended Architecture

**Core principle:** Access owns and implements all Access-owned contracts. Tenant owns and implements all Tenant-owned contracts. No module implements another module's interfaces.

| Contract | Owned By | Implemented By | Reads From |
|---|---|---|---|
| `IAccessScopeContext` | Access | Access (`ClaimsAccessScopeContext`) | JWT `tenant_id` claim (via `HttpContext.Items["tenant_id"]` set by Tenant middleware) |
| `IAccessMembership` | Access | Access (`AccessMembershipCache`) | Access's own local membership cache (populated by events) |
| `ICurrentUser` | Access | Access (`ClaimsAccessCurrentUser`) | JWT `sub` claim |
| `ICurrentTenant` | Tenant | Tenant (`CurrentTenant`) | `TenantResolutionMiddleware` (reads JWT `tenant_id` claim) |
| `ITenantMembershipReader` | Tenant | Tenant (`TenantMembershipReader`) | Tenant's own `TenantMember` table |
| `IPermissionChecker` | Access | Access (`AccessPermissionChecker`) | Access's own `AccessService` |
| `IAccessTokenClaimsContributor` | Access | Not implemented (no tenant_id claim issued) | N/A |
| `IAccessIntegrationEvent` | Access | Tenant publishes (`TenantMemberAdded`, `TenantMemberRemoved`); Access consumes | N/A |

**Data flow:**

```
Tenant publishes events ──► Access local membership cache
                               │
                               ▼
HTTP Request ──► JWT Validation (Identity) ──► TenantResolutionMiddleware
                                               │ reads tenant_id claim
                                               ▼
                                            Sets ICurrentTenant + HttpContext.Items["tenant_id"]
                                               │
                                               ▼
                                           AccessGuard.RequireAsync
                                               │ reads IAccessScopeContext.Current
                                               ▼
                                           AccessService.GetAccessAsync
                                               │ calls IAccessMembership.FindAsync
                                               ▼
                                           Access's own cache
                                               │ (tenantId, userId, generation)
                                               ▼
                                           AccessAssignment table
                                               │ (userId, roleId, generation)
                                               ▼
                                           Authorization decision
```

---

## 7. Module Dependency Diagram

### Project Reference Graph (compile-time)

```
CoreKit.Access  ← CoreKit.Identity  ← CoreKit.SampleHost
      ↑              ↑
      └──── CoreKit.Tenant ────┘
```

**Access is a leaf module** — no project references. Identity and Tenant depend on Access. SampleHost depends on all three.

### Runtime Call Graph (desired, after Option E)

```
HTTP Request
    │
    ▼
Identity (JWT validation) ──────────────────────────────┐
    │                                                   │
    ▼                                                   │
TenantResolutionMiddleware                              │
    │ (reads tenant_id claim, validates membership)    │
    ▼                                                   │
Sets ICurrentTenant (Tenant-owned)                      │
    │                                                   │
    ▼                                                   │
AccessGuard.RequireAsync                                │
    │ reads IAccessScopeContext.Current                 │
    ▼                                                   │
AccessService.GetAccessAsync                            │
    │ calls IAccessMembership.FindAsync                 │
    ▼                                                   │
Access's own membership cache ◄─────── Tenant events   │
    │ (populated by events)                             │
    ▼                                                   │
AccessAssignment table                                  │
    │                                                   │
    ▼                                                   │
Authorization decision                                  │
```

**Key separation:**
- Project references: Tenant → Access (Access is leaf)
- Runtime event flow: Tenant → Access (events)
- Runtime call flow: Access → Access's own cache (no Tenant dependency at runtime)

---

## 8. Sequence Diagrams

### 8.1 Tenant HTTP Authorization

```
Client                          Identity                  Tenant                  Access
  │                               │                         │                     │
  │──── POST /tenants/{id}/action ─────────────────────────►│                     │
  │                               │                         │                     │
  │                               │ Validate JWT (sub claim)│                     │
  │                               │                         │                     │
  │                               │◄──── Valid JWT ─────────┤                     │
  │                               │                         │                     │
  │                               │                         │ Resolve tenant      │
  │                               │                         │ from X-Tenant-Id header│
  │                               │                         │                     │
  │                               │                         │ Validate tenant     │
  │                               │                         │ exists & active     │
  │                               │                         │                     │
  │                               │                         │ Validate user       │
  │                               │                         │ is member (ITenantMembershipReader)│
  │                               │                         │                     │
  │                               │                         │ Set ICurrentTenant  │
  │                               │                         │ + Items["tenant_id"]│
  │                               │                         │                     │
  │                               │                         │                     │
  │                               ▼                         ▼                     ▼
  │                               │                         │ AccessGuard.RequireAsync
  │                               │                         │ reads IAccessScopeContext.Current
  │                               │                         │ (Access's implementation reads
  │                               │                         │  tenant_id from Items["tenant_id"])
  │                               │                         │                     │
  │                               │                         │ AccessService.GetAccessAsync
  │                               │                         │ calls IAccessMembership.FindAsync
  │                               │                         │ reads Access's own cache
  │                               │                         │                     │
  │                               │                         │ AccessAssignment table
  │                               │                         │                     │
  │                               │                         │ Authorization decision
  │                               │                         │                     │
  │◄──── 200 OK ──────────────────┘                         │                     │
  │                               │                         │                     │
```

### 8.2 Platform Authorization

```
Client                          Identity                  Access
  │                               │                         │
  │──── POST /access/platform-members ─────────────────────►│
  │                               │                         │
  │                               │ Validate JWT            │
  │                               │                         │
  │                               │◄──── Valid JWT ─────────┤
  │                               │                         │
  │                               │                         │ AccessGuard.RequirePlatformScopePermissionAsync
  │                               │                         │ reads IAccessScopeContext.Current
  │                               │                         │ (fixed PlatformScope)
  │                               │                         │                     │
  │                               │                         │ AccessService.GetFreshAccessAsync
  │                               │                         │ reads IAccessMembership.FindAsync
  │                               │                         │ reads Access's own cache
  │                               │                         │                     │
  │                               │                         │ AccessAssignment table
  │                               │                         │                     │
  │                               │                         │ Authorization decision
  │                               │                         │                     │
  │◄──── 200 OK ────────────────────────────────────────────┤
  │                               │                         │
```

### 8.3 Membership Removal

```
Tenant Service                  Tenant DbContext          Access DbContext
  │                               │                         │
  │──── RemoveMemberAsync ───────►│                         │
  │                               │                         │
  │                               │ Begin transaction       │
  │                               │                         │
  │                               │ Set RemovedAt,          │
  │                               │   MembershipGeneration++│
  │                               │                         │
  │                               │ SaveAsync (commit)      │
  │                               │                         │
  │                               │ Publish TenantMemberRemoved│
  │                               │  event to outbox        │
  │                               │                         │
  │                               │                         │ Outbox publisher reads event│
  │                               │                         │ Access handler updates cache│
  │                               │                         │ + invalidates Access cache│
  │                               │                         │ Access SaveAsync (commit) │
  │                               │                         │
  │◄──── Success ─────────────────┘                         │
  │                               │                         │
```

**Failure scenario:** If Access's `SaveAsync` fails after Tenant's `SaveAsync` commits, the member is removed from Tenant but Access assignments remain. The event is in the outbox and will be retried. Until the event is processed, the member retains Access permissions until cache TTL expires.

### 8.4 Background-Job Authorization

```
Background Job                  Tenant                  Access
  │                               │                         │
  │──── Set ICurrentTenant ───────►│                         │
  │                               │                         │
  │                               │ Set Items["tenant_id"]  │
  │                               │                         │
  │──── AccessGuard.RequireAsync ─┼────────────────────────►│
  │                               │                         │
  │                               │                         │ AccessService.GetAccessAsync
  │                               │                         │ reads IAccessMembership.FindAsync
  │                               │                         │ reads Access's own cache
  │                               │                         │                     │
  │                               │                         │ AccessAssignment table
  │                               │                         │                     │
  │                               │                         │ Authorization decision
  │                               │                         │                     │
  │◄──── Decision ────────────────┴─────────────────────────┤
  │                               │                         │
```

**Note:** Background jobs don't go through `TenantResolutionMiddleware`. The job must explicitly set `ICurrentTenant` and `HttpContext.Items["tenant_id"]` before calling Access services. If the job doesn't set tenant context, `IAccessScopeContext.Current` returns null and `AccessGuard.RequireAsync` returns `ScopeRequired` (fail-closed).

### 8.5 Cache Hit

```
AccessService.GetAccessAsync
  │
  ▼
Check AccessCache.TryGet(scope, userId)
  │
  ├─ Cache hit (token matches) ───► Return cached EffectiveAccess
  │
  └─ Cache miss ───────────────────► ResolveAsync
                                        │
                                        ▼
                                    IAccessMembership.FindAsync
                                        │
                                        ▼
                                    Read from Access's own cache
                                        │
                                        ▼
                                    Read AccessAssignment table
                                        │
                                        ▼
                                    Build EffectiveAccess
                                        │
                                        ▼
                                    AccessCache.Set(scope, userId, access, token)
                                        │
                                        ▼
                                    Return EffectiveAccess
```

### 8.6 Cache Miss (Membership Revoked)

```
AccessService.GetAccessAsync
  │
  ▼
Check AccessCache.TryGet(scope, userId) → miss
  │
  ▼
ResolveAsync
  │
  ▼
IAccessMembership.FindAsync(scope, userId)
  │
  ▼
Read Access's own membership cache
  │
  ▼
Cache miss (user was removed, event processed)
  │
  ▼
Return null (not a member)
  │
  ▼
EffectiveAccess.None (IsMember = false)
  │
  ▼
AccessGuard.RequireAsync returns Denied
```

---

## 9. Exact Public Contract Proposals

### 9.1 Contract: IAccessScopeContext (Access-owned)

| Field | Value |
|---|---|
| Owning module | Access |
| Implemented by | Access (`ClaimsAccessScopeContext`) |
| Input | `HttpContext.Items["tenant_id"]` (set by TenantResolutionMiddleware) |
| Output | `AccessScope? Current` |
| Trust boundary | Claim is set by TenantResolutionMiddleware after validating against Tenant's membership database. Client-supplied header is not trusted directly. |

```csharp
public interface IAccessScopeContext
{
    AccessScope? Current { get; }
}
```

**Implementation:** `ClaimsAccessScopeContext` reads `tenant_id` from `HttpContext.Items["tenant_id"]` (set by `TenantResolutionMiddleware` after validating against Tenant's membership database). Returns `AccessScope.ForTenant(tenantId)` or null.

### 9.2 Contract: IAccessMembership (Access-owned)

| Field | Value |
|---|---|
| Owning module | Access |
| Implemented by | Access (`AccessMembershipCache`) |
| Input | `(AccessScope scope, Guid userId)` |
| Output | `AccessMember?` (IsOwner, MembershipGeneration) |
| Data source | Access's own local membership cache |
| Cache population | Tenant-published `TenantMemberAdded` / `TenantMemberRemoved` events via durable outbox |

```csharp
public interface IAccessMembership
{
    Task<AccessMember?> FindAsync(AccessScope scope, Guid userId, CancellationToken ct = default);
}
```

### 9.3 Contract: ICurrentUser (Access-owned)

| Field | Value |
|---|---|
| Owning module | Access |
| Implemented by | Access (`ClaimsAccessCurrentUser`) |
| Input | `HttpContext.User` (JWT claims) |
| Output | `Guid? UserId`, `bool IsAuthenticated` |
| Data source | JWT `sub` claim |

```csharp
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
```

**Note:** `ICurrentUser.Permissions` is removed — Access doesn't trust client-supplied permission claims. Permissions come from Access's own `AccessService`.

### 9.4 Contract: ICurrentTenant (Tenant-owned)

| Field | Value |
|---|---|
| Owning module | Tenant |
| Implemented by | Tenant (`CurrentTenant`) |
| Input | Set by `TenantResolutionMiddleware` |
| Output | `Guid? Id`, `bool IsResolved` |
| Trust validation | Middleware validates tenant exists, is active, user is member |

```csharp
public interface ICurrentTenant
{
    bool IsResolved { get; }
    Guid? Id { get; }
    Guid RequiredId { get; }
    IDisposable Change(Guid? tenantId);
}
```

### 9.5 Contract: ITenantMembershipReader (Tenant-owned)

| Field | Value |
|---|---|
| Owning module | Tenant |
| Implemented by | Tenant (`TenantMembershipReader`) |
| Input | `(Guid tenantId, Guid userId)` |
| Output | `TenantMemberInfo?` (IsOwner, MembershipGeneration) |
| Data source | Tenant's own `TenantMember` table |

```csharp
public interface ITenantMembershipReader
{
    Task<TenantMemberInfo?> FindAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
```

### 9.6 Contract: IPermissionChecker (Access-owned)

| Field | Value |
|---|---|
| Owning module | Access |
| Implemented by | Access (`AccessPermissionChecker`) |
| Input | `(string permission)` |
| Output | `bool` |
| Data source | `IAccessService.GetAccessAsync` → `IAccessMembership.FindAsync` → Access's own cache |

```csharp
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default);
    Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default);
}
```

### 9.7 Contract: IAccessIntegrationEvent (Access-owned)

| Field | Value |
|---|---|
| Owning module | Access |
| Implemented by | Tenant (publishes), Access (consumes) |
| Input | `(Guid TenantId, Guid UserId, long MembershipGeneration, DateTime OccurredAt, Guid EventId, int Sequence)` |
| Purpose | Notify Access of membership changes |

```csharp
public interface IAccessIntegrationEvent
{
    DateTime OccurredAt { get; }
    Guid EventId { get; }
    int Sequence { get; }
}

public sealed record TenantMemberAdded(
    Guid TenantId, Guid UserId, bool IsOwner, long MembershipGeneration,
    Guid EventId, int Sequence, DateTime OccurredAt) : IAccessIntegrationEvent;

public sealed record TenantMemberRemoved(
    Guid TenantId, Guid UserId, long MembershipGeneration,
    Guid EventId, int Sequence, DateTime OccurredAt) : IAccessIntegrationEvent;
```

**Note:** Events are defined in Access (Access-owned contracts). Tenant publishes them. Access consumes them. This is the correct direction — Access defines the contract, Tenant implements the publisher.

---

## 10. Cache and Consistency Design

### 10.1 Access Membership Cache

| Component | Design |
|---|---|
| Cache store | `IMemoryCache` (per instance) + persistent table `AccessMembership` |
| Cache key | `corekit.access:membership:{tenantId}:{userId}` |
| Cache value | `AccessMember` (IsOwner, MembershipGeneration, IsActive) |
| Cache TTL | Configurable (`Access:MembershipCacheSeconds`), default 300 seconds |
| Invalidation | Event-driven: `TenantMemberAdded` / `TenantMemberRemoved` events update cache immediately |
| Bootstrap | On startup, Access projects all existing members from Tenant via initial event replay or direct migration |
| Multi-instance | Each instance has its own cache. Events are in-process per instance. Other instances' caches stale until TTL or until they process the event. |

### 10.2 Access Authorization Cache

| Component | Design |
|---|---|
| Cache store | `IMemoryCache` (per instance, existing `AccessCache`) |
| Cache key | `corekit.access:{scope}:e{epoch}:v{scopeVersion}:u{userVersion}:{userId}` |
| Cache value | `EffectiveAccess` (IsMember, IsOwner, Permissions, Roles) |
| Cache TTL | Configurable (`Access:CacheSeconds`) |
| Invalidation | Generation-based: `InvalidateScope` / `InvalidateUser` advance generation counters |
| Membership generation change | Tenant's `RemoveMemberAsync` increments `MembershipGeneration`. Event published to Access. Access's cache invalidation advances user's generation in Access's cache. |

### 10.3 Membership Generation Flow

```
Tenant.AddMemberAsync:
  1. TenantMember.MembershipGeneration = 1 (new) or ++ (reactivation)
  2. TenantDbContext.SaveAsync() commits
  3. Publish TenantMemberAdded(tenantId, userId, generation, isOwner, eventId, sequence)
  4. Access event handler updates Access's membership cache: cache[tenantId][userId] = {generation, isActive: true}

Tenant.RemoveMemberAsync:
  1. TenantMember.RemovedAt = now, MembershipGeneration++
  2. TenantDbContext.SaveAsync() commits
  3. Publish TenantMemberRemoved(tenantId, userId, generation, eventId, sequence)
  4. Access event handler updates Access's membership cache: cache[tenantId][userId] = {generation, isActive: false}
  5. Access's AccessAssignmentRepository.RemoveUserAsync deletes assignments
  6. AccessCache.InvalidateUser(scope, userId) invalidates cached permissions
```

**Consistency guarantee:** After `TenantMemberRemoved` event is processed by Access, the user's Access assignments are deleted and Access cache is invalidated. Any subsequent authorization request returns `IsMember = false`.

**Stale window:** Between Tenant's `SaveAsync` and Access's event processing, the user retains Access permissions. Window size depends on event processing latency (typically milliseconds in-process).

---

## 11. Security/Concurrency/Failure Analysis

### 11.1 Concurrent Membership Removal and Protected Request

**Scenario:** Request A is mid-authorization (AccessService.ResolveAsync reading assignments) while Request B removes the member.

**Sequence:**
1. Request A: `IAccessMembership.FindAsync` returns member with generation 1
2. Request B: `TenantMemberRemoved` → generation becomes 2 → Access's cache updated → assignments deleted
3. Request A: `ListRolesForUserAsync(scope, userId, generation=1)` returns roles at generation 1

**Result:** Request A may authorize based on generation 1 assignments even though membership was revoked. This is a **bounded stale window** — the request was already in flight when removal committed.

**Mitigation:** `AccessService.ResolveAsync` reads membership first, then assignments with that generation. If membership was revoked between the membership read and the assignment read, the assignment read uses the old generation and may return roles.

**Fix:** `AccessService.ResolveAsync` should re-read membership after reading assignments and compare generations. If generation changed, retry. Or: `ListRolesForUserAsync` should filter by `MembershipGeneration == current generation` from Access's own cache. If the event was processed, the generation in Access's cache is incremented, and old assignments are filtered out. If the event wasn't processed yet, the old generation is still in cache and assignments are returned. This is the same bounded window.

### 11.2 Stale Cache

**Scenario:** Access cache has `IsMember = true, generation = 1`. Tenant removes member (generation becomes 2). Event not yet processed.

**Result:** Access cache returns `IsMember = true` until TTL expires or event is processed.

**Mitigation:** TTL is the safety net. Short TTL (e.g., 60 seconds) limits stale window.

### 11.3 Event Failure

**Scenario:** `TenantMemberRemoved` event published but Access's event handler fails.

**Result:** Access's cache still has `IsMember = true`. Access assignments not deleted. Member retains permissions until TTL.

**Mitigation:** Transactional outbox with retry. Dead letter queue for persistent failures. Reconciliation job that compares Tenant's members with Access's cache periodically.

### 11.4 Duplicate Event

**Scenario:** `TenantMemberAdded` published twice.

**Result:** Access's cache update is idempotent (set generation = max(existing, new)). No harm.

### 11.5 Delayed Event

**Scenario:** `TenantMemberRemoved` event delayed by 30 seconds.

**Result:** Member retains Access permissions for 30 seconds after removal.

**Mitigation:** Short TTL (e.g., 60 seconds) as additional safety net.

### 11.6 Access Unavailable

**Scenario:** Access's database is down.

**Result:** `AccessService.ResolveAsync` throws `DbException`. `AccessGuard.RequireAsync` propagates the error. Request fails with 500.

**Mitigation:** Circuit breaker, retry with backoff, fail-closed (don't authorize on error).

### 11.7 Tenant Unavailable

**Scenario:** Tenant's database is down during `TenantResolutionMiddleware`.

**Result:** Middleware returns `UnknownTenant` or `ForStatus`. Request fails before reaching Access.

**Mitigation:** Fail-closed. No tenant context = no authorization.

### 11.8 Invalid Tenant Context

**Scenario:** JWT has `tenant_id` claim but tenant doesn't exist or is suspended.

**Result:** `TenantResolutionMiddleware` returns `UnknownTenant` or `ForStatus`. Request fails before reaching Access.

**Mitigation:** Fail-closed. Invalid tenant = no authorization.

### 11.9 Missing Actor

**Scenario:** Request has no authenticated user (no JWT or invalid JWT).

**Result:** `AccessGuard.RequireAsync` returns `NotAuthenticated`. `TenantResolutionMiddleware` doesn't set `ICurrentTenant` (only sets for authenticated users). `IAccessScopeContext.Current` returns null. `AccessGuard.RequireAsync` returns `ScopeRequired`.

**Mitigation:** Fail-closed. No authentication = no authorization.

### 11.10 Platform/Tenant Scope Mismatch

**Scenario:** Tenant-scoped caller tries platform operation (`platform.members.manage`).

**Result:** `AccessGuard.RequirePlatformScopePermissionAsync` uses fixed `AccessScope.ForPlatform(PlatformId)`, not ambient tenant scope. `IAccessMembership.FindAsync(PlatformScope, userId)` returns null (user is not a platform member). `EffectiveAccess.None`. `RequirePlatformScopePermissionAsync` returns `Denied`.

**Mitigation:** Platform operations require platform membership, not tenant membership. Tenant owners are not automatically platform members.

---

## 12. Performance and Operations

### 12.1 Database Round Trips per Protected Request

| Operation | Current (Adapter Pattern) | Option E (Access-Owned Cache) |
|---|---|---|
| Resolve tenant | 1 (TenantDbContext) | 1 (TenantDbContext, in middleware) |
| Check membership | 1 (TenantDbContext via `ITenantMembershipReader`) | 0 (Access's own cache) |
| Read assignments | 1 (AccessDbContext) | 1 (AccessDbContext) |
| **Total** | **2-3** | **2** |

**Cache hit:** Access's `EffectiveAccess` cache hit → 0 additional DB reads (after initial resolution).

### 12.2 Cache Lookup Count

| Scenario | Lookups |
|---|---|
| Cache hit (permissions) | 1 (AccessCache) |
| Cache miss + member | 2 (AccessCache + AccessAssignment) |
| Cache miss + non-member | 1 (AccessCache + membership cache, returns null) |

### 12.3 Cache Hit/Miss Behavior

| Scenario | Behaviour |
|---|---|
| Cache hit | Return immediately, no DB access for permissions |
| Cache miss + member is active | Read assignments, cache result, return |
| Cache miss + user not member | Return `EffectiveAccess.None`, don't cache (member may be added later) |
| Cache miss + membership revoked | Return `EffectiveAccess.None` (from Access's own cache showing inactive) |

### 12.4 DB Connection Holding Time

| Operation | Time |
|---|---|
| TenantResolutionMiddleware | ~5-10ms (tenant lookup + membership check) |
| AccessService.ResolveAsync | ~2-5ms (assignment query) |
| AccessCache hit | <1ms (in-memory) |

**No connection held during external calls.** All operations are async, non-blocking.

### 12.5 Transaction Duration

| Transaction | Duration |
|---|---|
| Tenant member add/remove | ~10-50ms (single SaveChanges) |
| Access assignment CRUD | ~5-20ms (single SaveChanges) |
| Cross-module (Tenant + Access) | Not atomic — two separate transactions |

### 12.6 Event Delivery Overhead

| Operation | Overhead |
|---|---|
| Publish `TenantMemberAdded` | ~1-5ms (in-process event dispatch) |
| Access event handler update cache | <1ms (in-memory cache update) |
| AccessCache invalidation | <1ms (in-memory) |

**Current design:** No cross-instance communication. Each instance has its own cache. TTL is the safety net.

### 12.7 Cross-Instance Communication

| Operation | Overhead |
|---|---|
| None (in-process events only) | 0ms |
| Distributed cache (future option) | ~1-5ms per invalidation |

**Current design:** No cross-instance communication. Each instance has its own cache. TTL is the safety net.

### 12.8 Failure Behavior

| Failure | Behaviour |
|---|---|
| TenantDbContext down | `TenantResolutionMiddleware` returns error. Request fails before Access. |
| AccessDbContext down | `AccessService` throws. `AccessGuard` propagates error. Request fails with 500. |
| Event handler fails | Tenant change committed. Access cache stale until TTL. |
| Event missed | Access cache stale until TTL. |
| Cache corrupted | AccessCache returns miss. Re-resolves from AccessAssignment table. |

### 12.9 Operational Complexity

| Component | Complexity |
|---|---|
| Tenant → Access events | Low (in-process, existing pattern) |
| Access membership cache | Low (in-memory, existing `AccessCache` pattern) |
| Transactional outbox | High (not currently implemented, would need to add) |
| Reconciliation job | High (not currently implemented) |
| Distributed cache | High (not currently implemented, future option) |

**MVP:** In-process events + in-memory cache + TTL safety net. No distributed infrastructure.

**Future:** Transactional outbox + distributed cache (Redis) for multi-instance consistency.

---

## 13. Migration Slices

### Slice 1: Access-owned `IAccessMembership` implementation

**Problem:** Tenant implements `IAccessMembershipAdapter` (Access-owned interface).

**Changes:**
- Create `CoreKit.Access/Services/AccessMembershipCache.cs` — Access's own `IAccessMembership` implementation reading from an in-memory cache
- Remove `TenantAccessMembership` (Tenant implementing Access's interface)
- Remove `TenantAccessMembership` DI registration from `TenantServiceCollectionExtensions`
- Register `AccessMembershipCache` as `IAccessMembership` in `AccessServiceCollectionExtensions`
- `TenantMembershipReader` remains Tenant-owned (reads Tenant's own table)

**Dependencies:** None (Access is leaf module)

**Tests:**
- `AccessMembershipCacheTests` — cache get/set/invalidate
- `AccessServiceTests` — `GetAccessAsync` reads from cache, not Tenant

**Rollback:** Re-add `TenantAccessMembership`, revert DI registration

---

### Slice 2: Access-owned `IAccessScopeContext` implementation

**Problem:** Tenant implements `IAccessScopeContext` (Access-owned interface).

**Changes:**
- Create `CoreKit.Access/Services/ClaimsAccessScopeContext.cs` — Access's own `IAccessScopeContext` implementation reading `tenant_id` from `HttpContext.Items["tenant_id"]`
- Remove `TenantAccessScopeContext` (Tenant implementing Access's interface)
- Remove `TenantAccessScopeContext` DI registration from `TenantServiceCollectionExtensions`
- Register `ClaimsAccessScopeContext` as `IAccessScopeContext` in `AccessServiceCollectionExtensions`

**Dependencies:** Slice 1

**Tests:**
- `ClaimsAccessScopeContextTests` — reads `tenant_id` from Items, returns null when absent

**Rollback:** Re-add `TenantAccessScopeContext`, revert DI registration

---

### Slice 3: Tenant-published membership events consumed by Access

**Problem:** Access needs membership data without calling Tenant's tables.

**Changes:**
- Define `TenantMemberAdded` / `TenantMemberRemoved` as `IAccessIntegrationEvent` in Access (Access-owned contracts)
- `TenantMemberRemovedToAccessMaintenance` updates Access's membership cache instead of calling `IAccessMaintenance.RemoveUserAsync`
- `TenantMemberAdded` event published by `TenantMembershipService.AddMemberAsync`
- `AccessMembershipCache` updated by event handlers

**Dependencies:** Slice 1, Slice 2

**Tests:**
- `TenantMemberAddedHandlerTests` — cache updated on add
- `TenantMemberRemovedHandlerTests` — cache updated on remove, cache invalidated

**Rollback:** Revert to `IAccessMaintenance.RemoveUserAsync` calls

---

### Slice 4: Identity implements its own `ICurrentUser` (remove Identity's implementation of Access-owned interface)

**Problem:** `ICurrentUserService : ICurrentUser` (Identity implements Access-owned interface).

**Changes:**
- Access implements `ICurrentUser` (`ClaimsAccessCurrentUser` reading JWT `sub` claim)
- Remove `CurrentUserService : ICurrentUser` (Identity no longer implements Access's interface)
- `ICurrentUserService` remains Identity-owned (with `Email`, `Roles`, `Permissions`)
- Identity's `UserService` uses `ICurrentUserService` (Identity-owned), not `ICurrentUser` (Access-owned)
- `SessionIssuer` uses Access's `ICurrentUser` implementation

**Dependencies:** Slice 2

**Tests:**
- `ClaimsAccessCurrentUserTests` — reads `sub` from JWT claims
- `AuthServiceTests` — uses `ICurrentUserService` (Identity-owned), not `ICurrentUser`

**Rollback:** Re-add `CurrentUserService : ICurrentUser`, revert DI registration

---

### Slice 5: Remove `TenantClaimsContributor` (Tenant implementing Access-owned `IAccessTokenClaimsContributor`)

**Problem:** `TenantClaimsContributor : IAccessTokenClaimsContributor` (Tenant implements Access-owned interface).

**Changes:**
- Remove `TenantClaimsContributor`
- Remove DI registration from `TenantServiceCollectionExtensions`
- `tenant_id` claim no longer in JWT
- `TenantResolutionMiddleware` resolves tenant from alternative source (e.g., `X-Tenant-Id` header with validation)

**Dependencies:** Slice 2

**Tests:**
- `TenantResolutionMiddlewareTests` — resolves tenant from alternative source

**Rollback:** Re-add `TenantClaimsContributor`, revert DI registration

**Note:** This slice may be deferred if `tenant_id` claim is needed before an alternative resolution mechanism is implemented. Mark as Owner decision.

---

## 14. Owner Decision Register

| Decision | Status | Decision Required |
|---|---|---|
| Tenant–Access integration pattern | **Open** | Adapter pattern (Tenant implements Access's `IAccessScopeContext`/`IAccessMembershipAdapter`) is architecturally unresolved under D-018. Decide whether to implement Option E (Access-owned membership projection with event-driven synchronization) or approve current adapter pattern as exception to D-018 |
| `IAccessTokenClaimsContributor` implementation | **Open** | Tenant implements Access-owned interface. Decide: remove it (no tenant_id claim in JWT) or keep as transitional |
| `ICurrentTenant` access by Access | **Open** | Access's `IAccessScopeContext` reads `ICurrentTenant` (Tenant-owned). Decide: allow Access to consume Tenant's interface, or move tenant context resolution to Access |
| JWT `SecurityVersion` validation | **Open** (D-03) | Access tokens remain valid until expiry after credential change. Decide: implement JWT validation or document accepted window |
| Rate limiting on tenant/access writes | **Open** (D-18) | Only Identity auth endpoints rate-limited. Decide: add rate limiting to tenant/access write endpoints |
| Transactional outbox for events | **Open** | Current events are in-process only. Decide: implement outbox for reliable cross-module event delivery |
| Distributed cache for multi-instance | **Open** | Current cache is per-instance. Decide: implement Redis/distributed cache for multi-instance consistency |

---

## 15. Risks and Unresolved Evidence

| Risk | Severity | Evidence Level | Mitigation |
|---|---|---|---|
| Tenant–Access adapter violates D-018 | High | Confirmed (source inspection) | Implement Option E |
| Cross-DbContext non-atomicity | High | Confirmed (source inspection) | Transactional outbox + reconciliation |
| Stale JWT after credential change | Medium | Confirmed (source inspection) | Owner decision (D-03) |
| No rate limiting on tenant/access writes | Medium | Confirmed (source inspection) | Owner decision (D-18) |
| In-memory cache cross-instance staleness | Medium | Confirmed (source inspection) | TTL safety net; distributed cache future |
| Event handler failure = stale permissions | Medium | Confirmed (source inspection) | Outbox + retry + reconciliation |
| Identity implements `ICurrentUser` (Access-owned) | Low | Confirmed (source inspection) | Slice 4 |
| `TenantClaimsContributor` implements `IAccessTokenClaimsContributor` | Low | Confirmed (source inspection) | Slice 5 (may be deferred) |

**Unresolved evidence:**
- Runtime behaviour of `CompositeAccessMembership` with Tenant's adapter (not build/test verified)
- Actual cache hit/miss ratios in production (not measured)
- Cross-instance event propagation (not implemented)
- Outbox reliability (not implemented)

---

## 16. Exact Files Inspected

### CoreKit.Access
| File | Purpose |
|---|---|
| `CoreKit.Access/Abstractions/AccessPorts.cs` | `IAccessScopeContext`, `IAccessMembership`, `IAccessMembershipAdapter`, `AccessMember` |
| `CoreKit.Access/Abstractions/AccessScope.cs` | `AccessScope` value type (TenantType, PlatformType, PlatformId) |
| `CoreKit.Access/Abstractions/CompositeAccessMembership.cs` | Composite router for `IAccessMembershipAdapter` |
| `CoreKit.Access/Abstractions/ICurrentUser.cs` | `ICurrentUser : IAccessCurrentUser` |
| `CoreKit.Access/Abstractions/IAccessCurrentUser.cs` | `IAccessCurrentUser` (UserId, IsAuthenticated) |
| `CoreKit.Access/Abstractions/IAccessService.cs` | `IAccessService`, `EffectiveAccess` |
| `CoreKit.Access/Abstractions/IAccessTokenClaimsContributor.cs` | `IAccessTokenClaimsContributor` |
| `CoreKit.Access/Abstractions/IAccessMaintenance.cs` | `IAccessMaintenance` (unguarded) |
| `CoreKit.Access/Abstractions/IPermissionChecker.cs` | `IPermissionChecker` |
| `CoreKit.Access/Abstractions/PlatformAccessMembership.cs` | Platform membership adapter |
| `CoreKit.Access/Services/AccessGuard.cs` | Authorization guard (scope, auth, permission) |
| `CoreKit.Access/Services/AccessService.cs` | `GetAccessAsync`, `ResolveAsync`, `IFreshAccessSource` |
| `CoreKit.Access/Services/AccessCache.cs` | Generation-based permission cache |
| `CoreKit.Access/Services/PlatformMembershipService.cs` | Platform membership management |
| `CoreKit.Access/Services/AccessMaintenance.cs` | `IAccessMaintenance` implementation |
| `CoreKit.Access/Persistence/AccessDbContext.cs` | `ACCESS_*` tables |
| `CoreKit.Access/Persistence/AccessAssignmentRepository.cs` | `ACCESS_Assignments` queries |
| `CoreKit.Access/Extensions/AccessServiceCollectionExtensions.cs` | DI registration |
| `CoreKit.Access/Constants/AccessPermissionNames.cs` | Permission name constants |

### CoreKit.Identity
| File | Purpose |
|---|---|
| `CoreKit.Identity/Services/ICurrentUserService.cs` | `ICurrentUserService : ICurrentUser` |
| `CoreKit.Identity/Services/CurrentUserService.cs` | `CurrentUserService` implementation |
| `CoreKit.Identity/Services/AuthService.cs` | Login, refresh, logout, change-password |
| `CoreKit.Identity/Services/SessionIssuer.cs` | JWT + refresh token issuance |
| `CoreKit.Identity/Services/TokenService.cs` | JWT creation, refresh token hashing |
| `CoreKit.Identity/Services/RefreshTokenRevocation.cs` | Revoke all tokens for user |
| `CoreKit.Identity/Entities/User.cs` | `SecurityVersion`, `AccessFailedCount`, `LockoutEnd` |
| `CoreKit.Identity/Entities/RefreshToken.cs` | `FamilyId`, `RevokedAt`, `RevokedReason` |
| `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` | DI registration (including `ICurrentUser` mapping) |
| `CoreKit.Identity/Migrations/20261110063614_AddIdentityLockoutColumns.cs` | Lockout columns |
| `CoreKit.Identity/Migrations/20261110174117_DK_Core_Security_03A.cs` | `SecurityVersion`, `FamilyId`, `RevokedReason` |
| `CoreKit.Identity/Migrations/20261114113050_RemoveIdentityRbacTables.cs` | Drop Identity RBAC tables |

### CoreKit.Tenant
| File | Purpose |
|---|---|
| `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs` | `TenantAccessScopeContext : IAccessScopeContext` |
| `CoreKit.Tenant/Integration/TenantAccessMembership.cs` | `TenantAccessMembership : IAccessMembershipAdapter` |
| `CoreKit.Tenant/Integration/TenantClaimsContributor.cs` | `TenantClaimsContributor : IAccessTokenClaimsContributor` |
| `CoreKit.Tenant/Integration/TenantMemberRemovedToAccessMaintenance.cs` | Event handler → `IAccessMaintenance.RemoveUserAsync` |
| `CoreKit.Tenant/Abstractions/TenantActor.cs` | `TenantActor` (depends on `ICurrentUser?`) |
| `CoreKit.Tenant/Abstractions/ICurrentTenant.cs` | `ICurrentTenant` (Tenant-owned) |
| `CoreKit.Tenant/Abstractions/ITenantMembershipReader.cs` | `ITenantMembershipReader` (Tenant-owned) |
| `CoreKit.Tenant/Services/TenantMembershipService.cs` | Add/remove/set owner members |
| `CoreKit.Tenant/Services/TenantMembershipReader.cs` | `TenantMembershipReader` (cached membership lookup) |
| `CoreKit.Tenant/Services/TenantAccessGuard.cs` | Tenant boundary guard |
| `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` | DI registration |
| `CoreKit.Tenant/Resolution/TenantResolutionMiddleware.cs` | Resolve tenant from JWT claim |
| `CoreKit.Tenant/Resolution/ClaimTenantResolutionStrategy.cs` | Resolve from `tenant_id` claim |
| `CoreKit.Tenant/Persistence/TenantDbContext.cs` | `TENANT_*` tables |
| `CoreKit.Tenant/Persistence/TenantDbContextConfigurator.cs` | Npgsql configuration |
| `CoreKit.Tenant/Entities/TenantMember.cs` | `MembershipGeneration`, `RemovedAt` |
| `CoreKit.Tenant/Entities/TenantEntity.cs` | `TenantEntity` |
| `CoreKit.Tenant/Events/IntegrationEvents.cs` | `TenantMemberRemoved` (Tenant-owned event) |

---

## 17. Explicit Confirmation

**No repository changes were made during this audit.** No source code, tests, migrations, documentation, project files, or Git history were modified. No builds, tests, migrations, or database operations were performed. All findings are based on static source inspection at baseline `73932a6125a89f42553732c313a6dda496a42656`.

**This is a design document only.** No implementation has been started or authorized. All recommendations are subject to Project Owner approval.

---

**End of design report.** Awaiting independent review by Claude Account 2 and Project Owner decision.
