# DK-TENANT-ACCESS-INTEGRATION-REVIEW-06

## Independent Architectural Review --- Design-05

**Repository:** `waqas-dba/Dastarkhwan`\
**Latest documentation commit:**
`ba74a4d0d9f98771ef504b94a734ea3ed938dd9c`\
**Source baseline:** `73932a6125a89f42553732c313a6dda496a42656`\
**Verdict:** **Reject --- not implementation-ready**\
**Review mode:** Remote-only, read-only static inspection

## 1. Baseline verification

Remote `master` HEAD is `ba74a4d0d9f98771ef504b94a734ea3ed938dd9c`. Its
parent is exactly `73932a6125a89f42553732c313a6dda496a42656`.

Recursive tree comparison shows only five changed Markdown reports: -
`DK-TENANT-ACCESS-INTEGRATION-DESIGN-03-REPORT.md` -
`DK-TENANT-ACCESS-INTEGRATION-DESIGN-04-REPORT.md` -
`DK-TENANT-ACCESS-INTEGRATION-DESIGN-05-REPORT.md` -
`DK-TENANT-ACCESS-INTEGRATION-REVIEW-04-REPORT.md` -
`DK-TENANT-ACCESS-INTEGRATION-REVIEW-05-REPORT.md`

No source path changed. The claimed source baseline is confirmed. This
was remote GitHub verification only; local HEAD, branch and working-tree
status are not claimed.

Inspected documents: `CLAUDE.md`, `roadmap.md`, `current-state.md`,
`decisions.md`, `architecture.md`, `ai-tasks.md`, Design-05 and
Review-05.

## 2. Source evidence inventory

Pinned to `73932a6125a89f42553732c313a6dda496a42656`:

**Access:** `Abstractions/IAccessIntegrationEvent.cs`, `AccessPorts.cs`,
`CompositeAccessMembership.cs`, `PlatformAccessMembership.cs`,
`IAccessMaintenance.cs`; `Services/AccessService.cs`, `AccessCache.cs`,
`AccessGuard.cs`, `DefaultPorts.cs`, `AccessMaintenance.cs`,
`AccessEventHandlers.cs`; `Persistence/AccessDbContext.cs`;
`Extensions/AccessServiceCollectionExtensions.cs`.

**Tenant:** `Persistence/TenantDbContext.cs`, `TenantUnitOfWork.cs`,
`TenantMemberRepository.cs`; `Entities/TenantMember.cs`;
`Services/TenantMembershipService.cs`, `TenantMembershipReader.cs`,
`TenantEventPublisher.cs`, `TenantService.cs`;
`Events/IntegrationEvents.cs`; `Abstractions/TenantEvents.cs`;
`Resolution/TenantResolutionMiddleware.cs`,
`ClaimTenantResolutionStrategy.cs`, `CurrentTenant.cs`;
`Integration/TenantAccessMembership.cs`, `TenantAccessScopeContext.cs`,
`TenantClaimsContributor.cs`,
`TenantMemberRemovedToAccessMaintenance.cs`;
`Extensions/TenantServiceCollectionExtensions.cs`.

**Identity/composition:** `Services/ICurrentUserService.cs`,
`CurrentUserService.cs`, `Extensions/ServiceCollectionExtensions.cs`;
`CoreKit.SampleHost/Program.cs`.

The source tree has no `OutboxMessage`, `OutboxPublisher`, Access
`AccessMembership` entity/table, `AccessMembershipCache`,
`ClaimsAccessScopeContext`, or `ClaimsAccessCurrentUser` implementation.
These remain proposed artifacts, not verified code.

## 3. Executive verdict

**Reject --- not implementation-ready.**

Design-05 improves the architecture direction by proposing an
Access-owned membership projection and removing most Tenant/Identity
implementations of Access-owned interfaces. It does not prove its
central revocation claim, define an executable outbox protocol, or
provide a valid non-HTTP actor/context model.

## 4. D-018 ownership and dependency review

`decisions.md` lines 112--119 accepts D-018: no SharedKernel,
module-owned contracts, Tenant and Identity reference Access, and Access
remains a leaf. D-002, D-003 and D-011 also remain binding.

The proposed target is directionally compliant: - Tenant owns canonical
membership. - Access owns authorization and its own membership
projection. - Identity owns authentication and tokens. - Access need not
reference Tenant or Identity. - No SharedKernel, SampleHost requirement,
broker or microservice is necessary.

