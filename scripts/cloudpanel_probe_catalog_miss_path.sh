#!/usr/bin/env bash
# Probe ASP.NET catalog miss envelopes (Batch 5).
# Unauth → expect 401. Auth + cold key → expect 404 cache_miss / vin_cache_miss.
# Never prints API keys. Never disables PHP. Never claims cutover.
#
# Usage:
#   set -a; source /etc/ecomae-aspnet/platform.env; set +a
#   bash scripts/cloudpanel_probe_catalog_miss_path.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${ECOMAE_ASPNET_ENV_FILE:-/etc/ecomae-aspnet/platform.env}"
BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
LOOP="${ECOMAE_ASPNET_LOOPBACK:-http://127.0.0.1:5100}"
UA='Mozilla/5.0 (compatible; EcomAE-CatalogMissProbe/1.0)'

if [[ -z "${ECOMAE_CATALOG_API_KEY:-}" && -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

KEY="${ECOMAE_CATALOG_API_KEY:-${CATALOG_API_KEY:-}}"

say() { printf '%s\n' "$*"; }

curl_code() {
  # Usage: curl_code OUTFILE URL [extra curl args...]
  local out="$1"; shift
  local url="$1"; shift
  curl -sS -m 30 -A "$UA" -o "$out" -w '%{http_code}' "$@" "$url" 2>/dev/null || echo 000
}

probe_host() {
  local host="$1"
  local path="$2"
  local out="$3"
  shift 3
  local code
  code="$(curl_code "$out" "${host}${path}" "$@")"
  if [[ "$code" == "000" || "$code" == "403" || "$code" == "502" || "$code" == "503" ]]; then
    return 1
  fi
  printf '%s' "$code"
  return 0
}

pick_host_code() {
  local path="$1"
  local out="$2"
  shift 2
  local code
  if code="$(probe_host "$LOOP" "$path" "$out" "$@")"; then
    printf '%s' "$code"
    return 0
  fi
  if code="$(probe_host "$BASE" "$path" "$out" "$@")"; then
    printf '%s' "$code"
    return 0
  fi
  printf '000'
}

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

say "== Catalog miss path probe (Batch 5) =="
say "Loopback: $LOOP"
say "Public:   $BASE"
say "Policy:   ASP.NET cache readers only; PHP/UMAPI remains authoritative for fills"
say "cutoverAllowed=false"

fail=0

# 1) Unauthenticated miss-shaped request → 401 (or 404 if gateway strips auth differently — record either)
unauth_path='/api/v1/catalog/engines?section=passenger&mfa_id=999999001'
unauth_out="$tmpdir/unauth.json"
unauth_code="$(pick_host_code "$unauth_path" "$unauth_out")"
say "unauth engines cold-key → HTTP $unauth_code"
if [[ "$unauth_code" == "401" ]]; then
  say "  OK: unauthorized without API key"
elif [[ "$unauth_code" == "404" ]]; then
  say "  WARN: got 404 without key (gateway may inject auth); inspect body"
else
  say "  FAIL: expected 401 (or 404 behind unusual gateway), got $unauth_code"
  fail=1
fi

if [[ -z "$KEY" ]]; then
  say "SKIP auth miss probes: ECOMAE_CATALOG_API_KEY missing"
  say "HINT: set -a; source $ENV_FILE; set +a"
  if [[ "$fail" -ne 0 ]]; then
    exit 1
  fi
  say "PASS (partial): unauth probe only"
  exit 0
fi

say "API key: present (value not printed)"

expect_miss() {
  local label="$1"
  local path="$2"
  local want_code="$3"
  local out="$tmpdir/${label}.json"
  local code
  code="$(pick_host_code "$path" "$out" -H "X-API-Key: ${KEY}")"
  local body_code
  body_code="$(python3 - "$out" <<'PY'
import json,sys
p=sys.argv[1]
try:
    d=json.load(open(p,encoding="utf-8"))
except Exception:
    print("")
    raise SystemExit
err=d.get("error") if isinstance(d.get("error"),dict) else {}
print(str(err.get("code") or ""))
PY
)"
  say "auth $label → HTTP $code error.code=${body_code:-?}"
  if [[ "$code" == "200" ]]; then
    say "  WARN: cache HIT for intended cold key — pick colder params / different tenant DB"
    return 0
  fi
  if [[ "$code" == "404" && "$body_code" == "$want_code" ]]; then
    say "  OK: miss envelope $want_code"
    return 0
  fi
  if [[ "$code" == "404" && ( "$body_code" == "cache_miss" || "$body_code" == "vin_cache_miss" ) ]]; then
    say "  OK: miss envelope $body_code (wanted $want_code)"
    return 0
  fi
  if [[ "$code" == "401" || "$code" == "403" ]]; then
    say "  FAIL: auth rejected ($code). Re-issue smoke creds with catalog allowlist."
    fail=1
    return 1
  fi
  say "  FAIL: expected 404+$want_code, got HTTP $code code=$body_code"
  fail=1
  return 1
}

expect_miss engines '/api/v1/catalog/engines?section=passenger&mfa_id=999999001' cache_miss || true
expect_miss analogs '/api/v1/catalog/analogs?section=passenger&article=ZZZMISSNOFILL001&brand=ZZZ' cache_miss || true
expect_miss vin '/api/v1/catalog/vin?vin=ZZZMISSNOFILLVIN01' vin_cache_miss || true
expect_miss article-brands '/api/v1/catalog/article-brands?section=passenger&article=ZZZMISSNOFILL001' cache_miss || true

say "PHP fill paths remain authoritative: api/umapi_proxy.php , api/v1/catalog.php"
say "Compare stubs/live: python3 $ROOT/scripts/compare_catalog_miss_dual_samples.py"

if [[ "$fail" -ne 0 ]]; then
  say "FAIL: one or more miss probes unexpected"
  exit 1
fi
say "PASS: catalog miss probe (cutoverAllowed=false)"
exit 0
