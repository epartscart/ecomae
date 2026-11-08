#!/usr/bin/env bash
# ONBOARD_AUTO247 — onboard tenant auto247: dedicated DB (schema cloned from docpart),
# portal registry row, registry domain map (epc_tenant_domains), tenant countries,
# and the blue/white/black brand tokens.
#
# Domains (all → site_key auto247, one CP/ERP):
#   auto247.ae / www.auto247.ae            country AE
#   auto247.pk / www.auto247.pk            country PK
#   auto247.com.pk / www.auto247.com.pk    country PK
#   auto247.com / www.auto247.com          default (no pinned country)
#
# CloudPanel root:
#   export ECOMAE_CONFIRM_ONBOARD_AUTO247=YES
#   bash scripts/cloudpanel_onboard_auto247.sh
set -euo pipefail

echo '======== ONBOARD_AUTO247 ========'
[[ "${ECOMAE_CONFIRM_ONBOARD_AUTO247:-}" == "YES" ]] || { echo 'RESULT=FAIL set ECOMAE_CONFIRM_ONBOARD_AUTO247=YES' >&2; exit 2; }

ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
[[ -f "$ENV_FILE" ]] || { echo "RESULT=FAIL missing $ENV_FILE" >&2; exit 1; }

CS="$(grep -E '^ConnectionStrings__TenantRegistry=' "$ENV_FILE" | head -1 | cut -d= -f2- || true)"
[[ -n "$CS" ]] || { echo 'RESULT=FAIL TenantRegistry connection string missing' >&2; exit 1; }

python3 - "$CS" <<'PY'
import json, random, re, string, subprocess, sys, time

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
if host.strip().lower() in ("localhost", "localhost.localdomain", "::1"):
    host = "127.0.0.1"
if not user:
    print("RESULT=FAIL TenantRegistry CS has no user")
    raise SystemExit(1)

# MYSQL_PWD keeps credentials out of the process list.
import os
os.environ["MYSQL_PWD"] = password

def run_mysql(sql, db=database, silent=False):
    cmd = ["mysql", "-N", "-h", host, "-P", port, "-u", user, db, "-e", sql]
    p = subprocess.run(cmd, capture_output=True, text=True)
    if p.returncode != 0:
        if not silent:
            print("MYSQL_ERR", p.stderr.strip()[:300])
        raise RuntimeError(p.stderr.strip()[:300])
    return p.stdout

def esc(value):
    return str(value).replace("\\", "\\\\").replace("'", "\\'")

SITE = "auto247"
now = int(time.time())

# 1) dedicated DB + schema clone from docpart (structure only — no epartscart rows leak)
found = run_mysql(f"SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`='{SITE}' LIMIT 1").strip()
if found:
    print(f"EXISTS {SITE} id={found.strip()}")
else:
    print("CREATE DATABASE auto247 + clone docpart schema ...")
    run_mysql(f"CREATE DATABASE IF NOT EXISTS `{SITE}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", db="mysql")
    dump = subprocess.run(
        ["mysqldump", "-h", host, "-P", port, "-u", user,
         "--no-data", "--skip-lock-tables", "--single-transaction", "docpart"],
        capture_output=True, text=True)
    if dump.returncode != 0 or not dump.stdout.strip():
        print("RESULT=FAIL mysqldump docpart:", dump.stderr.strip()[:300]); raise SystemExit(1)
    load = subprocess.run(
        ["mysql", "-h", host, "-P", port, "-u", user, SITE],
        input=dump.stdout, capture_output=True, text=True)
    if load.returncode != 0:
        print("RESULT=FAIL schema load:", load.stderr.strip()[:300]); raise SystemExit(1)

    db_pass = "".join(random.choice(string.ascii_letters + string.digits) for _ in range(20))
    run_mysql(f"CREATE USER IF NOT EXISTS '{SITE}'@'localhost' IDENTIFIED BY '{db_pass}'", db="mysql", silent=True)
    run_mysql(f"CREATE USER IF NOT EXISTS '{SITE}'@'127.0.0.1' IDENTIFIED BY '{db_pass}'", db="mysql", silent=True)
    run_mysql(f"GRANT ALL PRIVILEGES ON `{SITE}`.* TO '{SITE}'@'localhost'", db="mysql")
    run_mysql(f"GRANT ALL PRIVILEGES ON `{SITE}`.* TO '{SITE}'@'127.0.0.1'", db="mysql")
    run_mysql("FLUSH PRIVILEGES", db="mysql", silent=True)

    intro = json.dumps({"admin_cp_email": "", "industry_code": "auto_parts",
                        "business": "auto parts + vehicle sales/purchase/trade-in",
                        "brand": "auto247 blue/white/black"})
    run_mysql(
        "INSERT INTO `epc_portal_tenants` "
        "(`site_key`,`hostname`,`industry_code`,`status`,`trade_name`,`hub_name`,`from_email`,"
        "`db_name`,`db_user`,`db_password`,`notes`,`intro_json`,`hosted_on`,`erp_only_shared`,"
        "`dedicated_db`,`scale_policy`,`blockchain_mode`,`is_active`,`is_demo`,`country_code`,"
        f"`created_at`,`updated_at`) VALUES ('{SITE}','www.auto247.ae','auto_parts','dns_pending',"
        f"'Auto247','Electronic World Group','','{SITE}','{SITE}','{db_pass}',"
        "'Multi-domain tenant (ae/pk/com.pk/com) — one CP/ERP.','" + esc(intro) + "','client',0,1,"
        f"'dedicated_mysql','anchor',1,0,'AE',{now},{now})"
    )
    print(f"CREATED tenant {SITE} dedicated_db=1 status=dns_pending")

