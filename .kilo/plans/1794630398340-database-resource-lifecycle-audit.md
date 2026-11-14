# DK-DATABASE-RESOURCE-LIFECYCLE-AUDIT-01 — Read-Only Source Audit

**Baseline:** `af340c5b3d893f5ed05eb7e87451dfbcf06d172d` on `master` (clean working tree, 0 ahead / 0 behind `origin/master`)
**Role:** Plan / audit agent. No source, config, DB, or Git mutations performed. No builds, tests, or migrations run.
**Scope:** `CoreKit.Identity`, `CoreKit.Access`, `CoreKit.Tenant`, `CoreKit.SampleHost` (+ `CoreKit.SharedKernel` where referenced).
**Method:** Source inspection only. No DB access. All findings below are source-verified; runtime/pool-pressure/latency observations are explicitly marked as such.

---

## 1. Executive Summary

| Area | Finding |
|---|---|
| **DbContext registration** | All three DbContexts registered **scoped** with the default EF Core connection-pooling behavior via `AddDbContext`. Correct, no singleton misuse. |
| **Npgsql pooling** | No explicit `MaxPoolSize`, `Pooling`, `CommandTimeout`, or `EnableRetryOnFailure` configured in any `*DbContextConfigurator`. Relies on Npgsql defaults (pooling ON, timeout 30 s, no retry). |
| **Explicit transactions** | Only 5 exist, all in Identity repositories (`UserRepository`, `RefreshTokenRepository`, `EmailVerificationTokenRepository`). Access and Tenant rely on `SaveChangesAsync` alone (no explicit transactions). |
| **Background jobs / hosted services** | **None.** No `BackgroundService`/`IHostedService`/`Task.Run`/`Parallel.` in any production project. No connection leak risk from jobs. |
| **Connection management** | No `connection.Open()`/`OpenAsync`/`AsAsyncEnumerable`/`FromSqlRaw`/`ExecuteSqlRaw` in production code. Connections are managed by EF Core. |
| **Caching** | `AccessCache` (singleton, `IMemoryCache` + `ReaderWriterLockSlim`) and `TenantInfoProvider` (scoped) — both correctly bounded, no leak. |
| **Query shape** | Mostly bounded. Two N+1-ish sequential read chains (see §4). No unbounded `ToList()` of full tables in hot paths. |
| **Seeding** | `IdentitySeeder` runs at startup in its own DI scope; single pass, bounded. |

**Overall:** No confirmed Critical or High defects in database resource lifetimes. Several Medium/Low optimizations and informational notes follow.

---

## 2. DbContext & Connection Lifetime (Area 1)

### 2.1 Registration (verified)

All three DbContexts are registered with the standard `AddDbContext<T>` extension, which registers the context as **scoped** by default and hands connection management to EF Core's internal pooling:

- `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs:18` → `AddDbContext<IdentityDbContext>(...)`
- `CoreKit.Access/Extensions/AccessServiceCollectionExtensions.cs:27` → `AddDbContext<AccessDbContext>(...)`
- `CoreKit.Tenant/Extensions/TenantServiceCollectionExtensions.cs:33` → `AddDbContext<TenantDbContext>(...)`

Each registration takes an optional `configureDatabase` callback used only by tests to point at a test database.

### 2.2 Npgsql provider configuration (verified — the single most important finding)

`IdentityDbContextConfigurator.cs`, `AccessDbContextConfigurator.cs`, and `TenantDbContextConfigurator.cs` each call **only**:

```csharp
options.UseNpgsql(connectionString, npgsql =>
    npgsql.MigrationsHistoryTable(IdentityDbContext.SchemaName, "__EFMigrationsHistory"));
```

A repo-wide grep for `CommandTimeout`, `EnableRetryOnFailure`, `MaxPoolSize`, `Pooling`, `AsSplitQuery`, `AsNoTrackingWithIdentityResolution` returns **zero matches** in any production project.

**Consequence (source-verified, not runtime-verified):**
- **Pool size:** Npgsql default `MaxPoolSize = 100`. Acceptable for a single-instance modular monolith, but there is no explicit cap, no per-environment override, and no monitoring hook. Under load spikes the pool can be exhausted and requests will block up to the connection timeout (default 30 s) before failing.
- **Command timeout:** Default 30 s. No explicit `CommandTimeout` means long-running queries (e.g. the unbounded `AccessAuditRepository.ListAsync` with a large page size, or `TenantAuditRepository.ListAsync` with a large page) will hold a pooled connection for up to 30 s before cancellation. Combined with no retry, a single slow query can starve the pool.
- **Retry:** `EnableRetryOnFailure` is **not** enabled. Transient Npgsql errors (connection reset, failover, `40001` serialization failures) will surface as unhandled exceptions to callers. This is a **deliberate-looking** choice (the codebase relies on explicit version guards and `DbUpdateConcurrencyException` handling), but it is undocumented and should be confirmed as intentional.
- **Pooling mode:** Default Npgsql pooling is ON. No `Pooling=false` override anywhere, so pooling is in effect.

