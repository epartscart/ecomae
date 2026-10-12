#!/usr/bin/env bash
# Surgical repair: /erp/logout must proxy Host/Cookie as nginx variables, not literals.
# A prior write escaped $host/$scheme/$http_cookie inside location = /erp/logout,
# so HTTPS logout returned 400 and never cleared the session.
# Not a cutover. PHP stays fallback. Only the logout location headers are touched.
set -euo pipefail

CONF="${ECOMAE_NGINX_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
[[ -f "$CONF" ]] || { echo "RESULT=SKIP missing $CONF"; exit 0; }

python3 - "$CONF" <<'PY'
import pathlib
import re
import sys

path = pathlib.Path(sys.argv[1])
text = path.read_text()
pattern = re.compile(
    r"(location\s*=\s*/erp/logout\s*\{)(.*?)(\n[ \t]*\})",
    re.S,
)

def fix(match: re.Match[str]) -> str:
    body = match.group(2)
    body = body.replace(r"\$host", "$host")
    body = body.replace(r"\$scheme", "$scheme")
    body = body.replace(r"\$proxy_add_x_forwarded_for", "$proxy_add_x_forwarded_for")
    body = body.replace(r"\$http_cookie", "$http_cookie")
    return match.group(1) + body + match.group(3)

new, n = pattern.subn(fix, text)
if n == 0:
    print("RESULT=SKIP no /erp/logout location")
    sys.exit(0)
if new == text:
    print(f"RESULT=PASS already_fixed blocks={n}")
    sys.exit(0)
path.write_text(new)
print(f"RESULT=PASS repaired_erp_logout_nginx blocks={n}")
PY

nginx -t
nginx -s reload
echo "NGINX_RELOAD=erp-logout-headers-only"
