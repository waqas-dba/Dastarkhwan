# Dastarkhwan / CoreKit — Engineering Instructions

## 1. Product scope

Build a secure, maintainable modular application for food-item delivery and parcel pickup/delivery. Pakistan is the first market; additional markets, including the UK, come later through configuration and replaceable integrations.

Do not add hotel, dine-in, or unrelated verticals without an approved decision.

## 2. Project source of truth

- `roadmap.md` — milestone order and completion criteria.
- `architecture.md` — target architecture and module boundaries.
- `current-state.md` — implementation status and supporting evidence.
- `decisions.md` — accepted decisions and open questions.
- `ai-tasks.md` — agent roles, task workflow, handovers, review templates, the mandatory nine-part task briefing and owner actions (§13), and the mandatory documentation, code-comment and explainability standard (§14), and Git guidance.
- `AGENTS.md` — short entry point for all AI agents.

Do not duplicate full sections across files. If documents conflict, report the exact conflict and ask before changing security, persistence, product behavior, compatibility, or module boundaries.

Actual repository code is evidence of implemented behavior. Architecture documents describe intended design unless implementation is independently verified.

## 3. Mandatory repository check

Before every task, including documentation-only tasks:

1. Verify repository root and the actual paths of relevant files.
2. Read the current HEAD hash and commit date.
3. Check staged, unstaged, and untracked changes using `git status --short`.
4. Inspect only files and diffs relevant to the task.
5. State the exact HEAD hash used as the task baseline.
6. If the baseline differs from the task brief, or relevant uncommitted changes could be affected, stop and ask the user.

Never discard, overwrite, or silently absorb existing changes.

The M0 report references `9555b8c` dated 2026-10-23. This is a historical baseline, not necessarily the current HEAD. Always inspect the actual repository before work.

Read-only Git inspection is allowed. Git operations that change repository state require explicit Project Owner approval before execution; see Section 10.

## 4. Token and context efficiency

- One task should have one small, measurable outcome.
- Read only the relevant documentation sections and source files.
- Reuse existing verified reports where the source has not changed.
- Do not repeat project context already recorded in project documents.
- Load only the agent instructions or specialist skills needed for the current task.
- Avoid speculative alternatives, unnecessary research, broad audits, and premature abstractions.
- Do not investigate future milestones while completing the current task.
- Do not generate extra documentation or refactor unrelated code.
- Stop when the assigned outcome and acceptance criteria are complete.
- Keep plans and reports concise.

## 5. Micro-milestone execution

For every non-trivial task:

1. Verify the latest repository baseline and working tree.
2. Read only the relevant files.
3. Provide a task briefing using the mandatory nine-part structure and the four owner actions in `ai-tasks.md` §13: goal/current issue, scope, expected files, acceptance criteria, risks, and the owner's decision, action, verification and acceptance.
4. Wait for user approval before editing or executing state-changing commands.
5. Implement only the approved task.
6. Report changed files, outcome, tests added or updated but not run, risks, and one suggested next task.
7. Stop and wait for the user.

A micro-task should normally change one concern and have a clear review boundary. Split work further if it touches multiple module boundaries, persistence changes, or unrelated behaviors.

## 6. Agent responsibilities

The approved team consists of multiple AI participants and the user.

| Participant | Responsibility |
|---|---|
| ChatGPT Account 1 | Project Manager and Architecture Coordinator. Defines tasks, coordinates agents, maintains roadmap and milestones, evaluates reports, and recommends acceptance. |
| OpenCode | Default primary implementation agent. Implements approved tasks, runs approved-task validation, supplies exact diffs, and provides completion reports. |
| Kilo | Alternative primary implementation agent. May own approved implementation tasks when explicitly assigned, following the same workflow and safeguards as OpenCode. |
| ChatGPT Account 2 | Independent Reviewer. Reviews actual code, architecture, security, and the focused diff. Read-only. |
| Kimi | Architecture discussion, research and critique. |
| DeepSeek Web | Research and technical preparation only. |
| OpenHands | Repository exploration and planning; isolated implementation only when explicitly assigned and not concurrently editing with OpenCode or Kilo. |
| Claude | Limited reserve for difficult implementation or security/architecture review. |
| DeepSeek Harness | Unavailable until the Project Owner says otherwise. |
| User | Project Owner. Approves scope and architecture, controls standing/explicit approvals, performs manual testing, and makes final acceptance decisions. |

Coordination rules:

- ChatGPT Account 1 coordinates but does not modify repository files or perform Git writes.
- OpenCode or Kilo may prepare plans/handover and, when assigned, implement the approved task.
- OpenCode is the default implementation owner; Kilo is the alternative implementation owner when assigned.
- DeepSeek Web may perform research and technical preparation only; it is not DeepSeek Harness and must not be assigned implementation work.
- DeepSeek Harness may implement only when the user explicitly assigns it a separate, isolated, low-risk implementation task. That task must have one clearly named owner.
- ChatGPT Account 2 performs independent read-only review and does not implement fixes.
- One implementation owner per task.
- Only one AI agent may modify a shared working tree at a time.
- Other agents must use a read-only copy or supplied diff/report.
- All participating agents must use and state the same task baseline.
- No agent may silently expand scope or take over another agent's responsibility.
- Security-critical and architectural changes require explicit user approval and independent review.
- The user decides which review findings to accept.

