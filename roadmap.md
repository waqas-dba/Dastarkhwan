# Dastarkhwan / CoreKit Roadmap

## Objective
Build a secure SaaS platform with independent branded store websites and a central marketplace. Initial scope: food-item delivery and parcel pickup/delivery in Pakistan. Future countries should use market configuration and replaceable integrations, not duplicated core logic.

## Delivery rules
- Work one small, reviewable milestone at a time.
- Inspect the repository before assuming a feature is missing.
- Reuse verified code; no unrelated refactoring.
- AI may add/update tests but does not run them. User verifies.
- AI never stages, commits, pushes, changes branches, or alters Git history.
- Update current-state/decision docs when facts or decisions change.

## M0 — Repository baseline
Inspect solution/projects, target frameworks, startup/DI, modules, database/migrations, authentication, authorization, tenancy, APIs, tests, configuration, and docs. Record verified facts, unknowns, risks, and the smallest next task.

**Done when:** current-state reflects evidence and a safe next step is identified.

## M1 — Confirm essential decisions
Confirm checkout grouping, parcel pickup initiation, authentication/session approach, tenant data isolation, initial payment methods/COD, market/currency rules, and website/domain strategy as needed.

**Done when:** decisions required for the next task are accepted and recorded.

## M2 — Module foundation
Align module registration, public contracts, persistence ownership, configuration, errors, and test conventions. Ensure no circular dependencies or private cross-module database access.

**Done when:** one existing or minimal example follows the agreed pattern.

## M3 — Identity
Secure account lifecycle, authentication, credentials, sessions/tokens, and recovery as required. Reuse existing implementation where sound.

**Done when:** key success/failure paths and sensitive handling have focused tests.

## M4 — Access
Implement roles, permissions, assignments, and server-side authorization. Keep Identity private persistence inaccessible.

**Done when:** positive and negative authorization tests are added, including cross-scope cases.

## M5 — Tenant and Store
Define tenant/store relationships and lifecycle. Resolve context from trusted server-side context and fail closed. Scope every applicable query and mutation.

**Done when:** ownership rules, constraints, and isolation tests exist.

## M6 — Domain routing
Map approved hostnames to the right site/store. Verify custom-domain ownership if supported. Handle unknown, inactive, unverified, or conflicting domains safely.

**Done when:** routing tests cover invalid/spoofed hostnames; hostname is never treated as authorization.

## M7 — Store management and Catalog
Support store profile/branding and product/category/availability flows. Enforce store ownership on every protected read/write.

**Done when:** public catalog and merchant management are scoped and tested.

## M8 — Cart and Checkout
Recalculate prices, discounts, fees, and availability server-side. Confirm single-store or multi-store semantics first. Validate market/currency and preserve snapshots.

**Done when:** tampered totals, invalid availability, and checkout edge cases are tested.

## M9 — Orders and Payments
Define explicit order/payment states, transitions, snapshots, initial payment methods, and validated idempotent callbacks.

**Done when:** transactions, retries, state changes, and authorization are covered.

## M10 — Fulfillment
Implement the agreed delivery and parcel pickup flows. Keep fulfillment state separate from order/payment state; authorize assignment and updates.

**Done when:** transitions and unauthorized updates are tested.

## M11 — API and Website
Build customer, merchant, and platform experiences with thin endpoints, clear contracts, validation, pagination, and server-side security.

**Done when:** primary flows and security rules are documented/tested.

## M12 — Marketplace
Add store discovery after store/catalog contracts stabilize. Filter by public visibility, market, and availability. Do not introduce multi-store checkout accidentally.

**Done when:** results are paginated and private store data cannot leak.

## M13 — Operational readiness
Add only needed notifications, background jobs, audit/logging, health checks, backup/deployment guidance, and performance baselines.

**Done when:** operational checklist and known limitations are documented.

## Start here
Begin with M0. This roadmap describes intended work, not proof that any feature is absent. Do not build all modules upfront.
