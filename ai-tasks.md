# AI Task Workflow — ChatGPT, Claude, DeepSeek

## Roles
| Participant | Responsibility | Avoid |
|---|---|---|
| ChatGPT | Coordinate architecture, plan milestones, define acceptance criteria, review | Repeated redesign of accepted decisions |
| Claude | Primary implementation owner for one task at a time | Scope expansion, unrelated refactors, unauthorized test runs |
| DeepSeek | Independent review of a focused diff/contracts | Reimplementing the same feature |
| IDE assistant | Routine local completion where available | Unapproved broad design changes |
| User | Product decisions, manual tests, review, Git, acceptance/release | Accepting unverified claims |

Free-tool availability and limits may change. Models do not automatically share chat history; use repository docs as shared context.

## One-owner rule
One task has one implementation owner. Other assistants review the diff or focused contracts instead of rewriting the same feature. Share relevant requirements and changed files, not the entire repository by default. User resolves conflicts and accepts changes.

## Workflow
1. Select the smallest dependency-ready roadmap item.
2. Read `CLAUDE.md` and only relevant sections/files.
3. State a short plan and identify blocking decisions.
4. Implement one coherent change.
5. Request focused independent review when useful, especially for security-sensitive work.
6. Add/update tests but do not run them.
7. Update current-state, decisions, or roadmap only where facts/status changed.
8. Report changed files, tests not run, risks, and manual verification.
9. User runs tests, reviews, and decides whether to accept/commit.

## Save tokens/credits
- One measurable outcome per task.
- Inspect relevant files only; avoid full-repository dumps.
- Do not repeatedly summarize the same files.
- One implementer; other models review.
- Use review on risky changes, not every trivial edit.
- Avoid speculative abstractions and broad refactoring.
- Ask for concise, factual completion reports.

## Implementation task template
```markdown
# Task: [short measurable title]

## Goal
[One observable outcome.]

## Context
Read `CLAUDE.md` and only relevant sections of `roadmap.md`,
`docs/architecture.md`, `docs/current-state.md`, and `docs/decisions.md`.
Relevant files/contracts: [paths if known].

## Scope
- [Required behavior]
- Owning module: [module]
- Acceptance criteria:
  - [ ] Observable behavior works.
  - [ ] Security/data-integrity requirements are met.
  - [ ] Relevant tests are added/updated.
  - [ ] Documentation/status is updated if needed.

## Out of scope
- Unrelated refactoring/features.
- Changes to other modules' private persistence.
- Unapproved architectural decisions.

## Constraints
- Inspect existing code and reuse sound behavior.
- Follow `CLAUDE.md` and accepted decisions.
- Keep change small, secure, maintainable, and performance-conscious.
- Enforce authorization and tenant/store isolation where applicable.
- Add/update tests but do not run them.
- Do not stage, commit, push, change branches, or alter Git history.
- Ask rather than guess about security, money, persisted data, or module boundaries.

## Completion report
1. Files changed and why.
2. Behavior implemented.
3. Tests added/updated (not run).
4. Security/data/performance considerations.
5. Manual verification steps for user.
6. Risks and unresolved questions.
7. Next milestone.
8. Suggested Git checkpoint/message, if appropriate.
```

## Independent review template
```markdown
Review this focused diff against the task acceptance criteria,
`CLAUDE.md`, `docs/architecture.md`, and accepted decisions.

Do not implement or rewrite. Report actionable findings ordered by severity,
with file/line references where available.

Check requirement gaps, edge cases, authentication/authorization, tenant/store
isolation, fail-closed behavior, validation, transactions, idempotency,
cross-module coupling, N+1/unbounded queries, compatibility, errors, and tests.

For each finding, describe the concrete failure scenario and smallest fix.
Separate confirmed defects from questions/speculation. If there are no material
findings, say so.
```

## Manual verification handoff
Tell the user which tests changed, how they can run them in their own environment, expected results, required safe configuration, and any migration/deployment cautions. Do not claim success before results are available.

## Git checkpoint
Never stage or commit. After a coherent, reviewable milestone, you may suggest a manual checkpoint, e.g. `feat(access): enforce store-scoped permissions`. Do not recommend committing incomplete work or unresolved critical security issues.
