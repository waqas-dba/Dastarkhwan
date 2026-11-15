# DK-TENANT-ACCESS-INTEGRATION-REVIEW-05 — Independent Architectural Review (Read-Only)

**Repository:** `waqas-dba/Dastarkhwan`  
**Required baseline:** `73932a6125a89f42553732c313a6dda496a42656`  
**Verified baseline:** `73932a6125a89f42553732c313a6dda496a42656` (2026-11-15 16:14:29 +0500, `docs(governance): reconcile architecture decisions and roadmap with baseline c9dbbe12`) — **matches required baseline (no stop condition)**  
**Branch:** `master`  
**Remote HEAD (`origin/master`):** `73932a6125a89f42553732c313a6dda496a42656` — matches local HEAD  
**Working tree at inspection:** `?? DK-TENANT-ACCESS-INTEGRATION-DESIGN-03-REPORT.md`, `?? DK-TENANT-ACCESS-INTEGRATION-DESIGN-04-REPORT.md`, `?? DK-TENANT-ACCESS-INTEGRATION-REVIEW-04-REPORT.md` (untracked review/design artifacts only; no tracked modifications observed)  
**Task type:** Independent architectural review — **read-only**  
**Reviewer:** Kilo (independent read-only reviewer; Claude Account 2 role requested by task)  
**Design under review:** `DK-TENANT-ACCESS-INTEGRATION-DESIGN-04-REPORT.md`  
**Previous review:** `DK-TENANT-ACCESS-INTEGRATION-REVIEW-04-REPORT.md`

**Actions not performed (per task):** no source/project documentation modifications, no implementation/fix patches, no builds, no tests, no migrations, no database operations, no staging/commit/push/branch/history changes. No local runtime/working-tree behavior was verified beyond static inspection and Git metadata.

---

## 1. Baseline and Evidence Inventory

### 1.1 Baseline verification (independently verified)
| Item | Evidence |
|---|---|
| Local HEAD / hash | `git rev-parse HEAD` → `73932a6125a89f42553732c313a6dda496a42656` |
| Commit metadata | `git log -1` → 2026-11-15 16:14:29 +0500 |
| Branch | `git branch --show-current` → `master` |
| Working tree | `git status --short` → 3 untracked report files only |
| Required baseline match | **PASS** |

### 1.2 Documents inspected (independently verified)
| Document | Purpose |
|---|---|
| `DK-TENANT-ACCESS-INTEGRATION-REVIEW-04-REPORT.md` | RC-01..RC-08 baseline findings and verdict |
| `DK-TENANT-ACCESS-INTEGRATION-DESIGN-04-REPORT.md` | Proposed revised architecture (Option E variant), contracts, flows, slices, risks |
| `decisions.md` | D-002, D-003, D-016, D-017, **D-018** (no SharedKernel; no Tenant/Identity implementation of Access-owned interfaces; module-owned abstractions; Access leaf) |
| `architecture.md` (previously inspected in this workspace context) | Target boundaries/tenant-access integration direction |

