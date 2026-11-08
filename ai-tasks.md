# AI Task Workflow — Dastarkhwan

## 1. Team roles

| Participant | Responsibility | Must avoid |
|---|---|---|
| ChatGPT Account 1 — Project Manager | Coordinates the project, defines tasks and acceptance criteria, maintains roadmap priorities, oversees architecture, evaluates reports, and recommends acceptance. | Editing repository files, Git writes, or independently approving architecture changes. |
| DeepSeek Harness — Preparation Engineer | Researches the repository, investigates issues, prepares technical plans, implementation instructions, and handovers. May implement only explicitly approved isolated, low-risk tasks. | Taking over the primary implementation or independent review role. |
| OpenCode + Nemotron — Primary Developer | Owns primary implementation for approved tasks, makes the scoped changes, and supplies exact diffs and completion reports. | Scope expansion, unrelated refactoring, unauthorized commands, or Git writes. |
| ChatGPT Account 2 — Independent Reviewer | Independently reviews actual implementation, architecture, security, compatibility, and acceptance criteria. Read-only. | Editing implementation files, implementing fixes, or relying only on the developer's report. |
| User — Project Owner | Approves scope and architecture, controls Git operations, performs manual testing, and makes final acceptance decisions. | Treating an unverified report as proof of completion. |

The user is the final authority. AI agents coordinate and recommend; they do not independently approve architecture changes or releases.

## 2. Source of truth

Use the following repository documents:

- `CLAUDE.md` — engineering rules and restrictions.
- `AGENTS.md` — short entry point for all agents.
- `roadmap.md` — milestones and priorities.
- `current-state.md` — implementation status and evidence.
- `architecture.md` — target architecture and module boundaries.
- `decisions.md` — accepted decisions and open questions.
- `ai-tasks.md` — agent workflow and task templates.

Actual repository code is the strongest evidence of implemented behavior. Approved architecture decisions define intended boundaries. Documentation and agent reports must not override verified code.

If documents conflict, report the exact conflict and request direction before changing architecture, security, persistence, compatibility, or product behavior.

## 3. Mandatory repository verification

Before every task, each participating agent must:

1. Confirm repository root and relevant file paths.
2. Read current HEAD hash and commit date.
3. Check staged, unstaged, and untracked changes with `git status --short`.
4. Inspect only relevant files and diffs.
5. State the exact baseline hash used.
6. Stop if the baseline differs from the task brief or relevant uncommitted work could be affected.

Do not discard, overwrite, or silently absorb existing work.

The recorded M0 baseline is `9555b8c`, dated 2026-10-23. It is historical and must not be assumed to be the current HEAD.

Git inspection must remain read-only.

## 4. Token and context budget

- One task should have one small, measurable outcome.
- Read only the files required for that outcome.
- Do not dump the whole repository or repeat existing project context.
- Reuse verified reports when the relevant source has not changed.
- Inspect changes since the previous baseline where needed.
- Do not conduct broad audits or speculative research for a focused task.
- Do not investigate later milestones prematurely.
- Do not create additional documentation unless requested or required by the approved task.
- Keep plans, prompts, and reports concise.
- Stop when the acceptance criteria are met.

## 5. Task coordination and handover

### Standard workflow

1. **ChatGPT Account 1 — Define**
   - Identify the milestone and task.
   - Check prior decisions and dependencies.
   - Define one implementation owner.
   - Specify baseline, scope, approved files, exclusions, and acceptance criteria.
   - Prepare the task prompt.

2. **DeepSeek Harness — Prepare**
   - Inspect the relevant repository areas.
   - Investigate dependencies, existing patterns, and risks.
   - Prepare a concise technical plan and handover for the implementation owner.
   - Do not implement the same task unless explicitly assigned a separate implementation slice.

3. **OpenCode + Nemotron — Implement**
   - Verify the same baseline and local working-tree state.
   - Follow the approved task and DeepSeek handover.
   - Modify only approved files.
   - Provide exact diffs and a concise completion report.
   - Do not run tests, builds, or migrations without specific user authorization.

4. **ChatGPT Account 2 — Review**
   - Independently inspect the actual implementation and focused diff.
   - Use the same baseline and task acceptance criteria.
   - Review security, architecture, correctness, compatibility, and tests.
   - Remain read-only.
   - Report actionable findings with severity and file/line references.

