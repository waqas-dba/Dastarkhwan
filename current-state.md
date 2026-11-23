# Current State — Dastarkhwan / CoreKit

## Purpose and evidence rules

This file records implementation facts, not aspirations. Architecture and decisions documents describe the target unless code evidence confirms implementation.

Use these evidence labels consistently:

- **Verified at current snapshot:** directly inspected committed files at the recorded snapshot HEAD. This confirms only the inspected source or documentation, not successful builds, tests, migrations, or database operations.
- **Reported complete (not independently verified):** reported by an agent, but not independently confirmed against the relevant implementation or artifact.
- **In progress:** implementation or documentation work has started but its acceptance criteria are not yet met.
- **Pending:** work has not started or awaits an approval, decision, or required action.
- **Under review:** an implementation or artifact exists and requires independent review or acceptance.
- **Blocked:** work cannot safely proceed until a stated dependency or decision is resolved.
- **Unknown:** not inspected or insufficient evidence.

A status must describe the evidence available, not the intended architecture or an agent's confidence.

## Historical M0 baseline

| Field | Value |
|---|---|
| Baseline commit | `9555b8c` |
| Commit date in M0 report | 2026-10-23 |
| M0 status | Reported complete (not independently verified) |
| Evidence source | User-provided M0 inspection report |
| Scope | Historical, inspection-only baseline |

`9555b8c` is a historical baseline, not the current repository HEAD. Its findings must not be presented as current implementation facts without checking later changes.

## Current repository snapshot

| Field | Value |
|---|---|
| Latest inspected commit | `ef761df` |
| Parent commit | `d641921` |
| Repository evidence | Local build + test verification at current HEAD |
| Local HEAD | Verified: `ef761df` |
| Local working-tree status | Clean (all changes committed at `ef761df`) |
| Source inspection baseline | `ef761df` |
| Documentation snapshot | `ef761df` |
| Scope | Committed source at current HEAD; build + test verified |

Build verification: `dotnet build Dastarkhwan.slnx` — 0 errors, 0 warnings.

Test results (observed, current HEAD `ef761df`):
- `CoreKit.Access.Tests`: 354 passed, 0 failed, 0 skipped — includes actor schema validation, check constraint enforcement, whitespace validation (space, tab, CR, LF, mixed), historical migration backfill, bootstrap trust boundary
- `CoreKit.Identity.Tests`: 151 passed, 1 failed, 0 skipped — the single failure is `PostgresLoginTransitionTests.ConcurrentFailedLogins_OnPostgres_DoNotLoseIncrementsAndEstablishLockout`, which requires a live PostgreSQL instance (`ConnectionStrings__Access`) not available in this environment; all other Identity tests pass including lockout, concurrent failed attempts, refresh rotation, recovery
- `CoreKit.Tenant.Tests`: 358 passed, 0 failed, 0 skipped — includes tenant lifecycle, isolation, outbox, projection recovery

PostgreSQL integration tests: `CoreKit.Access.Tests/AccessAuditActorTests.HistoricalMigrationBackfill_AppliesActualEFMigration_OnPostgres` returns early (no assertions executed) when `ConnectionStrings__Access` is not set. With `COREKIT_REQUIRE_POSTGRES=1`, unavailability causes test failure rather than a silent pass.

The previous remote HEAD `d641921` (2026-11-22, `P5: Atomic refresh-token rotation (C-05/H-02)`) is the current baseline. Historical baselines preserved: `7ed4c61fca8b06f35a0e2f1ce5f806ae22ad0043` (2026-11-13 19:53:27 +0500, `docs(governance): accept D-017 automation and align agent guidance`), `c9dbbe12e4611883ad4374f84067efc99b03ff29` (2026-11-15 16:01:53 +0500, `update the current sate and road map`), `7115b14b9d90da5617e55836283e8d5dd00810db` (2026-11-15 15:07:48 +0500, `docs(governance): synchronize core documents with baseline 1c832de`), and `1c832de9710630fc9f0e8bbffd43ca08fe382c2d` (2026-11-15 13:57:49 +0500, `Fix Tenant membership revocation -> Access cache consistency`).

## Baselines preserved as historical evidence

- Historical M0 baseline: `9555b8c` (2026-10-23) — inspection-only historical baseline from the M0 report.
- Source-inspection baseline: `5dfa796ca52fc0b122b3efea182f2dc7dbd5cde9` (2026-11-11) — used to inspect source code and classify Tenant's Identity dependency status.
- Historical documentation snapshot: `beb0a0b550f1d825d416c48ae0a69fef188ea303` (2026-11-11) — documentation/governance corrections commit, not a source-inspection baseline.
- Previous documentation snapshot: `548dca791009d094ef7e2e9bd917a3bc0b8b0f0c` (2026-11-12) — documentation-only snapshot recorded before later syncs.
- Previous latest verified local baseline: `70d757fd099c58de5976383677255ce64aac15e3` (2026-11-13) — implementation/review-evidence baseline for `DK-ACCESS-PLATFORM-SCOPE-01` (**Under review / Request Changes**, not accepted).
- Previous local/documentation baseline: `3f58add6da963669cbc6d113f2bc030478a112c6` (2026-11-13) — docs-sync commit recorded prior to this remote baseline.
- FIX1 baseline: `f09f236bcbc162387b572e0ed8cee4d29949a816` (2026-11-13 19:36:18 +0500) — documentation-task baseline recorded for `DK-DEV-AUTOMATION-01-FIX1`.
- FIX1 implementation commit / previous remote HEAD: `7ed4c61fca8b06f35a0e2f1ce5f806ae22ad0043` (2026-11-13 19:53:27 +0500).
- Current verified baseline: `c9dbbe12e4611883ad4374f84067efc99b03ff29` (2026-11-15 16:01:53 +0500) — task baseline for DK-DOCUMENTATION-FINALIZE-06; `update the current sate and road map`. Baseline `7115b14` (2026-11-15 15:07:48 +0500) is retained as a historical documentation-sync baseline that aligned all six core documents with baseline `1c832de`.

Documentation-only commits (`45a5789`, `beb0a0b`, `d7585c3`, `335b9ed`, `0504f3a`, `6e79f13`, `c9d4b36`, `548dca`, `3f58add`) did not re-inspect source code; no claim is made that source behavior changed between these baselines except where explicitly noted. Commit `70d757f` (`DK-ACCESS-PLATFORM-SCOPE-01`) remains **Under review / Request Changes** (not accepted) at its own baseline and is not the current HEAD. The `335b9ed` commit (DK-TENANT-ACCESS-DOCS-FIX3) carries the immutable historical message "docs: correct FIX2 commit attribution and DeepSeek Web role"; `335b9ed` is recorded as FIX3 to preserve actual commit history. The historical M0 baseline `9555b8c` remains unchanged.

