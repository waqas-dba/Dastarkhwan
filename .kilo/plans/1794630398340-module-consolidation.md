# MC-01 — Three-module consolidation: remove duplication, tighten separation of concerns, loosen coupling

## 1. Task ID and name
`MC-01` — Consolidate the platform into exactly three modules (Identity, Access, Tenant), remove duplicated code and concepts, fix separation-of-concerns and coupling defects.

## 2. Purpose
The platform currently carries a fourth project (`CoreKit.SharedKernel`) whose only job is to hold contracts, plus several concepts that are declared two or three times (current user, permission checker, permission definitions, endpoint-prefix helper, result/error plumbing). Removing the extra project and collapsing the duplicate concepts makes the design easier to reason about, removes drift risk, and leaves three modules with clear ownership.

## 3. Current issue (evidence from the working tree, HEAD `b069694`)

### 3.1 A whole project exists only to hold contracts
`CoreKit.SharedKernel` (9 files, 159 lines) is referenced by Identity, Tenant and all three test projects, and is enforced by the architecture tests.

### 3.2 Duplicated concepts (confirmed in code)
| Concept | Copies | Evidence |
|---|---|---|
| `IPermissionChecker` | 2, different shapes | `CoreKit.Access/Abstractions/IPermissionChecker.cs` (2 methods) vs `CoreKit.SharedKernel/Abstractions/IPermissionChecker.cs` (1 method). The SharedKernel one has **no consumer**; Access's is the real one. |
| Current-user abstraction | 2 | `IAccessCurrentUser` (Access) and `ICurrentUser` (SharedKernel) both expose `UserId`/`IsAuthenticated`; two implementations read JWT claims (`ClaimsCurrentUser`, `CurrentUserService`). |
| `SaveOutcome` + `SaveOutcomeExtensions` + Unit of Work | 2 | `CoreKit.Access/Persistence/AccessUnitOfWork.cs` (61 lines) vs `CoreKit.Tenant/Persistence/TenantUnitOfWork.cs` (66 lines) — identical except error type and visibility. |
| Endpoint prefix helper | 3 | `NormalizePrefix` in `IdentityEndpointExtensions.cs:24`, `AccessEndpointExtensions.cs:62`, `TenantEndpointExtensions.cs:30` — identical. |
| Result / Error / ErrorKind + `ToHttpResult` | 3 | `IdentityResult.cs`, `AccessResult.cs`, `TenantResult.cs` (+ `*ResultExtensions.cs`) — same shape, module-specific error vocabulary. |
| DbContext configurator | 3 | `IdentityDbContextConfigurator.cs`, `AccessDbContextConfigurator.cs`, `TenantDbContextConfigurator.cs` — same pattern. |
| Permission-definition shape | 3 | `IdentityAccessPermissionNames` uses `(string Name, string Description)` tuples; `AccessPermissionNames` uses `AccessPermissionDefinition`; `TenantPermissionNames` declares its own `TenantPermissionDefinition`. |
| `DatabaseOptions` | 1 + 2 in flight | SharedKernel copy, plus the per-module copies already staged. |

### 3.3 Separation-of-concerns / coupling defects
| Defect | Evidence |
|---|---|
| Identity reaches into Access's registration | `Identity/Extensions/ServiceCollectionExtensions.cs:68` calls `AddAccessPermissions(...)`; `using CoreKit.Access.Extensions;`. |
| Tenant implements Access ports inside Tenant | `Tenant/Integration/TenantAccessScopeContext.cs`, `TenantAccessMembership.cs` — adapter pattern, but it is the only reason Tenant references Access. |
| Three endpoint-authorization filters with near-identical logic | `Access/Endpoints/RequireAccess.cs`, `Access/Endpoints/RequireAccessAttribute.cs`, `Tenant/Authorization/EndpointPermissionExtensions.cs`. |
| Service locator in filters | All three filters call `context.HttpContext.RequestServices.GetRequiredService<...>()` instead of taking dependencies. |
| `TenantActor.IsPlatformAdmin` reads claim permissions | `Tenant/Abstractions/TenantActor.cs:16` uses `ICurrentUser.Permissions`; tokens carry no permission claims, so it is **always false** (live-confirmed: every platform-only tenant call returns `tenant.platform_only`). |
| Two current-user implementations | `Access/Services/DefaultPorts.cs` (`ClaimsCurrentUser`) and `Identity/Services/CurrentUserService.cs` both read the same claims. |

### 3.4 Duplication that is justified and will stay (documented, not "fixed")
- `SaveOutcome`/Unit of Work: each module has its own `DbContext`, so a shared implementation would re-introduce a shared kernel.
- `Result`/`Error`/`ErrorKind`: each module owns its own error vocabulary and HTTP status mapping.
- `DbContextConfigurator`: each module configures its own provider/connection string.

## 4. Proposed changes (phased; each phase builds and tests green before the next)

