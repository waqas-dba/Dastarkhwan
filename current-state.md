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
| Current repository HEAD | See "Current repository snapshot" below; verify before every task |
| Working-tree status | See "Current repository snapshot" below; verify before every task |

`9555b8c` is a historical baseline, not a claim that it is the latest commit today. Before relying on any finding, check the real HEAD, commit date, staged/unstaged/untracked changes, and relevant files. Refresh this document only when new evidence is collected.

## Current repository snapshot
| Field | Value |
|---|---|
| Snapshot HEAD | `daa732666a497c87c9b9275abe1ed40899c3bf3b` |
| Commit date | 2026-11-08T14:45:23+05:00 |
| Branch | `master` |
| Evidence basis | Local clone inspected at the commit above. Findings below cite committed files only; the remote was not queried, so no GitHub-side verification is implied. |
| Local working tree (not part of the committed repository) | Reported from the local clone, not verified remotely: it also carries uncommitted migration work — untracked `CoreKit.Access/Migrations/` and `CoreKit.Identity/Migrations/`, a regenerated `CoreKit.Tenant/Migrations/` (`20261108104346_InitialTenant` untracked; the two `init_tenant*` files deleted) and a modified `TenantDbContextModelSnapshot.cs`. This work is uncommitted, unreviewed and must not be treated as approved, complete or ready to merge. |
| Scope | Point-in-time record. Re-verify HEAD before every task and refresh only when new evidence is collected. |

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

## Verified findings at the current snapshot
| Area | Status | Evidence at `daa7326` |
|---|---|---|
| Architecture tests | Partial | `ModuleBoundaryTests` covers SharedKernel, Identity, Access and Tenant; Tenant → Identity is an explicit temporary exception citing D-015/S4.3; the test project references Access, Identity and Tenant. Tests were not run, so passing behaviour is unverified. |
| Architecture test organization | Not started | Access-focused suites (`RoleServiceTests`, `AssignmentServiceTests`, `MaintenanceAndAuditTests`, `DefaultsAndHostingTests`, `Support/RulesTests`) still live in `CoreKit.Architecture.Tests`; no `CoreKit.Access.Tests` project exists (roadmap S2.1). |
| Access implementation | Partial | Role, permission, assignment, audit-log and scope-guard services plus `/access/*` endpoints exist. Committed recent work: `d60125a`, `70dc435`, `daa7326`. No build or test result is recorded, and **manual verification is still pending**. |
| Access migrations | Not started | No Access migration is tracked at this commit. The local untracked migration files noted in the snapshot row are unreviewed and unapproved. |
| Identity | Partial | No account-lockout fields or logic exist (searched `CoreKit.Identity` for failed-attempt and lockout members). Identity still owns roles and permissions, contrary to D-002. |
| Tenant | Partial | `CoreKit.Tenant.csproj` still references `CoreKit.Identity` directly, so Tenant → Identity decoupling (D-015, S4.3) is incomplete. |
| Store and M6–M13 | Not started | The solution contains no Store, Catalog, Cart, Orders, Payments, Fulfillment, Website or Marketplace project. |
| Migrations overall | Partial | Only the two Tenant `init_tenant*` migrations and their snapshot are tracked. Local regeneration is uncommitted; see the snapshot row. |
| Committed-secret follow-up | Unknown | Carried forward from the M0 finding; no new evidence and no rotation recorded. |

## Milestone status
| Milestone | Status | Notes |
|---|---|---|
| M0 — Repository baseline | Inspection reported complete | Baseline report at `9555b8c`; this was inspection-only. |
| M1 — Essential decisions | In progress | Decisions are recorded in `decisions.md`; documentation synchronization is S1 and remains in progress until implementation and review finish. Implementation is not implied. |
| M2 — Module foundation | Partial | Boundary tests cover all four modules; Access tests still sit in the architecture test project (S2.1 open). |
| M3 — Identity | Partial | Renamed project exists; lockout missing; roles/permissions still in Identity. |
| M4 — Access | Partial | Access services, endpoints and tests exist; no tracked migration; role ownership transfer (S6.2) not started; manual verification pending. |
| M5 — Tenant and Store | Partial | Tenant still references Identity directly (S4.3 incomplete); Store not started. |
| M6–M13 | Not started | No product code is present in the solution at this snapshot. |

## Accepted decisions and implementation status
Decision statements are recorded in `decisions.md`, which is the single source. They are deliberately not repeated here.

| Decision | Status | Evidence at `daa7326` |
|---|---|---|
| D-002 — Identity and Access ownership | Not started | Identity still owns role/permission entities, services and endpoints; roadmap S6.1/S6.2 open |
| D-011 — Tenant isolation strategy | Partial | Tenant isolation scaffolding exists; cross-module enforcement policy not implemented |
| D-012 — Session and lockout policy | Partial | Rotation and reuse detection exist; account lockout absent |
| D-015 — Tenant–Identity dependency | Not started | Direct project reference remains in `CoreKit.Tenant.csproj` |

## Deferred decisions
Do not resolve these prematurely. Decide only when the dependent milestone needs them: single-store vs multi-store checkout, parcel pickup initiation and lifecycle, payment methods/COD and payment provider, detailed country/currency/tax behavior, storefront/website technology, custom-domain verification/HTTPS operations, and marketplace behavior.

## Security follow-up — committed secrets
- M0 reported that `appsettings.Development.json` was not tracked at `9555b8c`, but was present in an earlier commit.
- Treat credentials, signing keys, connection strings, and API keys that may have been committed as compromised, even if the file is now deleted or ignored.
- Rotate affected secrets and move active values to secure local/user-secret/environment/secret-manager configuration as appropriate.
- Do not print secret values in reports. History cleanup, if needed, is a user-controlled Git operation and must be planned carefully; rotating secrets is the priority.
- At the current snapshot no new evidence was collected and no rotation is recorded; this action remains open.

## Next proposed work
Use the ordered slices in `roadmap.md`. Each slice is prepared by DeepSeek Harness, implemented by OpenCode + Nemotron, independently reviewed by ChatGPT Account 2, coordinated by ChatGPT Account 1, and approved by the user. S1 (documentation synchronization) remains in progress until implementation and review finish. After S1: a dedicated `CoreKit.Access.Tests` project (S2.1), review and approval of any Access migration (S3), Tenant decoupling (S4.1 → S4.3), Identity lockout (S5.1 → S5.2), and role-ownership transfer (S6.1 → S6.2).

## Update log
| Date | Change | Evidence |
|---|---|---|
| 2026-10-23 | M0 baseline inspection findings recorded | User-provided report for commit `9555b8c` |
| 2026-11-04 | Documentation package refreshed to reflect M0 report, approved M1 decisions, micro-slice workflow, and Git message guidance | Prepared documentation package; verify against actual repository before applying |
| 2026-11-08 | Snapshot refresh at `daa7326`: historical M0 baseline preserved; current snapshot recorded separately; accepted decisions separated from implementation; Access, Identity, Tenant and migration statuses updated from direct inspection; S1 left in progress | Section references above; evidence is committed-file inspection at the snapshot HEAD, with local working-tree details labelled as reported and uncommitted |

## Update rules
- Before updating this file, verify HEAD and working-tree state and inspect relevant code.
- Record commit hash/date and file paths or test names as evidence where possible.
- Distinguish target design, accepted decision, and implemented behavior.
- Do not mark tests/builds/migrations complete unless the result was observed.
- Never include passwords, tokens, signing keys, connection strings, or secret values.
- Keep this file concise; detailed architectural rules belong in `architecture.md`, decisions in `decisions.md`, and work queues in `roadmap.md` / `ai-tasks.md`.