But Design-05 is internally inconsistent: - Slice 5 says
`TenantClaimsContributor` removal may be deferred, while §3/§5 claims no
transitional exceptions and complete D-018 compliance. - The main
request flow still requires the `tenant_id` JWT claim, while Slice 5
removes it and does not choose a replacement. -
`ClaimsAccessScopeContext` is HTTP-specific and does not solve
background execution. - `TenantActor` continues consuming Access
`ICurrentUser`; this is consumption, not an implementation violation,
but its minimal contract should be explicit.

## 5. Review-05 finding resolution matrix

  ----------------------------------------------------------------------------------
  Review-05 finding       Design-05 assessment               Status
  ----------------------- ---------------------------------- -----------------------
  F-01 / RC-01: D-018     Most target adapters removed;      Partially resolved
  adopter violations      claim-contributor deferral and     
                          contradictory flow remain          

  F-02 / RC-02: no        Outbox proposed, but sequence      Partially resolved
  durable atomic outbox   contradicts same-transaction       
                          requirement; publisher protocol    
                          incomplete                         

  F-03 / RC-03:           Basic table proposed; no           Partially resolved
  projection/bootstrap    deterministic                      
  undefined               bootstrap/rebuild/reconciliation   
                          protocol                           

  F-04 / RC-04: event     EventId/Sequence/Generation named; Not resolved
  ordering/idempotency    persisted dedupe, sequence         
                          allocation and gap policy absent   

  F-05 / RC-06:           Claimed formula is not enforceable Not resolved
  revocation unbounded                                       

  F-06 / RC-05: no        `HttpContext.Items` remains        Not resolved
  universal tenant        central; job design requires       
  context                 nonexistent HTTP context           

  F-07:                   Claim flow remains while removal   Not resolved
  claim/header/Items      is deferred and replacement        
  contradictions          unspecified                        

  F-08: client selector   Signed claim is validated, but     Partially resolved
  trust                   cached membership and switching    
                          policy unresolved                  

  F-09: unsupported       Some estimates labelled            Partially resolved
  performance claims      unmeasured; cache/round-trip       
                          assertions still conflict          

  F-10: insufficient      Critical multi-instance, replay,   Not resolved
  tests/operations        poison, restart and race tests are 
                          not mandatory acceptance criteria  

  F-11: event contract    Exact event ownership,             Partially resolved
  drift                   serialization and consumer         
                          protocol not fully specified       
  ----------------------------------------------------------------------------------

## 6. Current-source defects and design/source discrepancies

1.  `TenantMembershipService.cs:200–204`: removal is saved, then local
    membership cache invalidated, then event published. Addition
    similarly saves before publishing (`140–145`).
2.  `TenantUnitOfWork.cs:36–55`: `SaveAsync` completes
    `TenantDbContext.SaveChangesAsync` before returning.
3.  `TenantDbContext.cs:15–21`: no outbox DbSet exists.
4.  `TenantEventPublisher.cs:24–44, 54–64`: events are dispatched
    in-process; handler exceptions are logged and swallowed.
5.  `IntegrationEvents.cs:3–17` explicitly documents post-save
    in-process delivery and that handler failure does not undo the
    change.
6.  `AccessDbContext.cs:10–18`: no membership projection DbSet exists;
    `AccessEventHandlers.cs:1–8` has no handler implementation.
7.  `AccessService.cs:41–59`: a cached positive `EffectiveAccess` is
    returned before membership is resolved at line 72.
8.  `AccessCache.cs:13, 192–200`: cache token/key includes only
    process-local epoch, scope version and user version; no Tenant
    generation or durable projection version.
9.  `TenantMembershipReader.cs:27–44`: Tenant membership uses
    process-local `IMemoryCache`.
10. `TenantResolutionMiddleware.cs:80–98`: HTTP membership validation
    uses that cached reader.
11. `CurrentTenant.cs:4–26`: `Set` assigns only a scoped `_id`; it does
    not populate `HttpContext.Items`.
12. `ClaimTenantResolutionStrategy.cs:15–28`: current tenant selector
    comes from the JWT claim. `TenantResolutionMiddleware.cs:32–69`
    rejects conflicting resolved tenant identifiers.
