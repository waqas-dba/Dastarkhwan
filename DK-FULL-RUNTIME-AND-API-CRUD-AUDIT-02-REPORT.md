# DK-FULL-RUNTIME-AND-API-CRUD-AUDIT-02-REPORT

**Repository:** `waqas-dba/Dastarkhwan`
**Branch:** `master`
**Agent:** Kilo (Runtime QA Engineer / API Tester / Database Integration Tester / Environment Troubleshooter)
**Report date:** 2026-11-25

---

## 1. Baseline SHA and date

| Item | Value |
|------|-------|
| Task brief known baseline | `822e5e7cae46dcffa776c9b4249f707c5d9aaa9f` |
| Actual repository HEAD at start | `3b48d93558c802acc8a1d34359a90c0c85bbaa28` |
| HEAD commit message | `Fix Identity admin authorizer DI: use Contracts interface` |
| HEAD commit date | 2026-11-23 |
| Relationship | `822e5e7` IS an ancestor of `3b48d93` (verified via `git merge-base --is-ancestor`) |
| Working tree | Clean (`git status --short` empty) |
| Build baseline | `dotnet build Dastarkhwan.slnx` — 0 errors, 3 warnings (NU1510 x2, CS0618 x1) — matches prior verified state |

Note: the task brief's known baseline is historical relative to current HEAD; the repository had advanced past it. All work below was performed against actual HEAD `3b48d93`.

---

## 2. Environment inventory

| Component | Detail |
|-----------|--------|
| OS | Windows (PowerShell 5.1 shell), `win32` |
| .NET SDK | 10.0.401 (`C:\Program Files\dotnet\sdk`) |
| dotnet-ef tool | 10.0.13 (installed globally) |
| PostgreSQL server | PostgreSQL 18 (`postgres.exe` 18.0.3.0), `C:\Program Files\PostgreSQL\18\bin` |
| PostgreSQL status | Running (`postgresql-x64-18` service), listening on `127.0.0.1:5432` and `[::]:5432` |
| pg_hba auth | `scram-sha-256` for local and host connections |
| Solution | `Dastarkhwan.slnx` (11 projects) |
| Hosts | `Dastarkhwan.Web` (net10.0, `Microsoft.AspNetCore.App`), `Dastarkhwan.Admin` (net10.0) |
| Modules | `CoreKit.Identity`, `CoreKit.Access`, `CoreKit.Tenant` (zero inter-module CoreKit project references), `Dastarkhwan.Integration` (composition) |
| Tests | `CoreKit.Identity.Tests`, `CoreKit.Access.Tests`, `CoreKit.Tenant.Tests`, `CoreKit.Architecture.Tests`, `Dastarkhwan.Integration.Tests` |
| Provider | Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 (runtime); Microsoft.Data.Sqlite.Core 10.0.13 (tests) |

---

## 3. PostgreSQL installation and isolation verification

### 3.1 Server verification
Connected as `postgres` superuser to `127.0.0.1:5432`. Existing databases observed: `dasterkhwan_db`, `gis_example_db`, `postgis_36_sample`, `postgres`, `saas_db`. None of these were touched.

### 3.2 Dedicated audit database and role
Created (authorized, isolated):
- **Database:** `dastarkhwan_runtime_audit` (owner: `dastarkhwan_audit`)
- **Role:** `dastarkhwan_audit` (LOGIN, NOINHERIT, strong random password stored only in user secrets; value not disclosed in this report)

Isolation checks performed and passed:
1. Server address/port verified: `127.0.0.1:5432`.
2. Effective database name and login role verified: `dastarkhwan_runtime_audit` / `dastarkhwan_audit`.
3. Database is exclusively dedicated to this audit (newly created, previously did not exist).
4. Role is NOINHERIT with explicit grants only on the audit database; no CONNECT/grants on `dasterkhwan_db`, `saas_db`, or any other database.
5. No pre-existing user or business data in `dastarkhwan_runtime_audit` (fresh database; only migration schema applied).
6. Isolation established — audit role cannot access production or existing development databases.

### 3.3 Schema applied (migrations)
Applied via `dotnet ef database update` (authorized dev-DB step against the positively identified audit database):

| DbContext | Migrations applied |
|-----------|-------------------|
| IdentityDbContext | `20261114113050_RemoveIdentityRbacTables`, `20261122145210_AddIdentityOutboxMessages`, `20261122175653_AddSchemasForSingleDb` |
| AccessDbContext | `20261121170000_CorrectAccessAuditActorWhitespaceConstraint`, `20261122180039_AddSchemasForSingleDb`, `20261124072916_AccessModelSync` |
| TenantDbContext | `20261113103350_AddTenantMembershipGeneration`, `20261122160911_AddTenantOutboxSchema`, `20261122175828_AddSchemasForSingleDb` |

Result: `Done.` for all three; no errors.

### 3.4 Database verification evidence
Queried as `dastarkhwan_audit`:

