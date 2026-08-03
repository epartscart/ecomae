#!/usr/bin/env bash
# Apply scripts/sql/epc_api_clients.sql + GRANTs to TenantRegistry DB using elevated MySQL auth.
# Tries (never prints passwords):
#   clpctl db:show:master-credentials (CloudPanel root@127.0.0.1)
#   --defaults-file=/etc/mysql/debian.cnf and other local defaults-files
#   socket / sudo mysql / ECOMAE_MYSQL_ADMIN_*
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

# GRANT only to existing mysql.user Host rows for DB_USER.
# Avoids ERROR 1410 (create user via GRANT) when e.g. user@'%' does not exist.
grant_existing_hosts() {
  local host grant_sql
  local -a hosts=()
  mapfile -t hosts < <(
    "$@" -N -e "SELECT Host FROM mysql.user WHERE User='${DB_USER}' ORDER BY Host" 2>/dev/null || true
  )
  if [[ "${#hosts[@]}" -eq 0 ]]; then
    printf 'WARN: no mysql.user rows for %s — table created; GRANT skipped\n' "$DB_USER" >&2
    printf 'HINT: issue smoke may still work if %s already has DB-level rights on %s\n' "$DB_USER" "$DB_NAME" >&2
    return 0
  fi
  for host in "${hosts[@]}"; do
    [[ -z "$host" ]] && continue
    # Host from mysql.user; allow only safe patterns (localhost, IPs, %, hostname chars).
    if [[ ! "$host" =~ ^[A-Za-z0-9._%-]+$ ]]; then
      printf 'WARN: skipping unexpected Host value for GRANT\n' >&2
      continue
    fi
    grant_sql="$(printf \
      'GRANT SELECT, INSERT, UPDATE ON `%s`.`epc_api_clients` TO '\''%s'\''@'\''%s'\''; GRANT SELECT, INSERT ON `%s`.`sessions` TO '\''%s'\''@'\''%s'\'';' \
      "$DB_NAME" "$DB_USER" "$host" "$DB_NAME" "$DB_USER" "$host")"
    if "$@" -e "$grant_sql" >/dev/null 2>&1; then
      printf 'OK GRANT to %s@%s\n' "$DB_USER" "$host"
    else
      printf 'WARN: GRANT failed for %s@%s (non-fatal if platform user can INSERT)\n' "$DB_USER" "$host" >&2
    fi
  done
  "$@" -e 'FLUSH PRIVILEGES;' >/dev/null 2>&1 || true
}

apply_sql() {
  local label="$1"
  shift
  if ! "$@" -N -e "SELECT 1" >/dev/null 2>&1; then
    return 1
  fi
  printf 'Using %s\n' "$label"
  {
    printf 'USE `%s`;\n' "$DB_NAME"
    cat "$SQL_FILE"
  } | "$@" --force
  grant_existing_hosts "$@"
  if "$@" -N -e "SELECT 1 FROM \`${DB_NAME}\`.epc_api_clients LIMIT 1" >/dev/null 2>&1; then
    printf 'OK table present in %s.epc_api_clients\n' "$DB_NAME"
    return 0
  fi
  printf 'WARN: apply finished but SELECT verify failed\n' >&2
  return 1
}

finish_ok() {
  printf '\nNext:\n'
  printf '  ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh\n'
  printf '  ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \\\n'
  printf '    bash scripts/cloudpanel_issue_smoke_credentials.sh\n'
  exit 0
}

if ! command -v mysql >/dev/null 2>&1; then
  printf 'mysql client not found\n' >&2
  exit 1
fi