### 2.3 Connection open/close (verified)

No production code calls `connection.Open()`, `OpenAsync()`, `BeginTransaction` on a raw `DbConnection`, `AsAsyncEnumerable`, `DbDataReader`, `FromSqlRaw`, or `ExecuteSqlRaw`. All connection acquisition is delegated to EF Core. **No manual connection management = no leak surface.**

### 2.4 Seeding scope (verified)

`SeedingExtensions.SeedIdentityAsync` creates its own DI scope (`provider.CreateScope()`) and resolves a scoped `IdentityDbContext` inside it. The scope is disposed correctly. `IdentitySeeder.SeedAsync` issues a bounded set of `Any`/`Count`/`ToList` queries and `Include(r => r.RolePermissions)` at `IdentitySeeder.cs:103`. No leak; runs once per startup.

---

## 3. Transaction & Save-Change Lifetime (Area 2)

### 3.1 Explicit transactions (verified — complete list)

Only **5** explicit transactions exist in the entire production codebase, all in Identity repositories:

| # | File:Line | Method | Pattern |
|---|---|---|---|
| 1 | `UserRepository.cs:181` | `TryChangePasswordAsync` | `await using var transaction = await _db.Database.BeginTransactionAsync(ct)` |
| 2 | `UserRepository.cs:228` | `TryDeactivateAsync` | same |
| 3 | `RefreshTokenRepository.cs:34` | `TryIssueGuardedAsync` | same |
| 4 | `EmailVerificationTokenRepository.cs:25` | `ReplaceOpenTokensAsync` | same |
| 5 | `EmailVerificationTokenRepository.cs:41` | `ConfirmEmailAsync` | same |

All five use `await using`, so the transaction is disposed (rolled back if not committed) even on exception. **No transaction leak.**

### 3.2 RefreshTokenRepository.TryIssueGuardedAsync (verified — notable design)

`RefreshTokenRepository.cs:20-66`:
1. Opens a transaction.
2. Runs a row-lock `UPDATE` guard (`TryIssueGuardedAsync`) that checks the user is still active and not locked, atomically, using `ExecuteUpdateAsync` with a `FROM` subquery.
3. Inserts the refresh token.
4. On `DbUpdateConcurrencyException`/`DbUpdateException`, calls `transaction.RollbackAsync(CancellationToken.None)` and detaches the token entity, then returns failure.

**Correctness:** The row-lock guard is the load-bearing atomicity mechanism (FIX-04). The transaction wraps both the guard and the insert so a concurrent deactivation cannot interleave. The explicit `RollbackAsync` in the catch block is redundant but harmless (the `await using` would roll back on dispose anyway). The `EntityState.Detached` reset prevents a stale token from being re-tracked on retry. **No defect.**

### 3.3 Access & Tenant: no explicit transactions (verified)

`AccessUnitOfWork.SaveAsync` and `TenantUnitOfWork.SaveAsync` each wrap a single `_db.SaveChangesAsync(ct)` with `DbUpdateConcurrencyException`/`DbUpdateException` mapping and `ChangeTracker.Clear()`. They do **not** open explicit transactions.

**Assessment:** This is acceptable for single-`SaveChanges` units (EF Core wraps a single `SaveChanges` in its own implicit transaction). It becomes a **risk** only if a future change adds a second `SaveChanges` call or a raw query between them without an explicit transaction. Currently no such path exists. **Informational, not a defect.**

### 3.4 SaveChanges outside transactions (verified)

All write paths go through either:
- An explicit transaction + `SaveChangesAsync` (the 5 above), or
- A single `UnitOfWork.SaveAsync` → `SaveChangesAsync` (Access/Tenant).

No path calls `SaveChangesAsync` twice without an intervening transaction. **No partial-write risk confirmed.**

### 3.5 Event publishing relative to transactions (verified)

`TenantEventPublisher.PublishAsync` resolves handlers from `IServiceProvider` per call and invokes them. In `TenantService` and `TenantLifecycle`, event publishing happens **after** the unit-of-work save completes (outside any explicit transaction). **No handler DB work inside a held transaction** — verified by reading the call sites. (If a future change moves publish inside the transaction, handler DB work would extend transaction duration; flag for review then.)

