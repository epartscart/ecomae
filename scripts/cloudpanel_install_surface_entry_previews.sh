#!/usr/bin/env bash
# Install exact ASP.NET entry/login shadows for ERP, CP, and BOS.
# Never broad-proxies a surface prefix and never changes PHP source.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-surface-entry-previews-example.conf"

source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_SURFACE_ENTRY_PREVIEWS:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_SURFACE_ENTRY_PREVIEWS=YES\n' >&2
  exit 2
fi
[[ -f "$CONF" ]] || { printf 'ERROR: missing %s\n' "$CONF" >&2; exit 1; }
[[ -f "$EXAMPLE" ]] || { printf 'ERROR: missing %s\n' "$EXAMPLE" >&2; exit 1; }
ecomae_assert_nginx_shadow_target_allowed "$CONF" exact-route

bak="/root/$(basename "$CONF").bak.surface-entry-previews.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re
import sys

conf_path, example_path = map(Path, sys.argv[1:])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8").strip() + "\n"
routes = ("/erp", "/erp/login", "/cp/app", "/cp/login", "/bos", "/bos/login")

present = {
    route
    for route in routes
    if re.search(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{", text)
}
missing = [route for route in routes if route not in present]
if not missing:
    print("ALREADY PRESENT: " + ", ".join(routes))
    raise SystemExit(0)

marker = re.search(
    r"\n[ \t]*(?:location / \{|location /\s*\{|location ~|include[ \t]+)",
    text,
)
if not marker:
    raise SystemExit(
        "ERROR: insertion point missing; set ECOMAE_NGINX_SITE_CONF to the active site config"
    )

blocks = []
for block in re.split(r"(?=^location\s)", example, flags=re.MULTILINE):
    if block.strip():
        blocks.append("\n".join(f"  {line}" if line else line for line in block.rstrip().splitlines()))

selected = []
for block in blocks:
    match = re.search(r"^  location\s*=\s*(\S+)\s*\{", block, flags=re.MULTILINE)
    if match and match.group(1) in missing:
        selected.append(block)

insert_at = marker.start() + 1
conf_path.write_text(text[:insert_at] + "\n\n".join(selected) + "\n\n" + text[insert_at:], encoding="utf-8")
print("INSERTED: " + ", ".join(missing))
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx with exact ASP.NET entry/login previews.\n'
printf 'PHP remains fallback for all non-exact paths.\n'
printf 'Backup: %s\n' "$bak"
printf 'Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