13. `TenantClaimsContributor.cs:13–29` and
    `TenantServiceCollectionExtensions.cs:99–115` register the
    default-tenant claim path and forbidden adapters in the current
    source.
14. `TenantService.DeleteAsync` removes the tenant and cascades members
    (`TenantService.cs:249–275`), then emits `TenantDeletedEvent`;
    Design-05's membership event plan does not define projection/role
    cleanup for tenant deletion or status transitions.

Design-05 §8.3 says the Tenant save commits and then
`TenantEventPublisher.PublishAsync` writes an outbox row "in the same
TenantDbContext transaction". That is impossible in that order. Slice 3
later correctly says the outbox row must be staged before the same
`SaveAsync`. The main sequence must be corrected.

## 7. Outbox and event reliability

A Tenant outbox is viable in a modular monolith without a broker if the
outbox row is staged in the same TenantDbContext unit of work as the
membership mutation.

The proposal is not yet executable: - Outbox schema omits claim/lease
owner, lease expiry, next-attempt time, terminal state, aggregate key
and event schema version. - Multiple publishers are mentioned, but no
atomic claim/lease/locking algorithm is specified. - A per-tenant
single-threaded publisher is asserted without a cross-instance mechanism
enforcing it. - `EventId` is named, but Access has no specified durable
inbox/deduplication record. The proposed projection schema has no last
event ID or sequence. - Sequence allocation under concurrent membership
mutations, sequence gaps, poison-event replay and permanent failure
behavior are undefined. - Crash after Access commits but before Tenant
acknowledges the outbox item is not addressed. - Bootstrap/rebuild and
reconciliation are named, not specified. - Tenant deletion, suspension,
archive and restore transitions are omitted from the projection
protocol.

## 8. Revocation-bound proof or rejection

Design-05 §9.1 claims:

`maximum stale authorization window = max(eventProcessingTime, accessCacheTTL)`

**Rejected.** No hard maximum is imposed on event-processing time. A
stopped publisher, persistent database outage or poison event can delay
projection updates indefinitely.

TTL does not repair a stale projection. If the outbox event has not
updated Access's durable membership row, expiry of the permission cache
causes Access to read the still-active projection and cache the stale
positive result again. The stale allow can therefore persist
indefinitely.

Even after a projection update, a competitive consumer processes the
event once; it does not broadcast cache invalidation to every process. A
local cache refreshed just before the projection update can remain stale
for almost another full TTL. In that case, event delay and residual
cache lifetime are additive, not the maximum of the two.

The design also acknowledges that an in-flight request may continue
after membership removal. No cancellation, commit-boundary fence, lease
or operation serialization is specified.

**Conclusion:** stale authorization is unbounded under the stated
failure model. A bounded eventual-consistency claim needs an enforced
delivery deadline and fail-closed mechanism independent of eventual
event arrival. Immediate revocation needs synchronous fencing or a
strongly consistent authoritative check; strict in-flight completion
prevention requires additional synchronization.

## 9. Multi-instance cache analysis

Design-05 says both "events are delivered to all instances" and that a
shared outbox uses competitive consumption so each event is processed
once. These are different delivery models.

A competitive consumer gives one worker the event; it does not fan out
invalidation to every instance. If one worker updates a shared Access
projection, other instances observe it only when they read the database.
Their local `AccessCache` entries are not invalidated by that database
write.

Possible designs include: - durable membership/version read before any
positive permission-cache hit; - per-instance fan-out subscriptions with
durable per-instance cursors; - shared cache/version coordination with
explicit consistency guarantees; - an explicitly accepted
bounded-staleness model with enforceable failure behavior.

No option is fully specified. Redis pub/sub alone is not a durable
guarantee because offline subscribers can miss messages.

## 10. HTTP and background tenant context

A JWT `tenant_id` claim may be a selector, never proof of current
membership. Middleware currently validates membership, but uses the
cached `TenantMembershipReader`.

A user with multiple tenants receives a default tenant claim. Middleware
rejects conflicting identifiers from multiple strategies
(`TenantResolutionMiddleware.cs:32–69`), so a header selecting another
tenant may conflict with the claim. Design-05 does not choose and
sequence a replacement selector before deferring claim removal.

`HttpContext.Items` is safe only if set by trusted middleware and not
replaceable by later code. A typed immutable request scope and tenant-ID
consistency invariant are missing.

