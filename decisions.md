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
**Decision:** Work in small, independently reviewable micro-slices. AI may add/update focused tests as part of approved implementation tasks. Routine in-scope validation (relevant builds, automated tests, formatting, and static analysis) is covered by standing authorization within approved implementation tasks per D-017. Broader or expensive suites should be justified in the task/report; unrelated failures must not be fixed without approval.  
**Consequences:** One measurable outcome per task. Report exact commands, results, failures, and skipped tests. Documentation-only tasks must not run builds, tests, migrations, or scheduled checks unless specifically requested. D-017 governs routine automation boundaries and protected exceptions; separate approval remains required for actions outside approved scope.

## D-008 — Git remains user-controlled
**Status:** Accepted  
**Decision:** Git inspection commands (`git status`, `git log`, `git diff`, branch listing) are permitted without approval. Routine scoped Git operations covered by D-017 (stage only approved task files, commit after required validation/review, and push to the already-authorized branch within an approved implementation task) are authorized by standing authorization. Protected Git operations and repository-state changes outside that scope require separate explicit Project Owner approval before execution.  
**Consequences:** Standing authorization applies only within approved task scope/baseline/authorized branch and never includes unrelated/pre-existing changes. Separate explicit approval is always required for branch creation/switching, pull, merge, rebase, force-push, reset, clean, amend, cherry-pick, revert, history rewriting, and Git config/hook changes. For separately approved actions, explain purpose, exact command, benefits, risks/consequences, and affected branches/commits/remotes; asking for approval does not authorize execution. The user retains final authority over repository history and acceptance. See `ai-tasks.md` §15.2 and D-017.

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
**Supersession:** The SharedKernel-contracts aspect of this decision is superseded by D-018 (2026-11-15), which directs that no SharedKernel be introduced and that cross-module contracts live in module-owned `Abstractions` namespaces. The direct-dependency requirement (Tenant does not reference Identity) remains accepted and is met at project-reference level at baseline `1c832de`; re-verified at baseline `e2ca8c1` (2026-11-18), where no Identity-related semantic coupling remains in `TenantActor`, which consumes Tenant-owned `ITenantCurrentUser` and `ITenantPermissionChecker`.

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
**Supersession:** The SharedKernel-bridge example in this decision (SharedKernel as a candidate neutral bridge for `ITenantMembershipReader`) is superseded by D-018 (2026-11-15). Tenant→Access integration through Access public contracts (`IAccessScopeContext`, `IAccessMembership`, `IAccessService`, `IPermissionChecker`) remains accepted; `ITenantMembershipReader` lives in `CoreKit.Tenant.Abstractions`, not SharedKernel.  
**Review trigger:** Evidence that Tenant–Access authorization integration cannot be established through Access public contracts alone, or that platform authorization semantics conflict with tenant-scoped authorization.

## D-017 — Approved implementation-task automation (standing authorization)
**Status:** Accepted  
**Date:** 2026-11-13  
**Decision:** For each Project Owner–approved implementation task, the assigned implementation agent (OpenCode or Kilo, one at a time) has standing authorization to perform routine scoped development actions within that task without requesting command-by-command approval: inspect/read, implement approved scope, add/update focused tests, run relevant builds/tests/formatting/static analysis, fix implementation-related failures, review the diff, generate/inspect EF Core migrations and SQL scripts when the approved task changes a model, apply migrations to a positively identified development database and verify results when included in the approved task, and stage only approved task files, commit after required validation/review, and push to the already-authorized branch. The Project Owner explicitly authorized this routine automation for approved implementation tasks.
**Context:** Current documentation required per-command approval for many routine actions, creating inconsistent guidance and friction. A standard, auditable workflow for OpenCode and Kilo is needed while preserving safety boundaries.
**Consequences:**
- Standing authorization applies only within the explicitly approved task scope, baseline, and authorized branch; it does not authorize unrelated work or pre-existing/unrelated changes.
- Development database migration application requires positive identification of the target as a development database (never assumed from name/environment variable alone); migration names, target metadata (non-secret), and verification results must be reported.
- Separate explicit Project Owner approval remains required for: production database changes; database deletion/recreation/destructive reset; data deletion or irreversible transformations; unapproved migration rollback/downgrade; changes to database credentials or infrastructure; any operation with uncertain target or destructive impact; branch creation/switching, pull, merge, rebase, force-push, reset, clean, amend, cherry-pick, revert, or history rewriting; Git config/hook changes; and any action outside the approved scope.
- This decision reconciles routine command-approval language in D-007 and D-008 for actions covered above while preserving their protected-operation boundaries.
- Independent review requirements, final owner acceptance, and architecture/security approval boundaries remain unchanged.
- Weekly migration-drift monitoring is documented as a separate future read-only scheduled workflow and is not authorized to modify databases.
**Review trigger:** Evidence of unsafe automation, baseline/scope violations, incorrect database targeting, or need to narrow/expand standing authorization.

