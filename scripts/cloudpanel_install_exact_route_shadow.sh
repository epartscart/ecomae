#!/usr/bin/env bash
# Install ONE exact-route ASP.NET shadow into a CloudPanel nginx site conf.
# Never enables broad /api /cp /erp /bos /storefront. Never removes PHP.
#
# Usage:
#   ECOMAE_CONFIRM_INSTALL_EXACT_ROUTE_SHADOW=YES \
#     bash scripts/cloudpanel_install_exact_route_shadow.sh /api/v1/catalog/status
#
# Defaults to /etc/nginx/sites-enabled/www.ecomae.com.conf
set -euo pipefail

ROUTE="${1:-}"
CONF="${ECOMAE_NGINX_SITE_CONF:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if [[ "${ECOMAE_CONFIRM_INSTALL_EXACT_ROUTE_SHADOW:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_EXACT_ROUTE_SHADOW=YES\n' >&2
  exit 2
fi
if [[ -z "$ROUTE" || "$ROUTE" != /* ]]; then
  printf 'Usage: %s /api/v1/catalog/status\n' "$(basename "$0")" >&2
  exit 2
fi
case "$ROUTE" in
  /api|/api/|/cp|/cp/|/erp|/erp/|/bos|/bos/|/storefront|/storefront/|/CP|/ERP|/BOS)
    printf 'ERROR: refusing broad path %s\n' "$ROUTE" >&2
    exit 2
    ;;
esac
if [[ ! -f "$CONF" ]]; then
  printf 'ERROR: missing nginx site conf %s\n' "$CONF" >&2
  exit 1
fi

# Build location block from extracted snippet or known templates.
SNIPPET="/tmp/ecomae-exact-route-${ROUTE//\//-}.conf.disabled"
SNIPPET="${SNIPPET/--/-}" # normalize
# Prefer freshly extracted snippet when present.
if [[ ! -s "$SNIPPET" ]]; then
  # Common catalog/status / price paths
  SNIPPET="/tmp/ecomae-exact-route-${ROUTE#/}.conf.disabled"
  SNIPPET="${SNIPPET//\//-}"
fi
# Canonical extract path used by cloudpanel_extract_exact_route_shadow.sh
EXTRACTED="/tmp/ecomae-exact-route-$(printf '%s' "$ROUTE" | sed 's#^/##; s#/#-#g').conf.disabled"

block=""
if [[ -s "$EXTRACTED" ]]; then
  block="$(awk '/^location = /,/^}/' "$EXTRACTED")"
elif [[ -x "$ROOT/scripts/cloudpanel_extract_exact_route_shadow.sh" ]]; then
  bash "$ROOT/scripts/cloudpanel_extract_exact_route_shadow.sh" "$ROUTE" >/dev/null
  EXTRACTED="/tmp/ecomae-exact-route-$(printf '%s' "$ROUTE" | sed 's#^/##; s#/#-#g').conf.disabled"
  block="$(awk '/^location = /,/^}/' "$EXTRACTED")"
fi
if [[ -z "$block" ]]; then
  # Fallback for catalog status / price lookup headers.
  block="$(cat <<EOF
location = ${ROUTE} {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host \$host;
    proxy_set_header X-Forwarded-Proto \$scheme;
    proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
    proxy_set_header X-API-Key \$http_x_api_key;
    proxy_set_header Authorization \$http_authorization;
    proxy_set_header X-EcomAE-Route-Cutover api-shadow-approved;
}
EOF
)"
fi

# Indent like CloudPanel site conf (2 spaces).
block="$(printf '%s\n' "$block" | sed 's/^/  /')"

bak="/root/$(basename "$CONF").bak.$(date -u +%Y%m%d%H%M%S)"
cp -a "$CONF" "$bak"
printf 'Backup: %s\n' "$bak"

python3 - "$CONF" "$ROUTE" "$block" <<'PY'
from pathlib import Path
import re, sys
path = Path(sys.argv[1])
route = sys.argv[2]
block = sys.argv[3].rstrip() + "\n"
text = path.read_text(encoding="utf-8")
if f"location = {route}" in text:
    print(f"ALREADY PRESENT: location = {route}")
    raise SystemExit(0)

# Prefer the TLS/front server block that already has ASP.NET /health.
# Insert immediately BEFORE the catch-all `location / {` in that block.
marker = None
health = text.find("location = /health")
loc_root = text.find("\n  location / {")
if health >= 0 and loc_root > health:
    # ensure health and location / are in same server{} roughly: no "\nserver {" between them
    between = text[health:loc_root]
    if "\nserver {" not in between:
        marker = loc_root + 1  # points at "  location / {"
if marker is None:
    # Fallback: first `location / {`
    m = re.search(r"\n  location / \{", text)
    if not m:
        raise SystemExit("ERROR: could not find `location / {` insertion point in site conf")
    marker = m.start() + 1

new_text = text[:marker] + block + "\n" + text[marker:]
path.write_text(new_text, encoding="utf-8")
print(f"INSERTED location = {route} immediately before location /")
PY

nginx -t
systemctl reload nginx
printf 'Reloaded nginx.\n'

# Show surrounding context
printf '\n-- Conf context --\n'
grep -n "location = ${ROUTE}\|location = /health\|location ^~ /migration\|location / {" "$CONF" | head -20

printf '\n-- Quick probe (no secrets printed) --\n'
code="$(curl -sS -m 20 -o /tmp/ecomae-exact-route-probe.body -w '%{http_code}' "https://www.ecomae.com${ROUTE}" || echo 000)"
ctype="$(file -b --mime-type /tmp/ecomae-exact-route-probe.body 2>/dev/null || true)"
printf 'GET %s -> HTTP %s (%s)\n' "$ROUTE" "$code" "$ctype"
head -c 180 /tmp/ecomae-exact-route-probe.body; echo
if grep -qi '<!DOCTYPE\|<html' /tmp/ecomae-exact-route-probe.body; then
  printf 'WARN: still HTML — location may be in the wrong server block. Run: nginx -T | grep -n "%s" -n\n' "$ROUTE" >&2
  exit 4
fi
printf 'OK: non-HTML response (likely ASP.NET JSON gate or success).\n'
printf 'Do NOT remove PHP. Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
