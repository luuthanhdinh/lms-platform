Generate a standup update for the user.

Inputs (run in parallel):
- `git log --author="$(git config user.email)" --since=yesterday --pretty=format:"%h %s"`
- `git log --author="$(git config user.email)" --since="2 days ago" --until=yesterday --pretty=format:"%h %s"`
  (yesterday's work for "what I did")
- `gh pr list --author @me --state open`
- `gh pr list --author @me --state merged --search "merged:>=$(date -v-1d +%Y-%m-%d)"`
- `git branch --show-current`
- Read `.claude/run-summary.json` if recent

Output (paste-ready, ≤ 8 bullets total):

```
*Yesterday*
- ...
- merged #123 (course publish flow)

*Today*
- ...

*Blockers*
- ... (or "none")
```

Tone: terse, factual, no emoji unless the user asks. Don't invent
work that isn't in git/PR history.
