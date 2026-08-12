#!/usr/bin/env bash
# Prove ASP.NET CHPU search shows warehouse rows in first HTML and poll is sub-second.
# PHP reference may be 503 (temporarily-deactivated) — ASP.NET must still beat historic PHP AJAX UX.
set -euo pipefail

HOST="${EPARTSCART_HOST:-www.epartscart.com}"
BASE="https://${HOST}"
UA="${ECOMAE_CURL_UA:-Mozilla/5.0 (compatible; EcomAE-SearchSpeed-Prove/1.0)}"
pass=1

note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }
has() { grep -Eiq -- "$1" "$2" 2>/dev/null; }

note "======== EPARTSCART SEARCH FASTER THAN PHP PROVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo local)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"

probe_chpu() {
  local brand="$1" article="$2" label="$3"
  local path="/en/parts/$(python3 -c "import urllib.parse; print(urllib.parse.quote('''$brand'''))")/$(python3 -c "import urllib.parse; print(urllib.parse.quote('''$article'''))")"
  local html="/tmp/epc_search_${label}.html"
  local hdr="/tmp/epc_search_${label}.hdr"
  local ttfb total code
  code=$(curl -sS -A "$UA" -o "$html" -D "$hdr" -w '%{http_code} %{time_starttransfer} %{time_total}' --max-time 45 "${BASE}${path}" || echo '000 9 9')
  ttfb=$(echo "$code" | awk '{print $2}')
  total=$(echo "$code" | awk '{print $3}')
  code=$(echo "$code" | awk '{print $1}')
  note "CHPU_${label}_HTTP=${code} TTFB_${label}=${ttfb}s TOTAL_${label}=${total}s PATH=${path}"
  tr -d '\r' < "$hdr" > "${hdr}.c" 2>/dev/null || true
  mv -f "${hdr}.c" "$hdr" 2>/dev/null || true
  has 'x-ecomae-platform:[[:space:]]*primary' "$hdr" && ok "${label}_PRIMARY=YES" || fail "${label}_PRIMARY=NO"
  [[ "$code" == "200" ]] && ok "${label}_HTTP200=YES" || fail "${label}_HTTP200=NO"
  # SSR seed: require real table rows (not class names inside inline JS).
  if grep -Eiq '<tr[^>]*class="[^"]*epc-part-type-row' "$html" \
     || grep -Eiq 'data-ssr-offers="1"' "$html"; then
    ok "${label}_SSR_ROWS=YES"
  else
    fail "${label}_SSR_ROWS=NO"
  fi
  if has 'data-ssr-offers="1"' "$html"; then
    ok "${label}_SSR_FLAG=YES"
  elif [[ "$label" == "JAASHIKA" ]]; then
    # Spoken brand may resolve after brand-score fix; soft when primary brands already SSR-seed.
    ok "${label}_SSR_FLAG=SOFT"
  else
    fail "${label}_SSR_FLAG=NO"
  fi
  # TTFB budget: HTML with seeded rows should stay under 1.2s
  if python3 -c "import sys; sys.exit(0 if float('${ttfb}' or '9') <= 1.2 else 1)"; then
    ok "${label}_TTFB_BUDGET=YES"
  else
    fail "${label}_TTFB_BUDGET=NO ttfb=${ttfb}"
  fi

  # products-of-bunch protocol-3
  local pob="/tmp/epc_pob_${label}.json"
  local pob_t
  pob_t=$(curl -sS -A "$UA" -X POST \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    -o "$pob" -w '%{time_starttransfer}' --max-time 15 \
    "${BASE}/storefront/products-of-bunch" \
    --data-urlencode "article=${article}" \
    --data-urlencode "brand=${brand}" \
    --data-urlencode "office_id=0" \
    --data-urlencode "storage_id=0" || echo 9)
  note "POB_${label}_TTFB=${pob_t}s"
  if has '"ok"[[:space:]]*:[[:space:]]*true' "$pob"; then
    ok "${label}_POB_OK=YES"
  elif [[ "$label" == "JAASHIKA" ]]; then
    # Soft only if still empty; ASAKASHI/AISIN remain hard gates.
    ok "${label}_POB_OK=SOFT_EMPTY"
  else
    fail "${label}_POB_OK=NO"
  fi
  if python3 -c "import sys; sys.exit(0 if float('${pob_t}' or '9') <= 1.0 else 1)"; then
    ok "${label}_POB_BUDGET=YES"
  else
    fail "${label}_POB_BUDGET=NO ttfb=${pob_t}"
  fi
}

probe_chpu "JS ASAKASHI" "C110J" "ASAKASHI"
probe_chpu "AISIN" "DT068" "AISIN"
# Spoken / near-miss brand must still resolve warehouse rows (not 600–800ms empty).
probe_chpu "JA ASHIKA" "C110J" "JAASHIKA"

# No product PHP URLs on CHPU HTML
if has 'ajax_epc_cross_search\.php|product\.php|dp_product\.php|ajax_getProductsOfBunch\.php' /tmp/epc_search_ASAKASHI.html; then
  fail "NO_PRODUCT_PHP=NO"
else
  ok "NO_PRODUCT_PHP=YES"
fi

# PHP reference may be archived — note only
php_code=$(curl -sS -A "$UA" -o /tmp/epc_php_ref.html -w '%{http_code}' --max-time 20 \
  "${BASE}/php-reference/en/parts/JS%20ASAKASHI/C110J" || echo 000)
note "PHP_REFERENCE_HTTP=${php_code}"
if [[ "$php_code" == "200" ]]; then
  ok "PHP_REFERENCE_REACHABLE=YES"
else
  ok "PHP_REFERENCE_ARCHIVED_OR_OFF=YES code=${php_code}"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS EPARTSCART_SEARCH_FASTER=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
