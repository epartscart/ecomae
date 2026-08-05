#!/usr/bin/env bash
# Probe classic-entry:
#   www.ecomae.com  — /cp /erp /bos / → ASP.NET same-URL
#   epartscart.com  — / /cp /erp /bos → ASP.NET same-URL (PHP style chrome)
# PHP reference remains on separate /php-reference/* links only.
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

# ASP.NET storefront chrome should carry PHP-style fingerprints (not PHP HTML).
is_aspnet_storefront_php_style() {
  local body="$1"
  if ! is_aspnet_body "$body"; then
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
    # Hard login walls must not kick shared shells away from browse detail.
    if [[ "$path" == "/cp" || "$path" == "/cp/" || "$path" == "/erp" || "$path" == "/erp/" || "$path" == "/bos" || "$path" == "/bos/" ]]; then
      if [[ "$loc" == *"/login"* ]]; then
        say "FAIL  ${base}${path} hard-redirected to login (${loc}) — guest must browse ASP.NET shell"
        fail=$((fail + 1))
        return
      fi
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

check_tenant_home_aspnet_php_style() {
  local base="$1"
  local final body
  final="$(curl -sS -A "$UA" -L --max-redirs 5 -o /tmp/classic_tenant_home.body -w '%{http_code}' --max-time 60 "${base}/" 2>/dev/null || echo 000)"
  body="$(cat /tmp/classic_tenant_home.body 2>/dev/null || true)"
  if [[ "$final" != "200" ]]; then
    say "FAIL  ${base}/ want ASP.NET PHP-style HTTP 200, got ${final}"
    fail=$((fail + 1))
    return
  fi
  if is_aspnet_storefront_php_style "$body"; then
    say "PASS  ${base}/ ASP.NET PHP-style storefront chrome HTTP 200 (len=${#body})"
    pass=$((pass + 1))
    return
  fi
  if is_aspnet_body "$body"; then
    say "FAIL  ${base}/ ASP.NET body missing PHP-style fingerprints (Garage Manager / WhatsApp / AI Parts Expert / search tabs)"
    fail=$((fail + 1))
    return
  fi
  say "FAIL  ${base}/ missing ASP.NET storefront chrome (still PHP HTML?) (len=${#body})"
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

say "=== classic-entry probe (ASP.NET primary + PHP style; PHP reference only) ==="
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
  check_tenant_home_aspnet_php_style "$TENANT"
  check_php_reference "$TENANT" "/php-reference/home"
  check_php_reference "$TENANT" "/php-reference/cp"
fi

say ""
say "PASS=$pass FAIL=$fail"
if [[ "$fail" -gt 0 ]]; then
  say "RESULT=FAIL — classic-entry probe not green"
  say "Tenant home must be ASP.NET with PHP-style chrome; PHP only via /php-reference/*."
  exit 1
fi
say "RESULT=PASS — www + epartscart shared entries ASP.NET; PHP reference-only"
exit 0
