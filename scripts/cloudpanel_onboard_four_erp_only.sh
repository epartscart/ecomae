#!/usr/bin/env bash
# ONBOARD_FOUR_ERP_ONLY — create asap / indj / fito / real on live Super CP registry.
# No storefront. Operator emails xxxx@ecomae.com. Prints temp passwords once.
# Does not invent a send. Does not delete PHP. Does not touch commerce keys.
#
# CloudPanel root (production already on SHA 7c73c5c59+):
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/reg-kyc-erp-cover-5dbd/scripts/cloudpanel_onboard_four_erp_only.sh'
#   TMP=/tmp/cloudpanel-onboard-four-erp-only.sh
#   curl -fsSL "$URL" -o "$TMP"
#   test -s "$TMP" || { echo RESULT=FAIL empty_download; exit 1; }
#   grep -q ONBOARD_FOUR_ERP_ONLY "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_CONFIRM_ONBOARD_ERP_ONLY=YES
#   bash "$TMP"
set -euo pipefail

echo '======== ONBOARD_FOUR_ERP_ONLY ========'
[[ "${ECOMAE_CONFIRM_ONBOARD_ERP_ONLY:-}" == "YES" ]] || { echo 'RESULT=FAIL set ECOMAE_CONFIRM_ONBOARD_ERP_ONLY=YES' >&2; exit 2; }

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
[[ -f "$ENV_FILE" ]] || { echo "RESULT=FAIL missing $ENV_FILE" >&2; exit 1; }

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || { echo 'RESULT=FAIL TenantRegistry connection string missing' >&2; exit 1; }

python3 - "$CS" <<'PY'
import json, os, random, re, subprocess, sys, time

cs = sys.argv[1]
parts = {}
for chunk in cs.split(";"):
    if "=" not in chunk:
        continue
    k, v = chunk.split("=", 1)
    parts[k.strip().lower()] = v.strip().strip('"').strip("'")

host = parts.get("server") or parts.get("host") or parts.get("data source") or "127.0.0.1"
port = parts.get("port") or "3306"
user = parts.get("uid") or parts.get("user") or parts.get("user id") or ""
password = parts.get("pwd") or parts.get("password") or ""
database = parts.get("database") or parts.get("initial catalog") or "ecomae"
if "<db_" in user or "<db_" in password or not user:
    print("RESULT=FAIL platform.env still has placeholders")
    raise SystemExit(1)

# CloudPanel: ecomae@127.0.0.1 (TCP) often works while ecomae@localhost (socket) is 1045.
if host.strip().lower() in ("localhost", "localhost.localdomain", "::1"):
    host = "127.0.0.1"

def parse_clpctl_master(text):
    host_v, user_v, password_v, port_v = "127.0.0.1", "root", "", "3306"

    def grab(label):
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
        host_v = h
    if u:
        user_v = u
    if p:
        password_v = p
    if po and po.isdigit():
        port_v = po
    m5 = re.search(r"-p'([^']+)'", text) or re.search(r'-p"([^"]+)"', text)
    m3 = re.search(r"-h['\"]?([^'\"\s]+)", text)
    m4 = re.search(r"-u['\"]?([^'\"\s]+)", text)
    if m3:
        host_v = m3.group(1)
    if m4:
        user_v = m4.group(1)
    if m5:
        password_v = m5.group(1)
    if host_v.strip().lower() in ("localhost", "localhost.localdomain", "::1"):
        host_v = "127.0.0.1"
    return host_v, user_v, port_v, password_v

def run_mysql(sql, db, h, u, p, po):
    cmd = ["mysql", "-h", h, "-P", str(po), "-u", u, "--protocol=TCP", "--batch", "--raw", "-N", db, "-e", sql]
    env = os.environ.copy()
    if p:
        env["MYSQL_PWD"] = p
    else:
        env.pop("MYSQL_PWD", None)
    proc = subprocess.run(cmd, capture_output=True, text=True, env=env)
    err = (proc.stderr or "").strip()
    if proc.returncode != 0:
        raise RuntimeError(err or (proc.stdout or "mysql failed").strip())
    return proc.stdout

active = (host, user, password, port)
try:
    run_mysql("SELECT 1", database, *active)
except RuntimeError as first:
    clp = subprocess.run(["clpctl", "db:show:master-credentials"], capture_output=True, text=True)
    if clp.returncode != 0 or not (clp.stdout or "").strip():
        print("RESULT=FAIL registry user 1045 and clpctl master unavailable")
        print(str(first))
        raise SystemExit(1)
    h, u, po, p = parse_clpctl_master(clp.stdout)
    if not p:
        print("RESULT=FAIL could not parse clpctl db:show:master-credentials")
        raise SystemExit(1)
    try:
        run_mysql("SELECT 1", database, h, u, p, po)
    except RuntimeError as second:
        print("RESULT=FAIL clpctl master could not open TenantRegistry")
        print(str(second))
        raise SystemExit(1)
    active = (h, u, p, po)
    print("AUTH=clpctl-master")
