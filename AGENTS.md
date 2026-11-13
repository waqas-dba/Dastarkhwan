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
- **Approved implementation tasks (standing automation, `decisions.md` D-017/D-007/D-008):** the assigned agent may, within approved scope/baseline/authorized branch, inspect, implement, add/update focused tests, run relevant builds/tests/formatting/static analysis, fix implementation-related failures, generate/inspect EF Core migrations and SQL scripts when the model changes, apply migrations to a positively identified development database and verify when included in the task, and stage only approved files, commit after required validation/review, and push to the already-authorized branch. Report exact commands/results/failures/skips and migration/DB verification metadata (non-secret).
- **Always stop/protect:** unrelated or pre-existing changes must not be included; never expand scope; never claim unobserved results; never expose secrets.
- **Separate explicit Project Owner approval always required:** production DB changes; DB deletion/recreation/destructive reset; data deletion/irreversible transformations; unapproved rollback/downgrade; DB credential/infrastructure changes; uncertain-target or destructive operations; branch create/switch, pull, merge, rebase, force-push, reset, clean, amend, cherry-pick, revert/history rewriting; actions outside approved scope.
- **Documentation-only tasks:** no builds/tests/migrations/scheduled checks unless specifically requested.
- Security-critical and architectural changes require explicit user approval and independent review.
- Every task must use the nine-part briefing and four owner actions in `ai-tasks.md` §13. Documentation and code comments must follow `ai-tasks.md` §14. Keep secrets out of files, logs, and reports. Refer to `commands.txt` for actual EF Core/migration command examples (placeholders only).

## 5. Architecture boundaries

Preserve the approved modular monolith architecture and module ownership/contract rules in `architecture.md` and `decisions.md` (Identity/Access separation, Tenant isolation, no cross-module private DbContext/table access, server-side authorization, no circular dependencies).

## 6. OpenCode and Kilo (implementation agents)

When assigned as implementation owner, verify baseline/working-tree state, follow approved scope, perform authorized implementation/validation/EF Core dev-DB steps as applicable, and deliver exact diffs with a complete factual report per `ai-tasks.md` and `CLAUDE.md`. Stop on mismatch, unexpected/unrelated changes, or protected operations requiring separate approval.

## 7. ChatGPT Account 2

Performs independent read-only review against the same baseline and acceptance criteria, reporting actionable findings with severity and file/line references. Does not edit or implement fixes.

## 8. Completion

Use only approved project statuses (Completed verified, Reported complete not independently verified, In progress, Pending, Under review, Blocked). Agent reports do not constitute final acceptance; the Project Owner makes the final decision.

For full procedures, consult `ai-tasks.md` and `CLAUDE.md`.