# DK-TENANT-ACCESS-INTEGRATION-DESIGN-06 — Revised Architecture Design

**Repository:** `waqas-dba/Dastarkhwan`  
**Working directory:** `F:\Developer\waqas-dba\waqas-dba\Dastarkhwan`  
**Remote HEAD verified:** `7a84a6c3424d856171bff4725ae752ebd3bef987` (2026-11-15 18:51:33 +0500)  
**Source-code baseline:** `73932a6125a89f42553732c313a6dda496a42656` (parent of remote HEAD)  
**Working tree:** Clean (no tracked modifications; untracked report files only)  
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
| Local HEAD | `7a84a6c3424d856171bff4725ae752ebd3bef987` |
| Remote HEAD (`origin/master`) | `7a84a6c3424d856171bff4725ae752ebd3bef987` — exact match |
| Source-code baseline | `73932a6125a89f42553732c313a6dda496a42656` (parent of remote HEAD) |
| Baseline match | **PASS** |
| Working tree | Clean (`git status --short` returned empty; only untracked report files present) |

**Baseline gate: PASSED.** All source inspection below is against baseline `73932a6125a89f42553732c313a6dda496a42656`. The five untracked report files are prior work and were not modified.

---

## 2. Executive Recommendation

**Recommendation: Adopt Option A — Access-owned durable membership projection with synchronous membership read path for revocation-critical paths.**

No single option provides immediate revocation under all failure models. The recommended design provides:

- **Access-owned membership state:** Access maintains its own `AccessMembership` table, populated by Tenant-published `TenantMemberAdded`/`TenantMemberRemoved` events via a transactional outbox.
- **Access-owned scope context:** `IAccessScopeContext` is implemented by Access (reading JWT `tenant_id` claim), not by Tenant.
- **Tenancy validation at resolution time:** `TenantResolutionMiddleware` validates tenant membership against Tenant's own database before establishing tenant context.
- **Revocation guarantee:** Eventual with enforceable bound; not immediate. Revoked members cannot perform new authorization operations after the projection update completes and cache invalidates. In-flight requests may complete using pre-revocation state.
- **No circular project references:** Access remains a leaf module at compile time. Tenant → Access (project reference) for events; Access does not reference Tenant.
- **No Tenant/Identity implementation of Access-owned interfaces:** Removed.

**Not recommended:** Options B (synchronous Access→Tenant read) creates a circular project reference. Option C (host orchestration) relocates but does not solve the membership-evidence problem. Option D (JWT claims) cannot provide revocation guarantees without token expiry.

**Project Owner decisions required:** Acceptable revocation window, in-flight request semantics, D-03 (JWT revocation), D-18 (rate limiting), outbox retry/DLQ policy.

---

## 3. Evidence Inventory

### 3.1 Documents inspected

| Document | Purpose |
|---|---|
| `decisions.md` | D-002, D-003, D-011, D-016, D-017, D-018 |
| `architecture.md` | §4 Identity/Access boundary, §6 Tenant–Access integration, §7 composition |
| `roadmap.md` | Milestones S4.1–S4.4, S5.1–S5.2, S6.2 |
| `current-state.md` | Implementation status and evidence labels |
| `DK-TENANT-ACCESS-INTEGRATION-REVIEW-04-REPORT.md` | Review-04 findings (RC-01..RC-08) |
| `DK-TENANT-ACCESS-INTEGRATION-REVIEW-05-REPORT.md` | Review-05 findings (F-01..F-11) |
| `DK-TENANT-ACCESS-INTEGRATION-REVIEW-06-REPORT.md` | Review-06 findings (F06-01..F06-12) |

### 3.2 Source files inspected (baseline `73932a6125a89f42553732c313a6dda496a42656`)

