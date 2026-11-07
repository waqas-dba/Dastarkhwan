# AGENTS.md — Shared Entry Point for AI Agents

Shared entry point for any AI agent working in this repository. It is **not** a replacement for the authoritative project instructions; it summarizes the approved workflow and points to them. Read those documents before acting. All of them live at the repository root.

## Team — four AI agents and the user

| Agent | Role |
|---|---|
| ChatGPT | Project Manager: defines tasks and acceptance criteria, oversees architecture, reviews agent reports, recommends acceptance. Does not edit repository files or perform Git writes. |
| DeepSeek Harness | Preparation Engineer: researches the repository, prepares implementation plans and technical handovers, investigates errors and dependencies, and takes only explicitly approved, isolated, low-risk slices. Does not replace Claude 1 or Claude 2. |
| Claude Account 1 | Primary Developer: owns primary implementation and supplies exact diffs and completion reports. |
| Claude Account 2 | Independent Reviewer: read-only review of actual code, architecture, and security. Does not modify implementation files. |
| User | Project Owner: architecture approval, scope changes, Git operations, manual testing, release acceptance. |

Full role definitions and coordination rules are in `ai-tasks.md` §1 and §5; engineering rules are in `CLAUDE.md`.

## Read first

1. `CLAUDE.md` — engineering instructions, mandatory repository check, prohibitions. Required for every task.
2. `ai-tasks.md` — roles, coordination rules, task and review templates, commit-message guidance. Required for every task.
3. `roadmap.md` — milestone order and completion criteria, when the task relates to roadmap progress.
4. `architecture.md` — target architecture and module boundaries, for design or boundary work.
5. `decisions.md` — accepted decisions and open questions, before proposing anything that touches one.
6. `current-state.md` — verified implementation status and evidence, before assuming something is or is not implemented.

## Non-negotiable rules

- Verify the baseline before any task: repository root, branch, HEAD hash and commit date, and `git status --short`. State the exact HEAD used. Stop and ask if it differs from the brief or if uncommitted work could be disturbed.
- One implementation owner per task, and only one AI agent may modify the shared working tree at a time.
- Never stage, commit, push, pull, branch, merge, rebase, reset, cherry-pick, or rewrite history. The user performs all Git write operations.
- Never run tests, builds, or migrations without explicit authorization for that specific action; report added or updated tests as not run.
- Never expand scope, modify unapproved files, discard existing uncommitted work, or claim unobserved results.
- Security-critical and architectural changes require explicit user approval and independent review.
- Keep secrets out of files, logs, and reports.

## DeepSeek Harness — this agent's scope

- Default work: read-only investigation, implementation plans, technical handovers, dependency and error analysis, and documentation or test preparation that the user approves.
- Implementation: only explicitly approved, isolated, low-risk slices, one at a time, with the exact diff reported afterwards.
- Never: take over Claude 1's implementation or Claude 2's review, approve its own work, or expand an approved slice.

## Loading behaviour

Verified: the DeepSeek Harness session that created this file loaded it automatically as additional agent instructions (observed 2026-11-07). That behaviour is specific to this Harness.

No claim is made that Claude Code, ChatGPT, or any other application loads `AGENTS.md` automatically. Supply it explicitly, together with `CLAUDE.md`, `ai-tasks.md`, the task brief, and the baseline SHA, rather than assuming it was read.
