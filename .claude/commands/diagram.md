Render a Mermaid diagram of a flow, service, or architecture: $ARGUMENTS

`$ARGUMENTS` examples:
- `enroll flow` → sequence diagram across services
- `course-service` → component diagram of one service
- `events` → graph of all event publishers/consumers from `LMS.Contracts`
- `architecture` → top-level system diagram from `docs/architecture.md`

Process:

1. Identify the scope; read only what's needed:
   - For event graphs: `grep -rn "IConsumer<" src/` + publisher
     `Publish(new ...)` calls
   - For service flows: trace endpoint → service → DbContext → events
   - For architecture: read `docs/architecture.md` + AppHost
2. Render Mermaid (`sequenceDiagram` for flows, `flowchart LR` for
   topology, `erDiagram` for data model). Keep ≤ 30 nodes — split
   if larger.
3. Output:
   - Inline in chat for quick view
   - Save to `docs/diagrams/{slug}.md` (Markdown with the Mermaid
     fenced block) so it's reviewable in GitHub
4. Cite the file:line for every node/edge claim. No invented links.

Read-only. Don't modify code.