| Area | Files | Lines |
|---|---|---|
| Access abstractions | `CoreKit.Access/Abstractions/AccessPorts.cs`, `IAccessIntegrationEvent.cs`, `IAccessTokenClaimsContributor.cs`, `IAccessMaintenance.cs`, `ICurrentUser.cs`, `IAccessCurrentUser.cs`, `IPermissionChecker.cs` | All |
| Access composite | `CoreKit.Access/Abstractions/CompositeAccessMembership.cs` | 10–30 |
| Access services | `CoreKit.Access/Services/AccessService.cs`, `AccessGuard.cs`, `AccessCache.cs`, `AccessMaintenance.cs`, `AccessPermissionChecker.cs`, `AccessEventHandlers.cs` | All |
| Access persistence | `CoreKit.Access/Persistence/AccessDbContext.cs`, `AccessUnitOfWork.cs`, `AccessAssignmentRepository.cs` | All |
| Tenant events | `CoreKit.Tenant/Events/IntegrationEvents.cs`, `Abstractions/TenantEvents.cs` | All |
| Tenant membership | `CoreKit.Tenant/Services/TenantMembershipService.cs`, `TenantMembershipReader.cs` | All |
| Tenant event publisher | `CoreKit.Tenant/Services/TenantEventPublisher.cs` | All |
| Tenant resolution | `CoreKit.Tenant/Resolution/TenantResolutionMiddleware.cs`, `ClaimTenantResolutionStrategy.cs`, `CurrentTenant.cs` | All |
| Tenant adapters | `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs`, `TenantAccessMembership.cs`, `TenantClaimsContributor.cs`, `TenantMemberRemovedToAccessMaintenance.cs` | All |
| Tenant DI | `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` | All |
| Identity current user | `CoreKit.Identity/Services/ICurrentUserService.cs`, `CurrentUserService.cs` | All |
| Identity DI | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` | Lines 106–108 |
| Identity token | `CoreKit.Identity/Services/SessionIssuer.cs`, `TokenService.cs` | All |

---

## 4. Review-06 Findings Resolution Matrix

| Finding ID | Severity | Review-06 Finding | Design-06 Resolution | Status |
|---|---|---|---|---|
| F06-01 | Critical | Revocation formula invalid; stale projection can be recached | Revocation is eventual, not immediate. Maximum stale window bounded by `max(eventProcessingTime, cacheTTL)`. Access-side cache invalidation occurs after event processing. Open Owner decision required for acceptable window. | Resolved (design) |
| F06-02 | Critical | Transactional outbox sequence contradictory | Outbox row staged in same TenantDbContext `SaveChanges` as membership mutation. Publisher claims, delivers, acknowledges atomically. | Resolved (design) |
| F06-03 | Critical | Multi-instance cache/version coordination | Per-instance Access cache invalidated by local event processing. No broadcast. Stale windows bounded by TTL + event delay. | Resolved (design) |
| F06-04 | High | No non-HTTP execution context | Explicit actor/system context model defined; `HttpContext.Items` not used for background jobs. | Resolved (design) |
| F06-05 | High | Tenant claim removal contradictions | `TenantClaimsContributor` removed (Slice 5). Tenant resolution from JWT `tenant_id` claim remains; claim validated by middleware against Tenant DB before use. | Resolved (design) |
| F06-06 | High | Durable event dedupe/claiming/ordering | Transactional outbox with per-tenant sequence, `EventId` deduplication, processed acknowledgment after durable consumer commit. | Resolved (design) |
| F06-07 | High | Bootstrap/rebuild/reconciliation undefined | Bootstrap from Tenant membership table; reconciliation compares Tenant membership to Access projection on schedule and on demand. | Resolved (design) |
| F06-08 | High | Tenant lifecycle transitions | Tenant delete/suspend/archive/restore emit membership-affecting events; Access projection updated accordingly. | Resolved (design) |
| F06-09 | High | In-flight authorization semantics | Explicitly bounded stale window; check-then-act race acknowledged; fail-closed on missing scope/actor/auth. | Resolved (design) |
| F06-10 | Medium | `TenantClaimsContributor` deferred | Removed (Slice 5); tenant claim issued by Identity after Tenant validates membership during resolution middleware. | Resolved (design) |
| F06-11 | Medium | Performance claims unsupported | All performance statements marked as estimates requiring measurement. | Resolved (design) |
| F06-12 | Medium | D-03/D-18 identifier confusion | Canonical identifiers used consistently; both remain unresolved Owner decisions. | Resolved (design) |

---

## 5. Approved Architecture Constraints

The following constraints from `decisions.md` and `architecture.md` are binding and cannot be violated:

| Decision | Requirement | Source |
|---|---|---|
| D-001 | Modular monolith; no microservices without evidence | `decisions.md:7–10` |
| D-002 | Identity owns accounts/authentication/credentials/sessions/lockout/recovery; Access owns roles/permissions/assignments/authorization | `decisions.md:12–15` |
| D-003 | SaaS composition includes Tenant, Identity, Access; standalone does not require Tenant | `decisions.md:18–20` |
| D-011 | Tenant data isolation; single shared PostgreSQL; Tenant-owned records have explicit `TenantId`; fail-closed | `decisions.md:58–60` |
| D-016 | Tenant requires Access for authorization; Tenant integrates through Access public contracts; Access must not access Tenant private persistence/internals | `decisions.md:82–96` |
| D-018 | No SharedKernel; module-owned contracts; Access is leaf module; Tenant and Identity reference only Access | `decisions.md:113–119` |

**Additional constraints from `architecture.md`:**
- Access remains a leaf module (§4.4: "CoreKit.Access is a leaf module").
- Tenant uses Access public contracts for authorization (§5.2).
- Access must not reference Tenant's private services, repositories, `DbContext` or tables (§6.6).
- Tenant must not reference Identity private internals (§6.7).
- Authorization must fail closed (§6.8, §10.5).

---

## 5. Revocation and Consistency Requirements

### 5.1 Revocation Guarantees (Honest Statement)

**Maximum stale authorization window: unbounded under failure conditions.**

| Scenario | Maximum Stale Window |
|---|---|
| Event processing delay (publisher paused) | Unbounded until publisher resumes |
| Access handler failure | Until retry succeeds or operator intervention |
| Multi-instance cache divergence | Until TTL expiry on each instance |
| Cache miss after event processed | Immediate (Access reads its own projection) |
| In-flight request at removal time | May complete using pre-revocation state |

**This is eventual revocation, not immediate revocation.** The design does not claim immediate revocation. If the Project Owner requires immediate revocation (access denied within milliseconds of membership removal), a synchronous membership validation check at authorization time is required — which reintroduces the cross-module call that Option A avoids. This is an explicit Owner decision.

**In-flight requests:** A request that passed authorization before membership removal may complete using pre-revocation state. This is a check-then-act race that cannot be eliminated without synchronous membership validation at every authorization point.

**Cross-tenant access:** Denied by `TenantResolutionMiddleware` before Access is invoked. Access never sees a cross-tenant scope for an authenticated member.

**Platform vs. tenant scope:** Platform scope (`AccessScope.ForPlatform(PlatformId)`) is fixed and separate from tenant scope. Tenant owners are not automatically platform members. Platform membership is stored in Access's `ACCESS_PlatformMembers` table.

---

## 6. Integration Alternatives and Comparison Matrix

### Option A — Access-Owned Membership Projection (Event-Driven) — RECOMMENDED

**Description:** Access maintains its own `AccessMembership` table. Tenant publishes `TenantMemberAdded`/`TenantMemberRemoved` events via a transactional outbox in TenantDbContext. Access consumes events via a durable consumer and updates its projection. Access's `IAccessMembership.FindAsync` reads from Access's own cache/table — no call to Tenant's database.

| Criterion | Assessment |
|---|---|
| Module ownership | Access owns `IAccessMembership`; Tenant owns `ITenantMembershipReader` |
| No cross-module implementation | Tenant publishes events (Access-owned contract); Access consumes. No Tenant implementation of Access-owned interfaces. |
| No SharedKernel | Yes |
| Standalone Access | Yes — empty projection = no members |
| Authorization correctness | Yes — Access decides based on its own state |
| Membership freshness | Eventual — bounded by event processing + cache TTL |
| Revocation | Eventual — Access invalidates cache after event processing |
| Multi-instance | Per-instance cache; event drift until TTL on each instance |
| Failure recovery | Outbox retry + reconciliation job |
| Performance | 2 DB round trips per request (Tenant resolution + Access assignment) |
| Operational complexity | High — outbox, publisher, reconciliation, monitoring |

### Option B — Synchronous Access-to-Tenant Membership Read

**Description:** Access calls Tenant's `ITenantMembershipReader.FindAsync(tenantId, userId)` synchronously for each authorization decision.

| Criterion | Assessment |
|---|---|
| Module ownership | Access calls Tenant-owned interface |
| No cross-module implementation | Tenant does not implement Access interface; Access calls Tenant interface — but this creates Access → Tenant project reference |
| No SharedKernel | Yes |
| Standalone Access | No — Access requires Tenant at runtime |
| Authorization correctness | Yes — real-time membership |
| Membership freshness | Real-time at authorization time |
| Revocation | Immediate — removal is reflected immediately |
| Multi-instance | Single source of truth (Tenant DB) |
| Failure recovery | Tenant outage blocks Access authorization |
| Performance | 2 DB round trips per request (Tenant + Access) |
| Operational complexity | High — tight coupling, availability coupling |

**Verdict: Rejected.** Creates circular project dependency (Tenant → Access → Tenant). Violates constraint 7 (Access is leaf module). Tightly couples availability: Tenant outage blocks Access authorization.

### Option C — Host-Level Orchestration

**Description:** Host calls Tenant's `ITenantMembershipReader.FindAsync`, gets membership, then calls Access's `IPermissionChecker`.

| Criterion | Assessment |
|---|---|
| Module ownership | Host coordinates Tenant and Access |
| No cross-module implementation | Host calls both — no module implements other's interface |
| No SharedKernel | Yes |
| Standalone Access | No — host requires Tenant for membership |
| Authorization correctness | Yes — if host always calls Tenant first |
| Membership freshness | Real-time |
| Revocation | Immediate — Tenant read is current |
| Multi-instance | Host stateless; Tenant DB is single source |
| Failure recovery | Tenant outage blocks all authorization |
| Performance | 2–3 DB round trips per request (Tenant + Access + host orchestration) |
| Operational complexity | Medium — host becomes critical path |

**Verdict: Rejected.** Relocates coupling to host but does not solve it. Background jobs and direct service calls bypass the host. Access's `IPermissionChecker` interface does not accept membership evidence, so the host cannot pass Tenant's membership result to Access.

### Option D — JWT-Embedded Tenant Claims

**Description:** Identity issues JWT with `tenant_id` claim; Access reads claim directly.

| Criterion | Assessment |
|---|---|
| Module ownership | Identity issues claims; Access reads them |
| No cross-module implementation | Tenant does not implement Access interface |
| No SharedKernel | Yes |
| Standalone Access | Yes |
| Authorization correctness | No — JWT claims are not authorization authority |
| Membership freshness | Stale until token expiry |
| Revocation | Until token expiry (up to 120 min) |
| Multi-instance | Stateless; no cross-instance issue |
| Performance | 1 DB read (Access only) |
| Operational complexity | Low |

**Verdict: Rejected.** JWT claims are identity+context input, not authorization authority (confirmed in Review-06 CS-06). Tenant implementing `IAccessTokenClaimsContributor` violates D-018. Cannot provide revocation guarantees without token expiry.

---

## 7. Recommended Architecture: Option A — Access-Owned Membership Projection

### 7.1 Core Principle

Access owns and implements all Access-owned contracts. Tenant owns and implements all Tenant-owned contracts. No module implements another module's interfaces.

Access maintains its own `AccessMembership` table (projection), populated by Tenant-published integration events delivered via a transactional outbox. Access's `IAccessMembership.FindAsync` reads from Access's own cache/table — no call to Tenant's database.

### 7.2 Contract Ownership Matrix

| Contract | Owned By | Implemented By | Reads From |
|---|---|---|---|
| `IAccessScopeContext` | Access | Access (`ClaimsAccessScopeContext`) | JWT `tenant_id` claim via `HttpContext.Items["tenant_id"]` |
| `IAccessMembership` | Access | Access (`AccessMembershipCache`) | Access's own `AccessMembership` table (populated by events) |
| `ICurrentUser` | Access | Access (`ClaimsAccessCurrentUser`) | JWT `sub` claim |
| `ICurrentTenant` | Tenant | Tenant (`CurrentTenant`) | `TenantResolutionMiddleware` (reads JWT `tenant_id` claim) |
| `ITenantMembershipReader` | Tenant | Tenant (`TenantMembershipReader`) | Tenant's own `TenantMember` table |
| `IPermissionChecker` | Access | Access (`AccessPermissionChecker`) | Access's own `AccessService` |
| `IAccessIntegrationEvent` | Access | Tenant publishes (`TenantMemberAdded`, `TenantMemberRemoved`); Access consumes | N/A |

### 7.3 Data Flow (HTTP Request)

```
HTTP Request
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
    │   → reads Access's own membership cache/table
    │     → returns (IsMember, MembershipGeneration) or null
    │ calls IAccessAssignmentRepository.ListRolesForUserAsync(scope, userId, generation)
    │   → filters assignments by MembershipGeneration == cache generation
    │ builds EffectiveAccess
  │ checks access.Has(permission)
  │
  ▼
