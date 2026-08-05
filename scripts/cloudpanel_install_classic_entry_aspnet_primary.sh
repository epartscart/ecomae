#!/usr/bin/env bash
# Install exact-route classic tenant-shared entries → ASP.NET with URL PRESERVED.
# /cp /erp /bos / stay the same in the browser (proxy to ASP.NET apps).
# PHP reference is separate: /php-reference/{home,cp,erp,bos,storefront}
#
# www.ecomae.com (default):
#   ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
#     bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh
#
# www.epartscart.com (named tenant — requires live-tenant confirm):
#   ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
#   ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
#   ECOMAE_CLASSIC_ENTRY_HOST=tenant \
#   ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/www.epartscart.com.conf \
#     bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh
#
# Both hosts:
#   ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
#   ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
#     bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HOST_MODE="${ECOMAE_CLASSIC_ENTRY_HOST:-www}"
DO_ALL=0
if [[ "${1:-}" == "--all-hosts" ]]; then
  DO_ALL=1
fi

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES\n' >&2
  printf 'Installs exact-route same-URL proxies for / /cp /erp /bos + /php-reference/*\n' >&2
  exit 2
fi

install_one() {
  local conf="$1"
  local example="$2"
  local label="$3"

  if [[ ! -f "$conf" ]]; then
    # try common CloudPanel alternate names
    local alt
    for alt in \
      "${conf}" \
      "${conf/.conf/}" \
      "/etc/nginx/sites-enabled/$(basename "$conf")" \
      "/etc/nginx/sites-enabled/$(basename "$conf" .conf)" ; do
      if [[ -f "$alt" ]]; then conf="$alt"; break; fi
    done
  fi
  if [[ ! -f "$conf" ]]; then
    printf 'ERROR: missing nginx site conf %s (%s)\n' "$conf" "$label" >&2
    return 1
  fi
  if [[ ! -f "$example" ]]; then
    printf 'ERROR: missing example %s\n' "$example" >&2
    return 1
  fi

  ecomae_assert_nginx_shadow_target_allowed "$conf" exact-route

  local bak="/root/$(basename "$conf").bak.classic-entry-aspnet.$(date -u +%Y%m%d%H%M%S)"
  cp -a "$conf" "$bak"
  printf 'Backup (%s): %s\n' "$label" "$bak"

  python3 - "$conf" "$example" <<'PY'
from pathlib import Path
import re, sys

conf_path, example_path = Path(sys.argv[1]), Path(sys.argv[2])
text = conf_path.read_text(encoding="utf-8")
example = example_path.read_text(encoding="utf-8")

blocks = []
for m in re.finditer(r"(?m)^(location = (/[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    if route in {"/api", "/storefront"}:
        raise SystemExit(f"ERROR: refusing broad path {route}")
    is_proxy = bool(re.search(r"(?m)^\s*proxy_pass\s+http://127\.0\.0\.1:5100/", block_raw))
    is_php_ref = route.startswith("/php-reference/") and "rewrite ^" in block_raw
    if not is_proxy and not is_php_ref:
        raise SystemExit(f"ERROR: block must proxy_pass ASP.NET or php-reference rewrite ({route})")
    if "return 302" in block_raw:
        raise SystemExit(
            f"ERROR: tenant-shared URLs must stay unchanged — no return 302 in {route}"
        )
    indented = "\n".join(("  " + line if line.strip() else line) for line in block_raw.splitlines())
    blocks.append((route, indented.rstrip() + "\n"))

expected = 18  # / + 4 CP + 4 ERP + 4 BOS + 5 php-reference
if len(blocks) != expected:
    raise SystemExit(f"ERROR: expected {expected} classic-entry routes, found {len(blocks)}")

inserted, replaced = [], []
for route, block in blocks:
    pattern = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S)
    if pattern.search(text):
        text = pattern.sub(block + "\n", text, count=1)
        replaced.append(route)
        continue
    m = re.search(r"\n  location / \{", text)
    if not m:
        raise SystemExit("ERROR: insertion point missing (location / {)")
    marker = m.start() + 1
    text = text[:marker] + block + "\n" + text[marker:]
    inserted.append(route)

conf_path.write_text(text, encoding="utf-8")
print(f"REPLACED: {len(replaced)}")
for r in replaced:
    print("  ~", r)
print(f"INSERTED: {len(inserted)}")
for r in inserted:
    print("  +", r)
PY

  printf 'OK installed classic-entry on %s\n' "$conf"
}

WWW_CONF="${ECOMAE_NGINX_SITE_CONF_WWW:-/etc/nginx/sites-enabled/www.ecomae.com.conf}"
TENANT_CONF="${ECOMAE_NGINX_SITE_CONF_TENANT:-/etc/nginx/sites-enabled/www.epartscart.com.conf}"
# fallbacks for CloudPanel naming
if [[ ! -f "$TENANT_CONF" && -f /etc/nginx/sites-enabled/epartscart.com.conf ]]; then
  TENANT_CONF=/etc/nginx/sites-enabled/epartscart.com.conf
fi

WWW_EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf"
TENANT_EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf"

FAIL=0
if [[ "$DO_ALL" -eq 1 ]]; then
  if [[ "${ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW:-}" != "YES" ]]; then
    printf 'Refusing --all-hosts without ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES\n' >&2
    exit 2
  fi
  install_one "$WWW_CONF" "$WWW_EXAMPLE" "www.ecomae.com" || FAIL=1
  install_one "$TENANT_CONF" "$TENANT_EXAMPLE" "epartscart.com" || FAIL=1
elif [[ "$HOST_MODE" == "tenant" ]]; then
  if [[ "${ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW:-}" != "YES" ]]; then
    printf 'Refusing tenant host without ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES\n' >&2
    exit 2
  fi
  CONF="${ECOMAE_NGINX_SITE_CONF:-$TENANT_CONF}"
  install_one "$CONF" "$TENANT_EXAMPLE" "epartscart.com" || FAIL=1
else
  CONF="${ECOMAE_NGINX_SITE_CONF:-$WWW_CONF}"
  install_one "$CONF" "$WWW_EXAMPLE" "www.ecomae.com" || FAIL=1
fi

if [[ "$FAIL" -ne 0 ]]; then
  exit 1
fi

nginx -t
systemctl reload nginx

printf '\nReloaded nginx. Tenant-shared URLs now ASP.NET (URL unchanged):\n'
printf '  https://www.ecomae.com/cp  /erp  /bos  /\n'
printf '  https://www.epartscart.com/cp  /erp  /bos  /   (if --all-hosts or tenant mode)\n'
printf '\nPHP reference (separate links):\n'
printf '  /php-reference/home  /php-reference/cp  /php-reference/erp  /php-reference/bos  /php-reference/storefront\n'
printf 'Deep PHP module paths under /cp/... /erp/... still hit PHP (exact entry only cut over).\n'
