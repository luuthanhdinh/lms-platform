Onboard me to this project (fresh-clone walkthrough).

1. Print the stack one-liner from root `CLAUDE.md`.
2. Verify prerequisites in parallel and report any missing:
   - `dotnet --version` (need 9.x)
   - `node --version` (need 20+)
   - `pnpm --version`
   - `docker info` (Aspire needs containers)
   - `gh auth status`
3. Print the doc reading order:
   - `CLAUDE.md` (absolute rules)
   - `docs/architecture.md`
   - `docs/services/{name}.md` for the service you'll touch first
   - `docs/frontend.md`
   - `.claude/README.md` (the multi-agent workflow)
4. Print the run commands:
   - `dotnet restore && dotnet build`
   - `cd frontend && pnpm install`
   - `dotnet run --project src/LMS.AppHost`
   - Aspire dashboard URL appears in the AppHost log
5. List the slash commands they'll use most often:
   `/catchup`, `/plan`, `/ship`, `/fix`, `/test`, `/pr`, `/commit`.
6. Pinpoint Phase 1 services and the recommended build order from
   root `CLAUDE.md`.

Read-only. Do not modify anything.
