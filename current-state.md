# Current State — Dastarkhwan / CoreKit

## Purpose and evidence rules

This file records implementation facts, not aspirations. Architecture and decisions documents describe the target unless code evidence confirms implementation.

Use these evidence labels consistently:

- **Verified at current snapshot:** directly inspected committed files at the recorded snapshot HEAD. This confirms only the inspected source or documentation, not successful builds, tests, migrations, or database operations.
- **Reported complete (not independently verified):** reported by an agent, but not independently confirmed against the relevant implementation or artifact.
- **In progress:** implementation or documentation work has started but its acceptance criteria are not yet met.
- **Pending:** work has not started or awaits an approval, decision, or required action.
- **Under review:** an implementation or artifact exists and requires independent review or acceptance.
- **Blocked:** work cannot safely proceed until a stated dependency or decision is resolved.
- **Unknown:** not inspected or insufficient evidence.

A status must describe the evidence available, not the intended architecture or an agent's confidence.

## Historical M0 baseline

| Field | Value |
|---|---|
| Baseline commit | `9555b8c` |
| Commit date in M0 report | 2026-10-23 |
| M0 status | Reported complete (not independently verified) |
| Evidence source | User-provided M0 inspection report |
| Scope | Historical, inspection-only baseline |

`9555b8c` is a historical baseline, not the current repository HEAD. Its findings must not be presented as current implementation facts without checking later changes.

## Current repository snapshot

| Field | Value |
|---|---|
| Snapshot HEAD | `beb0a0b550f1d825d416c48ae0a69fef188ea303` |
| Parent commit | `45a578958e555b9cb3fc77a5ae602f796fd270b1` |
| Branch | `master` |
| Latest commit message | `docs: resolve documentation governance and baseline inconsistencies` |
| Evidence basis | Git metadata inspection at the snapshot HEAD. The remote was not queried; no claims are made about remote state. |
| Local working-tree status | Clean at inspection time (`git status --short` returned empty). Not verified remotely. |
| Scope | Point-in-time record of committed repository evidence |

The latest repository snapshot is `beb0a0b`, a documentation-only commit. It changed project documents but did not change application source code.

The previous documentation synchronization commit is `45a5789`.

The source-inspection baseline for the Tenant/Identity and Tenant/Access findings remains `5dfa796ca52fc0b122b3efea182f2dc7dbd5cde9`. That earlier baseline was used to inspect source code and classify Tenant's Identity dependency status. The documentation-only commits at `45a5789`, `beb0a0b` and `d7585c3` did not re-inspect source code; no claim is made that source behavior changed between these baselines.

The historical M0 baseline `9555b8c` remains unchanged.

The commit at `43c2179` includes committed migration artifacts:

- Access: `CoreKit.Access/Migrations/`
- Identity: `CoreKit.Identity/Migrations/`
- Tenant: `CoreKit.Tenant/Migrations/`

Their presence in Git does not establish that they are reviewed, compatible with existing databases, successfully applied, or safe for production. No database application is claimed. No later commit changed these migration artifacts.

Before every implementation task, verify the actual local HEAD, commit date, staged changes, unstaged changes, and untracked files. Do not assume the local working tree matches GitHub.

## Verified M0 findings at `9555b8c`

These findings are retained as historical evidence from the M0 report.

| Area | Status at baseline | Finding / evidence from M0 report |
|---|---|---|
| Repository documentation | Verified at historical baseline | `CLAUDE.md`, `roadmap.md`, `current-state.md`, `decisions.md`, `architecture.md`, and `ai-tasks.md` existed. |
| Project naming | Verified at historical baseline | Identity and sample host were renamed to `CoreKit.Identity` and `CoreKit.SampleHost`. |
| SharedKernel | Verified at historical baseline | `CoreKit.SharedKernel` existed. |
| Architecture tests | In progress | `CoreKit.Architecture.Tests` existed; its project reference covered only Access at that baseline. Access tests were located inside this project. |
| Identity | In progress | `CoreKit.Identity` existed. No account lockout was found. Identity still owned roles and permissions, contrary to the target ownership boundary. |
| Access | In progress | Access implementation existed, but no Access migration was present at that baseline. |
| Tenant | In progress | Tenant implementation existed but directly referenced Identity in six code files at that baseline. |
| Store | Pending | No Store implementation was reported at that baseline. |
| M6–M13 product modules | Pending | No product code was reported for these roadmap areas at that baseline. |
| Development configuration | Under review | M0 reported that `appsettings.Development.json` was no longer tracked at `9555b8c`, but had been tracked in an earlier commit. Treat any secrets ever committed as exposed and rotate them. Removing a file from the current tree does not remove it from Git history. |

