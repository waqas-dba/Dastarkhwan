# Dastarkhwan / CoreKit — Engineering Instructions

## 1. Product scope
Build a secure, maintainable modular application for food-item delivery and parcel pickup/delivery. Pakistan is the first market; additional markets, including the UK, come later through configuration and replaceable integrations. Do not add hotel, dine-in, or unrelated verticals without an approved decision.

## 2. Project source of truth
- `roadmap.md` — milestone order and completion criteria.
- `architecture.md` — target architecture and module boundaries.
- `current-state.md` — verified implementation status and evidence.
- `decisions.md` — accepted decisions and open questions.
- `ai-tasks.md` — AI workflow, agent roles, task templates, and Git message guidance.
- `AGENTS.md` — short entry point for AI agents; summarizes the four-agent workflow and points to the files above. It is a summary, not a source of truth.

Do not duplicate full sections across files. If documents conflict, report the exact conflict and ask before changing security, persistence, product behavior, compatibility, or module boundaries. Code inspected at the latest repository baseline is evidence of implementation; architecture documents describe intended design unless marked implemented.

## 3. Mandatory pre-task repository check
Before starting **every** task, including documentation-only tasks:
1. Verify repository root and the actual paths of the relevant files.
2. Read the current HEAD hash and commit date (`git rev-parse HEAD`, `git show -s --format=%H%n%cI HEAD`).
3. Check staged, unstaged, and untracked changes (`git status --short`).
4. Inspect only the files and diffs relevant to the assigned task. Check changes since the supplied baseline only when needed.
5. State the exact HEAD hash used as the task baseline and whether relevant local changes exist.
6. If the baseline differs from the task brief, or relevant uncommitted changes could be overwritten or conflict, stop and ask the user how to proceed. Never discard, overwrite, or silently absorb existing changes.

The M0 report references commit `9555b8c` dated 2026-10-23. This is a **historical verified baseline**, not a claim that it remains the current HEAD. Always check the actual HEAD before work. Do not scan all Git history or the whole solution unless the task requires it.

Read-only Git inspection is allowed. Git write operations are prohibited; see Section 10.

## 4. Token efficiency — strict
Token and context conservation are priorities.
- One request should have one small, measurable outcome.
- Read only the relevant documentation sections and source files; do not dump or summarize the whole repository.
- Use existing task reports and `current-state.md`; do not repeat completed investigations unless code has changed.
- Do not repeat project context already present in these files.
- Load only the skill, agent, or specialist needed for the current slice.
- Avoid speculative alternatives, unnecessary web research, broad audits, premature abstractions, and verbose explanations.
- Do not investigate future milestones while completing the current slice.
- Do not generate extra documentation or refactor unrelated code.
- Stop as soon as the assigned outcome and acceptance criteria are complete.
- Keep plans, progress reports, and completion reports concise. If a blocker exists, state only the blocker and required decision.

## 5. Micro-milestone execution
For every non-trivial task:
1. Verify the latest code and working tree as in Section 3.
2. Read only relevant files.
3. Provide a short plan: goal, files expected to change, and acceptance criteria.
4. Wait for user approval before editing or executing commands that change state.
5. Implement only the approved micro-slice; do not bundle unrelated tasks.
6. Report changed files, outcome, tests added/updated but not run, risks, and one next suggested slice.
7. Stop and wait. Do not automatically continue to the next slice.

A micro-slice should normally change one concern and have a clear review boundary. Split further if it touches multiple module boundaries, persistence changes, or unrelated behaviors. No task may quietly expand its scope.

## 6. Multi-agent responsibilities

Four AI agents plus the user participate. Full role definitions and coordination rules are in `ai-tasks.md` §1 and §5; do not restate them elsewhere.

| Agent | Responsibility |
|---|---|
| **ChatGPT — Project Manager** | Coordinates the project, defines tasks and acceptance criteria, oversees architecture, reviews agent reports, and recommends acceptance. Does not modify repository files or perform Git writes. |
| **DeepSeek Harness — Preparation Engineer** | Researches and explores the repository, prepares implementation plans and technical handovers, investigates errors and dependencies, and performs only explicitly approved, isolated, low-risk implementation slices. Does not replace Claude 1 or Claude 2. |
| **Claude Account 1 — Primary Developer** | Owns primary implementation; implements approved changes and provides exact diffs and completion reports. |
| **Claude Account 2 — Independent Reviewer** | Independent read-only review of actual code, architecture, and security against the task brief and focused diff/report. Does not modify implementation files. |
| **User — Project Owner** | Controls architecture approval, scope changes, Git operations, manual testing, and release acceptance. |

