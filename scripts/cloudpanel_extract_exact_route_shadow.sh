#!/usr/bin/env bash
# Extract ONE exact-route `location =` block from a shadow example into a disabled
# nginx snippet. Never enables traffic, never proxies broad /cp|/erp|/bos|/api|/storefront.
#
# Usage:
#   bash scripts/cloudpanel_extract_exact_route_shadow.sh /api/v1/catalog/status
#   bash scripts/cloudpanel_extract_exact_route_shadow.sh /cp/dashboard-summary /tmp/out.conf
#
# Then (operator, after smoke + parity):
#   sudo cp <snippet> <nginx include path>
#   sudo nginx -t && sudo systemctl reload nginx
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ROUTE="${1:-}"
OUT_PATH="${2:-}"

if [[ -z "$ROUTE" || "$ROUTE" == "-h" || "$ROUTE" == "--help" ]]; then
  printf 'Usage: %s <exact-path> [output.conf]\n' "$(basename "$0")" >&2
  printf 'Example: %s /api/v1/catalog/status\n' "$(basename "$0")" >&2
  exit 2
fi

if [[ "$ROUTE" != /* ]]; then
  printf 'ERROR: route must start with / (got %s)\n' "$ROUTE" >&2
  exit 2
fi

# Refuse broad prefixes — only exact location = /path is allowed.
case "$ROUTE" in
  /api|/api/|/cp|/cp/|/CP|/CP/|/erp|/erp/|/ERP|/ERP/|/bos|/bos/|/BOS|/BOS/|/storefront|/storefront/)
    printf 'ERROR: refusing broad surface cutover path %s\n' "$ROUTE" >&2
    printf 'Use an exact digest/API path, e.g. /api/v1/catalog/status or /cp/dashboard-summary\n' >&2
    exit 2
    ;;
esac

# Prefer packed ContentRoot on the live host, then git checkout.
search_roots=()
if [[ -n "${ECOMAE_ASPNET_CONTENT_ROOT:-}" ]]; then
  search_roots+=("$ECOMAE_ASPNET_CONTENT_ROOT")
fi
search_roots+=("/var/www/ecomae-aspnet/current/platform" "$ROOT")

found_file=""
found_block=""
needle="location = ${ROUTE} {"

for base in "${search_roots[@]}"; do
  [[ -d "$base/deploy/aspnet" ]] || continue
  while IFS= read -r -d '' conf; do
    if grep -Fq "$needle" "$conf"; then
      block="$(awk -v route="$ROUTE" '
        $0 ~ ("location = " route " \\{") {grab=1}
        grab {print}
        grab && /^}/ {exit}
      ' "$conf")"
      if [[ -n "$block" ]]; then
        found_file="$conf"
        found_block="$block"
        break 2
      fi
    fi
  done < <(find "$base/deploy/aspnet" -maxdepth 1 -type f -name 'nginx-*-shadow-example.conf' -print0 2>/dev/null)
done

if [[ -z "$found_block" ]]; then
  printf 'ERROR: no location = %s block in nginx-*-shadow-example.conf under:\n' "$ROUTE" >&2
  printf '  %s\n' "${search_roots[@]}" >&2
  printf 'Add the exact-route stub first, or pick a path from deploy/aspnet/nginx-*-shadow-example.conf\n' >&2
  exit 1
fi

safe_name="$(printf '%s' "$ROUTE" | sed 's#^/##; s#[^A-Za-z0-9._-]#-#g')"
if [[ -z "$OUT_PATH" ]]; then
  OUT_PATH="/tmp/ecomae-exact-route-${safe_name}.conf.disabled"
fi

{
  printf '# DISABLED exact-route shadow snippet — do not enable until staging smoke + parity.\n'
  printf '# Source: %s\n' "$found_file"
  printf '# Route:  %s\n' "$ROUTE"
  printf '# PHP remains authoritative. Rollback: remove this include and reload nginx.\n'
  printf '# Never change this to location /cp, /erp, /bos, /api, or /storefront.\n\n'
  printf '%s\n' "$found_block"
} >"$OUT_PATH"

printf 'Wrote DISABLED exact-route snippet: %s\n' "$OUT_PATH"
printf 'Source example: %s\n' "$found_file"
printf 'Next (operator only, after smoke):\n'
printf '  1) Review the snippet (must be location = %s only)\n' "$ROUTE"
printf '  2) sudo cp %s <site-include-path>/ecomae-%s-shadow.conf\n' "$OUT_PATH" "$safe_name"
printf '  3) sudo nginx -t && sudo systemctl reload nginx\n'
printf 'Rollback: remove the include and reload nginx. Do NOT remove PHP.\n'