Baseline `1c832de9710630fc9f0e8bbffd43ca08fe382c2d` (2026-11-15 13:57:49 +0500, `Fix Tenant membership revocation -> Access cache consistency`) is preserved as the historical source/audit baseline. Baseline `1c832de` addressed audit finding D-02 (Tenant–Access cache-invalidation handler + DI registration committed). D-01 (test rewrite of `CoreKit.Tenant.Tests/TenantMemberRemovedAccessCacheTests.cs`) existed as an uncommitted working-tree change at `1c832de` and is committed at `7115b14`; it is not build-verified or test-verified, and cache-invalidated regression coverage is not confirmed until its assertions are independently reviewed. Baseline `7115b14` (2026-11-15 15:07:48 +0500, `docs(governance): synchronize core documents with baseline 1c832de`) is the subsequent documentation-sync commit that reconciled all six core documents with `1c832de`. Baseline `c9dbbe12` (2026-11-15 16:01:53 +0500, `update the current sate and road map`) is the current task baseline for DK-DOCUMENTATION-FINALIZE-06.

Note on commit attribution: the immutable historical messages are preserved without rewriting.

## Verified M0 findings at `9555b8c`

These findings are retained as historical evidence from the M0 report.

| Area | Status at baseline | Finding / evidence from M0 report |
|---|---|---|
| Repository documentation | Verified at historical baseline | `CLAUDE.md`, `roadmap.md`, `current-state.md`, `decisions.md`, `architecture.md`, and `ai-tasks.md` existed. |
| Project naming | Verified at historical baseline | Identity and sample host were renamed to `CoreKit.Identity` and `CoreKit.SampleHost`. |
| SharedKernel | Verified at historical baseline | `CoreKit.SharedKernel` existed. |
| Architecture tests | In progress | `CoreKit.Architecture.Tests` existed; its project reference covered only Access at that baseline. Access tests were located inside this project. |
| Identity | In progress | `CoreKit.Identity` existed. No account lockout was found. Identity still owned roles and permissions, contrary to the target ownership boundary. |
| Access | In progress | Access implementation existed, but no Access migration was present at that baseline. |
| Tenant | In progress | Tenant implementation existed but directly referenced Identity in six code files at that baseline. |
| Store | Pending | No Store implementation was reported at that baseline. |
| M6–M13 product modules | Pending | No product code was reported for these roadmap areas at that baseline. |
| Development configuration | Under review | M0 reported that `appsettings.Development.json` was no longer tracked at `9555b8c`, but had been tracked in an earlier commit. Treat any secrets ever committed as exposed and rotate them. Removing a file from the current tree does not remove it from Git history. |

## Findings at the current snapshot

The following source findings were inspected at `ef761df` and reflect the current implementation state.

| Area | Status | Evidence and limitations |
|---|---|---|
| Architecture tests | In progress | Boundary enforcement now includes evaluated MSBuild project references, solution inventory and IVT policy (`MsBuildProjectEvaluator.cs`, `SolutionInventory.cs`, `ProjectReferenceBoundaryTests.cs`, `ModuleBoundaryMatrix.cs`). Tests were not run as part of this inspection. |
| Architecture test organization | Under review | `CoreKit.Access.Tests` exists with 9 files (created in `470d42c`): four test files, `GlobalUsings.cs`, `Support/{Fakes,RulesTests,TestEnv}.cs`, and the csproj. Independent review and a user-run test execution are pending (S2.1). |
| Access implementation | In progress | Access services, endpoints, audit and scope-related implementation exist. Build, test and manual verification results are not established here. |
| Access migrations | Under review | Access migration artifacts are committed under `CoreKit.Access/Migrations/` at `43c2179`. Schema correctness, compatibility and database application are not verified. |
| Identity | In progress | Identity implementation exists. The available evidence does not establish completion of the accepted Identity responsibilities or account lockout. |
| Identity migrations | Under review | Identity migration artifacts are committed under `CoreKit.Identity/Migrations/`. Schema correctness, compatibility and database application are not verified. |
| Identity role and permission ownership | Under review | The accepted target assigns roles and permissions to Access. The implementation ownership transfer must be verified against current Identity and Access entities, services, persistence and consumers before claiming completion. |
| Tenant | In progress | `CoreKit.Tenant/CoreKit.Tenant.csproj` has no direct CoreKit project references. `TenantActor` consumes Tenant-owned `ITenantCurrentUser` and `ITenantPermissionChecker`; no `ICurrentUser`/`.Permissions` reference remains in `CoreKit.Tenant` at source baseline `e2ca8c1`. Tenant owns its lifecycle, membership, context and durable outbox. Access membership authorization is intended to use the Access-owned projection. However, Integration still registers `TenantAccessMembership`, which reads Tenant membership directly. This conflicts with the projection-authoritative requirement in D-019 and requires implementation correction or an explicit architectural resolution. |
| Tenant–Access authorization | Under review | The approved design is D-019: Tenant publishes durable membership events through its outbox; Integration delivers them to the Access membership-event consumer; Access maintains and uses its own membership projection. Relevant source artifacts include `TenantOutboxMessage`, `TenantToAccessOutboxTransport`, `AccessMembershipEventConsumer` and the Access projection. However, `IntegrationServiceCollectionExtensions` still registers `TenantAccessMembership` as an `IAccessMembershipAdapter`, retaining a direct Tenant membership lookup. The projection-only authorization boundary is therefore not yet verified as fully implemented. No database application or runtime verification is claimed. |
| Platform authorization | Under review (historical baseline `70d757fd`, not current HEAD `ef761df`) | `DK-ACCESS-PLATFORM-SCOPE-01` (`70d757fd099c58de5976383677255ce64aac15e3`, 2026-11-13) adds fixed platform-scope authorization for Access platform membership add/revoke operations (`CoreKit.Access/Services/AccessGuard.cs`, `CoreKit.Access/Services/PlatformMembershipService.cs`) with focused tests (`CoreKit.Access.Tests/PlatformMembershipTests.cs`). Independent review status at that baseline: **Request Changes** (not accepted). Confirmed findings: (1) `PlatformMembershipService.AddMemberAsync`/`RevokeMemberAsync` collapse guard failures to `access.denied`, so unauthenticated callers are not surfaced as `access.not_authenticated` despite guard intent and added unauthenticated test expectation; (2) earlier review finding that guard authorization path did not explicitly check `IsAuthenticated` (guard relies on `UserId` null-check/fail-closed actor identity — must be resolved/confirmed in correction scope); (3) stale-cache/revoked-authority coverage and setup are insufficient and missing Revoke regression cases for the claimed fresh-authority/security properties; (4) commit includes out-of-scope `CoreKit.Tenant.Tests/TenantAccessAuthorizationTests.cs` addition (`HasPermissionAsync_ReturnsFalse_WhenTenantOwnerIsNotAccessMember`) which must be assessed separately. A commit/push governance violation is recorded for this work (Git state-changing operation(s) without explicit recorded Project Owner approval before execution); historical commit information is preserved and this does not imply retrospective approval. Broader Tenant platform-authorization boundary redesign is limited to platform-level authorization (S4.3); the `TenantActor`/`ICurrentUser.Permissions` and D-015 Identity semantic coupling was resolved at source baseline `e2ca8c1` and is not outstanding work. Platform authorization itself remains under review until independently verified. |
| Per-operation permission enforcement | Pending | Tenant permission enforcement across individual protected operations is not yet implemented. Named Access permissions are not enforced across Tenant operations. |
| Tenant isolation | In progress | `CoreKit.Tenant/Isolation/TenantAwareDbContext.cs` contains tenant query-filter logic. `CoreKit.Tenant.Tests/IsolationTests.cs` contains isolation test coverage. These files alone do not prove complete enforcement across all application operations. |
| Tenant migrations | Under review | Tenant migration artifacts have changed and are committed at `43c2179`. Compatibility and database application are not verified. |
| Store and later product modules | Pending | No implementation completion is established for Store, Catalog, Cart, Orders, Payments, Fulfillment, Website or Marketplace. |
| Migrations overall | Under review | Access, Identity and Tenant migration artifacts are committed. No successful migration execution or database state is claimed. |
| Committed-secret follow-up | Unknown | Carried forward from the M0 finding. No new evidence of secret rotation or history remediation is recorded. |