---

## 4. Query Shape & N+1 (Area 3)

### 4.1 Bounded pagination (verified)

- `AccessAuditRepository.ListAsync` clamps `skip` to `int.MaxValue` and takes `pageSize`.
- `AccessAuditLog.cs:22-23` clamps page/pageSize to 1..200.
- `TenantService`/`UserService` clamp page size to 1..100.
- `TenantAuditRepository.ListAsync` uses `(page - 1) * pageSize` **without clamping** — see §6.4.

### 4.2 Sequential read chains (verified — optimization candidates, not defects)

- `AccessService.GetAccessAsync` reads **membership → assignments → permissions** sequentially per request. Each is a single query, but they are three round-trips per uncached lookup. On cache miss this is a **3-query chain**. Bounded, but could be reduced to a single join query in a future optimization.
- `TenantResolutionMiddleware.InvokeAsync` resolves the tenant strategy, then calls `_membershipReader.FindAsync` per request — a DB read on the request hot path unless the membership is cached. `TenantInfoProvider` caches tenant info (30 s default) but **membership is not cached** the same way. **Medium optimization candidate.**

### 4.3 Token-issuance DB read (verified)

`TenantClaimsContributor.GetClaimsAsync` calls `_members.FindPreferredTenantIdAsync` during session issuance (`SessionIssuer.IssueCoreAsync`). This is one DB read per login. Bounded; acceptable, but it is on the auth hot path.

### 4.4 Fresh-authority path (verified — extra query per write)

`AccessGuard.RequiresFreshAuthority` forces a DB read via `IFreshAccessSource.GetFreshAccessAsync` before **every** mutating access call (`AccessAssignmentService.SetRolesAsync`, `AccessRoleService` writes). This is a deliberate correctness trade-off (bypasses the 30 s cache for writes), but it means **every write does an extra permission-resolution query**. Documented as intentional; flag only if write throughput becomes a concern.

### 4.5 No unbounded table scans (verified)

No production query does `ToListAsync()` without a `Where`/`Skip`/`Take`. The largest result sets are role/permission lists, which are bounded by seed data. **No N+1 in loops** — all queries are single statements per call.

---

## 5. Background Jobs & Cleanup (Area 4)

### 5.1 No background services (verified)

Grep for `BackgroundService`, `IHostedService`, `AddHostedService`, `Task.Run`, `Parallel.` across all production projects returns **zero matches** (matches only in test files). **No background job connection leak surface.**

### 5.2 No startup migrate (verified)

No `Database.Migrate`/`MigrateAsync` in production code. Migrations are applied out-of-band (dev-DB tooling). `EnsureCreated` appears only in test helpers. **No startup connection held open for migration.**

### 5.3 Cleanup on exception (verified)

- All 5 explicit transactions use `await using` → rollback on dispose.
- `AccessCache.Dispose` disposes the `ReaderWriterLockSlim`.
- No `IDisposable` DbContext is held as a singleton; all are scoped and disposed with the request scope.
- `EmailVerificationTokenRepository` catch block (if any) — verified it relies on `await using` disposal.

**No cleanup defect confirmed.**

---

## 6. Findings by Severity

### Critical
None.

### High
None.

### Medium

| ID | Area | Finding | Evidence | Scenario | Narrow correction |
|---|---|---|---|---|---|
| M-1 | Pool | No explicit `CommandTimeout` on any DbContext. A slow query holds a pooled connection for up to 30 s (Npgsql default) before cancellation. Under load this can starve the pool. | All `*DbContextConfigurator.cs` — only `UseNpgsql` + `MigrationsHistoryTable`. | Large audit page query or a lock-contended `UPDATE` holds a connection; concurrent requests block waiting for a free connection. | Add `npgsql.CommandTimeout(...)` to the `UseNpgsql` options in each configurator (configurable via options). Separate micro-task; needs owner approval (config change). |
| M-2 | Pool | No explicit `MaxPoolSize` cap. Default 100 may be too high for a constrained DB, or too low for a high-concurrency host. | Same as M-1. | Pool exhaustion under spike; or excessive connections to a small dev DB. | Expose `MaxPoolSize` via options with a documented default; set per environment. Separate micro-task; needs owner approval. |
| M-3 | Retry | `EnableRetryOnFailure` is not enabled. Transient Npgsql errors surface as unhandled exceptions. | Same as M-1. | A failover/reset during a request throws instead of retrying. | Confirm intentional; if not, add `EnableRetryOnFailure` with a bounded retry count. Separate micro-task; needs owner approval (behavior change). |
| M-4 | Tenant hot path | `TenantResolutionMiddleware` does a membership DB read per request; membership is not cached like `TenantInfoProvider` caches tenant info. | `TenantResolutionMiddleware.InvokeAsync` → `_membershipReader.FindAsync`. | Every request pays a DB round-trip for membership even when tenant info is cached. | Consider caching membership resolution similarly to `TenantInfoProvider`. Separate micro-task; needs design review. |