### 1.3 Source inspected (independently verified, static)
| Area | Files (key lines/evidence) |
|---|---|
| Access abstractions/contracts | `CoreKit.Access/Abstractions/AccessPorts.cs` (`IAccessScopeContext`, `IAccessMembership`, `IAccessMembershipAdapter`, `AccessMember`), `IAccessIntegrationEvent.cs`, `IAccessTokenClaimsContributor.cs`, `CompositeAccessMembership.cs`, `PlatformAccessMembership.cs`, `IAccessService.cs`, `IAccessMaintenance.cs`, `ICurrentUser.cs`/`IAccessCurrentUser.cs`, `IPermissionChecker.cs` |
| Access runtime/services | `Services/AccessService.cs` (`ResolveAsync` uses `_membership.FindAsync` + generation-filtered assignments), `AccessGuard.cs` (scope/auth/fail-closed, fresh authority for manage ops), `AccessCache.cs` (IMemoryCache, generation/epoch tokening), `AccessMaintenance.cs` (unguarded trusted ops), `AccessPermissionChecker.cs`, `AccessEventHandlers.cs`, `DefaultPorts.cs`, `Extensions/AccessServiceCollectionExtensions.cs` |
| Access persistence | `Persistence/AccessDbContext.cs`, `AccessUnitOfWork.cs` (single `SaveChanges`, outcomes), `AccessAssignmentRepository.cs` |
| Tenant integration/boundary crossings | `Integration/TenantAccessScopeContext.cs` (`: IAccessScopeContext`), `TenantAccessMembership.cs` (`: IAccessMembershipAdapter` → `ITenantMembershipReader`), `TenantClaimsContributor.cs` (`: IAccessTokenClaimsContributor`), `TenantMemberRemovedToAccessMaintenance.cs` (`IAccessMaintenance.RemoveUserAsync`); `Extensions/TenantServiceCollectionExtensions.cs` (DI registrations/overrides) |
| Tenant membership/events | `Services/TenantMembershipService.cs` (add/remove/set-owner flows, generation changes, save-then-publish), `TenantMembershipReader.cs`, `TenantEventPublisher.cs` (in-process dispatch, logs/swallow handler failures), `Persistence/TenantUnitOfWork.cs`; `Events/IntegrationEvents.cs` and `Abstractions/TenantEvents.cs` (`TenantMemberAddedEvent`/`TenantMemberRemovedEvent` Tenant-owned/internal event types, `OccurredAt` only) |
| Tenant context resolution | `Resolution/TenantResolutionMiddleware.cs` (resolves/validates tenant + authenticated membership, sets context; fail-closed paths) |
| Identity integration | `Services/SessionIssuer.cs` (Access read for response body; guarded issuance), `TokenService.cs` (JWT claims), `Extensions/ServiceCollectionExtensions.cs` (`ICurrentUser` mapping to Identity service) |

### 1.4 Evidence confidence
- **Independently verified:** statements tied to inspected source/docs/Git output above.
- **Report assertion (unverified):** claims in DESIGN-04 not backed by inspected implementation (no outbox tables/publisher/consumer, no Access membership projection table, no `ClaimsAccessScopeContext`/`AccessMembershipCache`/`ClaimsAccessCurrentUser` implementations, no runtime measurements).

---

## 2. Overall Verdict

**Verdict: Reject (not implementation-ready; not acceptable for implementation approval).**

DESIGN-04 improves directionally (explicitly targets removal of Tenant/Identity implementations of Access-owned interfaces, Access-owned membership projection, event-carried generation/idempotency fields, and acknowledges outbox/reconciliation need). However, it **does not fully resolve Review-04 blockers**: D-018 end-state compliance remains ambiguous/deferrable in key areas (Slice 5 optionality and residual claim-contributor/contract posture), durable outbox/retry/dedupe/replay/reconciliation/projection rebuild semantics are not normatively specified or evidenced, revocation remains eventual and unquantified as a guaranteed bound, tenant context is not universally defined for non-HTTP/background execution, and several contract/flow/test claims are inconsistent with baseline or internally contradictory. **Implementation remains blocked pending corrections and explicit Project Owner approval.**

---

## 3. Review-04 Correction Matrix (RC-01 .. RC-08)

