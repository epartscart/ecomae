#!/usr/bin/env bash
# GRANT_ERP_ONLY_SHOP_DBS — CloudPanel master CREATE DATABASE + GRANT the
# TenantRegistry user onto ASAP / INDJ / FITO / REAL shop schemas.
# Does not remap TenantRegistry to PHP. Does not invent a MySQL user from
# db_user=site_key with an empty password. Registry CS opens the shop DB.
#
# Paste as root:
#   export ECOMAE_CONFIRM_GRANT_ERP_ONLY=YES
#   bash scripts/cloudpanel_grant_erp_only_shop_dbs.sh
set -euo pipefail

echo '======== GRANT_ERP_ONLY_SHOP_DBS ========'
[[ "${ECOMAE_CONFIRM_GRANT_ERP_ONLY:-}" == "YES" ]] || { echo 'RESULT=FAIL set ECOMAE_CONFIRM_GRANT_ERP_ONLY=YES' >&2; exit 2; }

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
[[ -f "$ENV_FILE" ]] || { echo "RESULT=FAIL missing $ENV_FILE" >&2; exit 1; }

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || { echo 'RESULT=FAIL TenantRegistry connection string missing' >&2; exit 1; }

python3 - "$CS" <<'PY'
import os, re, subprocess, sys

cs = sys.argv[1]
parts = {}
for chunk in cs.split(";"):
    if "=" not in chunk:
        continue
    k, v = chunk.split("=", 1)
    parts[k.strip().lower()] = v.strip().strip('"').strip("'")

reg_host = parts.get("server") or parts.get("host") or parts.get("data source") or "127.0.0.1"
reg_port = parts.get("port") or "3306"
reg_user = parts.get("uid") or parts.get("user") or parts.get("user id") or ""
reg_password = parts.get("pwd") or parts.get("password") or ""
reg_db = parts.get("database") or parts.get("initial catalog") or "ecomae"
if "<db_" in reg_user or "<db_" in reg_password or not reg_user:
    print("RESULT=FAIL platform.env still has placeholders")
    raise SystemExit(1)
if reg_host.strip().lower() in ("localhost", "localhost.localdomain", "::1"):
    reg_host = "127.0.0.1"

protected = {"docpart", "ecomae", "epartscart", "mysql", "information_schema", "performance_schema", "sys"}
keys = [k.strip().lower() for k in os.environ.get("ECOMAE_ERP_ONLY_KEYS", "asap,indj,fito,real").split(",") if k.strip()]
for key in keys:
    if not re.fullmatch(r"[a-z0-9_]{1,64}", key) or key in protected:
        print("RESULT=FAIL refused_protected_or_invalid_key " + key)
        raise SystemExit(1)

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
    if proc.returncode != 0:
        raise RuntimeError((proc.stderr or proc.stdout or "mysql failed").strip())
    return proc.stdout

clp = subprocess.run(["clpctl", "db:show:master-credentials"], capture_output=True, text=True)
if clp.returncode != 0 or not (clp.stdout or "").strip():
    print("RESULT=FAIL clpctl master unavailable")
    raise SystemExit(1)
mh, mu, mpo, mp = parse_clpctl_master(clp.stdout)
if not mp:
    print("RESULT=FAIL could not parse clpctl db:show:master-credentials")
    raise SystemExit(1)
run_mysql("SELECT 1", "mysql", mh, mu, mp, mpo)
print("AUTH=clpctl-master")
print("REGISTRY_USER=" + reg_user)

hosts = [
    row.split("\t")[0]
    for row in run_mysql(
        "SELECT Host FROM mysql.user WHERE User='" + reg_user.replace("'", "''") + "'",
        "mysql", mh, mu, mp, mpo,
    ).splitlines()
    if row.strip()
]
if not hosts:
    print("RESULT=FAIL no mysql.user rows for " + reg_user)
    raise SystemExit(1)
print("REGISTRY_HOSTS=" + ",".join(hosts))

created = granted = proved = 0
for key in keys:
    run_mysql(
        "CREATE DATABASE IF NOT EXISTS `" + key + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci",
        "mysql", mh, mu, mp, mpo,
    )
    created += 1
    print("CREATED_OR_EXISTS " + key)
    for host in hosts:
        if host not in ("%", "localhost", "127.0.0.1"):
            print("SKIP_HOST " + host)
            continue
        sql = (
            "GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, INDEX, REFERENCES, "
            "CREATE TEMPORARY TABLES, LOCK TABLES ON `" + key + "`.* TO '"
            + reg_user.replace("'", "''") + "'@'" + host.replace("'", "''") + "'"
        )
        run_mysql(sql, "mysql", mh, mu, mp, mpo)
        granted += 1
        print("GRANT " + key + " -> " + reg_user + "@" + host)
    run_mysql("FLUSH PRIVILEGES", "mysql", mh, mu, mp, mpo)
    try:
        run_mysql("SELECT DATABASE()", key, reg_host, reg_user, reg_password, reg_port)
        proved += 1
        print("PROVE_OPEN " + key + "=ok")
    except RuntimeError as exc:
        print("PROVE_OPEN " + key + "=FAIL " + str(exc).splitlines()[-1][:160])
        print("RESULT=FAIL registry_cannot_open_" + key)
        raise SystemExit(1)

print(
    "RESULT=PASS GRANT_ERP_ONLY_SHOP_DBS created_or_exists="
    + str(created) + " grants=" + str(granted) + " proved=" + str(proved)
)
print("TenantRegistry stays on the platform CS. PHP is not remapped.")
PY
