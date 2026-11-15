# DK-TENANT-ACCESS-INTEGRATION-REVIEW-04 — Independent Architectural Review (Read-Only)

**Repository:** `waqas-dba/Dastarkhwan`  
**Baseline (required / verified):** `73932a6125a89f42553732c313a6dda496a42656` (2026-11-15 16:14:29 +0500, `docs(governance): reconcile architecture decisions and roadmap with baseline c9dbbe12`)  
**Branch:** `master`  
**Remote HEAD (`origin/master`):** `73932a6125a89f42553732c313a6dda496a42656` — matches local HEAD  
**Working tree at inspection:** `?? DK-TENANT-ACCESS-INTEGRATION-DESIGN-03-REPORT.md` (untracked design artifact under review; no tracked modifications observed)  
**Task type:** Independent architectural review — **read-only**  
**Reviewer:** Kilo (independent read-only reviewer)  
**Inputs reviewed:** `DK-TENANT-ACCESS-INTEGRATION-DESIGN-03-REPORT.md`, `decisions.md` (D-018, D-002, D-016, D-003, D-017), `architecture.md` (§4, §6, §7), relevant source in `CoreKit.Access`, `CoreKit.Tenant`, `CoreKit.Identity`

**Actions not performed (per task):** no source/docs modifications, no builds, no tests, no migrations, no database operations, no Git writes/commits/pushes.

---

## 1. Executive Conclusion

**Conclusion: Reject** (as a complete, implementation-ready target design).

`DK-TENANT-ACCESS-INTEGRATION-DESIGN-03` correctly identifies the core current-state violations and the main integrity risks (cross-module adapter pattern vs D-018, cross-DbContext non-atomicity on member removal, stale authorization windows, and in-process/non-durable event delivery). However, the **recommended Option E variant is not yet fully compliant with D-018 / stated module-boundary constraints**, leaves **event durability, ordering, replay/reconciliation, and revocation guarantees underspecified or unsafe for multi-step consistency**, and contains **contract/source mismatches and optimistic performance/consistency claims** that must be corrected before Owner approval or implementation.

**Disposition summary**
- **Option E direction (Access-owned membership state/cache populated from Tenant-published membership facts):** architecturally promising and closer to D-018 than the current adapter baseline.
- **Option E as written in DESIGN-03:** **not acceptable** without corrections (boundary violations/transitional exceptions, missing outbox/idempotency/reconciliation semantics, ambiguous integration-event ownership, and incomplete failure/test model).

---

## 2. Scope, Evidence, and Method

### 2.1 Inspected evidence (primary)
- Design: `DK-TENANT-ACCESS-INTEGRATION-DESIGN-03-REPORT.md` (baseline, inventory, alternatives, contracts, cache/consistency, failure, perf, migration slices, owner decisions)
- Governance: `decisions.md` (esp. D-002, D-015/D-016 supersession notes, D-017, **D-018**), `architecture.md` (§4 Identity/Access boundary, §6 Tenant–Access integration, §7 composition)
- Access: `Abstractions/AccessPorts.cs`, `IAccessIntegrationEvent.cs`, `IAccessTokenClaimsContributor.cs`, `CompositeAccessMembership.cs`, `PlatformAccessMembership.cs`, `IAccessService.cs`, `IAccessMaintenance.cs`, `ICurrentUser.cs`, `IAccessCurrentUser.cs`; `Services/AccessService.cs`, `AccessGuard.cs`, `AccessCache.cs`, `AccessMaintenance.cs`, `AccessPermissionChecker.cs`, `AccessEventHandlers.cs`, `DefaultPorts.cs`; `Persistence/AccessDbContext.cs`, `AccessUnitOfWork.cs`, `AccessAssignmentRepository.cs`; `Extensions/AccessServiceCollectionExtensions.cs`
- Tenant: `Integration/TenantAccessScopeContext.cs`, `TenantAccessMembership.cs`, `TenantClaimsContributor.cs`, `TenantMemberRemovedToAccessMaintenance.cs`; `Services/TenantMembershipService.cs`, `TenantMembershipReader.cs`, `TenantEventPublisher.cs`; `Persistence/TenantUnitOfWork.cs`; `Resolution/TenantResolutionMiddleware.cs`; `Events/IntegrationEvents.cs` (+ `TenantMemberRemovedEvent` dispatch); `Extensions/TenantServiceCollectionExtensions.cs`; `Abstractions/ITenantMembershipReader.cs`
- Identity: `Services/SessionIssuer.cs`, `TokenService.cs`, `ICurrentUserService.cs`/`CurrentUserService.cs` (by inventory), `Extensions/ServiceCollectionExtensions.cs`

