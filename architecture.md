# Architecture — Dastarkhwan / CoreKit

## 1. Direction
Use a modular monolith initially. Split deployment only when measured performance, reliability, team ownership, or deployment needs justify it. Initial product scope is food-item delivery and parcel pickup/delivery in Pakistan. Backend development is the first priority; frontend work follows stable backend contracts.

This file describes the **target architecture**. `current-state.md` records what exists in code. Do not report a target rule as implemented unless verified in source and tests.

## 2. Logical modules and ownership
These are responsibilities, not a requirement to create every project immediately. Inspect existing code before adding projects or abstractions.

| Module | Owns | Does not own |
|---|---|---|
| SharedKernel | Small, stable cross-module primitives/contracts genuinely needed by multiple modules | Business workflows or private module persistence |
| Identity | Accounts, authentication, credentials, sessions/tokens, lockout, recovery | Business roles/permissions policy |
| Access | Roles, permissions, assignments, authorization contracts/policy/enforcement | Identity credentials or Identity private tables |
| Tenant | SaaS tenant lifecycle, tenant context/resolution contracts, tenant settings | Store/catalog/order rules; Identity implementation |
| Domain | Hostname ownership verification and hostname-to-tenant/site routing | Authorization or tenant membership policy |
| Store | Store profile, status, settings, branding, store ownership | Catalog pricing, orders, payments |
| Catalog | Products, categories, availability | Checkout totals/payment state |
| Cart & Checkout | Cart, checkout validation and calculation | Payment settlement |
| Orders | Order lifecycle, order lines, immutable price/currency snapshots | Provider internals or fulfillment execution |
| Payments | Payment attempts/state, provider adapters, callback validation | Catalog management |
| Fulfillment | Delivery/pickup tasks, assignment, fulfillment state | Payment settlement/pricing |
| Gateway API | HTTP composition, request validation, response mapping | Duplicated business rules/private persistence |
| Website/UI | Customer, merchant, platform interfaces | Replacement for server authorization |
| Marketplace | Public store discovery/listing | Private merchant data |
| Market configuration | Country/currency/address/tax/payment settings and adapters | Duplicated core business logic |
| Notifications | Workflow message delivery/preferences when justified | Owning the triggering transaction |

A capability may remain inside a well-defined module until splitting provides real value. Do not create all planned modules up front.

## 3. Dependency rules
1. Cross modules only through documented public contracts or documented events.
2. Never access another module's private `DbContext`, tables, repositories, or internal classes.
3. Avoid circular dependencies; redesign around stable contracts or orchestration when necessary.
4. Keep shared contracts small, stable, and genuinely shared. Do not turn SharedKernel into a dumping ground.
5. Keep endpoints thin and provider SDKs behind adapters.
6. Do not add event sourcing, distributed messaging, or microservices without a concrete need.
7. The module that owns a business rule owns its implementation and persistence.

## 4. Identity and Access boundary
- Identity authenticates a person/service and manages credentials, account state, sessions/tokens, lockout, and recovery.
- Access decides what an authenticated identity may do: roles, permissions, assignments, and authorization.
- Access must not read or write Identity private tables or use its private `DbContext`.
- Expose only the minimal stable identity identifier/claims contract required for authorization. Do not duplicate password, credential, or account lifecycle logic in Access.
- Identity must not remain the owner of business roles/permissions after the approved ownership transfer is complete.
- M0 reported that Identity still owned roles/permissions and lacked lockout. Those are implementation gaps, not changes to the target design.

## 5. Tenant and data isolation
- Tenant represents the SaaS account boundary; Store represents a merchant business. They are separate concepts.
- Tenant must not depend directly on Identity. Use stable SharedKernel contracts/abstractions for any required identity reference or authorization integration.
- The initial strategy is one shared PostgreSQL database. Tenant-owned rows carry explicit `TenantId` ownership, or an explicitly documented ownership path where direct `TenantId` is not appropriate.
- Apply EF Core global query filters as a default safety boundary, but **never treat filters as the only security control**. Enforce tenant scope in application services and authorization, and test cross-tenant denial.
- Tenant context must be derived/validated from trusted server-side context and authenticated membership. A client-supplied header such as `X-Tenant-ID` is not authorization by itself.
- Fail closed when tenant context is absent, invalid, inactive, or inconsistent with user membership.
- Scope reads, writes, exports, reports, background jobs, webhooks, and platform/admin actions according to an explicit authorization policy.
- Add tenant-safe unique constraints and indexes where required by the actual data model. Avoid assuming every table is tenant-owned.

