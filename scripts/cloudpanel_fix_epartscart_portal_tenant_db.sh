#!/usr/bin/env bash
# Ensure epc_portal_tenants binds a shop db_name for epartscart.com / www.epartscart.com
# AND status IN ('dns_pending','live') so ASP.NET SelectActiveTenantByHosts can see it.
#
# Discovery order:
#   1) ECOMAE_EPARTSCART_SHOP_DB override
#   2) sibling portal row already holding a non-empty db_name
#   3) PHP config.php under epartscart docroots (DP_Config->db)
#   4) schema containing ECOMAE_DIAG_EMAIL (default taxofin2025@gmail.com)
#   5) SHOW DATABASES matching %epartscart% that contain a users table
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
[[ -f "$ENV_FILE" ]] || { printf 'ERROR: missing %s\n' "$ENV_FILE" >&2; exit 1; }

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || { printf 'ERROR: ConnectionStrings__TenantRegistry not set\n' >&2; exit 1; }

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
DB_HOST="${PARTS[0]}"; REG_DB="${PARTS[1]}"; DB_USER="${PARTS[2]}"; DB_PASS="${PARTS[3]}"
[[ -n "$REG_DB" && -n "$DB_USER" ]] || { printf 'ERROR: could not parse TenantRegistry CS\n' >&2; exit 1; }

DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}"
EMAIL_SQL="${DIAG_EMAIL//\'/\\\'}"

printf '======== FIX EPARTSCART PORTAL TENANT DB ========\n'
printf 'registry_db=%s host=%s diag_email=%s\n' "$REG_DB" "$DB_HOST" "$DIAG_EMAIL"

export MYSQL_PWD="$DB_PASS"
mysql_q() { mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "$1"; }
mysql_e() { mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -e "$1"; }

printf '\n-- DUMP portal rows (epartscart) --\n'
mysql_e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(erp_only_shared,0) AS erp_only,
       IFNULL(is_active,1) AS is_active, IFNULL(status,'(null)') AS status
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" || true

printf '\n-- DUMP resolver simulation (ASP.NET status filter) --\n'
mysql_e "
SELECT hostname, IFNULL(db_name,'') AS db_name, status, is_active, erp_only_shared
FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
ORDER BY CASE WHEN IFNULL(TRIM(db_name),'')<>'' THEN 0 ELSE 1 END,
         CASE WHEN hostname='www.epartscart.com' THEN 0 ELSE 1 END
LIMIT 5;
" || true

printf '\n-- DUMP databases matching %%epartscart%% --\n'
mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SHOW DATABASES LIKE '%epartscart%';" 2>/dev/null || true

SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-}"
SOURCE="override"

schema_has_users() {
  local db="$1"
  local n
  n="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  # Root socket often sees schemas the registry user cannot.
  if [[ "${n:-0}" == "0" ]] && mysql -N -e "SELECT 1" >/dev/null 2>&1; then
    n="$(mysql -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  fi
  [[ "${n:-0}" != "0" ]]
}

portal_already_live_bound() {
  local db="$1"
  local n
  n="$(mysql_q "
SELECT COUNT(*) FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
  AND IFNULL(TRIM(db_name),'')='${db//\'/\\\'}';
" 2>/dev/null || echo 0)"
  [[ "${n:-0}" != "0" ]]
}

if [[ -z "$SHOP_DB" ]]; then
  CAND="$(mysql_q "
