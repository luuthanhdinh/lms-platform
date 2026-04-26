Smart commit current staged + relevant unstaged changes: $ARGUMENTS

1. Run in parallel: `git status`, `git diff --staged`, `git diff`,
   `git log -5 --oneline` (mirror repo style).
2. If nothing is staged, identify the related changed files for the
   logical commit (don't blanket `git add -A`). List them and ask
   before staging if scope is ambiguous.
3. Refuse to stage:
   - `.env*`, anything matching `*secret*`, `*credentials*`
   - `appsettings.Production.json`
   - large binaries (> 1 MB)
4. Draft a conventional-commit message:
   - `feat(scope): ...`, `fix(scope): ...`, `refactor(scope): ...`,
     `chore(scope): ...`, `docs(scope): ...`, `test(scope): ...`
   - scope = service short-name (`course`, `gateway`, `frontend`) or
     `claude` for `.claude/` changes
   - Subject ≤ 70 chars, imperative mood, no trailing period
   - Body explains WHY (not what — diff shows that), wrapped at 72
5. If `$ARGUMENTS` is provided, use it as the subject line.
6. Commit via HEREDOC (preserve formatting), include co-author line:

   ```bash
   git commit -m "$(cat <<'EOF'
   <subject>

   <body>

   Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
   EOF
   )"
   ```

7. Run `git status` after — verify clean / what's still pending.

NEVER `--amend` (creates a new commit instead). NEVER `--no-verify`.
If a pre-commit hook fails, fix the underlying issue, re-stage,
new commit.
