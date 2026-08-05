#!/usr/bin/env bash
# Install exact-route classic tenant-shared entries → ASP.NET with URL PRESERVED.
# /cp /erp /bos / stay the same in the browser (proxy to ASP.NET apps).
# PHP reference is separate: /php-reference/{home,cp,erp,bos,storefront}
#
# IMPORTANT: CloudPanel often packs many tenants into www.ecomae.com.conf as
# separate server{ } blocks. Installs are scoped by server_name host — never
# file-first-block only.
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

EDIT_PY="$ROOT/scripts/lib/ecomae_nginx_server_block_edit.py"

# shellcheck source=scripts/lib/ecomae_nginx_site_safety.sh
source "$ROOT/scripts/lib/ecomae_nginx_site_safety.sh"

if [[ "${ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES\n' >&2
  printf 'Installs exact-route same-URL proxies for / /cp /erp /bos + /php-reference/*\n' >&2
  exit 2
fi

if [[ ! -f "$EDIT_PY" ]]; then
  printf 'ERROR: missing %s\n' "$EDIT_PY" >&2
  exit 1
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

conf_server_name_has_epartscart() {
  local conf="$1"
  [[ -f "$conf" ]] || return 1
  # Accept only real server_name lines — not host-gate ifs / comments.
  grep -Ei '^[[:space:]]*server_name[[:space:]].*epartscart\.com' \
    "$conf" >/dev/null 2>&1
}

resolve_epartscart_site_conf() {
  # Prefer explicit override only when it actually serves epartscart.com.
  local override="${ECOMAE_NGINX_SITE_CONF_TENANT:-}"
  if [[ -n "$override" && -f "$override" ]]; then
    if conf_server_name_has_epartscart "$override"; then
      printf '%s\n' "$override"
      return 0
    fi
    printf 'ERROR: ECOMAE_NGINX_SITE_CONF_TENANT=%s has no server_name for epartscart.com\n' "$override" >&2
    grep -nE 'server_name' "$override" 2>/dev/null | head -n 20 >&2 || true
    printf 'wildcard-ecomae with server_name *.ecomae.com is NOT epartscart — refuse.\n' >&2
    return 1
  fi

  # Prefer a dedicated enabled epartscart conf if present.
  local candidate
  for candidate in \
    /etc/nginx/sites-enabled/www.epartscart.com.conf \
    /etc/nginx/sites-enabled/epartscart.com.conf \
    /etc/nginx/sites-enabled/eparts-cart.com.conf
  do
    if [[ -f "$candidate" ]] && conf_server_name_has_epartscart "$candidate"; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  local discover_py="$ROOT/scripts/lib/ecomae_discover_epartscart_nginx_conf.py"
  if [[ -f "$discover_py" ]]; then
    local found
    found="$(python3 "$discover_py" --print-path 2>/dev/null || true)"
    if [[ -n "${found:-}" && -f "$found" ]]; then
      printf '%s\n' "$found"
      return 0
    fi
  fi

  for candidate in \
    /etc/nginx/sites-available/www.epartscart.com.conf \
    /etc/nginx/sites-available/epartscart.com.conf
  do
    if [[ -f "$candidate" ]] && conf_server_name_has_epartscart "$candidate"; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  # Last resort: scan sites-enabled for server_name (never pick bare wildcard-ecomae).
  if [[ -d /etc/nginx/sites-enabled ]]; then
    local f
    for f in /etc/nginx/sites-enabled/*; do
      [[ -f "$f" ]] || continue
      case "$(basename "$f")" in
        wildcard-ecomae|wildcard-ecomae.conf|*.bak*|*.bak) continue ;;
      esac
      if conf_server_name_has_epartscart "$f"; then
        printf '%s\n' "$f"
        return 0
      fi
    done
    for f in /etc/nginx/sites-enabled/wildcard-ecomae /etc/nginx/sites-enabled/wildcard-ecomae.conf; do
      if [[ -f "$f" ]] && conf_server_name_has_epartscart "$f"; then
        printf '%s\n' "$f"
        return 0
      fi
    done
  fi
  return 1
}

install_one() {
  local conf="$1"
  local example="$2"
  local label="$3"
  local target_host="$4"

  if [[ ! -f "$conf" ]]; then
    printf 'ERROR: missing nginx site conf %s (%s)\n' "$conf" "$label" >&2
    return 1
  fi
  if [[ ! -f "$example" ]]; then
    printf 'ERROR: missing example %s\n' "$example" >&2
    return 1
  fi
  if [[ -z "$target_host" ]]; then
    printf 'ERROR: target_host required for server-block scoped install\n' >&2
    return 1
  fi

  # Explicit checks: bash disables set -e inside `if install_one; then` bodies.
  if ! ecomae_assert_nginx_shadow_target_allowed "$conf" exact-route; then
    printf 'ERROR: safety refused %s (%s) — aborting this host (no write)\n' "$conf" "$label" >&2
    return 1
  fi

  if [[ "$label" == *epartscart* ]]; then
    if ! conf_server_name_has_epartscart "$conf"; then
      printf 'ERROR: refusing classic-entry on %s — server_name does not include epartscart.com\n' "$conf" >&2
      grep -nE 'server_name' "$conf" 2>/dev/null | head -n 20 >&2 || true
      return 1
    fi
  fi

  # Unique bak per label so www/tenant same-file installs cannot clobber each other.
  local stamp label_slug bak
  stamp="$(date -u +%Y%m%d%H%M%S)"
  label_slug="$(printf '%s' "$label" | tr -c 'A-Za-z0-9._-' '_' )"
  bak="/root/$(basename "$conf").bak.classic-entry-aspnet.${label_slug}.${stamp}"
  cp -a "$conf" "$bak"
  printf 'Backup (%s): %s\n' "$label" "$bak"
  printf 'Installing classic-entry into server_name host=%s inside %s\n' "$target_host" "$conf"

  if ! python3 "$EDIT_PY" install "$conf" "$example" "$target_host"; then
    printf 'ERROR: server-block install failed for %s — restoring %s\n' "$target_host" "$bak" >&2
    cp -a "$bak" "$conf"
    return 1
  fi

  # Reload after EACH successful host so a later host failure cannot leave www dirty/unreloaded.
  if ! nginx -t; then
    printf 'ERROR: nginx -t failed — restoring %s\n' "$bak" >&2
    cp -a "$bak" "$conf"
    nginx -t
    return 1
  fi
  systemctl reload nginx
  printf 'OK installed + reloaded classic-entry on %s (host=%s)\n' "$conf" "$target_host"
}

WWW_EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf"
TENANT_EXAMPLE="$ROOT/deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf"

WWW_CONF="$(resolve_site_conf ecomae \
  "${ECOMAE_NGINX_SITE_CONF_WWW:-}" \
  /etc/nginx/sites-enabled/www.ecomae.com.conf \
  /etc/nginx/sites-enabled/ecomae.com.conf \
  /etc/nginx/sites-available/www.ecomae.com.conf \
  || true)"

# Resolve by server_name containing epartscart.com — NEVER assume wildcard-ecomae
# (that file is usually server_name *.ecomae.com only and never sees epartscart Host).
# On this CloudPanel, epartscart is often a server{} INSIDE www.ecomae.com.conf.
TENANT_CONF="$(resolve_epartscart_site_conf || true)"
if [[ -n "${TENANT_CONF:-}" ]]; then
  printf 'Resolved epartscart conf: %s\n' "$TENANT_CONF"
  grep -nE '^[[:space:]]*server_name[[:space:]].*epartscart' "$TENANT_CONF" 2>/dev/null | head -n 20 || true
  if [[ "${TENANT_CONF}" == "${WWW_CONF:-}" ]]; then
    printf 'NOTE: epartscart shares mega-conf with www — install is server-block scoped to www.epartscart.com\n'
  fi
else
  printf 'NOTE: no nginx conf has server_name for epartscart.com yet.\n' >&2
  printf 'Create one: ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST=YES bash scripts/cloudpanel_ensure_epartscart_nginx_vhost.sh\n' >&2
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
    printf 'Using www conf: %s (server host www.ecomae.com)\n' "$WWW_CONF"
    if install_one "$WWW_CONF" "$WWW_EXAMPLE" "www.ecomae.com" "www.ecomae.com"; then WWW_OK=1; else FAIL=1; fi
  fi
  if [[ -z "${TENANT_CONF:-}" ]]; then
    printf 'ERROR: could not find epartscart nginx site conf (by server_name).\n' >&2
    printf 'Do NOT point ECOMAE_NGINX_SITE_CONF_TENANT at wildcard-ecomae unless server_name includes epartscart.com.\n' >&2
    printf 'Run discover + ensure, then re-run --all-hosts:\n' >&2
    printf '  bash scripts/cloudpanel_discover_epartscart_nginx_conf.sh\n' >&2
    printf '  ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST=YES bash scripts/cloudpanel_ensure_epartscart_nginx_vhost.sh\n' >&2
    printf 'Available sites-enabled:\n' >&2
    ls -1 /etc/nginx/sites-enabled 2>/dev/null >&2 || true
    FAIL=1
  else
    printf 'Using tenant conf: %s (server host www.epartscart.com)\n' "$TENANT_CONF"
    if install_one "$TENANT_CONF" "$TENANT_EXAMPLE" "epartscart.com" "www.epartscart.com"; then TENANT_OK=1; else FAIL=1; fi
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
  install_one "$CONF" "$TENANT_EXAMPLE" "epartscart.com" "www.epartscart.com"
  TENANT_OK=1
else
  CONF="${ECOMAE_NGINX_SITE_CONF:-${WWW_CONF:-}}"
  if [[ -z "$CONF" ]]; then
    printf 'ERROR: missing www.ecomae.com site conf; set ECOMAE_NGINX_SITE_CONF\n' >&2
    ls -1 /etc/nginx/sites-enabled 2>/dev/null || true
    exit 1
  fi
  install_one "$CONF" "$WWW_EXAMPLE" "www.ecomae.com" "www.ecomae.com"
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
