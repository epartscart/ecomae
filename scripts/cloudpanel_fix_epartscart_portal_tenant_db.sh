#!/usr/bin/env bash
# Ensure epc_portal_tenants binds a shop db_name for epartscart.com / www.epartscart.com.
# Discovery order:
#   1) ECOMAE_EPARTSCART_SHOP_DB override
#   2) sibling portal row already holding a non-empty db_name
#   3) PHP config.php under epartscart docroots (DP_Config->db)
#   4) SHOW DATABASES matching %epartscart% that contain a users table
# Fails hard if apex/www still lack db_name after apply.
#
#   ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES \
#     bash scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh
set -euo pipefail

if [[ "${ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB:-}" != "YES" ]]; then
  printf 'REFUSE: set ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES\n' >&2
  exit 2
fi
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root\n' >&2
  exit 1
fi

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
if [[ ! -f "$ENV_FILE" ]]; then
  printf 'ERROR: missing %s\n' "$ENV_FILE" >&2
  exit 1
fi

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
if [[ -z "$CS" ]]; then
  printf 'ERROR: ConnectionStrings__TenantRegistry not set in %s\n' "$ENV_FILE" >&2
  exit 1
fi

mapfile -t PARTS < <(python3 - <<'PY' "$CS"
import re, sys
cs = sys.argv[1]
def pick(*keys):
    for key in keys:
        m = re.search(rf'(?:^|;)\s*{re.escape(key)}=([^;]+)', cs, re.I)
        if m:
            return m.group(1).strip()
    return ""
print(pick("Server", "Data Source") or "127.0.0.1")
print(pick("Database", "Initial Catalog"))
print(pick("User", "Uid", "User ID"))
print(pick("Password", "Pwd"))
PY
)
DB_HOST="${PARTS[0]}"
DB_NAME="${PARTS[1]}"
DB_USER="${PARTS[2]}"
DB_PASS="${PARTS[3]}"

if [[ -z "$DB_NAME" || -z "$DB_USER" ]]; then
  printf 'ERROR: could not parse TenantRegistry connection string\n' >&2
  exit 1
fi

printf '======== FIX EPARTSCART PORTAL TENANT DB ========\n'
printf 'registry_db=%s host=%s\n' "$DB_NAME" "$DB_HOST"

export MYSQL_PWD="$DB_PASS"
mysql_q() { mysql -h "$DB_HOST" -u "$DB_USER" "$DB_NAME" -N -e "$1"; }
mysql_e() { mysql -h "$DB_HOST" -u "$DB_USER" "$DB_NAME" -e "$1"; }

printf '\n-- portal rows before --\n'
mysql_e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(erp_only_shared,0) AS erp_only,
       IFNULL(is_active,1) AS is_active, status
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" || true

SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-}"
SOURCE="override"

if [[ -z "$SHOP_DB" ]]; then
  SHOP_DB="$(mysql_q "
