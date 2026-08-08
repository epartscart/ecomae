#!/usr/bin/env bash
# Read-only dump of ePartsCart portal tenant binding. Always exits 0 with RESULT=DUMP_OK
# so operators can paste evidence even when bind cannot discover a shop DB.
#
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_EPARTSCART_PORTAL_DUMP_NOW.sh'
#   curl -fsSL "$URL" | bash 2>&1 | tee /root/epartscart-portal-dump.log
#   grep -E 'RESULT=|ROW=|RESOLVER_|SHOW_DB=|EMAIL_HIT=|PHP_DB=|secret=' /root/epartscart-portal-dump.log
set -euo pipefail

printf '======== EPARTSCART PORTAL_DUMP_NOW ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
[[ "$(id -u)" -eq 0 ]] || { printf 'RESULT=FAIL must_run_as_root\n'; exit 1; }

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
[[ -f "$ENV_FILE" ]] || { printf 'RESULT=FAIL missing_env\n'; exit 1; }
CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || { printf 'RESULT=FAIL missing_tenant_registry_cs\n'; exit 1; }

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
printf 'registry_db=%s mysql_host=%s user=%s\n' "$REG_DB" "$DB_HOST" "$DB_USER"
export MYSQL_PWD="$DB_PASS"
DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}"
EMAIL_SQL="${DIAG_EMAIL//\'/\\\'}"

printf '\n-- portal rows --\n'
mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CONCAT('ROW=', IFNULL(site_key,''), '|', IFNULL(hostname,''), '|db=', IFNULL(db_name,''),
  '|status=', IFNULL(status,''), '|active=', IFNULL(is_active,1), '|erp_only=', IFNULL(erp_only_shared,0))
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" 2>&1 || printf 'ROW=QUERY_FAILED\n'

printf '\n-- resolver (status live/dns_pending + db_name) --\n'
mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CONCAT('RESOLVER=', IFNULL(hostname,''), '|', IFNULL(db_name,''), '|', IFNULL(status,''))
FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live') AND COALESCE(is_active,1)=1
  AND IFNULL(TRIM(db_name),'')<>''
ORDER BY CASE WHEN hostname='www.epartscart.com' THEN 0 ELSE 1 END LIMIT 3;
" 2>&1 || true
RESOLVED="$(mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "
SELECT CONCAT(hostname,'|',db_name,'|',status) FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live') AND COALESCE(is_active,1)=1
  AND IFNULL(TRIM(db_name),'')<>''
ORDER BY CASE WHEN hostname='www.epartscart.com' THEN 0 ELSE 1 END LIMIT 1;
" 2>/dev/null || true)"
printf 'RESOLVER_ROW=%s\n' "${RESOLVED:-NONE}"

printf '\n-- SHOW DATABASES %%epartscart%% --\n'
while IFS= read -r d; do
  printf 'SHOW_DB=%s\n' "$d"
done < <(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SHOW DATABASES LIKE '%epartscart%';" 2>/dev/null || true)

printf '\n-- PHP config.php db --\n'
while IFS= read -r cfg; do
  [[ -f "$cfg" ]] || continue
  pdb="$(php -r '
$f=$argv[1]; $_SERVER["DOCUMENT_ROOT"]=dirname($f);
if (!defined("_ASTEXE_")) define("_ASTEXE_",1);
try { require $f; if (!class_exists("DP_Config", false)) exit(1);
  $c=new DP_Config(); $local=dirname($f)."/config.local.php";
  if (is_file($local)) { $epc_config_local=null; require $local;
    if (isset($epc_config_local)&&is_array($epc_config_local)) foreach($epc_config_local as $k=>$v) if(property_exists($c,$k)) $c->$k=$v; }
  $db=trim((string)($c->db??"")); if($db!==""){echo $db; exit(0);} } catch(Throwable $e){}
exit(1);
' "$cfg" 2>/dev/null || true)"
  printf 'PHP_DB path=%s db=%s\n' "$cfg" "${pdb:-NONE}"
done < <(find /home /var/www -maxdepth 5 \( -path '*/www.epartscart.com/config.php' -o -path '*/epartscart.com/config.php' \) 2>/dev/null | head -10)

printf '\n-- email hits for %s (first matches) --\n' "$DIAG_EMAIL"
hits=0
while IFS= read -r cand; do
  [[ -n "$cand" ]] || continue
  case "$cand" in information_schema|mysql|performance_schema|sys) continue ;; esac
  has="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${cand//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  [[ "${has:-0}" != "0" ]] || continue
  cnt="$(mysql -h "$DB_HOST" -u "$DB_USER" "$cand" -N -e "SELECT COUNT(*) FROM users WHERE LOWER(email)=LOWER('${EMAIL_SQL}');" 2>/dev/null || echo 0)"
  if [[ "${cnt:-0}" != "0" ]]; then
    printf 'EMAIL_HIT db=%s count=%s\n' "$cand" "$cnt"
    hits=$((hits + 1))
  fi
  [[ "$hits" -ge 5 ]] && break
done < <(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT schema_name FROM information_schema.schemata ORDER BY CASE WHEN schema_name LIKE '%epartscart%' THEN 0 WHEN schema_name LIKE '%taxofin%' THEN 1 ELSE 2 END, schema_name LIMIT 100;" 2>/dev/null || true)
[[ "$hits" -eq 0 ]] && printf 'EMAIL_HIT=NONE\n'

printf '\n-- public prove --\n'
BC="$(curl -sS -o /tmp/dump-bunch.json -w '%{http_code}' --max-time 20 -k \
  'https://www.epartscart.com/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$BC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' /tmp/dump-bunch.json; then
  printf 'PUBLIC_BUNCHES=BOUND code=%s\n' "$BC"
else
  printf 'PUBLIC_BUNCHES=UNBOUND code=%s\n' "$BC"
fi
REDIR="$(curl -sS -o /dev/null -w '%{redirect_url}' --max-time 20 -k \
  -X POST 'https://www.epartscart.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode 'contact=taxofin2025@gmail.com' \
  --data-urlencode 'password=__dump__' || true)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"

unset MYSQL_PWD

printf '\n======== PASTE_ME_BEGIN ========\n'
printf 'RESOLVER_ROW=%s\n' "${RESOLVED:-NONE}"
printf 'PUBLIC_BUNCHES_CODE=%s\n' "$BC"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
printf 'ECOMAE_EPARTSCART_SHOP_DB_HINT=set this to EMAIL_HIT db= or PHP_DB db= value then re-run BIND\n'
printf '======== PASTE_ME_END ========\n'
printf 'RESULT=DUMP_OK\n'
exit 0
