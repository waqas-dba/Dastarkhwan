# Architecture — Dastarkhwan / CoreKit

## Direction
Use a modular monolith initially. Split deployment only when measured performance, reliability, team ownership, or deployment needs justify it. Product scope begins with food-item delivery and parcel pickup/delivery in Pakistan.

## Logical modules
These are responsibilities, not a requirement to create every project immediately. Map existing code first.

| Module | Owns | Does not own |
|---|---|---|
| Identity | Accounts, authentication, credentials, sessions/tokens, recovery | Business permission policy |
| Access | Roles, permissions, assignments, authorization contracts | Identity private tables/credentials |
| Tenant | SaaS tenant lifecycle/context contract | Store/catalog/order rules |
| Domain | Hostname verification and hostname-to-site routing | Authorization |
| Store | Store profile, status, settings, branding | Catalog pricing, orders, payments |
| Catalog | Products, categories, availability | Checkout totals/payment state |
| Cart & Checkout | Cart, checkout validation and calculation | Payment settlement |
| Orders | Order lifecycle, order lines, immutable price/currency snapshots | Provider internals/fulfillment execution |
| Payments | Payment attempts/state, provider adapters, callback validation | Catalog management |
| Fulfillment | Delivery/pickup tasks, assignment, fulfillment state | Payment settlement/pricing |
| Gateway API | HTTP composition, request validation, response mapping | Duplicated business rules/private persistence |
| Website/UI | Customer, merchant, platform interfaces | Replacement for server authorization |
| Marketplace | Public store discovery/listing | Private merchant data |
| Market configuration | Country/currency/address/tax/payment settings and adapters | Duplicated core business logic |
| Notifications | Workflow message delivery/preferences when justified | Owning the triggering transaction |

A capability may stay inside a well-defined module until splitting provides real value. Avoid a generic plugin system or unnecessary shared library.

## Dependency rules
1. Cross modules only through documented public contracts or documented events.
2. Never access another module's private `DbContext`, tables, repositories, or internal classes.
3. Avoid circular dependencies; redesign around stable contracts/orchestration when necessary.
4. Keep endpoints thin and provider SDKs behind adapters.
5. Keep shared code small and genuinely shared.
6. Do not add event sourcing, distributed messaging, or microservices without a concrete need.

## SaaS and standalone composition
- **SaaS:** Tenant, Identity, and Access are required; all applicable operations enforce tenant/store scope.
- **Standalone:** Tenant infrastructure is not required. Identity/Access may be optional only when a secure alternative is explicitly configured for protected operations.
- Use a separate `StandaloneStore` module only if lifecycle/business rules materially differ. If only configuration differs, reuse Store with explicit host configuration.
- Keep host-specific composition at clear boundaries; do not scatter SaaS/standalone conditionals throughout unrelated modules.

## Tenant, store, market, currency
These are separate concepts: Tenant is the SaaS account boundary; Store is a merchant business; Market/country controls market rules; Currency is the transaction unit. Hostname is routing input, not authorization.

Pakistan is the initial market. Future UK support should use market settings/adapters for address/postcode, payments, tax/invoicing, and locale. Verify legal requirements before implementing them.

Persist explicit currency codes and appropriate decimal money values. Snapshot item prices, discounts, taxes, and fees on orders. Never recompute historical order totals from current catalog settings. Validate compatibility between store, market, currency, and payment method.

## Data and workflows
- Each module owns its schema and persistence mapping.
- Use database constraints and short transactions for important invariants.
- Do not hold a transaction open during slow external network calls.
- Use explicit orchestration and idempotency for cross-module workflows.
- Add outbox/retry infrastructure only when justified.
- Plan migrations and backward compatibility before destructive schema changes.

## Security
- Authentication identifies the caller; authorization grants actions.
- Authorize every protected server operation and verify resource ownership.
- Enforce scope in list/detail/update/delete/export/report/job/webhook/admin paths.
- Fail closed for missing/conflicting tenant, domain, permission, or resource context.
- Validate webhook signatures and make handlers safe to retry.
- Keep secrets out of source control/logs; use least privilege, input validation, parameterized queries, safe errors, and audit sensitive actions.

## Performance
Use async I/O, projections, bounded pagination, indexes based on real query patterns, and avoid N+1 queries/unbounded loads. Measure before caching or architectural changes. Cache keys/results must be tenant/store-safe with clear invalidation. Use background processing for suitable long work, with retries and idempotency. Rate-limit expensive/abuse-prone endpoints.

## Request path
Conceptually: `HTTP endpoint → authentication → authorization/context resolution → application use case → owning module/persistence → response`. Add layers only where they serve a real responsibility.

## Decisions needing confirmation
See `docs/decisions.md` for checkout grouping, pickup initiation, auth/session design, data isolation, payment methods, website technology, domain/HTTPS automation, and distribution needs.