## Completed audit and architecture items at `ef761df`

| Item | Status | Evidence |
|---|---|---|
| B1 — Email-sender check fixed | Verified at current snapshot | `Dastarkhwan.Web/Program.cs` registers email sender only in Development |
| B2 — SizeLimit removed from shared `IMemoryCache` | Verified at current snapshot | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` registers `IMemoryCache` without SizeLimit |
| B3 — AccessBootstrap idempotent | Verified at current snapshot | `CoreKit.Access/Services/AccessBootstrap.cs` uses conditional seeding |
| B4 — EF migration for Identity outbox | Verified at current snapshot | `CoreKit.Identity/Migrations/20261122145210_AddIdentityOutboxMessages.cs` |
| B5 — Account deletion uses outbox + owner protection | Verified at current snapshot | `CoreKit.Identity/Services/UserService.cs:233-255` (DeleteAsync), `Dastarkhwan.Integration/Identity/UserDeletionGuard.cs` |
| B6 — AdministratorGuard uses PostgreSQL advisory lock | Verified at current snapshot | `CoreKit.Identity/Services/AdministratorGuard.cs` uses `pg_advisory_xact_lock` |
| M1 — TenantResolutionMiddleware exempts paths | Verified at current snapshot | `CoreKit.Tenant/Resolution/TenantResolutionMiddleware.cs` uses `ExcludedPathPrefixes` |
| M3 — Unified ProblemDetails via ProblemDetailsFactory | Verified at current snapshot | `CoreKit.Abstractions/Errors/ProblemDetailsFactory.cs`, used by Identity, Access, Tenant |
| M5 — Demo routes gated to Development | Verified at current snapshot | `Dastarkhwan.Web/Program.cs` gates demo endpoints |
| M7 — Production startup checks pending migrations | Verified at current snapshot | `Dastarkhwan.Web/Program.cs` checks all three DbContexts |
| REV-01 — Forwarded-header trust restricted | Verified at current snapshot | `Dastarkhwan.Web/Program.cs` limits forwarded headers |
| REV-04 — Tenant owner removal protection | Verified at current snapshot | `Dastarkhwan.Integration/Identity/UserDeletionGuard.cs` implements `IUserDeletionGuard` |
| REV-05 — Last-administrator protection | Verified at current snapshot | `CoreKit.Identity/Services/AdministratorGuard.cs` with advisory lock |
| REV-06 — Refresh-token replay detection | Verified at current snapshot | `CoreKit.Identity/Services/AuthService.cs:200-209` (now atomic via RotationResult.ReplayDetected) |
| REV-07 — JWT `secver` claim | Verified at current snapshot | `CoreKit.Identity/Services/TokenService.cs` includes SecurityVersion in access token |
| REV-12 — Identity outbox for account deletion | Verified at current snapshot | `CoreKit.Identity/Outbox/` (IdentityOutboxMessage, Publisher, Dispatcher, Service, Transport) |
| REV-13 — SharedKernel deleted | Verified at current snapshot | `CoreKit.SharedKernel/` folder empty, no project reference |
| REV-14 — Projection cutover to Projection source | Verified at current snapshot | `Dastarkhwan.Web/Program.cs` defaults to `Access:MembershipSource=Projection` |
| REV-15 — SecurityVersion caching | Verified at current snapshot | `CoreKit.Identity/Services/TokenService.cs` caches SecurityVersion |
| REV-16 — Atomic account updates | Verified at current snapshot | `CoreKit.Identity/Repositories/UserRepository.cs` uses conditional updates |
| REV-17 — Startup projection recovery + AccessBootstrap | Verified at current snapshot | `CoreKit.Access/Services/TenantProjectionRecoveryService.cs`, `AccessBootstrap.cs` |
| REV-18 — Owner-role lifecycle + default roles | Verified at current snapshot | `CoreKit.Access/Services/AccessMembershipEventConsumer.cs` handles MemberOwnershipChanged/MemberReactivated/MemberRemoved |
| REV-19 — Suspended tenant claim handling | Verified at current snapshot | `CoreKit.Access/Services/ProjectionAccessMembership.cs` checks tenant lifecycle state |
| REV-20 — Owner validation on tenant creation | Verified at current snapshot | `CoreKit.Tenant/Services/TenantService.cs` validates owner |
| REV-24 — Default role provisioning | Verified at current snapshot | `CoreKit.Access/Services/AccessBootstrap.cs` seeds default roles |
| REV-25 — `secver` reserved claim | Verified at current snapshot | `CoreKit.Identity/Services/TokenService.cs` marks `secver` as reserved |
| REV-26 — Retry-on-failure disabled | Verified at current snapshot | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` no retry policy |
| REV-28 — `IMemoryCache` size limits | Verified at current snapshot | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` no SizeLimit |
| REV-30 — Account deletion outbox durability | Verified at current snapshot | `CoreKit.Identity/Outbox/IdentityOutboxPublisher` writes in same transaction |
| C-01 — Outbox dispatcher wiring verified | Verified at current snapshot | `Dastarkhwan.Web/Program.cs` registers `AddDastarkhwanIntegration` (single registration) |
| C-02 — JWT placeholder signing key rejection | Verified at current snapshot | `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` `IsPlaceholderSigningKey()` validation |
| C-03 — Projection freshness fail-closed | Verified at current snapshot | `CoreKit.Access/Services/ProjectionAccessMembership.cs:GetLastEventSequenceAsync` returns cursor's `LastAppliedSequence` |
| C-04 — Owner-role lifecycle | Verified at current snapshot | `CoreKit.Access/Services/AccessMembershipEventConsumer.cs` assigns/revokes Owner role on MemberOwnershipChanged/MemberReactivated/MemberRemoved |
| C-05 / H-02 — Atomic refresh-token rotation | Verified at current snapshot | `CoreKit.Identity/Repositories/RefreshTokenRepository.cs:TryRotateAsync`, `CoreKit.Identity/Services/SessionIssuer.cs:RotateAsync`, `CoreKit.Identity/Services/AuthService.cs:199-215` |

## Milestone status

| Milestone | Status | Notes |
|---|---|---|
| M0 — Repository baseline | Reported complete (not independently verified) | Historical inspection report at `9555b8c`; inspection-only. |
| M1 — Essential architecture decisions | In progress | Accepted decisions are recorded in `decisions.md` (including D-018, No SharedKernel). Implementation status is tracked in this file and is not implied by decision acceptance. This documentation synchronization (DK-DOCUMENTATION-SYNC-04) aligns all six core documents with baseline `1c832de`. |
| M2 — Module foundation | In progress | Boundary tests cover Identity, Access and Tenant. The three modules are independently reusable and Integration composes them. Access suites now live in `CoreKit.Access.Tests` (S2.1 under review); boundary enforcement uses evaluated project references (S2.2 in progress). |
| M3 — Identity | In progress | Identity exists; completion of lockout and the role/permission ownership transfer is not established. **Refresh-token atomic rotation (C-05/H-02) implemented at `ef761df`; JWT placeholder rejection (C-02) implemented.** |
| M4 — Access | In progress | Access services, endpoints and tests exist. Migration artifacts are committed at `43c2179` and require review. Role ownership transfer and manual verification remain open. **Projection freshness fail-closed (C-03) and Owner-role lifecycle (C-04/H-08) implemented at `ef761df`.** |
| M5 — Tenant and Store | In progress | The three modules are independently reusable and Integration composes them. Tenant owns its lifecycle, membership, context and durable outbox. Tenant–Access authorization follows the D-019 outbox/projection design; however, `TenantAccessMembership` is still registered in Integration as an `IAccessMembershipAdapter`, reading Tenant membership directly, which conflicts with the projection-authoritative requirement. Platform authorization (S4.3) and per-operation permission enforcement (S4.4) remain outstanding. Store implementation is pending. |
| M6 — SaaS Subscription (`CoreKit.Subscription`) | Pending | No implementation completion established. |
| M7–M14 | Pending | Product implementation and acceptance have not been established. |

## Accepted decisions and implementation status

Decision statements are recorded in `decisions.md`, which remains the single source for accepted decisions. Acceptance of a decision does not mean implementation is complete.

| Decision | Implementation status | Evidence and limitations |
|---|---|---|
| D-002 — Identity and Access ownership | Under review | The target assigns authentication and identity lifecycle to Identity, and roles, permissions, assignments and authorization to Access. Verify current entities, services, persistence and consumers before claiming the ownership transfer is complete. S6.1/S6.2 remain open. |
| D-011 — Tenant data isolation | In progress | `CoreKit.Tenant/Isolation/TenantAwareDbContext.cs` contains tenant query-filter logic. `CoreKit.Tenant.Tests/IsolationTests.cs` includes tenant-isolation test coverage. Complete enforcement across application services, authorization, background jobs and other access paths is not established by these references alone. |
| D-012 — Authentication and session approach | Under review | The accepted policy is recorded in `decisions.md`. The available evidence does not establish completion of refresh-token rotation, reuse detection, revocation and account lockout together. **Atomic refresh-token rotation (C-05/H-02) implemented at `ef761df`; JWT placeholder rejection (C-02) implemented.** Verify the relevant Identity implementation before updating this status. |
| D-015 — Tenant–Identity dependency | Under review | No direct `CoreKit.Identity` project reference in `CoreKit.Tenant/CoreKit.Tenant.csproj`. No Identity semantic coupling remains in Tenant; `TenantActor` uses Tenant-owned `ITenantCurrentUser`/`ITenantPermissionChecker` (verified at `e2ca8c1`). Decision acceptance does not imply implementation completion. |
| D-016 — Tenant requires Access for authorization | Under review | Decision accepted (2026-11-11). Tenant–Access authorization now follows D-019's outbox/projection design: Tenant publishes durable membership events through its outbox; Integration delivers them to the Access membership-event consumer; Access maintains and uses its own membership projection. The old direct adapter design (`TenantAccessScopeContext`, `TenantAccessMembership` registered in `TenantServiceCollectionExtensions`) is not the accepted target. However, `TenantAccessMembership` is still registered in Integration as an `IAccessMembershipAdapter`, reading Tenant membership directly, which conflicts with the projection-authoritative requirement and requires resolution. Platform authorization and per-operation permission enforcement remain outstanding (S4.3/S4.4). |
| D-018 — No SharedKernel; module-owned cross-module contracts; Identity RBAC data disposition | Under review | Decision accepted (2026-11-15). `CoreKit.SharedKernel/` is empty (no `.csproj`, no source, unreferenced). Cross-module contracts live in module-owned `Abstractions` namespaces. `CoreKit.Tenant` and `CoreKit.Identity` each reference only `CoreKit.Access`; `CoreKit.Access` is a leaf module. Identity RBAC table-drop migration (`RemoveIdentityRbacTables`) accepted as acceptable for deployment. No data-transfer or legacy RBAC recovery requirement. |
| D-019 — Option B independent-module architecture and Integration foundation | Under review | Decision accepted (2026-11-17). `CoreKit.Identity`, `CoreKit.Access`, `CoreKit.Tenant` are independently reusable with zero direct CoreKit project references. `Dastarkhwan.Integration` composes them. Tenant membership changes reach Access through durable outbox + Access projection. **At `ef761df`, `IntegrationServiceCollectionExtensions` still registers `TenantAccessMembership` as `IAccessMembershipAdapter`, conflicting with projection-authoritative requirement.** Full implementation pending resolution. |

## Deferred decisions

Do not resolve these prematurely. Decide only when the dependent milestone needs them:

- Single-store vs multi-store checkout.
- Parcel pickup initiation and lifecycle details.
- Payment methods, COD behavior and payment provider.
- Detailed country, currency and tax behavior.
- Storefront and website technology.
- Custom-domain verification and HTTPS operations.
- Marketplace behavior.

## Open Project Owner decisions (audit findings)

The independent audit `DK-FULL-APPLICATION-ARCHITECTURE-AUDIT-01` and remediation report `DK-FULL-APPLICATION-REMEDIATION-01` classify the following as unresolved Project Owner decisions. They remain open and are not resolved by this documentation task:

- **D-03 — JWT revocation / `SecurityVersion` validation:** `SecurityVersion` exists on `User` and is incremented on credential change/deactivation, but JWT issuance and validation never read it. Stolen access tokens remain valid until expiry (up to the configured access-token window). Awaiting Project Owner security-architecture decision (no implementation authorized).
- **D-18 — Rate limiting on tenant and access write endpoints:** Rate limiting is configured only for Identity auth endpoints; tenant (`/tenants/*`) and access (`/access/*`) write endpoints have `RequireAuthorization()` but no rate limiting. Awaiting Project Owner rate-limiting policy approval (no implementation authorized).

Neither finding is converted into an approved decision here. No data-transfer or legacy RBAC recovery requirement is added.

## Security follow-up — committed secrets

- M0 reported that `appsettings.Development.json` was not tracked at `9555b8c`, but was present in an earlier commit.
- Treat credentials, signing keys, connection strings and API keys that may have been committed as compromised, even if the file is now deleted or ignored.
- Rotate affected secrets and move active values to secure local, user-secret, environment, or secret-manager configuration as appropriate.
- Do not print secret values in reports.
- History cleanup, if needed, is a user-controlled Git operation and must be planned carefully. Rotating secrets is the priority.
- No new evidence of secret rotation or history remediation is recorded at the current snapshot. This action remains open.

## Current agent allocation

The current agreed operating allocation is:

- **ChatGPT Account 1 — Project Manager and Architecture Coordinator:** defines tasks, coordinates agents, maintains roadmap and milestones, evaluates reports, recommends acceptance. Does not modify repository files or perform Git writes.
- **OpenCode — Default primary implementation agent:** implements approved development tasks, supplies exact diffs, provides completion reports, and performs in-scope validation, EF Core migration/dev-DB steps when included, and scoped Git operations subject to safeguards and approvals (D-017/D-007/D-008).
- **Kilo — Alternative primary implementation agent (when assigned):** may own approved implementation tasks following the same workflow, safeguards, reporting, and permission model as OpenCode.
- **ChatGPT Account 2 — Independent read-only reviewer:** inspects actual implementation, architecture, security, and focused diff. Read-only.
- **Kimi — Architecture discussion, research and critique.**
- **DeepSeek Web — Research and technical preparation only. Not DeepSeek Harness; do not assign implementation work.**
- **OpenHands — Repository exploration and planning; isolated implementation only when explicitly assigned and not concurrently editing with OpenCode or Kilo.**
- **Claude — Limited reserve for difficult implementation or security/architecture review.**
- **DeepSeek Harness — Unavailable until the Project Owner says otherwise.**
- **User — Project Owner:** final authority, approves scope/architecture and standing automation (D-017 accepted), controls separately required approvals, performs manual testing, and makes final acceptance decisions.

Only one agent may modify the shared working tree at a time. All participating agents must verify and report the same baseline SHA. Routine implementation automation for approved implementation tasks is governed by accepted decisions D-017, D-007, and D-008, subject to safeguards and protected-operation approvals.

### Instruction-file synchronization

`CLAUDE.md`, `ai-tasks.md`, `AGENTS.md`, `roadmap.md`, `current-state.md`, `decisions.md`, and `commands.txt` are updated in `DK-DEV-AUTOMATION-01-FIX1` against latest evidenced remote baseline `f09f236bcbc162387b572e0ed8cee4d29949a816` (2026-11-13). This synchronization records accepted D-017 automation policy reconciliation, corrected baseline/evidence wording, and verified EF CLI command guidance; it does not constitute independent review acceptance (except as separately reported), Project Owner acceptance, or authorization for implementation/tests/builds/migrations outside approved task scope. Prior synchronization remains historically evidenced (including `DK-DOCS-SYNC-03` and `DK-DEV-AUTOMATION-01` records).

## Active review and planning items (evidence at current verified baseline `7ed4c61`)

| Item | Status | Evidence / notes |
|---|---|---|
| `DK-ACCESS-PLATFORM-SCOPE-01` | **Under review / Request Changes (historical baseline `70d757fd`, not current HEAD `c9dbbe12`)** | Commit `70d757fd099c58de5976383677255ce64aac15e3` (2026-11-13). Confirmed issues: error-code propagation defect in both `PlatformMembershipService.AddMemberAsync` and `RevokeMemberAsync` (guard unauthenticated result collapsed to `access.denied`); earlier finding that guard does not explicitly check `IsAuthenticated`; insufficient stale-cache test setup and missing Revoke regression cases; out-of-scope Tenant test `CoreKit.Tenant.Tests/TenantAccessAuthorizationTests.cs` addition assessed separately. Confirmed commit/push governance violation recorded (no retrospective approval). Not accepted. Platform authorization remains outstanding at the current baseline `c9dbbe12`. |
| `DK-IDENTITY-BOUNDARY-01` | **Blocked** (investigation completed, read-only; no implementation) | Requested baseline `63a0eb183d4fcd53ec3d18c59861aceb3df4a6f5` differs from actual HEAD `c9dbbe12e4611883ad4374f84067efc99b03ff29`; task stopped for baseline reconciliation before implementation planning/execution. Requires Project Owner approval of required architectural decisions and baseline reconciliation. Scope is planning/investigation only: address `ICurrentUser` coupling (the `ICurrentUser` contract lives in `CoreKit.Access.Abstractions`; the `CoreKit.SharedKernel/` folder is empty and unreferenced at baseline `c9dbbe12`) while preserving Identity behavior and D-002/D-015/D-016 boundaries. Does not authorize implementation. |
| `DK-IDENTITY-BOUNDARY-01A–01E` | **Proposed task sequence (not approved)** | Proposed split: 01A design/contract approval, 01B Access-owned platform decision, 01C Tenant rewiring/fail-closed, 01D SharedKernel/Identity boundary cleanup (no-op at baseline `1c832de` because `CoreKit.SharedKernel` is empty/unreferenced), 01E docs/status alignment if approved. Planning proposal only; not approved implementation work and not scheduled without explicit Project Owner decisions/approval. |

## Next proposed work

Follow the ordered micro-slices in `roadmap.md`, subject to review outcomes and owner decisions.

- Resolve `DK-ACCESS-PLATFORM-SCOPE-01` Request Changes before treating Access platform-membership scope hardening as accepted (correction task/micro-slice requires separate approved briefing).
- Reconcile baseline and obtain required architectural decisions for `DK-IDENTITY-BOUNDARY-01` before any implementation; this is separate from the Access platform-membership fix.
- This documentation synchronization (DK-DOCUMENTATION-SYNC-04 at baseline `7115b14`) aligns all six core documents with the historical audit baseline `1c832de`; it does not itself complete S1, which awaits independent review and Project Owner acceptance. The current task baseline `c9dbbe12` (DK-DOCUMENTATION-FINALIZE-06) further reconciles architecture decisions and roadmap statuses.
- Migration artifacts committed at `43c2179` require review; their presence does not authorize database application. No tests, builds, migrations, or Git write operations are authorized by this documentation sync.

### Reconciliation note (DK-DOCUMENTATION-FINALIZE-05)

The audit report `DK-FULL-APPLICATION-ARCHITECTURE-AUDIT-01` and remediation report `DK-FULL-APPLICATION-REMEDIATION-01` were inspected against baseline `1c832de`. The remediation report's rewrite of `CoreKit.Tenant.Tests/TenantMemberRemovedAccessCacheTests.cs` is committed at `7115b14` but is not build-verified or test-verified; cache-invalidated regression coverage is not confirmed until its assertions are independently reviewed. D-02 (event-handler implementation) is committed at `1c832de` but durable retry, cross-instance invalidation, and runtime verification remain unverified. The documentation-sync commit `7115b14` reconciles all six core documents with `1c832de` and records D-018 (No SharedKernel), D-03 (JWT revocation — open Owner decision), and D-18 (rate limiting — open Owner decision). Baseline `c9dbbe12` (DK-DOCUMENTATION-FINALIZE-06) is the current task baseline; no additional source or runtime verification has been performed since `7115b14`.

## Update log

| Date | Change | Evidence |
|---|---|---|
| 2026-10-23 | M0 baseline inspection findings recorded | User-provided report for commit `9555b8c` |
| 2026-11-04 | Documentation package refreshed to reflect M0 report, approved M1 decisions, micro-slice workflow and Git message guidance | Prepared documentation package; verify against actual repository before applying |
| 2026-11-08 | Snapshot refreshed at `daa7326`; historical M0 baseline preserved; current snapshot recorded separately; accepted decisions separated from implementation | Committed-file inspection at `daa7326`; local working-tree details were separately reported and not remotely verified |
| 2026-11-08 | Current snapshot updated for `43c2179`; committed Access, Identity and Tenant migration artifacts recorded; evidence labels and agent-allocation discrepancy clarified | GitHub committed-file inspection at `43c2179`; local working-tree and database state not verified |
| 2026-11-08 | Snapshot refreshed to `576c84d`; S2.1 recorded Under review; architecture-test evidence updated; obsolete instruction-file discrepancy removed; mandatory task briefing recorded in `ai-tasks.md` §13 | Committed-file inspection at `576c84d`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | Snapshot refreshed to `5dfa796ca52fc0b122b3efea182f2dc7dbd5cde9`; D-016 (Tenant requires Access for authorization) recorded; D-015 direct-dependency marked completed at source level; Tenant–Access integration, platform authorization and per-operation enforcement recorded as Pending; agent allocation synchronized to current team | Committed-file inspection at `5dfa796c`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | DK-TENANT-ACCESS-DOCS-FIX1 corrections committed at `45a578958e555b9cb3fc77a5ae602f796fd270b1`; correction task `DK-TENANT-ACCESS-DOCS-FIX1` addresses review findings; S1 remains Under review; source-inspection baseline preserved at `5dfa796c` | Committed-file inspection at `45a5789`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | DK-TENANT-ACCESS-DOCS-FIX2 governance consistency corrections committed at `d7585c34cd1c06d5119bb3c51ea29c7178273b39`; corrected commit attribution distinguishing `beb0a0b` from `d7585c3`; instruction-file synchronization references updated; source-inspection baseline preserved at `5dfa796c`; DeepSeek Web restricted to research and technical preparation only | Committed-file inspection at `d7585c3`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | DK-TENANT-ACCESS-DOCS-FIX4 historical governance commit attribution clarification committed at `0504f3a14d707a8ceb4a1baf0dcb161e4e3b3de2`; distinguished snapshot baseline `beb0a0b` from current documentation HEAD `0504f3a`; source-inspection baseline preserved at `5dfa796c`; no claim of source re-inspection at any documentation-only commit | Committed-file inspection at `0504f3a`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | DK-TENANT-ACCESS-DOCS-FIX3 corrections committed at `335b9ed750e3f3b9f222c678067bb802d1b7e984`; commit description references FIX2 work (immutable historical message preserved); corrected commit attribution; DeepSeek Web restricted to research and technical preparation only | Committed-file inspection at `335b9ed`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | feat(tenant): implement Tenant–Access integration foundation committed at `aee85c208207241a308f93d49507b3eb41ade6f2`; adds `TenantAccessScopeContext` (`IAccessScopeContext`) and `TenantAccessMembership` (`IAccessMembership`) adapters; registers both with `AddScoped` in `TenantServiceCollectionExtensions`; `CoreKit.Tenant.csproj` now references `CoreKit.Access`; adds `TenantAccessIntegrationTests` covering valid membership, non-membership, missing context/user, tenant-owner-not-owner, and DI registration; D-016 integration foundation completed | Committed-file inspection at `aee85c2`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | feat(architecture): allow Tenant to reference CoreKit.Access committed at `c9d4b36ee56450d214599c4103520278442efd46`; snapshot updated to `beb0a0b`; source-inspection baseline preserved at `5dfa796c`; Git, build/test and agent-role wording harmonized | Committed-file inspection at `c9d4b36`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | fix(identity): add lockout columns to `Identity_Users` committed at `b6d7ff2c3217c0ad610c0f90daa7538c659522c0`; adds `AccessFailedCount` and `LockoutEnd` columns via migration `20261112110000_AddLockoutColumns` | Committed-file inspection at `b6d7ff2`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | fix(tenant): remove unused Identity UserSecrets package committed at `9051158cb53887b90f1b940a55636a2307641f28`; removes `CoreKit.Identity` package reference from `CoreKit.Tenant.csproj` and `CoreKit.Tenant.Tests.csproj`; D-015 direct-dependency requirement remains met | Committed-file inspection at `9051158`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | fix(tenant): fix Identity integration test compilation committed at `c3c1c235050f68873519007d8e03850178a26a01`; fixes `CoreKit.Tenant.Tests/TenantAccessIntegrationTests.cs` compilation errors | Committed-file inspection at `c3c1c23`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | fix(tenant): fix Identity access integration test compilation committed at `be436b3fc19b61b9501c0a4fbf6e9c3b8f71a8e1`; fixes `CoreKit.Tenant.Tests/TenantAccessIntegrationTests.cs` compilation errors | Committed-file inspection at `be436b3`; local tree clean at inspection; no tests or builds run |
| 2026-11-12 | test(tenant): remove unused variable from lockout test committed at `26f540584cf0703a1f7abde039cb0f7c3946d41d`; removes unused `AccessFailedCount` variable from `UserServiceTests.cs` | Committed-file inspection at `26f5405`; local tree clean at inspection; no tests or builds run |
| 2026-11-12 | update the comands file committed at `08e307c22bbf5ccaa8bff7ee9850d9be82615164`; updates `commands.txt` with PostgreSQL connection string guidance | Committed-file inspection at `08e307c`; local tree clean at inspection; no tests or builds run |
| 2026-11-12 | docs: clarify historical governance commit attribution committed at `959c6ab99f406eec56161f7dae71f94a5f432771`; snapshot updated to `959c6ab`; source-inspection baseline preserved at `5dfa796c`; no claim of source re-inspection at any documentation-only commit | Committed-file inspection at `959c6ab`; local tree clean at inspection; no tests or builds run |
| 2026-11-13 | `DK-DOCS-SYNC-03`: synchronized six core Markdown documents against latest verified local baseline `70d757fd099c58de5976383677255ce64aac15e3` (clean working tree). Recorded `DK-ACCESS-PLATFORM-SCOPE-01` as Under review / Request Changes with confirmed findings (error-code propagation defect in both platform membership operations, earlier guard `IsAuthenticated` finding, insufficient stale-cache setup/missing Revoke regressions, out-of-scope Tenant test assessed separately) and recorded commit/push governance violation without retrospective approval. Recorded `DK-IDENTITY-BOUNDARY-01` as Blocked (baseline mismatch vs requested `63a0eb1`, investigation only, no implementation) and `DK-IDENTITY-BOUNDARY-01A–01E` as proposed/not approved. Preserved accepted D-002/D-015/D-016 meanings. No source, tests, builds, migrations, or Git write operations performed. | Local Git inspection at `70d757fd` (`rev-parse`, `log -1`, `status --short`, `show`); remote state not queried |
| 2026-11-15 | `DK-DOCUMENTATION-SYNC-04`: synchronized six core Markdown documents (`CLAUDE.md`, `roadmap.md`, `current-state.md`, `decisions.md`, `architecture.md`, `ai-tasks.md`) against latest verified remote baseline `1c832de9710630fc9f0e8bbffd43ca08fe382c2d` (2026-11-15 13:57:49 +0500, `Fix Tenant membership revocation -> Access cache consistency`). Removed SharedKernel as an active architectural component (empty folder, no `.csproj`, no source, unreferenced; contracts live in module-owned `Abstractions` namespaces). Recorded D-018 superseding the SharedKernel aspects of D-015/D-016 and confirming no legacy Identity RBAC data to preserve and the table-drop migration acceptable. Kept D-03 (JWT revocation) and D-18 (rate limiting) as unresolved Owner decisions. Updated Tenant project-reference findings (references only `CoreKit.Access`). No source, tests, builds, migrations, database operations, or Git writes performed. | Local + remote Git inspection (`git rev-parse HEAD`, `git log -1`, `git status --short`, `git ls-remote origin master`); both local HEAD and remote `origin/master` are `1c832de` |
| 2026-11-15 | `DK-DOCUMENTATION-FINALIZE-05`: final documentation reconciliation against current verified baseline `7115b14b9d90da5617e55836283e8d5dd00810db` (2026-11-15 15:07:48 +0500). Updated `current-state.md` snapshot table and historical baselines to reference `7115b14` as current HEAD (preserving `1c832de` as historical audit baseline). Corrected D-01: test rewrite of `CoreKit.Tenant.Tests/TenantMemberRemovedAccessCacheTests.cs` is committed at `7115b14` but not build-verified or test-verified; cache-invalidated regression coverage not confirmed until assertions independently reviewed. Corrected S4.2 (roadmap.md): Tenant–Access adapter implementation exists at commit `aee85c2` but is architecturally unresolved under the Owner-approved direction rejecting cross-module adopter/interface patterns; status changed to Under review, redesign and Owner approval outstanding. Corrected S6.2 (roadmap.md): `RemoveIdentityRbacTables` migration exists but milestone requires evidence Identity no longer owns active RBAC services/endpoints/persistence/consumers and Access is sole owner; status changed to Under review; no legacy data-transfer/recovery requirement added. Replaced non-standard "Completed at source level" statuses for D-015/D-016 with "Under review". Updated `1c832de`-as-current-HEAD references to `7115b14`. No source, tests, migrations, database operations, or Git writes performed. | Local + remote Git inspection (`git rev-parse HEAD`, `git log -1`, `git status --short`, `git ls-remote origin master`); both local HEAD and remote `origin/master` are `7115b14b9d90da5617e55836283e8d5dd00810db` |
| 2026-11-15 | `DK-DOCUMENTATION-FINALIZE-06`: reconcile architecture decisions and roadmap against current verified baseline `c9dbbe12e4611883ad4374f84067efc99b03ff29` (2026-11-15 16:01:53 +0500). Updated `current-state.md` snapshot table, historical baselines, and active review items to reference `c9dbbe12` as current task baseline (preserving `7115b14` and `1c832de` as historical baselines). Updated `roadmap.md` S2.2 to remove SharedKernel from active boundary description; S1 baseline reference to `c9dbbe12`; DK-IDENTITY-BOUNDARY-01 actual HEAD reference to `c9dbbe12`. Updated `architecture.md` Tenant–Access integration section to document the existing adapter implementation as architecturally unresolved under the Owner-approved direction rejecting cross-module adopter/interface patterns, not as the approved target architecture. No source, tests, migrations, database operations, or Git writes performed. | Local + remote Git inspection (`git rev-parse HEAD`, `git log -1`, `git status --short`, `git ls-remote origin master`); both local HEAD and remote `origin/master` are `c9dbbe12e4611883ad4374f84067efc99b03ff29` |
| 2026-11-16 | `PR-8` Tenant slice implemented at baseline `9ebb6cffd6f684dbb49279872b3ec8ba650c6fc2` (uncommitted). Tenant now stages `tenant.lifecycle.created`, `tenant.lifecycle.status_changed` and `tenant.lifecycle.deleted` in the same transaction as the tenant change, which fixes the PR-3 defect where creation wrote no outbox events and the initial owner never reached the Access projection. Adds a snapshot reader (head-read, members, head-read again, with a bounded retry) that stays consistent with the tenant's event sequence, a comparer that separates in-flight, Access-ahead, same-sequence drift and in-sync, a platform-guarded recovery service (`RebuildTenantAsync`, `RebuildAllAsync`, `ReconcileTenantAsync`, `ReconcileAllAsync`, `RequeueDeadLettersAsync`) and an opt-in `ITenantProjectionGateway` port plus `AddTenantProjectionRecovery`. The Access half of PR-8 is NOT implemented: this baseline has no PR-5 event consumer/stream/processed-event store and no PR-6 freshness watermark/quarantine/projection membership adapter, so the consumer, snapshot installer, projection reader and tenant-state gate cannot compile. No Access files were changed and no migration is needed for this slice. Verification observed on 2026-11-16: `dotnet build Dastarkhwan.slnx` succeeded with 0 warnings/errors; `dotnet test CoreKit.Tenant.Tests` passed 393/393 (including 12 new lifecycle-outbox and recovery tests); `CoreKit.Architecture.Tests` passed 60/60. | Local build and test runs at `9ebb6cf`; working tree contains the changed Tenant files and the new Tenant tests |
| 2026-11-16 | `PR-5`/`PR-6`/`PR-8` Access slice implemented at baseline `9ebb6cffd6f684dbb49279872b3ec8ba650c6fc2` (uncommitted); this resolves the Access-half gap recorded in the `PR-8` row above. Access now owns the tenant membership projection: an event stream cursor and processed-event dedup store, a freshness watermark and per-tenant quarantine, and a lifecycle-state copy. The consumer applies Tenant membership and lifecycle events with at-least-once redelivery, rejects a sequence gap instead of skipping it, never lowers a member's generation, and treats tenant deletion as terminal. The snapshot installer replaces a tenant's rows in one transaction, ignores a snapshot older than the cursor, repairs same-sequence drift, and refuses a tenant already deleted in Access. The projection membership adapter answers only for a tenant scope and only while `Access:MembershipSource` is `Projection`, and fails closed (denies) when the heartbeat is missing or stale, the tenant is quarantined, its lifecycle state is unknown, or it is not Active; a tenant member is never an Access owner. The cutover stays configuration-only (default `TenantAdapter`), and a hosted service writes the heartbeat while projection mode is selected. New EF migration `20261116043029_AddAccessMembershipStreamAndFreshness` adds five Access tables (membership stream cursors, processed events, membership watermarks, quarantined tenants, tenant states) and the unique tenant/sequence index; no existing table changed. `ITenantProjectionGateway` is still not registered: Tenant's recovery needs a host-side adapter that references both modules, so `TenantProjectionRecoveryService` is not resolvable until a host provides one. No database migration was applied. Verification observed on 2026-11-16: `dotnet build Dastarkhwan.slnx` succeeded with 0 warnings/errors; `dotnet test CoreKit.Access.Tests` passed 294/294 (31 new membership-flow tests plus a snapshot/live-model table-shape test); `CoreKit.Tenant.Tests` passed 393/393; `CoreKit.Identity.Tests` passed 162/162; `CoreKit.Architecture.Tests` passed 60/60. | Local build and test runs at `9ebb6cf`; working tree contains the new Access files, the EF migration and snapshot update, and the new Access tests |
| 2026-11-22 | `ef761df` P5: Atomic refresh-token rotation (C-05/H-02), JWT placeholder rejection (C-02), Projection freshness fail-closed (C-03), Owner-role lifecycle (C-04/H-08) | Local build verification at `ef761df`; all main projects build successfully; test failures are pre-existing in test fakes |
| 2026-11-16 | `PR-7` background execution context implemented at baseline `9ebb6cffd6f684dbb49279872b3ec8ba650c6fc2` (uncommitted, six new source/test files). Adds an explicit, opt-in background scope-and-actor context for Access: `AddAccessBackgroundContext` decorates the registered `IAccessScopeContext` and `IAccessCurrentUser` (wrappers bypass `HttpContext` while a job runs), grants are Data Protection payloads bound to a job kind and a required permission with a default 15-minute / maximum 1-hour lifetime, `RunAsync` re-checks the permission on fresh data in a new DI scope, `RunAsSystemAsync` runs a non-user system actor that fails closed for every user-permission check, and a startup check fails fast if a later registration replaces a wrapper. The system actor is deliberately not a user. No existing Access service was rewritten. Verification observed on 2026-11-16: `dotnet build Dastarkhwan.slnx` succeeded with 0 warnings/errors; `dotnet test CoreKit.Access.Tests` passed 262/262 (including 22 new `AccessBackgroundContextTests`); `CoreKit.Identity.Tests` passed 162/162; `CoreKit.Tenant.Tests` passed 381/381; `CoreKit.Architecture.Tests` passed 60/60. No migration and no database operation. Remaining limits: grants are replayable until expiry (exactly-once is the job store's responsibility), multi-instance hosts must share the Data Protection key ring, `ICurrentUser` and `ITenantActor` are not wrapped, and tenant-side background workflows remain for PR-8. | Local build and test runs at `9ebb6cf`; working tree contains only the six new files listed above |

## Update rules

- Before updating this file, verify actual HEAD and local working-tree state and inspect relevant code.
- Record commit hash, date and file paths or test names as evidence where possible.
- Distinguish target design, accepted decision and implemented behavior.
- Do not mark tests, builds, migrations or database operations complete unless the result was observed.
- Never include passwords, tokens, signing keys, connection strings or secret values.
- Keep this file concise. Detailed architectural rules belong in `architecture.md`, decisions in `decisions.md`, and work queues in `roadmap.md` and `ai-tasks.md`.

## Applied fixes (HEAD a57f891)
- N1: Identity outbox transport replaced (RemoveAll + AddScoped).
- B6: last-admin check moved inside transaction.
- M1: tenant exclusion prefixes corrected to /identity, /access, /tenants.
- N3: email placeholder values rejected at startup.

