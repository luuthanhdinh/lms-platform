---
name: code-reviewer
description: >
  Reviews implementation against spec, contracts, and LMS
  absolute rules. Produces structured report with contract +
  absolute-rule violations highlighted first.
allowed-tools: Read, Bash, Grep, Glob
user-invocable: false
---

Work through this checklist in order:

1. **Contract compliance** — matches `.claude/contracts.md` exactly?
2. **LMS absolute-rule compliance** (from root `CLAUDE.md`):
   - Backend: TenantEntity, global TenantId filter, no cross-service
     HTTP, no JWT re-validation, events in `LMS.Contracts`, ILlmClient,
     QuestPDF for PDFs, feature-flag defaults, IsFree → 409 stub,
     `Assessment.LessonId` nullable semantics
   - Frontend: no token in localStorage, central apiClient, no raw
     fetch in components, tenantId from token, ProtectedRoute,
     React Hook Form, Tailwind only
3. **Correctness** — logic matches task spec?
4. **Error handling** — all failure paths handled (`Result<T>` /
   error boundary)?
5. **Test coverage** — new paths covered (xUnit / Vitest)?
6. **Security** — input validation, secrets via config, no PII logged

## Output (required sections, in this order)

### Contract violations           ← most critical
### Absolute-rule violations      ← BLOCKERS
### Blockers                       ← must fix before merge
### Suggestions                    ← should fix
### Approved                       ← what looks good