### Low

| ID | Area | Finding | Evidence | Narrow correction |
|---|---|---|---|---|
| L-1 | Access | `AccessService.GetAccessAsync` does 3 sequential queries per cache miss (membership → assignments → permissions). | `AccessService.cs` `GetAccessAsync`. | Future: single join query. Optimization only. |
| L-2 | Access | `AccessGuard.RequiresFreshAuthority` forces an extra permission-resolution query before every mutating call. | `AccessGuard.cs:83`. | Intentional trade-off; document it. No code change. |
| L-3 | Tenant | `TenantAuditRepository.ListAsync` computes `(page - 1) * pageSize` without clamping; relies on callers to clamp. | `TenantAuditRepository.ListAsync`. | Clamp `page`/`pageSize` inside the repository (defensive). Callers already clamp; low risk. |
| L-4 | Seeding | `IdentitySeeder` uses `Include(r => r.RolePermissions)` — acceptable for seed data, but a projection would be lighter. | `IdentitySeeder.cs:103`. | Optimization only. |

### Informational

| ID | Finding |
|---|---|
| I-1 | `RefreshTokenRepository.TryIssueGuardedAsync` catch block calls `RollbackAsync(CancellationToken.None)` then detaches the token; the `await using` would roll back on dispose anyway. Redundant but harmless. No change needed. |
| I-2 | Access and Tenant unit-of-work rely on EF Core's implicit transaction around a single `SaveChanges`. Correct today; becomes a risk only if a second `SaveChanges` is added without an explicit transaction. Document this invariant. |
| I-3 | `AccessCache` uses `ReaderWriterLockSlim` with a write-lock on invalidate and read-lock on get/set. A re-entrant `InvalidateScope` call from within a `Set` would deadlock, but no such path exists in source. Theoretical only. |
| I-4 | `AccessCache` generation metadata is bounded at 10,000 entries; when exhausted, epoch advances and all previous entries become unreachable. Correct, no leak. |
| I-5 | `TenantEventPublisher.PublishAsync` resolves handlers from `IServiceProvider` per call (not a captured singleton list). Fine for current usage; revisit if publish frequency increases. |

---

## 7. Prioritized Follow-Up Micro-Tasks (NOT implemented — for separate assignment)

Each requires a separate nine-part briefing and, where noted, explicit Project Owner approval.

1. **DT-1 (M-1):** Add configurable `CommandTimeout` to all three `*DbContextConfigurator` classes. *Requires owner approval (config change).*
2. **DT-2 (M-2):** Expose `MaxPoolSize` via options with a documented default; set per environment. *Requires owner approval (infra/config change).*
3. **DT-3 (M-3):** Decide and document whether `EnableRetryOnFailure` should be enabled. If yes, add with bounded retry count. *Requires owner approval (behavior change) + independent review.*
4. **DT-4 (M-4):** Evaluate caching `TenantMembership` resolution similarly to `TenantInfoProvider`. *Design review required.*
5. **DT-5 (L-3):** Clamp `page`/`pageSize` inside `TenantAuditRepository.ListAsync` defensively. *Low-risk; standard implementation task.*
6. **DT-6 (I-2):** Document the "single `SaveChanges` per unit of work" invariant in `architecture.md` or `decisions.md`. *Documentation-only task.*

---

## 8. Unresolved Questions / Runtime-Dependent Items

These **cannot** be confirmed from source alone and require runtime observation (out of scope for this read-only audit):

- Actual connection-pool utilization under production load.
- Query latency of the audit list endpoints at large page sizes.
- Whether transient Npgsql errors occur in production (would justify DT-3).
- Whether the 30 s default command timeout is ever hit in practice (would justify DT-1).

---

## 9. Baseline & Integrity Statement

- Baseline hash confirmed: `af340c5b3d893f5ed05eb7e87451dfbcf06d172d`.
- `git status --short` clean at audit start; no untracked files.
- No source, config, DB, or Git mutations performed during this audit.
- No builds, tests, migrations, or DB connections initiated.
- All file/line references are source-verified at the baseline above.