### 2.2 Confidence labels
- **Confirmed:** directly evidenced in inspected source/design/governance text at baseline.
- **Assumption / not evidenced:** plausible from design intent but not proven by inspected implementation or missing required mechanism.

---

## 3. Current-State Findings Relevant to Option E (Confirmed)

| ID | Finding | Evidence | Impact |
|---|---|---|---|
| CS-01 | Tenant implements Access-owned interfaces (`IAccessScopeContext`, `IAccessMembershipAdapter`, `IAccessTokenClaimsContributor`) and DI overrides Access defaults | `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs:10`, `TenantAccessMembership.cs:10`, `TenantClaimsContributor.cs:13`; `TenantServiceCollectionExtensions.cs:109-110` (+ contributor registration); `AccessServiceCollectionExtensions.cs` registers composite/default adapters | Violates D-018 prohibition on cross-module adopter/interface implementation (Tenant/Identity implementing Access-owned interfaces) and inverts ownership intent |
| CS-02 | Access runtime depends on Tenant-owned membership read path for tenant authorization | `AccessService.ResolveAsync` → `_membership.FindAsync` (`AccessService.cs:72`); `CompositeAccessMembership` fans out to adapters (`CompositeAccessMembership.cs:19-29`); `TenantAccessMembership.FindAsync` → `ITenantMembershipReader.FindAsync` (`TenantAccessMembership.cs:17-25`); middleware also validates membership via `ITenantMembershipReader` (`TenantResolutionMiddleware.cs`) | Access is not runtime-leaf for tenant scope despite leaf project references; coupling and failure propagation across modules/DbContexts |
| CS-03 | Cross-DbContext non-atomic member removal / Access cleanup | `TenantMembershipService.RemoveMemberAsync` saves Tenant (`TenantUnitOfWork.SaveAsync`) then publishes event (`TenantMemberRemovedEvent`) (`TenantMembershipService.cs` ~171-204); `TenantMemberRemovedToAccessMaintenance` calls `IAccessMaintenance.RemoveUserAsync` which deletes assignments and saves Access (`AccessMaintenance.cs:RemoveUserAsync`); `TenantUnitOfWork` and `AccessUnitOfWork` are separate single-`SaveChanges` units | **Confirmed integrity hazard:** Tenant removal can commit while Access cleanup/cache invalidation fails or is not persisted → stale tenant-scoped permissions until TTL/retry (if any) |
| CS-04 | In-process, best-effort event delivery (no outbox, no retry persistence) | `TenantEventPublisher` dispatches via DI handlers and logs/swallow failures except cancellation (`TenantEventPublisher.cs`); `AccessEventHandlers.cs` indicates Access no longer handles tenant lifecycle directly (Tenant handler bridges) | Missed/delayed handler execution can leave Access state stale; no durable replay |
| CS-05 | Authorization/membership caches are in-memory, per-instance, TTL/generation based | `AccessCache` (`IMemoryCache`, epoch/scope/user generations, TTL); `TenantMembershipReader` caches membership info (`IMemoryCache`, TTL, invalidated on membership changes in Tenant flow) | Cross-instance staleness and bounded stale windows are inherent without distributed invalidation/outbox-driven convergence |
| CS-06 | JWT/access-token trust model does not encode Access membership/permissions; `tenant_id` is a routing/context claim contributed by Tenant | `SessionIssuer` resolves roles/permissions from Access for response body only, not embedded in JWT (`SessionIssuer.cs:49-55`); `TokenService` issues `sub/jti/email/iss/aud/iat/nbf/exp` (+ optional name, filtered extras) (`TokenService.cs:38-48`); `TenantClaimsContributor` adds preferred `tenant_id` claim (`TenantClaimsContributor.cs:19-29`) | Token is identity+context input, not authorization authority (consistent with Access-owned authorization), but claim-contributor pattern currently crosses ownership boundary |
| CS-07 | Tenant resolution validates tenant state and authenticated membership before setting context | `TenantResolutionMiddleware` resolves/validates tenant existence/Active status, rejects mismatch/unknown/forbidden status, and for authenticated users requires current member via `ITenantMembershipReader` (`TenantResolutionMiddleware.cs`) | Positive server-side enforcement exists, but it depends on Tenant membership read availability and occurs before Access authorization |
| CS-08 | Access guard/permission checks fail closed on missing scope/actor/auth | `AccessGuard.RequireAsync` and `RequirePlatformScopePermissionAsync` check scope/actor/auth and require fresh authority for manage operations (`AccessGuard.cs:24-86`); `AccessPermissionChecker` returns false when actor absent/unauthenticated (`AccessPermissionChecker.cs`) | Correct fail-closed posture at guard layer; does not by itself solve stale-authorized in-flight or stale-cache windows |
| CS-09 | `IAccessMaintenance` is trusted/unguarded and explicitly not for direct caller exposure | `IAccessMaintenance` XML and implementation behavior (no permission checks; system provisioning/cleanup) | Safe only if strictly internal/event/system use; misuse would be a security defect (design/implementation governance concern) |
| CS-10 | `SecurityVersion`/JWT revocation-on-credential-change remains unresolved (design acknowledges) | `SessionIssuer` guarded issuance path exists (per prior evidence in report context) but JWT validation/staleness trade-off is an open decision (D-03 referenced by design) | Stolen/unrevoked access tokens may remain usable until expiry depending on chosen policy — **open Owner/security decision**, not silently resolved by Option E |

