# Mandatory delivery workflow

Use this workflow for every task that modifies the repository. It applies equally to feature work, bug fixes, refactors, tests, documentation and configuration changes.

## 1. Create an isolated base

Before editing files:

1. Inspect the current worktrees, branch and status.
2. Leave existing dirty worktrees and unrelated changes untouched.
3. Fetch the remote base branch:

   ```powershell
   rtk git fetch origin main
   ```

4. Create a sibling worktree and a new branch directly from the fetched base:

   ```powershell
   rtk git worktree add -b <type>/<short-slug> <sibling-worktree-path> origin/main
   ```

`origin/main` is the authoritative freshly pulled base. Do not start from a stale local `main`. Use a descriptive branch such as `feat/...`, `fix/...`, `refactor/...` or `docs/...`.

If a task is already underway, verify that the active worktree is dedicated to that task and based on `origin/main`; otherwise stop and move the work to a new worktree before continuing. Never reset or discard another worktree's changes to make this possible.

## 2. Implement and validate

- Read the repository instructions and the relevant domain skills before editing.
- Follow the repository's test-first rules for executable production changes.
- Keep changes scoped to the request and preserve unrelated user work.
- Update documentation and thematic memory for non-trivial changes.
- Run `graphify update .` after code changes.
- Validate the touched application surface locally, including responsive behavior for visible UI changes.
- Use `rtk` as the command prefix, as required by the repository instructions.
- Before committing, inspect `rtk git diff --check`, `rtk git status --short` and the staged diff.

## 3. Rebase the completed branch

After implementation and before pushing:

```powershell
rtk git fetch origin main
rtk git rebase origin/main
```

Resolve any conflict deliberately, rerun the affected validation, and inspect the final diff. Do not force-push a branch another contributor may use. If a force-with-lease is genuinely required for this isolated branch, explain it in the handoff.

## 4. Commit and push

Use a Conventional Commit that describes the user-visible outcome:

```powershell
rtk git add <scoped-paths>
rtk git commit -m "<type>(<scope>): <imperative summary>"
rtk git push -u origin <branch>
```

Do not include unrelated files from the primary checkout or other worktrees.

## 5. Open the pull request

Use the GitHub CLI after confirming authentication and the repository:

```powershell
rtk gh auth status
rtk gh pr create --base main --head <branch> --title "<conventional title>" --body-file <reviewed-body-file>
```

The PR description must accurately include:

```markdown
## Summary
- What changed.

## Why
- User problem and intended outcome.

## Implementation
- Important technical decisions and affected surfaces.

## Validation
- Exact tests, builds, lint checks and manual checks run.
- Explicitly list checks that could not be run and why.

## Risks and follow-up
- Known limitations, rollout concerns and remaining work.

## Checklist
- [ ] Reviewed the final diff
- [ ] Tests/build/linters pass (or limitation is documented)
- [ ] Responsive/manual verification completed (or limitation is documented)
- [ ] Documentation and memory updated when applicable
- [ ] `graphify update .` run after code changes
```

Use the repository's pull request template if one exists, and do not claim a check passed when it was not run. The PR must target `main`, and the task handoff must include the direct GitHub PR URL. Stop after opening the PR: validation and merge remain the user's decision unless explicitly delegated.

## 6. Handoff

Return:

- the PR link;
- the worktree path and branch;
- the validation result;
- any pending CI, device, credential or environment checks;
- confirmation that the PR was not merged.

For read-only questions that do not change repository state, this delivery workflow is not needed.
