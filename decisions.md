# Architecture and Product Decisions

Use this log for decisions affecting product behavior, security, data, compatibility, or module boundaries. Never silently turn an assumption into an accepted decision. An accepted decision is not proof that code has implemented it.

**Statuses:** Proposed, Accepted, Rejected, Superseded. Dates below record the decision context available in the project conversation; retain/update them only if confirmed by the repository owner.

## D-001 — Modular monolith
**Status:** Accepted  
**Decision:** Start as a modular monolith; split deployment only when measured needs justify it.  
**Consequences:** Clear contracts, module-owned persistence, no circular dependencies, and no premature distributed infrastructure.

## D-002 — Identity and Access are separate
**Status:** Accepted  
**Decision:** Identity owns accounts, authentication, credentials, sessions/tokens, and recovery. Access owns roles, permissions, assignments, and authorization.  
**Consequences:** Access must not access Identity private tables or `DbContext`. Identity may provide only the stable public identity contracts/claims required by Access. The M0 baseline still had roles/permissions in Identity; implementation must be migrated in a controlled, separately reviewed change.

## D-003 — SaaS and standalone composition
**Status:** Accepted  
**Decision:** SaaS composition includes Tenant, Identity, and Access. Standalone composition does not require Tenant infrastructure. Identity/Access may be optional only when a secure alternative is explicitly configured for protected operations.  
**Consequences:** Keep host-specific composition at clear boundaries. Use a separate `StandaloneStore` only when lifecycle/business rules materially differ; configuration-only differences should reuse Store.

## D-004 — Initial product scope
**Status:** Accepted  
**Decision:** Food-item delivery and parcel pickup/delivery. Do not add hotels, dine-in, or unrelated verticals without an explicit decision.  
**Consequences:** Keep MVP focused; introduce future verticals only when requested.

## D-005 — Country/market extensibility
**Status:** Accepted  
**Decision:** Pakistan first; future countries, including the UK, should be supported through market configuration and replaceable integrations.  
**Consequences:** Keep tenant, store, market, and currency distinct. Verify country-specific legal, tax, payment, and invoicing requirements before implementation.

## D-006 — Preserve transaction snapshots
**Status:** Accepted  
**Decision:** Orders preserve currency and price-related snapshots, including applicable discounts, taxes, and fees.  
**Consequences:** Historical totals must not change when catalog settings change. Use explicit currency codes and appropriate decimal precision for money.

## D-007 — Small milestones and user verification
**Status:** Accepted  
**Decision:** Work in small, independently reviewable micro-slices. AI may add/update tests but must not run tests/builds without explicit permission; the user normally verifies manually.  
**Consequences:** One measurable outcome per task. Stop after each slice and wait for approval. Report tests as “added/updated, not run” unless an authorized run was observed.

## D-008 — Git remains user-controlled
**Status:** Accepted  
**Decision:** Git inspection commands (`git status`, `git log`, `git diff`, branch listing) are permitted without approval. Git operations that change repository state require explicit Project Owner approval before execution.  
**Consequences:** Before requesting approval, the agent must explain: purpose, exact command, benefits, risks and consequences, and whether branches, commits or remote repositories will be affected. Asking for approval does not authorize execution; the agent must wait for explicit approval. This applies to staging, committing, pushing, pulling, switching/creating branches, merging, rebasing, cherry-picking, resetting, cleaning, reverting and history rewriting. AI may recommend a manual checkpoint and prepare a descriptive commit subject/body. The user performs all Git write operations. See `ai-tasks.md` §15.2.

## D-009 — Checkout grouping
**Status:** Proposed  
**Decision to confirm when M8 requires it:** Single-store checkout vs one checkout containing products from multiple stores.  
**Reason:** This affects order ownership, delivery fees, payment allocation, refunds, fulfillment, and merchant access.  
**Rule:** Do not implement multi-store checkout by assumption.

## D-010 — Parcel pickup initiation
**Status:** Proposed  
**Decision to confirm when M10 requires it:** Is pickup initiated by a customer, a merchant, or both? Define addresses, pricing, assignment, cancellation, and states at that time.

