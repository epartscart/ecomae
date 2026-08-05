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

fail=0
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
On the CloudPanel host as root:

  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"

Must print RESULT=PASS, then Ctrl+Shift+R on https://www.epartscart.com/
EOF
  exit 1
fi

printf '\nRESULT=FRESH — public site matches post-#877 storefront publish\n'
exit 0
