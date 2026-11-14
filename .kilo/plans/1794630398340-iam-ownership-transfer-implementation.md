# DK-IAM-OWNERSHIP-TRANSFER-IMPLEMENT-01 — Deep Implementation Plan

Baseline: `1b9a046` (master). Task: make `CoreKit.Access` the sole owner of
roles/permissions/assignments/authorization; remove legacy RBAC from
`CoreKit.Identity`; Tenant keeps using Access authorization with its own
membership.

Owner decisions already given (this session):
- S1: drop `permission`/`role` claims from the JWT (breaking) — approved.
- S2: generate the destructive EF migration; do NOT execute it — approved.
- S3: "Administrator" = a platform member holding an `Administrator`-named
  Access role in the platform scope — approved.
- S4: Identity → `CoreKit.Access` project reference is acceptable — approved.

---

## 1. Resolved dependency facts (Phase B)

### D1 — SharedKernel blocker is removable without editing SharedKernel
The only SharedKernel coupling in Identity's authorization is
`CoreKit.Identity/Authorization/IdentityPolicies.cs` → `CoreKit.SharedKernel.PermissionPolicy`
(lines 3, 12, 15). Deleting Identity's `Authorization/` folder removes it. No
SharedKernel change is needed and none is permitted.

### D2 — Dependency direction: Identity → Access (new, approved)
`CoreKit.Access` already exposes the cross-module port
`IPermissionChecker` (`CoreKit.Access/Abstractions/IPermissionChecker.cs`,
impl `AccessPermissionChecker`, registered in `AddAccess`). Its doc states the
intent: "other modules never reference Access" — i.e. consume via DI. Identity
must add a project reference to `CoreKit.Access` to use the type. This is the
"Identity may authorize identity-management HTTP actions via Access" clause.

### D3 — Hard boundary constraint (must be updated as part of this task)
`CoreKit.Architecture.Tests/ModuleBoundaryMatrix.cs` currently forbids
Identity → Access in BOTH allowlists:
- `CompiledAssemblyAllowlist["CoreKit.Identity"] = { "CoreKit.SharedKernel" }`
- `ProjectReferenceAllowlist["CoreKit.Identity/...csproj"] = { SharedKernel }`

This is an architectural change and is explicitly approved (S4). The matrix
must be updated to add `CoreKit.Access` to both entries for Identity.

### D4 — Access is dependency-free and must stay so
`CoreKit.Access.csproj` has no project references and the matrix enforces
`CompiledAssemblyAllowlist["CoreKit.Access"] = {}`. Access must not reference
SharedKernel or Identity. All new cross-module surface must live in Access and be
consumed by Identity via DI (Identity references Access, not the reverse).

### D5 — Token contract is affected (S1)
`TokenService.CreateAccessToken` embeds `role` + `permission` claims from
Identity RBAC (`TokenService.cs:52-53`; `ReservedClaims` at :14-17). Access reads
permissions **fresh from DB** (`AccessService.ResolveAsync`) and never from JWT;
`IAccessCurrentUser` only reads `sub`. So Identity's `permission`/`role` JWT
claims are already non-authoritative for Access. Removing them is the breaking
change (S1). `CurrentUserService.Roles/Permissions` (claim readers) become dead
and are removed with the RBAC.

---

## 2. Platform-membership design (the crux)

### Current platform model in Access
- Fixed `AccessScope.PlatformId` = `...0001` (`AccessScope.cs:14`).
- `AccessPlatformMember` table: `(PlatformId, UserId, RevokedAt, MembershipGeneration)`.
- `PlatformAccessMembership` adapter reads `AccessPlatformMember` where
  `RevokedAt == null` (`PlatformAccessMembership.cs:14-23`).
- `PlatformMembershipService.AddMemberAsync/RevokeMemberAsync/IsMemberAsync`
  (guarded by `PlatformMembersManage` via `RequirePlatformScopePermissionAsync`).
- `AccessAssignment` stores platform-scope role assignments; `MembershipGeneration`
  invalidates old assignments when a member is removed and rejoins.

### Decision: explicit platform membership (Model 1)
The `MembershipGeneration` mechanism only makes sense with explicit membership
records, so platform membership stays in `AccessPlatformMember`. Identity users
become platform members explicitly.

**Problem:** `PlatformMembershipService.AddMemberAsync` is permission-guarded, so
it cannot be used for system provisioning (user creation). A trusted, unguarded
path is required.

