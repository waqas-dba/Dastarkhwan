# AGENTS.md — Dastarkhwan Shared Agent Instructions

This file is the shared entry point for AI agents working in this repository. It summarizes the approved team workflow and points to the authoritative project documents.

Read the relevant instructions before acting. Do not assume another agent has already read these files.

## 1. Team roles

| Participant | Role |
|---|---|
| ChatGPT Account 1 | Project Manager and Architecture Coordinator |
| OpenCode | Default primary implementation agent |
| Kilo | Alternative primary implementation agent (follows the same approved workflow and safeguards as OpenCode) |
| ChatGPT Account 2 | Independent Reviewer — read-only |
| Kimi | Architecture discussion, research and critique |
| DeepSeek Web | Research and technical preparation only. Not DeepSeek Harness; do not assign implementation work. |
| OpenHands | Repository exploration and planning; isolated implementation only when explicitly assigned and not concurrently editing with OpenCode or Kilo |
| Claude | Limited reserve for difficult implementation or security/architecture review |
| DeepSeek Harness | Unavailable until the Project Owner says otherwise |
| User | Project Owner and final decision-maker |

Full responsibilities and coordination rules are defined in `ai-tasks.md`.

## 2. Read first

1. `CLAUDE.md` — engineering instructions, architecture constraints, security rules, and prohibitions.
2. `ai-tasks.md` — roles, handovers, task templates, review process, the mandatory nine-part task briefing and owner actions (§13), the mandatory documentation, code-comment and explainability standard (§14), and Git guidance.
3. `current-state.md` — implementation status and supporting evidence.
4. `roadmap.md` — milestone order and acceptance criteria.
5. `architecture.md` — target architecture and module boundaries.
6. `decisions.md` — accepted decisions and open questions.

Read only the sections relevant to the assigned task.

## 3. Mandatory baseline check

Before every task:

- Confirm repository root and relevant file paths.
- Check branch, HEAD hash, and commit date.
- Check staged, unstaged, and untracked changes using `git status --short`.
- Inspect only relevant files and diffs.
- State the exact baseline hash.
- Stop if the baseline differs from the task brief or relevant uncommitted work may be affected.

Never overwrite or discard existing work.

## 4. Non-negotiable rules

- One implementation owner per task. Only one AI agent may modify a shared working tree at a time.
- OpenCode is the default primary implementation agent; Kilo is the alternative primary implementation agent when assigned. Both follow the same approved workflow and safeguards.
- ChatGPT Account 2 reviews independently and remains read-only. ChatGPT Account 1 coordinates and recommends acceptance; it does not modify repository files.
- **Approved implementation tasks (proposed standing automation, `decisions.md` D-017 — requires Project Owner acceptance):** the assigned agent may, within approved scope/baseline/authorized branch, inspect, implement, add/update focused tests, run relevant builds/tests/formatting/static analysis, fix implementation-related failures, generate/inspect EF Core migrations and SQL scripts when the model changes, apply migrations to a positively identified development database and verify when included in the task, and stage only approved files, commit after required validation/review, and push to the already-authorized branch. Report exact commands/results/failures/skips and migration/DB verification metadata (non-secret).
- **Always stop/protect:** unrelated or pre-existing changes must not be included; never expand scope; never claim unobserved results; never expose secrets.
- **Separate explicit Project Owner approval always required:** production DB changes; DB deletion/recreation/destructive reset; data deletion/irreversible transformations; unapproved rollback/downgrade; DB credential/infrastructure changes; uncertain-target or destructive operations; branch create/switch, pull, merge, rebase, force-push, reset, clean, amend, cherry-pick, revert/history rewriting; actions outside approved scope.
- **Until D-017 is accepted:** routine command-by-command approvals remain governed by existing accepted decisions (`D-007`, `D-008`) and current authoritative instructions.
- **Documentation-only tasks:** no builds/tests/migrations/scheduled checks unless specifically requested.
- Security-critical and architectural changes require explicit user approval and independent review.
- Every task must use the nine-part briefing and four owner actions in `ai-tasks.md` §13. Documentation and code comments must follow `ai-tasks.md` §14. Keep secrets out of files, logs, and reports. Refer to `commands.txt` for actual EF Core/migration command examples (placeholders only).

## 5. Architecture boundaries

Preserve the approved modular monolith architecture:

- Identity owns users, authentication, credentials, sessions, and tokens.
- Access owns roles, permissions, assignments, and authorization.
- Tenant must not depend directly on Identity.
- Standalone modules must not require Tenant infrastructure.
- Modules must not access another module's private DbContext, tables, or internals.
- Enforce server-side authorization and tenant isolation.
- Avoid circular dependencies and unnecessary refactoring.

Consult `architecture.md` and `decisions.md` before proposing boundary changes.

## 6. DeepSeek Harness

Unavailable until the Project Owner says otherwise.

## 7. OpenCode and Kilo (implementation agents)

Implementation responsibilities (when assigned as owner):

- Verify the approved baseline and working-tree state; stop on mismatch or unexpected/unrelated changes.
- Follow the task brief and approved scope; implement only assigned work.
- Add/update focused tests and run relevant validation (builds/tests/formatting/static analysis) as included/authorized; report exact commands, results, failures, and skipped tests.
- For approved model changes, handle EF Core migration generation/SQL review and, when included, apply/verify against a positively identified development database; report migration names and non-secret target metadata/verification.
- Stage only approved files, commit after required validation/review, and push to the already-authorized branch only when covered by approved standing automation (D-017, if accepted) or explicit approval; never include unrelated/pre-existing changes.
- Provide exact diffs and a complete, factual completion report. Do not claim unobserved results.

## 8. ChatGPT Account 2

Independent reviewer responsibilities:

- Inspect the actual focused implementation.
- Use the same baseline and acceptance criteria.
- Review security, architecture, correctness, compatibility, and test coverage.
- Report actionable findings with severity and file/line references.
- Do not edit or implement fixes.

## 9. Completion

Use only the approved project statuses:

- Completed (verified)
- Reported complete (not independently verified)
- In progress
- Pending
- Under review
- Blocked

Agent reports do not constitute final acceptance. The user performs manual verification and makes the final decision.

For full procedures, consult `ai-tasks.md` and `CLAUDE.md`.