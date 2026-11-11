# Dastarkhwan / CoreKit Roadmap

## Objective and scope

Build a secure, reusable modular SaaS platform for food-item delivery and parcel pickup/delivery. Pakistan is the first market. Future markets, including the UK, should use market configuration and replaceable integrations rather than duplicated core logic. Current priority is backend development; frontend work comes later.

## Delivery rules

- Verify the actual latest commit and local working-tree status before each task. `9555b8c` is the historical M0 baseline, not the current HEAD.
- Inspect only relevant files. Do not assume a feature is missing without checking current code.
- Break work into small, independently reviewable micro-slices; one approved slice per request.
- Current agreed workflow: OpenCode prepares plans and technical handovers; OpenCode implements approved slices; ChatGPT Account 2 independently reviews read-only; ChatGPT Account 1 coordinates and recommends acceptance; the user approves, verifies manually and controls all Git operations.
- Only one agent modifies the shared working tree at a time. Every participating agent verifies and reports its own baseline SHA.
- `CLAUDE.md`, `ai-tasks.md` and `AGENTS.md` state the current agent allocation and the mandatory task briefing standard (`ai-tasks.md` §13). Verify them before relying on role assignments.
- **Implementation tasks:** relevant builds and automated tests may be run after implementation when explicitly included in the approved task. The agent must report exact commands, results, failures and skipped tests. Broader or expensive test suites require justification and approval where appropriate. Do not fix unrelated failures without approval. **Documentation-only tasks:** no builds or tests unless specifically requested.
- AI does not execute migrations or modify databases without explicit permission. Creating migration files also requires approval for that specific slice.
- **Git:** inspection commands are permitted without approval. Git operations that change repository state require explicit Project Owner approval before execution. AI never performs Git write operations without such approval. The user alone stages, commits, pushes, merges and controls repository history.
- Update documentation only when facts, decisions or status change. Do not duplicate material across files.

## M0 — Repository baseline

**Status: Reported complete (not independently verified) at historical commit `9555b8c` (2026-10-23).**

The M0 report is recorded in `current-state.md`. It was an inspection-only milestone and does not mean later development work is complete. Verify current HEAD and relevant file changes before relying on historical findings.

**Done when:** solution/module structure, framework, persistence/migrations, Identity/Access, tenancy, tests and key gaps are recorded with evidence and a safe next task.

## M1 — Essential architecture decisions

**Status: In progress — accepted decisions are recorded in `decisions.md`; implementation is tracked in `current-state.md` and is not implied by acceptance. S1 documentation synchronization remains in progress until implementation and review finish.**

Decision statements live in `decisions.md` and are not repeated here. Deferred decisions are listed in `current-state.md`.

**Done when:** accepted decisions are recorded accurately in `decisions.md` and `current-state.md` distinguishes decisions from implementation status.

## Near-term execution queue (one micro-slice per request)

Slices are prepared by OpenCode, implemented by OpenCode, independently reviewed by ChatGPT Account 2, coordinated by ChatGPT Account 1, and approved by the user. Verify the actual repository and local working tree before each slice. Obtain explicit user approval before implementation or any state-changing command.