---

## 4. Option E Review Against D-018 and Mandatory Criteria

### 4.1 D-018 / boundary compliance

| Criterion | Assessment | Evidence / Rationale | Verdict |
|---|---|---|---|
| No SharedKernel introduced/referenced | **Compliant in intent** | Design states Access-owned/Tenant-owned contracts in module abstractions; no SharedKernel proposed | **Pass** |
| Cross-module contracts in module-owned abstractions | **Partially compliant (ambiguous in proposal)** | Design proposes Access-owned integration events consumed by Access and published by Tenant (`DESIGN-03` §9.7, §11 Slice 3) while also claiming Tenant no longer implements Access interfaces; but events defined as Access-owned contracts create a publish/consume contract coupling that must be explicitly reconciled with D-018 phrasing and dependency direction | **Correction required** |
| No module implements interfaces owned by another module (Tenant/Identity must not implement Access-owned) | **Not compliant as written** | Design explicitly treats `TenantClaimsContributor : IAccessTokenClaimsContributor` as a **transitional** exception kept for MVP (`DESIGN-03` §4 Option E discussion, §6/§14/§15). Identity currently implements `ICurrentUser` (Access-owned) per CS-01/DI (`ServiceCollectionExtensions.cs:106+`) and Slice 4 proposes removal, but design does not guarantee end-state compliance without deferring exceptions | **Fail (as complete target)** |
| Access remains leaf / no dependency on Tenant | **Improved but not fully proven in design text** | Option E aims for Access-owned membership cache and `ClaimsAccessScopeContext` reading JWT claim (removing runtime call to Tenant reader). However design discussion wavers on `ICurrentTenant`/host adapters and does not provide a finalized, contradiction-free contract map proving Access has no compile-time/runtime dependency on Tenant abstractions/services after all slices | **Correction required** |
| Tenant uses Access for authorization of protected operations | **Preserved conceptually** | Access remains authorization decision owner (`IPermissionChecker`/`AccessService`/`AccessGuard`) | **Pass (conceptual)** |

### 4.2 Transactional outbox, event loss / duplication / delay / reordering

