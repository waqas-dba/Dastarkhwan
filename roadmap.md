# Dastarkhwan / CoreKit Roadmap

## Objective and scope
Build a secure, reusable modular SaaS platform for food-item delivery and parcel pickup/delivery. Pakistan is the first market. Future markets, including the UK, should use market configuration and replaceable integrations rather than duplicated core logic. Current priority is backend development; frontend work comes later.

## Delivery rules
- Verify the actual latest commit and working-tree status before each task; `9555b8c` is the recorded M0 baseline, not necessarily today's HEAD.
- Inspect only relevant files. Do not assume a feature is missing without checking current code.
- Break work into small, independently reviewable micro-slices; one approved slice per request.
- Agreed workflow: DeepSeek Harness prepares plans and technical handovers; OpenCode + Nemotron implement approved slices; ChatGPT Account 2 independently reviews read-only; ChatGPT Account 1 coordinates and recommends acceptance; the user approves, verifies manually and controls all Git operations.
- Only one agent modifies the shared working tree at a time, and every participating agent verifies the same baseline SHA. Coordination rules: `ai-tasks.md` §5.
- AI may add/update tests but does not run tests/builds without explicit permission. User performs normal verification.
- AI does not execute migrations or modify databases without explicit permission.
- AI never performs Git write operations. It may suggest a manual checkpoint and prepare the commit message for the user.
- Update docs only when facts, decisions, or status change. Do not duplicate material across files.

## M0 — Repository baseline
**Status: Inspection reported complete at commit `9555b8c` (2026-10-23).** The baseline report is recorded in `current-state.md`. It is an inspection-only milestone; it does not mean later development work is complete. Before relying on it, verify current HEAD and relevant file changes.

**Done when:** solution/module structure, framework, persistence/migrations, Identity/Access, tenancy, tests, and key gaps are recorded with evidence and a safe next task.

## M1 — Essential architecture decisions
**Status: In progress — accepted decisions are recorded in `decisions.md`; implementation is tracked in `current-state.md` and is not implied by acceptance. S1 (documentation synchronization) remains in progress until implementation and review finish.**

Decision statements live in `decisions.md` and are not repeated here. Deferred decisions are listed in `current-state.md`.

**Done when:** accepted decisions are recorded accurately in `decisions.md` and `current-state.md` distinguishes decisions from implementation status.

## Near-term execution queue (one micro-slice per request)
Slices are prepared by DeepSeek Harness, implemented by OpenCode + Nemotron, independently reviewed by ChatGPT Account 2, coordinated by ChatGPT Account 1, and approved by the user. Verify the repository before each slice and obtain user approval before implementation or command execution.

| Slice | Task | Status | Boundary / completion evidence |
|---|---|---|---|
| S1 | Documentation only: synchronize `current-state.md` and `roadmap.md` with the accepted decisions and current evidence | In progress | This change; not complete until implementation and review finish |
| S2.1 | Create a dedicated `CoreKit.Access.Tests` project | Not started | Project exists and contains only Access-focused tests moved/created in a controlled, reviewed change. |
| S2.2 | Update architecture-test coverage/references for every intended module | Partial | Boundary rules cover SharedKernel, Identity, Access and Tenant with a documented temporary exception; tests not run |
| S3 | Add the Access migration | Not started | Files are generated only after explicit approval; do not apply to any database unless separately authorized. Local untracked migration files are reported as uncommitted, unreviewed and unapproved. |
| S4.1 | Inventory Tenant-to-Identity references and classify each | Reported / needs refresh | Inventory and classification reported; not yet recorded as an approved repository artifact |
| S4.2 | Define/approve SharedKernel contracts for the dependency | Not started | Contract ownership and direction are approved before code changes. |
| S4.3 | Remove Tenant's direct Identity dependency | Not started | Tenant still references Identity directly in `CoreKit.Tenant.csproj` |
| S5.1 | Inspect current Identity login/failed-attempt flow | Not started | Focused design for lockout, thresholds, duration, reset behavior, concurrency, and rate limiting. |
| S5.2 | Implement account lockout | Not started | No lockout fields or logic exist in `CoreKit.Identity` |
| S6.1 | Inventory role/permission ownership and dependencies | Reported / needs refresh | Inventory and dependency map reported; not yet recorded as an approved repository artifact |
| S6.2 | Move role ownership to Access | Not started | Identity still owns role/permission persistence; Identity remains responsible for identity/authentication when this starts |

