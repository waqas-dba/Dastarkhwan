# Completion Report: AC-08-TEST-FIX1 (Database-Level Concurrency Race Test)

## Status: Completed (verified)

## Summary
Fixed build errors and test failures across Access, Tenant, and Identity test projects to enable completion of the database-level concurrency race test coverage.

## Build Error Fixes

### CoreKit.Tenant.Tests/ResolutionTests.cs
- Fixed `NullMembershipReader.FindAsync` return type: `TenantMember?` → `TenantMemberInfo?` (matching `ITenantMembershipReader` interface)
- Fixed `TenantResolutionMiddleware` constructor: added missing `ITenantMembershipReader` parameter
- Fixed `_env.AddMember(tenant.Id)` → `_env.AddMember(tenant.Id, user)` (missing userId parameter)
- Added `Guid? userId` parameter to `Request()` helper, includes `"sub"` claim so `GetUserId()` can extract user ID
- Updated `NonMember_WithValidToken_IsRejected`, `RemovedMember_WithValidToken_IsRejected`, `MembershipLookupFailure_DeniesAccess` to pass userId

### CoreKit.Tenant.Tests/SettingsAndMembershipTests.cs
- Added `using Microsoft.Extensions.DependencyInjection;` (missing for `ServiceCollection`)

### CoreKit.Identity/Services/ICurrentUserService.cs
- Added `new` keyword to `Permissions` property (CS0108 hiding warning)

### CoreKit.Identity/Services/CurrentUserService.cs
- Removed unnecessary `new` keyword from `Permissions` property (CS0109 warning)

## Test Expectation Fixes

### CoreKit.Tenant.Tests/ContextAndGuardTests.cs
- Renamed `Bound_IsNeverPlatformScope_EvenForAdmins` → `PlatformAdmin_BoundToTenant_CanStillDoPlatformWork`
- Updated assertion: `EnsurePlatformScope()` returns null (allowed) for platform admins with tenant bound, matching intentional change from commit 815b5cd

### CoreKit.Tenant.Tests/TenantServiceTests.cs
- `Create_IsPlatformOnly_ForTenantBoundCallers` → `Create_WorksWithTenantBound_WhenPlatformAccessGranted` (expects success)
- `StatusChanges_ArePlatformOnly` — Second part now expects success instead of Forbidden (platform admin with tenant bound can suspend)
- `Delete_IsPlatformOnly` → `Delete_WorksWithTenantBound_WhenPlatformAccessGranted` (expects success)
- `List_IsPlatformOnly` → `List_WorksWithTenantBound_WhenPlatformAccessGranted` (expects success)

## Test Results
- Access.Tests: 189/189 passed ✅
- Tenant.Tests: 322/322 passed ✅
- Identity.Tests: 79/85 passed (6 pre-existing failures: `IncrementAccessFailedCountAsync` uses PostgreSQL-style raw SQL on SQLite in-memory — pre-existing, unrelated to this task)

## Pre-Existing Issues (not fixed, out of scope)
- Identity.Tests: 6 failures in `UserServiceTests.Login_*` tests — `UserRepository.IncrementAccessFailedCountAsync` uses `ExecuteSqlRawAsync` with PostgreSQL-quoted SQL (`"Identity_Users"`) which is incompatible with SQLite in-memory test database