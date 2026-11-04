---
name: backend-microtask
description: Plan and implement one small .NET 10 / EF Core 10 / PostgreSQL backend micro-task in CoreKit (Identity, Access, Tenant, SharedKernel). Use when asked to add, change or fix a backend behaviour, entity, repository, service, endpoint or migration as a small slice. Requires inspecting the latest code first and never runs tests, builds or migrations without permission.
---

# Backend micro-task (.NET 10, EF Core 10, PostgreSQL)

Builds on `CLAUDE.md` (§3 repository check, §4 token budget, §5 micro-milestones, §8-9 rules, §11 report) and the task template in `ai-tasks.md` §7. This skill adds only the how-to; do not repeat those rules.

## 1. Inspect first (read only)

1. Baseline: HEAD hash/date and `git status --short`. Stop and ask if the baseline differs from the brief or uncommitted work overlaps.
2. Grep before opening files. Read only what the slice touches: entity, its configuration, repository + interface, service, endpoint, existing tests, and the latest migration/snapshot if the schema changes.
3. Check `architecture.md` §3 dependency rules: Identity references only SharedKernel; no module touches another module's DbContext.
4. Confirm the behaviour is not already implemented.

## 2. Slice it

One measurable outcome per slice, independently reviewable and revertible. Order: contract and validation, then persistence, service, endpoint, tests. Split when a slice spans more than one module, more than about 8 files, or mixes a schema change with a behaviour change (schema first, additive). List the slices, then implement only the approved first one.

## 3. Rules

- **Minimal change:** no renames, formatting or refactors outside the slice. Reuse existing folders and patterns (`Interfaces/`, `Repositories/`, `Services/`, `Models/`). Only repositories touch the DbContext.
- **Backward compatibility:** keep public signatures, routes, JSON shapes and config keys unless the brief says otherwise. Prefer optional parameters or overloads. Flag every breaking change explicitly; a renamed config key must fail loudly at startup.
- **Validation:** at the boundary and in the service; lengths match column limits; normalise once with the existing normalisers. Never trust client-sent tenant or user ids: take them from the current user/tenant context.
- **Errors:** expected problems return a typed result with a stable code and status (400/401/403/404/409/503). Unexpected errors propagate to middleware. Log without secrets or personal data; never return raw exception text. Handle unique-constraint races (`DbUpdateException`) by re-reading.
- **EF Core 10 / PostgreSQL:** async with `CancellationToken`; `AsNoTracking` for reads; project to DTOs; no queries in loops (watch N+1); `ExecuteUpdate`/`ExecuteDelete` for bulk changes; an explicit transaction when several writes must succeed together; UTC timestamps and `TimeProvider`; unique indexes enforce invariants and the code handles the violation; conditional updates or tokens for concurrency.

## 4. Database-change review (schema slices)

Give the user this checklist instead of acting:
- Additive or destructive? (drop, rename, new NOT NULL on a populated table = destructive, needs a data step.)
- Default or backfill for existing rows; index for the new queries; `Down` works; snapshot matches the model.
- PostgreSQL: index on a large table should use `CONCURRENTLY` (hand-written SQL); long locks.
- Deploy order: migrate first, then the new code.

Do not run `dotnet ef`, generate, or apply migrations without explicit permission. Provide the plan and the exact commands for the user.

## 5. Verification

Do not run tests, builds or migrations without permission. Instead list the tests to add or update (name and what each proves), give the exact `dotnet` commands for the user, and state what was **not** verified. Never claim success.

## 6. Output

Short report per `CLAUDE.md` §11: files changed, slice done, not verified, database impact, suggested next slice.