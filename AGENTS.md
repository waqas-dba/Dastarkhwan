# AGENTS.md — Dastarkhwan Shared Agent Instructions

This file is the shared entry point for AI agents working in this repository. It summarizes the approved team workflow and points to the authoritative project documents.

Read the relevant instructions before acting. Do not assume another agent has already read these files.

## 1. Team roles

| Participant | Role |
|---|---|
| ChatGPT Account 1 | Project Manager and Architecture Coordinator |
| DeepSeek Harness | Preparation Engineer |
| OpenCode + Nemotron | Primary Developer |
| ChatGPT Account 2 | Independent Reviewer — read-only |
| User | Project Owner and final decision-maker |

Full responsibilities and coordination rules are defined in `ai-tasks.md`.

## 2. Read first

1. `CLAUDE.md` — engineering instructions, architecture constraints, security rules, and prohibitions.
2. `ai-tasks.md` — roles, handovers, task templates, review process, the mandatory nine-part task briefing and owner actions (§13), and Git guidance.
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
- OpenCode + Nemotron is the default primary implementation owner.
- DeepSeek Harness prepares plans and handovers; it implements only explicitly approved isolated, low-risk tasks.
- ChatGPT Account 2 reviews independently and remains read-only.
- ChatGPT Account 1 coordinates and recommends acceptance; it does not modify repository files.
- Only one AI agent may modify a shared working tree at a time.
- The user controls all Git write operations.
- Never stage, commit, push, pull, switch branches, merge, rebase, reset, cherry-pick, clean, amend, or rewrite history.
- Never run tests, builds, or migrations without explicit authorization for that specific action.
- Creating migration files requires explicit approval.
- Never expand task scope or modify unapproved files.
- Never claim unobserved results.
- Security-critical and architectural changes require explicit user approval and independent review.
- Every task must use the nine-part briefing and the four owner actions defined in `ai-tasks.md` §13 before implementation starts.
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

Default responsibilities:

- Read-only repository investigation.
- Dependency and error analysis.
- Technical planning and implementation handovers.
- Preparing focused task prompts and evidence.

Implementation is permitted only when the user explicitly approves a separate, isolated, low-risk task and names DeepSeek as its owner.

## 7. OpenCode + Nemotron

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