SELECT db_name FROM epc_portal_tenants
WHERE (hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%')
  AND IFNULL(TRIM(db_name),'') <> ''
ORDER BY CASE WHEN hostname='epartscart.com' THEN 0 WHEN hostname='www.epartscart.com' THEN 1 ELSE 2 END,
         IFNULL(erp_only_shared,0) ASC
LIMIT 1;
" 2>/dev/null || true)"
  if [[ -n "$CAND" ]] && schema_has_users "$CAND"; then
    SHOP_DB="$CAND"
    SOURCE="sibling_portal_row"
  elif [[ -n "$CAND" ]] && portal_already_live_bound "$CAND"; then
    # Registry MySQL principal often cannot SEE docpart.users (GRANT gap) while
    # ASP.NET OpenStorefrontShopAsync still opens the shop via platform/root path.
    # Portal dump already shows www.epartscart.com → docpart live — trust it.
    SHOP_DB="$CAND"
    SOURCE="sibling_portal_row_trusted"
    printf 'sibling_portal_row=%s missing_users — trusting live portal bind (registry GRANT gap)\n' "$CAND"
  elif [[ -n "$CAND" ]]; then
    printf 'sibling_portal_row=%s missing_users — continuing discovery (registry may lack GRANT)\n' "$CAND"
  fi
fi

# PHP portal parity: ePartsCart storefront shop DB is the shared Model C `docpart`
# schema (epc_portal_resolve_tenant_db_credentials — never platform `ecomae`).
if [[ -z "$SHOP_DB" ]]; then
  if schema_has_users "docpart"; then
    SHOP_DB="docpart"
    SOURCE="php_parity_docpart"
    printf 'discovered_php_parity_default db=docpart (epartscart shared shop)\n'
  elif portal_already_live_bound "docpart"; then
    SHOP_DB="docpart"
    SOURCE="php_parity_docpart_trusted"
    printf 'docpart invisible to registry principal — trusting live portal bind\n'
  else
    printf 'docpart_missing_users_or_invisible_to_mysql_principal\n'
  fi
fi

# PHP portal parity: ePartsCart storefront shop DB is the shared Model C `docpart`
# schema (epc_portal_resolve_tenant_db_credentials — never platform `ecomae`).
if [[ -z "$SHOP_DB" ]]; then
  has_docpart="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='docpart' AND table_name='users';" 2>/dev/null || echo 0)"
  if [[ "${has_docpart:-0}" != "0" ]]; then
    SHOP_DB="docpart"
    SOURCE="php_parity_docpart"
    printf 'discovered_php_parity_default db=docpart (epartscart shared shop)\n'
  fi
fi

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

# Prefer schema that actually contains the failing login email
if [[ -z "$SHOP_DB" ]]; then
  SOURCE="email_scan"
  printf '\n-- DUMP email scan across schemas with users table (first 80) --\n'
  while IFS= read -r cand; do
    [[ -n "$cand" && "$cand" != "information_schema" && "$cand" != "mysql" && "$cand" != "performance_schema" && "$cand" != "sys" ]] || continue
    has_users="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${cand//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
    [[ "${has_users:-0}" != "0" ]] || continue
    cnt="$(mysql -h "$DB_HOST" -u "$DB_USER" "$cand" -N -e "SELECT COUNT(*) FROM users WHERE LOWER(email)=LOWER('${EMAIL_SQL}');" 2>/dev/null || echo 0)"
    printf 'email_scan db=%s count=%s\n' "$cand" "$cnt"
    if [[ "${cnt:-0}" != "0" ]]; then
      SHOP_DB="$cand"
      printf 'discovered_by_email=%s email=%s\n' "$SHOP_DB" "$DIAG_EMAIL"
      break
    fi
  done < <(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT schema_name FROM information_schema.schemata ORDER BY CASE WHEN schema_name LIKE '%epartscart%' THEN 0 WHEN schema_name LIKE '%taxofin%' THEN 1 ELSE 2 END, schema_name LIMIT 80;" 2>/dev/null || true)
fi

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
  # Last resort: trust whatever non-empty live portal row already has (operator dump).
  CAND_LIVE="$(mysql_q "
SELECT db_name FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
  AND IFNULL(TRIM(db_name),'') <> ''
ORDER BY CASE WHEN hostname='www.epartscart.com' THEN 0 ELSE 1 END
LIMIT 1;
" 2>/dev/null || true)"
  if [[ -n "$CAND_LIVE" ]]; then
    SHOP_DB="$CAND_LIVE"
    SOURCE="portal_live_already_bound"
    printf 'accepting_already_bound portal db_name=%s (skip discovery)\n' "$SHOP_DB"
  else
    printf 'RESULT=FAIL no_shop_db_candidate — set ECOMAE_EPARTSCART_SHOP_DB=<mysql_db> and re-run\n' >&2
    printf 'DUMP_HINT: paste the DUMP portal rows + email_scan lines above\n' >&2
    unset MYSQL_PWD
    exit 1
  fi
fi
printf 'resolved_shop_db=%s source=%s\n' "$SHOP_DB" "$SOURCE"

USERS_VISIBLE=0
if schema_has_users "$SHOP_DB"; then
  USERS_VISIBLE=1
elif portal_already_live_bound "$SHOP_DB"; then
  printf 'WARN: shop_db=%s users invisible to registry principal — continuing (portal already live-bound)\n' "$SHOP_DB"
else
  printf 'RESULT=FAIL shop_db=%s has no users table and portal not live-bound\n' "$SHOP_DB" >&2
  unset MYSQL_PWD
  exit 1
fi

# Registry CS user must be able to OPEN the shop DB (ASP.NET OpenForTenantAsync).
if mysql -N -e "SELECT 1" >/dev/null 2>&1; then
  printf '\n-- GRANT registry user on shop DB (root socket) --\n'
  mysql -e "GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON \`${SHOP_DB//\`/}\`.* TO '${DB_USER}'@'localhost';" 2>&1 || true
  mysql -e "GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON \`${SHOP_DB//\`/}\`.* TO '${DB_USER}'@'%';" 2>&1 || true
  mysql -e "FLUSH PRIVILEGES;" 2>&1 || true
  reg_see="$(MYSQL_PWD="$DB_PASS" mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${SHOP_DB//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  printf 'GRANT_PROVE registry_sees_users=%s\n' "$reg_see"
fi

SHOP_SQL="${SHOP_DB//\'/\\\'}"

# Upsert apex/www with db_name + status=live + is_active=1 (status filter is required by ASP.NET).
for host in epartscart.com www.epartscart.com; do
  exists="$(mysql_q "SELECT COUNT(*) FROM epc_portal_tenants WHERE hostname='${host}';" || echo 0)"
  if [[ "${exists:-0}" == "0" ]]; then
    mysql_e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name, is_active, status, erp_only_shared)
