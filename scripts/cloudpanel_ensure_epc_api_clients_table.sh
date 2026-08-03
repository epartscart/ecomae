#!/usr/bin/env bash
# Create epc_api_clients once in the ASP.NET TenantRegistry database (e.g. asap).
# Tries: mysql socket/root/sudo, ECOMAE_MYSQL_ADMIN_*, then PHP (platform → PHP user → admin).
# On CREATE denial, prints paste-ready DDL+GRANT (also: cloudpanel_print_epc_api_clients_ddl.sh).
# Never prints secrets. Never removes PHP.
#
# Usage:
#   ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"

printf '%s\n' '== Ensure epc_api_clients table =='

if [[ "${ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE:-}" != "YES" ]]; then
  printf 'Refusing without confirmation.\n' >&2
  printf 'Run:\n' >&2
  printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n' >&2
  exit 2
fi

if [[ ! -f "$ENV_FILE" ]]; then
  printf 'Missing env file: %s\n' "$ENV_FILE" >&2
  exit 1
fi

if [[ -z "${ECOMAE_PHP_DOCROOT:-}" ]]; then
  for candidate in \
    /home/ecomae/htdocs/www.ecomae.com \
    /home/ecomae/htdocs \
    /home/cloudpanel/htdocs/www.ecomae.com \
    /var/www/www.ecomae.com
  do
    if [[ -f "$candidate/config.php" ]]; then
      export ECOMAE_PHP_DOCROOT="$candidate"
      printf 'Using PHP docroot: %s\n' "$ECOMAE_PHP_DOCROOT"
      break
    fi
  done
fi

DB_NAME="$(python3 - "$ENV_FILE" <<'PY'
import sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
db = ""
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
        if pk.strip().lower() in ("database", "initial catalog"):
            db = pv.strip()
print(db)
PY
)"

if [[ -z "$DB_NAME" ]]; then
  printf 'Could not parse Database= from ConnectionStrings__TenantRegistry in %s\n' "$ENV_FILE" >&2
  exit 1
fi

printf 'Target database: %s\n' "$DB_NAME"

SQL_FILE="$ROOT/scripts/sql/epc_api_clients.sql"
if [[ ! -f "$SQL_FILE" ]]; then
  printf 'Missing %s\n' "$SQL_FILE" >&2
  exit 1
fi

run_mysql_admin() {
  local label="$1"
  shift
  if "$@" -N -e "SELECT 1" >/dev/null 2>&1; then
    printf 'Using %s\n' "$label"
    {
      printf 'USE `%s`;\n' "$DB_NAME"
      cat "$SQL_FILE"
    } | "$@"
    printf 'OK created/verified via %s\n' "$label"
    printf '\nNext:\n'
    printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
    printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
    return 0
  fi
  return 1
}

if command -v mysql >/dev/null 2>&1; then
  if run_mysql_admin 'mysql socket' mysql --protocol=socket; then
    exit 0
  fi
  if run_mysql_admin 'mysql -uroot' mysql -uroot; then
    exit 0
  fi
  if command -v sudo >/dev/null 2>&1 && run_mysql_admin 'sudo mysql' sudo mysql; then
    exit 0
  fi
  if [[ -n "${ECOMAE_MYSQL_ADMIN_USER:-}" ]]; then
    # Password from env only; never printed.
    export MYSQL_PWD="${ECOMAE_MYSQL_ADMIN_PASSWORD:-${ECOMAE_MYSQL_ROOT_PASSWORD:-}}"
    if run_mysql_admin "mysql -u${ECOMAE_MYSQL_ADMIN_USER}" mysql -u"${ECOMAE_MYSQL_ADMIN_USER}" -h127.0.0.1; then
      unset MYSQL_PWD
      exit 0
    fi
    unset MYSQL_PWD
  elif [[ -n "${ECOMAE_MYSQL_ROOT_PASSWORD:-}" ]]; then
    export MYSQL_PWD="${ECOMAE_MYSQL_ROOT_PASSWORD}"
    if run_mysql_admin 'mysql -uroot (ECOMAE_MYSQL_ROOT_PASSWORD)' mysql -uroot -h127.0.0.1; then
      unset MYSQL_PWD
      exit 0
    fi
    unset MYSQL_PWD
  fi
  printf 'mysql present but elevated auth unavailable — trying PHP CREATE paths\n'
fi

export ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES
export ECOMAE_ASPNET_ENV_FILE="$ENV_FILE"
set +e
php "$ROOT/scripts/php/ensure_epc_api_clients_table.php"
rc=$?
set -e

if [[ "$rc" -ne 0 ]]; then
  printf '\nIf CREATE is denied for ecomae_aspnet, paste DDL as MySQL admin:\n' >&2
  bash "$ROOT/scripts/cloudpanel_print_epc_api_clients_ddl.sh" >&2 || true
  printf '\nOptional elevated env (not printed):\n' >&2
  printf '  ECOMAE_MYSQL_ADMIN_USER=root ECOMAE_MYSQL_ADMIN_PASSWORD=... \\\n' >&2
  printf '    ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n' >&2
  exit "$rc"
fi

printf '\nNext:\n'
printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
