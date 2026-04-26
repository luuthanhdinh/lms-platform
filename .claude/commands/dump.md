Dump current working state to a shareable file: $ARGUMENTS

Output: `.claude/dumps/dump-{YYYYMMDD-HHMM}.md` (or path in $ARGUMENTS).

Capture in parallel:
- `git rev-parse HEAD` + branch + remote
- `git status` (no -uall)
- `git diff main...HEAD --stat` and full diff (clipped to 2k lines)
- Open PRs: `gh pr list --author @me --state open`
- Active worktrees: `git worktree list`
- Subagent run summary: `.claude/run-summary.json` (if present)
- Latest review/security reports: `.claude/review-report.md`,
  `.claude/security-report.md` (if present)
- Last 20 commits: `git log -20 --oneline --decorate`

Format as a single Markdown file with sections + collapsible details
for the long diff. Suitable for pasting into Slack/issue/handoff.

NEVER include secrets, env files, or `appsettings.Production.json`
content. NEVER auto-upload anywhere — local file only.
