#!/usr/bin/env bash
# SELF-CONTAINED — no git checkout required.
# Discovers a real ePartsCart shop schema (users table), binds portal rows
# www.epartscart.com / epartscart.com → that DB with status=live, GRANTs the
# ASP.NET registry MySQL user onto the shop DB, restarts platform, proves gates.
#
# 2026-08-09 fail: hardcoding docpart then checking users via registry user
# printed missing_users_table while portal already had docpart|live — either
# registry user lacks privilege on docpart, or shop data lives elsewhere.
# CloudPanel: discover via clpctl db:show:master-credentials (TCP root), GRANT, bind.
# unix_socket `mysql` as OS root often fails on this host.
#
# Paste as root (must paste RESULT= / PASTE_ME lines back):
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/finish-pending-epartscart-lifeos-7b3b/scripts/cloudpanel_EPARTSCART_BIND_DOCPART_STANDALONE.sh'
#   TMP=/tmp/epartscart-bind-docpart-standalone.sh
#   curl -fsSL "$URL" -o "$TMP"
#   grep -q BIND_DOCPART_STANDALONE "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   bash "$TMP" 2>&1 | tee /root/epartscart-bind-docpart-standalone.log
#   grep -E 'RESULT=|BOUND_|GATE_|RESOLVER_|POST_LOGIN|ROW_|PASTE_ME_|EMAIL_HIT|discovered_|GRANT_|shop_db|ERROR' /root/epartscart-bind-docpart-standalone.log | tail -100
set -euo pipefail

printf '======== EPARTSCART BIND_DOCPART_STANDALONE ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root"

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
[[ -f "$ENV_FILE" ]] || die "missing_env $ENV_FILE"

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || die "missing_TenantRegistry_connection_string"

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
[[ -n "$REG_DB" && -n "$DB_USER" ]] || die "could_not_parse_connection_string"
DIAG_EMAIL="${ECOMAE_DIAG_EMAIL:-taxofin2025@gmail.com}"
EMAIL_SQL="${DIAG_EMAIL//\'/\\\'}"
printf 'registry_db=%s mysql_host=%s registry_user=%s diag_email=%s\n' \
  "$REG_DB" "$DB_HOST" "$DB_USER" "$DIAG_EMAIL"

# Elevated MySQL for discovery/GRANT.
# CloudPanel master is usually root@127.0.0.1 via `clpctl db:show:master-credentials`
# — unix_socket `mysql` as OS root often fails (this host: mysql_root_socket=NO).
ROOT_OK=0
ROOT_LABEL=""
ROOT_MYSQL_PWD=""
# shellcheck disable=SC2034
declare -a ROOT_MYSQL=()

try_elevated_mysql() {
  local label="$1"; shift
  local saved_pwd="${MYSQL_PWD-}"
  if [[ -n "${ROOT_MYSQL_PWD}" ]]; then
    export MYSQL_PWD="$ROOT_MYSQL_PWD"
  else
    unset MYSQL_PWD 2>/dev/null || true
  fi
  if "$@" -N -e "SELECT 1" >/dev/null 2>&1; then
    ROOT_OK=1
    ROOT_LABEL="$label"
    ROOT_MYSQL=("$@")
    printf 'mysql_elevated=YES via=%s\n' "$label"
    if [[ -n "$saved_pwd" ]]; then export MYSQL_PWD="$saved_pwd"; else unset MYSQL_PWD 2>/dev/null || true; fi
    return 0
  fi
  if [[ -n "$saved_pwd" ]]; then export MYSQL_PWD="$saved_pwd"; else unset MYSQL_PWD 2>/dev/null || true; fi
  return 1
}

if try_elevated_mysql "unix_socket" mysql; then
  :
elif try_elevated_mysql "unix_socket_protocol" mysql --protocol=socket; then
  :
fi

if [[ "$ROOT_OK" != "1" ]] && command -v clpctl >/dev/null 2>&1; then
  printf 'trying_clpctl_db_show_master_credentials\n'
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
      ROOT_MYSQL_PWD="${clp_vars[3]}"
      try_elevated_mysql "clpctl_master(${clp_vars[1]}@${clp_vars[0]}:${clp_vars[2]})" \
        mysql -h"${clp_vars[0]}" -P"${clp_vars[2]}" -u"${clp_vars[1]}" || true
      if [[ "$ROOT_OK" != "1" ]]; then
        ROOT_MYSQL_PWD=""
        printf 'WARN clpctl_master_auth_failed\n'
      fi
    else
      printf 'WARN clpctl_master_parse_failed\n'
    fi
  else
    printf 'WARN clpctl_db_show_master_credentials_failed rc=%s\n' "${clp_rc:-na}"
  fi