else:
    print("AUTH=tenant-registry")

def mysql(sql, db=database):
    return run_mysql(sql, db, *active)

alters = [
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `hosted_on` VARCHAR(16) NOT NULL DEFAULT 'client'",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `erp_only_shared` TINYINT(1) NOT NULL DEFAULT 0",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `dedicated_db` TINYINT(1) NOT NULL DEFAULT 0",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `scale_policy` VARCHAR(32) NOT NULL DEFAULT 'shared_docpart'",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 1",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `is_demo` TINYINT(1) NOT NULL DEFAULT 0",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `operator_temp_password` VARCHAR(120) NOT NULL DEFAULT ''",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `intro_json` MEDIUMTEXT NULL",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `from_email` VARCHAR(120) NOT NULL DEFAULT ''",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `blockchain_mode` VARCHAR(24) NOT NULL DEFAULT 'anchor'",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `hub_name` VARCHAR(120) NOT NULL DEFAULT ''",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `notes` VARCHAR(500) NOT NULL DEFAULT ''",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `db_user` VARCHAR(64) NOT NULL DEFAULT ''",
    "ALTER TABLE `epc_portal_tenants` ADD COLUMN `db_password` VARCHAR(255) NOT NULL DEFAULT ''",
]
for sql in alters:
    try:
        mysql(sql)
    except RuntimeError as exc:
        if "Duplicate column" not in str(exc) and "1060" not in str(exc):
            raise

alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"

def esc(value):
    return str(value).replace("\\", "\\\\").replace("'", "\\'")

clients = [
    ("asap", "ASAP Autoparts", "auto_parts"),
    ("indj", "INDJ Jewellery", "jewellery"),
    ("fito", "FITO Fit-out", "fit_out"),
    ("real", "REAL Real Estate", "real_estate"),
]
created = existed = 0
now = int(time.time())
print("LOGIN_URL_BASE=https://www.ecomae.com/cp/client-erp/")
for key, trade, industry in clients:
    email = f"{key}@ecomae.com"
    found = mysql(f"SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`='{esc(key)}' LIMIT 1").strip()
    if found:
        existed += 1
        print(f"EXISTS {key} email={email} login=https://www.ecomae.com/cp/client-erp/{key}/")
        continue
    password = "".join(random.choice(alphabet) for _ in range(12))
    intro = json.dumps({
        "admin_cp_email": email,
        "operator_login_email": email,
        "erp_only_shared": 1,
        "erp_only": True,
        "industry_code": industry,
    })
    try:
        mysql(f"CREATE DATABASE IF NOT EXISTS `{key}`", db="mysql")
        db_note = "dedicated DB created"
    except RuntimeError as exc:
        db_note = "registry saved — CREATE DATABASE refused"
        print(f"WARN {key} {exc}")
    mysql(
        "INSERT INTO `epc_portal_tenants` "
        "(`site_key`,`hostname`,`industry_code`,`status`,`trade_name`,`hub_name`,`from_email`,"
        "`db_name`,`db_user`,`db_password`,`notes`,`intro_json`,`hosted_on`,`erp_only_shared`,"
        "`dedicated_db`,`scale_policy`,`blockchain_mode`,`is_active`,`is_demo`,"
        "`operator_temp_password`,`created_at`,`updated_at`) VALUES ("
        f"'{esc(key)}','www.ecomae.com','{esc(industry)}','live','{esc(trade)}','',"
        f"'{esc(email)}','{esc(key)}','{esc(key)}','',"
        f"'ERP-only. No public storefront.','{esc(intro)}','platform',1,1,"
        f"'dedicated_mysql','anchor',1,0,'{esc(password)}',{now},{now})"
    )
    created += 1
    print(f"CREATED {key} industry={industry} email={email} login=https://www.ecomae.com/cp/client-erp/{key}/ temp_password={password} {db_note}")

print(f"RESULT=PASS ONBOARD_FOUR_ERP_ONLY created={created} existed={existed}")
print("Copy temp_password now. This script does not invent a send.")
print("Open https://www.ecomae.com/cp/tenants-app — Super CP can reset any password.")
PY

if [[ -f scripts/cloudpanel_grant_erp_only_shop_dbs.sh ]]; then
  printf 'GRANT_ERP_ONLY_SHOP_DBS after onboard (CREATE + registry GRANT)...\n'
  ECOMAE_CONFIRM_GRANT_ERP_ONLY=YES bash scripts/cloudpanel_grant_erp_only_shop_dbs.sh \
    || printf 'WARN grant_erp_only_shop_dbs failed — Super CP password reset needs CloudPanel CREATE/GRANT on asap/indj/fito/real\n'
fi