## 6. Tenant–Access integration (Option B)
- Tenant requires Access for authorization of protected Tenant operations.
- Tenant owns tenant lifecycle, membership, tenant context and tenant-specific business rules.
- Access owns effective permissions and authorization decisions.
- Tenant may implement adapters for Access public abstractions (`IAccessScopeContext`, `IAccessMembership`, `IAccessService`, `IPermissionChecker`).
- `ITenantMembershipReader` (SharedKernel) is a candidate neutral bridge for Access to query Tenant membership.
- Access must not reference Tenant's private services, repositories, `DbContext` or tables.
- Tenant must not reference Identity private internals.
- Missing or invalid tenant scope, membership or authorization must fail closed.
- Tenant owner-to-Access owner mapping must be explicitly validated because Access owner semantics may grant broad permissions.
- Platform scope is distinct from tenant scope and needs an explicit authorization design.
- Tenant permission enforcement across individual operations remains implementation work, not an already completed capability.

**Current evidence note (latest verified local baseline `70d757fd`, 2026-11-13):** `DK-ACCESS-PLATFORM-SCOPE-01` introduced fixed platform-scope authorization for Access platform-membership add/revoke operations only; it is **Under review / Request Changes** and not accepted. It does not complete Tenant platform-authorization redesign, does not remove `TenantActor`/`ICurrentUser.Permissions` semantic coupling (D-015 outstanding coupling), and does not authorize removal of SharedKernel permission claims or Identity RBAC. Broader platform authorization and per-operation Tenant enforcement remain pending design/implementation under approved decisions.

Preserve the modular monolith architecture and existing approved module ownership boundaries.

## 7. SaaS and standalone composition
- **SaaS:** compose Tenant, Identity, and Access, plus the application modules required by the product. All applicable operations enforce tenant/store scope.
- **Standalone:** Tenant infrastructure is not required. Identity/Access may be optional only when a secure alternative is explicitly configured for protected operations.
- Keep host-specific composition at clear boundaries; do not scatter SaaS/standalone conditionals throughout unrelated modules.
- Create a separate `StandaloneStore` only if lifecycle/business rules materially differ. If only configuration differs, reuse Store with explicit host configuration.

## 8. Tenant, store, market, and currency
These are distinct concepts:
- Tenant: SaaS account boundary.
- Store: merchant business.
- Market: country/market rules and availability.
- Currency: transaction unit.
- Domain/hostname: routing input, not authorization.

Pakistan is the initial market. Future UK support should use market settings/adapters for address/postcode, payments, tax/invoicing, and locale. Verify current legal requirements before implementation. Persist explicit currency codes and appropriate decimal money values. Snapshot item prices, discounts, taxes, fees, and currency on orders; historical totals must not change when catalog settings change.

## 9. Authentication and session security
- Use short-lived JWT access tokens and rotating refresh tokens as accepted in `decisions.md`.
- Detect refresh-token reuse and revoke the affected token family/session according to the approved implementation design.
- Do not store plaintext refresh tokens; protect stored token material and revoke sessions securely.
- Lock an account for 15 minutes after 5 failed login attempts. Account lockout is not a replacement for IP/request rate limiting.
- Avoid leaking whether a particular account exists through login and recovery responses.
- Keep token contents minimal and avoid placing secrets or sensitive personal data in JWT claims.

## 10. Data and workflows
- Each module owns its schema and persistence mapping.
- Use database constraints and short transactions for important invariants.
- Do not hold a transaction open during slow external network calls.
- Use explicit orchestration and idempotency for cross-module workflows and callbacks.
- Add outbox/retry infrastructure only when justified.
- Plan migrations and backward compatibility before destructive schema changes. Review migration contents before applying them.
- Do not execute or apply migrations without explicit user authorization.

## 11. API and application design
Conceptual request path: `HTTP endpoint → authentication → authorization/context resolution → application use case → owning module/persistence → response`.

Use validation at boundaries, clear error contracts, bounded pagination, projections, async I/O, safe logging, and parameterized queries. Add layers only where they serve a real responsibility. Validate client-controlled prices, discounts, IDs, ownership, and state transitions on the server.

## 12. Security and operational controls
- Authorize every protected server operation and verify resource ownership.
- Enforce scope in list/detail/create/update/delete/export/report/job/webhook/admin paths.
- Validate webhook signatures and make handlers safe to retry.
- Keep secrets out of source control and logs; use least privilege and secure configuration.
- Add audit records for security-sensitive actions where appropriate, without logging credentials or tokens.
- Use safe errors; do not leak stack traces or internal details to clients.
- Use rate limits for abuse-prone endpoints and understand cache invalidation/scope.

## 13. Performance
Use async I/O, projections, bounded pagination, indexes based on real query patterns, and avoid N+1 queries/unbounded loads. Measure before caching or architectural changes. Cache keys/results must be user-, tenant-, and store-safe with clear invalidation. Use background processing for suitable long tasks, with retries and idempotency.

## 14. Decisions and evidence
Accepted decisions and open questions are in `decisions.md`. Current implementation status is in `current-state.md`. Roadmap milestones are not evidence of implementation. Verify the latest repository commit before each task and update architecture only when a decision or verified structure changes.