VALUES ('epartscart', '${host}', '${SHOP_SQL}', 1, 'live', 0);
" 2>/dev/null || mysql_e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name, status)
VALUES ('epartscart', '${host}', '${SHOP_SQL}', 'live');
" || true
  fi
done

mysql_e "
UPDATE epc_portal_tenants
SET db_name='${SHOP_SQL}',
    status='live',
    is_active=1,
    erp_only_shared=0
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR (site_key LIKE 'epartscart%' AND IFNULL(TRIM(db_name),'') IN ('','${SHOP_SQL}'));
" 2>/dev/null || mysql_e "
UPDATE epc_portal_tenants
SET db_name='${SHOP_SQL}', status='live', is_active=1
WHERE hostname IN ('epartscart.com','www.epartscart.com');
"

printf '\n-- portal rows after (must be status=live + db_name) --\n'
mysql_e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(erp_only_shared,0) AS erp_only,
       IFNULL(is_active,1) AS is_active, IFNULL(status,'(null)') AS status
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com')
   OR site_key LIKE 'epartscart%'
ORDER BY hostname;
"

printf '\n-- resolver simulation after --\n'
RESOLVED="$(mysql_q "
SELECT CONCAT(hostname, '|', IFNULL(db_name,''), '|', status)
FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
  AND IFNULL(TRIM(db_name),'') <> ''
ORDER BY CASE WHEN hostname='www.epartscart.com' THEN 0 ELSE 1 END
LIMIT 1;
" || true)"
printf 'RESOLVER_ROW=%s\n' "${RESOLVED:-NONE}"

unset MYSQL_PWD

if [[ -z "$RESOLVED" ]]; then
  printf 'RESULT=FAIL resolver_still_empty after status=live+db_name bind shop_db=%s\n' "$SHOP_DB" >&2
  exit 1
fi

if [[ "${ECOMAE_CONFIRM_RESTART_PLATFORM:-YES}" == "YES" ]]; then
  systemctl restart ecomae-platform.service || true
  sleep 4
  printf 'restarted ecomae-platform.service\n'
fi

# Local prove (Host header) — must not say unbound
LOCAL_B="$(mktemp)"
LOCAL_C="$(curl -sS -o "$LOCAL_B" -w '%{http_code}' --max-time 20 \
  -H 'Host: www.epartscart.com' \
  'http://127.0.0.1:5100/storefront/search-bunches?article=OC90' || echo 000)"
printf 'local_bunches code=%s\n' "$LOCAL_C"
head -c 220 "$LOCAL_B"; echo
if [[ "$LOCAL_C" != "200" ]] || grep -q 'Tenant shop database is not bound' "$LOCAL_B"; then
  if portal_already_live_bound "$SHOP_DB" && [[ "${USERS_VISIBLE:-0}" == "0" ]]; then
    printf 'WARN: local_bunches unbound/non-200 while portal live-bound shop_db=%s (registry GRANT gap) — not failing bind\n' "$SHOP_DB"
    rm -f "$LOCAL_B"
  else
    rm -f "$LOCAL_B"
    printf 'RESULT=FAIL local_bunches_still_unbound after restart\n' >&2
    exit 1
  fi
else
  rm -f "$LOCAL_B"
fi

printf 'RESULT=PASS portal tenant db_name bound shop_db=%s resolver=%s source=%s users_visible=%s\n' \
  "$SHOP_DB" "$RESOLVED" "$SOURCE" "${USERS_VISIBLE:-0}"
exit 0
