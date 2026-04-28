Review all branches from the last `task-graph.json` run.

1. Read `.claude/task-graph.json` to find all branches.
2. For each branch: `git diff main..<branch-name>`.
3. Read `.claude/contracts.md` and root `CLAUDE.md` (absolute rules).
4. Apply the `code-reviewer` skill to each diff.
5. Produce a combined report at `.claude/review-report.md`:
   - One section per branch
   - "Cross-branch" section for contract violations
     (incompatible assumptions about a shared boundary)
   - "Absolute-rule violations" section (TenantId, JWT, events, etc.)
