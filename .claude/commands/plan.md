Run the master agent to plan: $ARGUMENTS

The master agent will:

1. Read project rules + relevant `docs/services/*.md`
2. Write `.claude/contracts.md` (events, endpoints, hooks, types)
3. Write `.claude/task-graph.json`
4. Print a summary for your approval

STOP after the plan. Do not execute anything.