| RC | Review-04 issue (summary) | DESIGN-04 disposition (report assertion) | Independent assessment | Status |
|---|---|---|---|---|
| RC-01 | D-018 end-state compliance (no Tenant/Identity implementing Access-owned interfaces; no transitional ambiguity) | Proposes removing `TenantAccessScopeContext`, `TenantAccessMembership`, `TenantClaimsContributor` and Identity `ICurrentUser` implementation via slices; defines Access-owned contracts | **Partially addressed, not resolved.** Current violations are confirmed in baseline (`TenantAccessScopeContext.cs:10`, `TenantAccessMembership.cs:10`, `TenantClaimsContributor.cs:13`, Identity DI mapping in `ServiceCollectionExtensions.cs:106`). DESIGN-04 Slice 5 is marked deferrable/"Owner decision" (§13, §14) and still contains tenant-claim/context contradictions (see Findings F-01/F-07). Access-owned integration-event contract direction is stated but must be reconciled concretely with module dependencies and D-018 without ambiguity. | **Open (Blocker)** |
| RC-02 | Durable delivery: transactional outbox, retries, DLQ/poison handling, idempotency, reconciliation, revocation semantics | Mentions outbox in sequence/cache text and risks; adds `EventId`/`Sequence`/generation to proposed events | **Not resolved.** Baseline remains in-process best-effort (`TenantEventPublisher.cs`; `TenantUnitOfWork`/`AccessUnitOfWork` separate). DESIGN-04 simultaneously states MVP "in-process events + in-memory cache + TTL safety net" and lists outbox as open/high-complexity future (§10.1, §12.6-§12.9, §14, §15) without normative outbox schema, publisher/consumer transactions, retry/backoff/DLQ, dedupe store, replay cursoring, or reconciliation job design. | **Open (Blocker)** |
| RC-03 | Membership projection storage/lifecycle/rebuild/convergence behavior | Proposes `AccessMembershipCache` and states "IMemoryCache + persistent table `AccessMembership`" and bootstrap/replay ideas | **Not resolved.** No projection schema, ownership, bootstrap source, rebuild procedure, startup behavior, or fail-closed/convergence rules are normatively defined; Access DbContext currently has no membership projection table (`AccessDbContext.cs`). Persistence claim is inconsistent with "in-memory cache" MVP posture. | **Open (Blocker)** |
| RC-04 | Ordering/duplication/replay semantics for all membership transitions | Adds generation/eventId/sequence fields; idempotent `max(existing,new)` claim | **Partially addressed, insufficient.** Fields are proposed but no canonical per-aggregate ordering key, gap handling, duplicate detection persistence, replay/idempotency store, or deterministic rules for out-of-order add/remove/reactivation/owner/default transitions are specified. Baseline events carry only ids/time (`TenantEvents.cs`, `IntegrationEvents.cs`). | **Open (High)** |
| RC-05 | Singular authoritative tenant-context flow for HTTP and background jobs | Proposes middleware sets `ICurrentTenant` + `HttpContext.Items["tenant_id"]`; Access scope reads Items; jobs must set both | **Not resolved.** `HttpContext.Items` is not a universal/non-HTTP context carrier (confirmed limitation). Design does not define an Access-owned non-HTTP scope/context abstraction or authoritative precedence/consistency checks. Current middleware resolves tenant from claim and validates membership (`TenantResolutionMiddleware.cs`) but DESIGN-04 text alternates claim/header/Items sources. | **Open (Blocker/High)** |
| RC-06 | Bounded revocation/staleness invariants and mandatory race/failure tests | Acknowledges stale window/races; suggests TTL and possible re-read | **Not resolved.** No quantified maximum stale-authorization bound/SLA, no required generation-checked invariant codified as mandatory, and no concrete failure-injection/concurrency/multi-instance/replay test matrix. Eventual revocation is still described as possible/TTL-bounded. | **Open (High)** |
| RC-07 | Open decisions (D-03, D-18, multi-instance/distributed cache) remain explicit prerequisites | Lists D-03/D-18/outbox/distributed cache as open | **Mostly addressed as listed open items**, but D-03/D-18 must remain explicit non-approved prerequisites and are not resolved by DESIGN-04 (correctly). | **Partially resolved (documentation) / Prerequisite remains open** |
| RC-08 | Contract/source mismatches and unsupported claims | Partially aligns to current interfaces/files; adds more proposed contracts | **Not resolved.** Multiple mismatches remain (e.g. middleware currently claim-based vs proposed header/Items flow, current Tenant-owned `TenantMemberAddedEvent`/`TenantMemberRemovedEvent` signatures vs proposed Access-owned `TenantMemberAdded/Removed` with `EventId/Sequence/Generation`, performance/latency/overhead figures presented without evidence, "persistent table" vs MVP in-memory posture). | **Open (Medium)** |

---

## 4. Prioritized Findings