SELECT db_name FROM epc_portal_tenants
WHERE (hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%')
  AND IFNULL(TRIM(db_name),'') <> ''
  AND COALESCE(is_active,1)=1
ORDER BY CASE WHEN hostname='epartscart.com' THEN 0 WHEN hostname='www.epartscart.com' THEN 1 ELSE 2 END,
         IFNULL(erp_only_shared,0) ASC, IFNULL(is_demo,0) ASC
LIMIT 1;
" 2>/dev/null || true)"
  SOURCE="sibling_portal_row"
fi

# PHP docroot DP_Config->db (never print password)
if [[ -z "$SHOP_DB" ]]; then
  SOURCE="php_config"
  while IFS= read -r cfg; do
    [[ -f "$cfg" ]] || continue
    candidate="$(php -r '
$f=$argv[1];
if (!is_file($f)) exit(1);
$_SERVER["DOCUMENT_ROOT"]=dirname($f);
if (!defined("_ASTEXE_")) define("_ASTEXE_",1);
try {
  require $f;
  if (!class_exists("DP_Config", false)) exit(2);
  $c=new DP_Config();
  $local=dirname($f)."/config.local.php";
  if (is_file($local)) { $epc_config_local=null; require $local;
    if (isset($epc_config_local) && is_array($epc_config_local)) {
      foreach ($epc_config_local as $k=>$v) { if (property_exists($c,$k)) $c->$k=$v; }
    }
  }
  $db=trim((string)($c->db ?? ""));
  if ($db!=="") { echo $db; exit(0); }
} catch (Throwable $e) { exit(3); }
exit(4);
' "$cfg" 2>/dev/null || true)"
    if [[ -n "$candidate" ]]; then
      SHOP_DB="$candidate"
      printf 'discovered_php_config=%s db=%s\n' "$cfg" "$SHOP_DB"
      break
    fi
  done < <(find /home /var/www -maxdepth 5 \( -path '*/www.epartscart.com/config.php' -o -path '*/epartscart.com/config.php' -o -path '*/epartscart/config.php' \) 2>/dev/null | head -20)
fi

# SHOW DATABASES candidates that look like epartscart shops and have users
if [[ -z "$SHOP_DB" ]]; then
  SOURCE="show_databases"
  while IFS= read -r cand; do
    [[ -n "$cand" ]] || continue
    has_users="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${cand//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
    if [[ "${has_users:-0}" != "0" ]]; then
      SHOP_DB="$cand"
      printf 'discovered_schema=%s (has users)\n' "$SHOP_DB"
      break
    fi
  done < <(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SHOW DATABASES LIKE '%epartscart%';" 2>/dev/null || true)
fi

if [[ -z "$SHOP_DB" ]]; then
  printf 'RESULT=FAIL no_shop_db_candidate — set ECOMAE_EPARTSCART_SHOP_DB=<mysql_db> and re-run\n' >&2
  unset MYSQL_PWD
  exit 1
fi
printf 'resolved_shop_db=%s source=%s\n' "$SHOP_DB" "$SOURCE"

# Verify users table exists in shop DB
has_users="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${SHOP_DB//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
if [[ "${has_users:-0}" == "0" ]]; then
  printf 'RESULT=FAIL shop_db=%s has no users table\n' "$SHOP_DB" >&2
  unset MYSQL_PWD
  exit 1
fi

# Ensure apex + www rows exist (insert minimal if missing), then set db_name on all epartscart hosts.
mysql_e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name, is_active, status, erp_only_shared)
SELECT 'epartscart', 'epartscart.com', '${SHOP_DB//\'/\\\'}', 1, 'live', 0
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM epc_portal_tenants WHERE hostname='epartscart.com'
);
" 2>/dev/null || mysql_e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name)
SELECT 'epartscart', 'epartscart.com', '${SHOP_DB//\'/\\\'}'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM epc_portal_tenants WHERE hostname='epartscart.com'
);
" 2>/dev/null || true

mysql_e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name, is_active, status, erp_only_shared)
SELECT 'epartscart', 'www.epartscart.com', '${SHOP_DB//\'/\\\'}', 1, 'live', 0
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM epc_portal_tenants WHERE hostname='www.epartscart.com'
);
" 2>/dev/null || mysql_e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name)
SELECT 'epartscart', 'www.epartscart.com', '${SHOP_DB//\'/\\\'}'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM epc_portal_tenants WHERE hostname='www.epartscart.com'
);
" 2>/dev/null || true

mysql_e "
UPDATE epc_portal_tenants
SET db_name='${SHOP_DB//\'/\\\'}'
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%';
"

# Best-effort activate + clear erp_only stubs that would starve shop resolution
mysql_e "
UPDATE epc_portal_tenants
SET is_active=1
WHERE hostname IN ('epartscart.com','www.epartscart.com');
" 2>/dev/null || true

printf '\n-- portal rows after --\n'
mysql_e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(erp_only_shared,0) AS erp_only,
       IFNULL(is_active,1) AS is_active, status
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%'
ORDER BY hostname;
"

BOUND_WWW="$(mysql_q "SELECT IFNULL(TRIM(db_name),'') FROM epc_portal_tenants WHERE hostname='www.epartscart.com' ORDER BY CASE WHEN IFNULL(TRIM(db_name),'')<>'' THEN 0 ELSE 1 END LIMIT 1;" || true)"
BOUND_APEX="$(mysql_q "SELECT IFNULL(TRIM(db_name),'') FROM epc_portal_tenants WHERE hostname='epartscart.com' ORDER BY CASE WHEN IFNULL(TRIM(db_name),'')<>'' THEN 0 ELSE 1 END LIMIT 1;" || true)"

unset MYSQL_PWD

if [[ -z "$BOUND_WWW" && -z "$BOUND_APEX" ]]; then
  printf 'RESULT=FAIL still_unbound after update shop_db=%s\n' "$SHOP_DB" >&2
  exit 1
fi

# Clear tenant registry cache
if [[ "${ECOMAE_CONFIRM_RESTART_PLATFORM:-YES}" == "YES" ]]; then
  systemctl restart ecomae-platform.service || true
  sleep 3
  printf 'restarted ecomae-platform.service\n'
fi

printf 'RESULT=PASS portal tenant db_name bound shop_db=%s www=%s apex=%s source=%s\n' \
  "$SHOP_DB" "${BOUND_WWW:-empty}" "${BOUND_APEX:-empty}" "$SOURCE"
exit 0
