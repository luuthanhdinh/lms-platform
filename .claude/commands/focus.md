Find unfinished work on the current branch.

Scan in parallel (limit each to current branch's diff vs main):

1. `git diff main...HEAD --name-only` → file list
2. In those files, grep for:
   - `TODO`, `FIXME`, `XXX`, `HACK`
   - `NotImplementedException`, `throw new NotImplemented`
   - `.Skip`, `[Fact(Skip = `, `it.skip`, `xit(`, `xdescribe(`
   - `console.log`, `Console.WriteLine` (debug leftovers)
   - `IgnoreQueryFilters` without a `// tenant-bypass:` justification
   - `any` type in TS (`: any`, `as any`)
   - Unfinished marker `// TBD` or `// LATER`
3. Cross-check with `gh pr view --json files` if a PR exists for
   this branch — anything in the diff but uncommitted is also
   "unfinished".
4. Check for failing tests: `dotnet test --no-build --logger:trx`
   (best-effort) and `pnpm test --run` for frontend if it's quick.

Output a checklist grouped by file, with severity:
- 🔴 BLOCKER — must fix before PR (NotImplemented, skipped tests)
- 🟡 SHOULD — TODO/FIXME, debug logs, `any` types
- 🟢 NICE — comments tagged TBD/LATER

Print, don't fix. The user picks what to address with `/fix` or
direct edits.