| Topic | Assessment | Evidence | Verdict |
|---|---|---|---|
| Transactional outbox | **Missing in current state; required by Option E but underspecified** | `TenantEventPublisher` in-process dispatch after save; `TenantUnitOfWork`/`AccessUnitOfWork` single-save, separate DbContexts; DESIGN-03 recommends outbox as future/MVP gap (`§10`, `§12.9`, `§14`, `§15`) | **Fail for reliable design** |
| Event loss | **Confirmed current risk; not eliminated by proposed MVP** | Handler failures logged and do not undo Tenant change (`TenantEventPublisher.cs`); no durable retry queue/reconciliation mandated in executable design | **Fail** |
| Event duplication | **Partially addressed conceptually only** | DESIGN-03 claims idempotent cache updates (`max(existing,new)` / set generation) but no concrete idempotency key, dedupe store, or handler contract shown in source | **Correction required** |
| Event delay | **Acknowledged; bounded only by TTL assumptions** | TTL safety net discussed (`DESIGN-03` §10/§11/§12), but no SLO/bound, no staleness telemetry, and no hard revocation guarantee | **Assumption / insufficient** |
| Event reordering | **Not adequately addressed** | Member add/remove/reactivation/change flows and generation semantics exist in Tenant (`MembershipGeneration` increments/reactivation logic in `TenantMembershipService`) but DESIGN-03 does not define ordering guarantees, monotonic handling rules across event types/versions, or out-of-order/drop-older semantics for Access projection | **Fail** |

### 4.3 Revocation guarantees

| Scenario | Assessment | Evidence | Verdict |
|---|---|---|---|
| Membership removal propagation to Access authorization | **Eventual, not immediate/atomic (confirmed)** | Two-phase commit across modules absent; Access cleanup currently via handler + separate save/cache invalidation | **Fail for strong revocation guarantee** |
| In-flight request during revocation | **Bounded race acknowledged** | DESIGN-03 §11.1 correctly notes check-then-act/stale window; proposed mitigation is partial and not codified as a required invariant/test | **Correction required** |
| Platform/member role/assignment revocation freshness | **Cache-generation dependent** | `AccessCache` invalidation on mutations in Access services/maintenance; stale until invalidation propagates and caches refresh per instance | **Assumption (bounded by current design), not strong guarantee** |

### 4.4 Projection storage

| Topic | Assessment | Evidence | Verdict |
|---|---|---|---|
| Proposed projection location | **Ambiguous / inconsistent** | DESIGN-03 alternates between "own local membership cache" (`IMemoryCache`) and "table or in-memory cache in Access's own database" (`§4 Option E`, §9.2 "Access's own local membership cache") without a single normative choice | **Correction required** |
| Durability/rebuildability | **Not specified** | No schema, rebuild-from-source, bootstrap, or replay procedure defined for Access-owned membership projection | **Fail** |
| Source of truth vs projection | **Conceptually stated but operationally incomplete** | Tenant remains membership source-of-truth; Access projection must converge — but without outbox/reconciliation this is not guaranteed | **Correction required** |

### 4.5 JWT trust, tenant context, and background jobs

| Topic | Assessment | Evidence | Verdict |
|---|---|---|---|
| JWT trust boundary | **Generally sound for identity; not for authorization** | JWT signed/validated by Identity; Access decisions derived from server-side Access data/cache, not JWT permissions (`SessionIssuer`, `AccessService`) | **Pass (with caveat)** |
| `tenant_id` claim trust/use | **Routing/context hint only but currently coupled to Tenant-owned contributor** | `TenantClaimsContributor` injects claim; `TenantResolutionMiddleware` treats claim as input and validates tenant/membership server-side before context set | **Pass for validation posture; fail for D-018 ownership** |
| `IAccessScopeContext` derivation in Option E | **Proposed shift to JWT claim read in Access is plausible but requires consistency rules** | If Access reads `tenant_id` from claims directly, precedence/consistency with `ICurrentTenant` and middleware-validated tenant context must be defined to avoid split-brain context sources | **Correction required** |
| Background jobs | **Partially addressed in design, unverified in implementation** | DESIGN-03 §8.4 states jobs must set `ICurrentTenant` explicitly; fail-closed if scope absent. No evidence of a standardized job-context abstraction that also satisfies Access scope derivation without Tenant-owned adapters under Option E | **Assumption / correction required** |

### 4.6 Failure handling

