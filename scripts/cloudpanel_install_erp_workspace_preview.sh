#!/usr/bin/env bash
# Install only the exact ASP.NET ERP workspace route.
# Never broad-proxies /erp and never removes PHP fallback.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-erp-workspace-preview-example.conf"

source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_ERP_WORKSPACE_PREVIEW:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_ERP_WORKSPACE_PREVIEW=YES\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }
[[ -f "$EXAMPLE" ]] || { printf 'ERROR: missing %s\n' "$EXAMPLE" >&2; exit 1; }
ecomae_assert_nginx_shadow_target_allowed "$CONF" exact-route

bak="/root/$(basename "$CONF").bak.erp-workspace-preview.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re
import sys

conf_path, example_path = map(Path, sys.argv[1:])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8").strip() + "\n"
route = "/erp/app"

if re.search(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{", text):
    print(f"ALREADY PRESENT: {route}")
    raise SystemExit(0)

marker = re.search(
    r"\n[ \t]*(?:location / \{|location /\s*\{|location ~|include[ \t]+)",
    text,
)
if not marker:
    raise SystemExit(
        "ERROR: insertion point missing; set ECOMAE_NGINX_SITE_CONF to the active site config"
    )

block = "\n".join(f"  {line}" if line else line for line in example.splitlines())
insert_at = marker.start() + 1
conf_path.write_text(text[:insert_at] + block + "\n\n" + text[insert_at:], encoding="utf-8")
print(f"INSERTED: {route}")
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx. Exact ASP.NET ERP workspace: /erp/app\n'
printf 'PHP remains the fallback for /erp, /erp/*, and every other route.\n'
printf 'Backup: %s\n' "$bak"
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
