# DK-COMPLETE-APPLICATION-E2E-OUTBOX-RESULTS-01

Phase 5 — Integration / transactional outbox assessment.

## Baseline
- Commit: `6b6fd2a` (Phase 2–4 changes still uncommitted).
- Host: Web `https://localhost:53584` (Development), real PostgreSQL `dastarkhwan_e2e_audit`.
- Note: the Integration module exposes **no HTTP endpoints**; it is cross-module event wiring
  (outbox + in-process handlers). This phase is therefore a static + DB + runtime-log assessment,
  not an HTTP spec.

## What Integration wires (Dastarkhwan.Integration)
- `IdentityOutboxTransport` — delivers Identity outbox events to in-process `IEventHandler<>`s.
- `TenantCreatedToAccessProvision` — on `TenantCreated`, provision a tenant's default Access roles.
- `TenantMemberRemovedToAccessMaintenance` — on `TenantMemberRemoved`, clear cached access.
- `IdentityUserDeletedToTenantCleanup` / `IdentityUserDeletedToAccessCleanup` — on user deletion.
- `TenantClaimsContributor` / `PlatformAdminClaimsContributor` — token claims.
- Bridges between Tenant and Access abstractions.

## Outbox infrastructure (verified)
- Tables present: `identity.Identity_OutboxMessages`, `tenant.TENANT_OutboxMessages`.
- Columns support a durable transactional outbox: `Status`, `AttemptCount`, `NextAttemptAt`,
  `LeaseOwner`, `LeaseExpiresAt`, `LastAttemptAt`, `DeliveredAt`, `DeadLetteredAt`, `LastError`.
- Both tables currently hold **0 rows** (no user/tenant creation has occurred — blocked by D-E2E-001).

## Runtime evidence (host logs)
- **Identity outbox publisher IS running:** the log shows a continuous poll
  `SELECT ... FROM identity."Identity_OutboxMessages" WHERE "Status" = 1 AND "NextAttemptAt" <= now()`.
  Registered via `AddIdentity` → `AddIdentityOutbox` (hosted services `IdentityOutboxPublisherService`,
  `IdentityOutboxTransportGuard`).
- **Tenant outbox publisher is NOT running:** there is no corresponding poll of
  `tenant."TENANT_OutboxMessages"` anywhere in the logs.

## Finding D-E2E-002 (Integration reliability gap — needs Owner confirmation)
- `AddTenant` (TenantServiceCollectionExtensions.cs) registers `ITenantOutbox` only; it does **not**
  call `AddTenantOutboxPublisher`, which is the method that registers the
  `TenantOutboxPublisherService` hosted service.
- Neither host calls `AddTenantOutboxPublisher`: `Dastarkhwan.Web/Program.cs:121` and
  `Dastarkhwan.Admin/Program.cs:104` have a "Register outbox publisher" comment followed by nothing.
- Effect: `TenantEventPublisher` enqueues `TenantCreated` / `TenantMemberRemoved` to
  `TENANT_OutboxMessages`, but **nothing dispatches them**. The handlers
  `TenantCreatedToAccessProvision` and `TenantMemberRemovedToAccessMaintenance` therefore never run,
  so a newly created tenant would not receive its default Access roles, and membership-removal cache
  maintenance would not fire.
- Classification: the Integration extension comment says the publisher "arrives with the projection
  cutover," so this may be intentionally deferred rather than a regression. **Owner to confirm.**
  If tenant provisioning is expected to work now, this is a High-severity defect; if deferred, it is a
  documented limitation. No code was changed.

## Functional outbox E2E status
- Blocked by D-E2E-001: producing outbox messages requires creating a user or tenant, which the
  under-provisioned Administrator role cannot do (403). Once D-E2E-001 is fixed, add an Outbox spec
  that: creates a tenant → asserts a `TenantCreated` row appears and is delivered (Status → Delivered,
  DeliveredAt set) → asserts default Access roles were provisioned; and creates+deletes a user →
  asserts cleanup handlers ran.

## Harness fix (test-only)
- `helpers/apiClient.ts` `get()` now skips `undefined`/`null` params. Previously it appended the
  literal string `undefined` (e.g. `GET /tenants?status=undefined`), which caused a 400 model-binding
  error and masked the real 403. Tenant context tests still pass (4/4) after the fix.

## Notes
- No application code changed this phase.
- Re-verify after D-E2E-001 (and a decision on D-E2E-002): outbox delivery, default-role provisioning,
  and user-deletion cleanup all become testable.