| Topic | Assessment | Evidence | Verdict |
|---|---|---|---|
| Fail-closed authorization on missing scope/auth | **Confirmed** | `AccessGuard`, `AccessService.HasPermissionAsync(string)` checks scope + authenticated actor | **Pass** |
| Tenant/Access DB unavailability | **Mostly fail-closed (errors propagate), but availability semantics differ by stage** | Middleware/Access service DB calls will fail requests; no degraded-mode authorization permitted (good) | **Pass** |
| Event handler / projection update failure | **Unsafe for consistency** | Logged/swallowed in publisher; separate transactions mean partial state can persist | **Fail** |
| Reconciliation/retry/idempotent recovery | **Not implemented / not specified concretely** | Mentioned as future/mitigation ideas only | **Fail** |

### 4.7 Tests, security, and module coupling

| Topic | Assessment | Evidence | Verdict |
|---|---|---|---|
| Tests (as proposed by design) | **Insufficiently specified for risk level** | DESIGN-03 lists unit-style slices/tests but omits mandatory negative/concurrency/integration cases for out-of-order/duplicate/delayed/lost events, cross-DbContext failure injection, multi-instance staleness, revocation races, and projection rebuild/reconciliation | **Correction required** |
| Security posture | **Mixed** | Good: server-side membership validation in middleware, fail-closed guards, unguarded maintenance kept internal by contract. Risks: stale authorization windows, eventual revocation, and unresolved JWT staleness policy (D-03) | **Correction required** |
| Module coupling (target) | **Improved in intent, not cleanly achieved in text** | Current adapters and contributor are concrete coupling violations; Option E reduces runtime read coupling but introduces event-contract coupling and transitional exceptions without a final compliant boundary model | **Fail (as written)** |

### 4.8 Performance and operational claims

| Claim (DESIGN-03) | Assessment | Notes |
|---|---|---|
| Reduced DB round trips / no cross-module call per authorization | **Assumption, context-dependent** | Middleware already performs tenant/membership validation against Tenant; Access may avoid Tenant reader on permission resolution, but end-to-end request cost and consistency behavior depend on chosen projection, cache hit rates, and event convergence. Claims should be framed as targets, not confirmed outcomes. |
| In-process event overhead "low" | **Assumption** | True only for happy-path in-process dispatch; ignores durability/retry/reconciliation operational cost required for correctness. |
| "MVP: in-process events + in-memory cache + TTL safety net" | **Not acceptable as a correctness guarantee** | Acceptable only as an explicitly time-bounded, owner-approved degraded-consistency posture with documented revocation SLA/risk; otherwise conflicts with integrity/revocation expectations. |

---

## 5. Confirmed Defects / Required Corrections (Prioritized)

### RC-01 (Blocker, Confirmed): D-018 compliance not fully achieved by recommended design
- **Issue:** Option E keeps `TenantClaimsContributor : IAccessTokenClaimsContributor` as transitional and does not provide a fully compliant replacement path; Identity implementing Access-owned `ICurrentUser` remains in current state and migration to a compliant end-state is not made mandatory/non-deferrable in the design.
- **Required correction:** Define a **single, D-018-compliant end-state** (no Tenant/Identity implementations of Access-owned interfaces) with explicit replacement contracts/ownership, or obtain an explicit Owner-approved, time-bounded exception recorded as a decision (not a design note). Remove "transitional" ambiguity from the target architecture.

### RC-02 (Blocker, Confirmed): Cross-module consistency and revocation rely on non-durable in-process events
- **Issue:** Member removal and Access cleanup/authorization convergence are not atomic and lack durable delivery guarantees.
- **Required correction:** Specify a **transactional outbox (or equivalent durable delivery)**, handler retry policy, poison/dead-letter handling, idempotency keys, monotonic generation/version checks, and a **reconciliation/rebuild** mechanism for Access membership projection. Define revocation semantics (target bound/SLA) and behavior on delivery failure.

### RC-03 (Blocker, Confirmed/Design gap): Access membership projection contract and storage are underdefined
- **Issue:** Projection described inconsistently (memory vs table/cache), no schema/lifecycle/bootstrap/recovery.
- **Required correction:** Normatively define projection storage (durable table, memory cache, or hybrid), ownership, write/read paths, cache invalidation rules, startup warmup/rebuild, and how Access behaves before projection convergence (fail-closed vs bounded stale) with explicit authorization impact.