### Phase 1 — Remove `CoreKit.SharedKernel` (no behavior change)
1. Re-home contracts (already staged):
   - `CoreKit.Access/Abstractions/`: `ICurrentUser`, `IAccessTokenClaimsContributor`.
   - `CoreKit.Tenant/Abstractions/`: `ICurrentTenant` + `TenantNotResolvedException`, `ITenantMembershipReader` + `TenantMemberInfo`.
   - `CoreKit.Tenant/Authorization/`: `PermissionPolicy`, `PermissionSeed`.
   - `CoreKit.Tenant/Events/`: `IIntegrationEvent`, `IEventHandler<T>`, `TenantCreated`, `TenantDeleted`, `TenantMemberRemoved`.
   - `CoreKit.Identity/Persistence/DatabaseOptions.cs` and `CoreKit.Tenant/Persistence/DatabaseOptions.cs`.
2. Delete the dead `CoreKit.SharedKernel.Abstractions.IPermissionChecker` (no consumer).
3. Delete `CoreKit.SharedKernel/` and its `Dastarkhwan.slnx` entry.
4. Remove the project reference from `CoreKit.Identity`, `CoreKit.Tenant`, `CoreKit.Identity.Tests`, `CoreKit.Access.Tests`, `CoreKit.Tenant.Tests`, `CoreKit.Architecture.Tests`.
5. Update `using`/global-using statements in Identity (5), Tenant (9), SampleHost (1), and the three test `GlobalUsings.cs`.
6. Update architecture tests: `SolutionInventory.ProductionModules` / `ExpectedAssemblyNames`, `ModuleBoundaryMatrix` allowlists and `ProductionModules`; delete the two SharedKernel-only facts in `ModuleBoundaryTests.cs`.

### Phase 2 — Remove duplication
1. One permission-definition shape: use `AccessPermissionDefinition` for Access, Tenant and Identity catalogs; delete `TenantPermissionDefinition` and the tuple list in `IdentityAccessPermissionNames`.
2. One `NormalizePrefix` in `CoreKit.Access/Common/EndpointRoutes.cs`; remove the three local copies and call the shared one.
3. Keep (documented) the per-module Unit of Work, Result/Error and DbContext configurator.

### Phase 3 — Separation of concerns and loose coupling
1. `ICurrentUser : IAccessCurrentUser`; register the Identity implementation for both so there is one current-user implementation; keep `ClaimsCurrentUser` only as the Access fallback.
2. One endpoint-authorization helper in Access with constructor-injected dependencies (no service locator); delete `RequireAccessAttribute` and Tenant's `RequireAccessPermission` in favour of it.
3. `TenantActor.IsPlatformAdmin` asks Access (`IPermissionChecker.HasPlatformPermissionAsync(TenantPermissionNames.Platform)`) instead of reading claim permissions. This is the live `tenant.platform_only` defect (C2) and is behavior-changing, so it lands last, behind tests.

## 5. Expected result
- Three production modules: `CoreKit.Identity`, `CoreKit.Access`, `CoreKit.Tenant` (+ `CoreKit.SampleHost` as a dummy host).
- No `CoreKit.SharedKernel` anywhere; `grep -r "CoreKit.SharedKernel"` returns nothing outside build output.
- One definition each of current-user, permission checker, permission definition, and endpoint-prefix helper.
- One endpoint-authorization filter, with dependencies injected.
- Platform-only tenant operations work for an administrator (Phase 3.3).
- `dotnet build Dastarkhwan.slnx` succeeds; `dotnet test` is green for Architecture, Identity, Access, Tenant.

## 6. Files and scope
**In scope:** `CoreKit.SharedKernel/**` (delete); new contract files in Access/Tenant (staged); `using`/global-using updates in Identity, Tenant, SampleHost, tests; `Dastarkhwan.slnx`; six `.csproj`; `CoreKit.Architecture.Tests/{SolutionInventory,ModuleBoundaryMatrix,ModuleBoundaryTests}.cs`; `IdentityAccessPermissionNames.cs`, `TenantPermissionNames.cs`, `AccessPermissionNames.cs`; the three `*EndpointExtensions.cs`; the three endpoint-filter files; `CurrentUserService.cs`, `ICurrentUserService.cs`, `DefaultPorts.cs`, `TenantActor.cs`; `TenantAccessScopeContext.cs`, `TenantAccessMembership.cs`.

**Out of scope:** auth/lockout/token logic; DB schema and migrations; Access policy evaluation semantics; Tenant lifecycle rules; SampleHost behavior; any product module (Store/Catalog/...). No secret values.

## 7. Responsible agents
- Preparation: Kilo
- Implementation: Kilo
- Independent review: ChatGPT Account 2 (read-only) — **required (architectural)**
- Coordination: ChatGPT Account 1
- Final authority: User

## 8. Acceptance criteria
1. `CoreKit.SharedKernel/` absent; no solution entry; no `CoreKit.SharedKernel` references.
2. `dotnet build Dastarkhwan.slnx -c Debug` succeeds with no errors.
3. `dotnet test` passes for `CoreKit.Architecture.Tests`, `CoreKit.Identity.Tests`, `CoreKit.Access.Tests`, `CoreKit.Tenant.Tests`.
4. One definition each of `ICurrentUser`, `IPermissionChecker`, permission-definition type, and `NormalizePrefix`.
5. No `RequestServices.GetRequiredService` inside the endpoint-authorization filters.
6. Platform-only tenant operations succeed for a seeded administrator (manual check against the dev DB).