5. **OpenCode + Nemotron — Correct**
   - Address only approved review findings in a separate micro-task.
   - Provide a new diff and report.
   - Do not expand the original scope.

6. **ChatGPT Account 1 — Recommend**
   - Evaluate implementation and independent review evidence.
   - Record remaining risks and user actions.
   - Recommend acceptance or further work.

7. **User — Verify and decide**
   - Run manual tests and any approved verification.
   - Perform all Git write operations.
   - Approve or reject the task's final acceptance.

### Coordination rules

- One implementation owner per task.
- OpenCode + Nemotron is the default primary implementation owner.
- DeepSeek may implement only an explicitly approved, isolated, low-risk task with its own named owner.
- ChatGPT Account 2 is always read-only.
- ChatGPT Account 1 coordinates and recommends; it does not modify the repository.
- Only one AI agent may modify a shared working tree at a time.
- Other agents must use read-only access or a supplied diff/report.
- Every agent must verify its own baseline.
- Never assume agents share chat history, local files, or working-tree state.
- Stop on baseline mismatch, unexpected changes, access errors, or security-critical findings.
- Do not duplicate work or use agents to bypass service limits or terms.

## 6. Micro-task rules

A micro-task must have one primary concern, a small expected file set, and observable acceptance criteria.

For every task:

1. Verify the repository baseline and working tree.
2. Inspect only relevant files.
3. Provide a short plan with goal, expected files, and acceptance criteria.
4. Wait for user approval before editing or executing state-changing commands.
5. Implement only the approved scope.
6. Report changed files, outcome, tests added/updated but not run, risks, and one suggested next task.
7. Stop and wait for the user.

Split tasks that touch multiple module boundaries, persistence models, or unrelated behaviors.

## 7. Permission boundaries

AI agents may inspect Git read-only.

AI agents must not:

- Stage, commit, or push.
- Pull or change branches.
- Merge, rebase, reset, cherry-pick, clean, amend, or rewrite history.
- Change Git configuration or hooks.
- Run tests or builds without explicit authorization for that specific run.
- Apply migrations or modify databases without explicit authorization.
- Create migration files without explicit approval for that task.
- Discard or overwrite existing uncommitted work.
- Expose passwords, tokens, API keys, connection strings, or other secrets.

AI may add or update test files, but must report them as not run unless the user explicitly authorized and the result was observed.

## 8. Implementation task template

Before using this template, complete the mandatory briefing standard in §13, including the four owner actions.

```markdown
# Micro-task: [one measurable outcome]

## Baseline
- Repository HEAD: [hash and date]
- Working tree: [clean / relevant local changes]

## Owner
- Implementation owner: [OpenCode + Nemotron / DeepSeek Harness]
- Preparation: [DeepSeek Harness, if applicable]
- Reviewer: ChatGPT Account 2
- Coordinator: ChatGPT Account 1

## Goal
[One observable outcome.]

## Read
[Exact files or relevant sections.]

## Scope
- Owning module: [module]
- Expected files: [paths, if known]

## Acceptance criteria
- [ ] [Observable result]
- [ ] [Security/data/compatibility condition]
- [ ] Focused tests added or updated if applicable (not run)
- [ ] Relevant documentation/status updated if approved

## Out of scope
- Other roadmap slices.
- Unrelated refactoring.
- Unapproved architecture or persistence changes.
- Git write operations.

## Constraints
- Follow CLAUDE.md and accepted decisions.
- Inspect existing implementation before changing it.
- Keep the smallest compatible change.
- Do not run tests/builds/migrations without explicit authorization.

## Completion report
- Baseline hash and working-tree status:
- Files changed:
- Outcome:
- Tests added/updated: [not run unless authorized]
- Risks and manual verification:
- Next suggested micro-task:
- Suggested manual Git checkpoint/message (optional):
```

## 9. Independent review template