# 2) registry domain map — every country domain binds to the same tenant row
run_mysql(
    "CREATE TABLE IF NOT EXISTS `epc_tenant_domains` ("
    "`hostname` VARCHAR(255) NOT NULL PRIMARY KEY,"
    "`site_key` VARCHAR(64) NOT NULL,"
    "`country_code` CHAR(2) NOT NULL DEFAULT '',"
    "`is_active` TINYINT(1) NOT NULL DEFAULT 1,"
    "`created_at` INT NOT NULL DEFAULT 0,"
    "`updated_at` INT NOT NULL DEFAULT 0,"
    "KEY `ix_tenant_domains_site` (`site_key`)"
    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
)
domains = [
    ("auto247.ae", "AE"), ("www.auto247.ae", "AE"),
    ("auto247.pk", "PK"), ("www.auto247.pk", "PK"),
    ("auto247.com.pk", "PK"), ("www.auto247.com.pk", "PK"),
    ("auto247.com", ""), ("www.auto247.com", ""),
]
for h, cc in domains:
    run_mysql(
        "INSERT INTO `epc_tenant_domains` (`hostname`,`site_key`,`country_code`,`is_active`,`created_at`,`updated_at`) "
        f"VALUES ('{h}','{SITE}','{cc}',1,{now},{now}) "
        f"ON DUPLICATE KEY UPDATE `site_key`='{SITE}',`country_code`='{cc}',`is_active`=1,`updated_at`={now}"
    )
print(f"DOMAIN_MAP rows={len(domains)}")

# 3) tenant country configs (tenant DB): AE default (AED/en) + PK (PKR/ur/rtl) + GCC set
countries = [
    ("AE", "United Arab Emirates", "ae", "AED", 1.0, 0, 0.01, 5, "en", "ltr", 1, 0),
    ("PK", "Pakistan", "pk", "PKR", 76.15, 0, 0.01, 17, "ur", "rtl", 0, 10),
    ("SA", "Saudi Arabia", "sa", "SAR", 1.0221, 0, 0.01, 15, "ar", "rtl", 0, 20),
    ("QA", "Qatar", "qa", "QAR", 1.0005, 0, 0.01, 0, "en", "ltr", 0, 30),
    ("KW", "Kuwait", "kw", "KWD", 0.0837, 0, 0.01, 0, "en", "ltr", 0, 40),
    ("BH", "Bahrain", "bh", "BHD", 0.1025, 0, 0.01, 10, "en", "ltr", 0, 50),
    ("OM", "Oman", "om", "OMR", 0.1048, 0, 0.01, 5, "en", "ltr", 0, 60),
]
exists = run_mysql(
    "SELECT COUNT(*) FROM information_schema.tables "
    "WHERE table_schema='auto247' AND table_name='epc_tenant_countries'", db="mysql").strip().split()[-1]
if exists != "0":
    for cc, name, sub, cur, fx, markup, rnd, vat, lang, direc, dflt, sort in countries:
        run_mysql(
            "INSERT INTO `epc_tenant_countries` "
            "(`country_code`,`country_name`,`subdomain`,`currency_code`,`fx_rate`,"
            "`price_markup_percent`,`price_round_to`,`vat_percent`,`language_code`,`direction`,"
            "`seo_title`,`seo_description`,`seo_keywords`,`address_text`,`is_default`,`is_active`,`sort_order`) "
            f"VALUES ('{cc}','{esc(name)}','{sub}','{cur}',{fx},{markup},{rnd},{vat},'{lang}','{direc}',"
            f"'','','','',{dflt},1,{sort}) "
            f"ON DUPLICATE KEY UPDATE `country_name`='{esc(name)}',`currency_code`='{cur}',"
            f"`language_code`='{lang}',`direction`='{direc}',`vat_percent`={vat}",
            db=SITE)
    print("COUNTRIES ae(default)+pk+gcc seeded")

# 4) brand tokens — blue primary, white base, black supporting (auto247 identity)
brand = {
    "brand_primary": "#1d4ed8", "brand_secondary": "#1e40af", "brand_accent": "#3b82f6",
    "brand_bg_dark": "#0f172a", "brand_sidebar_bg": "#0f172a", "brand_header_bg": "#1d4ed8",
    "brand_login_bg": "#eff6ff", "brand_font": "", "brand_radius": "8",
    "brand_logo_url": "",
}
se = run_mysql(
    "SELECT COUNT(*) FROM information_schema.tables "
    "WHERE table_schema='auto247' AND table_name='epc_settings'", db="mysql").strip().split()[-1]
if se == "0":
    run_mysql(
        "CREATE TABLE `epc_settings` ("
        "`site_key` varchar(190) NOT NULL DEFAULT '', "
        "`setting_key` varchar(190) NOT NULL DEFAULT '', "
        "`setting_value` mediumtext NULL, "
        "`updated_at` bigint NULL, "
        "PRIMARY KEY (`site_key`, `setting_key`))", db=SITE)
for k, v in brand.items():
    run_mysql(
        "INSERT INTO `epc_settings` (`site_key`,`setting_key`,`setting_value`,`updated_at`) "
        f"VALUES ('{SITE}','{k}','{esc(v)}',{now}) "
        f"ON DUPLICATE KEY UPDATE `setting_value`='{esc(v)}',`updated_at`={now}", db=SITE)
print("BRAND_TOKENS blue/white/black set")

print("RESULT=PASS ONBOARD_AUTO247")
print("Next: point DNS for the 4 domains at this server (A record), then set status='live'.")
PY
