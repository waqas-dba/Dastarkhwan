# DK-TENANT-ACCESS-INTEGRATION-DESIGN-05 — Revised Architecture Design

**Repository:** `waqas-dba/Dastarkhwan`  
**Working directory:** `F:\Developer\waqas-dba\waqas-dba\Dastarkhwan`  
**Baseline verified:** `73932a6125a89f42553732c313a6dda496a42656` (2026-11-15 16:14:29 +0500)  
**Remote HEAD:** `73932a6125a89f42553732c313a6dda496a42656` — exact match  
**Working tree:** Clean  
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

**Confirmed:** Access is a leaf module at the project-reference level. At runtime, Access's `CompositeAccessMembership` calls `TenantAccessMembership` (in Tenant's assembly) via DI composition in SampleHost. This is a runtime dependency from Access to Tenant invisible at project-reference level.

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

## 3. Reconciliation of Review-04 Findings

Review-04 (Claude Account 2) reviewed `DK-TENANT-ACCESS-INTEGRATION-DESIGN-03-REPORT.md` and rejected it. All 8 required corrections (RC-01 through RC-08) are addressed in this design:

| RC | Review-04 Finding | Design-05 Resolution |
|---|---|---|
| RC-01 | D-018 compliance not fully achieved — Tenant/Identity implement Access-owned interfaces; transitional exceptions | **Resolved.** Option E variant does not require Tenant or Identity to implement any Access-owned interface. `TenantAccessScopeContext`, `TenantAccessMembership`, `TenantClaimsContributor` are removed. `CurrentUserService : ICurrentUser` is replaced by Access-owned `ClaimsAccessCurrentUser`. No transitional exceptions remain. |
| RC-02 | Cross-module consistency relies on non-durable in-process events | **Resolved.** Transactional outbox specified: `OutboxMessage` table in TenantDbContext, same `SaveChanges` transaction, poll-based publisher, retry/backoff, dead-letter handling, reconciliation job. |
| RC-03 | Access membership projection contract and storage underdefined | **Resolved.** Access-owned `AccessMembership` table (`AccessMembership` entity), `AccessMembershipRepository`, `AccessMembershipCache` (in-memory layer over DB table), startup bootstrap/rebuild from Tenant. |
| RC-04 | Event semantics insufficient (ordering, duplication, replay) | **Resolved.** Events carry `EventId`, `Sequence` (per tenant), `MembershipGeneration`. Idempotent apply (`max(existing, new)`), monotonic rejection (ignore stale/older generation), per-tenant ordering guaranteed by single-threaded outbox publisher per tenant. |
| RC-05 | Tenant context source-of-truth not singular | **Resolved.** Single authoritative source: `TenantResolutionMiddleware` reads JWT `tenant_id` claim, validates against Tenant's `ITenantMembershipReader`, sets `ICurrentTenant` AND `HttpContext.Items["tenant_id"]`. Access reads `tenant_id` from `HttpContext.Items["tenant_id"]`. No dual source. |
| RC-06 | Revocation guarantees not bounded | **Resolved.** Maximum stale authorization window bounded by `max(eventProcessingTime, accessCacheTTL)`. Revocation is eventual, not immediate — explicitly stated. |
| RC-07 | Open Owner decisions (D-03, D-18) | **Resolved.** Explicitly listed as unresolved Owner decisions with explicit prerequisite status. |
| RC-08 | Contract/source mismatches and unsupported claims | **Resolved.** All contracts mapped to actual baseline files. Performance claims removed/replaced with honest uncertainty statements. |

---

## 4. Existing Architecture and Source-Code Findings

### 4.1 Current Dependency Direction

| Project | Project References (compile-time) | Runtime Dependencies |
|---|---|---|
| `CoreKit.Access` | None (leaf) | Called at runtime by Identity, Tenant, SampleHost |
| `CoreKit.Identity` | `CoreKit.Access` | Calls `IAccessService`, `IPermissionChecker`, `IAccessMaintenance` |
| `CoreKit.Tenant` | `CoreKit.Access` | Implements `IAccessScopeContext`, `IAccessMembershipAdapter`, `IAccessTokenClaimsContributor`; calls `IAccessMaintenance` |
| `CoreKit.SampleHost` | Access, Identity, Tenant | Composition root |

