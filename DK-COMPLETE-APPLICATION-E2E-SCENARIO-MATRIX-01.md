# DK-COMPLETE-APPLICATION-E2E — Scenario Matrix (DK-COMPLETE-APPLICATION-E2E-SCENARIO-MATRIX-01)

**Baseline:** `da45a14`. Read-only. Each endpoint maps to applicable scenarios; non-applicable marked N/A with reason. Scenario codes: S=success, U=unauthenticated(401), X=unauthorized(403), I=invalid-input(400), B=boundary, D=duplicate(409), M=missing-resource(404), C=cross-tenant(403/404 + DB unchanged), P=persistence, T=transaction/rollback, K=concurrency, F=failure/recovery, R=rate-limit(429).

## 1. Identity — Auth (endpoints 1–10)
| Endpoint | S | U | X | I | B | D | M | C | P | T | K | F | R |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 me | ✓ | ✓ | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | ✓(secver) | N/A |
| 2 login | ✓ | N/A(anon) | N/A | ✓ | ✓(pw len) | N/A | ✓(unknown) | N/A | ✓(refresh row) | ✓ | ✓(concurrent) | ✓(lockout) | ✓ |
| 3 refresh | ✓ | N/A | N/A | ✓ | N/A | ✓(replay) | ✓ | N/A | ✓(rotation) | ✓ | ✓(concurrent) | ✓(reuse) | ✓ |
| 4 logout | ✓ | N/A | N/A | ✓ | N/A | ✓(reuse) | N/A | N/A | ✓(revoked) | N/A | N/A | ✓ | ✓ |
| 5 change-password | ✓ | ✓ | N/A | ✓ | ✓ | N/A | N/A | N/A | ✓(secver++) | ✓ | N/A | ✓(wrong cur) | ✓ |
| 6 verify-email | ✓ | N/A | N/A | ✓ | N/A | ✓(reuse) | ✓ | N/A | ✓ | N/A | N/A | ✓(expired) | ✓ |
| 7 send-verification | ✓ | ✓ | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | ✓ | ✓ |
| 8 resend-verification | ✓ | N/A | N/A | ✓ | N/A | N/A | ✓ | N/A | N/A | N/A | N/A | ✓(cooldown) | ✓ |
| 9 forgot-password | ✓(always 200) | N/A | N/A | ✓ | N/A | N/A | ✓(unknown→200) | N/A | ✓(token) | N/A | N/A | N/A | ✓ |
| 10 reset-password | ✓ | N/A | N/A | ✓ | ✓ | ✓(reuse) | ✓ | N/A | ✓(pw hash) | ✓ | N/A | ✓(expired) | ✓ |

## 2. Identity — Users (11–15)
| Endpoint | S | U | X | I | B | D | M | P | T | K | F |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 11 list | ✓ | ✓ | ✓(no perm) | ✓(bad page) | ✓(pageSize) | N/A | N/A | N/A | N/A | N/A | N/A |
| 12 get | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | N/A | N/A | N/A | N/A |
| 13 create | ✓ | ✓ | ✓ | ✓ | ✓ | ✓(dup email 409) | N/A | ✓ | ✓(rollback) | ✓(dup concurrent) | N/A |
| 14 update | ✓ | ✓ | ✓ | ✓ | N/A | N/A | ✓ | ✓(email immutable) | ✓ | ✓(optimistic) | N/A |
| 15 delete | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | ✓ | N/A | ✓(owner-protected 409) |