## 9. Risks and dependencies
- Phase 3.3 changes `IsPlatformAdmin` from sync to async and ripples through `TenantAccessGuard`, `TenantService`, `TenantMembershipService` and their tests. Highest risk; isolated last.
- Phase 3.2 changes the public endpoint-filter surface (removes `RequireAccessAttribute`, replaces `RequireAccessPermission`); update SampleHost and any tests that call them.
- Architecture tests are the safety net; they must move in the same change.
- Phase 1 is mechanical but wide (~20 files).

## What I need from you
**Your decision:** approve the three-phase plan, and confirm that keeping per-module Unit of Work / Result-Error / DbContext configurator (rather than forcing a shared implementation) is acceptable.
**Your action:** none.
**Your verification:** run the build and the four test projects once after each phase.
**Your acceptance:** build green, tests green, and you confirm the module layout.

---

## Status (implementation record)

**Phase 1 — done.** `CoreKit.SharedKernel` deleted (project, folder, solution entry, all six project references). Contracts re-homed: `ICurrentUser`, `IAccessTokenClaimsContributor` → `CoreKit.Access.Abstractions`; `ICurrentTenant` + `TenantNotResolvedException`, `ITenantMembershipReader` + `TenantMemberInfo` → `CoreKit.Tenant.Abstractions`; `PermissionPolicy`, `PermissionSeed` → `CoreKit.Tenant.Authorization`; integration events → `CoreKit.Tenant.Events`; `DatabaseOptions` → per-module `CoreKit.Identity.Persistence` / `CoreKit.Tenant.Persistence`. The dead `CoreKit.SharedKernel.Abstractions.IPermissionChecker` was dropped (Access's own `IPermissionChecker` is the real one). Architecture inventory, boundary matrix and the two SharedKernel-only facts updated.

**Phase 2 — done.** Permission definitions unified on `AccessPermissionDefinition` (`TenantPermissionDefinition` and the Identity tuple list deleted; `PermissionSeed` + `TenantPermissionSeeds` deleted as dead). `NormalizePrefix` consolidated into `CoreKit.Access.Common.EndpointRoutes` (three copies removed). Per-module Unit of Work, Result/Error and DbContext configurator kept and documented.

**Phase 3 — partially done.** `ICurrentUser : IAccessCurrentUser` and Identity's `CurrentUserService` is registered for both, so there is one current-user implementation (Access's `ClaimsCurrentUser` remains only as the no-Identity fallback). The three endpoint-authorization filters were collapsed to one: `RequireAccessAttribute` deleted, Tenant's `RequireAccessPermission` is now a thin wrapper over Access's `RequireAccess`, and the duplicated filter body is gone.

**Phase 3.3 — done.** `TenantActor` no longer reads `ICurrentUser.Permissions` (always empty). The platform flag now asks Access, the authority for permissions:

- `ITenantActor.IsPlatformAdmin` → `Task<bool> IsPlatformAdminAsync(CancellationToken)`.
- `ITenantPlatformAccess.IsGranted` → `Task<bool> IsGrantedAsync(CancellationToken)`.
- `TenantActor(ICurrentUser?, IPermissionChecker?)` answers via `HasPlatformPermissionAsync(TenantPermissionNames.Platform, ct)`, and fails closed (false) when Access is not registered.
- `TenantAccessGuard` methods became async and are awaited at every call site in `TenantService`, `TenantMembershipService`, `TenantAuditLog` and `TenantSettingsService`.
- Tests: `FakeTenantActor.IsPlatformAdminAsync` added; `TenantActorTests` rewritten around `FakePermissionChecker`; guard tests converted to async; one new DI test (`ThePlatformFlagIsAnsweredByAccess_ThroughTheContainer`) proves the container injects the real `IPermissionChecker` into `TenantActor` and that the guard then allows platform scope.

**Verification:** `dotnet build Dastarkhwan.slnx -c Debug` → 0 warnings, 0 errors. `dotnet test` → Access 222 passed, Identity 162 passed, Tenant 350 passed, Architecture 60 passed, 0 failed. The long-standing `IvtMechanismScannerTests.Ignores_file_level_alias_from_another_file` failure was fixed here: the scanner was correct and the test was wrong (both files declared their own alias, so two findings is right, and the test could not detect leakage at all). It now has FileB use the alias without declaring it, and a companion test covers the declaring-file case.

**Acceptance criterion 6 (manual platform-admin check) — not performed.** The dev DB holds platform administrators but their passwords are unknown (the seeder skips because an Administrator already exists), and resetting or adding users would be an unapproved data mutation. Automated coverage replaces it: the DI test plus `AccessPermissionChecker.HasPlatformPermissionAsync` tests in the Access suite. The owner's manual check remains outstanding.

**Remaining justified duplication:** `SaveOutcome` + `SaveOutcomeExtensions` (per-module Unit of Work, different DbContext and error type) and `DatabaseOptions` (per-module DbContext configuration).