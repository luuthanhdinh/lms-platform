Clean up artifacts from finished `/ship` runs: $ARGUMENTS

Default (no args): dry-run — list what would be removed.
`/cleanup --apply` to actually delete.

Sweep:

1. **Worktrees** — for each `.claude-worktrees/T*-*` whose branch
   is fully merged into `main`:
   - `git worktree remove <path>`
   - `git branch -d <branch>` (safe delete)
2. **Branches** — local branches matching `feat/*-T*` whose tip is
   merged into `main`: safe-delete with `-d` (never `-D`).
3. **Artifacts** — older than 30 days under `.claude/dumps/`,
   `.claude/run-summary.json` if its `completed_at` is > 30 days.
4. **Stale .claude generated files** — `contracts.md`,
   `task-graph.json` whose mtime > 7 days AND whose feature is in a
   merged PR: move to `.claude/archive/{YYYYMMDD-feature}/`.

Refuse to:
- Delete unmerged branches (require explicit user approval per branch)
- Delete `main` or any branch not matching `feat/*` / `fix/*` / `chore/*`
- Touch anything outside `.claude*` and matching feature branches

Print a summary table: kind, path, age, action.