## 3. Access (16–29)
| Endpoint | S | U | X | I | D | M | C(scope) | P | K | F |
|---|---|---|---|---|---|---|---|---|---|---|
| 16 me | ✓ | ✓ | N/A | N/A | N/A | N/A | ✓(400 no scope) | N/A | N/A | N/A |
| 17 permissions | ✓ | ✓ | N/A | N/A | N/A | N/A | ✓ | N/A | N/A | N/A |
| 18 roles | ✓ | ✓ | N/A | N/A | N/A | N/A | ✓ | N/A | N/A | N/A |
| 19 role get | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | N/A | N/A | N/A |
| 20 role create | ✓ | ✓ | ✓ | ✓ | ✓(dup name) | N/A | ✓ | ✓ | ✓(concurrent) | N/A |
| 21 role update | ✓ | ✓ | ✓ | ✓(stamp) | ✓(stamp conflict) | ✓ | ✓ | ✓ | ✓ | N/A |
| 22 role perms set | ✓ | ✓ | ✓ | ✓ | ✓(stamp) | ✓ | ✓ | ✓ | N/A | N/A |
| 23 role delete | ✓ | ✓ | ✓ | N/A | N/A | ✓ | ✓ | ✓ | N/A | N/A |
| 24 user roles get | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | N/A | N/A | N/A |
| 25 user roles set | ✓ | ✓ | ✓ | ✓ | N/A | ✓ | ✓ | ✓ | ✓(concurrent) | N/A |
| 26 audit | ✓ | ✓ | N/A | ✓(page) | N/A | N/A | ✓ | N/A | N/A | N/A |
| 27 platform member add | ✓ | ✓ | ✓ | ✓ | ✓(dup) | N/A | N/A(platform) | ✓ | ✓ | N/A |
| 28 platform member get | ✓ | ✓ | N/A | N/A | N/A | N/A(404 not member) | N/A | N/A | N/A | N/A |
| 29 platform member revoke | ✓ | ✓ | ✓ | N/A | N/A | ✓ | N/A | ✓ | N/A | N/A |

## 4. Tenant (30–51)
| Endpoint | S | U | X | I | B | D | M | C | P | T | K | F | R |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 30 list(platform) | ✓ | ✓ | ✓ | ✓ | ✓ | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 31 current | ✓ | ✓ | N/A | N/A | N/A | N/A | ✓(tenant.required 400) | ✓(mismatch) | N/A | N/A | N/A | ✓(suspended 403) | N/A |
| 32 me | ✓ | ✓ | N/A | N/A | N/A | N/A | N/A | N/A | ✓ | N/A | N/A | N/A | N/A |
| 33 default | ✓ | ✓ | N/A | ✓ | N/A | N/A | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ |
| 34 get | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | N/A | N/A | N/A | N/A | N/A |
| 35 create | ✓ | ✓ | ✓ | ✓ | ✓ | ✓(dup slug 409) | N/A | N/A | ✓(+owner) | ✓ | ✓(concurrent) | N/A | ✓ |
| 36 update | ✓ | ✓ | ✓ | ✓ | N/A | N/A | ✓ | ✓(C) | ✓ | ✓ | ✓ | N/A | ✓ |
| 37 activate | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | ✓ | ✓(transition) | N/A | ✓(invalid transition 409) | ✓ |
| 38 suspend | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | ✓ | ✓ | N/A | ✓ | ✓ |
| 39 archive | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | ✓ | ✓ | N/A | ✓ | ✓ |
| 40 restore | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓ | ✓ | ✓ | N/A | ✓ | ✓ |
| 41 delete | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | ✓(read_only 409) | ✓ | N/A | N/A | ✓ |
| 42 audit | ✓ | ✓ | ✓ | ✓ | ✓ | N/A | ✓ | ✓(C) | N/A | N/A | N/A | N/A | N/A |
| 43 settings list | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | N/A | N/A | N/A | N/A | N/A |
| 44 settings get | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | N/A | N/A | N/A | ✓(404 key) | N/A |
| 45 settings put key | ✓ | ✓ | ✓ | ✓ | ✓(len) | N/A | ✓ | ✓(C) | ✓ | N/A | ✓ | N/A | ✓ |
| 46 settings put bulk | ✓ | ✓ | ✓ | ✓ | N/A | N/A | ✓ | ✓(C) | ✓ | ✓ | N/A | N/A | ✓ |
| 47 settings del | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | ✓ | N/A | N/A | ✓(404) | ✓ |
| 48 members list | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | N/A | N/A | N/A | N/A | N/A |
| 49 member add | ✓ | ✓ | ✓ | ✓ | N/A | ✓(dup) | ✓ | ✓(C) | ✓ | ✓ | ✓(concurrent) | ✓(owner validate) | ✓ |
| 50 member remove | ✓ | ✓ | ✓ | N/A | N/A | N/A | ✓ | ✓(C) | ✓ | ✓ | N/A | ✓(last owner) | ✓ |
| 51 owner set | ✓ | ✓ | ✓ | ✓ | N/A | N/A | ✓ | ✓(C) | ✓ | ✓ | N/A | ✓(owner rules) | ✓ |