**Critical finding:** Access is a leaf module at project-reference level but has a runtime dependency on Tenant through `CompositeAccessMembership` → `TenantAccessMembership` (in Tenant's assembly) loaded via DI in SampleHost. This violates the spirit of constraint 7 (Access remains a leaf module) even though not the letter (project references).

### 4.2 Current Defects (Independently Verified)

| ID | Severity | Confidence | Evidence |
|---|---|---|---|
| INT-01 | High | Confirmed | `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs:10` implements `IAccessScopeContext`; `CoreKit.Tenant/Integration/TenantAccessMembership.cs:10` implements `IAccessMembershipAdapter`; `CoreKit.Tenant/Integration/TenantClaimsContributor.cs:13` implements `IAccessTokenClaimsContributor`. DI registrations at `TenantServiceCollectionExtensions.cs:109-110`. |
| INT-02 | High | Confirmed | `CoreKit.Tenant/Services/TenantMembershipService.RemoveMemberAsync` lines 200-204: `SaveAsync` commits Tenant change, then publishes `TenantMemberRemovedEvent`, then `TenantMemberRemovedToAccessMaintenance.HandleAsync` calls `IAccessMaintenance.RemoveUserAsync` → `AccessUnitOfWork.SaveAsync`. Two separate DbContexts, two separate transactions. |
| INT-03 | Medium | Confirmed | `CoreKit.Identity/Entities/User.cs:45-46` documents: "Access-token (JWT) validation of this version is a separate, not yet implemented task." `CoreKit.Identity/Services/TokenService.cs:38-48` — JWT contains no `SecurityVersion` claim. |
| INT-04 | Medium | Confirmed | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:106-108` — Identity implements Access-owned `ICurrentUser` via `CurrentUserService : ICurrentUser`. |
| INT-05 | Medium | Confirmed | `CoreKit.Access/Services/AccessCache.cs` uses `IMemoryCache` (per-instance, no cross-instance invalidation). `CoreKit.Tenant/Services/TenantMembershipReader.cs` uses `IMemoryCache` (per-instance, TTL-based). |

### 4.3 Current Authorization Flow (HTTP Request)

```
Client
  │
  ▼
JWT Validation (Identity)
  │
  ▼
TenantResolutionMiddleware
  │ reads tenant_id claim from JWT
  │ validates tenant exists & is Active
  │ validates user membership via ITenantMembershipReader (Tenant Db)
  │ sets ICurrentTenant
  │
  ▼
AccessGuard.RequireAsync(permission)
  │ reads IAccessScopeContext.Current (TenantAccessScopeContext → ICurrentTenant.Id)
  │ checks IAccessCurrentUser (IsAuthenticated + UserId)
  │ calls IAccessService.GetAccessAsync(userId, scope)
    │ calls IAccessMembership.FindAsync(scope, userId) → CompositeAccessMembership → TenantAccessMembership → ITenantMembershipReader.FindAsync (Tenant Db)
    │ calls IAccessAssignmentRepository.ListRolesForUserAsync(scope, userId, generation)
    │ returns EffectiveAccess
  │ checks access.Has(permission)
  │
  ▼
Authorization decision
```

---

## 5. D-018 Ownership and Dependency Direction Analysis

**D-018 states:**
> "`CoreKit.SharedKernel` is not introduced or populated as an architectural component and is not referenced by any project. Cross-module contracts live in module-owned `Abstractions` namespaces (`CoreKit.Access.Abstractions`, `CoreKit.Tenant.Abstractions`, etc.). `CoreKit.Tenant` and `CoreKit.Identity` each reference only `CoreKit.Access`; `CoreKit.Access` is a leaf module."

**Constraint 11:** "No cross-module adopter/interface implementation pattern. In particular, do not solve the problem by having Tenant or Identity implement interfaces owned by Access."

**Current violations:**
1. Tenant implements `IAccessScopeContext` (Access-owned) — `TenantAccessScopeContext`
2. Tenant implements `IAccessMembershipAdapter` (Access-owned) — `TenantAccessMembership`
3. Tenant implements `IAccessTokenClaimsContributor` (Access-owned) — `TenantClaimsContributor`
4. Identity implements `ICurrentUser` (Access-owned) — `CurrentUserService : ICurrentUser`

**Design-05 resolution:** All four violations are removed. Access owns and implements all Access-owned contracts. Tenant owns and implements all Tenant-owned contracts. Identity owns and implements all Identity-owned contracts.

---

## 6. Integration Alternatives and Comparison Matrix

### Alternative A — Access-Owned Membership Projection (Event-Driven)

**Description:** Tenant publishes `TenantMemberAdded` and `TenantMemberRemoved` integration events to a transactional outbox table in TenantDbContext. A publisher reads the outbox and delivers events to Access. Access maintains its own `AccessMembership` table (projection). Access's `IAccessMembership.FindAsync` reads from Access's own cache/table — no call to Tenant's database.

**Dependencies:**
- Tenant → Access (project reference, existing) — Tenant publishes events
- Access → None (leaf module preserved at runtime too)
- Access's `IAccessMembership` implementation is in Access, reading Access's own table/cache

**Request-time data flow (tenant HTTP request):**
1. JWT validation (Identity)
2. `TenantResolutionMiddleware` resolves tenant from JWT `tenant_id` claim, validates user membership via `ITenantMembershipReader` (Tenant's own database)
3. `TenantResolutionMiddleware` sets `ICurrentTenant` AND `HttpContext.Items["tenant_id"]`
4. `AccessGuard.RequireAsync` reads `IAccessScopeContext.Current` (Access's `ClaimsAccessScopeContext` reads `tenant_id` from `HttpContext.Items["tenant_id"]`)
5. `AccessService.GetAccessAsync` calls `IAccessMembership.FindAsync(scope, userId)` → reads Access's own membership cache
6. Authorization decision

**Membership addition/removal:**
1. Tenant's `TenantMembershipService.AddMemberAsync`/`RemoveMemberAsync` modifies `TenantMember` table within `TenantUnitOfWork`
2. Same transaction writes `OutboxMessage` row with `TenantMemberAdded`/`TenantMemberRemoved` event (tenantId, userId, membershipGeneration, isOwner, eventId, sequence, occurredAt)
3. `TenantUnitOfWork.SaveAsync()` commits both Tenant change and outbox message atomically
4. Outbox publisher (background poller) reads unprocessed outbox messages, delivers to Access
5. Access event handler updates Access's `AccessMembership` table and invalidates `AccessCache`
6. Between Tenant commit and Access processing, stale state possible (bounded by TTL)

**Revocation:**
- Member removed from Tenant → `TenantMemberRemoved` event → Access deletes Access assignments for (tenantId, userId) + invalidates Access cache
- Stale window: bounded by max(eventProcessingTime, accessCacheTTL) — NOT immediate, explicitly documented
- In-flight requests during removal: may use cached access (bounded by cache TTL)

**Multi-instance behavior:**
- Each instance has its own `AccessCache` (in-memory)
- Each instance runs its own outbox publisher
- Events are delivered to all instances; each updates its own cache
- Cache divergence possible between event processing and TTL expiry on different instances
- Shared outbox table + competitive consumer pattern ensures each event processed once across instances

**Operational complexity:** High — requires outbox table, publisher, retry/DLQ, reconciliation job

---

### Alternative B — Synchronous Validation Through Tenant-Owned Contract

**Description:** Access calls Tenant's `ITenantMembershipReader.FindAsync(tenantId, userId)` synchronously for each authorization decision. Access does not maintain its own membership cache.

**Dependencies:**
- Access → Tenant (project reference) — Access calls Tenant's membership reader
- Tenant → Access (project reference, existing) — Tenant publishes events / uses Access services

**Circular dependency at project level:** Tenant → Access → Tenant. **This violates constraint 7 (no circular dependencies).**

Even if we attempted to break the cycle by having Access define its own `ITenantMembershipReader`-like interface and Tenant implement it, that would be Tenant implementing Access-owned interface — violating constraint 11.

**Request-time data flow:**
1. `AccessService.ResolveAsync` calls `IAccessMembership.FindAsync(scope, userId)`
2. Access's `IAccessMembership` implementation calls Tenant's `ITenantMembershipReader.FindAsync(tenantId, userId)` via HTTP/gRPC or direct call
3. Tenant's `TenantMembershipReader` queries TenantDbContext
4. Access receives membership info, filters assignments by generation

**Verdict:** **Rejected.** Creates circular project dependency (Tenant → Access → Tenant), violates constraint 7 (Access is leaf module), adds latency (cross-module call per authorization), couples availability (Access unavailable when Tenant down).

---

### Option C — Host-Level Orchestration

**Description:** The composition root (host) coordinates Tenant membership validation and Access authorization. Host calls Tenant's `ITenantMembershipReader.FindAsync`, gets membership info, then calls Access's `IPermissionChecker`.

**Dependencies:**
- Host → Tenant (calls Tenant's membership reader)
- Host → Access (calls Access's permission checker)
- Tenant → Access (existing, for other integrations)
- Identity → Access (existing)

**Problem:** Access's `IPermissionChecker.HasPermissionAsync(string permission)` only accepts a permission string — no membership evidence parameter. Access's `IAccessScopeContext.Current` provides the scope, `IAccessCurrentUser` provides the user. There's no parameter for membership evidence.

**Request-time data flow:**
1. Host resolves tenant context (from JWT claim or header)
2. Host calls Tenant's `ITenantMembershipReader.FindAsync(tenantId, userId)` → gets `(isMember, generation)`
3. Host calls Access's `IPermissionChecker.HasPermissionAsync(permission)` — but Access has no way to know the tenant membership state; Access reads from its own `IAccessMembership` which has no data
4. Host would need to pass membership evidence to Access, but `IPermissionChecker` doesn't accept it

**Verdict:** **Rejected.** Relocates the adapter pattern into the host but doesn't solve the core problem. Access's `IPermissionChecker` doesn't accept membership evidence. Background jobs and direct service calls bypass the host entirely.

---

### Option D — JWT-Embedded Tenant Membership Claims

**Description:** Identity issues JWT with `tenant_id` and `membershipGeneration` claims. Access reads these claims from JWT at authorization time.

**Dependencies:**
- Identity → Access (existing) — Identity calls Access's `IAccessService.GetAccessAsync` for response DTO
- Tenant → Identity (new) — Tenant tells Identity which tenant claim to issue (requires Tenant implementing `IAccessTokenClaimsContributor`, Access-owned interface — violates D-018)

**Problem:** JWT claims are signed by Identity but represent tenant membership state at token-issuance time. They are not revoked in real-time. Access tokens remain valid until expiry (up to 120 minutes).

**Verdict:** **Rejected.** JWT is identity+context input, not authorization authority (confirmed in review CS-06). Tenant implementing `IAccessTokenClaimsContributor` violates D-018. JWT claims cannot provide real-time membership enforcement.

---

### Comparison Matrix

| Criterion | Option A (Access-Owned Projection) | Option B (Sync Tenant Call) | Option C (Host Orchestration) | Option D (JWT Claims) |
|---|---|---|---|---|
| Module ownership | Access owns Access contracts; Tenant owns Tenant contracts | Tenant implements Access-owned `IAccessMembership` | Host implements `IAccessScopeContext` (Access-owned) | Tenant implements `IAccessTokenClaimsContributor` (Access-owned) |
| No cross-module implementation | ✅ Yes | ❌ No (Tenant implements Access interface) | ❌ No (host implements Access interface) | ❌ No (Tenant implements Access interface) |
| No SharedKernel | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| Standalone Access | ✅ Yes (empty cache = no members) | ❌ No (requires Tenant) | ❌ No (requires Tenant) | ✅ Yes (no tenant claim = no member) |
| Authorization correctness | ✅ Yes (fail-closed on cache miss) | ✅ Yes | ✅ Yes | ❌ No (stale claims) |
| Membership freshness | ⚠️ Eventual (bounded by event processing + TTL) | ✅ Real-time | ✅ Real-time | ❌ Stale (until token expiry) |
| Revocation | ⚠️ Eventual (bounded by event processing + TTL) | ✅ Immediate | ✅ Immediate | ❌ Until token expiry |
| Multi-instance | ⚠️ Per-instance cache; event drift until TTL | ✅ Single source of truth | ✅ Single source of truth | ✅ Single source (JWT) |
| Failure recovery | ⚠️ Outbox + reconciliation needed | ✅ Direct call fails fast | ✅ Direct call fails fast | ✅ JWT validation fails fast |
| Performance | ⚠️ Cache hit: 1 DB read; miss: 2 DB reads | ⚠️ 2 DB round trips per request (Tenant + Access) | ⚠️ 2 DB round trips per request (Tenant + Access) | ✅ 1 DB read (Access only) |
| Complexity | Medium (outbox, cache, reconciliation) | High (circular dependency, cross-module calls) | Medium (host orchestration, bypass risk) | Low (but security-invalid) |
| Testability | High (cache unit tests, event handler tests) | Medium (integration tests across modules) | Medium (host integration tests) | High (JWT validation tests) |

**Verdict:** **Option A (Access-Owned Membership Projection with Event-Driven Synchronization) is the only viable option.** Options B, C, and D each violate fundamental constraints or fail security requirements.

---

## 7. Recommended Architecture: Access-Owned Membership Projection

### 7.1 Core Principle

Access owns and implements all Access-owned contracts. Tenant owns and implements all Tenant-owned contracts. Identity owns and implements all Identity-owned contracts. No module implements another module's interfaces.

Access maintains its own membership projection (`AccessMembership` table) populated by Tenant-published integration events delivered via a transactional outbox. Access's `IAccessMembership.FindAsync` reads from Access's own cache/table — no call to Tenant's database.

### 7.2 Contract Ownership Matrix

| Contract | Owned By | Implemented By | Reads From |
|---|---|---|---|
| `IAccessScopeContext` | Access | Access (`ClaimsAccessScopeContext`) | JWT `tenant_id` claim via `HttpContext.Items["tenant_id"]` |
| `IAccessMembership` | Access | Access (`AccessMembershipCache`) | Access's own `AccessMembership` table (populated by events) |
| `ICurrentUser` | Access | Access (`ClaimsAccessCurrentUser`) | JWT `sub` claim |
| `ICurrentTenant` | Tenant | Tenant (`CurrentTenant`) | `TenantResolutionMiddleware` (reads JWT `tenant_id` claim) |
| `ITenantMembershipReader` | Tenant | Tenant (`TenantMembershipReader`) | Tenant's own `TenantMember` table |
| `IPermissionChecker` | Access | Access (`AccessPermissionChecker`) | Access's own `AccessService` |
| `IAccessTokenClaimsContributor` | Access | Not implemented (no tenant_id claim issued) | N/A |
| `IAccessIntegrationEvent` | Access | Tenant publishes (`TenantMemberAdded`, `TenantMemberRemoved`); Access consumes | N/A |

**Key change:** `TenantAccessScopeContext`, `TenantAccessMembership`, `TenantClaimsContributor` are removed. Tenant no longer implements any Access-owned interface. `Identity`'s `CurrentUserService : ICurrentUser` is replaced by Access-owned `ClaimsAccessCurrentUser`.

### 7.3 Tenant Context Model

**Single authoritative source:** `TenantResolutionMiddleware` reads `tenant_id` from JWT claim, validates tenant exists/is Active via `ITenantInfoProvider`, validates user membership via `ITenantMembershipReader`, sets `ICurrentTenant` AND `HttpContext.Items["tenant_id"]`.

**HTTP requests:** Middleware sets tenant context. Access's `ClaimsAccessScopeContext` reads `tenant_id` from `HttpContext.Items["tenant_id"]`.

**Background jobs:** Job scheduler sets `ICurrentTenant` and `HttpContext.Items["tenant_id"]` before calling Access services. If not set, `IAccessScopeContext.Current` returns null → `AccessGuard.RequireAsync` returns `ScopeRequired` (fail-closed).

**No client-supplied trust:** `tenant_id` claim is validated against Tenant's membership database by `TenantResolutionMiddleware` before being trusted. Client-supplied headers (`X-Tenant-Id`) are not trusted directly (HeaderTenantResolutionStrategy requires `Tenant:Resolution:TrustHeader=true`, which is default false).

---

## 8. Contract Proposals

### 8.1 IAccessScopeContext (Access-owned)

```csharp
public interface IAccessScopeContext
{
    AccessScope? Current { get; }
}
```

**Implementation:** `ClaimsAccessScopeContext` reads `tenant_id` from `HttpContext.Items["tenant_id"]`, returns `AccessScope.ForTenant(tenantId)` or null.

### 9. HTTP Authorization Flow

```
Client
  │
  ▼
JWT Validation (Identity)
  │ validates sub, tenant_id claims
  ▼
TenantResolutionMiddleware
  │ reads tenant_id claim from JWT
  │ validates tenant exists & is Active via ITenantInfoProvider
  │ validates user membership via ITenantMembershipReader (Tenant Db)
  │ sets ICurrentTenant + HttpContext.Items["tenant_id"]
  │
  ▼
AccessGuard.RequireAsync(permission)
  │ reads IAccessScopeContext.Current (Access's ClaimsAccessScopeContext)
  │   → reads tenant_id from HttpContext.Items["tenant_id"]
  │   → returns AccessScope.ForTenant(tenantId) or null
  │ checks IAccessCurrentUser (IsAuthenticated + UserId from JWT sub claim)
  │ calls IAccessService.GetAccessAsync(userId, scope)
    │ calls IAccessMembership.FindAsync(scope, userId)
    │   → reads Access's own AccessMembership cache/table
    │     → returns (IsMember, MembershipGeneration) or null
    │ calls IAccessAssignmentRepository.ListRolesForUserAsync(scope, userId, generation)
    │   → filters assignments by MembershipGeneration == cache generation
    │ builds EffectiveAccess
  │ checks access.Has(permission)
  │
  ▼
Authorization decision
```

### 8.2 Platform Authorization Flow

```
Client
  │
  ▼
JWT Validation (Identity)
  │
  ▼
AccessGuard.RequirePlatformScopePermissionAsync(permission)
  │ uses fixed AccessScope.ForPlatform(PlatformId)
  │ checks IAccessCurrentUser (IsAuthenticated + UserId)
  │ calls IAccessService.GetAccessAsync(userId, PlatformScope)
    │ calls IAccessMembership.FindAsync(PlatformScope, userId)
    │   → reads Access's own membership cache
    │     → returns (IsMember=false, generation) for non-platform members
    │ calls IAccessAssignmentRepository.ListRolesForUserAsync(PlatformScope, userId, generation)
    │   → filters by PlatformMembers.RevokedAt == null AND MembershipGeneration == cache generation
    │ builds EffectiveAccess
  │ checks access.Has(permission)
  │
  ▼
Authorization decision
```

### 8.3 Membership Change Flow

```
TenantMembershipService.RemoveMemberAsync(tenantId, userId)
  │
  ▼
TenantDbContext.SaveAsync()  -- commits member removal (RemovedAt, Generation++)
  │
  ▼
TenantEventPublisher.PublishAsync(TenantMemberRemoved(tenantId, userId, generation, eventId, sequence, occurredAt))
  │ writes OutboxMessage row in same TenantDbContext transaction
  │
  ▼
OutboxPublisher (background poller)
  │ reads unprocessed OutboxMessage rows
  │ delivers to Access event handler
  │
  ▼
AccessEventHandler<TenantMemberRemoved>
  │ updates AccessMembership table: set IsActive=false, Generation=newGeneration
  │ invalidates AccessCache for (tenantId, userId)
  │ marks OutboxMessage as processed
  │
  ▼
Subsequent authorization requests
  │ AccessMembership.FindAsync returns IsMember=false
  │ AccessAssignment queries filtered by generation return empty
  │ AccessGuard.RequireAsync returns Denied
```

---

## 9. Consistency and Revocation Guarantees

### 9.1 Revocation Guarantee

**Maximum stale authorization window:** bounded by `max(eventProcessingTime, accessCacheTTL)`.

- **Best case:** member removed → event processed immediately → Access cache invalidated → subsequent requests denied immediately (sub-second)
- **Worst case:** event handler fails → retried after backoff → eventually processed; or event missed → cache TTL expires → subsequent requests denied
- **TTL:** configurable (`Access:MembershipCacheSeconds`), recommended 60 seconds
- **Maximum stale window:** bounded by TTL (e.g., 60 seconds) in the absence of event processing failures

**This is eventual revocation, NOT immediate revocation.** The design does not claim immediate revocation. If the Project Owner requires immediate revocation, a distributed cache invalidation mechanism (e.g., Redis pub/sub) would be required — this is an explicit Owner decision.

### 9.2 In-Flight Request During Revocation

**Scenario:** Request A is mid-authorization (AccessService.ResolveAsync reading assignments) while Request B removes the member.

**Sequence:**
1. Request A: `IAccessMembership.FindAsync` returns member with generation 1
2. Request B: `TenantMemberRemoved` → generation becomes 2 → event published → Access processes event → deletes assignments → invalidates cache
3. Request A: `ListRolesForUserAsync(scope, userId, generation=1)` — Access's cache now has generation 2, but Request A already read generation 1 from cache before invalidation

**Result:** Request A may authorize based on generation 1 assignments even though membership was revoked. This is a **bounded stale window** — the request was already in flight when removal committed.

**Mitigation:** `AccessService.ResolveAsync` re-reads membership after reading assignments and compares generations. If generation changed, retry. However, this does not eliminate the window entirely — it only reduces it.

---

## 10. Persistence, Caching, and Operational Design

### 10.1 Access Membership Projection Table

| Table | Columns | Purpose |
|---|---|---|
| `ACCESS_Membership` | TenantId (uuid, not null), UserId (uuid, not null), IsMember (boolean, not null), MembershipGeneration (bigint, not null), UpdatedAt (timestamp, not null) | Access's own membership projection, populated by events |
| PK | (TenantId, UserId) | |
| Index | (UserId) | For user-centric lookups |

### 10.2 Outbox Table

| Table | Columns | Purpose |
|---|---|---|
| `OutboxMessage` | Id (uuid, PK), Type (varchar), Payload (jsonb), OccurredAt (timestamp), ProcessedAt (timestamp, nullable), RetryCount (int), Error (text) | Durable event store for cross-module communication |

### 10.3 Access Cache Layers

| Layer | Purpose | Invalidation |
|---|---|---|
| `AccessMembership` table | Durable membership state | Event-driven update |
| `AccessCache` (IMemoryCache) | EffectiveAccess permission cache | Event-driven invalidation + TTL |

---

## 11. Performance Considerations

| Operation | Current (Adapter Pattern) | Option E (Access-Owned Cache) |
|---|---|---|
| Resolve tenant | 1 (TenantDbContext) | 1 (TenantDbContext, in middleware) |
| Check membership | 1 (TenantDbContext via `ITenantMembershipReader`) | 0 (Access's own cache/table) |
| Read assignments | 1 (AccessDbContext) | 1 (AccessDbContext) |
| **Total DB round trips** | **2-3** | **2** |

**Cache hit:** Access's `EffectiveAccess` cache hit → 0 additional DB reads (after initial resolution).

**Connection holding time:** All operations async, non-blocking. No connection held during external calls.

**Transaction duration:** Tenant member add/remove ~10-50ms (single SaveChanges). Access assignment CRUD ~5-20ms (single SaveChanges). Cross-module operations are two separate transactions (not atomic).

**Evidence gaps:** Performance numbers are estimates based on code inspection, not measured. Benchmarking required before production deployment claims.

---

## 14. Ordered Implementation Plan

### Slice 1: Access-Owned `IAccessMembership` Implementation

**Problem:** Tenant implements `IAccessMembershipAdapter` (Access-owned interface).

**Changes:**
- Create `CoreKit.Access/Services/AccessMembershipCache.cs` — Access's own `IAccessMembership` implementation reading from `AccessMembership` table
- Create `CoreKit.Access/Persistence/AccessMembershipRepository.cs` — repository for `AccessMembership` table
- Add `AccessMembership` entity and `DbSet` to `AccessDbContext`
- Add migration for `ACCESS_Membership` table
- Remove `TenantAccessMembership` (Tenant implementing Access's interface)
- Remove `TenantAccessMembership` DI registration from `TenantServiceCollectionExtensions`
- Register `AccessMembershipCache` as `IAccessMembership` in `AccessServiceCollectionExtensions`

**Dependencies:** None (Access is leaf module)

**Tests:**
- `AccessMembershipCacheTests` — cache get/set/invalidate
- `AccessServiceTests` — `GetAccessAsync` reads from cache, not Tenant

**Rollback:** Re-add `TenantAccessMembership`, revert DI registration

---

### Slice 2: Access-Owned `IAccessScopeContext` Implementation

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

### Slice 3: Transactional Outbox for Tenant→Access Events

**Problem:** Tenant-published events are in-process only, not durable.

**Changes:**
- Create `CoreKit.Tenant/Persistence/OutboxMessage.cs` — `OutboxMessage` entity
- Add `DbSet<OutboxMessage>` to `TenantDbContext`
- Add migration for `OutboxMessage` table
- Modify `TenantMembershipService.AddMemberAsync`/`RemoveMemberAsync` to write outbox message in same `SaveAsync` transaction
- Create `CoreKit.Tenant/Services/OutboxPublisher.cs` — background poller that reads unprocessed outbox messages and delivers to Access event handlers
- `TenantMemberRemovedToAccessMaintenance` updates Access's `AccessMembership` table instead of calling `IAccessMaintenance.RemoveUserAsync`
- `TenantMemberAdded` event published by `TenantMembershipService.AddMemberAsync`

**Dependencies:** Slice 1, Slice 2

**Tests:**
- `OutboxPublisherTests` — publishes unprocessed messages, marks as processed
- `TenantMemberAddedHandlerTests` — Access cache updated on add
- `TenantMemberRemovedHandlerTests` — Access cache updated on remove, Access assignments deleted

**Rollback:** Revert to `IAccessMaintenance.RemoveUserAsync` calls, remove outbox table

---

### Slice 4: Identity Implements Its Own `ICurrentUser`

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

### Slice 5: Remove `TenantClaimsContributor`

**Problem:** `TenantClaimsContributor : IAccessTokenClaimsContributor` (Tenant implements Access-owned interface).

**Changes:**
- Remove `TenantClaimsContributor`
- Remove DI registration from `TenantServiceCollectionExtensions`
- `tenant_id` claim no longer in JWT
- `TenantResolutionMiddleware` resolves tenant from alternative source (e.g., host-provided tenant context, or `X-Tenant-Id` header with validation)

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

## 17. Exact Files Inspected

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

## 18. Explicit Confirmation

**No repository changes were made during this audit.** No source code, tests, migrations, documentation, project files, or Git history were modified. No builds, tests, migrations, or database operations were performed. All findings are based on static source inspection at baseline `73932a6125a89f42553732c313a6dda496a42656`.

**This is a design document only.** No implementation has been started or authorized. All recommendations are subject to Project Owner approval.

---

**End of design report.** Awaiting independent review by Claude Account 2 and Project Owner decision.