fi

if [[ "$ROOT_OK" != "1" ]]; then
  for defaults in \
    /etc/mysql/debian.cnf \
    /etc/mysql/mariadb.conf.d/debian.cnf \
    /root/.my.cnf \
    /etc/mysql/conf.d/root.cnf
  do
    [[ -r "$defaults" ]] || continue
    try_elevated_mysql "defaults-file:${defaults}" mysql --defaults-file="$defaults" && break
  done
fi

if [[ "$ROOT_OK" != "1" ]]; then
  try_elevated_mysql "root_tcp" mysql -uroot -h127.0.0.1 || true
fi
if [[ "$ROOT_OK" != "1" ]]; then
  try_elevated_mysql "root_local" mysql -uroot || true
fi
if [[ "$ROOT_OK" != "1" && -n "${ECOMAE_MYSQL_ROOT_PASSWORD:-}" ]]; then
  ROOT_MYSQL_PWD="$ECOMAE_MYSQL_ROOT_PASSWORD"
  try_elevated_mysql "env_ECOMAE_MYSQL_ROOT_PASSWORD" mysql -uroot -h127.0.0.1 || true
  [[ "$ROOT_OK" == "1" ]] || ROOT_MYSQL_PWD=""
fi
if [[ "$ROOT_OK" != "1" && -n "${ECOMAE_MYSQL_ADMIN_USER:-}" ]]; then
  ROOT_MYSQL_PWD="${ECOMAE_MYSQL_ADMIN_PASSWORD:-${ECOMAE_MYSQL_ROOT_PASSWORD:-}}"
  try_elevated_mysql "env_admin_${ECOMAE_MYSQL_ADMIN_USER}" \
    mysql -u"${ECOMAE_MYSQL_ADMIN_USER}" -h127.0.0.1 || true
  [[ "$ROOT_OK" == "1" ]] || ROOT_MYSQL_PWD=""
fi

if [[ "$ROOT_OK" == "1" ]]; then
  printf 'mysql_root_socket=%s\n' "$([[ "$ROOT_LABEL" == unix_socket* ]] && echo YES || echo NO)"
  printf 'mysql_elevated_label=%s\n' "$ROOT_LABEL"
else
  printf 'mysql_root_socket=NO\n'
  printf 'mysql_elevated=NO — discovery limited; GRANT impossible without clpctl/root\n'
fi

mysql_reg() {
  MYSQL_PWD="$DB_PASS" mysql -h "$DB_HOST" -u "$DB_USER" "$@"
}
mysql_priv() {
  if [[ "$ROOT_OK" == "1" ]]; then
    local saved_pwd="${MYSQL_PWD-}"
    if [[ -n "${ROOT_MYSQL_PWD}" ]]; then
      export MYSQL_PWD="$ROOT_MYSQL_PWD"
    else
      unset MYSQL_PWD 2>/dev/null || true
    fi
    "${ROOT_MYSQL[@]}" "$@"
    local rc=$?
    if [[ -n "$saved_pwd" ]]; then export MYSQL_PWD="$saved_pwd"; else unset MYSQL_PWD 2>/dev/null || true; fi
    return "$rc"
  else
    MYSQL_PWD="$DB_PASS" mysql -h "$DB_HOST" -u "$DB_USER" "$@"
  fi
}

schema_has_users() {
  local db="$1"
  local n
  n="$(mysql_priv -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  [[ "${n:-0}" != "0" ]]
}

email_count_in() {
  local db="$1"
  mysql_priv -N -e "SELECT COUNT(*) FROM \`${db//\`/}\`.users WHERE LOWER(email)=LOWER('${EMAIL_SQL}');" 2>/dev/null || echo 0
}

printf '\n-- before (registry) --\n'
mysql_reg "$REG_DB" -e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(status,'') AS status, IFNULL(is_active,1) AS is_active,
       IFNULL(db_user,'') AS db_user
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" 2>&1 | sed 's/^/ROW_BEFORE /' || true

SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-}"
SOURCE="override"

# 1) Prefer docpart when it actually has users (root can see it).
if [[ -z "$SHOP_DB" ]] && schema_has_users "docpart"; then
  SHOP_DB="docpart"
  SOURCE="docpart_has_users"
  printf 'discovered_docpart_has_users=YES\n'
elif [[ -z "$SHOP_DB" ]]; then
  printf 'discovered_docpart_has_users=NO\n'
fi

