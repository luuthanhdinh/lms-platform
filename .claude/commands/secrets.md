Manage user-secrets safely: $ARGUMENTS

Argument: `list <Service>` | `set <Service> <Key> <Value>` |
`rotate <Service> <Key>` | `init <Service>`.

All commands operate on `dotnet user-secrets` against the chosen
service's Api project (or Migrator if the secret is DB-only).

## init <Service>

```bash
dotnet user-secrets init -p src/services/LMS.{Name}Service/LMS.{Name}Service.Api
```

## list <Service>

```bash
dotnet user-secrets list -p src/services/LMS.{Name}Service/LMS.{Name}Service.Api
```

Print KEYS only — NEVER print values to chat (they could be
captured in logs or a screenshot).

## set <Service> <Key> <Value>

1. Refuse if the key looks like a production secret name
   (`Prod*`, `*Production*`); production secrets go through the
   secret manager, not user-secrets.
2. Refuse if value appears to be a real JWT, an Anthropic API key
   (`sk-ant-`), or a Postgres URL with a real host.
3. Run:

   ```bash
   dotnet user-secrets set "<Key>" "<Value>" \
     -p src/services/LMS.{Name}Service/LMS.{Name}Service.Api
   ```

4. Confirm by listing keys (not values).

## rotate <Service> <Key>

1. Generate a new value (32-byte url-safe random for tokens,
   ask the user for API keys).
2. `set` the new value.
3. Print: "Restart the affected services. Old value still valid in
   any running instance until restart."

NEVER write secrets to a file in the repo. NEVER paste secret
values into chat output. NEVER commit `.env*` or
`secrets.json` — `settings.json` deny-list already blocks these.