Full role definitions and handover rules are in `ai-tasks.md`. Routine standing automation for approved implementation tasks is documented as proposed in `decisions.md` D-017 and requires Project Owner acceptance before it becomes authoritative over prior command-by-command approval text.

Full role definitions and handover rules are in `ai-tasks.md`.

## 7. Backend-first development

Initial development priority is backend only:

1. CoreKit module boundaries and SharedKernel contracts.
2. Identity authentication, credentials, sessions, and tokens.
3. Access roles, permissions, assignments, and authorization.
4. Tenant lifecycle, context, and tenant isolation.
5. Backend APIs and application use cases for food delivery and parcel pickup.

Do not start frontend/UI work or later business workflows until their roadmap dependencies and decisions are ready.

## 8. Architecture and implementation rules

- Prefer a modular monolith. Do not introduce microservices without an evidenced need and user approval.
- Each module owns its business rules and persistence.
- Cross module boundaries only through approved public contracts or events.
- Never access another module's private DbContext, tables, repositories, or internals.
- Avoid circular dependencies and unnecessary generic frameworks.
- Identity owns accounts, authentication, credentials, sessions/tokens, and account recovery.
- Access owns roles, permissions, assignments, and authorization.
- Tenant must not depend directly on Identity. Use an approved stable abstraction at the appropriate boundary.
- Keep endpoints thin; business rules belong to the owning application/domain layer.
- Preserve existing behavior and compatibility where practical.
- Explain required breaking changes before implementation.
- Do not assume planned architecture is already implemented; verify source and tests.

## 9. Security, data, and quality

- Authorize every protected server operation.
- Fail closed when identity, tenant, store, permission, or resource ownership is unclear.
- Never trust client-supplied user, tenant, store, price, discount, or state values without server-side validation.
- Apply tenant/store scope to reads, writes, exports, reports, jobs, callbacks, and admin paths.
- Hostnames and request headers are routing/context inputs, not authorization by themselves.
- Tenant-owned rows must carry explicit tenant ownership under the approved shared-database strategy.
- EF Core global query filters are defense-in-depth, not the only security boundary.
- Enforce scope in application services and authorization, and test cross-tenant denial.
- Keep secrets out of source control and logs.
- Use parameterized data access, validation, safe errors, appropriate constraints, short transactions, and idempotency where required.
- Add focused positive and negative tests for relevant changes, especially authorization and cross-tenant access.
- **Approved implementation tasks:** run relevant builds, tests, formatting, and static analysis; report exact commands, results, failures, and skipped tests. Fix implementation-related failures and rerun required checks. Broader suites may be run when justified; do not fix unrelated failures without approval.
- **EF Core/migrations (when included in approved task and model changes):** identify actual DbContext/migration project/provider/history, generate and inspect migrations and SQL scripts, build/test, and apply only to a positively identified development database; verify migration history and expected schema changes and report migration name, target metadata (non-secret), and verification results. Never assume development by name/environment variable.
- **Documentation-only tasks:** do not run builds, tests, migrations, or scheduled checks unless specifically requested.
- **Separate explicit Project Owner approval required:** production database changes; database deletion/recreation/destructive reset; data deletion or irreversible transformations; unapproved migration rollback/downgrade; database credential/infrastructure changes; any uncertain-target or destructive operation. Creating/applying migrations outside approved development-task scope also requires explicit approval.
- Never claim a test, build, migration, or deployment succeeded unless its result was actually observed and authorized. Never expose secrets in files, logs, commits, or reports.

## 10. Git restrictions

The user retains final authority over Git write operations and final acceptance. Git inspection commands (`git status`, `git log`, `git diff`, branch listing) are permitted without approval.

**Within an approved implementation task (subject to Project Owner acceptance of D-017 standing automation):** the assigned implementation agent may stage only approved task files, commit after required validation/review, and push to the already-authorized branch. Review the complete diff first, stop if unrelated/pre-existing changes are present, and never include unrelated work.

**Always require separate explicit Project Owner approval:** branch creation/switching, pull, merge, rebase, force-push, reset, clean, amend, cherry-pick, revert/history rewriting, and Git config/hook changes. Do not perform these automatically.

**Outside approved implementation-task standing automation (or until D-017 is accepted):** state-changing Git operations require explicit Project Owner approval before execution; explain purpose, exact command, benefits, risks/consequences, and affected branches/commits/remotes, then wait for explicit approval.

Do not suggest or create a commit for incomplete work, unreviewed required changes, unresolved critical security/data issues, or when unrelated changes would be included. Prepare clear commit messages and report exact Git actions/results.

## 11. Completion report

Keep reports short and factual:

1. Baseline HEAD hash and working-tree status.
2. Files changed and outcome.
3. Tests added/updated, clearly marked not run unless explicitly authorized.
4. Security, data, and compatibility considerations.
5. Outstanding risks and manual verification steps.
6. One suggested next micro-task.
7. Optional suggested manual Git checkpoint and commit message.
8. Documentation and code comments updated per `ai-tasks.md` §14, or a stated reason they were not required.

Never claim local working-tree verification based only on GitHub inspection.