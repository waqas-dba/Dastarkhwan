---
name: git-workflow
description: Git branching, commit, pull-request, merge, conflict and recovery guidance for the Dastarkhwan/CoreKit repository. Use when the user asks which branch to create, how to name or split commits, how to open or review a pull request, how to merge, resolve a conflict, undo a mistake, or keep master, uncommitted work, migrations and secrets safe. Advisory only - Claude never runs Git write commands and instead gives commands for the user to run.
---

# Git workflow (advisory)

Claude NEVER runs Git write commands (`CLAUDE.md` §10, D-008). Read-only inspection is fine: `git status --short`, `git log`, `git diff`, `git branch --show-current`. Everything below is for the **user to run**: give commands in one code block, one step at a time, say what each does, and mark destructive ones with ⚠.

Before advising, inspect branch, `git status --short`, HEAD hash/date. If uncommitted work could be affected, say so first.

## Branch or direct commit

| Type | Use for |
|---|---|
| `feature/` | New behaviour or module slice (one approved micro-task) |
| `fix/` | Bug or security fix with one cause |
| `docs/` | Markdown/comments only, no behaviour change |
| `experiment/` | Throw-away spike. Never merged as-is: redo cleanly on a `feature/` branch, then delete |

**Direct commit to `master`** only for a reviewed, docs-only change (typo, doc update). **Always branch** for anything touching `*.cs`, `.csproj`, migrations, configuration, auth or tenant logic. Rule: `master` must always build.

## Naming

`<type>/<module>-<short-slug>`: lowercase, hyphens, at most 5 words, one branch per micro-slice.
`feature/identity-lockout` · `fix/access-cache-invalidation` · `docs/skills-setup` · `experiment/platform-scope`

## Commits

- **Small:** one reason to change, understandable alone. Never mix refactor/rename with behaviour. Put a migration in its own commit directly after the model change it belongs to.
- **Conventional Commits:** `type(scope): imperative subject, max 72 chars`. Types: `feat fix docs refactor test chore perf`. Scope = module (`identity`, `access`, `tenant`, `shared-kernel`, `docs`). Breaking change: `feat(identity)!:` plus a `BREAKING CHANGE:` footer. Full format and examples: `ai-tasks.md` §9.
- Stage reviewed hunks only, and read the staged diff before committing:

```
git switch -c feature/identity-lockout     # from an up-to-date master
git add -p                                 # stage reviewed hunks only
git diff --staged                          # last look, also for secrets
git commit -m "feat(identity): lock account after repeated failed sign-ins"
```

## Pull request, review, merge

- Open a PR into `master` even when working alone. Keep it small (about 400 changed lines or fewer). Description: what and why, how the user verified it, migration yes/no, security impact, rollback.
- Review: the read-only reviewer account (`CLAUDE.md` §6) reviews the diff. Resolve critical findings before merging.
- Merge: **squash** when the branch has WIP commits; **rebase/merge commit** only when every commit is clean and meaningful. No force-push to shared branches. Delete the branch after merging.
- Update a branch: `git fetch`, then `git rebase origin/master` (unpushed branch only) or `git merge origin/master` (already pushed).

## Conflicts and recovery

```
git status                     # list conflicted files; fix the <<<<<<< markers
git add <file>
git rebase --continue          # or: git merge --continue
git rebase --abort             # or: git merge --abort   (back to before)
```

- **Migrations:** never hand-merge a `ModelSnapshot`. Keep one migration chain: drop your unpushed migration, merge `master`, then re-create it (when authorised).
- Before anything risky: `git branch backup/<name>`.
- Undo last unpushed commit, keep the work: `git reset --soft HEAD~1`.
- Undo a pushed commit safely: `git revert <sha>`.
- Lost a commit or branch: `git reflog`, then `git switch -c rescue <sha>`.
- ⚠ Destroy uncommitted work: `git restore <file>`, `git reset --hard`, `git clean -fd`, `git checkout -- .`. Use only after `git stash push -u` or a backup branch.

## Protect master and uncommitted work

- On GitHub (user setting): protect `master`: require PR and a passing check, block force-push and deletion.
- Before `switch`, `pull` or `rebase`: `git status --short` must be clean, or run `git stash push -u -m "wip: <what>"` and later `git stash pop` (`git stash list` to see them).
- Never pull or switch while uncommitted changes touch files the operation changes.

## Migrations and secrets

- **Migrations:** commit with the model change, on the same branch. Never edit a migration that is pushed or applied: add a new one. No `dotnet ef database update` without explicit authorisation (`CLAUDE.md` §9). The PR must state data-loss risk and that `Down` works.
- **Secrets:** never commit them. Use user-secrets or environment variables. `.gitignore` must cover `appsettings.Development.json`, `.env`, and local `*.ps1` helpers. Check before committing:

```
git diff --staged | grep -niE "password=|secret|apikey|signingkey|-----BEGIN"
```

- **If a secret was committed:** treat it as leaked. **Rotate it first**; removing it from history (`git filter-repo` plus force-push) does not un-leak a public repo. See the committed-secrets follow-up in `current-state.md`.