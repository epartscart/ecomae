#!/usr/bin/env bash
# Prove CP Website tracker assets + shell after publish (no FORCE_LIVE).
set -euo pipefail
HOST="${ECOMAE_PROVE_HOST:-https://www.epartscart.com}"
fail=0
check() {
  local name="$1" url="$2" needle="$3"
  local body code
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' -L --max-redirs 2 "${url}?t=$(date +%s)" || echo 000)"
  if [[ "$code" != "200" ]] && [[ "$code" != "302" ]] && [[ "$code" != "401" ]] && [[ "$code" != "403" ]]; then
    printf 'FAIL %s http=%s\n' "$name" "$code"; fail=1; rm -f "$body"; return
  fi
  if [[ -n "$needle" ]] && ! grep -Fq "$needle" "$body"; then
    if grep -Eqi 'login|bos-login|epc-cp-login' "$body"; then
      printf 'PASS %s auth-gate http=%s\n' "$name" "$code"; rm -f "$body"; return
    fi
    printf 'FAIL %s missing %q http=%s\n' "$name" "$needle" "$code"; fail=1; rm -f "$body"; return
  fi
  printf 'PASS %s http=%s\n' "$name" "$code"
  rm -f "$body"
}
check css "$HOST/platform-assets/epc_web_tracker_cp.css" 'wt-funnel'
check js "$HOST/platform-assets/epc_web_tracker_aspnet.js" 'svgLineChart'
check app "$HOST/cp/web-tracker-app" 'epc-web-tracker'
check dash "$HOST/cp/web-tracker/dashboard" ''
if [[ "$fail" -eq 0 ]]; then printf 'RESULT=PASS\n'; else printf 'RESULT=FAIL\n'; exit 1; fi
