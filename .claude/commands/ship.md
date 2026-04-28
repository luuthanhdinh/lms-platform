Full pipeline for: $ARGUMENTS

1. Run master agent (`/plan $ARGUMENTS`). STOP and show plan.
2. Print: "Review .claude/task-graph.json and contracts.md.
   Reply 'approved' to start execution."
3. Wait for approval.
4. On approval: run `python .claude/orchestrate.py`
5. When done: run reviewer agent on all changed branches.
6. Show reviewer report and STOP.