```markdown
Review this focused implementation against the approved task,
CLAUDE.md, architecture.md, and accepted decisions.

Baseline commit: [hash]

Review role: ChatGPT Account 2 — Independent Reviewer.

Read-only restrictions:
- Do not edit files.
- Do not implement fixes.
- Do not run tests/builds.
- Do not perform Git write operations.

Inspect the actual implementation, focused diff, and relevant surrounding code.

Report actionable findings in severity order. For each finding provide:
- Severity: Critical / High / Medium / Low
- File and line/reference
- Concrete failure scenario
- Smallest suggested correction

Check:
- Acceptance criteria and requirement gaps.
- Authentication and authorization.
- Tenant/store isolation and fail-closed behavior.
- Validation and resource ownership.
- Transactions and idempotency where relevant.
- Module boundaries and dependency direction.
- Persistence and migration compatibility.
- Error handling and security-sensitive logging.
- Focused test coverage.

Separate confirmed defects from questions or speculation.
If no material findings exist, say so briefly.
```

## 10. Completion and review statuses

Use only these statuses:

- **Completed (verified)**
- **Reported complete (not independently verified)**
- **In progress**
- **Pending**
- **Under review**
- **Blocked**

Never mark a task completed and verified based only on an agent's report.

An implementation report is not independent verification. A reviewer report is not user manual verification. The user makes the final acceptance decision.

## 11. Git commit message guidance

The user performs all Git write operations. AI may suggest a commit subject and body only after the implementation is complete, review findings are resolved, and required user verification has been performed.

Use:

`<type>(<scope>): <imperative summary>`

Suggested types:

- `docs` — documentation only.
- `feat` — new behavior.
- `fix` — defect correction.
- `refactor` — behavior-preserving restructuring.
- `test` — test-only changes.
- `build` — build/package/dependency configuration.
- `security` — security hardening, if consistent with repository convention.

Keep subjects concise and specific. Explain what changed and why in the body. Mention migration compatibility or required manual actions where relevant. Never include secrets.

Do not recommend a commit while critical review findings remain or verification is incomplete.

## 12. Final authority

ChatGPT Account 1 coordinates and recommends.
DeepSeek Harness prepares and investigates.
OpenCode + Nemotron implements approved tasks.
ChatGPT Account 2 independently reviews.
The user owns scope, architecture approval, Git operations, manual testing, and final acceptance.

## 13. Mandatory task briefing standard

Every task brief, handover, review request and implementation instruction must use the same
nine-part structure and must end with the four owner actions. A brief that omits any part is
incomplete and must not be implemented.

### Required sections, in this order

1. **Task ID and name** — one unique identifier plus a descriptive name.
2. **Purpose** — why the work exists; the risk or gap it removes.
3. **Current issue** — the observed problem, with evidence and current status.
4. **Proposed changes** — what will change, described as behavior and artifacts.
5. **Expected result** — the observable outcome once the change is accepted.
6. **Files and scope** — the only files authorized to change, and what is explicitly out of scope.
7. **Responsible agents** — preparation, implementation, independent review, coordination, final authority.
8. **Acceptance criteria** — verifiable conditions; never mark one satisfied without evidence.
9. **Risks and dependencies** — known risks, blockers and required decisions.

### Required owner actions

Every brief must end with these four items, written concretely:

- **Your decision** — what the Project Owner must approve or choose before work starts.
- **Your action** — what only the owner can do (Git operations, environment or approvals).
- **Your verification** — what the owner must check manually.
- **Your acceptance** — what closes the task.

### Rules

- No implementation starts before the owner approves the briefing.
- The task ID appears in the handover, the implementation report and the review report.
- "No action required" is acceptable only when it is true and the reason is stated.
- Briefs may be short, but no section may be omitted for small tasks.
- This standard is the canonical one; other documents reference it and do not restate it.

### Copy-ready template

```markdown
# [Task ID] — [name]

## 1. Task ID and name
## 2. Purpose
## 3. Current issue
## 4. Proposed changes
## 5. Expected result
## 6. Files and scope
## 7. Responsible agents
- Preparation: DeepSeek Harness
- Implementation: OpenCode + Nemotron
- Independent review: ChatGPT Account 2 (read-only)
- Coordination: ChatGPT Account 1
- Final authority: User
## 8. Acceptance criteria
## 9. Risks and dependencies

## What you need to do
**Your decision:**
**Your action:**
**Your verification:**
**Your acceptance:**
```