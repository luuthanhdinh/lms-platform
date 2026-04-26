Author a new ADR (Architecture Decision Record): $ARGUMENTS

`$ARGUMENTS`: the decision in one line ("use Outbox for course events").

Process:

1. List existing ADRs: `ls docs/adr/` — find the next number
   (NNN, zero-padded; current range goes up to 031 per CLAUDE.md).
2. Pick a slug from the decision (kebab-case, ≤ 40 chars).
3. Read 2 nearby ADRs to mirror the project's tone and depth.
4. Create `docs/adr/adr-{NNN}-{slug}.md`:

   ```markdown
   # ADR {NNN}: {Title}
   _Status: Proposed · Date: YYYY-MM-DD_

   ## Context
   What forces are at play. Cite the spec/issue/incident.

   ## Decision
   The choice, in active voice. One paragraph.

   ## Consequences
   - Positive: ...
   - Negative / trade-offs: ...
   - Follow-ups required: ...

   ## Alternatives considered
   - Option B — rejected because ...
   - Option C — rejected because ...

   ## References
   - Spec: docs/specs/...
   - PR: #...
   ```

5. Print the path and the next step (link from relevant
   `docs/services/{name}.md`).

Don't change ADRs 001–031 (historical). Don't auto-flip Status to
Accepted — leave that for the human reviewer.