| Slice | Task | Status | Boundary / completion evidence |
|---|---|---|---|
| S1 | Documentation only: synchronize `current-state.md` and `roadmap.md` with accepted decisions, current evidence and current agent allocation | Under review | Documentation changes applied and committed at `45a5789`. Initial review identified documentation corrections; correction task `DK-TENANT-ACCESS-DOCS-FIX1` addresses them. Independent review and Project Owner acceptance remain pending. |
| S2.1 | Create a dedicated `CoreKit.Access.Tests` project | Under review | `CoreKit.Access.Tests` exists with its Access-focused suites (created in `470d42c`). Independent review and user-run tests pending. File presence alone is not completion. |
| S2.2 | Update architecture-test coverage and references for intended modules | In progress | Boundary rules cover SharedKernel, Identity, Access and Tenant, with a documented temporary exception. Boundary enforcement now evaluates actual MSBuild project references (imports included), validates solution inventory/classification and the IVT policy. Tests have not been run as part of this documentation task. |
| S3 | Review the Access migration | Under review | Access migration files are committed under `CoreKit.Access/Migrations/` at `43c2179`. Review schema, constraints, relationships and compatibility. Database application requires separate explicit authorization. |
| S4.1 | Inventory Tenant-to-Identity references and classify each | Reported complete (not independently verified) | Previously reported inventory requires refresh against the current implementation and an approved artifact. |
| S4.2 | Tenant–Access foundation: establish public contract integration, tenant scope context, membership adapter, safe DI composition and fail-closed behavior | Pending | Access public contracts (`IAccessScopeContext`, `IAccessMembership`, `IAccessService`, `IPermissionChecker`) are available. Tenant adapter and DI composition not yet implemented. |
| S4.3 | Platform authorization: define and implement explicit Access-owned platform/cross-tenant authorization | Pending | Platform authorization design not yet established. Must not be combined with tenant-scoped permission enforcement. |
| S4.4 | Tenant permission enforcement: enforce named Access permissions across protected Tenant operations | Pending | Per-operation permission enforcement not yet implemented. Fail-closed behavior for missing/invalid authorization must be established. |
| S5.1 | Inspect current Identity login and failed-attempt flow | Pending | Produce a focused design covering thresholds, duration, reset behavior, concurrency and rate limiting. |
| S5.2 | Implement account lockout | Pending | Implement the accepted five-failed-attempt, fifteen-minute policy with focused tests. Tests are not run by AI unless separately authorized. |
| S6.1 | Inventory role and permission ownership and dependencies | Reported complete (not independently verified) | Previously reported inventory and dependency map require refresh against current source and an approved artifact. |
| S6.2 | Move role ownership to Access | Pending | Access owns role/permission persistence and behavior; Identity remains responsible for identity and authentication. Approve migration and data-compatibility strategy before implementation. |

### Migration note

The commit `43c2179` includes migration artifacts for Access, Identity and Tenant. Their committed presence does not establish successful migration execution, database compatibility, or production readiness. Do not apply them to any database without separate explicit user authorization.

Do not combine adjacent slices automatically. Stop after each slice and wait for user review and approval.

## M2 — Module foundation

Align module registration, public contracts, persistence ownership, configuration, errors and test conventions. Remove circular dependencies and private cross-module database access.

Boundary tests cover SharedKernel, Identity, Access and Tenant, with Tenant → Identity as an explicit temporary exception under D-015. Access suites now live in `CoreKit.Access.Tests` (S2.1 under review); boundary enforcement uses evaluated project references (S2.2 in progress).

**Done when:** module dependency rules and test organization accurately cover the intended modules, with no circular or private persistence access.

## M3 — Identity

Identity owns accounts, authentication, credentials, sessions/tokens, lockout and recovery. Reuse sound existing code; do not reimplement working flows unnecessarily.

The historical M0 report found Identity to be partial, without lockout, and still containing roles/permissions contrary to the target boundary. Current completion of lockout and role/permission ownership transfer has not been established.

**Done when:** identity lifecycle, login failure/lockout, token issuance/rotation/revocation and recovery paths have reviewed contracts and focused tests.

## M4 — Access

Access owns roles, permissions, assignments and server-side authorization. It must not access Identity private tables or `DbContext`.

Access implementation exists. Migration artifacts are committed at `43c2179` and require review. Their presence does not prove schema correctness, database compatibility or successful application. Access test suites were separated into `CoreKit.Access.Tests` (`470d42c`). Role ownership transfer remains a separate task.

**Done when:** role/permission ownership and authorization are implemented with positive and negative tests, including tenant/resource-scope cases, and persistence migration is reviewed.

## M5 — Tenant and Store

Tenant represents the SaaS account boundary; Store represents a merchant business. They are separate concepts.

