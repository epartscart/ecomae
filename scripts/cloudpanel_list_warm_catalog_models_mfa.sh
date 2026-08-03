#!/usr/bin/env bash
# List MFA_IDs with non-empty epc_umapi_models rows (TenantRegistry DB).
# Never prints DB passwords. Never removes PHP.
#
# Usage:
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   bash scripts/cloudpanel_list_warm_catalog_models_mfa.sh
set -euo pipefail

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
SECTION="${ECOMAE_CATALOG_SECTION:-passenger}"
LIMIT="${ECOMAE_WARM_MFA_LIMIT:-10}"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

parsed="$(python3 - "$ENV_FILE" <<'PY'
import sys
raw = open(sys.argv[1], encoding="utf-8", errors="replace").read()
host, port, db, user, password = "127.0.0.1", "3306", "", "", ""
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
        if pk in ("server", "data source", "host") and pv:
            host = pv.split(",")[0]
        elif pk == "port" and pv:
            port = pv
        elif pk in ("database", "initial catalog") and pv:
            db = pv
        elif pk in ("user", "uid", "user id") and pv:
            user = pv
        elif pk in ("password", "pwd") and pv:
            password = pv
print(f"{host}\t{port}\t{db}\t{user}\t{password}")
PY
)"
HOST="$(printf '%s' "$parsed" | cut -f1)"
PORT="$(printf '%s' "$parsed" | cut -f2)"
DB_NAME="$(printf '%s' "$parsed" | cut -f3)"
DB_USER="$(printf '%s' "$parsed" | cut -f4)"
DB_PASS="$(printf '%s' "$parsed" | cut -f5-)"

if [[ -z "$DB_NAME" || -z "$DB_USER" ]]; then
  printf 'ERROR: could not parse ConnectionStrings__TenantRegistry from %s\n' "$ENV_FILE" >&2
  exit 1
fi

printf 'DB=%s section=%s (host/user redacted)\n' "$DB_NAME" "$SECTION"
SQL="$(printf "SELECT mfa_id, COUNT(*) AS c FROM epc_umapi_models WHERE section='%s' GROUP BY mfa_id ORDER BY c DESC LIMIT %s;" \
  "$SECTION" "$LIMIT")"

# Prefer mysql client; fall back to PHP PDO via a tiny inline script if needed.
if command -v mysql >/dev/null 2>&1; then
  MYSQL_PWD="$DB_PASS" mysql -h "$HOST" -P "$PORT" -u "$DB_USER" "$DB_NAME" -N -e "$SQL"
  exit 0
fi

php -r '
$h=$argv[1]; $p=$argv[2]; $d=$argv[3]; $u=$argv[4]; $w=$argv[5]; $sql=$argv[6];
$pdo=new PDO("mysql:host=$h;port=$p;dbname=$d;charset=utf8mb4",$u,$w,[PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION]);
foreach($pdo->query($sql) as $row){ echo $row[0],"\t",$row[1],"\n"; }
' "$HOST" "$PORT" "$DB_NAME" "$DB_USER" "$DB_PASS" "$SQL"
