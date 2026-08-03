#!/usr/bin/env bash
# Apply scripts/sql/epc_api_clients.sql + GRANTs to TenantRegistry DB using elevated MySQL auth.
# Tries (never prints passwords):
#   --defaults-file=/etc/mysql/debian.cnf (debian-sys-maint)
#   other local defaults-files, socket, sudo mysql, ECOMAE_MYSQL_ADMIN_*
# Requires ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES.
# Never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
SQL_FILE="$ROOT/scripts/sql/epc_api_clients.sql"

printf '%s\n' '== Apply epc_api_clients DDL (elevated MySQL) =='

if [[ "${ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL:-}" != "YES" ]]; then
  printf 'Refusing without confirmation.\n' >&2
  printf 'Run:\n' >&2
  printf '  ECOMAE_CONFIRM_APPLY_EPC_API_CLIENTS_DDL=YES bash scripts/cloudpanel_apply_epc_api_clients_ddl.sh\n' >&2
  exit 2
fi

if [[ ! -f "$ENV_FILE" ]]; then
  printf 'Missing env file: %s\n' "$ENV_FILE" >&2
  exit 1
fi
if [[ ! -f "$SQL_FILE" ]]; then
  printf 'Missing %s\n' "$SQL_FILE" >&2
  exit 1
fi

parsed="$(python3 - "$ENV_FILE" <<'PY'
import sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
db, user = "", "ecomae_aspnet"
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
        pk, pv = pk.strip().lower(), pv.strip()
        if pk in ("database", "initial catalog") and pv:
            db = pv
        elif pk in ("user", "uid", "user id") and pv:
            user = pv
print(f"{db}\t{user}")
PY
)"
DB_NAME="$(printf '%s' "$parsed" | cut -f1)"
DB_USER="$(printf '%s' "$parsed" | cut -f2)"
if [[ -z "$DB_NAME" ]]; then
  printf 'Could not parse TenantRegistry Database=\n' >&2
  exit 1
fi
printf 'Target database: %s (GRANT to %s)\n' "$DB_NAME" "$DB_USER"

apply_sql() {
  local label="$1"
  shift
  if ! "$@" -N -e "SELECT 1" >/dev/null 2>&1; then
    return 1
  fi
  printf 'Using %s\n' "$label"
  # --force: continue if optional GRANT host patterns are absent.
  {
    printf 'USE `%s`;\n' "$DB_NAME"
    cat "$SQL_FILE"
    printf 'GRANT SELECT, INSERT, UPDATE ON `%s`.`epc_api_clients` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
    printf 'GRANT SELECT, INSERT, UPDATE ON `%s`.`epc_api_clients` TO '\''%s'\''@'\''%%'\'';\n' "$DB_NAME" "$DB_USER"
    printf 'GRANT SELECT, INSERT ON `%s`.`sessions` TO '\''%s'\''@'\''localhost'\'';\n' "$DB_NAME" "$DB_USER"
    printf 'FLUSH PRIVILEGES;\n'
  } | "$@" --force
  # Verify platform-shaped SELECT via same admin (table exists).
  if "$@" -N -e "SELECT 1 FROM \`${DB_NAME}\`.epc_api_clients LIMIT 1" >/dev/null 2>&1; then
    printf 'OK table present in %s.epc_api_clients\n' "$DB_NAME"
    return 0
  fi
  printf 'WARN: apply finished but SELECT verify failed\n' >&2
  return 1
}

if ! command -v mysql >/dev/null 2>&1; then
  printf 'mysql client not found\n' >&2
  exit 1
fi

tried=0
for defaults in \
  /etc/mysql/debian.cnf \
  /etc/mysql/mariadb.conf.d/debian.cnf \
  /root/.my.cnf \
  /etc/mysql/conf.d/root.cnf
do
  if [[ -r "$defaults" ]]; then
    tried=1
    if apply_sql "mysql --defaults-file=${defaults}" mysql --defaults-file="$defaults"; then
      printf '\nNext:\n'
      printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
      printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
      printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
      exit 0
    fi
    printf 'WARN: defaults-file auth failed for %s\n' "$defaults"
  fi
done

if apply_sql 'mysql socket' mysql --protocol=socket; then
  exit 0
fi
if apply_sql 'mysql -uroot' mysql -uroot; then
  exit 0
fi
if command -v sudo >/dev/null 2>&1 && apply_sql 'sudo mysql' sudo -n mysql; then
  exit 0
fi
if [[ -n "${ECOMAE_MYSQL_ADMIN_USER:-}" ]]; then
  export MYSQL_PWD="${ECOMAE_MYSQL_ADMIN_PASSWORD:-${ECOMAE_MYSQL_ROOT_PASSWORD:-}}"
  if apply_sql "mysql -u${ECOMAE_MYSQL_ADMIN_USER}" mysql -u"${ECOMAE_MYSQL_ADMIN_USER}" -h127.0.0.1; then
    unset MYSQL_PWD
    exit 0
  fi
  unset MYSQL_PWD
elif [[ -n "${ECOMAE_MYSQL_ROOT_PASSWORD:-}" ]]; then
  export MYSQL_PWD="${ECOMAE_MYSQL_ROOT_PASSWORD}"
  if apply_sql 'mysql -uroot (env password)' mysql -uroot -h127.0.0.1; then
    unset MYSQL_PWD
    exit 0
  fi
  unset MYSQL_PWD
fi

printf 'BLOCKED: no elevated MySQL auth could apply DDL (tried=%s).\n' "$tried" >&2
printf 'Alternatives:\n' >&2
printf '  1) Paste: bash scripts/cloudpanel_print_epc_api_clients_ddl.sh\n' >&2
printf '  2) Or align TenantRegistry to PHP app DB (when epc_api_clients already exists there):\n' >&2
printf '       bash scripts/cloudpanel_diagnose_smoke_db.sh\n' >&2
printf '       ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES \\\n' >&2
printf '         bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n' >&2
exit 1
