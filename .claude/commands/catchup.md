Bring me up to speed on the current state of work.

Run in parallel:
- `git status` (no `-uall`)
- `git log --oneline -20`
- `git log --since="3 days ago" --pretty=format:"%h %an %s"`
- `git branch -vv` (track which branches exist locally)
- `git diff main...HEAD --stat` if on a feature branch
- `gh pr list --author @me --state open` and `gh pr list --state open --limit 10`
- `find .claude-worktrees -maxdepth 2 -name DONE.md -o -name BUG.md 2>/dev/null` to surface in-flight subagent work
- `cat .claude/run-summary.json` if it exists

Synthesize a < 15-line briefing:
- What branch you're on + clean/dirty
- What landed on `main` recently
- Open PRs (yours and others)
- Any in-flight `.claude-worktrees/` runs (failed/skipped tasks)
- Suggested next action (resume work / open PR / investigate failure)

Do NOT read any code. Pure status only.
