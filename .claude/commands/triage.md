Pick the next thing I should work on.

Sources (parallel):
- Open issues assigned to me: `gh issue list --assignee @me --state open`
- PRs needing my review: `gh pr list --search "review-requested:@me"`
- PRs of mine with failing CI: `gh pr list --author @me --state open --json number,title,statusCheckRollup`
- Failed/skipped tasks in `.claude/run-summary.json` (if recent)
- TODO/FIXME in changed files since main:
  `git diff main...HEAD --name-only | xargs grep -nE "TODO|FIXME|XXX" 2>/dev/null`
- Active worktrees with `BUG.md`: `find .claude-worktrees -name BUG.md`

Rank by:
1. **Blockers on others** (review requests, failing PR CI) — top
2. **In-flight failed tasks** (cheaper to fix while context is hot)
3. **Assigned issues** by priority label
4. **Drift** (TODOs in your branch)

Output ≤ 6 ranked items, each with: title, link/path, why it's
ranked there, estimated effort (S/M/L), suggested next command
(`/fix`, `/pr`, `/plan`, etc.). Print, don't act.
