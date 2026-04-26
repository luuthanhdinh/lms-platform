Write a technical spec for: $ARGUMENTS

Output: `docs/specs/spec-{YYYYMMDD}-{slug}.md`

Process:

1. Read root `CLAUDE.md`, `docs/architecture.md`, and any
   `docs/services/{name}.md` the feature touches.
2. Survey existing code briefly — note patterns to reuse, ADRs that apply.
3. Write the spec with these sections (no fluff, no marketing):

   ```markdown
   # Spec: <feature>
   _Status: Draft · Author: <user> · Date: YYYY-MM-DD_

   ## Problem
   What user/business problem this solves. One paragraph.

   ## Non-goals
   What this explicitly does NOT do.

   ## Approach
   The chosen design. Reference ADRs.

   ## API / events
   - Endpoints (method, path, request, response, error codes)
   - MassTransit events (record signature, publisher, consumers)

   ## Data model
   New/changed entities. Migration outline.

   ## UX
   Routes, components, key flows. Skip if backend-only.

   ## Security & multi-tenancy
   Tenant isolation, authz policies, rate-limit.

   ## Rollout
   Feature flag, phase, backfill plan, kill switch.

   ## Risks & alternatives
   What could go wrong. What we considered and rejected, with reasons.

   ## Open questions
   ```

4. Print the path; do NOT proceed to implementation.
   Suggest `/plan <feature>` as the next step to convert the spec
   into a task graph.