### RC-04 (High, Confirmed): Event semantics do not cover ordering/duplication/replay for all membership transitions
- **Issue:** `TenantMemberAdded/Removed` proposal omits generation-carrying, idempotent, order-safe semantics across add/reactivate/remove/set-default/owner changes (as applicable) and does not define handling for duplicates, late arrivals, or gaps.
- **Required correction:** Define canonical integration event(s) with required fields (e.g. tenantId, userId, membershipGeneration/version, state/change type, occurredAt/eventId), idempotency/dedupe rules, and deterministic conflict resolution (ignore stale/older generation).

### RC-05 (High, Design correction): Scope/context source-of-truth must be singular and consistent
- **Issue:** Option E proposes Access reading `tenant_id` from JWT claims while Tenant middleware resolves/validates tenant and sets `ICurrentTenant`; split-brain risk and background-job behavior are not fully resolved.
- **Required correction:** Specify one authoritative tenant-context flow for HTTP and background jobs (including how `IAccessScopeContext` is derived after middleware validation), and require consistency checks/fail-closed behavior when sources disagree.

### RC-06 (High, Security/Integrity): In-flight and cache-staleness revocation windows must be explicitly bounded and tested
- **Issue:** DESIGN-03 acknowledges races/stale windows but treats TTL/eventual processing as sufficient without enforceable bounds.
- **Required correction:** Define required invariants (e.g. generation-checked authorization read model, post-read verification/retry rule where applicable), maximum acceptable staleness/revocation bound, and mandatory tests (concurrency/race, handler failure injection, delayed/duplicate/out-of-order events, multi-instance cache divergence).

### RC-07 (Medium, Confirmed governance gap): Open security/product decisions must remain explicit prerequisites
- **Issue:** JWT `SecurityVersion`/revocation-on-credential-change (D-03), rate limiting on tenant/access writes (D-18 in design register), and distributed cache/multi-instance consistency remain open.
- **Required correction:** Do not present Option E as resolving these; record them as explicit prerequisites/follow-on Owner decisions with acceptance criteria and risk statements in the implementation plan.

### RC-08 (Medium, Design quality): Correct contract/source mismatches and overconfident claims in the report
- **Issue:** Examples include event naming/ownership drift vs current abstractions (`TenantMemberRemoved` integration fact currently Tenant-owned in `CoreKit.Tenant.Events` vs proposed Access-owned event contracts), `IAccessMembership` vs `IAccessMembershipAdapter` usage in text, and performance claims stated as facts.
- **Required correction:** Align the design document with actual baseline contracts and clearly label assumptions/targets; include a traceable delta from current state to each proposed contract move.

---

## 6. Assumptions / Not-Evidenced Items (explicit)

- **Assumption:** Access-owned JWT-claim scope context (`tenant_id`) can fully replace `TenantAccessScopeContext` without regressions for all hosts/background contexts — **not proven** without a concrete, compliant implementation design and tests.
- **Assumption:** Eventual-consistency windows implied by TTL/in-process handling are acceptable to the Project Owner — **Owner decision required**.
- **Assumption:** Performance improvements claimed by Option E materialize in end-to-end flows — **not evidenced/measured**.
- **Not evidenced:** Any existing outbox table, retry scheduler, dead-letter store, reconciliation job, or distributed invalidation mechanism in the inspected baseline.

---

## 7. Final Verdict

**Reject `DK-TENANT-ACCESS-INTEGRATION-DESIGN-03` in its current form.**

**Acceptable path forward:** Resubmit as `DESIGN-03` revision (or `DESIGN-04`) that (1) achieves a fully D-018-compliant end-state without transitional interface-implementation exceptions, (2) specifies durable outbox/idempotent/reorder-safe event processing and projection rebuild/reconciliation with explicit revocation guarantees, (3) defines a single authoritative tenant-context model for HTTP/background jobs, and (4) includes a risk-driven verification/test plan and explicit Owner-decision prerequisites. Until then, implementation based on the current document would carry confirmed architectural-boundary and authorization-consistency risks.  