Do not combine adjacent slices automatically. Stop after each slice and wait for user review/approval.

## M2 — Module foundation
Align module registration, public contracts, persistence ownership, configuration, errors, and test conventions. Remove circular dependencies and private cross-module database access. Existing SharedKernel and architecture-test projects are present. Boundary tests now cover SharedKernel, Identity, Access and Tenant, with Tenant → Identity as an explicit temporary exception (D-015); Access-focused tests still live inside the architecture test project (S2.1).

**Done when:** module dependency rules and test organization accurately cover the intended modules, with no circular/private persistence access.

## M3 — Identity
Identity owns accounts, authentication, credentials, sessions/tokens, lockout, and recovery. Reuse sound existing code; do not reimplement working flows unnecessarily. M0 reported Identity as partial, without lockout, and still containing roles/permissions contrary to the target boundary; at this snapshot no lockout fields or logic exist in `CoreKit.Identity`.

**Done when:** identity lifecycle, login failure/lockout, token issuance/rotation/revocation, and recovery paths have reviewed contracts and focused tests.

## M4 — Access
Access owns roles, permissions, assignments, and server-side authorization. It must not access Identity private tables or `DbContext`. M0 reported Access code exists but no migration was present at that baseline. At this snapshot Access services and tests exist, no Access migration is tracked, local untracked migration files remain unreviewed, and manual verification is pending.

**Done when:** role/permission ownership and authorization are implemented with positive and negative tests, including tenant/resource-scope cases, and persistence migration is reviewed.

## M5 — Tenant and Store
Tenant is implemented partially; M0 reported six code-file references from Tenant to Identity. Store had not started. Remove the Tenant-to-Identity dependency using approved contracts. Define Store separately and its relationship to Tenant; do not put Store behavior inside Tenant. At this snapshot `CoreKit.Tenant` still references `CoreKit.Identity` directly, so decoupling is incomplete (D-015 / S4.3).

**Done when:** Tenant is reusable and decoupled, Store ownership/lifecycle is explicit, tenant/store resolution fails closed, and tenant-owned access is enforced and tested.

## M6 — Domain routing
Map verified hostnames to the correct tenant/site/store. Verify custom-domain ownership if supported. Handle unknown, inactive, unverified, and conflicting domains safely. A hostname is never authorization.

**Done when:** routing behavior and spoofed/invalid-host cases are covered by focused tests.

## M7 — Store management and Catalog
Implement store profile/status/settings/branding and product/category/availability workflows. Enforce store ownership on protected operations.

**Done when:** public catalog and merchant management are correctly scoped and tested.

## M8 — Cart and Checkout
Confirm checkout grouping before implementation. Recalculate prices, discounts, fees, and availability server-side. Preserve currency and price snapshots. Do not assume multi-store checkout.

**Done when:** tampered totals, unavailable items, invalid scope, and checkout edge cases are addressed.

## M9 — Orders and Payments
Confirm initial payment methods and COD behavior before implementation. Define order/payment states, transitions, snapshots, provider adapters, signature validation, idempotency, and refunds/reconciliation as applicable.

**Done when:** retries, callbacks, state transitions, authorization, and transaction invariants are covered.

## M10 — Fulfillment
Implement approved food delivery and parcel pickup workflows. Keep fulfillment state separate from payment/order state; define assignment, cancellation, and updates only after product decisions are accepted.

**Done when:** transitions, assignment rules, and unauthorized updates are covered.

## M11 — API and Website
Build customer, merchant, and platform interfaces after backend contracts stabilize. Keep endpoints thin and enforce server-side security. Frontend is not the initial priority.

**Done when:** primary user journeys work against stable APIs and are documented/tested.

## M12 — Marketplace
Add store discovery after Store/Catalog contracts stabilize. Filter by public visibility, market, and availability. Do not accidentally introduce multi-store checkout.

**Done when:** results are paginated and private merchant/store data cannot leak.

## M13 — Operational readiness
Add only needed notifications, background jobs, audit/logging, health checks, backup/deployment guidance, and performance baselines.

**Done when:** operational checklist, recovery considerations, and known limitations are documented.

## Roadmap status rule
A roadmap item is not complete merely because it is described or a plan exists. Mark it complete only when implementation evidence and acceptance criteria are reviewed. Record actual code status in `current-state.md`; record decisions in `decisions.md`.
