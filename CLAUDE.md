# Dastarkhwan / CoreKit — Engineering Rules

## Purpose
Build a secure, maintainable, performant modular application for food-item delivery and parcel pickup/delivery, initially in Pakistan. Prefer small steps and reuse sound existing code.

## Source of truth
- `roadmap.md`: milestone order.
- `docs/architecture.md`: module boundaries and dependencies.
- `docs/current-state.md`: verified implementation status.
- `docs/decisions.md`: accepted decisions and open questions.
- `docs/ai-tasks.md`: AI workflow and task templates.

If documents conflict, do not silently invent a decision. Explain the conflict and ask when it affects security, data, product behavior, or module boundaries.

## Workflow
1. Briefly plan substantial changes.
2. Inspect only relevant files and existing patterns.
3. Identify owning module, dependencies, scope, and acceptance criteria.
4. Implement the smallest coherent change.
5. Add/update focused tests, but **do not run tests**; the user runs them.
6. Update relevant documentation.
7. Report changed files, tests not run, risks, and next step.

## Scope and architecture
- Do not rewrite working code or perform unrelated refactoring.
- Preserve compatibility where practical; explain breaking changes.
- Prefer a modular monolith; no premature microservices or distributed infrastructure.
- Each module owns its rules and persistence. Cross modules only through explicit public contracts/events.
- Never access another module's private `DbContext`, tables, repositories, or internals.
- Avoid circular dependencies and generic frameworks for hypothetical needs.
- Identity owns accounts/authentication/credentials/sessions; Access owns roles/permissions/authorization.
- Create separate modules only for genuinely distinct responsibilities or business rules.
- Keep controllers/endpoints thin; business rules belong in the owning application/domain layer.

## Security
- Enforce authorization server-side for every protected operation.
- Fail closed when identity, tenant, store, permission, or resource ownership is unclear.
- Never trust client-supplied user IDs, tenant/store IDs, prices, discounts, or state transitions without validation.
- Apply tenant/store scope to reads, writes, exports, reports, jobs, callbacks, and admin paths.
- Hostnames are routing inputs, not authorization.
- Validate input; use parameterized database operations and safe output encoding.
- Keep secrets out of source control and logs; do not log tokens, passwords, payment secrets, or connection strings.
- Validate webhook signatures; make retries idempotent.
- Add negative tests for unauthorized and cross-tenant/store access where applicable.

## Data and performance
- Use decimal/fixed precision for money and explicit currency codes.
- Preserve order price, discount, tax, fee, and currency snapshots.
- Use database constraints and short transactions for important invariants.
- Use async I/O, bounded pagination, projections, and avoid N+1 queries.
- Add indexes for real query patterns; measure before optimizing.
- Cache only with understood invalidation and tenant-safe keys.
- Use background processing for suitable long tasks, with retry/idempotency.
- Never trade security or correctness for unmeasured speed.

## Quality and Git
- Follow existing conventions and verify framework versions before changing them.
- Comments only for non-obvious design, security, or complex business rules.
- Do not run tests/builds/migrations/destructive commands unless explicitly authorized.
- Never stage, commit, push, change branches, or rewrite Git history.
- You may recommend a user-made checkpoint and provide a commit message.
- Never claim verification succeeded unless the result was observed.

## Completion report
1. Files changed and behavior.
2. Tests added/updated (not run).
3. Security/data/performance considerations.
4. Risks, assumptions, and manual verification steps.
5. Suggested next milestone and Git checkpoint/message if appropriate.

## Multi-AI Development Workflow

### Roles
- ChatGPT: coordinate milestones, clarify requirements, and prepare implementation tasks.
- Claude Account 1 (Developer): implement one approved task at a time.
- Claude Account 2 (Reviewer): independently review the proposed changes and report defects or improvements. Do not implement the same task.
- Additional Claude accounts: perform separate research or specialist reviews only when needed.
- DeepSeek: provide an independent second opinion when useful.
- User: approve important decisions, run tests manually, and control Git operations.

### Shared Project Context
- Provide each AI with the relevant project documentation before assigning work.
- Treat `CLAUDE.md`, `roadmap.md`, and `docs/` as the shared source of project guidance.
- Do not assume different accounts share chat history or context.
- The developer must report changed files, implementation details, unresolved issues, and manual verification steps.
- Transfer the developer's change summary and diff to the reviewer.

### Change Coordination
- Only one AI may implement changes in the repository at a time.
- Reviewers must not edit the same task or overwrite the developer's changes.
- Do not start another implementation until the current changes are reviewed and reconciled.
- Keep changes small, focused, and consistent with the existing architecture.
- Never stage, commit, push, switch branches, or rewrite Git history.
- The user runs tests manually; AI assistants must not run tests, builds, or migrations unless explicitly authorized.