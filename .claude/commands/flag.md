Add or remove a feature flag (Microsoft.FeatureManagement): $ARGUMENTS

Usage:
- `/flag add <FlagName> [service]` — add a new flag, default `false`
- `/flag remove <FlagName>` — remove a flag and its `[FeatureGate]`
  usages (only if the flag is fully rolled out, confirm first)
- `/flag list` — list flags + their default + where they're gated

For `add`:

1. Pick the right `appsettings.json`:
   - All-services flag → `src/LMS.SharedKernel/...` shared file
   - Single-service flag → that service's `appsettings.json`
2. Add:

   ```jsonc
   "FeatureManagement": {
     "<FlagName>": false   // Phase 3+ default; flip per environment
   }
   ```

3. If the user named a target endpoint/handler, add `[FeatureGate("<FlagName>")]`.
4. Print:
   - Where the default lives
   - What's gated
   - Suggested follow-up: schedule a cleanup agent in N weeks via
     `/schedule` once the flag is fully rolled out

For `remove`:

1. `grep -rn '"<FlagName>"' src/` and `grep -rn 'FeatureGate("<FlagName>")' src/`
2. Confirm the flag is at 100% in production before deleting code paths.
3. Remove the gate, the false branch, and the appsettings entry in
   one commit.

Always: Phase 3+ flags MUST default `false`, per CLAUDE.md.