**Resolution — new trusted Access maintenance surface** (in `AccessMaintenance`,
which is already "trusted, no permission checks"):
- `Task AddPlatformMemberAsync(Guid userId, CancellationToken ct)` — idempotent
  add/reactivate of the platform membership row (mirror of
  `PlatformMembershipService.AddMemberAsync` minus the guard), bumps generation on
  reactivation, invalidates cache.
- `Task RemovePlatformMemberAsync(Guid userId, CancellationToken ct)` — idempotent
  revoke (mirror of `RevokeMemberAsync` minus the guard), bumps generation,
  invalidates cache.

Identity calls these on user create / deactivation / delete. `AccessMaintenance`
already has `RemoveUserAsync(scope, userId)` (removes assignments for a scope) and
`RemoveScopeAsync`; the new platform-membership methods complement them.

**Existing users (STOP — DB):** a one-time data migration must add every existing
active Identity user as an active platform member (and assign the `Administrator`
role to the seeded admin). This is a destructive/irreversible DB operation and is
NOT executed by this task (S2).

### Administrator guard (S3)
`AdministratorGuard.IsLastActiveAdministratorAsync(userId)` must become
platform-scope-aware. "Active" = Identity `User.IsActive`. The join of
"platform members with the Administrator role" (Access) ∩ "active Identity users"
(Identity) must be computed. Resolution:
- Add an Access query `Task<IReadOnlyList<Guid>> ListUserIdsWithRoleAsync(
  AccessScope scope, string roleName, CancellationToken ct)` on
  `IAccessAssignmentRepository` (or a small `IAccessAssignmentQuery`), returning
  user ids currently holding a role by name in a scope (current generation only).
- Identity's `AdministratorGuard` becomes: `adminIds = access.ListUserIdsWithRoleAsync(platformScope, "Administrator")`;
  intersect with active Identity user ids (Identity knows `IsActive`); return
  `activeAdmins.Count == 1 && activeAdmins[0] == userId`.