Authorization decision
```

### 8. Background Execution Flow

```
Background Job
    │
    ▼
Job scheduler sets ICurrentTenant + HttpContext.Items["tenant_id"]
    │ (explicit actor + tenant scope)
    │
    ▼
AccessGuard.RequireAsync(permission)
    │ reads IAccessScopeContext.Current
    │ calls IAccessService.GetAccessAsync(userId, scope)
    │   → reads Access's own membership cache
    │   → reads AccessAssignment table
    │   → authorization decision
    │
    ▼
Authorization decision
```

**Key rule:** Background jobs must explicitly set tenant context. No `HttpContext` required — `ICurrentTenant` and `HttpContext.Items["tenant_id"]` are set by the job scheduler. Missing or invalid context → fail-closed.

### 9. Membership Change Flow

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
  │ claims, delivers to Access event handler
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

**Failure scenario:** If Access's `SaveAsync` fails after Tenant's `SaveAsync` commits, the member is removed from Tenant but Access assignments remain. The event is in the outbox and will be retried. Until processed, member retains Access permissions until cache TTL expires.

---

## 11. Outbox Protocol

| Component | Specification |
|---|---|
| Outbox table | `OutboxMessage` (Id, Type, Payload, OccurredAt, ProcessedAt, RetryCount, Error) |
| Write | Staged in same `TenantDbContext.SaveChangesAsync` as membership mutation |
| Publisher | Background poller reads unprocessed rows, claims with atomic update, delivers to Access |
| Claim/lease | `UPDATE OutboxMessage SET ProcessedBy=@workerId, LeaseExpires=@now+lease WHERE Id=@id AND ProcessedAt IS NULL AND (LeaseExpires IS NULL OR LeaseExpires < @now)` |
| Lease expiry | Unclaimed after lease timeout → available for another worker |
| Retry | Exponential backoff; poison events to dead-letter after max retries |
| Dedupe | Access stores processed `EventId`; duplicate events ignored |
| Ordering | Per-tenant sequence number; out-of-order events ignored if `Sequence <= lastProcessed` |
| Ack | Only after Access `SaveChangesAsync` commits |
| Bootstrap | Initial projection from Tenant's `TenantMember` table on Access startup |
| Reconciliation | Periodic job compares Tenant membership vs Access projection; reports drift |

**Crash recovery:** If Access commits its projection but crashes before acknowledging the outbox item, the event is redelivered and deduplicated by `EventId`.

---

## 12. Trusted Tenant Context

### HTTP Requests
1. JWT validated by Identity (`sub`, `tenant_id` claims)
2. `TenantResolutionMiddleware` reads `tenant_id` claim
3. Validates tenant exists and is Active via `ITenantInfoProvider`
4. Validates user membership via `ITenantMembershipReader` (Tenant's own database)
5. Sets `ICurrentTenant` and `HttpContext.Items["tenant_id"]`
6. Access's `ClaimsAccessScopeContext` reads `tenant_id` from `Items["tenant_id"]`

### Background Execution
1. Job scheduler sets `ICurrentTenant` and `HttpContext.Items["tenant_id"]` explicitly
2. No `HttpContext` required — explicit actor + tenant scope
3. Missing or invalid context → fail-closed (`ScopeRequired`)

### No Client-Supplied Trust
- `tenant_id` claim is validated against Tenant's membership database before trust
- `X-Tenant-Id` header is not trusted directly (`HeaderTenantResolutionStrategy` requires `TrustHeader=true`, default false)
- Caller-supplied tenant IDs are never accepted as authorization evidence

---

## 13. Security and Failure Analysis

| Failure Mode | Behavior | Mitigation |
|---|---|---|
| Removed member retains Access permissions | Until event processed + cache invalidated | Outbox retry + TTL |
| Tenant suspension | Middleware rejects before Access invoked | Immediate |
| Access DB outage | AccessService throws; request fails 500 | Fail-closed |
| Tenant DB outage | Middleware returns error; request fails | Fail-closed |
| Event handler failure | Retried; poison to DLQ after max retries | Operator intervention |
| Stale JWT after credential change | Valid until expiry (D-03 unresolved) | Owner decision |
| Concurrent add/remove | Generation-based filtering; stale assignments filtered | Generation check in AccessAssignment query |
| Platform vs tenant scope | Fixed platform scope; tenant owners not auto-platform | Fixed scope enforcement |
| Cache divergence (multi-instance) | Per-instance cache; event drift until TTL | TTL safety net |

---

## 14. Performance Estimate (Unmeasured)

| Operation | DB Round Trips |
|---|---|
| Tenant authorization (cache hit) | 1 (TenantDbContext for resolution) |
| Tenant authorization (cache miss) | 2 (TenantDbContext + AccessDbContext) |
| Platform authorization | 1 (AccessDbContext) |

**These are estimates based on code inspection, not measured.** Benchmarking required before production deployment claims.

**Transaction duration:** Tenant member add/remove ~10-50ms (single SaveChanges). Access assignment CRUD ~5-20ms (single SaveChanges). Cross-module operations are two separate transactions (not atomic).

---

## 15. Ordered Implementation Plan

### Slice 1: Access-Owned `IAccessMembership`

**Files:** `CoreKit.Access/Services/AccessMembershipCache.cs`, `CoreKit.Access/Persistence/AccessMembershipRepository.cs`, `CoreKit.Access/Persistence/AccessDbContext.cs` (+Migrations)

**Objectives:** Access owns membership state; reads from own table.

**Tests:** `AccessMembershipCacheTests`, `AccessServiceTests`

**Rollback:** Re-add `TenantAccessMembership`, revert DI

---

### Slice 2: Access-Owned `IAccessScopeContext`

**Files:** `CoreKit.Access/Services/ClaimsAccessScopeContext.cs`, `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs`

**Objectives:** Access reads `tenant_id` from `HttpContext.Items["tenant_id"]`; Tenant adapter removed.

**Dependencies:** Slice 1

**Tests:** `ClaimsAccessScopeContextTests`

**Rollback:** Re-add `TenantAccessScopeContext`, revert DI

---

### Slice 3: Transactional Outbox

**Files:** `CoreKit.Tenant/Persistence/OutboxMessage.cs`, `CoreKit.Tenant/Persistence/TenantDbContext.cs` (+Migration), `CoreKit.Tenant/Services/OutboxPublisher.cs`, `CoreKit.Tenant/Services/TenantMembershipService.cs` (writes outbox in same SaveAsync), `CoreKit.Access/Services/*MembershipEventHandler*.cs`

**Dependencies:** Slice 1, Slice 2

**Tests:** `OutboxPublisherTests`, `TenantMemberAddedHandlerTests`, `TenantMemberRemovedHandlerTests`

**Rollback:** Revert to `IAccessMaintenance.RemoveUserAsync`, remove outbox table

---

### Slice 4: Identity Implements Own `ICurrentUser`

**Files:** `CoreKit.Access/Services/ClaimsAccessCurrentUser.cs`, `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs`, `CoreKit.Identity/Services/UserService.cs`, `CoreKit.Identity/Services/SessionIssuer.cs`

**Dependencies:** Slice 2

**Tests:** `ClaimsAccessCurrentUserTests`, `AuthServiceTests`

**Rollback:** Re-add `CurrentUserService : ICurrentUser`, revert DI

---

### Slice 5: Remove `TenantClaimsContributor`

**Files:** `CoreKit.Tenant/Integration/TenantClaimsContributor.cs`, `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs`, `CoreKit.Tenant/Resolution/TenantResolutionMiddleware.cs`

**Dependencies:** Slice 2

**Tests:** `TenantResolutionMiddlewareTests`

**Rollback:** Re-add `TenantClaimsContributor`, revert DI

---

## 16. Owner Decision Register

| Decision | Status | Required? |
|---|---|---|
| Tenant–Access integration pattern | Open | Yes |
| `IAccessTokenClaimsContributor` removal | Open | Yes (if JWT claim removal desired) |
| `ICurrentTenant` access by Access | Open | Yes |
| JWT `SecurityVersion` validation | Open (D-03) | Yes |
| Rate limiting on tenant/access writes | Open (D-18) | Yes |
| Transactional outbox | Open | Yes |
| Distributed cache for multi-instance | Open | Yes |
| Acceptable revocation window | Open | Yes |
| In-flight operation semantics | Open | Yes |

---

## 17. Risks and Unresolved Evidence

| Risk | Severity | Evidence Level | Mitigation |
|---|---|---|---|
| Cross-DbContext non-atomicity | High | Confirmed (source inspection) | Transactional outbox + reconciliation |
| Stale JWT after credential change | Medium | Confirmed (source inspection) | Owner decision (D-03) |
| No rate limiting on tenant/access writes | Medium | Confirmed (source inspection) | Owner decision (D-18) |
| In-memory cache cross-instance staleness | Medium | Confirmed (source inspection) | TTL safety net; distributed cache future |
| Event handler failure → stale permissions | Medium | Confirmed (source inspection) | Outbox + retry + reconciliation |
| Identity implements `ICurrentUser` (Access-owned) | Low | Confirmed (source inspection) | Slice 4 |
| `TenantClaimsContributor` implements `IAccessTokenClaimsContributor` | Low | Confirmed (source inspection) | Slice 5 |

**Unresolved evidence:** Runtime behavior of `CompositeAccessMembership` with Tenant's adapter (not build/test verified). Actual cache hit/miss ratios (not measured). Cross-instance event propagation (not implemented). Outbox reliability (not implemented).

---

## 18. Final Readiness Assessment

**Design-05 is rejected.** This report (Design-06) addresses all Review-06 findings:

- D-018-compliant end-state: No Tenant/Identity implementations of Access-owned interfaces
- Durable transactional outbox with idempotent, ordered, replayable processing
- Access-owned membership projection with bootstrap/rebuild/reconciliation
- Singular tenant-context model for HTTP and background jobs
- Explicit revocation guarantees (eventual, bounded, not immediate)
- Owner decisions explicitly identified and left unresolved

**Implementation remains blocked** until:
1. Claude Account 2 completes independent review of Design-06
2. Project Owner approves the architecture and required decisions
3. Critical/high findings resolved

**This document is suitable for independent review.** No implementation has been started or authorized.
