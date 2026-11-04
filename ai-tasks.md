# AI Task Workflow — ChatGPT and Claude Accounts

## 1. Roles
| Participant | Responsibility | Must avoid |
|---|---|---|
| ChatGPT / coordinator | Clarify goals, define micro-slices and acceptance criteria, reconcile reviews | Repeating broad investigations or reopening accepted decisions without evidence |
| Claude Account 1 | Primary implementer for one approved slice at a time | Scope expansion, unrelated refactoring, unauthorized commands |
| Claude Account 2 | Independent read-only reviewer of a focused diff/report | Editing files, reimplementing, or reviewing a different/old baseline |
| User | Product decisions, manual testing, acceptance, all Git write operations | Treating unverified reports as proof |

AI accounts do not automatically share chat history, memory, repository access, or files. Supply each account only the task brief, relevant project docs, baseline hash, and relevant diff/files. Do not use multiple accounts to bypass service limits or terms.

## 2. Mandatory latest-code verification
Before every task, each account must:
1. Confirm repository root and relevant document/source paths.
2. Read current HEAD hash and commit date.
3. Check staged, unstaged, and untracked files with `git status --short`.
4. Inspect only relevant files and changes.
5. State the exact HEAD hash used as baseline.
6. Stop and ask if the baseline differs from the task brief or relevant uncommitted changes could be affected.

The recorded M0 baseline is `9555b8c` dated 2026-10-23. It is not automatically the latest HEAD. Never rely solely on old chat output or old documentation ZIPs. Do not scan all Git history unless the task specifically requires it.

## 3. Strict token and context budget
- One request = one small, measurable outcome.
- Read only the files required for that outcome.
- Do not dump the whole repository or entire documents when a relevant section is sufficient.
- Do not repeat project context, previous findings, or decisions already recorded.
- Reuse the last verified report; check only for changes since its baseline where relevant.
- Load only the skill/agent needed for the current task.
- Do not run broad audits, speculative research, or parallel agent reviews for trivial changes.
- Do not investigate later milestones or unresolved product questions prematurely.
- Prefer concise factual outputs, paths, and line references over long prose.
- Do not create additional documentation unless the user asks or the task requires it.
- Stop once the acceptance criteria for the current micro-slice are satisfied.

## 4. Micro-slice rules
A micro-slice should have one primary concern, a small expected file set, and observable acceptance criteria. Split a task further if it changes multiple module boundaries, persistence models, or unrelated behaviors.

For every task:
1. Verify latest code and working tree.
2. Inspect relevant files only.
3. Provide a short plan (goal, expected files, acceptance criteria).
4. Wait for approval before making changes or running state-changing commands.
5. Complete only the approved slice.
6. Report changed files, outcome, tests added/updated but not run, risks, and one next suggested slice.
7. Stop and wait for the user.

Never proceed automatically to the next slice. Never bundle multiple proposed slices simply to save turns.

## 5. One-owner and two-account coordination
- Account 1 is the only implementation owner for a given task.
- Account 2 is read-only. It receives the same task brief, relevant docs, baseline commit, and focused diff/report produced by Account 1.
- Account 2 independently checks requirements, security, architecture, compatibility, and task acceptance criteria. It does not edit or implement fixes.
- Only one AI account may modify the working tree at a time. Prefer a read-only copy or a pasted diff for Account 2.
- If review finds issues, Account 1 addresses only the approved findings in a separate micro-slice.
- The user decides which review findings to accept. Do not silently combine contradictory recommendations.
- Both accounts must report their own baseline hash. Do not assume one account's inspection represents the other account's current repository.

## 6. Permission boundaries
- AI may inspect Git read-only. AI must not stage, commit, push, pull, switch/checkout branches, merge, rebase, reset, cherry-pick, clean, amend, create branches, rewrite history, or change Git configuration/hooks.
- Do not run tests or builds unless the user explicitly authorizes that specific run. AI may add/update relevant test files; report them as not run.
- Do not generate/apply migrations or modify a database unless the user explicitly authorizes the specific operation. Creating migration files also requires approval.
- Do not modify/delete/overwrite existing uncommitted work.
- Do not expose secret values in logs, reports, or documentation.

