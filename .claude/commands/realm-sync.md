Sync the local Keycloak realm with the source-of-truth files: $ARGUMENTS

The Keycloak realm definitions live in `realms/` (loaded via
`builder.AddKeycloak("keycloak").WithRealmImport("./realms")` in
AppHost).

## export (default if no arg)

Pull the live realm config from the running local Keycloak and
write it back to `realms/{realm}.json`. Use this after editing
roles/clients in the Keycloak admin UI to capture the change in
git.

```bash
# Requires AppHost running and kcadm.sh available in the
# Keycloak container — adjust to your setup.
docker exec lms-keycloak /opt/keycloak/bin/kc.sh export \
  --realm lms --file /tmp/lms.json
docker cp lms-keycloak:/tmp/lms.json realms/lms.json
```

Then prettify + diff:

```bash
jq . realms/lms.json > realms/lms.tmp && mv realms/lms.tmp realms/lms.json
git diff realms/
```

## import

Reload the realm into local Keycloak after editing the JSON:

```bash
# Easiest: stop AppHost, restart — the realm import re-runs.
# Or: hot reload via kcadm.sh:
docker exec lms-keycloak /opt/keycloak/bin/kc.sh import \
  --file /opt/keycloak/data/import/lms.json --override true
```

## diff

Show drift between live realm and `realms/lms.json` without writing:
do an export to a temp file and `diff -u`. Useful pre-commit check.

NEVER commit secrets that leak in the export (client secrets,
LDAP bind passwords). Strip them before commit — Keycloak emits
empty placeholders for `secret` fields in proper exports; verify.
