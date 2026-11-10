# Completion Report: AC-07-IMPL1 (AccessCache Rewrite with Token-Based Invalidation)

## Status: Completed (verified)

## Summary
AccessCache was redesigned with token-based cache invalidation using per-scope and per-user version counters with sliding-expiry. The cache now uses `AccessCacheToken(long ScopeVersion, long UserVersion)` record struct with `Begin`/`Set(token)` pattern and `ReadVersion`/`Bump` helper methods.

## Files Changed
- `CoreKit.Access/Services/AccessCache.cs` — Rewritten with token-based invalidation
- `CoreKit.Access/Services/AccessService.cs` — FreshAccessSource interface, GetFreshAccessAsync, token-aware GetAccessAsync
- `CoreKit.Access/Services/AccessGuard.cs` — RequiresFreshAuthority for RolesManage/AssignmentsManage
- `CoreKit.Access/Services/AccessPermissionChecker.cs` — IFreshAccessSource interface
- `CoreKit.Access/CoreKit.Access.csproj` — Removed SharedKernel reference
- `CoreKit.Access/GlobalUsings.cs` — Removed CoreKit.SharedKernel
- `CoreKit.Access/Services/DefaultPorts.cs` — Access-scoped abstractions (IAccessCurrentUser, IPermissionChecker, IAccessIntegrationEvent, IAccessEventHandler)
- `CoreKit.Access/Services/AccessEventHandlers.cs` — Empty (handlers moved to Tenant)
- `CoreKit.Access/Extensions/AccessServiceCollectionExtensions.cs` — Register Access-scoped DI

## Test Results
- Access.Tests: 189/189 passed (all tests, including AccessCacheStaleResultTests)

## Notes
- Access module no longer references SharedKernel (replaced with Access-owned abstractions)
- IAccessMembership and IAccessScopeContext defaults changed to return null (no scope/user) when no module provides them
- Tenant.Tests: 322/322 passed after fixing test expectations for EnsurePlatformScope() behavior (platform admins can do platform work even with tenant bound, per commit 815b5cd)
- Identity.Tests: 79/85 passed (6 pre-existing failures from PostgreSQL raw SQL on SQLite in-memory, unrelated to this task)