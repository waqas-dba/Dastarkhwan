# Architecture and Product Decisions

Use this log for decisions affecting product behavior, security, data, compatibility, or module boundaries. Never silently convert an assumption into an accepted decision.

**Statuses:** Proposed, Accepted, Rejected, Superseded.

## D-001 — Modular monolith
**Status:** Accepted  
**Decision:** Start as a modular monolith; split deployment only when measured needs justify it.  
**Consequence:** Clear contracts, module-owned persistence, no circular dependencies or premature distributed infrastructure.

## D-002 — Identity and Access are separate
**Status:** Accepted  
**Decision:** Identity owns accounts/authentication/credentials/sessions. Access owns roles/permissions/authorization.  
**Consequence:** Access may use public Identity contracts but never Identity private tables or `DbContext`.

## D-003 — SaaS and standalone composition
**Status:** Accepted  
**Decision:** SaaS requires Tenant, Identity, Access. Standalone does not require Tenant; Identity/Access may be optional only when a secure alternative is configured for protected operations.  
**Consequence:** Separate `StandaloneStore` only when business rules/lifecycle materially differ; configuration-only differences should reuse Store.

## D-004 — Initial product scope
**Status:** Accepted  
**Decision:** Food-item delivery and parcel pickup/delivery. Do not add hotels or unrelated verticals without an explicit decision.  
**Consequence:** Keep MVP focused; introduce future verticals only when requested.

## D-005 — Country/market extensibility
**Status:** Accepted  
**Decision:** Pakistan first; future countries (including UK) through market configuration and replaceable integrations.  
**Consequence:** Keep tenant, store, market, and currency distinct. Verify country-specific legal/tax/payment rules before implementation.

## D-006 — Preserve transaction snapshots
**Status:** Accepted  
**Decision:** Orders preserve currency and price-related snapshots, including applicable discounts, taxes, and fees.  
**Consequence:** Historical totals must not change when catalog settings change; use explicit currency codes and decimal money values.

## D-007 — Small milestones and user verification
**Status:** Accepted  
**Decision:** Work in small, reviewable tasks. AI can add/update tests but does not run them; the user verifies.  
**Consequence:** Report tests as “added/updated, not run” unless results are observed.

## D-008 — Git remains user-controlled
**Status:** Accepted  
**Decision:** AI does not stage, commit, push, change branches, or rewrite history.  
**Consequence:** AI may recommend a checkpoint and provide a commit message for the user.

## D-009 — Checkout grouping
**Status:** Proposed  
**Decision to confirm:** Start with single-store checkout unless multi-store checkout is explicitly required.  
**Reason:** Simpler order ownership, delivery fees, refunds, payment allocation, fulfillment, and merchant access.  
**Question:** Does one customer checkout need to contain products from multiple stores?

## D-010 — Parcel pickup initiation
**Status:** Proposed  
**Decision to confirm:** Is pickup initiated by a customer, a merchant, or both? Define addresses, pricing, assignment, cancellation, and states.

## D-011 — Tenant data isolation
**Status:** Proposed  
**Decision to confirm:** Select shared database with strict tenant scoping vs database-per-tenant after inspecting current implementation and operations.  
**Compare:** Cost, backup/restore, migration complexity, security risk, scale, and compatibility. Authorization is required either way.

## D-012 — Authentication/session approach
**Status:** Proposed  
**Decision to confirm:** Inspect existing implementation before replacing/selecting auth/session/token mechanisms. Consider browser/API clients, revocation, recovery, CSRF/XSS, and operations.

## D-013 — Initial payments
**Status:** Proposed  
**Decision to confirm:** Launch payment methods, including COD. Verify provider availability, signatures, idempotency, refunds, reconciliation, and currency support.

## D-014 — Website and custom domains
**Status:** Proposed  
**Decision to confirm:** Website technology and operations for DNS ownership verification, hostname routing, HTTPS provisioning/renewal, and failures. A hostname is routing input, not authorization.

## Decision template
### D-XXX — Title
**Status:** Proposed / Accepted / Rejected / Superseded  
**Date:** YYYY-MM-DD  
**Decision:** One clear statement.  
**Context:** Why needed.  
**Alternatives:** Realistic options.  
**Consequences:** Trade-offs, security, migration, and operations.  
**Review trigger:** Evidence that would justify reconsideration.