## 5. Host-level (52–64)
| Endpoint | S | U | X | I | F | Notes |
|---|---|---|---|---|---|---|
| 52 health/live | ✓ | N/A | N/A | N/A | N/A | always live |
| 53 health/ready | ✓ | N/A | N/A | N/A | ✓(DB down→503) | |
| 54–62 dev/demo | ✓ | ✓ | ✓ | N/A | N/A | dev-only; 58/59/60 tenant+perm |
| 63 admin platform member | ✓ | ✓ | ✓(non-admin 403) | ✓ | N/A | Admin host |
| 64 admin health/ready | ✓ | ✓ | N/A | N/A | N/A | Admin host, auth-protected |

## 6. Cross-tenant isolation matrix (mandatory)
Actors: TenantA owner/staff, TenantB owner/staff, unprivileged user, platform admin. For each resource type, TenantA actor attempts TenantB resource:
| Operation | Endpoints | Expected | DB proof |
|---|---|---|---|
| GET single | 34 | 403/404 | B record read count unchanged |
| GET collection | 30,48,43,42,24 | 403/404 | no B rows returned |
| POST create under B | 49,45,46 | 403/404 | B members/settings unchanged |
| PUT/PATCH update B | 36,21,51 | 403/404 | B row UpdatedAt/values unchanged |
| DELETE B | 41,50,47,23 | 403/404 | B row still present |
| Assignment | 25 | 403/404 | no assignment row added |
| Resource/tenant ID substitution | all {id} | 403/404 | unchanged |
| Header/JWT mismatch | 31 | 400/403 | no context leak |

Each denied **write** must capture DB state before+after and assert equality (current `cross-tenant.spec.ts` only checks API responses, not DB — gap to close in Phase 4).

## 7. Outbox / background (where implemented)
| Scenario | Target | Status |
|---|---|---|
| Event creation + outbox persistence | Tenant lifecycle / Identity account-deletion | Applicable |
| Worker claim + lease + ordering | TenantOutboxPublisherService/Dispatcher | Applicable |
| Retry / max attempts / dead-letter | Dispatcher (MaxAttempts=10) | Applicable |
| Handler success/failure (IdentityUserDeleted→Tenant+Access cleanup) | IdentityOutboxTransport + IEventHandler | Applicable |
| Duplicate delivery / idempotency | projection (MembershipGeneration) | Applicable |
| No event loss on rollback | same-transaction staging | Applicable |
| Shutdown/cancellation, restart recovery | BackgroundService | Applicable (best-effort) |

## 8. PostgreSQL / persistence (where implemented)
Applicable: unique constraints (email, role name, tenant slug), FK integrity, check constraints, required fields, optimistic concurrency (ConcurrencyStamp on roles), tenant-scoped queries, transaction commit/rollback, RLS (Identity RLS migration present). SQLite substitution prohibited for PG-specific behavior.

## 9. Non-applicable (with reason)
- CORS for non-browser API E2E: N/A (APIRequestContext, no Origin).
- Email delivery (real SMTP): N/A — dev uses DevEmailInbox/Console.
- Production-only startup migration guard: N/A in Development.
- Store/Catalog/Orders/Payments modules: N/A — not implemented (Pending per roadmap).