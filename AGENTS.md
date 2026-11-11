# AGENTS.md — Dastarkhwan Shared Agent Instructions

This file is the shared entry point for AI agents working in this repository. It summarizes the approved team workflow and points to the authoritative project documents.

Read the relevant instructions before acting. Do not assume another agent has already read these files.

## 1. Team roles

| Participant | Role |
|---|---|
| ChatGPT Account 1 | Project Manager and Architecture Coordinator |
| OpenCode | Primary implementation agent |
| ChatGPT Account 2 | Independent Reviewer — read-only |
| Kimi | Architecture discussion, research and critique |
| DeepSeek Web | Research and technical preparation |
| OpenHands | Repository exploration and planning; isolated implementation only when explicitly assigned and not concurrently editing with OpenCode |
| Claude | Limited reserve for difficult implementation or security/architecture review |
| Kilo | Unavailable until the Project Owner says otherwise |
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

- One implementation owner per task.
- OpenCode is the default primary implementation agent. OpenCode prepares technical plans and handovers and implements only explicitly approved tasks within the assigned scope.
- ChatGPT Account 2 reviews independently and remains read-only.
- ChatGPT Account 1 coordinates and recommends acceptance; it does not modify repository files.
- Only one AI agent may modify a shared working tree at a time.
- The user retains final authority over Git write operations. Agents may execute Git write operations only after explicit Project Owner approval.
- Git inspection commands are permitted without approval. Git operations that change repository state require explicit Project Owner approval before execution.
- Never run tests, builds, or migrations without explicit authorization for that specific action. Relevant builds and automated tests may be run after implementation when explicitly included in an approved implementation task; documentation-only tasks do not run builds or tests unless specifically requested.
- Creating migration files requires explicit approval.
- Never expand task scope or modify unapproved files.
- Never claim unobserved results.
- Security-critical and architectural changes require explicit user approval and independent review.
- Every task must use the nine-part briefing and the four owner actions defined in `ai-tasks.md` §13 before implementation starts.
- Documentation and code comments must follow `ai-tasks.md` §14: business meaning first, technical detail second, and no comment that merely restates code.
- Keep secrets out of files, logs, and reports.

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

## 7. OpenCode

Primary implementation responsibilities:

- Verify the approved baseline and working-tree state.
- Follow the task brief and approved scope.
- Implement only the assigned micro-task.
- Provide exact diffs and a concise completion report.
- Report tests as not run unless explicitly authorized.

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