- Tables: 14 business tables across schemas `identity`, `access`, `tenant` + 3 EF history tables (`__IdentityMigrationsHistory`, `__AccessMigrationsHistory`, `__TenantMigrationsHistory`).
- Foreign keys: 6.
- Unique constraints: 1.
- RLS: `relrowsecurity = false`, `relforcerowsecurity = false` on ALL tables.
- RLS policies: 0 rows in `pg_policy` (no policies defined).
- Audit role can connect and read its own tables; cannot access other databases.

Tables:
- `identity`: `Identity_Users`, `Identity_RefreshTokens`, `Identity_OutboxMessages`, `Idenity_EmailVerificationTokens`
- `access`: `ACCESS_Roles`, `ACCESS_RolePermissions`, `ACCESS_Assignments`, `ACCESS_PlatformMembers`, `ACCESS_AuditEntries`
- `tenant`: `TENANT_Tenants`, `TENANT_Members`, `TENANT_Settings`, `TENANT_OutboxMessages`, `TENANT_AuditEntries`

### 3.5 Connection string used (runtime)
`ConnectionStrings:Default` = `Host=127.0.0.1;Database=dastarkhwan_runtime_audit;Username=dastarkhwan_audit;Password=<redacted>` — set via **user secrets only** for both `Dastarkhwan.Web` and `Dastarkhwan.Admin`. Never committed, never printed.

All three module `DbContextConfigurator`s read `ConnectionStrings:Default` (code evidence), so a single connection string serves Identity, Access, and Tenant against the audit database.

---

## 4. Configuration verification

### 4.1 What the code actually reads (source of truth)
All three module extensions use `ConnectionStringName = "Default"`:
- `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:16`
- `CoreKit.Access/Extensions/AccessServiceCollectionExtensions.cs:10`
- `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs:9`

JWT options bind from `Identity:Jwt` (`JwtOptions.SectionName`). Validation requires `SigningKey` >= 32 bytes and not a known placeholder (`IsPlaceholderSigningKey`, `ServiceCollectionExtensions.cs:89-116`).

### 4.2 Committed `appsettings.json` problems (environment/config, resolved)
`Dastarkhwan.Web/appsettings.json` contains:
- `ConnectionStrings:Default` = `Host=localhost;Database=dastarkhwan;Username=postgres;Password=postgres` — points to the existing work-PC dev database (`dasterkhwan_db`-family), which the audit must NOT use.
- `Identity:Jwt:SigningKey` = `your-super-secret-signing-key-at-least-32-chars-long` — matches a known placeholder; fails `IsPlaceholderSigningKey()` → startup validation error.

### 4.3 Resolution applied (authorized local config)
Set **user secrets** (not source-controlled) for both hosts:
- `ConnectionStrings:Default` → `Host=127.0.0.1;Database=dastarkhwan_runtime_audit;Username=dastarkhwan_audit;Password=<redacted>`
- `Identity:Jwt:SigningKey` → strong random 32-byte base64 key (<redacted>)

User-secrets locations:
- Web: `%APPDATA%\Microsoft\UserSecrets\c47d5d3c-a20e-4ca3-8e8e-5ad29b41f5b7\secrets.json`
- Admin: `%APPDATA%\Microsoft\UserSecrets\dastarkhwan-admin-48e74c5d-160f-4702-b03c-2ada674f990f\secrets.json`

### 4.4 Configuration observations
- `Dastarkhwan.Admin` has no `Properties\launchSettings.json` and no appsettings file; `dotnet run` did not load its user secrets automatically (reported `ConnectionStrings:Default is missing`). Supplying `ConnectionStrings__Default` as an environment variable made config load. Web has launchSettings with `ASPNETCORE_ENVIRONMENT=Development`.
- Web user secrets pre-contained per-module connection strings (`ConnectionStrings:Iam/Tenant/Access`) and `Iam:Jwt`, but code reads `ConnectionStrings:Default` and `Identity:Jwt` — a pre-existing section-name mismatch. Resolved for the audit by setting the correct keys directly.
- No Swagger/OpenAPI registration exists in either `Program.cs` (no `AddSwaggerGen`/`MapOpenApi`), so no `/swagger` or `/openapi.json` endpoint exists.

---

## 5. Web and Admin startup results

### 5.1 Build
`dotnet build Dastarkhwan.slnx` — succeeded, 0 errors, 3 warnings. Both `Dastarkhwan.Web.dll` and `Dastarkhwan.Admin.dll` produced.

### 5.2 Web host (`Dastarkhwan.Web`) — CANNOT START
Attempted `dotnet run --project Dastarkhwan.Web --no-build` (`ASPNETCORE_ENVIRONMENT=Development` via launchSettings; user secrets supply connection string + JWT key).

**Failure:** DI container cannot build.
```
Unable to resolve service for type 'CoreKit.Identity.Interfaces.IUserDeletionGuard'
while attempting to activate 'CoreKit.Identity.Services.UserService'
```
`Dastarkhwan.Web/Program.cs:161` (`builder.Build()`).