# 2) PHP config.php under epartscart docroots
if [[ -z "$SHOP_DB" ]]; then
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
    if [[ -n "$candidate" ]] && schema_has_users "$candidate"; then
      SHOP_DB="$candidate"
      SOURCE="php_config"
      printf 'discovered_php_config=%s db=%s\n' "$cfg" "$SHOP_DB"
      break
    fi
    printf 'php_config_skip=%s candidate=%s\n' "$cfg" "${candidate:-none}"
  done < <(find /home /var/www -maxdepth 6 \( -path '*/www.epartscart.com/config.php' -o -path '*/epartscart.com/config.php' -o -path '*/epartscart/config.php' \) 2>/dev/null | head -20)
fi

# 3) Email scan — prefer schemas containing the login email
if [[ -z "$SHOP_DB" ]]; then
  SOURCE="email_scan"
  printf '\n-- email scan (schemas with users) --\n'
  while IFS= read -r cand; do
    [[ -n "$cand" ]] || continue
    case "$cand" in
      information_schema|mysql|performance_schema|sys|ecomae) continue ;;
    esac
    schema_has_users "$cand" || continue
    cnt="$(email_count_in "$cand")"
    printf 'EMAIL_HIT_SCAN db=%s count=%s\n' "$cand" "$cnt"
    if [[ "${cnt:-0}" != "0" ]]; then
      # Prefer epartscart-ish names; still accept first email hit
      if [[ "$cand" == *epartscart* || "$cand" == "docpart" || -z "${EMAIL_HIT_DB:-}" ]]; then
        EMAIL_HIT_DB="$cand"
        printf 'EMAIL_HIT=%s email=%s\n' "$cand" "$DIAG_EMAIL"
        [[ "$cand" == *epartscart* || "$cand" == "docpart" ]] && break
      fi
    fi
  done < <(mysql_priv -N -e "
SELECT schema_name FROM information_schema.schemata
ORDER BY CASE
  WHEN schema_name='docpart' THEN 0
  WHEN schema_name LIKE '%epartscart%' THEN 1
  WHEN schema_name LIKE '%taxofin%' THEN 2
  ELSE 3 END, schema_name
LIMIT 120;" 2>/dev/null || true)
  if [[ -n "${EMAIL_HIT_DB:-}" ]]; then
    SHOP_DB="$EMAIL_HIT_DB"
    printf 'discovered_by_email=%s\n' "$SHOP_DB"
  fi
fi

# 4) Any %epartscart% schema with users
if [[ -z "$SHOP_DB" ]]; then
  SOURCE="show_databases"
  while IFS= read -r cand; do
    [[ -n "$cand" ]] || continue
    if schema_has_users "$cand"; then
      SHOP_DB="$cand"
      printf 'discovered_schema=%s (has users)\n' "$SHOP_DB"
      break
    fi
  done < <(mysql_priv -N -e "SHOW DATABASES LIKE '%epartscart%';" 2>/dev/null || true)
fi

# 5) Last resort: dump + actionable fail (elevated MySQL usually missing)
if [[ -z "$SHOP_DB" ]]; then
  printf '\n-- DUMP schemas (name only) --\n'
  mysql_priv -N -e "SHOW DATABASES;" 2>/dev/null | sed 's/^/SHOW_DB=/' || true
  if [[ "$ROOT_OK" != "1" ]]; then
    die "no_shop_db_with_users + no_elevated_mysql — CloudPanel needs clpctl master (not unix_socket). Re-run after: clpctl db:show:master-credentials works; or export ECOMAE_MYSQL_ROOT_PASSWORD=...; or ECOMAE_EPARTSCART_SHOP_DB=docpart once GRANT is possible"
  fi
  die "no_shop_db_with_users — set ECOMAE_EPARTSCART_SHOP_DB=<db> after DUMP; docpart missing users"
fi

printf 'resolved_shop_db=%s source=%s\n' "$SHOP_DB" "$SOURCE"
schema_has_users "$SHOP_DB" || die "shop_db=${SHOP_DB} still_missing_users_table"

# GRANT registry user onto shop schema so ASP.NET TenantRegistry CS can open it
if [[ "$ROOT_OK" == "1" ]]; then
  printf '\n-- GRANT registry user on shop DB (via %s) --\n' "$ROOT_LABEL"
  mysql_priv -e "GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON \`${SHOP_DB//\`/}\`.* TO '${DB_USER}'@'localhost';" 2>&1 | sed 's/^/GRANT_LOCAL /' || true
  mysql_priv -e "GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES ON \`${SHOP_DB//\`/}\`.* TO '${DB_USER}'@'%';" 2>&1 | sed 's/^/GRANT_PCT /' || true
  mysql_priv -e "FLUSH PRIVILEGES;" 2>&1 | sed 's/^/GRANT_FLUSH /' || true
  # Prove registry user can see users table now
  reg_see="$(MYSQL_PWD="$DB_PASS" mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${SHOP_DB//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  printf 'GRANT_PROVE registry_sees_users=%s\n' "$reg_see"
  [[ "${reg_see:-0}" != "0" ]] || die "grant_failed registry_user_still_cannot_see_${SHOP_DB}.users"
else
  printf 'GRANT_SKIP no_elevated_mysql — need clpctl db:show:master-credentials or ECOMAE_MYSQL_ROOT_PASSWORD\n'
  # If registry still cannot see shop.users, bind alone cannot fix CP login.
  reg_see="$(MYSQL_PWD="$DB_PASS" mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${SHOP_DB//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
  printf 'GRANT_PROVE registry_sees_users=%s (no_grant)\n' "$reg_see"
  if [[ "${reg_see:-0}" == "0" ]]; then
    die "no_elevated_mysql_for_grant — clpctl db:show:master-credentials required; registry_user=${DB_USER} cannot SEE ${SHOP_DB}.users"
  fi
fi

SHOP_SQL="${SHOP_DB//\'/\\\'}"

# Ensure portal rows + force db_name + status=live
for host in epartscart.com www.epartscart.com; do
  exists="$(mysql_reg "$REG_DB" -N -e "SELECT COUNT(*) FROM epc_portal_tenants WHERE hostname='${host}';" 2>/dev/null || echo 0)"
  if [[ "${exists:-0}" == "0" ]]; then
    mysql_reg "$REG_DB" -e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name, is_active, status, erp_only_shared)