## D-011 — Tenant data isolation
**Status:** Accepted  
**Decision:** Use one shared PostgreSQL database rather than a database or schema per tenant for the initial SaaS implementation. Tenant-owned records must have explicit `TenantId` ownership (or an explicitly approved ownership relationship), and EF Core global query filters should provide a default tenant scope.  
**Consequences:** Global query filters are defense-in-depth, not the only security control. Application services and authorization must enforce tenant scope for every read/write, including background jobs, exports, reports, callbacks, and admin paths. Fail closed if tenant context is missing or invalid. Add cross-tenant negative tests. Avoid treating client-supplied `X-Tenant-ID` as trusted authorization; resolve/validate it against authenticated membership and request context.

## D-012 — Authentication and session approach
**Status:** Accepted  
**Decision:** Use short-lived JWT access tokens with rotating refresh tokens. Add refresh-token reuse detection and secure token revocation. Lock an account for 15 minutes after 5 failed login attempts.  
**Consequences:** Define secure token storage, rotation atomicity, revocation, reuse response, lockout reset/expiry behavior, and rate limiting during implementation. Keep account lockout distinct from IP/request rate limiting. Do not store plaintext refresh tokens. Exact durations/settings beyond the approved 15-minute lockout should not be invented without a clear need. Add focused positive/negative tests; AI does not run them without permission.

## D-013 — Initial payments
**Status:** Proposed  
**Decision to confirm before M9:** Initial payment methods and COD behavior. Consider provider availability, signatures, idempotency, refunds, reconciliation, and market/currency support. Do not integrate a provider before its selection is approved.

## D-014 — Website and custom domains
**Status:** Proposed  
**Decision to confirm before M6/M11:** Website technology and operations for DNS ownership verification, hostname routing, HTTPS provisioning/renewal, and failure handling. A hostname is routing input, not authorization.

## D-015 — Tenant must not depend directly on Identity
**Status:** Accepted  
**Decision:** Remove the direct `CoreKit.Tenant` dependency on `CoreKit.Identity` through stable SharedKernel contracts/abstractions.  
**Consequences:** Inspect the existing six reported code-file references at the current baseline; classify each use before changing it. Choose contract ownership and dependency direction before implementation. Do not create duplicate user/account models or introduce circular references. Tenant must remain reusable by other SaaS applications.

## D-016 — Tenant requires Access for authorization
**Status:** Accepted  
**Date:** 2026-11-11  
**Decision:** Tenant requires Access for authorization of protected Tenant operations.  
**Context:** Option B (Tenant requires Access for authorization) was approved over the previous Option A direction. Tenant owns tenant lifecycle, membership, tenant context and tenant-specific business rules. Access owns roles, permissions, assignments and authorization decisions. Tenant integrates through Access public contracts and must not access Access private persistence or internals.  
**Consequences:**
- Tenant integrates through Access public contracts (`IAccessScopeContext`, `IAccessMembership`, `IAccessService`, `IPermissionChecker`).
- Access must not depend on Tenant or Identity private internals.
- SharedKernel provides neutral cross-module contracts where genuinely needed (e.g. `ITenantMembershipReader` as a candidate bridge for Access to query Tenant membership).
- Platform-wide/cross-tenant administration is explicitly modelled separately from tenant-scoped authorization.
- Identity permission claims must not be treated as the authoritative source of Tenant authorization.
- Protected operations must fail closed if Access authorization is unavailable or cannot establish a valid decision.
- Existing Identity RBAC data and services must not be deleted until a separately reviewed ownership-transfer and compatibility plan is approved.
- The dependency-registration mechanism is an implementation-design item to resolve; it is not invented here.
**Review trigger:** Evidence that Tenant–Access authorization integration cannot be established through Access public contracts alone, or that platform authorization semantics conflict with tenant-scoped authorization.

## Decision template
### D-XXX — Title
**Status:** Proposed / Accepted / Rejected / Superseded  
**Date:** YYYY-MM-DD  
**Decision:** One clear statement.  
**Context:** Why needed.  
**Alternatives:** Realistic options considered.  
**Consequences:** Product, security, data, compatibility, migration, and operational trade-offs.  
**Review trigger:** Evidence that would justify reconsideration.