A background job has no `HttpContext`. Do not require a scheduler to
populate `HttpContext.Items`. Define an explicit execution context with
a trusted user actor or registered system actor, explicit tenant scope,
authoritative validation, authorization policy, cancellation and failure
behavior. A caller-supplied tenant ID alone is not trusted context.

## 11. Security findings

-   Preserve Tenant-owner ≠ Access-owner. Current
    `TenantAccessMembership` returns
    `new AccessMember(false, generation)`
    (`TenantAccessMembership.cs:21–25`); the projection must not turn
    Tenant `IsOwner` into Access superuser status.
-   Preserve fixed platform scope.
    `AccessGuard.RequirePlatformScopePermissionAsync` uses
    `AccessScope.ForPlatform` (`AccessGuard.cs:57–80`).
-   Assignment cleanup must be generation-safe. Authorization must deny
    based on membership state; asynchronous deletion is cleanup, not the
    revocation gate.
-   `IAccessMaintenance` is trusted-only and has no permission checks
    (`IAccessMaintenance.cs:3–7`). Any proposed projection writer needs
    equivalent restricted registration and must not be exposed as an
    ordinary endpoint.
-   Check-to-use race remains: a protected operation may commit after an
    earlier allow decision.
-   D-03 JWT `SecurityVersion` validation remains unresolved. JWT
    validation currently checks issuer, audience, signing key and
    lifetime
    (`Identity/Extensions/ServiceCollectionExtensions.cs:128–140`), not
    current `SecurityVersion`.
-   D-18 write rate limiting remains unresolved. Do not decide it in
    this review.

## 12. Required verification and operations

Mandatory tests should cover: - membership/outbox same-transaction
commit and rollback; - crash after Tenant commit, after Access
projection commit and before outbox acknowledgement; - concurrent
publisher claims across two hosts; - duplicate, delayed, reordered,
missing and poison events; - sequence gaps and add/remove/rejoin
generations; - bootstrap racing with live events and full rebuild; -
tenant delete/suspend/archive/restore; - stale local cache on instance B
while instance A consumes the event; - Tenant membership cache stale
after removal; - Access/Tenant DB outages and outbox backlog; - tenant
switching and legacy JWT claim behavior; - background user/system
actors, missing actor and forged tenant ID; - in-flight request versus
revocation; - cross-tenant, platform/tenant and Tenant-owner negative
tests; - D-018 architecture checks.

Operational metrics/alerts: outbox age and lag, retry and dead-letter
counts, oldest pending revocation, projection generation lag,
reconciliation drift, rebuild failures, per-instance cache/version
drift, Access query latency and fail-closed denials. Runbooks must cover
replay, poison events, rebuild and stuck revocation recovery.

No tests or builds were run.

## 13. Prioritized findings

  ---------------------------------------------------------------------------------------------------------------------------
  ID             Severity       Confidence     Finding                            Evidence / impact
  -------------- -------------- -------------- ---------------------------------- -------------------------------------------
  F06-01         Critical       High           Revocation formula invalid; stale  Design-05 §9.1; `AccessService.cs:41–59`;
                                               projection can be recached         `AccessCache.cs:117–134`
                                               indefinitely                       

  F06-02         Critical       High           Outbox transaction sequence        Design-05 §8.3/Slice 3;
                                               contradictory                      `TenantMembershipService.cs:200–204`;
                                                                                  `TenantUnitOfWork.cs:36–55`

  F06-03         Critical       High           Competitive consumer does not fan  Design-05 §6/§9--10; `AccessCache.cs:41–67`
                                               out process-local cache            
                                               invalidation                       

  F06-04         High           High           Background context requires        Design-05 §7.3--8.1;
                                               nonexistent `HttpContext`          `CurrentTenant.cs:4–26`;
                                                                                  `DefaultPorts.cs:7–40`

  F06-05         High           High           Tenant claim removal and switching Design-05 Slice 5/§7.3;
                                               policy contradictory               `TenantClaimsContributor.cs:13–29`;
                                                                                  `ClaimTenantResolutionStrategy.cs:15–28`;
                                                                                  middleware `32–69`

  F06-06         High           High           Event protocol lacks durable       Design-05 §10.2/Slice 3;
                                               dedupe, safe claiming, ordering    `TenantEventPublisher.cs:24–64`
                                               and recovery details               

  F06-07         High           High           Projection                         Design-05 §3/§10.1;
                                               bootstrap/rebuild/reconciliation   `AccessDbContext.cs:10–18`
                                               undefined                          

  F06-08         High           High           Tenant deletion/status lifecycle   `TenantService.cs:249–275`;
                                               omitted from projection protocol   `TenantEventPublisher.cs:30–44`

  F06-09         High           High           In-flight check-to-use race is not Design-05 §9.2
                                               fenced                             

  F06-10         Medium         High           Prohibited claim-contributor       Design-05 Slice 5 and §14
                                               implementation may be deferred     

  F06-11         Medium         High           Cache-hit and database round-trip  Design-05 §11; `AccessService.cs:46–53`
                                               estimates conflict                 

  F06-12         Medium         High           D-03/D-18 identifiers ambiguous    `decisions.md`
                                               against D-003/D-018                
  ---------------------------------------------------------------------------------------------------------------------------

