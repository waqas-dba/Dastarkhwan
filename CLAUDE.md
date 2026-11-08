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

Read-only Git inspection is allowed. Git write operations are prohibited; see Section 10.

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

The approved team consists of four AI participants and the user.

| Participant | Responsibility |
|---|---|
| ChatGPT Account 1 | Project Manager and Architecture Coordinator. Defines tasks, coordinates agents, maintains roadmap and milestones, evaluates reports, and recommends acceptance. |
| DeepSeek Harness | Preparation Engineer. Researches the repository, investigates issues, prepares technical plans and handovers, and performs only explicitly approved isolated, low-risk implementation tasks. |
| OpenCode + Nemotron | Primary Developer. Implements approved development tasks, supplies exact diffs, and provides completion reports. |
| ChatGPT Account 2 | Independent Reviewer. Reviews actual code, architecture, security, and the focused diff. Read-only. |
| User | Project Owner. Approves scope and architecture, controls Git operations, performs manual testing, and makes final acceptance decisions. |

Coordination rules:

- ChatGPT Account 1 coordinates but does not modify repository files or perform Git writes.
- DeepSeek normally prepares implementation plans and handovers for OpenCode + Nemotron.
- OpenCode + Nemotron is the default implementation owner.
- DeepSeek may implement only when the user explicitly assigns it a separate, isolated, low-risk implementation task. That task must have one clearly named owner.
- ChatGPT Account 2 performs independent read-only review and does not implement fixes.
- One implementation owner per task.
- Only one AI agent may modify a shared working tree at a time.
- Other agents must use a read-only copy or supplied diff/report.
- All participating agents must use and state the same task baseline.
- No agent may silently expand scope or take over another agent's responsibility.
- Security-critical and architectural changes require explicit user approval and independent review.
- The user decides which review findings to accept.

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
- AI may create or update tests but must not run tests or builds unless the user explicitly authorizes that specific action.
- Do not execute database migrations or modify a database unless explicitly authorized.
- Creating migration files also requires explicit approval for that task.
- Never claim a test, build, migration, or deployment succeeded unless its result was actually observed and authorized.

## 10. Git restrictions

The user alone controls Git write operations.

AI agents must never:

- Stage or commit.
- Push or pull.
- Create or switch branches.
- Merge, rebase, reset, cherry-pick, clean, amend, or rewrite history.
- Change Git configuration or hooks.

Read-only inspection is permitted.

After a coherent slice is complete and reviewed, an agent may suggest a manual checkpoint and prepare a commit message. The user decides whether and when to commit.

Do not suggest a commit for incomplete work, unreviewed changes, or unresolved critical security/data issues.

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