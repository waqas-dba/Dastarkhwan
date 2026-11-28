# Architecture — Dastarkhwan / CoreKit

## 1. Direction
Use a modular monolith initially. Split deployment only when measured performance, reliability, team ownership, or deployment needs justify it. Initial product scope is food-item delivery and parcel pickup/delivery in Pakistan. Backend development is the first priority; frontend work follows stable backend contracts.

This file describes the **target architecture**. `current-state.md` records what exists in code. Do not report a target rule as implemented unless verified in source and tests.

## 2. Logical modules and ownership
These are responsibilities, not a requirement to create every project immediately. Inspect existing code before adding projects or abstractions.

| Module | Owns | Does not own |
|---|---|---|
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

**SharedKernel:** No active `CoreKit.SharedKernel` project, source files or project references are present in the committed repository. SharedKernel is not part of the approved architecture. Cross-module contracts remain owned by their respective modules. Do not introduce or populate SharedKernel.

A capability may remain inside a well-defined module until splitting provides real value. Do not create all planned modules up front.

## 3. Dependency rules
1. **No CoreKit module references another CoreKit module.** `CoreKit.Identity`, `CoreKit.Access` and `CoreKit.Tenant` are independently reusable class libraries with zero direct CoreKit project references.
2. **Cross modules only through documented public contracts or documented events.** `Dastarkhwan.Integration` references and coordinates the three modules through their public contracts; it is the only assembly that composes them.
3. Never access another module's private `DbContext`, tables, repositories, or internal classes.
4. Avoid circular dependencies; redesign around stable contracts or orchestration when necessary.
5. Keep shared contracts small, stable, and genuinely shared, owned in module-owned `Abstractions` namespaces. Do not turn any shared-contract location into a dumping ground. Do not introduce SharedKernel.
6. Keep endpoints thin and provider SDKs behind adapters.
7. Do not add event sourcing, distributed messaging, or microservices without a concrete need.
8. The module that owns a business rule owns its implementation and persistence.
9. Tenant membership changes reach Access through direct Tenant-to-Access queries via `ITenantMembershipReader` (`CoreKit.Tenant.Contracts`, D-020); Tenant also stages membership changes in its outbox for external side effects only. Access does not maintain a membership projection.
10. Access resolves tenant membership through `ITenantMembershipReader` (live Tenant data) and fails closed when Tenant is unavailable or the tenant is not found. There is no Access-owned membership projection as the authoritative source (D-019 outbox/projection superseded by D-020).

## 4. Identity and Access boundary
- Identity authenticates a person/service and manages credentials, account state, sessions/tokens, lockout, and recovery.
- Access decides what an authenticated identity may do: roles, permissions, assignments, and authorization.
- Access must not read or write Identity private tables or use its private `DbContext`.
- Expose only the minimal stable identity identifier/claims contract required for authorization. Do not duplicate password, credential, or account lifecycle logic in Access.
- Identity must not remain the owner of business roles/permissions after the approved ownership transfer is complete.
- M0 reported that Identity still owned roles/permissions and lacked lockout. Those are implementation gaps, not changes to the target design.

## 5. Tenant and data isolation
- Tenant represents the SaaS account boundary; Store represents a merchant business. They are separate concepts.
- Tenant must not depend directly on Identity or its private implementation. Tenant uses Access public contracts for authorization.
- The initial strategy is one shared PostgreSQL database. Tenant-owned rows carry explicit `TenantId` ownership, or an explicitly documented ownership path where direct `TenantId` is not appropriate.
- Apply EF Core global query filters as a default safety boundary, but **never treat filters as the only security control**. Enforce tenant scope in application services and authorization, and test cross-tenant denial.
- Tenant context must be derived/validated from trusted server-side context and authenticated membership. A client-supplied header such as `X-Tenant-ID` is not authorization by itself.
- Fail closed when tenant context is absent, invalid, inactive, or inconsistent with user membership.
- Scope reads, writes, exports, reports, background jobs, webhooks, and platform/admin actions according to an explicit authorization policy.
- Add tenant-safe unique constraints and indexes where required by the actual data model. Avoid assuming every table is tenant-owned.
## 6. Tenant–Access integration (D-020 direct queries — implemented direction)

**Approved direction:** D-020 (2026-11-22) — Access queries Tenant membership directly through the `ITenantMembershipReader` contract in `CoreKit.Tenant.Contracts`, rather than maintaining a durable Access-owned membership projection. Tenant publishes membership changes through its outbox for external side effects only (emails, webhooks, accounting); Access does not maintain a separate projection cache. Access fails closed when Tenant is unavailable or the tenant is not found.

D-019's Option B outbox/projection architecture (durable Tenant outbox → Access membership-event consumer → Access-owned `AccessMembershipProjection` as authoritative membership source) is **superseded by D-020** and is NOT implemented. The projection subsystem (~6,000 lines: event consumer, snapshot installer, freshness recorder, heartbeat, quarantine, stream cursors, processed events) was judged disproportionate complexity for a single-instance SaaS and is not part of the current target architecture.