## D-018 — No SharedKernel; module-owned cross-module contracts; Identity RBAC data disposition
**Status:** Accepted  
**Date:** 2026-11-15  
**Decision:** `CoreKit.SharedKernel` is not introduced or populated as an architectural component and is not referenced by any project. Cross-module contracts live in module-owned `Abstractions` namespaces (`CoreKit.Access.Abstractions`, `CoreKit.Tenant.Abstractions`, etc.). The empty `CoreKit.SharedKernel/` folder (no `.csproj`, no source files) is historical evidence only. `CoreKit.Tenant` and `CoreKit.Identity` each reference only `CoreKit.Access`; `CoreKit.Access` is a leaf module.  
**Context:** The Project Owner's approved architecture direction is a modular monolith in which Identity owns accounts/authentication/credentials/sessions/lockout/recovery, Access exclusively owns roles/permissions/assignments/authorization, Tenant owns tenant lifecycle/membership/context and uses Access for authorization, Access must not access Identity private persistence, Tenant must not depend on Identity private implementation, and there is no SharedKernel and no SampleHost-as-future-requirement. The repository baseline `1c832de` confirms `CoreKit.SharedKernel/` has no project file or source and is unreferenced. This decision supersedes the SharedKernel-contract aspects of D-015 and D-016.  
**Alternatives considered:** (1) Populate SharedKernel with shared contracts — rejected, contradicts the Owner's "no SharedKernel" direction. (2) Retain D-015/D-016 SharedKernel bridge language as active guidance — rejected; D-015/D-016 remain historically traceable with original IDs and context, but their SharedKernel references are superseded by this decision.  
**Consequences:** D-015's direct-dependency requirement (Tenant does not reference Identity) is met at project-reference level (verified at baseline `1c832de`; re-verified at baseline `e2ca8c1` with no remaining Identity semantic coupling in `TenantActor`). D-016's Tenant→Access integration through Access public contracts stands; the SharedKernel bridge example in D-016 is superseded by module-owned `ITenantMembershipReader` in `CoreKit.Tenant.Abstractions`. The Project Owner confirmed there is no legacy Identity RBAC data to preserve and that the table-drop migration (`RemoveIdentityRbacTables`) removing Identity-owned roles/permissions tables is acceptable for the intended deployment. No data-transfer or legacy RBAC recovery requirement is added. D-03 (JWT revocation / `SecurityVersion` validation) and D-18 (rate limiting on tenant and access write endpoints) remain unresolved Project Owner decisions and are not resolved here.  

**Subsequent clarification — D-019 (2026-11-17):** D-019 supersedes the D-018 statement that Tenant references Access. Under the accepted independent-module architecture, Tenant has no direct CoreKit project references. `Dastarkhwan.Integration` composes Identity, Access and Tenant. D-018's No SharedKernel principle and module-owned contract ownership remain in force. D-016's earlier direct Tenant-to-Access integration approach is superseded by D-019's durable outbox and Access-owned membership projection design.  
**Review trigger:** Evidence that a genuine cross-module contract is required by multiple modules and has no module-owned home — any such contract must not be placed in SharedKernel without a separate Owner decision.

## D-019 — Option B independent-module architecture and Integration foundation
**Status:** Accepted  
**Date:** 2026-11-17  
**Decision:** `CoreKit.Identity`, `CoreKit.Access` and `CoreKit.Tenant` are independently reusable modules with zero direct CoreKit project references. `Dastarkhwan.Integration` references and coordinates the three modules through their public contracts. No SharedKernel. No cross-module adopter/plugin implementation pattern. No module accesses another module's private persistence. Tenant membership changes reach Access through the durable Tenant outbox and Access's membership-event consumer + projection, not through a direct Tenant-to-Access runtime membership lookup. Access uses its own membership projection as the authoritative Tenant membership source; missing, stale, quarantined or untrusted projection data fails closed.  
**Context:** The current Tenant-to-Access project reference and runtime adapter arrangement (`TenantAccessScopeContext`, `TenantAccessMembership` registered in `TenantServiceCollectionExtensions`) conflict with the approved independent-module direction. `Dastarkhwan.Integration` does not exist and must be created as the composition layer.  
**Alternatives considered:** (1) Retain Tenant→Access project reference with adapters — rejected, contradicts independent-module direction and D-018. (2) SharedKernel bridge — rejected, contradicts D-018. (3) Direct module-to-module runtime calls — rejected, violates "no module accesses another module's private persistence" and creates coupling.  
**Consequences:** Tenant project reference to Access is removed; Tenant's Access-owned interface implementations and direct Access registrations move to Integration; Access defaults to its own membership projection (fail-closed when untrusted); `ITenantOutboxTransport` and `ITenantProjectionGateway` implementations live in Integration; `Dastarkhwan.Integration` is created referencing all three modules. SampleHost is not part of the approved architecture, including during transition. The future web-related host will be handled as a separate application project.  

**Implementation status clarification (source inspected at `c4afe1e`):** The independent project-reference boundary is present for Tenant, and `Dastarkhwan.Integration` references Identity, Access and Tenant. The Tenant outbox and Access membership projection components are present. However, `Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs` still registers `TenantAccessMembership` as an `IAccessMembershipAdapter`, which reads Tenant membership directly. This conflicts with D-019's projection-authoritative requirement. D-019 remains the accepted target architecture, but this implementation discrepancy must be resolved before claiming full implementation.  
**Supersession:** Supersedes the D-018 statement that "`CoreKit.Tenant` and `CoreKit.Identity` each reference only `CoreKit.Access`" as it applies to both Tenant and Identity. D-018's No SharedKernel and module-owned contracts principles remain in force.  
**Review trigger:** Evidence that the approved dependency graph cannot be achieved without a security or architecture decision not already approved; evidence that Access projection-based authorization cannot satisfy fail-closed requirements.

## Decision template
### D-XXX — Title
**Status:** Proposed / Accepted / Rejected / Superseded  
**Date:** YYYY-MM-DD  
**Decision:** One clear statement.  
**Context:** Why needed.  
**Alternatives:** Realistic options considered.  
**Consequences:** Product, security, data, compatibility, migration, and operational trade-offs.  
**Review trigger:** Evidence that would justify reconsideration.