VALUES ('epartscart', '${host}', '${SHOP_SQL}', 1, 'live', 0);
" 2>/dev/null || mysql_reg "$REG_DB" -e "
INSERT INTO epc_portal_tenants (site_key, hostname, db_name, status)
VALUES ('epartscart', '${host}', '${SHOP_SQL}', 'live');
" || true
  fi
done

mysql_reg "$REG_DB" -e "
UPDATE epc_portal_tenants
SET db_name='${SHOP_SQL}', status='live', is_active=1
WHERE hostname IN ('epartscart.com','www.epartscart.com');
" || die "update_epc_portal_tenants_failed"

mysql_reg "$REG_DB" -e "
UPDATE epc_portal_tenants SET erp_only_shared=0
WHERE hostname IN ('epartscart.com','www.epartscart.com');
" 2>/dev/null || true

printf '\n-- after --\n'
mysql_reg "$REG_DB" -e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(status,'') AS status, IFNULL(is_active,1) AS is_active
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" 2>&1 | sed 's/^/ROW_AFTER /' || true

RESOLVED="$(mysql_reg "$REG_DB" -N -e "
SELECT CONCAT(hostname,'|',IFNULL(db_name,''),'|',IFNULL(status,''))
FROM epc_portal_tenants
WHERE hostname IN ('www.epartscart.com','epartscart.com')
  AND status IN ('dns_pending','live')
  AND COALESCE(is_active,1)=1
  AND IFNULL(TRIM(db_name),'')<>''
ORDER BY CASE WHEN hostname='www.epartscart.com' THEN 0 ELSE 1 END
LIMIT 1;
" 2>/dev/null || true)"
printf 'RESOLVER_ROW=%s\n' "${RESOLVED:-NONE}"
[[ -n "$RESOLVED" ]] || die "resolver_still_empty_after_update"

# Email present in bound shop?
EMAIL_IN_SHOP="$(email_count_in "$SHOP_DB")"
printf 'EMAIL_IN_SHOP db=%s email=%s count=%s\n' "$SHOP_DB" "$DIAG_EMAIL" "$EMAIL_IN_SHOP"

# Restore php-reference serving flags
FLAG_ETC="/etc/ecomae-aspnet/php_serving_deactivated"
rm -f "$FLAG_ETC" 2>/dev/null || true
find /home /var/www -maxdepth 4 -name '.epc_php_serving_deactivated' -delete 2>/dev/null || true
if [[ -f "$ENV_FILE" ]]; then
  cp -a "$ENV_FILE" "${ENV_FILE}.bak.bind-docpart.$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
  python3 - <<PY
