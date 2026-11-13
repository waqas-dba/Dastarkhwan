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
6. If the baseline differs from the task brief, or relevant uncommitted changes could be affected, stop and report the conflict before proceeding.

Never discard, overwrite, or silently absorb existing changes.

The M0 report references `9555b8c` dated 2026-10-23. This is a historical baseline, not necessarily the current HEAD. Always inspect the actual repository before work.

Read-only Git inspection is allowed. Routine state-changing actions within an approved implementation task follow `decisions.md` D-017/D-007/D-008 (including scoped commit/push to the already-authorized branch and positively identified development-DB migration application/verification when included). Protected Git/DB operations, uncertain targets, destructive/irreversible actions, and scope expansion require separate explicit Project Owner approval (see Sections 9 and 10).

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
4. Follow the approved task scope and current permission model. For approved implementation tasks, routine in-scope validation, migration generation/review, positively identified development-DB migration application/verification (when included), and scoped commit/push to the already-authorized branch are covered by standing authorization per D-017/D-007/D-008; actions requiring separate explicit approval (protected/destructive/uncertain-target operations, scope expansion) must stop for Project Owner approval.
5. Implement only the approved task.
6. Report changed files, outcome, exact commands/results/failures/skips (including migration/DB verification metadata when applicable, non-secret), risks, and one suggested next task.
7. Stop and wait for review/Project Owner decision.

A micro-task should normally change one concern and have a clear review boundary. Split work further if it touches multiple module boundaries, persistence changes, or unrelated behaviors.

## 6. Agent responsibilities

The approved team consists of multiple AI participants and the Project Owner. Agent roles, coordination rules, and review boundaries are defined in `ai-tasks.md` §1 and `AGENTS.md`.

- OpenCode is the default primary implementation agent; Kilo is the alternative primary implementation agent when assigned.
- Only one implementation owner may modify a shared working tree at a time.
- ChatGPT Account 1 coordinates and recommends; ChatGPT Account 2 performs independent read-only review.
- Security-critical and architectural changes require explicit Project Owner approval and independent review.
- Routine standing automation for approved implementation tasks is governed by accepted decisions `D-017`, `D-007`, and `D-008` (scoped validation, EF Core/dev-DB steps when included, and scoped commit/push to the already-authorized branch), subject to safeguards and protected-operation approvals.

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

## 9. Security, data, and quality rules

- All authorization decisions must be server-side.
- Fail closed for permission and tenant access checks.
- Validate inputs, enforce ownership, and use transactions where operations must succeed or fail together.
- Do not trust client-supplied tenant/store IDs without authenticated membership validation.
- Use parameterized queries or ORM-generated SQL. Avoid raw SQL unless necessary, approved, and carefully reviewed.
- Never hard-code or expose secrets, passwords, connection strings, tokens, or signing keys.
- Prefer environment variables, user secrets, or a secure secret manager.
- If sensitive values are found in tracked files or history, report them and rotate/revoke them; do not print them.
- **Approved implementation tasks (D-017/D-007):** relevant builds, tests, formatting, and static analysis may run under standing authorization and must be reported with exact commands/results/failures/skips. Fix implementation-related failures in scope; do not fix unrelated failures without approval.
- **EF Core/dev DB (approved scope, D-017):** identify actual DbContext/migration project/provider/history, review migrations/SQL, and apply/verify only against a positively identified development database when included. Never assume target by name/environment variable alone.
- **Always require separate explicit Project Owner approval:** production database changes; database deletion/recreation/destructive reset; data deletion or irreversible transformations; unapproved migration rollback/downgrade; database credential/infrastructure changes; uncertain-target or destructive operations.
- **Documentation-only tasks:** no builds, tests, migrations, or scheduled checks unless specifically requested.

## 10. Git restrictions

Git inspection commands are allowed. Standing automation for approved implementation tasks (D-017/D-008) authorizes scoped stage/commit/push to the already-authorized branch after required validation/review.

- Stage only approved task files; never include unrelated or pre-existing changes.
- Stop and report if baseline differs or working tree contains unexpected/unrelated changes.
- **Separate explicit Project Owner approval required:** branch creation/switching, pull, merge, rebase, force-push, reset, clean, amend, cherry-pick, revert, history rewriting, and Git config/hook changes.
- AI may suggest commit messages and manual checkpoints, but the Project Owner retains final authority over repository history and acceptance.
- Asking for approval does not authorize execution for separately protected actions.

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