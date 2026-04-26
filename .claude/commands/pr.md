Open a PR for the current branch: $ARGUMENTS

1. Verify branch != `main`. If on main, abort.
2. Run in parallel:
   - `git status`
   - `git log main..HEAD --pretty=format:"%h %s"`
   - `git diff main...HEAD --stat`
   - `git diff main...HEAD` (full)
3. If there are uncommitted changes, ask before continuing.
4. If branch isn't pushed, `git push -u origin HEAD`.
5. Draft:
   - Title: < 70 chars, conventional-commit style
     (`feat(course): publish flow`, `fix(gateway): ...`)
   - Body sections: **Summary** (3 bullets max, the WHY),
     **Changes** (per-area), **Test plan** (checklist of how to verify),
     **Contracts touched** (events/endpoints/migrations from
     `.claude/contracts.md` if present)
6. Create with HEREDOC body:

   ```bash
   gh pr create --title "..." --body "$(cat <<'EOF'
   ## Summary
   - ...

   ## Changes
   - ...

   ## Test plan
   - [ ] ...

   ## Contracts
   - ...

   🤖 Generated with [Claude Code](https://claude.com/claude-code)
   EOF
   )"
   ```

7. Print the PR URL.

If `$ARGUMENTS` is `--draft`, pass `--draft` to `gh pr create`.