No direct Tenant-to-Identity project reference was found at the inspected source baseline (`5dfa796c`); D-015's direct-dependency requirement is met at project-reference level. Remaining Identity semantic coupling persists in `TenantActor` through `ICurrentUser.Permissions` and must be classified before removal. Tenant-to-Access authorization integration remains Pending under D-016. Existing Identity RBAC ownership and transition require separate review. Define Store separately and its relationship to Tenant; do not put Store behavior inside Tenant.

**Done when:** Tenant is reusable and decoupled, Store ownership/lifecycle is explicit, tenant/store resolution fails closed, and tenant-owned access is enforced and tested.

## M6 — SaaS Subscription (`CoreKit.Subscription`)

**Status: Pending**

**Priority: Required core platform capability.**

Implement subscription management for restaurant and store tenants using the Dastarkhwan SaaS platform.

**Scope:**
- Subscription plans, pricing and plan configuration.
- Tenant subscription lifecycle: trial, active, suspended, cancelled and expired.
- Subscription start, renewal, expiry and cancellation.
- Plan features, entitlements and configurable usage limits.
- Server-side subscription and entitlement enforcement.
- Subscription history and status transitions.
- Subscription billing records.
- Payment-provider integration through approved contracts.

**Architecture boundaries:**
- Implement as a separate reusable `CoreKit.Subscription` module.
- Keep subscription management separate from Identity, Access and Tenant responsibilities.
- Integrate through approved public contracts; never access another module's private tables or `DbContext`.
- Tenant subscription billing must remain separate from customer food-order payments handled by M10.
- Support replaceable payment-provider integrations and market-specific configuration.
- Do not introduce a specific payment provider without a separate approved decision.

**Dependencies:** M5 Tenant and Store foundations.

**Done when:** plan management, subscription lifecycle, entitlements, usage limits and tenant-level enforcement have reviewed contracts, focused tests, documented failure behavior and independent security review.

## M7 — Domain routing

Map verified hostnames to the correct tenant/site/store. Verify custom-domain ownership if supported. Handle unknown, inactive, unverified and conflicting domains safely. A hostname is never authorization.

**Done when:** routing behavior and spoofed/invalid-host cases are covered by focused tests.

## M8 — Store management and Catalog

Implement store profile/status/settings/branding and product/category/availability workflows. Enforce store ownership on protected operations.

**Done when:** public catalog and merchant management are correctly scoped and tested.

## M9 — Cart and Checkout

Confirm checkout grouping before implementation. Recalculate prices, discounts, fees and availability server-side. Preserve currency and price snapshots. Do not assume multi-store checkout.

**Done when:** tampered totals, unavailable items, invalid scope and checkout edge cases are addressed.

## M10 — Orders and Payments

Confirm initial payment methods and COD behavior before implementation. Define order/payment states, transitions, snapshots, provider adapters, signature validation, idempotency, and refunds/reconciliation as applicable.

**Done when:** retries, callbacks, state transitions, authorization and transaction invariants are covered.

## M11 — Fulfillment

Implement approved food delivery and parcel pickup workflows. Keep fulfillment state separate from payment/order state; define assignment, cancellation and updates only after product decisions are accepted.

**Done when:** transitions, assignment rules and unauthorized updates are covered.

## M12 — API and Website

Build customer, merchant and platform interfaces after backend contracts stabilize. Keep endpoints thin and enforce server-side security. Frontend is not the initial priority.

**Done when:** primary user journeys work against stable APIs and are documented/tested.

## M13 — Marketplace

Add store discovery after Store/Catalog contracts stabilize. Filter by public visibility, market and availability. Do not accidentally introduce multi-store checkout.

**Done when:** results are paginated and private merchant/store data cannot leak.

## M14 — Operational readiness

Add only needed notifications, background jobs, audit/logging, health checks, backup/deployment guidance and performance baselines.

**Done when:** operational checklist, recovery considerations and known limitations are documented.

## Roadmap status rule

A roadmap item is not complete merely because it is described or a plan exists. Mark it complete only when implementation evidence and acceptance criteria are reviewed. Record actual code status in `current-state.md`; record decisions in `decisions.md`.