| ID | Sev. | Finding (independently verified unless noted) | Evidence (file:line where available) | Impact |
|---|---|---|---|---|
| F-01 | **Critical** | **D-018 end-state not guaranteed:** current Tenant/Identity implementations of Access-owned interfaces remain; DESIGN-04 makes removal of `TenantClaimsContributor` deferrable and leaves contract/context transitions ambiguous. | `CoreKit.Tenant/Integration/TenantAccessScopeContext.cs:10`, `TenantAccessMembership.cs:10`, `TenantClaimsContributor.cs:13`; `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs` (registrations); `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:106` (`ICurrentUser` mapping); DESIGN-04 §13 Slice 5 "may be deferred", §14, §15 | Architectural boundary violation can persist; implementation could ship non-compliant "target" |
| F-02 | **Critical** | **No durable atomicity/outbox for membership→Access convergence:** Tenant save and Access projection/cleanup remain separable with best-effort in-process dispatch. | `CoreKit.Tenant/Services/TenantMembershipService.cs` (save then publish in remove/add flows), `CoreKit.Tenant/Persistence/TenantUnitOfWork.cs`, `CoreKit.Access/Persistence/AccessUnitOfWork.cs`; `CoreKit.Tenant/Services/TenantEventPublisher.cs` (catch/log non-cancel handler failures); `CoreKit.Tenant/Integration/TenantMemberRemovedToAccessMaintenance.cs`; DESIGN-04 §12.8/§12.9/§14/§15 admit outbox not implemented | Lost/failed handling can leave stale authorization state; no guaranteed recovery |
| F-03 | **Critical** | **Projection storage/bootstrap/rebuild undefined:** "IMemoryCache + persistent table `AccessMembership`" is asserted without schema, write path, replay/bootstrap, or startup/recovery semantics. | DESIGN-04 §10.1, §13 Slice 3; baseline `CoreKit.Access/Persistence/AccessDbContext.cs` has no membership projection table/entity | Correctness, restart recovery, and multi-instance convergence cannot be validated or implemented safely |
| F-04 | **High** | **Event protocol insufficient for idempotent, ordered, replayable processing:** proposed fields exist only in report; no dedupe persistence, ordering key, gap/out-of-order policy, or transition coverage. | DESIGN-04 §9.7, §10.3; baseline `CoreKit.Tenant/Abstractions/TenantEvents.cs` (`TenantMemberAddedEvent`/`TenantMemberRemovedEvent` lack generation/eventId/sequence), `CoreKit.Tenant/Events/IntegrationEvents.cs` | Duplicate/reordered/delayed/replayed events can corrupt or stale projection without deterministic rules |
| F-05 | **High** | **Revocation guarantee is eventual and unquantified:** stale windows (in-flight, handler delay/failure, per-instance cache, restart before convergence) are acknowledged but no maximum bound/SLA or mandatory invariant. | DESIGN-04 §10.3, §11.1-§11.5, §12.8; baseline cache/generation behavior in `CoreKit.Access/Services/AccessCache.cs`, `AccessService.cs:ResolveAsync` | Security-sensitive authorization may remain stale after removal; cannot claim immediate revocation |
| F-06 | **High** | **Tenant context not universal for non-HTTP/background/scheduled execution:** dependence on `HttpContext.Items["tenant_id"]` and middleware HTTP flow is incompatible with non-HTTP contexts. | DESIGN-04 §8.4, §9.1, §13 Slice 2/5; `CoreKit.Tenant/Resolution/TenantResolutionMiddleware.cs` (HTTP middleware); `CoreKit.Access/Services/DefaultPorts.cs`/scope resolution currently DI/context-based | Background jobs/tasks may fail closed or be incorrectly wired; context spoofing/ambiguity risk if ad hoc |
| F-07 | **High** | **Internal inconsistencies in proposed request flow and slice ordering:** report alternates tenant source (`tenant_id` JWT claim vs `X-Tenant-Id` header vs `HttpContext.Items`) and removes claim contributor in Slice 5 while earlier slices/flows still assume claim/Items set from claim path. | DESIGN-04 §8.1, §8.4, §9.1, §10 (flow text), §13 Slices 2/5; baseline `TenantResolutionMiddleware.cs` reads claim and validates via `ITenantMembershipReader`; `TenantClaimsContributor.cs` currently injects claim | Implementation sequencing can break tenant resolution/scope derivation; increases regression risk |
| F-08 | **Medium** | **Trust-boundary claims understate client influence:** `X-Tenant-Id`/header-origin wording appears while D-011/§11.8 style validation must remain server-side against authenticated membership; final design must state authoritative, validated source only. | DESIGN-04 §8.1/§8.4/§11.8 wording vs baseline `TenantResolutionMiddleware.cs` membership validation and fail-closed behavior | Potential for ambiguous implementation that over-trusts client-supplied tenant selectors |
| F-09 | **Medium** | **Unsupported performance/operational assertions presented as values:** latency ms, overhead ranges, and round-trip reductions are not evidenced. | DESIGN-04 §12.1-§12.7 (tables with timings/overheads) | Misleading operational expectations; capacity/SLO planning risk |
| F-10 | **Medium** | **Test plan insufficient for stated risk profile:** lacks explicit cases for outbox/retry/DLQ, duplicate/reordered/delayed/lost events, replay/rebuild, restart recovery, multi-instance divergence, concurrent revocation/in-flight checks, and background-job context. | DESIGN-04 §13 (slice tests only), §15 | Critical failure modes likely to escape verification |
| F-11 | **Low** | **Contract naming/ownership drift in proposal vs baseline:** proposed `TenantMemberAdded/Removed` as Access-owned integration events conflicts with current Tenant-owned/internal event records and current `IAccessIntegrationEvent`/handler abstractions unless migration is precisely specified. | Baseline `CoreKit.Access/Abstractions/IAccessIntegrationEvent.cs`, `CoreKit.Tenant/Abstractions/TenantEvents.cs`, `CoreKit.Tenant/Events/IntegrationEvents.cs`; DESIGN-04 §9.7 | Refactor breakage and dependency-direction confusion during implementation |

