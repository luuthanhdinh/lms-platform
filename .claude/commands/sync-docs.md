Run the docs-writer agent to bring `docs/` in sync with shipped code
since the last commit on `main` (or since `$ARGUMENTS` if a ref is given).

1. `git diff main...HEAD --stat` (or vs $ARGUMENTS) to see scope.
2. For each service touched, update `docs/services/{name}.md`.
3. For each event added/changed in `LMS.Contracts`, update `docs/events.md`.
4. For each entity changed, update `docs/entities.md`.
5. If a novel pattern landed (reviewer flagged), add a new ADR.
6. Print a diff summary; do NOT commit.
