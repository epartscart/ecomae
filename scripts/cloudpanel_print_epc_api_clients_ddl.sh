#!/usr/bin/env bash
# Print paste-ready DDL + GRANT for asap.epc_api_clients (or TenantRegistry DB).
# Never prints passwords. Never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

DB_NAME="asap"
DB_USER="ecomae_aspnet"
if [[ -f "$ENV_FILE" ]]; then
  parsed="$(python3 - "$ENV_FILE" <<'PY'
import sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
db, user = "asap", "ecomae_aspnet"
for line in raw.splitlines():
    line = line.strip()
    if not line or line.startswith("#") or "=" not in line:
        continue
    k, v = line.split("=", 1)
    if k.strip() != "ConnectionStrings__TenantRegistry":
        continue
    v = v.strip().strip("'").strip('"')
    for part in v.split(";"):
        part = part.strip()
        if "=" not in part:
            continue
        pk, pv = part.split("=", 1)
        pk = pk.strip().lower()
        pv = pv.strip()
        if pk in ("database", "initial catalog") and pv:
            db = pv
        elif pk in ("user", "uid", "user id") and pv:
            user = pv
print(f"{db}\t{user}")
PY
)"
  DB_NAME="$(printf '%s' "$parsed" | cut -f1)"
  DB_USER="$(printf '%s' "$parsed" | cut -f2)"
fi

printf '%s\n' '== Paste-ready epc_api_clients DDL + GRANT (no secrets) =='
printf 'Target DB: %s\n' "$DB_NAME"
printf 'Platform user: %s\n' "$DB_USER"
printf '\n-- Run as MySQL admin --\n'
# Use escaped backticks — unescaped `...` is bash command substitution.
printf 'USE `%s`;\n' "$DB_NAME"
cat "$ROOT/scripts/sql/epc_api_clients.sql"
printf '\n'
printf 'GRANT SELECT, INSERT, UPDATE ON `%s`.`epc_api_clients` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
printf 'GRANT SELECT, INSERT, UPDATE ON `%s`.`epc_api_clients` TO '\''%s'\''@'\''%%'\'';\n' "$DB_NAME" "$DB_USER"
printf 'GRANT SELECT, INSERT ON `%s`.`sessions` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
printf 'GRANT SELECT ON `%s`.`users` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
printf 'GRANT SELECT ON `%s`.`users_groups_bind` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
printf 'GRANT SELECT ON `%s`.`groups` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
printf 'FLUSH PRIVILEGES;\n'
printf '\nNext:\n'
printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
