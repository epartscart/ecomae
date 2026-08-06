#!/usr/bin/env bash
# Prove product /cp /erp /bos /storefront hit the platform (:5100), not legacy docroot.
#
# Important (operator model):
#   TemporarilyDeactivatePhpServing ONLY pauses /php-reference/* and interim /en/*.
#   Product /cp /erp / /storefront are already meant to be platform-primary.
#   If /cp/control still shows the old login form, nginx never installed
#   location = /cp/control → :5100 (run unbreak / classic-entry install).
#
#   bash scripts/cloudpanel_prove_product_is_platform_primary.sh
#   HOST=https://www.epartscart.com bash scripts/cloudpanel_prove_product_is_platform_primary.sh
set -euo pipefail

HOST="${HOST:-https://www.epartscart.com}"
UA='Mozilla/5.0 (compatible; EcomAE-PlatformProve/1.0)'
Q="epc_prove=$(date +%s)"
fail=0

is_platform_html() {
  local f="$1"
  # Platform SSR markers (do not rely on stack brand words in body copy).
  rg -q 'blazor-enhanced-nav|blazor-focus-on-navigate|ecomae-chrome-surface|charset=utf-8' "$f" 2>/dev/null \
    || rg -qi 'blazor-focus-on-navigate|ecomae-chrome-surface' "$f" 2>/dev/null
}

is_legacy_cp_login() {
  local f="$1"
  # Classic Homer / bootstrap_admin CP login (legacy docroot).
  rg -qi 'base href="/cp/templates/bootstrap_admin|auth_contact_select|lang_cp' "$f" 2>/dev/null
}

is_warmup_splash() {
  local f="$1"
  local n
  n=$(wc -c <"$f")
  [[ "$n" -lt 5000 ]] && rg -q 'Loading your store' "$f" 2>/dev/null
}

probe() {
  local path="$1"
  local expect="$2" # platform | legacy-forbidden | splash-ok | redirect-login
  local body hdr code size loc
  body=$(mktemp)
  hdr=$(mktemp)
  code=$(curl -sS -D "$hdr" -o "$body" -A "$UA" --max-time 45 -w '%{http_code}' \
    "${HOST}${path}?${Q}" || echo 000)
  size=$(wc -c <"$body")
  loc=$(rg -i '^location:' "$hdr" | tr -d '\r' | awk '{print $2}' | head -1 || true)
  local plat=0 leg=0 splash=0
  is_platform_html "$body" && plat=1
  # Also treat response headers from platform middleware as proof
  rg -qi 'blazor-enhanced-nav|^x-ecomae-' "$hdr" && plat=1
  is_legacy_cp_login "$body" && leg=1
  is_warmup_splash "$body" && splash=1

  local verdict=UNKNOWN
  case "$expect" in
    platform)
      if [[ "$splash" -eq 1 ]]; then verdict=FAIL_SPLASH
      elif [[ "$leg" -eq 1 ]]; then verdict=FAIL_LEGACY_DOCROOT
      elif [[ "$plat" -eq 1 && "$code" =~ ^(200|302)$ ]]; then verdict=PASS_PLATFORM
      elif [[ "$code" == "302" && "$loc" == *"/cp/login"* ]]; then verdict=PASS_PLATFORM_AUTH_REDIRECT
      else verdict=FAIL_NOT_PLATFORM
      fi
      ;;
    redirect-login)
      if [[ "$code" == "302" && "$loc" == *"/login"* ]]; then verdict=PASS_AUTH_GATE
      elif [[ "$plat" -eq 1 && "$code" == "200" && $(rg -qi 'log in|password' "$body" && echo 1 || echo 0) -eq 1 ]]; then
        verdict=PASS_PLATFORM_LOGIN
      elif [[ "$leg" -eq 1 ]]; then verdict=FAIL_LEGACY_LOGIN
      else verdict=FAIL_AUTH_GATE
      fi
      ;;
    splash-ok)
      if [[ "$splash" -eq 1 || "$code" == "503" ]]; then verdict=PASS_PAUSED
      else verdict=WARN_NOT_PAUSED
      fi
      ;;
  esac

  printf '%-28s http=%-3s size=%-7s plat=%s legacy=%s splash=%s %s' \
    "$path" "$code" "$size" "$plat" "$leg" "$splash" "$verdict"
  [[ -n "$loc" ]] && printf ' loc=%s' "$loc"
  printf '\n'

  case "$verdict" in
    FAIL_*) fail=1 ;;
  esac
  rm -f "$body" "$hdr"
}

printf '======== PROVE PRODUCT = PLATFORM PRIMARY (%s) ========\n' "$HOST"
printf '\nModel: product /cp /erp / /storefront should be platform (:5100).\n'
printf 'Temp archive pause only affects /php-reference/* and /en/* — NOT product /cp.\n\n'

printf '-- Product CP (must be platform, never legacy login HTML) --\n'
probe /cp platform
probe /cp/control redirect-login
probe /cp/control/ redirect-login
probe /cp/login platform

printf '\n-- Product ERP / storefront --\n'
probe /erp/login platform
probe /storefront/app platform
probe /storefront/search-app platform

printf '\n-- Archive / interim (paused only when TemporarilyDeactivatePhpServing) --\n'
probe /php-reference/cp splash-ok
probe /en/shop/part_search splash-ok

printf '\n-- How to read results --\n'
printf 'PASS_PLATFORM          = response from platform (:5100) — this is the real product test target\n'
printf 'PASS_PLATFORM_LOGIN    = platform login page (auth gate)\n'
printf 'PASS_AUTH_GATE         = 302 to /cp/login (correct when anonymous)\n'
printf 'FAIL_LEGACY_DOCROOT    = old /cp/control login still from docroot — reinstall classic-entry\n'
printf 'FAIL_SPLASH            = warm-up splash — run unbreak script\n'
printf '\nFix if /cp/control is FAIL_LEGACY_DOCROOT:\n'
printf '  ECOMAE_CONFIRM_UNBREAK_EPARTSCART_STOREFRONT=YES \\\n'
printf '    bash scripts/cloudpanel_unbreak_epartscart_storefront_now.sh\n'

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — product surfaces not fully on platform\n'
  exit 1
fi
printf '\nRESULT=PASS — product CP/ERP/storefront on platform; test here (not /php-reference)\n'
exit 0
