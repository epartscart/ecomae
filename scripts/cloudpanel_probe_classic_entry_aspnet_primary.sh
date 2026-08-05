#!/usr/bin/env bash
# Probe tenant-shared URLs serve ASP.NET at the SAME path (no redirect to /cp/app).
# PHP reference must be on separate /php-reference/* links.
set -euo pipefail

UA="${ECOMAE_PROBE_UA:-Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36}"
WWW="${ECOMAE_WWW_BASE:-https://www.ecomae.com}"
TENANT="${ECOMAE_TENANT_BASE:-https://www.epartscart.com}"
PROBE_TENANT="${ECOMAE_PROBE_TENANT:-1}"

pass=0
fail=0
say() { printf '%s\n' "$*"; }

check_same_url_aspnet() {
  local base="$1" path="$2"
  local headers code loc body final
  headers="$(curl -sSI -A "$UA" --max-time 30 "${base}${path}" 2>/dev/null || true)"
  code="$(printf '%s\n' "$headers" | awk 'BEGIN{c="000"} /^HTTP/{c=$2} END{print c}')"
  loc="$(printf '%s\n' "$headers" | awk 'BEGIN{IGNORECASE=1} /^location:/{sub(/\r$/,""); sub(/^location:[[:space:]]*/,""); print; exit}')"
  # Must NOT bounce tenants to /cp/app|/erp/app|/bos/app|/marketing/app|/storefront/app
  if [[ "$code" == "301" || "$code" == "302" || "$code" == "303" || "$code" == "307" || "$code" == "308" ]]; then
    if [[ "$loc" == *"/cp/app"* || "$loc" == *"/erp/app"* || "$loc" == *"/bos/app"* || "$loc" == *"/marketing/app"* || "$loc" == *"/storefront/app"* ]]; then
      say "FAIL  ${base}${path} redirected to app path (${loc}) — tenant-shared URL must stay unchanged"
      fail=$((fail + 1))
      return
    fi
  fi
  final="$(curl -sS -A "$UA" -L --max-redirs 2 -o /tmp/classic_same_url.body -w '%{http_code}' --max-time 45 "${base}${path}" 2>/dev/null || echo 000)"
  body="$(cat /tmp/classic_same_url.body 2>/dev/null || true)"
  if [[ "$final" == "200" ]] && grep -Eiq 'blazor|EcomAE|ecomae-php-chrome-surface|CONTROL|BOS|ERP' <<<"$body"; then
    if grep -Fq 'ECOMAE-MARKETING-HOME' <<<"$body" && [[ "$path" == "/cp"* || "$path" == "/erp"* || "$path" == "/bos"* ]]; then
      say "FAIL  ${base}${path} still PHP marketing chrome"
      fail=$((fail + 1))
      return
    fi
    if grep -Fq 'bootstrap_admin' <<<"$body" && ! grep -Eiq 'blazor|ecomae-php-chrome-surface' <<<"$body"; then
      say "FAIL  ${base}${path} still PHP CP chrome (bootstrap_admin)"
      fail=$((fail + 1))
      return
    fi
    say "PASS  ${base}${path} ASP.NET same-URL HTTP ${final}"
    pass=$((pass + 1))
    return
  fi
  say "FAIL  ${base}${path} want ASP.NET 200 same-URL, got ${final}"
  fail=$((fail + 1))
}

check_php_reference() {
  local base="$1" path="$2"
  local code body
  code="$(curl -sS -A "$UA" -o /tmp/php_ref.body -w '%{http_code}' --max-time 45 "${base}${path}" 2>/dev/null || echo 000)"
  body="$(cat /tmp/php_ref.body 2>/dev/null || true)"
  if [[ "$code" == "200" ]] && grep -Eiq 'ECOMAE-MARKETING-HOME|bootstrap_admin|epm-hub|DOCTYPE' <<<"$body"; then
    say "PASS  PHP reference ${base}${path} HTTP 200"
    pass=$((pass + 1))
  else
    say "FAIL  PHP reference ${base}${path} HTTP ${code}"
    fail=$((fail + 1))
  fi
}

say "=== classic-entry same-URL ASP.NET primary probe ==="
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
  check_same_url_aspnet "$TENANT" "/"
  check_php_reference "$TENANT" "/php-reference/home"
  check_php_reference "$TENANT" "/php-reference/cp"
fi

say ""
say "PASS=$pass FAIL=$fail"
if [[ "$fail" -gt 0 ]]; then
  say "RESULT=FAIL — tenant-shared URLs not fully on ASP.NET same-path"
  exit 1
fi
say "RESULT=PASS — /cp /erp /bos / on ASP.NET; PHP reference separate"
exit 0