## 14. Required corrections

1.  Replace the stale-window formula with an explicit consistency model.
    If event delay is not bounded and enforced, state that stale
    authorization is unbounded under publisher/projection failure.
2.  Require current membership/version state before reusing positive
    permission-cache entries, or provide an equivalent authoritative
    revocation fence.
3.  Select a multi-instance invalidation/version design. Do not claim
    competitive single-consumer delivery is broadcast.
4.  Correct the outbox sequence: stage outbox row before the same Tenant
    `SaveChanges`.
5.  Specify atomic claim/lease, retry/backoff, poison handling,
    persisted idempotency, acknowledgement order, replay and
    reconciliation.
6.  Define per-member generation and event ordering under concurrent
    add/remove/rejoin.
7.  Specify deterministic bootstrap/rebuild coordinated with live
    events.
8.  Include tenant delete, suspend, archive and restore.
9.  Define explicit non-HTTP actor/scope context. Do not simulate
    `HttpContext` for jobs.
10. Choose one tenant-selection policy and make claim removal
    non-deferrable if required for D-018.
11. Specify in-flight operation semantics; do not claim strict
    revocation without synchronization.
12. Expand test and operational acceptance criteria.
13. Keep D-03 and D-18 unresolved and clarify their canonical
    identifiers.

## 15. Owner decision register

  -----------------------------------------------------------------------
  Item                                Status
  ----------------------------------- -----------------------------------
  D-018 no SharedKernel/module-owned  Accepted and binding
  contracts                           

  D-002 Identity/Access ownership     Accepted and binding

  D-003 SaaS/standalone composition   Accepted and binding

  D-011 tenant isolation/fail-closed  Accepted and binding
  context                             

  D-03 JWT SecurityVersion/revocation Unresolved
  policy                              

  D-18 Tenant/Access write rate       Unresolved
  limiting                            

  Acceptable revocation guarantee and Owner decision required
  in-flight behavior                  

  Whether Access outage blocks        Owner decision required
  membership removal                  

  Multi-instance consistency target   Owner decision required

  Background system-principal policy  Owner decision required

  Tenant switching and legacy JWT     Owner decision required
  transition                          
  -----------------------------------------------------------------------

`decisions.md` has accepted D-018 and D-003 entries; it does not provide
unambiguous standalone entries named D-03 and D-18 in the same sense
used by the report. Preserve these as unresolved references and clarify
identifiers. Do not conflate D-18 rate limiting with D-018 no
SharedKernel.

## 16. Final recommendation

**Verdict: Reject --- not implementation-ready.**

The Access-owned projection may remain a viable direction, but Design-05
does not establish bounded eventual consistency, multi-instance cache
correctness, an executable outbox protocol or trusted background
execution. Produce Design-06 addressing F06-01 through F06-12 and submit
it for another independent review. Implementation remains blocked
pending Project Owner approval.

## 17. Read-only confirmation

-   Remote `master` HEAD verified at
    `ba74a4d0d9f98771ef504b94a734ea3ed938dd9c`.
-   Parent/source baseline verified as
    `73932a6125a89f42553732c313a6dda496a42656`.
-   Recursive tree comparison found report-only changes; no source
    changes.
-   No local HEAD or working-tree status is claimed.
-   No repository files, source, documentation, tests or migrations were
    modified.
-   No builds, tests, migrations or database operations were run.
-   No Git write operations were performed.
-   No architecture decision or implementation approval was granted.