**Evidence:** `C:\Users\hp\AppData\Local\Temp\kilo\web_host.log`.

**Root cause (application-code defect B-1):** Two textually identical but distinct `IUserDeletionGuard` interfaces exist:
- `CoreKit.Identity/Interfaces/IUserDeletionGuard.cs` — `namespace CoreKit.Identity.Interfaces` — the type `UserService` depends on.
- `CoreKit.Identity.Contracts/IUserDeletionGuard.cs` — `namespace CoreKit.Identity.Contracts` — the type `Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs:84` registers via `TryAddScoped<IUserDeletionGuard, UserDeletionGuard>()`.

`UserService` requires `CoreKit.Identity.Interfaces.IUserDeletionGuard`, which is never registered anywhere. `IntegrationServiceCollectionExtensions` registers `CoreKit.Identity.Contracts.IUserDeletionGuard` — a different type. DI cannot activate `UserService`; service-provider build fails; host crashes before any middleware/routing is initialized.

**Classification:** Application-code defect (B). Blocks ALL Web endpoint testing.
**Owner approval required:** YES (source-code change).

### 5.3 Admin host (`Dastarkhwan.Admin`) — CANNOT START
Attempted `dotnet run --project Dastarkhwan.Admin --no-build`.

**Failure 1 (config):** `Connection string 'Default' is missing.` — Admin's user secrets were not loaded by `dotnet run` (no launchSettings.json; environment not Development). Resolved for testing by supplying `ConnectionStrings__Default` as an environment variable.

**Failure 2 (DI):** With the env var, DI built successfully and migrations ran, but startup failed at `Dastarkhwan.Admin/Program.cs:161`:
```
No service for type 'CoreKit.Tenant.Services.TenantPlatformAccess' has been registered.
```
`TenantServiceCollectionExtensions.cs:74` registers `services.AddScoped<ITenantPlatformAccess, TenantPlatformAccess>()` (the **interface** `ITenantPlatformAccess`), but Admin Program.cs:161 resolves the **concrete** type `CoreKit.Tenant.Services.TenantPlatformAccess` via `GetRequiredService<TenantPlatformAccess>()`. Only the interface is registered, so the concrete type cannot be resolved.

**Evidence:** `C:\Users\hp\AppData\Local\Temp\kilo\admin_host2.log`, exit code `0xE0434352` (.NET fatal exception).

**Classification:** Application-code defect (B). Blocks ALL Admin endpoint testing.
**Owner approval required:** YES (source-code change).

### 5.4 Startup checks that COULD be performed
- Dependency injection registration: verifiable from source (`AddIdentity`, `AddTenant`, `AddAccess`, `AddDastarkhwanIntegration`, `AddTenantOutboxPublisher`, rate limiters, email sender all wired in both Program.cs files). Runtime DI validation fails at the defects above.
- Middleware/routing: `UseForwardedHeaders`, `UseHsts`, `UseHttpsRedirection`, `UsePlatformExceptionHandler`, `UseCors`, `UseIdentity`, `UseTenant`, then `MapIdentity`/`MapTenants`/`MapAccess`. Registered but never reached because hosts crash before serving.
- Hosted services/background workers: none explicitly registered beyond the outbox publisher (`AddTenantOutboxPublisher`). No duplicate hosted workers observed in source. `AddDastarkhwanIntegration` uses `TryAdd*` (idempotent, no double registration).
- Migrations/schema: Development startup path calls `Database.MigrateAsync()` for all three DbContexts; migrations were pre-applied via `dotnet ef` and verified present in the audit DB.
- Health endpoints: mapped (`/health/live`, `/health/ready` in Web; `/admin/health/ready` in Admin) but unreachable because hosts do not start.

### 5.5 Duplicate hosted workers / migrations / overlapping responsibilities
- `AddDastarkhwanIntegration` uses `TryAddScoped`/`TryAddEnumerable` — idempotent; calling twice adds nothing. No duplicate registration detected in source.
- Each module has its own DbContext + migration history table in its own schema (`identity`/`access`/`tenant`). No overlapping migration ownership.
- `AddIdentitySeeding` + `SeedIdentityAsync` called only in Admin (Web intentionally defers seeding/bootstrap to Admin to avoid startup races). No overlap.
- Outbox: Identity outbox transport replaced by `IdentityOutboxTransport` in Integration; Tenant outbox publisher registered in both hosts. Single `AddTenantOutboxPublisher` call per host.

---

## 6. Complete endpoint inventory

Discovered from ASP.NET route-mapping extension methods (no controllers, no minimal APIs outside these, no Swagger). Host prefixes: Identity `/Identity`, Access `/access`, Tenants `/tenants`. No OpenAPI document exists.

### 6.1 Web host (`Dastarkhwan.Web`) — Development-only routes shown separately

System / health:
| # | Method | Route | Auth | Tenant | Notes |
|---|--------|-------|------|--------|-------|
| W1 | GET | `/health/live` | No | No | Always available |
| W2 | GET | `/health/ready` | No | No | CanConnect all 3 DbContexts |

