#!/usr/bin/env bash
# Install exact-route classic tenant-shared entries → ASP.NET with URL PRESERVED.
# /cp /erp /bos / stay the same in the browser (proxy to ASP.NET apps).
# PHP reference is separate: /php-reference/{home,cp,erp,bos,storefront}
#
# www.ecomae.com (default):
#   ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
#     bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh
#
# Both hosts (www + epartscart):
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

resolve_site_conf() {
  # Usage: resolve_site_conf <name-fragment> [preferred] [candidate...]
  local frag="$1"
  shift || true
  local candidate
  for candidate in "$@"; do
    if [[ -n "$candidate" && -f "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  if [[ -n "$frag" && -d /etc/nginx/sites-enabled ]]; then
    local found
    # CloudPanel images often lack ripgrep — use grep -Ei.
    found="$(ls -1 /etc/nginx/sites-enabled 2>/dev/null | grep -Ei "${frag}" | head -n 1 || true)"
    if [[ -n "$found" && -f "/etc/nginx/sites-enabled/${found}" ]]; then
      printf '%s\n' "/etc/nginx/sites-enabled/${found}"
      return 0
    fi
  fi
  return 1
}

install_one() {
  local conf="$1"
  local example="$2"
  local label="$3"

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

def indent_block(block_raw: str) -> str:
    return "\n".join(("  " + line if line.strip() else line) for line in block_raw.splitlines()).rstrip() + "\n"

def find_insert_marker(cfg: str) -> int:
    for pat in (
        r"\n[ \t]*location / \{",
        r"\n[ \t]*location /php",
        r"\n[ \t]*location ~",
        r"\n[ \t]*include[ \t]+fastcgi",
    ):
        m = re.search(pat, cfg)
        if m:
            return m.start() + 1
    raise SystemExit("ERROR: insertion point missing (no location / or fastcgi include)")

# Optional named location for host-gated wildcard tenant packs.
named_blocks = []
for m in re.finditer(r"(?m)^(location @([A-Za-z0-9_]+)\s*\{.*?\n\})", example, flags=re.S):
    named_blocks.append((m.group(2), indent_block(m.group(1))))

blocks = []
for m in re.finditer(r"(?m)^(location = (/[^\s{]*)\s*\{.*?\n\})", example, flags=re.S):
    block_raw, route = m.group(1), m.group(2)
    if route in {"/api", "/storefront"}:
        raise SystemExit(f"ERROR: refusing broad path {route}")
    is_proxy = bool(re.search(r"(?m)^\s*proxy_pass\s+http://127\.0\.0\.1:5100/", block_raw))
    is_php_ref = route.startswith("/php-reference/") and (
        "rewrite ^" in block_raw or "return 302" in block_raw or "alias " in block_raw
    )
    if not is_proxy and not is_php_ref:
        raise SystemExit(f"ERROR: block must proxy_pass ASP.NET or php-reference ({route})")
    # Product-chrome entries must never 302 away from tenant-shared URLs.
    if route in {"/", "/cp", "/cp/", "/CP", "/CP/", "/erp", "/erp/", "/ERP", "/ERP/", "/bos", "/bos/", "/BOS", "/BOS/"}:
        if re.search(r"(?m)^\s*return\s+302\s+", block_raw):
            raise SystemExit(
                f"ERROR: tenant-shared URLs must stay unchanged — no return 302 in {route}"
            )
        if not is_proxy:
            raise SystemExit(f"ERROR: shared entry {route} must proxy_pass ASP.NET")
    blocks.append((route, indent_block(block_raw)))

expected = 18  # / + 4 CP + 4 ERP + 4 BOS + 5 php-reference
if len(blocks) != expected:
    raise SystemExit(f"ERROR: expected {expected} classic-entry routes, found {len(blocks)}")

inserted, replaced = [], []
for name, block in named_blocks:
    pattern = re.compile(rf"(?m)^[ \t]*location\s*@{re.escape(name)}\s*\{{.*?\n[ \t]*\}}\n?", re.S)
    if pattern.search(text):
        text = pattern.sub(block + "\n", text, count=1)
        replaced.append("@" + name)
    else:
        marker = find_insert_marker(text)
        text = text[:marker] + block + "\n" + text[marker:]
        inserted.append("@" + name)

for route, block in blocks:
    pattern = re.compile(rf"(?m)^[ \t]*location\s*=\s*{re.escape(route)}\s*\{{.*?\n[ \t]*\}}\n?", re.S)
    if pattern.search(text):
        text = pattern.sub(block + "\n", text, count=1)
        replaced.append(route)
        continue
    marker = find_insert_marker(text)
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

  # Reload after EACH successful host so a later host failure cannot leave www dirty/unreloaded.
  nginx -t
  systemctl reload nginx
  printf 'OK installed + reloaded classic-entry on %s\n' "$conf"
}

WWW_EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf"
TENANT_EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf"

WWW_CONF="$(resolve_site_conf ecomae \
  "${ECOMAE_NGINX_SITE_CONF_WWW:-}" \
  /etc/nginx/sites-enabled/www.ecomae.com.conf \
  /etc/nginx/sites-enabled/ecomae.com.conf \
  /etc/nginx/sites-available/www.ecomae.com.conf \
  || true)"

# CloudPanel often has NO www.epartscart.com.conf — epartscart is on wildcard-ecomae.
TENANT_CONF="$(resolve_site_conf epartscart \
  "${ECOMAE_NGINX_SITE_CONF_TENANT:-}" \
  /etc/nginx/sites-enabled/www.epartscart.com.conf \
  /etc/nginx/sites-enabled/epartscart.com.conf \
  /etc/nginx/sites-enabled/eparts-cart.com.conf \
  /etc/nginx/sites-enabled/wildcard-ecomae \
  /etc/nginx/sites-enabled/wildcard-ecomae.conf \
  /etc/nginx/sites-available/www.epartscart.com.conf \
  /etc/nginx/sites-available/epartscart.com.conf \
  /etc/nginx/sites-available/wildcard-ecomae \
  || true)"
if [[ -z "${TENANT_CONF:-}" && -f /etc/nginx/sites-enabled/wildcard-ecomae ]]; then
  TENANT_CONF=/etc/nginx/sites-enabled/wildcard-ecomae
fi
if [[ -n "${TENANT_CONF:-}" ]]; then
  printf 'Resolved tenant/wildcard conf: %s\n' "$TENANT_CONF"
  if [[ "$(basename "$TENANT_CONF")" == wildcard-ecomae* ]]; then
    printf 'NOTE: epartscart is served via wildcard vhost; classic-entry is host-gated to (www.)epartscart.com\n'
    grep -nE 'server_name' "$TENANT_CONF" 2>/dev/null | head -n 20 || true
  fi
fi

WWW_OK=0
TENANT_OK=0
FAIL=0

if [[ "$DO_ALL" -eq 1 ]]; then
  if [[ "${ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW:-}" != "YES" ]]; then
    printf 'Refusing --all-hosts without ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES\n' >&2
    exit 2
  fi
  if [[ -z "${WWW_CONF:-}" ]]; then
    printf 'ERROR: could not find www.ecomae.com nginx site conf under /etc/nginx/sites-enabled\n' >&2
    FAIL=1
  else
    printf 'Using www conf: %s\n' "$WWW_CONF"
    if install_one "$WWW_CONF" "$WWW_EXAMPLE" "www.ecomae.com"; then WWW_OK=1; else FAIL=1; fi
  fi
  if [[ -z "${TENANT_CONF:-}" ]]; then
    printf 'ERROR: could not find epartscart nginx site conf.\n' >&2
    printf 'Set ECOMAE_NGINX_SITE_CONF_TENANT=/etc/nginx/sites-enabled/<actual>.conf\n' >&2
    printf 'Available sites-enabled:\n' >&2
    ls -1 /etc/nginx/sites-enabled 2>/dev/null >&2 || true
    FAIL=1
  else
    printf 'Using tenant conf: %s\n' "$TENANT_CONF"
    if install_one "$TENANT_CONF" "$TENANT_EXAMPLE" "epartscart.com"; then TENANT_OK=1; else FAIL=1; fi
  fi
elif [[ "$HOST_MODE" == "tenant" ]]; then
  if [[ "${ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW:-}" != "YES" ]]; then
    printf 'Refusing tenant host without ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES\n' >&2
    exit 2
  fi
  CONF="${ECOMAE_NGINX_SITE_CONF:-${TENANT_CONF:-}}"
  if [[ -z "$CONF" ]]; then
    printf 'ERROR: missing epartscart site conf; set ECOMAE_NGINX_SITE_CONF\n' >&2
    ls -1 /etc/nginx/sites-enabled 2>/dev/null || true
    exit 1
  fi
  install_one "$CONF" "$TENANT_EXAMPLE" "epartscart.com"
  TENANT_OK=1
else
  CONF="${ECOMAE_NGINX_SITE_CONF:-${WWW_CONF:-}}"
  if [[ -z "$CONF" ]]; then
    printf 'ERROR: missing www.ecomae.com site conf; set ECOMAE_NGINX_SITE_CONF\n' >&2
    ls -1 /etc/nginx/sites-enabled 2>/dev/null || true
    exit 1
  fi
  install_one "$CONF" "$WWW_EXAMPLE" "www.ecomae.com"
  WWW_OK=1
fi

printf '\nTenant-shared URLs → ASP.NET (URL unchanged):\n'
printf '  https://www.ecomae.com/cp  /erp  /bos  /     (www_ok=%s)\n' "$WWW_OK"
printf '  https://www.epartscart.com/cp  /erp  /bos  / (tenant_ok=%s)\n' "$TENANT_OK"
printf '\nPHP reference (separate links):\n'
printf '  /php-reference/home  /php-reference/cp  /php-reference/erp  /php-reference/bos  /php-reference/storefront\n'

if [[ "$FAIL" -ne 0 ]]; then
  printf '\nFAIL: one or more hosts did not install; nginx was reloaded for any host that succeeded.\n' >&2
  exit 1
fi

printf '\nPASS: classic-entry installed on requested host(s)\n'
exit 0
