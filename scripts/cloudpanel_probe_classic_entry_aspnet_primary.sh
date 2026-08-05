#!/usr/bin/env bash
# Probe classic PHP entries now redirect to ASP.NET apps; PHP reference still reachable.
set -euo pipefail

BASE="${ECOMAE_MARKETING_BASE:-https://www.ecomae.com}"
UA="${ECOMAE_PROBE_UA:-Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36}"

pass=0
fail=0
say() { printf '%s\n' "$*"; }

check_redirect() {
  local path="$1" want="$2"
  local headers loc code
  headers="$(curl -sSI -A "$UA" --max-time 30 "${BASE}${path}" 2>/dev/null || true)"
  code="$(printf '%s\n' "$headers" | awk 'BEGIN{c="000"} /^HTTP/{c=$2} END{print c}')"
  loc="$(printf '%s\n' "$headers" | awk 'BEGIN{IGNORECASE=1} /^location:/{sub(/\r$/,""); sub(/^location:[[:space:]]*/,""); print; exit}')"
  if [[ "$code" == "301" || "$code" == "302" || "$code" == "303" || "$code" == "307" || "$code" == "308" ]]; then
    if [[ "$loc" == "$want" || "$loc" == "${BASE}${want}" || "$loc" == "https://www.ecomae.com${want}" || "$loc" == "http://www.ecomae.com${want}" ]]; then
      say "PASS  ${path} → ${want} (HTTP ${code})"
      pass=$((pass + 1))
      return
    fi
  fi
  say "FAIL  ${path} want redirect ${want}, got HTTP ${code} location=${loc:-<none>}"
  fail=$((fail + 1))
}

check_aspnet_app() {
  local path="$1"
  local body code
  code="$(curl -sS -o /tmp/classic_entry_body -w '%{http_code}' -A "$UA" -L --max-time 45 "${BASE}${path}" 2>/dev/null || echo 000)"
  body="$(cat /tmp/classic_entry_body 2>/dev/null || true)"
  if [[ "$code" == "200" ]] && grep -Fiq 'blazor' <<<"$body"; then
    say "PASS  ${path} ASP.NET/Blazor HTTP 200"
    pass=$((pass + 1))
  elif [[ "$code" == "200" ]] && grep -Fiq 'EcomAE' <<<"$body"; then
    say "PASS  ${path} ASP.NET app HTTP 200"
    pass=$((pass + 1))
  else
    say "FAIL  ${path} want ASP.NET app 200, got ${code}"
    fail=$((fail + 1))
  fi
}

check_php_reference() {
  local path="$1" needle="$2"
  local body code
  code="$(curl -sS -o /tmp/classic_php_ref -w '%{http_code}' -A "$UA" --max-time 45 "${BASE}${path}" 2>/dev/null || echo 000)"
  body="$(cat /tmp/classic_php_ref 2>/dev/null || true)"
  if [[ "$code" == "200" ]] && grep -Fq -- "$needle" <<<"$body"; then
    say "PASS  PHP reference ${path} contains ${needle}"
    pass=$((pass + 1))
  else
    say "FAIL  PHP reference ${path} missing ${needle} (HTTP ${code})"
    fail=$((fail + 1))
  fi
}

say "=== classic-entry ASP.NET primary probe ==="
say "BASE=$BASE"

check_redirect "/" "/marketing/app"
check_redirect "/cp/" "/cp/app"
check_redirect "/erp/" "/erp/app"
check_redirect "/bos/" "/bos/app"
check_redirect "/solutions" "/marketing/solutions"
check_redirect "/privacy" "/marketing/privacy"
check_redirect "/platform" "/marketing/platform"

check_aspnet_app "/marketing/app"
check_aspnet_app "/cp/app"
check_aspnet_app "/erp/app"
check_aspnet_app "/bos/app"

# PHP reference keep
check_php_reference "/index.php" "ECOMAE-MARKETING-HOME"
check_php_reference "/cp/shop/orders/orders" "bootstrap_admin"

say ""
say "PASS=$pass FAIL=$fail"
if [[ "$fail" -gt 0 ]]; then
  say "RESULT=FAIL — classic-entry ASP.NET primary not fully live"
  exit 1
fi
say "RESULT=PASS — classic entries on ASP.NET; PHP reference kept"
exit 0
