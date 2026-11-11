# AI Task Workflow — Dastarkhwan

## 1. Team roles

| Participant | Responsibility | Must avoid |
|---|---|---|
| ChatGPT Account 1 — Project Manager | Coordinates the project, defines tasks and acceptance criteria, maintains roadmap priorities, oversees architecture, evaluates reports, and recommends acceptance. | Editing repository files, Git writes, or independently approving architecture changes. |
| OpenCode — Primary implementation agent | Owns primary implementation for approved tasks, makes the scoped changes, and supplies exact diffs and completion reports. | Scope expansion, unrelated refactoring, unauthorized commands, or Git writes without explicit approval. |
| ChatGPT Account 2 — Independent Reviewer | Independently reviews actual implementation, architecture, security, compatibility, and acceptance criteria. Read-only. | Editing implementation files, implementing fixes, or relying only on the developer's report. |
| OpenHands — Repository exploration and planning | Repository exploration, planning, and separately assigned isolated implementation. Read-only unless explicitly assigned a separate task. | Editing files outside an explicitly assigned scope; modifying the same shared working tree as OpenCode simultaneously. |
| Kimi — Architecture discussion and critique | Research, architecture discussion and critique. | Implementation or repository modification. |
| DeepSeek Web — Research and technical preparation | Research and technical preparation only. Not DeepSeek Harness. Do not assign implementation work by implication. | Implementation work or repository modification. |
| Claude — Difficult implementation reserve | Difficult implementation and security/architecture review reserve; limited usage. | Repository modification outside an explicitly assigned scope. |
| Kilo — Unavailable | Do not assign until the Project Owner confirms availability. | Any repository work. |
| DeepSeek Harness — Unavailable | Do not assign until the Project Owner confirms availability. | Any repository work. |
| User — Project Owner | Approves scope and architecture, controls Git operation approval, performs manual testing, and makes final acceptance decisions. | Treating an unverified report as proof of completion. |

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

2. **OpenCode — Prepare**
   - Inspect the relevant repository areas.
   - Investigate dependencies, existing patterns, and risks.
   - Prepare a concise technical plan and handover for the implementation owner.
   - Do not implement the same task unless explicitly assigned a separate implementation slice.

3. **OpenCode — Implement**
   - Verify the same baseline and local working-tree state.
   - Follow the approved task.
   - Modify only approved files.
   - Provide exact diffs and a concise completion report.
   - Relevant builds and automated tests may be run after implementation when explicitly included in the approved task; report exact commands, results, failures and skipped tests. Documentation-only tasks do not run builds or tests unless specifically requested.

4. **ChatGPT Account 2 — Review**
   - Independently inspect the actual implementation and focused diff.
   - Use the same baseline and task acceptance criteria.
   - Review security, architecture, correctness, compatibility, and tests.
   - Remain read-only.
   - Report actionable findings with severity and file/line references.

5. **OpenCode — Correct**
   - Address only approved review findings in a separate micro-task.
   - Provide a new diff and report.
   - Do not expand the original scope.

6. **ChatGPT Account 1 — Recommend**
   - Evaluate implementation and independent review evidence.
   - Record remaining risks and user actions.
   - Recommend acceptance or further work.

7. **User — Verify and decide**
   - Run manual tests and any approved verification.
   - Retains final authority over Git write operations; agents execute Git write operations only after explicit Project Owner approval.
   - Approve or reject the task's final acceptance.

### Coordination rules

- One implementation owner per task.
- OpenCode is the default primary implementation owner.
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

AI agents may inspect Git read-only (`git status`, `git log`, `git diff`, branch listing). Git operations that change repository state require explicit Project Owner approval before execution; before requesting approval the agent must explain purpose, exact command, benefits, risks and consequences, and whether branches, commits or remote repositories will be affected. Asking for approval does not authorize execution.

AI agents must not:

