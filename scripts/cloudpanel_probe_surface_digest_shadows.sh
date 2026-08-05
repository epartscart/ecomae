#!/usr/bin/env bash
# Probe all surface-digest exact routes on public www for ASP.NET JSON auth gate.
# Expect unauth HTTP 401 with unauthorized (not PHP HTML).
# Retries cache-bust when CDN still serves prior PHP HTML.
#
# Usage:
#   bash scripts/cloudpanel_probe_surface_digest_shadows.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXAMPLE="${1:-$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf}"
BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
RETRIES="${ECOMAE_DIGEST_PROBE_RETRIES:-4}"

if [[ ! -f "$EXAMPLE" ]]; then
  printf 'ERROR: missing %s\n' "$EXAMPLE" >&2
  exit 1
fi

mapfile -t ROUTES < <(grep -E '^location = /(cp|erp|bos)/' "$EXAMPLE" | sed -E 's/^location = ([^ {]+).*/\1/')
if [[ "${#ROUTES[@]}" -ne 131 ]]; then
  printf 'ERROR: expected 131 digest routes, found %s\n' "${#ROUTES[@]}" >&2
  exit 1
fi

is_aspnet_gate() {
  local body="$1" code="$2"
  if grep -qi '<!DOCTYPE\|<html' "$body" 2>/dev/null; then
    return 1
  fi
  if [[ "$code" == "401" ]] && grep -qE '"unauthorized"|unauthorized' "$body" 2>/dev/null; then
    return 0
  fi
  if grep -qE '"ok"[[:space:]]*:[[:space:]]*false|missing_api_key' "$body" 2>/dev/null; then
    return 0
  fi
  return 1
}

pass=0
fail=0
printf '%-36s %-6s %s\n' 'ROUTE' 'HTTP' 'RESULT'
printf '%-36s %-6s %s\n' '-----' '----' '------'

for route in "${ROUTES[@]}"; do
  body="$(mktemp)"
  code="000"
  result="FAIL unexpected"
  attempt=1
  while [[ "$attempt" -le "$RETRIES" ]]; do
    code="$(curl -sS -m 20 \
      -H 'Cache-Control: no-cache' -H 'Pragma: no-cache' \
      -A 'Mozilla/5.0 EcomAE-digest-probe' \
      -o "$body" -w '%{http_code}' \
      "${BASE}${route}?_ecomae_probe=$(date +%s%N)-${attempt}" || echo 000)"
    if is_aspnet_gate "$body" "$code"; then
      result="PASS aspnet-unauthorized"
      break
    fi
    if grep -qi '<!DOCTYPE\|<html' "$body" 2>/dev/null && [[ "$attempt" -lt "$RETRIES" ]]; then
      sleep 1
      attempt=$((attempt + 1))
      continue
    fi
    if grep -qi '<!DOCTYPE\|<html' "$body" 2>/dev/null; then
      result="FAIL php-html"
    else
      result="FAIL unexpected"
    fi
    break
  done

  if [[ "$result" == PASS* ]]; then
    pass=$((pass + 1))
  else
    fail=$((fail + 1))
  fi
  printf '%-36s %-6s %s\n' "$route" "$code" "$result"
  rm -f "$body"
done

printf '\nSummary: PASS=%s FAIL=%s TOTAL=%s\n' "$pass" "$fail" "${#ROUTES[@]}"
if [[ "$fail" -gt 0 ]] || [[ "$pass" -ne 131 ]]; then
  exit 1
fi
printf 'OK: all %s surface digest exact-routes return ASP.NET JSON auth gate on %s\n' "$pass" "$BASE"