- `AdministratorGuard` stays in Identity (it needs Identity's active-user set) but
  sources the role set from Access. It consumes the Access query via DI.

---

## 3. Scope context for Identity user-management authorization

Identity user-management endpoints are platform-level (no tenant). Access's
`IPermissionChecker.HasPermissionAsync` resolves the **ambient** scope via
`IAccessScopeContext.Current`. With no tenant, `DefaultAccessScopeContext` returns
null → fail closed. So a plain `IPermissionChecker` call would always deny.

**Decision:** add an explicit platform-scoped permission check to the
`IPermissionChecker` port:
- `Task<bool> HasPlatformPermissionAsync(string permission, CancellationToken ct = default)`
  — always evaluates against `AccessScope.ForPlatform(AccessScope.PlatformId)`,
  ignoring the ambient scope (mirrors `AccessGuard.RequirePlatformScopePermissionAsync`).
- Implement in `AccessPermissionChecker` via `IAccessService.HasPermissionAsync(userId, platformScope, permission)`
  using `_actor` for the user id (fail closed when unauthenticated).
- Identity user-management services/endpoints call `HasPlatformPermissionAsync`
  with `identity.users.read/create/update/delete`.

This avoids changing `DefaultAccessScopeContext` (which would risk a tenant request
falling back to platform scope) and keeps the platform check explicit.

---

## 4. Identity permission surface (what remains)

Only identity user-management permissions remain relevant to Identity. The
RBAC-management permissions (`access.roles.*`, `access.assignments.*`,
`access.audit.read`, `platform.members.manage`) already live in Access. Identity
declares its own permissions in the Access catalog:
- `identity.users.read`, `identity.users.create`, `identity.users.update`,
  `identity.users.delete`.

Declared via `AddAccessPermissions` (called from `AddIdentity` so any host using
`AddIdentity` + `AddAccess` gets them; contributions are lazy and harmless without
`AddAccess`). A small `IdentityPermissionNames` remnant (just these four) replaces
the old RBAC constants class.

---

## 5. Slice sequence

### Slice A — Additive, non-breaking (safe; do first)
1. `CoreKit.Architecture.Tests/ModuleBoundaryMatrix.cs`: add `CoreKit.Access` to
   Identity's `CompiledAssemblyAllowlist` and `ProjectReferenceAllowlist`.
2. `CoreKit.Identity/CoreKit.Identity.csproj`: add
   `<ProjectReference Include="..\CoreKit.Access\CoreKit.Access.csproj" />`.
3. `CoreKit.Access/Abstractions/IPermissionChecker.cs`: add
   `HasPlatformPermissionAsync`.
4. `CoreKit.Access/Services/AccessPermissionChecker.cs`: implement it.
5. `CoreKit.Access/Services/AccessMaintenance.cs` + `IAccessMaintenance`: add
   `AddPlatformMemberAsync` / `RemovePlatformMemberAsync`.
6. `CoreKit.Access` assignment query: `ListUserIdsWithRoleAsync` (repo + interface).
7. `CoreKit.Identity`: add `IdentityPermissionNames` remnant (4 names) and register
   `AddAccessPermissions` in `AddIdentity`.
Build + run Architecture.Tests. (No behavior change yet.)

### Slice B — Identity user-management authorization via Access
- `UserService`: replace `_currentUser.Permissions.Contains(...)` with
  `await _permissionChecker.HasPlatformPermissionAsync(IdentityPermissionNames.X)`.
- `UserService.CreateAsync`: after user creation, call
  `IAccessMaintenance.AddPlatformMemberAsync(user.Id)`; role grants move to Access
  assignments (see Slice C wiring).
- Endpoints: replace `.RequirePermission(...)` with a platform-permission filter
  (or keep the service-level check and drop the attribute). Remove
  `RequirePermissionAttribute`/`PermissionPolicyProvider`/`PermissionAuthorizationHandler`
  usage from user endpoints.

### Slice C — Role/assignment authority moves to Access (platform scope)
- `UserService.SetRolesAsync` / `CreateAsync` role assignment: write to Access
  `IAccessAssignmentService.SetRolesAsync` for the platform scope, OR a dedicated
  Identity-facing assignment method. (Requires the platform scope to be resolvable;
  see scope note below.)
- `AdministratorGuard` → platform-scope-aware per §2.
- `AccessResolver` / `IAccessResolver` → replaced by `IAccessService.GetAccessAsync(userId, platformScope)`
  for building the user DTO's permissions (or a thin Identity adapter).

### Slice D — JWT breaking change (STOP before executing)
- `TokenService.CreateAccessToken`: drop `role` + `permission` claims; remove from
  `ReservedClaims` handling as needed.
- `SessionIssuer`: stop passing roles/permissions; drop `_accessResolver`.
- `IAccessTokenClaimsContributor.GetClaimsAsync(user, ct)` signature change
  (drop roles/permissions params) — update Tenant contributor.
- `CurrentUserService`: remove `Roles`/`Permissions`.

### Slice E — Remove Identity RBAC (source only; no DB)
Delete: entities `Role/Permission/UserRole/RolePermission`; `User.UserRoles`;
configs; `IRoleRepository`/`IPermissionRepository` (+ impls); `RoleService`/
`PermissionService`; `AccessResolver`; `Authorization/` (incl. `IdentityPolicies`,
`RequirePermissionAttribute`, `PermissionRequirement`, `PermissionPolicyProvider`,
`PermissionAuthorizationHandler`, `EndpointPermissionExtensions`); `MapRoles`/
`MapPermissions` + endpoint files; `IdentitySeeder` RBAC (keep admin user
bootstrap via platform membership); `IdentityRoleNames`; `RoleInputValidator`;
role DTOs; DbContext DbSets; `GlobalUsings` RBAC entries; DI registrations.

### Slice F — Destructive EF migration (STOP — do NOT execute)
Generate the Identity migration dropping `Identity_Roles`, `Identity_Permissions`,
`Identity_UserRoles`, `Identity_RolePermissions`. Do not run `dotnet ef database
update` or touch any DB.

### Slice G — Tests + verification
- `CoreKit.Identity.Tests`: remove RBAC references; update `UserServiceTests`,
  `CredentialVersionTests`.
- `CoreKit.Access.Tests`: add platform-authority tests (membership provisioning,
  platform-permission check, `ListUserIdsWithRoleAsync`).
- `CoreKit.Architecture.Tests`: confirm updated matrix passes.
- Build all; run tests (no DB).

---

## 6. STOP points (require separate explicit approval)
- Executing the destructive EF migration / any DB change (S2 — generate only).
- Committing / pushing (never without explicit request).
- The one-time data migration of existing users to platform members (DB write).
- Any change to `CoreKit.SharedKernel` (forbidden).

## 7. Open design note (flagged, not silently guessed)
Slice C's "role assignment writes to Access for the platform scope" needs the
platform scope to be resolvable inside `UserService`. Because `UserService` is
platform-level, the cleanest approach is a dedicated Identity-facing Access method
that always targets `AccessScope.ForPlatform(AccessScope.PlatformId)` (e.g.
`IAccessAssignmentService.SetPlatformUserRolesAsync(userId, roleIds)`), mirroring
the platform-scoped guard pattern, rather than relying on the ambient scope.
This is the recommended resolution; confirm before Slice C implementation.