- One implementation owner per task. Only one AI agent may modify a shared working tree at a time; every other agent uses a read-only copy or a supplied diff/report.
- DeepSeek prepares the task and handover before Claude 1 implements when prior investigation is useful. Claude 2 reviews independently. No agent may silently expand scope.
- Every participating agent verifies and states the same baseline commit. Do not assume agents share chat history or files.
- Preserve existing uncommitted work. Security-critical and architectural changes require explicit user approval and independent review.
- Resolve review findings before starting the next dependent slice. The user decides which findings to accept.
- Do not use multiple agents to duplicate work or bypass service limits or terms.

## 7. Backend-first development
Initial development priority is backend only:
1. CoreKit module boundaries and SharedKernel contracts.
2. Identity authentication, credentials, sessions, and tokens.
3. Access roles, permissions, and authorization.
4. Tenant lifecycle, context, and tenant isolation.
5. Backend APIs and application use cases for food delivery and parcel pickup.

Do not start frontend/UI work, marketplace features, or later business workflows until their roadmap dependencies and decisions are ready.

## 8. Architecture and implementation rules
- Prefer a modular monolith; do not introduce microservices or distributed infrastructure without an evidenced need.
- Each module owns its business rules and persistence. Cross modules only through explicit public contracts or events.
- Never access another module's private `DbContext`, tables, repositories, or internals.
- Avoid circular dependencies and hypothetical generic frameworks.
- **Identity** owns accounts, authentication, credentials, sessions/tokens, and account recovery.
- **Access** owns roles, permissions, assignments, and authorization policy/enforcement.
- Tenant must not depend directly on Identity; use an approved stable contract/abstraction at the appropriate boundary.
- Keep endpoints thin; business rules belong to the owning application/domain layer.
- Preserve existing behavior and compatibility where practical. Explain any required breaking change before implementation.
- Do not assume planned architecture is already implemented; verify source and tests.

## 9. Security, data, and quality
- Authorize every protected server operation. Fail closed when identity, tenant, store, permission, or resource ownership is unclear.
- Never trust client-supplied user, tenant, store, price, discount, or state values without server-side validation.
- Apply tenant/store scope to reads, writes, exports, reports, jobs, callbacks, and admin paths.
- Hostnames and request headers are routing/context inputs, not authorization by themselves.
- For the approved shared-database strategy, tenant-owned rows must carry explicit tenant ownership; EF Core global query filters are defense-in-depth, not the only security boundary. Enforce scope in application services/authorization and test cross-tenant denial.
- Keep secrets out of source control and logs. Do not print tokens, passwords, signing keys, connection strings, or secret configuration values.
- Use parameterized data access, validation, safe errors, appropriate constraints, short transactions, idempotency for retries/callbacks, and explicit currency/decimal handling for money.
- Add focused positive and negative tests for relevant changes, especially authorization and cross-tenant access.
- AI may create or update tests but **must not run tests or builds unless the user explicitly authorizes that specific action**. The user normally runs verification manually.
- Do not execute database migrations or modify a database unless explicitly authorized. Creating migration files also requires explicit approval for that slice.
- Never claim a test, build, migration, or deployment succeeded unless its result was actually observed and authorized.

## 10. Git and commit-message rules
The user alone controls Git write operations. AI must never stage, commit, push, pull, checkout/switch branches, merge, rebase, reset, cherry-pick, clean, amend, create branches, or rewrite history. Do not change Git configuration or hooks.

After a coherent slice is complete and reviewed, AI may **suggest** a manual checkpoint and prepare a commit message. The user decides whether and when to commit. Use the format and examples in `ai-tasks.md`. Do not suggest a commit for incomplete work, unreviewed changes, or unresolved critical security/data issues. Do not claim that a commit was created.

## 11. Completion report
Keep the report short and factual:
1. Baseline HEAD hash and working-tree status.
2. Files changed and the outcome.
3. Tests added/updated (not run), or tests explicitly authorized and their observed result.
4. Security/data/compatibility considerations and unresolved risks.
5. Manual verification steps for the user.
6. One next suggested micro-slice.
7. Optional user-run Git checkpoint and suggested commit subject/body.
