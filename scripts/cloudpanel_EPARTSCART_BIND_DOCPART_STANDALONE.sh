#!/usr/bin/env bash
# SELF-CONTAINED — no git checkout required.
# Binds www.epartscart.com / epartscart.com → shop DB `docpart`, status=live,
# restarts platform, restores php-reference serving flags, proves public gates.
#
# Paste as root (must paste RESULT= lines back):
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-portal-dump-bind-7b3b/scripts/cloudpanel_EPARTSCART_BIND_DOCPART_STANDALONE.sh'
#   TMP=/tmp/epartscart-bind-docpart-standalone.sh
#   curl -fsSL "$URL" -o "$TMP"
#   grep -q BIND_DOCPART_STANDALONE "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   bash "$TMP" 2>&1 | tee /root/epartscart-bind-docpart-standalone.log
#   grep -E 'RESULT=|BOUND_|GATE_|RESOLVER_|POST_LOGIN|ROW_|ERROR|docpart' /root/epartscart-bind-docpart-standalone.log | tail -80
set -euo pipefail

printf '======== EPARTSCART BIND_DOCPART_STANDALONE ========\n'
printf 'HOST=%s DATE_UTC=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root"

SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
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
printf 'registry_db=%s mysql_host=%s shop_db=%s\n' "$REG_DB" "$DB_HOST" "$SHOP_DB"

export MYSQL_PWD="$DB_PASS"
mysql_q() { mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -N -e "$1"; }
mysql_e() { mysql -h "$DB_HOST" -u "$DB_USER" "$REG_DB" -e "$1"; }

printf '\n-- before --\n'
mysql_e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(status,'') AS status, IFNULL(is_active,1) AS is_active
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" 2>&1 | sed 's/^/ROW_BEFORE /' || true

# Prove shop schema exists
has_users="$(mysql -h "$DB_HOST" -u "$DB_USER" -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${SHOP_DB//\'/\\\'}' AND table_name='users';" 2>/dev/null || echo 0)"
[[ "${has_users:-0}" != "0" ]] || die "shop_db=${SHOP_DB} missing_users_table — set ECOMAE_EPARTSCART_SHOP_DB"

SHOP_SQL="${SHOP_DB//\'/\\\'}"

# Ensure rows + force db_name + status=live (ASP.NET filter)
for host in epartscart.com www.epartscart.com; do
  exists="$(mysql_q "SELECT COUNT(*) FROM epc_portal_tenants WHERE hostname='${host}';" 2>/dev/null || echo 0)"
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
SET db_name='${SHOP_SQL}', status='live', is_active=1
WHERE hostname IN ('epartscart.com','www.epartscart.com');
" 2>/dev/null || die "update_epc_portal_tenants_failed"

# Best-effort clear erp_only on shop hosts
mysql_e "
UPDATE epc_portal_tenants SET erp_only_shared=0
WHERE hostname IN ('epartscart.com','www.epartscart.com');
" 2>/dev/null || true

printf '\n-- after --\n'
mysql_e "
SELECT site_key, hostname, IFNULL(db_name,'') AS db_name, IFNULL(status,'') AS status, IFNULL(is_active,1) AS is_active
FROM epc_portal_tenants
WHERE hostname IN ('epartscart.com','www.epartscart.com') OR site_key LIKE 'epartscart%'
ORDER BY hostname;
" 2>&1 | sed 's/^/ROW_AFTER /' || true

RESOLVED="$(mysql_q "
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

# Restore php-reference serving (Archive paused)
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

# Strip nginx archive-pause include if present
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

unset MYSQL_PWD

fail=0
# Local Host-header prove
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
else
  printf 'GATE_BAD public_bunches code=%s\n' "$BC"
  head -c 240 "$B"; echo
  printf 'BOUND_BUNCHES=NO\n'
  fail=$((fail + 1))
fi
rm -f "$B"

REDIR="$(curl -sS -o /dev/null -w '%{redirect_url}' --max-time 25 -k \
  -X POST 'https://www.epartscart.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  --data-urlencode 'surface=cp' \
  --data-urlencode 'redirect=/cp' \
  --data-urlencode 'contact_type=email' \
  --data-urlencode 'contact=taxofin2025@gmail.com' \
  --data-urlencode 'password=__standalone_wrong__' || true)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
if [[ "$REDIR" == *'tenant_db_unbound'* ]]; then
  printf 'GATE_BAD login_still_tenant_db_unbound\n'
  fail=$((fail + 1))
else
  printf 'GATE_OK login_not_tenant_db_unbound\n'
fi

PR="$(mktemp)"
PC="$(curl -sS -o "$PR" -w '%{http_code}' --max-time 25 -k \
  'https://www.epartscart.com/php-reference/en/users/registration' || echo 000)"
if [[ "$PC" == "200" ]] && ! grep -q 'Archive paused' "$PR"; then
  printf 'GATE_OK php_reference code=%s\n' "$PC"
else
  printf 'GATE_BAD php_reference code=%s\n' "$PC"
  head -c 100 "$PR"; echo
  fail=$((fail + 1))
fi
rm -f "$PR"

printf '======== PASTE_ME_BEGIN ========\n'
printf 'RESOLVER_ROW=%s\n' "$RESOLVED"
printf 'BOUND_BUNCHES=%s\n' "$([[ "$fail" -eq 0 ]] && echo YES || echo CHECK)"
printf 'POST_LOGIN_REDIRECT=%s\n' "$REDIR"
printf 'shop_db=%s\n' "$SHOP_DB"
printf '======== PASTE_ME_END ========\n'

[[ "$fail" -eq 0 ]] || die "gates_failed=$fail — send /root/epartscart-bind-docpart-standalone.log"
printf 'RESULT=PASS BIND_DOCPART_STANDALONE shop_db=%s resolver=%s\n' "$SHOP_DB" "$RESOLVED"
exit 0
