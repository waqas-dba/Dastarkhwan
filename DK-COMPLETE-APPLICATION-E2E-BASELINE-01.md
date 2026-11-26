# DK-COMPLETE-APPLICATION-E2E — Phase 0 Baseline (DK-COMPLETE-APPLICATION-E2E-BASELINE-01)

**Phase:** 0 — Read-only baseline verification and discovery.
**Date:** 2026-11-26.
**Scope:** Read-only. No source, docs, config, DB, builds, tests, or Git writes performed.

## 1. Repository baseline

| Field | Value |
|---|---|
| Local branch | `master` |
| Local HEAD (full SHA) | `da45a146ab491091739f8cc9c0087561a8a046c1` |
| Remote `origin/master` HEAD | `da45a146ab491091739f8cc9c0087561a8a046c1` |
| Ahead / behind (origin...HEAD) | `0 / 0` |
| Working tree | Clean — no staged, unstaged, or untracked changes (`git status --short` empty) |
| Baseline candidate `da45a14` still correct? | **Yes** — local == remote, tree clean |

### Commits since previous E2E audit baseline (`4101562`)
```
da45a14 updated
27d122c fix: F-1 add bearer token to post/put in apiClient.ts; F-2 replace nonexistent toBeIn matcher with valid Playwright pattern in cross-tenant.spec.ts
0491aa2 e2e independent review 02
8960d10 e2e setup and test data
c649a7d fix: reconcile Identity contracts, outbox, revocation reason, RLS cleanup, test scripts, Playwright setup
e4f78db automatic testing
```
No unexpected changes. Baseline accepted.

## 2. Documentation inspection (read-only)

| Document | Present | State vs baseline `da45a14` |
|---|---|---|
| `CLAUDE.md` | Yes | Current; governance/permission model aligns with `ai-tasks.md`. No baseline pin (intentional). |
| `AGENTS.md` | Yes | Current; team roles + standing automation (D-017/D-007/D-008). |
| `roadmap.md` | Yes | **Stale** — near-term queue references baseline `c9dbbe12` (2026-11-15); not updated to `da45a14`. |
| `current-state.md` | Yes | **Stale** — snapshot table pins `3c562cf`; milestone/decision tables predate E2E commits. |
| `decisions.md` | Yes | Current decision log (D-001…D-019). D-03 (JWT revocation) and D-18 (rate limiting) recorded as open Owner decisions in current-state, but code now enforces both (see §5). |
| `architecture.md` | Yes | Target architecture; explicitly states target vs implemented. SharedKernel removed. |
| `ai-tasks.md` | Yes | Current; §13 nine-part briefing + four owner actions, §14 documentation/comment standard. |

### Documentation contradictions recorded (not modified)
1. `current-state.md` "Current repository snapshot" = `3c562cf`, but actual HEAD = `da45a14` (6+ commits later, including E2E setup). Status tables do not reflect E2E work.
2. `roadmap.md` near-term queue references `c9dbbe12`; no E2E milestone/slice recorded.
3. `current-state.md` lists **D-03 (JWT `secver` not validated)** and **D-18 (no rate limiting on tenant/access writes)** as open Owner decisions, but source at `da45a14` shows both are implemented:
   - `CoreKit.Identity/Extensions/ServiceCollectionExtensions.cs` `OnTokenValidated` validates `secver` against `User.SecurityVersion` (D-03 resolved in code).
   - `AddTenantRateLimiting` (`tenant.write` 20/min) and `AddAccessRateLimiting` (`access.write`) are registered in both hosts (D-18 resolved in code).

## 3. Reviewed projects (actual, excluding test projects)

| Project | Role |
|---|---|
| `CoreKit.Abstractions` | `IIntegrationEvent`, `IEventHandler<TEvent>`, ProblemDetails factory. |
| `CoreKit.Identity` / `.Contracts` | Accounts, auth, tokens, lockout, recovery, Identity outbox. |
| `CoreKit.Access` / `.Contracts` | Roles, permissions, assignments, authorization, platform scope. |
| `CoreKit.Tenant` / `.Contracts` | Tenant lifecycle, membership, context/resolution, settings, durable outbox. |
| `Dastarkhwan.Integration` | Composes modules; claims contributors, event handlers, outbox transport, Tenant API endpoints, bridges. |
| `Dastarkhwan.Web` | SaaS host (Identity+Tenant+Access). |
| `Dastarkhwan.Admin` | Admin/POS host (Identity+Tenant+Access + platform member endpoint). |
| `CoreKit.SharedKernel` | Empty, unreferenced (not active). |
| `CoreKit.SampleHost` | Directory present in tree but **not in `Dastarkhwan.slnx`** (not built; excluded from discovery). |