## Findings at the current snapshot

The following source findings were inspected at `daa7326` and remain applicable to `43c2179` where the intervening commit did not change the relevant source files.

| Area | Status | Evidence and limitations |
|---|---|---|
| Architecture tests | In progress | Boundary enforcement now includes evaluated MSBuild project references, solution inventory and IVT policy (`MsBuildProjectEvaluator.cs`, `SolutionInventory.cs`, `ProjectReferenceBoundaryTests.cs`, `ModuleBoundaryMatrix.cs`). Tests were not run as part of this inspection. |
| Architecture test organization | Under review | `CoreKit.Access.Tests` exists with 9 files (created in `470d42c`): four test files, `GlobalUsings.cs`, `Support/{Fakes,RulesTests,TestEnv}.cs`, and the csproj. Independent review and a user-run test execution are pending (S2.1). |
| Access implementation | In progress | Access services, endpoints, audit and scope-related implementation exist. Build, test and manual verification results are not established here. |
| Access migrations | Under review | Access migration artifacts are committed under `CoreKit.Access/Migrations/` at `43c2179`. Schema correctness, compatibility and database application are not verified. |
| Identity | In progress | Identity implementation exists. The available evidence does not establish completion of the accepted Identity responsibilities or account lockout. |
| Identity migrations | Under review | Identity migration artifacts are committed under `CoreKit.Identity/Migrations/`. Schema correctness, compatibility and database application are not verified. |
| Identity role and permission ownership | Under review | The accepted target assigns roles and permissions to Access. The implementation ownership transfer must be verified against current Identity and Access entities, services, persistence and consumers before claiming completion. |
| Tenant | In progress | `CoreKit.Tenant/CoreKit.Tenant.csproj` references `CoreKit.SharedKernel`; no direct `CoreKit.Identity` project reference found at source level (D-015 direct-dependency requirement completed at project-reference level). Remaining Identity-related semantic coupling persists in `TenantActor` (`ICurrentUser.Permissions`) and must be classified before removal. Tenant–Access authorization integration is not yet implemented or verified. |
| Tenant–Access authorization | Pending | Tenant–Access authorization integration through Access public contracts is not yet implemented or verified. No Tenant adapter for Access public abstractions exists in the inspected source. |
| Platform authorization | Pending | Platform-wide/cross-tenant authorization is not yet implemented. No Access-owned platform authorization design has been established. |
| Per-operation permission enforcement | Pending | Tenant permission enforcement across individual protected operations is not yet implemented. Named Access permissions are not enforced across Tenant operations. |
| Tenant isolation | In progress | `CoreKit.Tenant/Isolation/TenantAwareDbContext.cs` contains tenant query-filter logic. `CoreKit.Tenant.Tests/IsolationTests.cs` contains isolation test coverage. These files alone do not prove complete enforcement across all application operations. |
| Tenant migrations | Under review | Tenant migration artifacts have changed and are committed at `43c2179`. Compatibility and database application are not verified. |
| Store and later product modules | Pending | No implementation completion is established for Store, Catalog, Cart, Orders, Payments, Fulfillment, Website or Marketplace. |
| Migrations overall | Under review | Access, Identity and Tenant migration artifacts are committed. No successful migration execution or database state is claimed. |
| Committed-secret follow-up | Unknown | Carried forward from the M0 finding. No new evidence of secret rotation or history remediation is recorded. |

## Milestone status

| Milestone | Status | Notes |
|---|---|---|
| M0 — Repository baseline | Reported complete (not independently verified) | Historical inspection report at `9555b8c`; inspection-only. |
| M1 — Essential architecture decisions | In progress | Accepted decisions are recorded in `decisions.md`; S1 documentation synchronization remains in progress until implementation and review finish. Decision acceptance does not imply implementation completion. |
| M2 — Module foundation | In progress | Boundary tests cover SharedKernel, Identity, Access and Tenant, with Tenant → Identity as an explicit temporary exception under D-015. Access suites now live in `CoreKit.Access.Tests` (S2.1 under review); boundary enforcement uses evaluated project references (S2.2 in progress). |
| M3 — Identity | In progress | Identity exists; completion of lockout and the role/permission ownership transfer is not established. |
| M4 — Access | In progress | Access services, endpoints and tests exist. Migration artifacts are committed and require review. Role ownership transfer and manual verification remain open. |
| M5 — Tenant and Store | In progress | Tenant has no direct `CoreKit.Identity` project reference at HEAD `5dfa796c` (D-015 direct-dependency completed at source level). Remaining Identity semantic coupling in `TenantActor` and Tenant–Access authorization integration (D-016) are outstanding. Store implementation is pending. |
| M6 — SaaS Subscription (`CoreKit.Subscription`) | Pending | No implementation completion established. |
| M7–M14 | Pending | Product implementation and acceptance have not been established. |

