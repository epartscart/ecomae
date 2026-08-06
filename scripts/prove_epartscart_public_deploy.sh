#!/usr/bin/env bash
# Run from ANY machine (laptop / CloudPanel / CI). Does not deploy — only proves
# whether https://www.epartscart.com/ is still on the pre-#877 ASP.NET binary.
#
#   bash scripts/prove_epartscart_public_deploy.sh
#   PUBLIC_BASE=https://www.epartscart.com bash scripts/prove_epartscart_public_deploy.sh
set -euo pipefail

PUBLIC_BASE="${PUBLIC_BASE:-https://www.epartscart.com}"
Q="epc_prove=$(date +%s)"
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

printf '======== PROVE PUBLIC DEPLOY (%s) ========\n' "$PUBLIC_BASE"
curl -sS -A 'Mozilla/5.0' --max-time 45 "${PUBLIC_BASE}/?${Q}" -o "$TMP" \
  -w 'home http=%{http_code} bytes=%{size_download}\n'
MARKER="$(curl -sS -A 'Mozilla/5.0' --max-time 15 "${PUBLIC_BASE}/epc-live-deploy-marker.txt?${Q}" || true)"
printf 'marker: %s\n' "$MARKER"
if [[ "$MARKER" == *'status=pass'* ]]; then
  printf 'PASS  deploy marker status=pass\n'
elif [[ "$MARKER" == *'status=pending'* ]] || [[ "$MARKER" == *'status=fail'* ]]; then
  printf 'FAIL  deploy marker is %s (PHP sync ≠ ASP.NET publish)\n' "$(printf '%s' "$MARKER" | awk '{print $1}')"
  fail=1
elif [[ -n "$MARKER" ]]; then
  printf 'WARN  deploy marker has no status= field (old script); ignoring sha-only\n'
else
  printf 'WARN  deploy marker missing\n'
fi

fail="${fail:-0}"
if grep -Fq 'action="/en/shop/part_search"' "$TMP"; then
  printf 'PASS  home forms post to /en/shop/part_search\n'
else
  printf 'FAIL  home missing action="/en/shop/part_search"\n'
  fail=1
fi
if grep -Fq 'action="/storefront/search-app"' "$TMP"; then
  printf 'FAIL  home STILL has action="/storefront/search-app" (OLD :5100 binary)\n'
  fail=1
else
  printf 'PASS  home has no /storefront/search-app form actions\n'
fi
if grep -Fq 'header-call-box a { background:#ef4444' "$TMP" \
  || grep -Fq 'header-call-box a{background:#ef4444' "$TMP"; then
  printf 'PASS  inline professional header CSS present (#880)\n'
else
  printf 'FAIL  inline professional header CSS missing (publish #877–#880 not live)\n'
  fail=1
fi
if grep -Fq 'Catalog' "$TMP" && grep -Fq 'of products' "$TMP"; then
  printf 'PASS  Catalog of products label present\n'
else
  printf 'FAIL  Catalog of products label missing (stale gray Catalog chrome)\n'
  fail=1
fi
if grep -Fq 'Mon-Fri from 9:00 to 18:00' "$TMP"; then
  printf 'PASS  PHP hours string present\n'
else
  printf 'FAIL  PHP hours string missing (still Mon–Sat stub)\n'
  fail=1
fi
if grep -Fq 'epc-garage-header-link' "$TMP"; then
  printf 'PASS  Garage Manager PHP header class present\n'
else
  printf 'FAIL  epc-garage-header-link missing\n'
  fail=1
fi
if grep -Fq 'background:linear-gradient(135deg,#090f1d' "$TMP"; then
  printf 'PASS  PHP top-menu gradient present\n'
else
  printf 'FAIL  PHP top-menu gradient missing\n'
  fail=1
fi
if grep -Fq 'color:rgba(255,255,255,.88) !important' "$TMP" \
  && grep -Fq 'header.epc-nero-header .top-menu-line .navbar-default .navbar-nav > li > a' "$TMP"; then
  printf 'PASS  top-menu visible CSS beats nero dark-gray links\n'
else
  printf 'FAIL  top-menu still loses to nero dark-gray link color (invisible menu)\n'
  fail=1
fi
if grep -Fq 'Home' "$TMP" && grep -Fq 'Selection catalogs' "$TMP" && grep -Fq 'Vehicle Parts intelligence AI' "$TMP"; then
  printf 'PASS  top-menu labels present in HTML\n'
else
  printf 'FAIL  top-menu labels missing from HTML\n'
  fail=1
fi

HDR="$(curl -sSI -A 'Mozilla/5.0' --max-time 20 \
  "${PUBLIC_BASE}/storefront/search-app?article=1310154101&${Q}" || true)"
printf '%s\n' "$HDR" | head -12
if printf '%s' "$HDR" | grep -qiE '^location:.*part_search'; then
  printf 'PASS  /storefront/search-app redirects to part_search\n'
elif printf '%s' "$HDR" | grep -qiE '^HTTP/.* 404'; then
  printf 'FAIL  /storefront/search-app is 404\n'
  fail=1
else
  printf 'FAIL  /storefront/search-app unexpected response\n'
  fail=1
fi

if [[ "$fail" -ne 0 ]]; then
  cat <<'EOF'

RESULT=STALE
GitHub merges do nothing until CloudPanel republishes Kestrel (:5100).
On the CloudPanel host as root (hardened script — requires RESULT=PASS banner):

  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/storefront-header-topmenu-visible-7b3b/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"

If RESULT=FAIL, paste output of:
  bash scripts/cloudpanel_DIAGNOSE_STALE_HOME.sh
Then Ctrl+Shift+R on https://www.epartscart.com/ only after RESULT=PASS.
EOF
  exit 1
fi

printf '\nRESULT=FRESH — public site matches post-#877 storefront publish\n'
exit 0
