# Dastarkhwan / CoreKit Roadmap

## Objective and scope
Build a secure, reusable modular SaaS platform for food-item delivery and parcel pickup/delivery. Pakistan is the first market. Future markets, including the UK, should use market configuration and replaceable integrations rather than duplicated core logic. Current priority is backend development; frontend work comes later.

## Delivery rules
- Verify the actual latest commit and working-tree status before each task; `9555b8c` is the recorded M0 baseline, not necessarily today's HEAD.
- Inspect only relevant files. Do not assume a feature is missing without checking current code.
- Break work into small, independently reviewable micro-slices; one approved slice per request.
- Account 1 implements; Account 2 independently reviews read-only. Only one AI modifies the working tree at a time.
- AI may add/update tests but does not run tests/builds without explicit permission. User performs normal verification.
- AI does not execute migrations or modify databases without explicit permission.
- AI never performs Git write operations. It may suggest a manual checkpoint and prepare the commit message for the user.
- Update docs only when facts, decisions, or status change. Do not duplicate material across files.

## M0 — Repository baseline
**Status: Inspection reported complete at commit `9555b8c` (2026-10-23).** The baseline report is recorded in `docs/current-state.md`. It is an inspection-only milestone; it does not mean later development work is complete. Before relying on it, verify current HEAD and relevant file changes.

**Done when:** solution/module structure, framework, persistence/migrations, Identity/Access, tenancy, tests, and key gaps are recorded with evidence and a safe next task.

## M1 — Essential architecture decisions
**Status: Three decisions accepted; documentation synchronization is the next small task.**
- Short-lived JWT access tokens, rotating refresh tokens, refresh-token reuse detection/revocation, and lockout after 5 failed attempts for 15 minutes.
- One shared PostgreSQL database; tenant-owned records have explicit `TenantId` ownership and EF Core global query filters, with application-service and authorization enforcement as additional boundaries.
- Remove Tenant's direct dependency on Identity through stable SharedKernel contracts/abstractions.

Deferred until needed by the relevant milestone: checkout grouping, parcel pickup initiation details, payment methods/COD, operational currency/market rules beyond the accepted Pakistan-first direction, and website/domain strategy.

**Done when:** accepted decisions are recorded accurately in `docs/decisions.md` and `docs/current-state.md` distinguishes decisions from implementation status.

## Near-term execution queue (one micro-slice per request)
These are the proposed order from the M0 report. Verify the repository before each slice and obtain user approval before implementation or command execution.

| Slice | Task | Boundary / completion evidence |
|---|---|---|
| S1 | Documentation only: record the three accepted M1 decisions and update current-state | Decisions have Accepted status; current-state preserves M0 evidence and clearly labels the baseline commit. |
| S2.1 | Create a dedicated `CoreKit.Access.Tests` project | Project exists and contains only Access-focused tests moved/created in a controlled, reviewed change. |
| S2.2 | Update architecture-test coverage/references for every intended module | Architecture rules cover the actual module set; no project references are added speculatively. |
| S3 | Add the Access migration | Migration files are generated only after explicit approval; do not apply to any database unless separately authorized. |
| S4.1 | Inventory Tenant-to-Identity references and classify each | Exact six previously reported code-file references are rechecked against current HEAD; list evidence and intended replacement contract. |
| S4.2 | Define/approve SharedKernel contracts for the dependency | Contract ownership and direction are approved before code changes. |
| S4.3 | Remove Tenant's direct Identity dependency | Tenant compiles conceptually against stable abstractions; no unrelated refactor. User authorizes any build/test commands separately. |
| S5.1 | Inspect current Identity login/failed-attempt flow | Focused design for lockout, thresholds, duration, reset behavior, concurrency, and rate limiting. |
| S5.2 | Implement account lockout | Five failed attempts trigger a 15-minute lockout; focused tests added but not run by AI. |
| S6.1 | Inventory role/permission ownership and dependencies | Identify all Identity-owned role/permission code, tables, contracts, and consumers. |
| S6.2 | Move role ownership to Access | Access owns role/permission persistence and behavior; Identity remains responsible for identity/authentication. Migration/data compatibility plan approved. |

Do not combine adjacent slices automatically. Stop after each slice and wait for user review/approval.

## M2 — Module foundation
Align module registration, public contracts, persistence ownership, configuration, errors, and test conventions. Remove circular dependencies and private cross-module database access. Existing SharedKernel and architecture-test projects are present per the M0 report; Access tests currently sit inside the architecture test project and its references cover only Access.

**Done when:** module dependency rules and test organization accurately cover the intended modules, with no circular/private persistence access.

## M3 — Identity
Identity owns accounts, authentication, credentials, sessions/tokens, lockout, and recovery. Reuse sound existing code; do not reimplement working flows unnecessarily. M0 reported Identity as partial, without lockout, and still containing roles/permissions contrary to the target boundary.

**Done when:** identity lifecycle, login failure/lockout, token issuance/rotation/revocation, and recovery paths have reviewed contracts and focused tests.

## M4 — Access
Access owns roles, permissions, assignments, and server-side authorization. It must not access Identity private tables or `DbContext`. M0 reported Access code exists but no migration was present at that baseline.

**Done when:** role/permission ownership and authorization are implemented with positive and negative tests, including tenant/resource-scope cases, and persistence migration is reviewed.

## M5 — Tenant and Store
Tenant is implemented partially; M0 reported six code-file references from Tenant to Identity. Store had not started. Remove the Tenant-to-Identity dependency using approved contracts. Define Store separately and its relationship to Tenant; do not put Store behavior inside Tenant.

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
A roadmap item is not complete merely because it is described or a plan exists. Mark it complete only when implementation evidence and acceptance criteria are reviewed. Record actual code status in `docs/current-state.md`; record decisions in `docs/decisions.md`.