---

## 5. Security, Consistency, and Revocation Analysis

### 5.1 Security and trust boundaries (independently verified baseline + design assessment)
- **Positive (confirmed):** Access authorization is server-side and fail-closed on missing scope/unauthenticated actor (`AccessGuard.cs:24-81`, `AccessService.cs` permission checks); middleware validates tenant existence/status and authenticated user membership before establishing tenant context (`TenantResolutionMiddleware.cs`); JWT permissions are not trusted/embedded as authority (roles/permissions resolved from Access for response in `SessionIssuer.cs:49-55`). `IAccessMaintenance` is unguarded by contract and must remain internal/trusted-only (`IAccessMaintenance` abstraction/`AccessMaintenance.cs`).
- **Risks/gaps:** D-018 boundary violations remain in baseline and are not conclusively eliminated by DESIGN-04 end-state (F-01). Tenant-selection source must remain server-validated; any move to header/Items must not weaken D-011/D-018 intent (F-06/F-08). **D-03 (JWT revocation/SecurityVersion validation) remains an unresolved Owner decision** — access tokens may remain valid until expiry by current model; DESIGN-04 does not resolve this. **D-18 (rate limiting on tenant/access writes) remains unresolved**.

### 5.2 Consistency model
- **Current/proposed state:** cross-module membership-to-authorization propagation is (and, under DESIGN-04 as written, remains) **eventual** due to separate DbContexts, non-durable/ambiguous delivery, and cache layers (`AccessCache`, Tenant membership cache, proposed Access membership projection).
- **Atomicity:** no cross-DbContext atomic commit demonstrated or normatively designed (F-02). DESIGN-04 does not provide an executable outbox transaction boundary.
- **Duplicates/reordering/replay:** not safely handled without persisted dedupe + monotonic versioning rules + deterministic apply logic (F-04).

### 5.3 Revocation guarantees (must not be described as immediate)
- **Maximum stale authorization window (quantified):** **Unbounded / not guaranteed by design** in failure cases (lost handler, undelivered/poison event, projection not rebuilt, restart before convergence, multi-instance non-propagation) because durable delivery/reconciliation is not normatively specified (F-02/F-03/F-05). In benign in-process delay cases, staleness is **at least** in-flight request duration plus event handling latency; with cache reliance it can extend toward configured TTLs (e.g. Access membership/authorization cache TTLs such as default 300s mentioned in DESIGN-04 §10.1 is a **report assertion**, not a guaranteed upper bound under failure). **No safe maximum bound can be approved from DESIGN-04.**
- **In-flight requests:** confirmed race window (membership read/assignment read/check-then-act) is acknowledged; not eliminated (DESIGN-04 §11.1, baseline `AccessService.ResolveAsync`).
- **Concurrent membership removal:** remove/reactivate generation increments exist in Tenant (`TenantMembershipService.cs`), but Access-side monotonic/idempotent application under concurrency/duplicates/reorder is unspecified (F-04/F-05).
- **Failed event delivery/cache invalidation failures:** can preserve stale allow decisions until manual intervention or unspecified recovery (F-02/F-05).

