#!/usr/bin/env bash
# Probe classic-entry:
#   www.ecomae.com  — /cp /erp /bos / → ASP.NET same-URL
#   epartscart.com  — /cp /erp /bos → ASP.NET; / → PHP same-to-same presentation
# PHP reference remains on separate /php-reference/* links.
set -euo pipefail

UA="${ECOMAE_PROBE_UA:-Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36}"
WWW="${ECOMAE_WWW_BASE:-https://www.ecomae.com}"
TENANT="${ECOMAE_TENANT_BASE:-https://www.epartscart.com}"
PROBE_TENANT="${ECOMAE_PROBE_TENANT:-1}"

pass=0
fail=0
say() { printf '%s\n' "$*"; }

is_aspnet_body() {
  local body="$1"
  if grep -Eiq 'blazor\.web\.js|ecomae-php-chrome-surface|php-chrome-surface|_blazor|blazor\.server\.js|<!--Blazor|dotnet\.js|blazor-focus-on-navigate' <<<"$body"; then
    return 0
  fi
  if grep -Eiq 'php-chrome-layout|PhpChromeLayout|data-aspnet-primary|X-EcomAE-Route-Cutover' <<<"$body"; then
    return 0
  fi
  return 1
}

is_php_cp_only_body() {
  local body="$1"
  if grep -Fq 'bootstrap_admin' <<<"$body" && ! is_aspnet_body "$body"; then
    return 0
  fi
  return 1
}

# Full PHP storefront chrome fingerprints (must match php-reference/home).
is_php_storefront_same_to_same() {
  local body="$1"
  # Reject simplified Blazor scaffold chrome.
  if grep -Eiq 'epc-modex-shell|Fast dispatch|VIN / VEHICLE SEARCH|ecomae-php-chrome-surface' <<<"$body"; then
    return 1
  fi
  if grep -Fq 'Garage Manager' <<<"$body" \
    && grep -Fq 'ERP Login' <<<"$body" \
    && grep -Eiq 'WhatsApp' <<<"$body" \
    && grep -Eiq 'AI Parts Expert' <<<"$body" \
    && grep -Eiq 'Request a call back' <<<"$body" \
    && grep -Eiq 'epc-header-search__tabs|By car|More info' <<<"$body"; then
    return 0
  fi
  return 1
}

check_same_url_aspnet() {
  local base="$1" path="$2"
  local headers code loc body final hdr
  headers="$(curl -sSI -A "$UA" --max-time 30 "${base}${path}" 2>/dev/null || true)"
  code="$(printf '%s\n' "$headers" | awk 'BEGIN{c="000"} /^HTTP/{c=$2} END{print c}')"
  loc="$(printf '%s\n' "$headers" | awk 'BEGIN{IGNORECASE=1} /^location:/{sub(/\r$/,""); sub(/^location:[[:space:]]*/,""); print; exit}')"

  if [[ "$code" == "301" || "$code" == "302" || "$code" == "303" || "$code" == "307" || "$code" == "308" ]]; then
    if [[ "$loc" == *"/cp/app"* || "$loc" == *"/erp/app"* || "$loc" == *"/bos/app"* || "$loc" == *"/marketing/app"* || "$loc" == *"/storefront/app"* ]]; then
      say "FAIL  ${base}${path} redirected to app path (${loc}) — tenant-shared URL must stay unchanged"
      fail=$((fail + 1))
      return
    fi
  fi

  final="$(curl -sS -A "$UA" -L --max-redirs 3 -D /tmp/classic_same_url.hdr -o /tmp/classic_same_url.body -w '%{http_code}' --max-time 45 "${base}${path}" 2>/dev/null || echo 000)"
  body="$(cat /tmp/classic_same_url.body 2>/dev/null || true)"
  hdr="$(cat /tmp/classic_same_url.hdr 2>/dev/null || true)"

  if [[ "$final" != "200" ]]; then
    say "FAIL  ${base}${path} want HTTP 200, got ${final} (initial ${code} loc=${loc:-none})"
    fail=$((fail + 1))
    return
  fi

  if is_aspnet_body "$body" || grep -Eiq 'x-ecomae-route-cutover:\s*classic-entry' <<<"$hdr"; then
    say "PASS  ${base}${path} ASP.NET same-URL HTTP 200"
    pass=$((pass + 1))
    return
  fi

  if is_php_cp_only_body "$body"; then
    say "FAIL  ${base}${path} still PHP CP chrome (bootstrap_admin, no Blazor)"
    fail=$((fail + 1))
    return
  fi

  if grep -Fq 'ECOMAE-MARKETING-HOME' <<<"$body" && [[ "$path" == "/cp"* || "$path" == "/erp"* || "$path" == "/bos"* ]]; then
    say "FAIL  ${base}${path} still PHP marketing chrome"
    fail=$((fail + 1))
    return
  fi

  if grep -Fq 'ECOMAE-MARKETING-HOME' <<<"$body"; then
    say "FAIL  ${base}${path} still PHP marketing home (ECOMAE-MARKETING-HOME)"
    fail=$((fail + 1))
    return
  fi

  say "FAIL  ${base}${path} missing ASP.NET/Blazor markers (len=${#body})"
  fail=$((fail + 1))
}

