#!/usr/bin/env bash
# Install ALL storefront-digest exact-route ASP.NET shadows from
# deploy/aspnet/nginx-storefront-digests-shadow-example.conf — one `location =`
# each. Never broad /storefront or /. Never removes PHP.
#
# Usage:
#   ECOMAE_CONFIRM_INSTALL_STOREFRONT_DIGEST_SHADOWS=YES \
#     bash scripts/cloudpanel_install_storefront_digest_shadows.sh
#
# Optional:
#   ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/www.ecomae.com.conf
#   ECOMAE_DIGEST_SHADOW_PROBE=0
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
EXAMPLE="$ROOT/deploy/aspnet/nginx-storefront-digests-shadow-example.conf"
PROBE="${ECOMAE_DIGEST_SHADOW_PROBE:-1}"

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_STOREFRONT_DIGEST_SHADOWS:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_STOREFRONT_DIGEST_SHADOWS=YES\n' >&2
  exit 2
fi
if [[ ! -f "$CONF" ]]; then
  printf 'ERROR: missing nginx site conf %s\n' "$CONF" >&2
  exit 1
fi
if [[ ! -f "$EXAMPLE" ]]; then
  printf 'ERROR: missing shadow example %s\n' "$EXAMPLE" >&2
  exit 1
fi
# Storefront digests on www only by default — live ePartsCart/tenant storefronts stay PHP.
ecomae_assert_nginx_shadow_target_allowed "$CONF" exact-route

bak="/root/$(basename "$CONF").bak.storefront-digests.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"
printf 'Source: %s\n' "$EXAMPLE"

python3 - "$CONF" "$EXAMPLE" <<'PY'
from pathlib import Path
import re
import sys

conf_path = Path(sys.argv[1])
example_path = Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")

blocks = []
for m in re.finditer(
    r"(?m)^(location = (/[^\s{]+)\s*\{.*?\n\})",
    example,
    flags=re.S,
):
    block_raw, route = m.group(1), m.group(2)
    if route in {"/storefront", "/storefront/", "/", "/cp", "/erp", "/bos", "/api"}:
        raise SystemExit(f"ERROR: refusing broad path in example: {route}")
    if not route.startswith("/storefront/"):
        continue
    indented = "\n".join(
        ("  " + line if line.strip() else line) for line in block_raw.splitlines()
    )
    blocks.append((route, indented.rstrip() + "\n"))

if len(blocks) != 4:
    raise SystemExit(f"ERROR: expected 4 storefront digest locations, found {len(blocks)}")

inserted = []
already = []
for route, block in blocks:
    loc_re = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{")
    if loc_re.search(text):
        already.append(route)
        continue

    marker = None
    health = text.find("location = /health")
    loc_root = text.find("\n  location / {")
    if health >= 0 and loc_root > health:
        between = text[health:loc_root]
        if "\nserver {" not in between:
            marker = loc_root + 1
    if marker is None:
        m = re.search(r"\n  location / \{", text)
        if not m:
            raise SystemExit("ERROR: could not find `location / {` insertion point")
        marker = m.start() + 1

    text = text[:marker] + block + "\n" + text[marker:]
    inserted.append(route)

conf_path.write_text(text, encoding="utf-8")
print(f"Routes in example: {len(blocks)}")
print(f"ALREADY PRESENT: {len(already)}")
for r in already:
    print(f"  = {r}")
print(f"INSERTED: {len(inserted)}")
for r in inserted:
    print(f"  + {r}")
if not inserted and not already:
    raise SystemExit("ERROR: nothing to install")
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx once after storefront digest batch insert.\n'

if [[ "$PROBE" != "1" ]]; then
  printf 'Skipping public probes (ECOMAE_DIGEST_SHADOW_PROBE=%s).\n' "$PROBE"
  printf 'Do NOT remove PHP. Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
  exit 0
fi

printf '\n-- Public storefront digest probes (expect ASP.NET 401 unauthorized JSON) --\n'
bash "$ROOT/scripts/cloudpanel_probe_storefront_digest_shadows.sh" || {
  printf 'WARN: some public probes failed (CDN lag?). Re-run:\n' >&2
  printf '  bash scripts/cloudpanel_probe_storefront_digest_shadows.sh\n' >&2
  printf 'Do NOT remove PHP. Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
  exit 5
}

printf 'Do NOT remove PHP. Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
printf 'OK: storefront digest exact-route batch install + public probes passed.\n'
