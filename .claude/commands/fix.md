Investigate and fix a bug: $ARGUMENTS

`$ARGUMENTS` should describe the symptom (error message, stack
trace, ticket link, or "X happens when Y").

Workflow:

1. **Reproduce mentally** — read the description, identify the
   smallest unit (endpoint, consumer, hook, route) that owns the
   behavior. Don't grep blindly.
2. **Locate** — read the suspected file(s); follow call chain only
   as far as needed. Use Grep for symbol references.
3. **Hypothesize** — write the hypothesis in one sentence before
   editing anything. If you can't, ask.
4. **Confirm with a failing test FIRST**:
   - Backend: add an xUnit test that reproduces the bug
     (use the `xunit-integration` skill if it crosses services)
   - Frontend: add a Vitest case
   Run it, see it fail.
5. **Fix** — minimal diff. No surrounding cleanup. No abstraction.
   Respect LMS absolute rules (TenantId, contracts, ILlmClient, etc.).
6. **Verify** — re-run the test (now passes) + adjacent tests.
7. Print: root cause (one sentence), fix (file:line), test added,
   blast radius.

Do NOT commit. Do NOT open a PR (use `/pr` after).