---

## 6. Contract and Dependency-Direction Assessment

| Contract / concern | Current (verified) | DESIGN-04 proposal | Assessment |
|---|---|---|---|
| `IAccessScopeContext` | Access-owned contract; implemented by Tenant adapter `TenantAccessScopeContext` (D-018 violation) | Access-owned implementation (`ClaimsAccessScopeContext`) reading `HttpContext.Items["tenant_id"]` | **Directionally correct if fully implemented**, but non-HTTP universality and source-of-truth consistency unresolved (F-06/F-07) |
| `IAccessMembership` | Access-owned contract + adapter/composite; Tenant adapter bridges to `ITenantMembershipReader` (runtime Access→Tenant) | Access-owned `AccessMembershipCache` fed by events; no Tenant runtime read | **Directionally correct**, but projection/event durability and lifecycle undefined (F-02/F-03/F-04) |
| `IAccessTokenClaimsContributor` / tenant claim | Access-owned contract; Tenant implements `TenantClaimsContributor` | Remove contributor/no tenant claim (Slice 5, deferrable) | **Not D-018-compliant as an optional end-state** (F-01/F-07) |
| Identity `ICurrentUser` implementation | Identity service mapped to Access-owned `ICurrentUser` (`ServiceCollectionExtensions.cs:106`) | Access-owned `ClaimsAccessCurrentUser`; Identity keeps Identity-owned service | **Required for D-018 alignment**, but only proposed (F-01) |
| Integration events | Access defines `IAccessIntegrationEvent`/handler abstractions; Tenant has its own event types/publisher and Access cleanup bridged in Tenant integration | Access-owned `TenantMemberAdded/Removed` with `EventId/Sequence/Generation` | **Needs precise migration plan** to avoid dependency inversion/confusion and to ensure durable, module-owned publication/consumption (F-04/F-11) |
| Dependency direction (target) | Compile-time mostly leaf-like for Access but runtime coupling via adapters/DI; Tenant→Access and Identity→Access refs exist | Access leaf at compile/runtime; Tenant publishes facts consumed by Access | **Feasible in principle, not proven by DESIGN-04 as written** (F-01/F-06/F-07) |
| `HttpContext.Items` as context carrier | N/A (proposed) | Used as bridge for tenant scope | **Not acceptable as universal solution** for HTTP + background jobs + scheduled/non-HTTP tasks (F-06) |

---

## 7. Test and Operational-Readiness Gaps

### 7.1 Missing/insufficient test coverage (required before approval/implementation confidence)
- **Concurrency/races:** concurrent member remove vs authorization resolve; reactivation/remove interleavings; generation monotonicity under parallel handlers.
- **Event delivery failures:** outbox publish/dispatch failure, handler exception after Tenant commit, retry exhaustion, DLQ/poison handling, duplicate delivery, delayed delivery, reordered delivery, replay/idempotency.
- **Restart/rebuild:** service restart mid-stream, projection rebuild/bootstrap correctness, empty/cold projection behavior (must fail closed or be explicitly bounded).
- **Multi-instance:** cache divergence across instances, event visibility/processing lag, no-double-apply/dedupe across nodes (if distributed later).
- **Revocation:** prove maximum stale window (or enforce fail-closed), in-flight request behavior, cache invalidation failure scenarios.
- **Authorization negatives:** tenant mismatch, non-member, removed member, invalid/missing tenant context in HTTP and non-HTTP jobs, platform-vs-tenant scope separation.
- **Regression/security:** D-018 boundary checks (no forbidden interface implementations/DI overrides), no trust of client-supplied tenant selector without validation, JWT staleness behavior relative to D-03 decision.

### 7.2 Operational readiness gaps
- No normative **transactional outbox** schema/monitoring/alerting/runbook.
- No **retry/backoff/DLQ**, lag metrics, or alert thresholds for projection convergence.
- No **reconciliation job** specification (scope, frequency, source-of-truth comparison, repair actions, auditability).
- No **projection rebuild/bootstrap** procedure or operational ownership.
- Multi-instance consistency strategy (distributed invalidation/cache/bus) remains open; current per-instance caches + eventual events are operationally fragile for revocation-sensitive paths.

