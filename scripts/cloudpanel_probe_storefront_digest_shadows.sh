#!/usr/bin/env bash
# Probe all storefront-digest exact routes on public www for ASP.NET JSON auth gate.
# Expect unauth HTTP 401 with unauthorized (customer cookie required for 200).
#
# Usage:
#   bash scripts/cloudpanel_probe_storefront_digest_shadows.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXAMPLE="${1:-$ROOT/deploy/aspnet/nginx-storefront-digests-shadow-example.conf}"
BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
RETRIES="${ECOMAE_DIGEST_PROBE_RETRIES:-4}"

if [[ ! -f "$EXAMPLE" ]]; then
  printf 'ERROR: missing %s\n' "$EXAMPLE" >&2
  exit 1
fi

mapfile -t ROUTES < <(grep -E '^location = /storefront/' "$EXAMPLE" | sed -E 's/^location = ([^ {]+).*/\1/')
if [[ "${#ROUTES[@]}" -ne 7 ]]; then
  printf 'ERROR: expected 7 storefront digest routes, found %s\n' "${#ROUTES[@]}" >&2
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
  return 1
}

pass=0
fail=0
blocked=0
OUT_DIR="${ECOMAE_PROBE_OUT_DIR:-$ROOT/docs/migration/evidence/decommission/public-probes}"
OUT_FILE="$OUT_DIR/www-storefront-digest-shadow-probe.json"
mkdir -p "$OUT_DIR"
TMP_ROWS="$(mktemp)"
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
      -A 'Mozilla/5.0 EcomAE-storefront-digest-probe' \
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

  status_norm="fail"
  if [[ "$result" == PASS* ]]; then
    pass=$((pass + 1))
    status_norm="pass"
  elif [[ "$result" == FAIL\ php-html ]]; then
    # Wired in example but shadow not live yet — track as blocked, not hard fail for artifact.
    blocked=$((blocked + 1))
    fail=$((fail + 1))
    status_norm="blocked-awaiting-shadow"
  else
    fail=$((fail + 1))
  fi
  printf '%-36s %-6s %s\n' "$route" "$code" "$result"
  printf '{"route":"%s","httpStatus":%s,"result":"%s"}\n' "$route" "$code" "$status_norm" >>"$TMP_ROWS"
  rm -f "$body"
done

python3 - "$OUT_FILE" "$TMP_ROWS" "$pass" "$fail" "$blocked" "${#ROUTES[@]}" "$BASE" <<'PY'
import json, sys, time
out, rows_path = sys.argv[1], sys.argv[2]
pass_n, fail_n, blocked_n, total = map(int, sys.argv[3:7])
base = sys.argv[7]
rows = [json.loads(l) for l in open(rows_path, encoding="utf-8") if l.strip()]
doc = {
    "role": "www-storefront-digest-shadow-probe",
    "generatedAtUnix": int(time.time()),
    "baseUrl": base,
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "aspNetInteractiveComplete": 0,
    "routeCount": total,
    "passed": pass_n,
    "failed": fail_n,
    "blocked": blocked_n,
    "wiredExpected": 7,
    "ok": total == 7,
    "results": rows,
    "note": "Unauth 401 ASP.NET JSON gate expected. Live lag vs wired 7 is blocked until CloudPanel install. Never invent cutover.",
}
open(out, "w", encoding="utf-8").write(json.dumps(doc, indent=2) + "\n")
print(json.dumps({"ok": doc["ok"], "passed": pass_n, "failed": fail_n, "blocked": blocked_n, "routes": total, "out": out}, indent=2))
PY
rm -f "$TMP_ROWS"

printf '\nSummary: PASS=%s FAIL=%s TOTAL=%s\n' "$pass" "$fail" "${#ROUTES[@]}"
printf 'Artifact: %s\n' "$OUT_FILE"
# Hard-fail only when inventory count wrong; live lag is recorded in artifact.
if [[ "${#ROUTES[@]}" -ne 7 ]]; then
  exit 1
fi
if [[ "${ECOMAE_STOREFRONT_PROBE_REQUIRE_ALL_LIVE:-0}" == "1" ]] && { [[ "$fail" -gt 0 ]] || [[ "$pass" -ne 7 ]]; }; then
  exit 1
fi
printf 'OK: storefront digest inventory 7; live PASS=%s (set ECOMAE_STOREFRONT_PROBE_REQUIRE_ALL_LIVE=1 to require 7/7)\n' "$pass"
