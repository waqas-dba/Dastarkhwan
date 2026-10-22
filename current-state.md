# Current State — Dastarkhwan / CoreKit

## Purpose and status labels
Record repository facts based on inspection. Planned architecture is not evidence of implementation.
- **Verified:** directly inspected or confirmed by recorded result.
- **Partially verified:** incomplete evidence.
- **Unknown:** not inspected.
- **Not implemented:** inspected and confirmed absent in scope.
- **Blocked:** awaiting a decision/dependency.

## Reported context — verify before relying on it
| Item | Previously reported | Status |
|---|---|---|
| Project | Dastarkhwan / CoreKit | Partially verified |
| Framework | .NET 10 / EF Core 10 | Unknown; inspect project files |
| Database | PostgreSQL, `saas_db` | Unknown; never expose credentials |
| Sample host | `CoreKit.IAM.SampleHost` | Unknown; verify solution |
| Admin/Public ports | 5065 / 5066 | Unknown; inspect launch profiles |
| Tenant header | `X-Tenant-ID` | Unknown; verify trust/validation |
| Identity/Access split | Identity = authentication; Access = authorization | Target decision; verify implementation |
| SaaS composition | Tenant, Identity, Access required | Target decision; verify implementation |
| Standalone composition | Tenant not required | Target decision; verify implementation |
| Cache | 30 seconds was mentioned | Unknown; inspect scope/invalidation |
| Owner permissions | Owners intended to receive applicable permissions | Unknown; verify enforcement |

## M0 repository checklist
| Area | Status | Evidence/notes |
|---|---|---|
| Solution/project structure | Unknown | |
| Framework/package versions | Unknown | |
| Startup, host composition, DI | Unknown | |
| Module boundaries/dependencies | Unknown | |
| Database provider and safe configuration | Unknown | Never paste secrets |
| Contexts, migrations, constraints | Unknown | |
| Authentication/account lifecycle | Unknown | |
| Authorization and permission enforcement | Unknown | |
| Tenant/store context and fail-closed behavior | Unknown | |
| Resource ownership checks | Unknown | |
| API endpoints/request flow | Unknown | |
| Validation/error handling | Unknown | |
| Logging, audit, health checks | Unknown | |
| Background jobs/webhooks | Unknown | |
| Unit/integration/security tests | Unknown | Add/update tests; AI does not run them |
| Build/test/deployment instructions | Unknown | |
| Known defects | Unknown | |

## Current milestone
**M0 — Repository baseline and current-state verification**  
**Status:** Not started; update after inspection.

Outputs:
- Concise solution/project map.
- Module/dependency map.
- Authentication, authorization, and tenant-isolation findings.
- Persistence/migration/test inventory.
- Confirmed gaps, unknowns, security risks.
- One recommended next task with acceptance criteria.

## Risks to verify early
1. Client-supplied `X-Tenant-ID` may be spoofed unless validated against authenticated identity and authorized tenant membership.
2. Identity and Access may be logically named but still coupled through private persistence.
3. Owner permissions must not bypass tenant/store boundaries.
4. Cache keys/results must not leak across users, tenants, or stores.
5. Optional Identity/Access in standalone must not mean protected operations are unprotected.
6. Verify reported frameworks, packages, ports, and database settings from actual files.

## Findings log
| Date | Finding | Evidence | Impact/next action |
|---|---|---|---|
| YYYY-MM-DD | Baseline pending | Repository inspection not recorded | Perform M0 |

## Update rules
Record file paths/non-sensitive evidence. Distinguish reported context from observed facts. Never include passwords, tokens, connection strings, or unnecessary personal data. Do not claim tests/build/deployment succeeded without evidence. Keep architecture decisions in `docs/decisions.md`.
