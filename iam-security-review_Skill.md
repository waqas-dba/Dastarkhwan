---
name: iam-security-review
description: Read-only security review of CoreKit Identity, Access, JWT and refresh tokens, account lockout, role ownership, token revocation and tenant isolation. Use when asked to review, audit or threat-check authentication, authorization, roles and permissions, session handling or cross-tenant access in a diff, a set of files or a module. Reports critical, important and minor findings separately and never modifies application code.
---

# Identity / Access security review (read-only)

Read and report only. Never edit code and never run tests, builds or migrations (`CLAUDE.md` §9). State the baseline commit/date first. Review the supplied diff or named files, not the whole system. Give evidence as `file:line`; separate **confirmed** from **suspected**; never claim files you did not read.

## Checks (apply the relevant ones)

**JWT / access token**
- Signing key comes from secrets and is validated at startup (length, not a placeholder); algorithm pinned; `alg: none` rejected; issuer, audience and lifetime validated; small clock skew.
- Short lifetime; minimal claims; no authorization data that can go stale unless it is revoked on change; a per-user version/stamp is checked on every request.

**Refresh tokens**
- Opaque, at least 256-bit random, stored hashed. Rotation is single-use and atomic (conditional update or transaction). Reuse of a rotated token revokes that user's sessions. Expiry enforced; empty input rejected; two simultaneous refreshes handled; revoked on password change, disable and logout; expired rows cleaned up.

**Passwords and enumeration**
- Slow salted hash; a dummy hash for unknown users; identical answer and timing for unknown, wrong password and disabled. Messages never reveal that an account exists. Password change needs the current password and ends sessions.

**Account lockout and throttling**
- Atomic failed-attempt counter, lock duration, reset on success; lockout cannot be abused as an easy denial of service. Per-IP limits use the real client address only behind trusted proxies. Email-sending endpoints are limited.

**Authorization and role ownership**
- Deny by default; every endpoint declares its requirement; checks run server-side per request against the source of truth, never client input.
- Object ids verified against the caller's scope (IDOR).
- Who may create, edit and assign roles; no privilege escalation (cannot grant what you do not hold); last owner/administrator protected; platform and tenant roles separate; the first administrator cannot be silently re-granted. Identity must not own roles that Access owns (D-002).

**Token revocation**
- Disable, delete, password change, role, membership or tenant change take effect on the next request, or are bounded by a stated TTL. Caches are invalidated on change. Stamp/version rotation is atomic with the change.

**Tenant isolation**
- Tenant comes from the verified token or membership, never from a header or body unless explicitly trusted and membership-checked.
- Every tenant-owned read is filtered and every write stamped; unique keys include the tenant; cache keys, jobs and events carry the tenant; cross-tenant escape hatches are platform-only and audited; suspended or archived tenants are blocked.
- Expect tests for tenant A reading, writing and deleting tenant B's data.

**Operations:** seeding is idempotent and race-safe; migrations do not drop auth data without a plan; no secrets in repository or logs; authentication events audited without secrets.

## Report (review template in `ai-tasks.md` §8; keep it short)

1. Baseline and scope reviewed (hash, files).
2. **Critical:** exploitable now (authentication bypass, cross-tenant access, token forgery, privilege escalation, secret exposure). For each: evidence, impact, minimal fix.
3. **Important:** a real weakness to fix before release (stale tokens, missing throttling, races, missing audit).
4. **Minor:** hardening and maintainability.
5. Uncertainties: the evidence needed to confirm.
6. Not reviewed.

Verdict: Approve / Approve with suggestions / Changes required.