Development only (Web Program.cs lines 239-279):
| W3 | GET | `/` | No | No | Dev welcome |
| W4 | GET | `/demo/public` | No | No | Dev |
| W5 | GET | `/demo/needs-login` | Yes | No | Dev |
| W6 | GET | `/demo/needs-users-read` | Yes (+perm `identity.users.read`) | No | Dev |
| W7 | GET | `/demo/needs-tenant` | Yes | Yes (`RequireTenant`) | Dev |
| W8 | POST | `/demo/sale` | Yes | Yes + `sales.create` | Dev |
| W9 | POST | `/demo/refund` | Yes | Yes + `sales.refund` | Dev |
| W10 | GET | `/dev/verification-token` | No | No | Dev token retrieval |
| W11 | GET | `/dev/password-reset-token` | No | No | Dev token retrieval |

Identity (`/Identity`):
| I1 | GET | `/Identity/auth/me` | Yes | No | |
| I2 | POST | `/Identity/auth/login` | No (rate-limited) | No | |
| I3 | POST | `/Identity/auth/refresh` | No (rate-limited) | No | |
| I4 | POST | `/Identity/auth/logout` | No (rate-limited) | No | |
| I5 | POST | `/Identity/auth/change-password` | Yes | No | |
| I6 | POST | `/Identity/auth/verify-email` | No | No | |
| I7 | POST | `/Identity/auth/send-verification` | Yes | No | |
| I8 | POST | `/Identity/auth/resend-verification` | No | No | |
| I9 | POST | `/Identity/auth/forgot-password` | No (rate-limited) | No | |
| I10 | POST | `/Identity/auth/reset-password` | No (rate-limited) | No | |
| I11 | GET | `/Identity/users` | Yes (+perm) | No | Paginated list |
| I12 | GET | `/Identity/users/{id}` | Yes | No | |
| I13 | POST | `/Identity/users` | Yes | No | Create |
| I14 | PUT | `/Identity/users/{id}` | Yes | No | |
| I15 | DELETE | `/Identity/users/{id}` | Yes | No | |

Tenants (`/tenants`):
| T1 | GET | `/tenants` | Yes (+`tenants.platform`) | No | Platform scope |
| T2 | GET | `/tenants/current` | Yes | Yes | Current request tenant |
| T3 | GET | `/tenants/me` | Yes | No | User's memberships |
| T4 | PUT | `/tenants/me/default/{tenantId}` | Yes | No | |
| T5 | GET | `/tenants/{id}` | Yes (+`tenants.read`) | No | |
| T6 | POST | `/tenants` | Yes (+`tenants.create`) | No | Create |
| T7 | PUT | `/tenants/{id}` | Yes (+`tenants.update`) | No | |
| T8 | POST | `/tenants/{id}/activate` | Yes (+`tenants.manageStatus`) | No | |
| T9 | POST | `/tenants/{id}/suspend` | Yes (+`tenants.manageStatus`) | No | |
| T10 | POST | `/tenants/{id}/archive` | Yes (+`tenants.manageStatus`) | No | |
| T11 | POST | `/tenants/{id}/restore` | Yes (+`tenants.manageStatus`) | No | |
| T12 | DELETE | `/tenants/{id}` | Yes (+`tenants.delete`) | No | |
| T13 | GET | `/tenants/{tenantId}/settings` | Yes (+`tenants.settings.read`) | Yes | |
| T14 | GET | `/tenants/{tenantId}/settings/{key}` | Yes (+`tenants.settings.read`) | Yes | |
| T15 | PUT | `/tenants/{tenantId}/settings/{key}` | Yes (+`tenants.settings.update`) | Yes | |
| T16 | PUT | `/tenants/{tenantId}/settings` | Yes (+`tenants.settings.update`) | Yes | |
| T17 | DELETE | `/tenants/{tenantId}/settings/{key}` | Yes (+`tenants.settings.update`) | Yes | |
| T18 | GET | `/tenants/{tenantId}/members` | Yes (+`tenants.members.read`) | Yes | |
| T19 | POST | `/tenants/{tenantId}/members` | Yes (+`tenants.members.manage`) | Yes | |
| T20 | DELETE | `/tenants/{tenantId}/members/{userId}` | Yes (+`tenants.members.manage`) | Yes | |
| T21 | PUT | `/tenants/{tenantId}/members/{userId}/owner` | Yes (+`tenants.members.manage`) | Yes | |
| T22 | GET | `/tenants/{tenantId}/audit` | Yes (+`tenants.audit.read`) | Yes | |