- Stage, commit, or push without explicit Project Owner approval.
- Pull or change branches without explicit Project Owner approval.
- Merge, rebase, reset, cherry-pick, clean, amend, or rewrite history without explicit Project Owner approval.
- Change Git configuration or hooks without explicit Project Owner approval.
- Apply migrations or modify databases without explicit authorization.
- Create migration files without explicit approval for that task.
- Discard or overwrite existing uncommitted work.
- Expose passwords, tokens, API keys, connection strings, or other secrets.

Relevant builds and automated tests may be run after implementation when explicitly included in the approved task; the agent must report exact commands, results, failures and skipped tests. Broader test suites require justification and approval where appropriate. Documentation-only tasks do not run builds or tests unless specifically requested. Do not fix unrelated failures without approval.

AI may add or update test files, but must report them as not run unless the user explicitly authorized and the result was observed.

## 8. Implementation task template

Before using this template, complete the mandatory briefing standard in §13, including the four owner actions.

```markdown
# Micro-task: [one measurable outcome]

## Baseline
- Repository HEAD: [hash and date]
- Working tree: [clean / relevant local changes]

## Owner
- Implementation owner: [OpenCode]
- Preparation: [OpenCode, if applicable]
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
- Relevant builds and automated tests may be run after implementation when explicitly included in the approved task; documentation-only tasks do not run builds or tests unless specifically requested. Do not run builds, tests, or migrations without explicit authorization where the task does not include them.

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

The user retains final authority over Git write operations. Agents may execute Git write operations only after explicit Project Owner approval. AI may suggest a commit subject and body only after the implementation is complete, review findings are resolved, and required user verification has been performed.

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
OpenCode prepares and implements approved tasks.
ChatGPT Account 2 independently reviews.
The user owns scope, architecture approval, manual testing, and final acceptance. The user retains final authority over Git write operations; agents execute Git write operations only after explicit Project Owner approval.

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
- Documentation produced under a brief must satisfy the standard in §14.

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
- Preparation: [OpenCode or designated preparer]
- Implementation: [OpenCode]
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

## 14. Documentation, code comments and explainability standard

Applies to every module, implementation, task brief, handover, review request, API/usage document, configuration note and code comment. Required documentation is part of acceptance, not optional.

### 14.1 Two audiences

Every relevant document must serve both readers:

- **Project Owner (non-technical):** understands the business purpose, may not know programming terms.
- **Developer:** needs implementation detail, dependencies, configuration and usage.

Explain business meaning first, technical detail second. Do not assume the reader knows programming, frameworks, databases, APIs or architecture. Explain a technical term in simple words the first time it is used.

### 14.2 Explain each module or significant feature in plain language

Cover all ten points:

1. What it is.
2. Why the project needs it.
3. What problem it solves.
4. How it works, step by step.
5. Who or what uses it.
6. What information it receives.
7. What result it produces.
8. How it connects to other modules.
9. How to configure and use it.
10. What happens when something goes wrong.

Add practical examples wherever they improve understanding.

### 14.3 Developer detail

Where applicable, document: module and project responsibilities; important classes, interfaces and services; public methods and their purpose; API endpoints with HTTP method and request/response examples; database entities, relationships and ownership; configuration settings and environment variables; authentication, authorization and tenant-scope requirements; dependencies and module integration; error handling and expected failure behaviour; installation/setup/usage steps; and testing instructions for the human operator. Never document secrets or real sensitive values.

### 14.4 Code comments

Comments explain reasoning, especially: complex business rules; security and authorization checks; tenant isolation; non-obvious validation; important dependencies; workarounds and compatibility requirements; unexpected behaviour and failure handling.

Prefer meaningful names and simple code over comments. Do not restate the code. Document public APIs and non-obvious methods well enough for another developer to use them correctly.

### 14.5 Examples

Where useful, provide realistic business scenarios; example requests and responses; configuration examples with safe placeholder values; step-by-step usage; expected results; common errors and troubleshooting. Clearly separate illustrative examples from verified implementation behaviour.

### 14.6 Accuracy and maintenance

Describe the actual implementation, never an assumed or planned one. Label planned, implemented, verified and unverified behaviour using the statuses in §10. Update documentation when approved code changes alter behaviour. Correct outdated instructions only when authorized. Keep one authoritative explanation and link to it instead of duplicating. Never claim a feature works without implementation evidence and appropriate verification.

### 14.7 Documentation acceptance

A task is incomplete if required documentation is missing or materially misleading. The Project Owner must be able to understand purpose, workflow and practical usage without reading source; a developer must be able to understand responsibilities, integration and usage without guessing. Exceptions require explicit Project Owner approval recorded in the task brief.

### 14.8 Scope of application

This standard governs: task briefs, handovers and reviews; module overview documents; endpoint and API documentation; configuration documentation; and code comments. It does not require retrofitting every existing document at once; a documentation audit can be a separately approved task.

## 15. Guided Development, Learning and Approval Policy

These six sections are mandatory project-wide rules for all participating development agents.

### 15.1 Development and Testing

- The implementation agent may inspect the repository and approved files.
- It may run relevant builds and automated tests after implementation when these are included in an approved implementation task.
- It must report exact commands, results, failures and skipped tests.
- Broader test suites may be run when justified.
- Do not fix unrelated failures without approval.
- Documentation-only tasks must not run builds or tests unless specifically requested.

### 15.2 Git Operations — Explicit Approval Required

Before performing any Git operation that changes repository state, the agent must:

1. Explain the purpose.
2. Show the exact command.
3. Explain benefits, risks and consequences.
4. Explain whether it affects branches, commits or remote repositories.
5. Obtain explicit Project Owner approval.

This applies to:

- Staging.
- Committing.
- Pushing.
- Branch creation or switching.
- Merging.
- Rebasing.
- Cherry-picking.
- Resetting.
- Cleaning.
- Reverting.
- History rewriting.

Read-only inspection commands such as `git status`, `git log`, `git diff` and branch listing are permitted.

Asking for approval is not authorization. Wait for explicit approval before executing the operation.

### 15.3 Database and Destructive Operations

Before migrations, database updates, schema changes, data deletion or destructive operations:

- Explain the purpose.
- Identify expected changes.
- Identify affected databases, tables and records.
- Explain backup and rollback arrangements.
- Present practical alternatives with advantages and disadvantages.
- Recommend the safest approach.
- Wait for explicit Project Owner approval.

Never assume approval from an earlier discussion or another approved task.

### 15.4 Guided Learning and Decision Support

The implementation agent must help the Project Owner understand technical work.

For every important decision or approval request, explain:

1. What decision is required.
2. The technical background in plain language.
3. Why the decision is necessary.
4. Practical options.
5. Advantages and disadvantages of each option.
6. Security, architecture, performance, maintenance and future impacts where relevant.
7. The recommended option and justification.
8. The exact approval or decision requested.

Avoid unexplained jargon. Use practical Dastarkhwan examples.

Do not merely ask the Project Owner to approve a technical change without explaining what it means and why it matters.

### 15.5 Approval Boundaries

- Never assume approval from silence.
- Never assume approval from a previous decision unless it explicitly covers the current action.
- Do not implement unapproved architectural changes.
- Do not expand task scope without approval.
- Stop and ask when unexpected risks, dependencies or baseline mismatches arise.
- Architecture approval does not automatically authorize implementation, database operations or Git operations.
- The Project Owner retains final authority.

### 15.6 Completion and Learning Report

At task completion, report:

- What changed and why.
- Files modified.
- Builds and tests executed with actual results.
- Important technical concepts learned.
- Decisions made and approvals received.
- Remaining risks and outstanding work.
- Suggested next step.

Do not claim tests passed unless they were actually executed and their results are available.

## 16. Teaching, Knowledge Transfer and Code Commenting Standards

These are permanent project-wide standards for implementation, review and documentation tasks.

### 16.1 Teach the Project Owner Throughout Development

Act as both a developer and a technical instructor.

Do not simply make changes and provide a completion report.

Whenever an important or unfamiliar concept is introduced, explain:

1. **What it is:** Explain in simple language.
2. **How it works:** Describe its operation in Dastarkhwan.
3. **Why it is needed:** Explain the problem it solves.
4. **Where it is used:** Identify the relevant module, class, interface, method or file.
5. **Alternatives:** Explain practical alternatives where they exist.
6. **Trade-offs:** Explain advantages, disadvantages, risks and maintenance implications.
7. **Recommendation:** Explain why the selected approach is appropriate.

Use practical Dastarkhwan examples.

Teach progressively without overwhelming the Project Owner. Focus on concepts relevant to the current task instead of unrelated tutorials.

### 16.2 Explain Meaningful Code Changes

For every meaningful implementation change, explain:

- What the previous code did.
- What the new code does.
- Why the change was necessary.
- How relevant classes, interfaces and methods interact.
- How the change affects other modules.
- Security, performance, scalability and maintenance implications.

Where useful, provide a small before-and-after example or explain the execution flow step by step.

### 16.3 Code Comments and XML Documentation

Add useful code comments where they improve understanding and long-term maintainability.

Comments should explain:

- Why a non-obvious implementation or design choice exists.
- Important business rules and architectural boundaries.
- Security-sensitive behaviour and fail-closed decisions.
- Non-obvious validation, error handling and edge cases.
- Important dependencies and lifecycle assumptions.
- Integration requirements.
- Workarounds and why they are necessary.

Use appropriate XML documentation for important public C# interfaces, classes and methods when their purpose, behaviour or usage is not self-evident.

For complex methods, explain important steps and reasoning rather than documenting every line.

**Commenting rules:**

- Prefer clear, self-explanatory names and simple code.
- Do not add comments that merely repeat the code.
- Do not add excessive comments to every line.
- Keep comments accurate when implementation changes.
- Remove or update misleading and obsolete comments.
- Do not use comments as a substitute for good architecture or documentation.
- Explain important security assumptions explicitly.
- Never expose secrets, credentials, tokens or sensitive configuration in comments.

### 16.4 Learning-Oriented Completion Report

In addition to the normal completion report, include:

1. **What I learned:** Important technical concepts introduced or clarified.
2. **How the implementation works:** A simple explanation of the execution flow.
3. **Code walkthrough:** Important changed files, classes, interfaces and methods.
4. **Comments and documentation:** Where comments or XML documentation were added or updated, and why.
5. **Future considerations:** Concepts the Project Owner should understand before extending the implementation.

### 16.5 Permanent Application

Apply these standards to future Dastarkhwan implementation, review and documentation tasks.

The goal is not merely to produce working software. The goal is to help the Project Owner progressively understand the system, its architecture, security and codebase so they can confidently maintain it and make informed decisions.

## 17. Execution Instructions

Follow this sequence:

**Step 1 — Baseline verification**

Verify local repository path, branch, HEAD and working-tree status. Stop on any unexpected discrepancy.

**Step 2 — Documentation inspection**

Read the actual instruction and architecture documents. Identify outdated statements, contradictions and missing requirements.

**Step 3 — Change plan**

Prepare a concise file-by-file change plan. Explain any ambiguity before proceeding.

**Step 4 — Documentation implementation**

Update only the authorized existing files. Keep changes focused and preserve unrelated content.

**Step 5 — Self-review**

Check all acceptance criteria, architecture consistency, task statuses, file references and agent instructions.

**Step 6 — Completion report**

Provide:

- Baseline branch, SHA and date.
- Working-tree status.
- Exact files modified.
- Summary of each document change.
- Important architecture and governance updates.
- Confirmation of excluded operations not performed.
- Remaining risks or unresolved questions.
- Learning and knowledge-transfer summary.
- Suggested next step.

Provide exact diffs or a sufficiently detailed file-by-file change summary so the Project Owner and reviewers can inspect the work.

**Step 7 — Stop for review**

Stop after providing the completion report.

Do not begin another task.

Wait for ChatGPT Account 1 review, independent ChatGPT Account 2 review and the Project Owner's manual acceptance.

Do not start `DK-TENANT-ACCESS-FOUNDATION-01` automatically.