# CloudPanel: master credentials are root@127.0.0.1 (TCP), not unix_socket/debian.cnf.
if command -v clpctl >/dev/null 2>&1; then
  printf 'Trying clpctl db:show:master-credentials (password not printed)\n'
  set +e
  clp_out="$(clpctl db:show:master-credentials 2>/dev/null)"
  clp_rc=$?
  set -e
  if [[ "$clp_rc" -eq 0 && -n "$clp_out" ]]; then
    set +e
    mapfile -t clp_vars < <(
      ECOMAE_CLPCTL_MASTER_TEXT="$clp_out" python3 <<'PY'
import os, re, sys
text = os.environ.get("ECOMAE_CLPCTL_MASTER_TEXT", "")
host, user, password, port = "127.0.0.1", "root", "", "3306"

def grab(label: str) -> str:
    m = re.search(rf"(?im)^\s*\|?\s*{re.escape(label)}\s*\|?\s*[|:]\s*\|?\s*([^\s|]+)", text)
    if m:
        return m.group(1).strip()
    m = re.search(rf"(?i){re.escape(label)}\s*[:=]\s*(\S+)", text)
    return m.group(1).strip() if m else ""

h = grab("Host")
u = grab("User Name") or grab("Username") or grab("User")
p = grab("Password")
po = grab("Port")
if h:
    host = h
if u:
    user = u
if p:
    password = p
if po and po.isdigit():
    port = po
m5 = re.search(r"-p'([^']+)'", text) or re.search(r'-p"([^"]+)"', text)
m3 = re.search(r"-h['\"]?([^'\"\s]+)", text)
m4 = re.search(r"-u['\"]?([^'\"\s]+)", text)
if m3:
    host = m3.group(1)
if m4:
    user = m4.group(1)
if m5:
    password = m5.group(1)
if not password:
    sys.exit(1)
print(host)
print(user)
print(port)
print(password)
PY
    )
    parse_rc=$?
    set -e
    if [[ "$parse_rc" -eq 0 && "${#clp_vars[@]}" -ge 4 && -n "${clp_vars[3]:-}" ]]; then
      export MYSQL_PWD="${clp_vars[3]}"
      if apply_sql "clpctl master (${clp_vars[1]}@${clp_vars[0]})" \
        mysql -h"${clp_vars[0]}" -P"${clp_vars[2]}" -u"${clp_vars[1]}"; then
        unset MYSQL_PWD
        finish_ok
      fi
      unset MYSQL_PWD
      printf 'WARN: clpctl master credentials did not authenticate for DDL\n'
    else
      printf 'WARN: could not parse clpctl db:show:master-credentials\n'
    fi
  else
    printf 'WARN: clpctl db:show:master-credentials failed\n'
  fi
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
      finish_ok
    fi
    printf 'WARN: defaults-file auth failed for %s\n' "$defaults"
  fi
done

if apply_sql 'mysql socket' mysql --protocol=socket; then
  finish_ok
fi
if apply_sql 'mysql -uroot -h127.0.0.1' mysql -uroot -h127.0.0.1; then
  finish_ok
fi
if apply_sql 'mysql -uroot' mysql -uroot; then
  finish_ok
fi
if command -v sudo >/dev/null 2>&1 && apply_sql 'sudo mysql' sudo -n mysql; then
  finish_ok
fi
if [[ -n "${ECOMAE_MYSQL_ADMIN_USER:-}" ]]; then
  export MYSQL_PWD="${ECOMAE_MYSQL_ADMIN_PASSWORD:-${ECOMAE_MYSQL_ROOT_PASSWORD:-}}"
  if apply_sql "mysql -u${ECOMAE_MYSQL_ADMIN_USER}" mysql -u"${ECOMAE_MYSQL_ADMIN_USER}" -h127.0.0.1; then
    unset MYSQL_PWD
    finish_ok
  fi
  unset MYSQL_PWD
elif [[ -n "${ECOMAE_MYSQL_ROOT_PASSWORD:-}" ]]; then
  export MYSQL_PWD="${ECOMAE_MYSQL_ROOT_PASSWORD}"
  if apply_sql 'mysql -uroot (env password)' mysql -uroot -h127.0.0.1; then
    unset MYSQL_PWD
    finish_ok
  fi
  unset MYSQL_PWD
fi

printf 'BLOCKED: no elevated MySQL auth could apply DDL (defaults_tried=%s).\n' "$tried" >&2
printf 'On CloudPanel, ensure `clpctl db:show:master-credentials` works as root.\n' >&2
printf 'Alternatives:\n' >&2
printf '  1) Paste: bash scripts/cloudpanel_print_epc_api_clients_ddl.sh\n' >&2
printf '  2) Use PHP DP_Config as TenantRegistry (table already on PHP db):\n' >&2
printf '       ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES \\\n' >&2
printf '         ECOMAE_CONFIRM_RESTART_PLATFORM=YES \\\n' >&2
printf '         bash scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh\n' >&2
printf '  3) Align Database= only (needs GRANT ecomae_aspnet → PHP db):\n' >&2
printf '       ECOMAE_CONFIRM_ALIGN_TENANT_REGISTRY_TO_PHP_DB=YES \\\n' >&2
printf '         bash scripts/cloudpanel_align_tenant_registry_to_php_db.sh\n' >&2
exit 1
