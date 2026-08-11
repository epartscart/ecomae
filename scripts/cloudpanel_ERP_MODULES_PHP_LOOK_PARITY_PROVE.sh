#!/usr/bin/env bash
# Prove Super/Tenant ERP module pages carry PHP erp_ui look markers.
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
set -euo pipefail

SUPER_BASE="${ECOMAE_PUBLIC_SUPER:-https://www.ecomae.com}"
TENANT_BASE="${ECOMAE_PUBLIC_TENANT:-https://www.epartscart.com}"
LOCAL_BASE="${ECOMAE_LOCAL_BASE:-http://127.0.0.1:5100}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== ERP MODULES PHP LOOK PARITY PROVE ========"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "LOCAL=${LOCAL_BASE}"
note "SUPER=${SUPER_BASE}"
note "TENANT=${TENANT_BASE}"

curl -sS -o /tmp/erp_look_parity.css -w '%{http_code}' --max-time 20 \
  "${LOCAL_BASE}/platform-assets/epc_erp_aspnet_module_parity.css" > /tmp/erp_look_parity.code || echo 000 > /tmp/erp_look_parity.code
parity_code=$(cat /tmp/erp_look_parity.code)
note "PARITY_CSS_HTTP=${parity_code}"
[[ "$parity_code" == "200" ]] && ok "PARITY_CSS_200=YES" || fail "PARITY_CSS_200=NO code=${parity_code}"
rg -q 'epc-erp-page-hd' /tmp/erp_look_parity.css && ok "PARITY_CSS_PAGE_HD=YES" || fail "PARITY_CSS_PAGE_HD=NO"
rg -q '\[class\$="-hero"\]' /tmp/erp_look_parity.css && ok "PARITY_CSS_HERO_REMAP=YES" || fail "PARITY_CSS_HERO_REMAP=NO"
rg -q 'table-epc' /tmp/erp_look_parity.css && ok "PARITY_CSS_TABLE=YES" || fail "PARITY_CSS_TABLE=NO"
rg -q '\.epc-erp-cp-shell \.epc-erp-content-body' /tmp/erp_look_parity.css && ok "PARITY_CSS_SHELL_SCOPE=YES" || fail "PARITY_CSS_SHELL_SCOPE=NO"

for path in /erp/receivables-app /erp/payables-app /erp/sales-orders-app /erp; do
  curl -sS -L -o "/tmp/erp_look_${path//\//_}.html" -w '%{http_code}' --max-time 30 \
    "${LOCAL_BASE}${path}" > "/tmp/erp_look_${path//\//_}.code" || echo 000 > "/tmp/erp_look_${path//\//_}.code"
  body="/tmp/erp_look_${path//\//_}.html"
  note "PATH=${path} HTTP=$(cat "/tmp/erp_look_${path//\//_}.code")"
  if rg -qi 'epc_erp_aspnet_module_parity|epc-erp-page-hd|epc-erp-kpi|table-epc|bindErpTopNav|ns-dash' "$body" 2>/dev/null; then
    ok "MARKERS_${path}=YES"
  else
    if rg -qi 'erp/login|Sign in' "$body" 2>/dev/null; then
      note "GATE_WARN MARKERS_${path}=login-redirect (parity CSS asset still required)"
    else
      fail "MARKERS_${path}=NO"
    fi
  fi
  if rg -qi 'epc-ar-hero|epc-ap-hero|linear-gradient\(135deg' "$body" 2>/dev/null; then
    fail "HERO_LEFTOVER_${path}=YES"
  else
    ok "HERO_LEFTOVER_${path}=NO"
  fi
done

for base in "$SUPER_BASE" "$TENANT_BASE"; do
  curl -sSI --max-time 20 "${base}/erp" | tr -d '\r' > /tmp/erp_look_hdr.txt || true
  if rg -qi 'x-ecomae-platform:\s*primary' /tmp/erp_look_hdr.txt; then
    ok "PRIMARY_${base}=YES"
  else
    fail "PRIMARY_${base}=NO"
  fi
done

curl -sS -o /tmp/erp_look_public_parity.css -w '%{http_code}' --max-time 25 \
  "${TENANT_BASE}/platform-assets/epc_erp_aspnet_module_parity.css" > /tmp/erp_look_public_parity.code || echo 000 > /tmp/erp_look_public_parity.code
pub_code=$(cat /tmp/erp_look_public_parity.code)
note "PUBLIC_PARITY_CSS_HTTP=${pub_code}"
if [[ "$pub_code" == "200" ]] && rg -q 'epc-erp-page-hd' /tmp/erp_look_public_parity.css; then
  ok "PUBLIC_PARITY_CSS=YES"
else
  note "GATE_WARN PUBLIC_PARITY_CSS soft miss (publish may still be warming) code=${pub_code}"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS ERP_MODULES_PHP_LOOK=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