## Accepted decisions and implementation status

Decision statements are recorded in `decisions.md`, which remains the single source for accepted decisions. Acceptance of a decision does not mean implementation is complete.

| Decision | Implementation status | Evidence and limitations |
|---|---|---|
| D-002 — Identity and Access ownership | Under review | The target assigns authentication and identity lifecycle to Identity, and roles, permissions, assignments and authorization to Access. Verify current entities, services, persistence and consumers before claiming the ownership transfer is complete. S6.1/S6.2 remain open. |
| D-011 — Tenant data isolation | In progress | `CoreKit.Tenant/Isolation/TenantAwareDbContext.cs` contains tenant query-filter logic. `CoreKit.Tenant.Tests/IsolationTests.cs` includes tenant-isolation test coverage. Complete enforcement across application services, authorization, background jobs and other access paths is not established by these references alone. |
| D-012 — Authentication and session approach | Under review | The accepted policy is recorded in `decisions.md`. The available evidence does not establish completion of refresh-token rotation, reuse detection, revocation and account lockout together. Verify the relevant Identity implementation before updating this status. |
| D-015 — Tenant–Identity dependency | Completed at source level (direct dependency) | No direct `CoreKit.Identity` project reference in `CoreKit.Tenant/CoreKit.Tenant.csproj` at HEAD `5dfa796c`; D-015's direct-dependency requirement is met at project-reference level. Remaining Identity-related semantic coupling in `TenantActor` (`ICurrentUser.Permissions`) is classified as outstanding and must be resolved separately. |
| D-016 — Tenant requires Access for authorization | Pending | Decision accepted (2026-11-11). Tenant–Access authorization integration is not yet implemented or verified. No Tenant adapter for Access public abstractions exists in the inspected source. Platform authorization and per-operation permission enforcement remain outstanding. |

## Deferred decisions

Do not resolve these prematurely. Decide only when the dependent milestone needs them:

- Single-store vs multi-store checkout.
- Parcel pickup initiation and lifecycle details.
- Payment methods, COD behavior and payment provider.
- Detailed country, currency and tax behavior.
- Storefront and website technology.
- Custom-domain verification and HTTPS operations.
- Marketplace behavior.

## Security follow-up — committed secrets

- M0 reported that `appsettings.Development.json` was not tracked at `9555b8c`, but was present in an earlier commit.
- Treat credentials, signing keys, connection strings and API keys that may have been committed as compromised, even if the file is now deleted or ignored.
- Rotate affected secrets and move active values to secure local, user-secret, environment, or secret-manager configuration as appropriate.
- Do not print secret values in reports.
- History cleanup, if needed, is a user-controlled Git operation and must be planned carefully. Rotating secrets is the priority.
- No new evidence of secret rotation or history remediation is recorded at the current snapshot. This action remains open.

## Current agent allocation

The current agreed operating allocation is:

- **ChatGPT Account 1 — Project Manager and Architecture Coordinator:** defines tasks, coordinates agents, maintains roadmap and milestones, evaluates reports, recommends acceptance. Does not modify repository files or perform Git writes.
- **OpenCode — Primary implementation agent:** implements approved development tasks, supplies exact diffs, provides completion reports.
- **ChatGPT Account 2 — Independent read-only reviewer:** inspects actual implementation, architecture, security, and focused diff. Read-only.
- **Kimi — Architecture discussion, research and critique.**
- **DeepSeek Web — Research and technical preparation.**
- **OpenHands — Repository exploration and planning; isolated implementation only when explicitly assigned and not concurrently editing with OpenCode.**
- **Claude — Limited reserve for difficult implementation or security/architecture review.**
- **Kilo — Unavailable until the Project Owner says otherwise.**
- **DeepSeek Harness — Unavailable until the Project Owner says otherwise.**
- **User — Project Owner:** final authority, manual testing, and approval of all Git write operations. Agents may execute Git write operations only after explicit Project Owner approval.

