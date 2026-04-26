Explain code, a flow, or a symbol: $ARGUMENTS

`$ARGUMENTS` can be:
- A file path → explain its purpose, key types, and how it fits in
- A symbol (`CoursePublished`, `useCourse`) → grep, find definition + uses,
  explain
- A "flow" prompt ("how does enroll work end-to-end?") → trace
  endpoint → service → DbContext → events → consumers → frontend hook

Rules:
- Read the actual code; do NOT speculate
- Cite file:line for every claim (`[CourseService.cs:42](...)`)
- Frame for the user's level (assume mid-senior backend if unclear)
- For cross-service flows, render a small Mermaid sequence diagram
- ≤ 25 lines of prose. If longer, you're probably summarizing too
  much code at once — narrow the scope and ask
- Don't propose changes. This is read-only.

If the explanation reveals a bug or smell, mention it in one line at
the end with severity tag (`[smell]`, `[bug]`, `[security]`).
