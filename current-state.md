# Current State — Dastarkhwan / CoreKit

## Purpose and evidence rules
This file records implementation facts, not aspirations. Every statement must be labelled or traceable to evidence. Architecture/decisions documents describe the target unless code evidence confirms implementation.

- **Verified at baseline:** reported as directly inspected at the named commit in the M0 report.
- **Reported / needs refresh:** taken from a report, but must be rechecked against current HEAD before implementation.
- **Partial:** some implementation exists; the full acceptance criteria are not met.
- **Not started:** the M0 report found no product implementation at its baseline.
- **Unknown:** not inspected or insufficient evidence.

## Last recorded baseline
| Field | Value |
|---|---|
| Baseline commit | `9555b8c` |
| Commit date in M0 report | 2026-10-23 |
| M0 status | Inspection-only baseline reported complete |
| Evidence source | User-provided M0 inspection report |
| Current repository HEAD | **Not known from this document; verify before every task** |
| Working-tree status | **Not known from this document; verify before every task** |

`9555b8c` is a historical baseline, not a claim that it is the latest commit today. Before relying on any finding, check the real HEAD, commit date, staged/unstaged/untracked changes, and relevant files. Refresh this document only when new evidence is collected.

## Verified M0 findings at `9555b8c`
| Area | Status at baseline | Finding / evidence from M0 report |
|---|---|---|
| Repository documentation | Present | `CLAUDE.md`, `roadmap.md`, `current-state.md`, `decisions.md`, `architecture.md`, and `ai-tasks.md` exist at this commit. |
| Project naming | Updated | Identity and sample host are renamed to `CoreKit.Identity` and `CoreKit.SampleHost`. Do not use old `CoreKit.IAM.SampleHost` name without checking current solution. |
| SharedKernel | Present | `CoreKit.SharedKernel` exists. |
| Architecture tests | Partial | `CoreKit.Architecture.Tests` exists; its project reference covered only Access at baseline. Access tests were located inside this project. |
| Identity | Partial | `CoreKit.Identity` exists. No account lockout was found. Identity still owned roles and permissions, contrary to the target ownership boundary. |
| Access | Partial | Access implementation exists, but no Access migration was present at baseline. |
| Tenant | Partial | Tenant implementation exists but directly referenced Identity in six code files at baseline. |
| Store | Not started | No Store implementation was reported at baseline. |
| M6–M13 product modules | Not started | No product code was reported for these roadmap areas at baseline. Recheck current HEAD before relying on this. |
| Development configuration | Security action required | `appsettings.Development.json` was no longer tracked at baseline, but had been tracked in an earlier commit. Treat any secrets ever committed as exposed; rotate them. Removing a file from the current tree does not remove it from Git history. |

## Milestone status
| Milestone | Status | Notes |
|---|---|---|
| M0 — Repository baseline | Inspection reported complete | Baseline report at `9555b8c`; this was inspection-only. |
| M1 — Essential decisions | Decisions approved; docs synchronization pending | Three decisions listed below were approved by the user. Record them in `decisions.md`; do not imply implementation is complete. |
| M2 — Module foundation | Partial | SharedKernel and architecture tests exist; test organization/references need work. |
| M3 — Identity | Partial | Renamed project exists; lockout missing; roles/permissions still in Identity. |
| M4 — Access | Partial | Access exists; no migration at baseline; role ownership transition incomplete. |
| M5 — Tenant and Store | Tenant partial; Store not started | Tenant directly referenced Identity in six code files at baseline. |
| M6–M13 | Not started at baseline | Refresh status from latest code before planning implementation. |

## Accepted M1 decisions — implementation not yet implied
1. **Authentication/session:** short-lived JWT access tokens and rotating refresh tokens; refresh-token reuse detection and secure revocation; lockout after 5 failed attempts for 15 minutes.
2. **Tenant isolation:** one shared PostgreSQL database. Tenant-owned records have explicit `TenantId` ownership and EF Core global query filters. Global filters are not the only security boundary; application services and authorization must enforce tenant scope too.
3. **Tenant/Identity coupling:** remove Tenant's direct Identity project dependency through stable SharedKernel contracts/abstractions. Exact contract design must be inspected and approved before implementation.

## Deferred decisions
Do not resolve these prematurely. Decide only when the dependent milestone needs them: single-store vs multi-store checkout, parcel pickup initiation and lifecycle, payment methods/COD and payment provider, detailed country/currency/tax behavior, storefront/website technology, custom-domain verification/HTTPS operations, and marketplace behavior.

## Security follow-up — committed secrets
- M0 reported that `appsettings.Development.json` was not tracked at `9555b8c`, but was present in an earlier commit.
- Treat credentials, signing keys, connection strings, and API keys that may have been committed as compromised, even if the file is now deleted or ignored.
- Rotate affected secrets and move active values to secure local/user-secret/environment/secret-manager configuration as appropriate.
- Do not print secret values in reports. History cleanup, if needed, is a user-controlled Git operation and must be planned carefully; rotating secrets is the priority.

## Next proposed work
Use the micro-slices in `roadmap.md` and `ai-tasks.md`. The first slice is documentation synchronization (S1). After that, separate Access test organization, architecture-test coverage, Access migration, Tenant decoupling, Identity lockout, and role ownership transfer into separately approved tasks.

## Update log
| Date | Change | Evidence |
|---|---|---|
| 2026-10-23 | M0 baseline inspection findings recorded | User-provided report for commit `9555b8c` |
| 2026-11-04 | Documentation package refreshed to reflect M0 report, approved M1 decisions, micro-slice workflow, and Git message guidance | Prepared documentation package; verify against actual repository before applying |

## Update rules
- Before updating this file, verify HEAD and working-tree state and inspect relevant code.
- Record commit hash/date and file paths or test names as evidence where possible.
- Distinguish target design, accepted decision, and implemented behavior.
- Do not mark tests/builds/migrations complete unless the result was observed.
- Never include passwords, tokens, signing keys, connection strings, or secret values.
- Keep this file concise; detailed architectural rules belong in `architecture.md`, decisions in `decisions.md`, and work queues in `roadmap.md` / `ai-tasks.md`.