from pathlib import Path
p = Path("$ENV_FILE")
keys = {
  "EcomAE__PhpReference__TemporarilyDeactivatePhpServing": "false",
  "EcomAE__PhpReference__KeepPhpProjectAvailable": "true",
  "EcomAE__PhpReference__Mode": "aspnet-primary-php-reference",
}
lines = p.read_text().splitlines()
out, seen = [], set()
for line in lines:
    if not line.strip() or line.lstrip().startswith("#") or "=" not in line:
        out.append(line); continue
    k = line.split("=", 1)[0].strip()
    if k in keys:
        out.append(f"{k}={keys[k]}"); seen.add(k)
    else:
        out.append(line)
for k, v in keys.items():
    if k not in seen:
        out.append(f"{k}={v}")
p.write_text("\\n".join(out) + "\\n")
print("php_reference_flags_restored")
PY
fi

python3 - <<'PY'
from pathlib import Path
marker = "# ecomae-temp-php-serving-off"
snippet = "include /etc/nginx/snippets/ecomae-php-serving-temporarily-deactivated.conf;"
changed = 0
for base in (Path("/etc/nginx/sites-enabled"), Path("/etc/nginx/conf.d")):
    if not base.exists():
        continue
    for conf in base.iterdir():
        if not conf.is_file():
            continue
        try:
            text = conf.read_text(errors="ignore")
        except Exception:
            continue
        if marker not in text and snippet not in text:
            continue
        lines = []
        for line in text.splitlines(True):
            if marker in line or snippet in line:
                changed += 1
                continue
            lines.append(line)
        conf.write_text("".join(lines))
        print("nginx_cleaned", conf)
if changed:
    print("nginx_lines_removed", changed)
PY
nginx -t 2>/dev/null && systemctl reload nginx 2>/dev/null || true

systemctl restart ecomae-platform.service || die "restart_ecomae_platform_failed"
sleep 5
printf 'platform=%s\n' "$(systemctl is-active ecomae-platform.service 2>/dev/null || echo unknown)"

fail=0
LB="$(mktemp)"
LC="$(curl -sS -o "$LB" -w '%{http_code}' --max-time 25 \
  -H 'Host: www.epartscart.com' \
  'http://127.0.0.1:5100/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$LC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$LB"; then
  printf 'GATE_OK local_bunches BOUND code=%s\n' "$LC"
else
  printf 'GATE_BAD local_bunches code=%s\n' "$LC"
  head -c 220 "$LB"; echo
  fail=$((fail + 1))
fi
rm -f "$LB"

B="$(mktemp)"
BC="$(curl -sS -o "$B" -w '%{http_code}' --max-time 30 -k \
  'https://www.epartscart.com/storefront/search-bunches?article=OC90' || echo 000)"
if [[ "$BC" == "200" ]] && ! grep -q 'Tenant shop database is not bound' "$B"; then
  printf 'GATE_OK public_bunches BOUND\n'
  printf 'BOUND_BUNCHES=YES\n'
  BOUND_BUNCHES=YES
else
  printf 'GATE_BAD public_bunches code=%s\n' "$BC"
  head -c 240 "$B"; echo
  printf 'BOUND_BUNCHES=NO\n'
  BOUND_BUNCHES=NO
  fail=$((fail + 1))
fi
rm -f "$B"

REDIR="$(curl -sS -o /dev/null -w '%{redirect_url}' --max-time 25 -k \
  -X POST 'https://www.epartscart.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' \
  --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode "contact=${DIAG_EMAIL}" \
  --data-urlencode 'password=__standalone_wrong__' || true)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
if [[ "$REDIR" == *'tenant_db_unbound'* ]]; then
  printf 'GATE_BAD login_still_tenant_db_unbound — LIVE_PUBLISH finish-pending branch (db_pass SQL fix) still required\n'
  fail=$((fail + 1))
else
  printf 'GATE_OK login_not_tenant_db_unbound\n'
fi

printf '======== PASTE_ME_BEGIN ========\n'
printf 'RESOLVER_ROW=%s\n' "$RESOLVED"
printf 'shop_db=%s source=%s\n' "$SHOP_DB" "$SOURCE"
printf 'BOUND_BUNCHES=%s\n' "$BOUND_BUNCHES"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
printf 'EMAIL_IN_SHOP=%s\n' "$EMAIL_IN_SHOP"
printf '======== PASTE_ME_END ========\n'

[[ "$fail" -eq 0 ]] || die "gates_failed=$fail — if login still tenant_db_unbound: publish finish-pending (PortalTenantSql db_pass fix). Log: /root/epartscart-bind-docpart-standalone.log"
printf 'RESULT=PASS BIND_DOCPART_STANDALONE shop_db=%s resolver=%s source=%s\n' "$SHOP_DB" "$RESOLVED" "$SOURCE"
exit 0