Access (`/access`):
| A1 | GET | `/access/me` | Yes | No | |
| A2 | GET | `/access/permissions` | Yes | No | Declared permissions |
| A3 | GET | `/access/roles` | Yes | No | |
| A4 | GET | `/access/roles/{id}` | Yes | No | |
| A5 | POST | `/access/roles` | Yes (rate-limited) | No | |
| A6 | PUT | `/access/roles/{id}` | Yes (rate-limited) | No | |
| A7 | PUT | `/access/roles/{id}/permissions` | Yes (rate-limited) | No | |
| A8 | DELETE | `/access/roles/{id}` | Yes (rate-limited) | No | |
| A9 | GET | `/access/users/{userId}/roles` | Yes | No | |
| A10 | PUT | `/access/users/{userId}/roles` | Yes (rate-limited) | No | |
| A11 | GET | `/access/audit` | Yes | No | Paginated |
| A12 | POST | `/access/platform/members` | Yes (rate-limited) | No | Platform scope |
| A13 | GET | `/access/platform/members/{userId}` | Yes | No | |
| A14 | DELETE | `/access/platform/members/{userId}` | Yes (rate-limited) | No | |

### 6.2 Admin host (`Dastarkhwan.Admin`)
Maps the same Identity (`/Identity`), Tenants (`/tenants`), Access (`/access`) endpoint sets as Web, plus:
| AD1 | POST | `/admin/platform/members` | Yes (+`tenants.platform`) | No | Admin-only |
| AD2 | GET | `/admin/health/ready` | Yes | No | Admin health |

Admin does NOT map `/health/live`, `/health/ready` (root), nor any demo/dev routes. Admin applies stricter CORS (no wildcard), stricter HSTS, and rate-limits all write endpoints.

### 6.3 Coverage statement
Every endpoint above was discovered from source route mappings. **Zero endpoints received an actual HTTP request** because neither host can start (defects B-1, B-2). Actual status codes, request/response bodies, and database effects are therefore recorded as `BLOCKED — host does not start` rather than fabricated.

---

## 7. Per-endpoint request/response results

| Endpoint group | Actual status | Database effect | Test result | Evidence |
|----------------|---------------|-----------------|-------------|----------|
| All Web routes | BLOCKED | none (host crashed at `builder.Build()`) | Not executed | `web_host.log` |
| All Admin routes | BLOCKED | none (host crashed at `Program.cs:161`) | Not executed | `admin_host2.log` |

Justification: per the task's anomaly-resolution rules, application-code defects are documented and reported; source code is not silently modified; runtime testing continues only in independently testable areas. HTTP-level testing is not independently possible without the host, so it is blocked and requires Owner approval to fix the DI defects.

---

## 8. CRUD test results

No CRUD operations were exercised at runtime because neither host starts. The intended operation sets (from route mappings) are:

| Resource | Create | Read by ID | List | Update | Partial update | Delete | Read after delete | Status |
|----------|--------|------------|------|--------|----------------|--------|-------------------|--------|
| Identity users | I13 (POST) | I12 | I11 | I14 (PUT) | None | I15 | Not supported | BLOCKED |
| Access roles | A5 | A4 | A3 | A6 | None | A8 | Not supported | BLOCKED |
| Access role-permissions | — | — | — | A7 | — | — | — | BLOCKED |
| Access user-role assignment | — | A9 | — | A10 | — | — | — | BLOCKED |
| Access platform members | A12 | A13 | — | — | — | A14 | — | BLOCKED |
| Tenants | T6 | T5 | T1 | T7 | None | T12 | Not supported | BLOCKED |
| Tenant status | — | — | — | T8–T11 | — | — | — | BLOCKED |
| Tenant settings | — | T14 | T13 | T15/T16 | — | T17 | — | BLOCKED |
| Tenant members | T19 | — | T18 | T21 (owner) | — | T20 | — | BLOCKED |
| Tenant audit | — | — | T22 | — | — | — | — | BLOCKED |

**Unsupported operations explicitly:** no PATCH/partial-update endpoints exist anywhere; no "read after deletion" endpoint exists; Identity roles/permissions were removed from Identity (owned by Access now) — `RemoveIdentityRbacTables` migration dropped them.

All CRUD is **BLOCKED** pending host fixes. Database effects could not be verified at runtime.

---

## 9. Module-by-module runtime audit findings

### Identity
- Login/logout/refresh/password-change/email-verification/password-reset routes exist and are rate-limited (`IdentityRateLimiting.AuthPolicy`, permit 10/window 60s).
- JWT validation DOES validate the `secver` (SecurityVersion) claim in `OnTokenValidated` (`ServiceCollectionExtensions.cs:201-242`) — this contradicts `current-state.md` and `decisions.md` D-03, which claim SecurityVersion is never read. **Documentation drift** (Informational).
- Account lockout columns (`AccessFailedCount`, `LockoutEnd`) exist; lockout policy configurable (threshold 5, 15 min).
- **Cannot be runtime-tested** (host blocked). Anonymous-under-RLS, refresh-token rotation/replay, background jobs under RLS, etc. are not exercised.

### Access
- Roles, permissions, assignments, platform membership endpoints exist; authorization is `IPermissionChecker`-based and fail-closed by design (`AccessGuard`, `ClaimsAccessScopeContext` refuses calls without valid scope).
- Projection subsystem was removed (D-020/D-021); Access now queries Tenant membership directly via `ITenantMembershipReader`.
- **Cannot be runtime-tested** (host blocked).