- **Dependency direction:** `Dastarkhwan.Integration` → `CoreKit.Identity`, `CoreKit.Access`, `CoreKit.Tenant`. No CoreKit module references another CoreKit module.
- `CoreKit.Tenant.csproj` references only `CoreKit.Tenant.Contracts` (no Identity, no Access) — D-015/D-019 direct-dependency requirement met at project-reference level.
- `CoreKit.Access` references `CoreKit.Tenant.Contracts` (`ITenantMembershipReader`) and `CoreKit.Access.Contracts`; Access is a consumer of Tenant's public contract, not of Tenant's implementation.
- `CoreKit.Identity` references `CoreKit.Identity.Contracts` and `CoreKit.Access.Contracts`.
- **All three `*.Contracts` assemblies exist and are referenced:** `CoreKit.Identity.Contracts`, `CoreKit.Access.Contracts`, `CoreKit.Tenant.Contracts`. `CoreKit.Access.Contracts` is NOT removed (contrary to the D-021 statement); it exposes `IAccessService`, `IPermissionChecker`, `IAccessMembership`, and related contracts consumed by Identity, Access, and Integration.
- Tenant membership changes reach Access through `ITenantMembershipReader` (live read) and, for external side effects, through the Tenant outbox staged in the same transaction as the membership change. There is no Access-owned membership projection, no membership-event consumer, and no projection freshness/heartbeat/quarantine machinery.
- Access cache invalidation on member removal exists (`TenantMemberRemovedToAccessMaintenance` → `IAccessMaintenance.RemoveUserAsync` → `_cache.InvalidateUser`). **Owner promotion/demotion does NOT invalidate the Access cache** — a demoted tenant owner retains cached owner permissions until the cache TTL expires. This is a documented cache-invalidation gap (see `current-state.md`, risk register).
- `ITenantMembershipReader` lives in `CoreKit.Tenant.Contracts` (module-owned, not SharedKernel). D-018's No SharedKernel principle remains in force; contracts live in module-owned `Contracts` assemblies.
- Access must not reference Tenant's private services, repositories, `DbContext` or tables. Tenant must not reference Identity private internals.
- Platform scope is distinct from tenant scope and needs an explicit authorization design.
- Tenant owner does NOT automatically become Access owner. `AccessMember(false)` is always returned for tenant scope; platform administration is granted via the "Administrator" role in Access's PlatformMembers table.
- Tenant permission enforcement across individual protected operations remains implementation work (S4.4).

**Implementation status clarification (source baseline `fc4a089...`, 2026-11-28):**
`CoreKit.Identity`, `CoreKit.Access` and `CoreKit.Tenant` are independently reusable modules. `Dastarkhwan.Integration` references the three modules plus all three `*.Contracts` assemblies and provides their composition.

`TenantActor` (`CoreKit.Tenant/Abstractions/TenantActor.cs`) consumes Tenant-owned `ITenantCurrentUser` and `ITenantPermissionChecker`. No `ICurrentUser` or `.Permissions` reference remains in `CoreKit.Tenant`.

`Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs` registers integration adapters (`TenantCreatedToAccessProvision`, `TenantMemberRemovedToAccessMaintenance`, `TenantDeletedToAccessCleanup`, `IdentityUserDeletedToTenantCleanup`, `IdentityUserDeletedToAccessCleanup`, `TenantClaimsContributor`, `PlatformAdminClaimsContributor`, `IPlatformAdministratorProvider`, `ITenantCurrentUser`/`ITenantPermissionChecker` bridges). It does NOT register `TenantAccessMembership`/`IAccessMembershipAdapter`; that registration was removed.

Tenant owner-to-Access owner mapping must be explicitly validated because Access owner semantics may grant broad permissions. Tenant ownership does NOT imply Access ownership.

Platform authorization and per-operation Tenant permission enforcement remain separate implementation concerns.

The Access migration `20261116043029_AddAccessMembershipStreamAndFreshness` (projection-substream schema) remains committed but is not part of the implemented direction; its database application and runtime behavior are not established by source inspection alone. `CoreKit.Access` still references `CoreKit.Access.Contracts` and `CoreKit.Tenant.Contracts`.

Preserve the modular monolith architecture and existing approved module ownership boundaries.
## 7. Integration layer (`Dastarkhwan.Integration`)