check_tenant_home_php_same_to_same() {
  local base="$1"
  local final body
  final="$(curl -sS -A "$UA" -L --max-redirs 5 -o /tmp/classic_tenant_home.body -w '%{http_code}' --max-time 60 "${base}/" 2>/dev/null || echo 000)"
  body="$(cat /tmp/classic_tenant_home.body 2>/dev/null || true)"
  if [[ "$final" != "200" ]]; then
    say "FAIL  ${base}/ want PHP same-to-same HTTP 200, got ${final}"
    fail=$((fail + 1))
    return
  fi
  if is_php_storefront_same_to_same "$body"; then
    say "PASS  ${base}/ PHP same-to-same storefront chrome HTTP 200 (len=${#body})"
    pass=$((pass + 1))
    return
  fi
  if is_aspnet_body "$body"; then
    say "FAIL  ${base}/ still ASP.NET scaffold chrome — want PHP same-to-same (Garage Manager / WhatsApp / AI Parts Expert)"
    fail=$((fail + 1))
    return
  fi
  say "FAIL  ${base}/ missing PHP storefront chrome fingerprints (len=${#body})"
  fail=$((fail + 1))
}

check_php_reference() {
  local base="$1" path="$2"
  local code body
  code="$(curl -sS -A "$UA" -L --max-redirs 3 -o /tmp/php_ref.body -w '%{http_code}' --max-time 45 "${base}${path}" 2>/dev/null || echo 000)"
  body="$(cat /tmp/php_ref.body 2>/dev/null || true)"
  if [[ "$code" == "200" ]] && grep -Eiq 'ECOMAE-MARKETING-HOME|bootstrap_admin|epm-hub|Garage Manager|DOCTYPE html' <<<"$body"; then
    say "PASS  PHP reference ${base}${path} HTTP 200"
    pass=$((pass + 1))
  else
    say "FAIL  PHP reference ${base}${path} HTTP ${code}"
    fail=$((fail + 1))
  fi
}

say "=== classic-entry probe (ASP.NET chrome + PHP same-to-same tenant home) ==="
say "WWW=$WWW TENANT=$TENANT"

for path in /cp /cp/ /erp /erp/ /bos /bos/; do
  check_same_url_aspnet "$WWW" "$path"
done
check_same_url_aspnet "$WWW" "/"

check_php_reference "$WWW" "/php-reference/home"
check_php_reference "$WWW" "/php-reference/cp"
check_php_reference "$WWW" "/index.php"

if [[ "$PROBE_TENANT" == "1" ]]; then
  for path in /cp /cp/ /erp /erp/ /bos /bos/; do
    check_same_url_aspnet "$TENANT" "$path"
  done
  check_tenant_home_php_same_to_same "$TENANT"
  check_php_reference "$TENANT" "/php-reference/home"
  check_php_reference "$TENANT" "/php-reference/cp"
fi

say ""
say "PASS=$pass FAIL=$fail"
if [[ "$fail" -gt 0 ]]; then
  say "RESULT=FAIL — classic-entry probe not green"
  say "Tenant home must be PHP same-to-same (not Blazor /storefront/app scaffold)."
  exit 1
fi
say "RESULT=PASS — www ASP.NET; epartscart /cp|/erp|/bos ASP.NET; epartscart / PHP same-to-same"
exit 0