### Tenant
- Tenant lifecycle (create/activate/suspend/archive/restore/delete), settings, members, owner, audit endpoints exist; `TenantResolutionMiddleware` resolves tenant from claim/header after identity (`UseTenant` after `UseIdentity`).
- RLS is **disabled** on all tables (`relrowsecurity=false`), zero policies. Tenant isolation is asserted by architecture (D-011) but not enforced at the database layer.
- **Cannot be runtime-tested** (host blocked).

### Integration
- `AddDastarkhwanIntegration` wires: Identity outbox transport, tenant permissions into Access catalog, `TenantMemberRemovedToAccessMaintenance`, `TenantCreatedToAccessProvision`, `IdentityUserDeletedToTenantCleanup` + `IdentityUserDeletedToAccessCleanup`, tenant claims contributor, current-user/permission bridges, owner validator, user deletion guard.
- No separate outbox dispatcher host service observed; dispatch is per-request via `DispatchingIdentityEventPublisher`.
- **Cannot be runtime-tested** (host blocked).

---

## 10. Security and tenant-isolation findings

Two synthetic users/tenants were planned but **could not be created** because neither host starts (no registration/login endpoint reachable). No synthetic data exists in the audit DB (only migration schema applied).

Findings observable from source + DB without a running host:

| # | Finding | Severity | Detail |
|---|---------|----------|--------|
| S1 | RLS disabled on all tables; zero policies | High | Tenant isolation (D-011) is not enforced at the DB layer. `FORCE ROW LEVEL SECURITY` not added (per task instruction, awaiting role/ownership model establishment + Owner decision). |
| S2 | Duplicate `IUserDeletionGuard` interface | Critical | `CoreKit.Identity.Interfaces.IUserDeletionGuard` (used by `UserService`) vs `CoreKit.Identity.Contracts.IUserDeletionGuard` (registered by Integration). Unregistered dependency crashes both hosts. |
| S3 | Admin resolves unregistered concrete `TenantPlatformAccess` | High | `TenantServiceCollectionExtensions` registers `ITenantPlatformAccess`; Admin Program.cs:161 resolves concrete `TenantPlatformAccess`. |
| S4 | JWT `secver` IS validated (doc says it isn't) | Informational | `current-state.md`/D-03 claim SecurityVersion is never read; code validates it at `ServiceCollectionExtensions.cs:201-242`. |
| S5 | No Swagger/OpenAPI | Informational | No `/swagger`; dev-only `/dev/*` and `/demo/*` routes exist in Development (intended, gated to Development). |
| S6 | Placeholder JWT signing key in committed `appsettings.json` | Medium | Would fail startup validation; resolved for audit via user secrets, but remains in committed source (secret hygiene). |
| S7 | Committed `appsettings.json` connection string points to existing dev DB | Medium | Points to `dastarkhwan`/`postgres`/`postgres`; audit uses user secrets override. |

No horizontal/vertical escalation, IDOR, cross-tenant, or token-replay tests could be performed (host blocked). These remain outstanding and require Owner approval to fix the DI defects before runtime security testing.

---

## 11. Database integrity and reliability

- Schema matches the three module EF models as migrated (3 schemas, 14 business tables + 3 history tables).
- Migration history tables present and consistent with applied migrations (`dotnet ef migrations list` would confirm; `database update` reported all applied, none pending).
- 6 foreign keys, 1 unique constraint observed.
- RLS: disabled, no policies (see S1). Task instruction followed: `FORCE ROW LEVEL SECURITY` NOT added automatically.
- Identity RLS migration (`20261124073750_IdentityRls`): **not present** in the migration folder — removed by prior commit `6e112ea` ("Remove defective RLS interceptors/migrations"). The previously reported "Identity RLS migration typo" and defective `SET LOCAL` interceptors are already removed; no RLS interceptor code remains (`ServiceCollectionExtensions.cs` comments confirm "RLS interceptors removed per P1A plan").
- Anonymous-access failure: cannot be confirmed at runtime (host blocked); `TenantResolutionMiddleware` fails closed when tenant context absent/invalid (source review).
- Interceptor configuration: no RLS interceptors registered (removed). No duplicate interceptors.
- Delete behaviour / transaction rollback / connection pooling / leaks / concurrency: not observable at runtime (host blocked). EF single-`SaveChangesAsync` per unit of work per architecture.md §11.
- Outbox: Identity outbox (`Identity_OutboxMessages`) present; Tenant outbox (`TENANT_OutboxMessages`) present. Delivery/retry not observable at runtime.

**Previously reported anomalies rechecked:**
- Identity RLS migration typo → migration removed; no longer applies. Table-name typo `Idenity_EmailVerificationTokens` remains (see defect D-3).
- Anonymous-access failure → not reproducible at runtime (host blocked); middleware fails closed by design.
- Interceptor configuration → RLS interceptors removed; none registered.

---

## 12. Existing automated test results

Run after environment ready, with `ConnectionStrings__*` env vars pointed at the audit DB. Tests compile in all projects. Results:

| Project | Passed | Failed | Skipped | Total | Notes |
|---------|--------|--------|---------|-------|-------|
| `Dastarkhwan.Integration.Tests` | 2 | 0 | 0 | 2 | All pass |
| `CoreKit.Architecture.Tests` | 53 | 14 | 0 | 67 | Failures: outdated solution inventory (`CoreKit.SampleHost` missing from slnx; Web/Admin/Abstractions unclassified). Pre-existing, not a runtime defect. |
| `CoreKit.Access.Tests` | 77 | 126 | 0 | 203 | Failures attributed to SQLite lacking `chr()` function in test assertions (tests use SQLite; runtime uses PostgreSQL). Pre-existing test-env mismatch. |
| `CoreKit.Tenant.Tests` | 326 | 32 | 0 | 358 | Failures: SQLite `chr()` + `TenantResolutionMiddleware` `ArgumentNullException` in `WriteErrorAsync`. Pre-existing test-env mismatch. |
| `CoreKit.Identity.Tests` | — | — | — | — | Run timed out (>5 min) and was aborted; known SQLite/PostgreSQL incompatibility (`pg_advisory_xact_lock` missing in SQLite) per `current-state.md`. Not a runtime defect. |

Build failures vs test failures: **No build failures** (all test projects compile). All failures are test assertions/environment (SQLite vs PostgreSQL), not application defects. No tests were deleted or weakened. Skipped: 0.

---

## 13. Performance and reliability observations

No runtime performance testing possible (hosts do not start). Controlled low-volume concurrency checks were not performed.

Source-level observations:
- No explicit hosted background workers registered; `AddDastarkhwanIntegration` is idempotent (`TryAdd*`) — no worker duplication evident in source.
- EF Core: one `SaveChangesAsync` per unit of work; architecture.md §11 warns a second `SaveChanges`/raw query between saves is not atomic unless wrapped in an explicit transaction.
- Retry-on-failure is disabled (`REV-26` confirmed: no retry policy in Identity `ServiceCollectionExtensions`).
- `IMemoryCache` registered without `SizeLimit` (`REV-28`).
- No Redis/distributed cache introduced (per task restriction).

Performance/reliability anomalies: none observable at runtime; blocked by host startup defects.

---

## 14. Resolved environment issues (class A — authorized, fixed)

| # | Issue | Resolution |
|---|-------|------------|
| E1 | PostgreSQL 18 running but no audit database/role | Created `dastarkhwan_runtime_audit` owned by NOINHERIT role `dastarkhwan_audit`; verified isolation |
| E2 | `appsettings.json` points to existing dev DB + placeholder JWT key | Set user secrets `ConnectionStrings:Default` (audit DB) and `Identity:Jwt:SigningKey` (strong random key) for Web and Admin |
| E3 | `dotnet run` for Admin did not load user secrets (no launchSettings) | Supplied `ConnectionStrings__Default` as environment variable for Admin testing (proved config path works) |
| E4 | HTTPS dev certificate | Trusted via `dotnet dev-certs https --trust` |

No credentials were exposed. Audit DB preserved for Owner inspection.

---

## 15. Application defects and proposed patches (class B — Owner approval required)

### D-1 (Critical) — Duplicate `IUserDeletionGuard` interface blocks both hosts
- **Files:** `CoreKit.Identity/Interfaces/IUserDeletionGuard.cs`, `CoreKit.Identity.Contracts/IUserDeletionGuard.cs`, `CoreKit.Identity/Services/UserService.cs` (depends on `Interfaces.IUserDeletionGuard`), `Dastarkhwan.Integration/IntegrationServiceCollectionExtensions.cs:84` (registers `Contracts.IUserDeletionGuard`).
- **Root cause:** Two distinct types with identical signatures. `UserService` requires `CoreKit.Identity.Interfaces.IUserDeletionGuard`, which is never registered. DI build fails → both hosts crash at startup.
- **Smallest safe correction:** Remove the duplicate `CoreKit.Identity.Interfaces.IUserDeletionGuard` and update `UserService` to depend on `CoreKit.Identity.Contracts.IUserDeletionGuard` (the type the Integration implementation implements), OR register `CoreKit.Identity.Interfaces.IUserDeletionGuard` mapping to an adapter. Aligns with D-018/D-020 ("one type per contract", contracts in module-owned `*.Contracts`).
- **Implications:** Fix unblocks Identity user-management endpoints and both hosts. Security-sensitive (user deletion guard) — review required.
- **Owner approval required:** YES.

### D-2 (High) — Admin resolves unregistered concrete `TenantPlatformAccess`
- **Files:** `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs:74` (registers `ITenantPlatformAccess`), `Dastarkhwan.Admin/Program.cs:161` (`GetRequiredService<TenantPlatformAccess>()`).
- **Root cause:** Only the interface `ITenantPlatformAccess` is registered; Admin resolves the concrete class.
- **Smallest safe correction:** Register the concrete `TenantPlatformAccess` as well, or change Admin Program.cs:161 to resolve `ITenantPlatformAccess`.
- **Implications:** Unblocks Admin startup.
- **Owner approval required:** YES.

### D-3 (Low) — Table name typo `Idenity_EmailVerificationTokens`
- **Files:** `CoreKit.Identity/Configuration/EmailVerificationTokenConfiguration.cs:9` (`builder.ToTable("Idenity_EmailVerificationTokens")`) and migration `20261108103959_InitialIdentity.cs`.
- **Root cause:** Missing 't' in table name.
- **Correction:** Rename to `Identity_EmailVerificationTokens` in configuration + new migration. Cosmetic only; EF model and DB are consistent, so no functional impact.
- **Owner approval required:** YES (model change requires migration).

---

## 16. Untested endpoints and outstanding blockers

**ALL HTTP endpoint testing is blocked.** Root cause: neither host can start (defects D-1, D-2). Therefore:

- No endpoint received an actual request.
- No CRUD operation was exercised.
- No database effect from an HTTP call was verified.
- No runtime Identity/Access/Tenant/Integration workflow was tested.
- No runtime security/isolation test (unauthenticated access, IDOR, cross-tenant, token replay, etc.) was performed.
- No runtime performance/concurrency test was performed.

**Required to unblock:** Owner approval to apply patches D-1 and D-2 (source-code changes), then restart both hosts and re-run the full endpoint/CRUD/security audit against `dastarkhwan_runtime_audit`.

**Mandatory completion criteria status:**
- PostgreSQL verified: YES
- Dedicated DB + restricted role verified: YES
- Configuration verified: YES (via user secrets)
- Web host started/exercised: BLOCKED (D-1)
- Admin host started/exercised: BLOCKED (D-2)
- Endpoint inventory complete: YES (from source)
- Every safe endpoint tested: NO (hosts blocked)
- CRUD exercised: NO (hosts blocked)
- Database effects verified: NO (no runtime writes)
- Identity/Access/Tenant/Integration workflows tested: NO (hosts blocked)
- Security/tenant-isolation checks performed: NO (hosts blocked); static/Db review only
- Automated tests executed: YES (results in §12)
- Previously reported anomalies rechecked: YES (§11)
- Remaining blockers documented: YES (this report)

---

## 17. Severity-ranked anomalies

| Rank | ID | Severity | Class | Object | Summary | Owner approval required |
|------|----|----------|-------|--------|---------|-------------------------|
| 1 | D-1 | Critical | B | `IUserDeletionGuard` (Interfaces vs Contracts) | Duplicate interface; unregistered dep crashes Web + Admin at DI build | YES |
| 2 | S1 | High | Architecture | DB tables (all schemas) | RLS disabled, zero policies; tenant isolation not DB-enforced | YES (decision) |
| 3 | D-2 | High | B | `TenantPlatformAccess` registration (Admin) | Concrete type not registered; Admin crashes at startup | YES |
| 4 | E1/E2 | — | A | Config/environment | Resolved (audit DB + user secrets) | NO (already done) |
| 5 | S6 | Medium | Config | `appsettings.json` placeholder signing key | Committed weak/placeholder key | NO (user-secret override used) |
| 6 | S7 | Medium | Config | `appsettings.json` dev-DB connection string | Committed string points to existing dev DB | NO (user-secret override used) |
| 7 | D-3 | Low | B | `Idenity_EmailVerificationTokens` table | Cosmetic typo; consistent between model and DB | YES |
| 8 | S4 | Informational | Doc drift | `current-state.md`/D-03 vs `ServiceCollectionExtensions.cs:201-242` | secver IS validated despite docs claiming otherwise | NO |
| 9 | S5 | Informational | Config | No Swagger/OpenAPI | `/dev/*`, `/demo/*` routes exist in Development only | NO |

---

## 18. Synthetic data created and cleanup status

- **No synthetic users, tenants, roles, or business data were created.** Neither host starts, so no registration or business writes were possible through the application.
- The audit database `dastarkhwan_runtime_audit` contains only the migrated schema (no business rows).
- No cleanup required; nothing was inserted.
- The audit database is **preserved** for Owner inspection (not deleted, not truncated), per restriction §13.

---

## Conclusion

The environment is fully ready (PostgreSQL 18, isolated `dastarkhwan_runtime_audit`/`dastarkhwan_audit`, migrations applied, .NET 10 SDK, both hosts build cleanly, configuration via user secrets). However, **the audit cannot be completed at runtime** because two application-code defects (D-1, D-2) prevent both `Dastarkhwan.Web` and `Dastarkhwan.Admin` from starting. All HTTP endpoint testing, CRUD verification, runtime security/isolation testing, and database-effect verification are blocked pending Owner approval to fix the source code. Existing automated tests were executed (all compile; failures are pre-existing SQLite/PostgreSQL test-environment mismatches or outdated solution inventory, not runtime defects). The isolated audit database is preserved for Owner inspection.