## 7. Implementation task template
```markdown
# Micro-task: [one measurable outcome]

## Baseline
- Repository HEAD: [hash and date]
- Working tree: [clean / relevant local changes]

## Goal
[One observable outcome only.]

## Read
[Exact paths or relevant sections only.]

## Scope
- Owning module: [module]
- Expected files: [paths, if known]
- Acceptance criteria:
  - [ ] [Observable result]
  - [ ] [Security/data/compatibility condition]
  - [ ] Focused tests added/updated if applicable (not run)
  - [ ] Relevant documentation/status updated if needed

## Out of scope
- Other roadmap slices or future product decisions.
- Unrelated refactoring.
- Other modules' private persistence.

## Constraints
- Follow CLAUDE.md and accepted decisions.
- Inspect existing implementation before changing it.
- Keep the smallest compatible change.
- Do not run tests/builds/migrations or Git write operations without explicit permission.

## Completion report
- Baseline hash / working-tree status:
- Files changed:
- Outcome:
- Tests added/updated: [not run unless explicitly authorized]
- Risks/manual verification:
- Next suggested micro-slice:
- Suggested manual Git checkpoint/message (optional):
```

## 8. Independent review template
```markdown
Review only this focused change against the task acceptance criteria,
CLAUDE.md, docs/architecture.md, and accepted decisions.

Baseline commit: [hash]

Do not edit files, implement fixes, run tests/builds, or perform Git write
operations. Inspect only the supplied diff and relevant surrounding code.

Report only actionable findings, ordered by severity. For each, provide:
- Severity (Critical / High / Medium / Low)
- File and line/reference
- Concrete failure scenario
- Smallest suggested correction

Check relevant requirement gaps, authn/authz, tenant/store isolation,
fail-closed behavior, validation, transactions/idempotency, module coupling,
compatibility, error handling, and test coverage. Separate confirmed defects
from questions or speculation. If no material findings exist, say so briefly.
```

## 9. Git commit message guidance (user performs commits)
AI must never create a commit. After a slice is complete, reviewed, and the user has run any required manual verification, AI may propose a commit subject and body for the user to use.

Use a concise Conventional Commit style:

`<type>(<scope>): <imperative summary>`

Suggested types:
- `docs` — documentation only.
- `feat` — new behavior.
- `fix` — defect correction.
- `refactor` — behavior-preserving restructuring.
- `test` — test-only changes.
- `build` — build/package/dependency configuration.
- `security` — security hardening (use only if consistent with the repository's chosen convention).

Guidelines:
- Subject: concise, specific, imperative; avoid vague text such as `updates`, `changes`, or `fixes`.
- Body: explain **what changed and why**, plus relevant design/security implications. Use short paragraphs or bullets; wrap long lines where practical.
- Reference a task/slice or issue if the repository uses identifiers.
- Mention migration compatibility or required manual action when relevant, but never include secrets.
- Use `BREAKING CHANGE: ...` only when the change truly breaks compatibility.
- Prefer one commit per coherent reviewed slice; do not combine unrelated slices.
- Do not recommend a commit while critical review findings remain or verification is incomplete.

Examples for this project:

**Documentation synchronization (S1)**
```text
docs(corekit): record approved M1 decisions

- Record JWT refresh rotation and account lockout policy
- Document shared-database tenant isolation safeguards
- Record Tenant and Identity dependency direction

Refs: M1 / S1
```

**Dedicated Access test project**
```text
test(access): separate Access tests from architecture tests

- Add a dedicated Access test project
- Keep architecture dependency tests focused on module boundaries

Refs: M2 / S2.1
```

**Tenant decoupling**
```text
refactor(tenant): depend on SharedKernel contracts

- Remove direct Identity project references from Tenant
- Preserve tenant behavior through stable public abstractions

Refs: M5 / S4
```

**Account lockout**
```text
feat(identity): enforce temporary account lockout

- Lock accounts after five failed login attempts
- Apply the approved fifteen-minute lockout duration
- Add focused lockout tests

Refs: M3 / S5
```

These are examples only. Adjust scope and wording to the actual reviewed change. The user decides whether to use them.

## 10. Completion and handoff
Keep the report short:
1. Baseline hash and working-tree status.
2. Files changed or reviewed.
3. Outcome/findings and acceptance status.
4. Tests added/updated (not run) or explicitly authorized test result.
5. Security/data/compatibility risks and manual verification.
6. One next micro-slice.
7. Optional suggested manual commit subject/body.

Never claim success for tests, builds, migrations, deployments, or commits unless the user authorized the operation and the result is observed.