Only one agent may modify the shared working tree at a time. All participating agents must verify and report the same baseline SHA.

### Instruction-file synchronization

`CLAUDE.md`, `ai-tasks.md`, `AGENTS.md`, `roadmap.md` and `current-state.md` now state the current agent allocation (ChatGPT Account 1, OpenCode, ChatGPT Account 2, Kimi, DeepSeek Web, OpenHands, Claude, Kilo unavailable, DeepSeek Harness unavailable, User) and the mandatory task briefing standard (`ai-tasks.md` §13). Verified at HEAD `d7585c3` (2026-11-11).

## Next proposed work

Follow the ordered micro-slices in `roadmap.md`.

S1 documentation synchronization changes were committed at `45a5789` (2026-11-11). Correction tasks `DK-TENANT-ACCESS-DOCS-FIX1`, `DK-TENANT-ACCESS-DOCS-FIX2` and `DK-TENANT-ACCESS-DOCS-FIX3` address review findings. Governance corrections were committed at `d7585c3` (2026-11-11). S1 remains Under review until independent review and Project Owner acceptance are complete.

After S1, follow the approved roadmap queue. Migration artifacts committed at `43c2179` require review; their presence does not authorize database application.

## Update log

| Date | Change | Evidence |
|---|---|---|
| 2026-10-23 | M0 baseline inspection findings recorded | User-provided report for commit `9555b8c` |
| 2026-11-04 | Documentation package refreshed to reflect M0 report, approved M1 decisions, micro-slice workflow and Git message guidance | Prepared documentation package; verify against actual repository before applying |
| 2026-11-08 | Snapshot refreshed at `daa7326`; historical M0 baseline preserved; current snapshot recorded separately; accepted decisions separated from implementation | Committed-file inspection at `daa7326`; local working-tree details were separately reported and not remotely verified |
| 2026-11-08 | Current snapshot updated for `43c2179`; committed Access, Identity and Tenant migration artifacts recorded; evidence labels and agent-allocation discrepancy clarified | GitHub committed-file inspection at `43c2179`; local working-tree and database state not verified |
| 2026-11-08 | Snapshot refreshed to `576c84d`; S2.1 recorded Under review; architecture-test evidence updated; obsolete instruction-file discrepancy removed; mandatory task briefing recorded in `ai-tasks.md` §13 | Committed-file inspection at `576c84d`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | Snapshot refreshed to `5dfa796ca52fc0b122b3efea182f2dc7dbd5cde9`; D-016 (Tenant requires Access for authorization) recorded; D-015 direct-dependency marked completed at source level; Tenant–Access integration, platform authorization and per-operation enforcement recorded as Pending; agent allocation synchronized to current team | Committed-file inspection at `5dfa796c`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | Documentation synchronization committed at `45a578958e555b9cb3fc77a5ae602f796fd270b1`; correction task `DK-TENANT-ACCESS-DOCS-FIX1` addresses review findings; S1 remains Under review; source-inspection baseline preserved at `5dfa796c` | Committed-file inspection at `45a5789`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | Earlier documentation/governance corrections committed at `beb0a0b550f1d825d416c48ae0a69fef188ea303`; snapshot updated to `beb0a0b`; source-inspection baseline preserved at `5dfa796c`; Git, build/test and agent-role wording harmonized | Committed-file inspection at `beb0a0b`; local tree clean at inspection; no tests or builds run |
| 2026-11-11 | DK-TENANT-ACCESS-DOCS-FIX2 governance consistency corrections committed at `d7585c34cd1c06d5119bb3c51ea29c7178273b39`; corrected commit attribution distinguishing `beb0a0b` from `d7585c3`; instruction-file synchronization references updated; source-inspection baseline preserved at `5dfa796c`; DeepSeek Web restricted to research and technical preparation only | Committed-file inspection at `d7585c3`; local tree clean at inspection; no tests or builds run |

## Update rules

- Before updating this file, verify actual HEAD and local working-tree state and inspect relevant code.
- Record commit hash, date and file paths or test names as evidence where possible.
- Distinguish target design, accepted decision and implemented behavior.
- Do not mark tests, builds, migrations or database operations complete unless the result was observed.
- Never include passwords, tokens, signing keys, connection strings or secret values.
- Keep this file concise. Detailed architectural rules belong in `architecture.md`, decisions in `decisions.md`, and work queues in `roadmap.md` and `ai-tasks.md`.