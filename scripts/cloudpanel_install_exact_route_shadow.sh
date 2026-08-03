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

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

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
# Live tenants stay PHP unless ECOMAE_CONFIRM_TENANT_HOST_SHADOW=YES for an approved exact-route.
ecomae_assert_nginx_shadow_target_allowed "$CONF" exact-route

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
  # Fallback when no shadow-example block found. Include Cookie for digest routes.
  block="$(cat <<EOF
location = ${ROUTE} {
    proxy_pass http://127.0.0.1:5100;
    proxy_http_version 1.1;
    proxy_set_header Host \$host;
    proxy_set_header X-Forwarded-Proto \$scheme;
    proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
    proxy_set_header X-API-Key \$http_x_api_key;
    proxy_set_header Authorization \$http_authorization;
    proxy_set_header Cookie \$http_cookie;
    proxy_set_header X-EcomAE-Route-Cutover exact-route-shadow-approved;
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
# Exact location match only — substring would false-positive
# /api/v1/catalog/article against article-brands / article-links.
loc_re = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{")
if loc_re.search(text):
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

# Show surrounding context (fixed-string exact location line; avoid article⊂article-links).
printf '\n-- Conf context --\n'
{
  grep -nF "location = ${ROUTE} {" "$CONF" || true
  grep -nE "location = /health \{|location \^~ /migration/|location = /api/v1/catalog/status \{|location / \{" "$CONF" || true
} | sort -t: -k1,1n | head -20

printf '\n-- Quick probes (no secrets printed) --\n'
is_aspnet_json_gate() {
  local body="$1"
  local code="$2"
  if grep -qi '<!DOCTYPE\|<html' "$body" 2>/dev/null; then
    return 1
  fi
  # Catalog/price: missing_api_key. Digests: unauthorized (admin cookie).
  if [[ "$code" == "401" ]] && grep -qE 'missing_api_key|"unauthorized"|unauthorized' "$body" 2>/dev/null; then
    return 0
  fi
  grep -qE 'missing_api_key|"unauthorized"|"ok"[[:space:]]*:[[:space:]]*false' "$body" 2>/dev/null
}

# 1) App loopback — proves the ASP.NET route exists (expect 401 JSON without key).
loop_code="$(curl -sS -m 10 -o /tmp/ecomae-exact-route-loop.body -w '%{http_code}' \
  "http://127.0.0.1:5100${ROUTE}" || echo 000)"
printf 'loopback :5100 %s -> HTTP %s\n' "$ROUTE" "$loop_code"
head -c 140 /tmp/ecomae-exact-route-loop.body; echo

# 2) Local nginx, bypass Cloudflare (IPv4 + SNI Host). May hit a non-www default_server.
local_code="$(curl -4 -sS -m 15 -k --resolve www.ecomae.com:443:127.0.0.1 \
  -H 'Host: www.ecomae.com' \
  -o /tmp/ecomae-exact-route-local.body -w '%{http_code}' \
  "https://www.ecomae.com${ROUTE}" || echo 000)"
printf 'local nginx  %s -> HTTP %s\n' "$ROUTE" "$local_code"
head -c 140 /tmp/ecomae-exact-route-local.body; echo

# 3) Public URL — proves edge/origin serve the shadow (authoritative when local SNI mismatches).
# Digests like /cp/users were real PHP HTML 200 pages; Cloudflare may cache that briefly after cutover.
probe_public() {
  local bust="${1:-}"
  local url="https://www.ecomae.com${ROUTE}${bust}"
  curl -sS -m 20 \
    -H 'Cache-Control: no-cache' -H 'Pragma: no-cache' \
    -A 'Mozilla/5.0 EcomAE-exact-route-probe' \
    -o /tmp/ecomae-exact-route-probe.body -w '%{http_code}' \
    "$url" || echo 000
}

pub_code="$(probe_public)"
printf 'public URL   %s -> HTTP %s\n' "$ROUTE" "$pub_code"
head -c 140 /tmp/ecomae-exact-route-probe.body; echo

loop_ok=0
local_ok=0
pub_ok=0
if is_aspnet_json_gate /tmp/ecomae-exact-route-loop.body "$loop_code"; then
  loop_ok=1
fi
if is_aspnet_json_gate /tmp/ecomae-exact-route-local.body "$local_code"; then
  local_ok=1
fi
if is_aspnet_json_gate /tmp/ecomae-exact-route-probe.body "$pub_code"; then
  pub_ok=1
fi

# Retry public with cache-bust when loopback is ASP.NET but edge still serves PHP HTML.
if [[ "$pub_ok" -eq 0 && "$loop_ok" -eq 1 ]]; then
  for delay in 2 3 5; do
    printf 'WARN: public still not ASP.NET JSON; retrying with cache-bust in %ss (CDN may cache prior PHP HTML 200)...\n' "$delay" >&2
    sleep "$delay"
    pub_code="$(probe_public "?_ecomae_shadow_probe=$(date +%s)")"
    printf 'public retry %s -> HTTP %s\n' "$ROUTE" "$pub_code"
    head -c 140 /tmp/ecomae-exact-route-probe.body; echo
    if is_aspnet_json_gate /tmp/ecomae-exact-route-probe.body "$pub_code"; then
      pub_ok=1
      printf 'OK: public URL serves ASP.NET JSON gate after CDN retry for %s.\n' "$ROUTE"
      break
    fi
  done
fi

if [[ "$local_ok" -eq 1 ]]; then
  printf 'OK: local nginx serves ASP.NET JSON gate for %s.\n' "$ROUTE"
elif [[ "$pub_ok" -eq 1 ]]; then
  printf 'OK: public URL serves ASP.NET JSON gate for %s.\n' "$ROUTE"
  if grep -qi '<!DOCTYPE\|<html' /tmp/ecomae-exact-route-local.body 2>/dev/null; then
    printf 'WARN: local --resolve hit HTML (likely wrong default_server/SNI). Public ASP.NET JSON confirms the location is live.\n' >&2
  fi
elif [[ "$loop_ok" -eq 1 ]]; then
  # Location was inserted + app route works; edge may still show cached PHP HTML (seen on /cp/users).
  printf 'WARN: public/local still HTML but loopback ASP.NET JSON gate OK for %s.\n' "$ROUTE" >&2
  printf 'WARN: treating as soft-OK (location= inserted). Re-probe public shortly:\n' >&2
  printf '  curl -sS -H "Cache-Control: no-cache" -o /tmp/p.json -w "%%{http_code}\\n" "https://www.ecomae.com%s?_=$(date +%%s)"\n' "$ROUTE" >&2
  printf '  # expect ASP.NET JSON 401 missing_api_key or unauthorized\n' >&2
else
  printf 'FAIL: neither local nginx nor public URL returned ASP.NET JSON for %s.\n' "$ROUTE" >&2
  printf 'Debug: nginx -T 2>/dev/null | grep -n "%s" -A8 | head -40\n' "$ROUTE" >&2
  exit 4
fi
printf 'Do NOT remove PHP. Rollback: cp -a %s %s && nginx -t && systemctl reload nginx\n' "$bak" "$CONF"