- References `CoreKit.Identity`, `CoreKit.Access` and `CoreKit.Tenant` plus all three `*.Contracts` assemblies; coordinates them through their public contracts.
- Registers integration adapters: `TenantCreatedToAccessProvision` (provision default tenant roles in Access), `TenantMemberRemovedToAccessMaintenance` (invalidate Access cache on member removal), `TenantDeletedToAccessCleanup`, `IdentityUserDeletedToTenantCleanup`, `IdentityUserDeletedToAccessCleanup`, `TenantClaimsContributor`, `PlatformAdminClaimsContributor`, `IPlatformAdministratorProvider`, `ITenantCurrentUser`/`ITenantPermissionChecker` bridges.
- Does NOT register `ITenantProjectionGateway` (no host-side adapter references both modules) and does NOT host an Access membership-event consumer — the D-019 outbox/projection integration is superseded by D-020 direct queries.
- `ITenantOutboxTransport` (Tenant-owned port) is implemented by `IdentityOutboxTransport` for Identity account-deletion outbox delivery; Tenant membership outbox events are staged for external side effects, not consumed by Access for authorization.
- Registers Tenant permission definitions in the Access catalog without Tenant referencing Access.
- Composes module services into the application host without owning module business rules. The future web-related host is a separate application project. SampleHost is not required and is not part of the approved architecture, including during transition.
- No SharedKernel. No cross-module adopter/plugin implementation pattern.

## 8. SaaS and standalone composition
- **SaaS:** compose Tenant, Identity, and Access, plus the application modules required by the product. All applicable operations enforce tenant/store scope.
- **Standalone:** Tenant infrastructure is not required. Identity/Access may be optional only when a secure alternative is explicitly configured for protected operations.
- Keep host-specific composition at clear boundaries; do not scatter SaaS/standalone conditionals throughout unrelated modules.
- Create a separate `StandaloneStore` only if lifecycle/business rules materially differ. If only configuration differs, reuse Store with explicit host configuration.

## 9. Tenant, store, market, and currency
These are distinct concepts:
- Tenant: SaaS account boundary.
- Store: merchant business.
- Market: country/market rules and availability.
- Currency: transaction unit.
- Domain/hostname: routing input, not authorization.

Pakistan is the initial market. Future UK support should use market settings/adapters for address/postcode, payments, tax/invoicing, and locale. Verify current legal requirements before implementation. Persist explicit currency codes and appropriate decimal money values. Snapshot item prices, discounts, taxes, fees, and currency on orders; historical totals must not change when catalog settings change.

## 10. Authentication and session security
- Use short-lived JWT access tokens and rotating refresh tokens as accepted in `decisions.md`.
- Detect refresh-token reuse and revoke the affected token family/session according to the approved implementation design.
- Do not store plaintext refresh tokens; protect stored token material and revoke sessions securely.
- Lock an account for 15 minutes after 5 failed login attempts. Account lockout is not a replacement for IP/request rate limiting.
- Avoid leaking whether a particular account exists through login and recovery responses.
- Keep token contents minimal and avoid placing secrets or sensitive personal data in JWT claims.

## 11. Data and workflows
- Each module owns its schema and persistence mapping.
- Use database constraints and short transactions for important invariants.
- Do not hold a transaction open during slow external network calls.
- Each unit of work (`AccessUnitOfWork`, `TenantUnitOfWork`) performs exactly one `SaveChangesAsync` call. EF Core wraps that single call in its own implicit transaction, so no explicit transaction is needed for the common single-save path. If a future change adds a second `SaveChanges` or a raw query between saves, an explicit `BeginTransactionAsync` must wrap them; otherwise the two saves are not atomic.
- Use explicit orchestration and idempotency for cross-module workflows and callbacks.
- Add outbox/retry infrastructure only when justified.
- Plan migrations and backward compatibility before destructive schema changes. Review migration contents before applying them.
- Do not execute or apply migrations without explicit user authorization.

## 12. API and application design
Conceptual request path: `HTTP endpoint → authentication → authorization/context resolution → application use case → owning module/persistence → response`.

Use validation at boundaries, clear error contracts, bounded pagination, projections, async I/O, safe logging, and parameterized queries. Add layers only where they serve a real responsibility. Validate client-controlled prices, discounts, IDs, ownership, and state transitions on the server.

## 13. Security and operational controls
- Authorize every protected server operation and verify resource ownership.
- Enforce scope in list/detail/create/update/delete/export/report/job/webhook/admin paths.
- Validate webhook signatures and make handlers safe to retry.
- Keep secrets out of source control and logs; use least privilege and secure configuration.
- Add audit records for security-sensitive actions where appropriate, without logging credentials or tokens.
- Use safe errors; do not leak stack traces or internal details to clients.
- Use rate limits for abuse-prone endpoints and understand cache invalidation/scope.

## 14. Performance
Use async I/O, projections, bounded pagination, indexes based on real query patterns, and avoid N+1 queries/unbounded loads. Measure before caching or architectural changes. Cache keys/results must be user-, tenant-, and store-safe with clear invalidation. Use background processing for suitable long tasks, with retries and idempotency.

## 15. Decisions and evidence
Accepted decisions and open questions are in `decisions.md`. Current implementation status is in `current-state.md`. Roadmap milestones are not evidence of implementation. Verify the latest repository commit before each task and update architecture only when a decision or verified structure changes.