Test projects (out of scope for Phase 0): `CoreKit.*.Tests`, `Dastarkhwan.Integration.Tests`, `CoreKit.Architecture.Tests`, `TestTypeCheck`.

## 4. Counts

| Metric | Count |
|---|---|
| Module business API endpoints (Identity+Access+Tenant) | **51** |
| Host-level endpoints (Web health/demo/dev + Admin) | **13** |
| Total discovered endpoints | **64** |
| Implemented business workflows (grouped) | **~20** |
| E2E scenario-matrix rows | See `DK-COMPLETE-APPLICATION-E2E-SCENARIO-MATRIX-01.md` |
| Existing E2E spec files | **6** |
| Existing E2E test cases | **52** |
| Confirmed harness defects | **7** |
| Database identification status | **Identified (development)** — see §6 |

## 5. Key implementation facts relevant to E2E

- JWT `secver` validation is **active** (cache 30s) — stolen tokens invalidated on credential change.
- Tenant resolution: header `X-Tenant-Id` trusted (`TrustHeader=true`), claim `tenant_id`, excluded prefixes `/identity,/health,/favicon.ico,/.well-known`. Only `/tenants/current` uses `.RequireTenant()`; other tenant routes enforce scope via `RequireAccess` + service-layer cross-tenant guards.
- Access scope context returns **400 `access.scope_required`** when no scope resolvable; platform admin claim (`platform_admin`) and `tenant_id` claim are injected at token issuance by Integration contributors.
- Tenant outbox `BackgroundService` (`TenantOutboxPublisherService`) is opt-in (`AddTenantOutboxPublisher`, registered in Web). Identity outbox publisher is registered by `AddIdentity`.
- Refresh-token atomic rotation + replay detection + family revocation implemented.

## 6. Database identification (non-secret metadata)

| Source | Database | Host | Role | Password |
|---|---|---|---|---|
| `Dastarkhwan.Web/appsettings.json` (base) | `dastarkhwan` | localhost | postgres | present (redacted) |
| `Dastarkhwan.Web/appsettings.Development.json` (active in Development) | `dastarkhwan_runtime_audit` | 127.0.0.1 | `dastarkhwan_audit` | present (redacted) |
| `Dastarkhwan.Admin/appsettings.json` | (none — inherits env/Web) | — | — | — |

**Active Development target:** `dastarkhwan_runtime_audit` @ 127.0.0.1, role `dastarkhwan_audit`. This is a dedicated audit DB (not production). **Status: positively identified as a development/test target by name and dedicated role.** Production DB (`prod-db`) only appears in `appsettings.Production.json` with `${DB_PASSWORD}` placeholder and is not to be touched.

> Phase 0 performed **no** DB connection. Confirmation that the role can connect and that the schema is migrated belongs to Phase 1 (requires Owner go-ahead since it is a DB operation).

## 7. Missing credentials / dependencies / blockers

- No missing credentials for the **Web** host in Development (signing key present in `appsettings.Development.json`).
- **Admin host lacks `Identity:Jwt:SigningKey`** in its `appsettings.json` and has no `appsettings.Development.json` — it relies on env var/Web copy. Running Admin standalone in Development will fail JWT validation unless the key is supplied via environment. **Blocker for Admin-host E2E** (Phase 1 must set `Identity__Jwt__SigningKey` env or Owner decision).
- E2E harness has no valid `npm test` script and baseURL protocol/port mismatch (see infrastructure assessment). **Harness blocker** (Phase 1 fix, in approved scope).
- `node_modules` not installed in `tests/e2e` (no install run in Phase 0).

## 8. Proposed small implementation phases (for Owner approval)

- **Phase 1 — Harness:** fix `package.json` scripts; correct `playwright.config.ts` baseURL (HTTPS 53584 Web / 53586 Admin) + `ignoreHTTPSErrors`; add CI reporters; add bearer for all verbs (already OK) and fix `.toBe(403).or.toBe(404)` matchers; add fixtures + cleanup; `.gitignore` node_modules/report/test-results; supply Admin signing key via env; verify one authenticated request per host.
- **Phase 2 — Module E2E:** Identity, Access, Tenant.
- **Phase 3 — Host/API coverage:** every Web + Admin endpoint.
- **Phase 4 — Security/data integrity:** cross-tenant with DB before/after evidence, PostgreSQL constraints/transactions.
- **Phase 5 — Reliability:** concurrency, token replay, outbox, rate limiting.
- **Phase 6 — Full execution + reports.**
- **Phase 7 — Independent review (Claude Account 2, read-only).**

**Stop:** Await Project Owner approval of endpoint inventory, scenario matrix, test DB, and plan before Phase 1.