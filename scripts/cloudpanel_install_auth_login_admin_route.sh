#!/usr/bin/env bash
# Emergency: ensure location = /auth/login/admin proxies to ASP.NET on www (+ optional all-hosts).
# Fixes HTTP 500 when BOS/CP/ERP login forms POST to /auth/login/admin but nginx sends PHP.
#
#   ECOMAE_CONFIRM_INSTALL_AUTH_LOGIN_ADMIN=YES \
#     bash scripts/cloudpanel_install_auth_login_admin_route.sh
#
# Prefer full classic-entry reinstall after the pack includes /auth/login/admin:
#   ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
#   ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
#     bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"

if [[ "${ECOMAE_CONFIRM_INSTALL_AUTH_LOGIN_ADMIN:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_AUTH_LOGIN_ADMIN=YES\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"
ecomae_assert_nginx_shadow_target_allowed "$CONF" presentation

bak="/root/$(basename "$CONF").bak.auth-login-admin.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" <<'PY'
from pathlib import Path
import re, sys

conf_path = Path(sys.argv[1])
text = conf_path.read_text(encoding="utf-8")
block = """location = /auth/login/admin {
    proxy_pass http://127.0.0.1:5100;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover classic-entry-auth-login-admin;
}
"""
if re.search(r"(?m)^[ \t]*location\s*=\s*/auth/login/admin\s*\{", text):
    print("ALREADY PRESENT: /auth/login/admin")
    sys.exit(0)

indented = "\n".join(("  " + line if line.strip() else line) for line in block.splitlines()) + "\n"
for pat in (r"\n[ \t]*location / \{", r"\n[ \t]*location /php", r"\n[ \t]*location ~"):
    m = re.search(pat, text)
    if m:
        marker = m.start() + 1
        text = text[:marker] + indented + "\n" + text[marker:]
        conf_path.write_text(text, encoding="utf-8")
        print("INSERTED: /auth/login/admin")
        sys.exit(0)
raise SystemExit("ERROR: insertion point missing")
PY

nginx -t
systemctl reload nginx
printf 'OK: /auth/login/admin → 127.0.0.1:5100 (rollback: cp -a %s %s && nginx -t && systemctl reload nginx)\n' "$bak" "$CONF"
printf 'Probe: curl -sS -o /dev/null -w "%%{http_code}\\n" -X POST https://www.ecomae.com/auth/login/admin -H "Content-Type: application/json" -d \'{"contact":"x","password":"y","surface":"bos"}\'\n'