---

## 8. Required Corrections (if DESIGN-04 is to be reconsidered)

1. **RC-01 (Blocker):** Publish a **normative, non-deferrable D-018-compliant target** (or explicit Owner exception decision): remove all Tenant/Identity implementations/DI registrations of Access-owned interfaces, including a concrete replacement for tenant-claim contribution/context that preserves server-side trust boundaries and dependency direction.
2. **RC-02 (Blocker):** Specify and require **durable outbox-based delivery** (schema, same-transaction write rules, publisher, consumer, retries/backoff, DLQ/poison path), **idempotent handlers** with persisted dedupe, and **reconciliation/replay** procedures with revocation/convergence guarantees.
3. **RC-03 (Blocker):** Define the **Access membership projection** normatively (durable store schema in Access, cache layering, write/apply logic, bootstrap/rebuild, startup and outage behavior, fail-closed policy before convergence).
4. **RC-04 (High):** Define **event contract and ordering** (event type/version, aggregate key, monotonic generation/sequence semantics, duplicate/out-of-order/gap handling) covering add/remove/reactivation/ownership-relevant transitions as needed.
5. **RC-05 (High):** Define a **single authoritative tenant-context model** for HTTP, background jobs, scheduled tasks, and non-HTTP execution (Access-owned abstraction or equivalent) — not `HttpContext.Items` alone — with validation and consistency/fail-closed rules.
6. **RC-06 (High):** Replace eventual-revocation ambiguity with **explicit bounded guarantees/invariants** (or explicit Owner-accepted risk bound), including in-flight handling; add mandatory failure-injection and concurrency test requirements.
7. **RC-08 (Medium):** Correct all **contract/source/flow mismatches** and label performance/operational figures as unverified targets or remove them; provide traceable current→target mapping per slice.
8. **RC-07 (Prerequisite clarity):** Keep **D-03 and D-18** explicitly unresolved unless Owner decides; do not imply resolution.

---

## 9. Explicit Owner Decisions and Implementation Prerequisites

| Item | Type | Status | Notes |
|---|---|---|---|
| Approve D-018-compliant Tenant–Access integration target (Option E revised, no transitional interface implementations) | Owner decision | **Required** | Reject/accept any exception explicitly; do not rely on "deferred" Slice 5 |
| Durable delivery posture (transactional outbox + retry/DLQ + reconciliation) vs accepting bounded eventual consistency risk | Owner decision | **Required** | Must include acceptable revocation bound/SLA and failure behavior |
| Membership projection durability/rebuild and startup fail-closed policy | Owner decision / design approval | **Required** | Security-sensitive authorization behavior |
| Authoritative tenant-context mechanism for non-HTTP/background execution | Owner/design decision | **Required** | `HttpContext.Items` alone insufficient |
| D-03 — JWT revocation / `SecurityVersion` validation | Owner decision | **Open / unresolved** | Not decided by DESIGN-04 or this review |
| D-18 — Rate limiting on tenant/access write endpoints | Owner decision | **Open / unresolved** | Not decided by DESIGN-04 or this review |
| Multi-instance consistency/distributed invalidation strategy | Owner decision | **Open** | Needed if revocation/convergence guarantees must hold across instances |
| Implementation authorization (slices, migrations, tests) | Owner approval | **Blocked** | Pending accepted revised design and this review disposition |

---

## 10. Final Recommendation

**Recommendation: Reject DESIGN-04 for implementation; return for a corrected revision (DESIGN-05 or equivalent) and explicit Project Owner decisions.**

DESIGN-04 should **not** be treated as implementation-ready. A resubmission can be considered for Owner review only after it: (1) demonstrates a fully D-018-compliant, non-ambiguous end-state and dependency/contract map; (2) normatively specifies durable outbox/event semantics, idempotent ordered processing, and a durable/rebuildable Access membership projection with explicit convergence and bounded revocation guarantees; (3) defines a universal authoritative tenant-context solution for HTTP and non-HTTP execution; and (4) replaces unsupported claims with evidenced/qualified statements and a complete risk-based verification plan. **This review does not approve architecture on behalf of the Project Owner.**  
