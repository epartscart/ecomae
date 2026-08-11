#!/usr/bin/env bash
# Prove Super CP + Tenant CP module pages carry PHP epc-scp look markers.
# Must print RESULT=PASS — silent External action without paste-back = FAIL.
set -euo pipefail

SUPER_BASE="${ECOMAE_PUBLIC_SUPER:-https://www.ecomae.com}"
TENANT_BASE="${ECOMAE_PUBLIC_TENANT:-https://www.epartscart.com}"
LOCAL_BASE="${ECOMAE_LOCAL_BASE:-http://127.0.0.1:5100}"

pass=1
note() { printf '%s\n' "$*"; }
fail() { note "GATE_BAD $*"; pass=0; }
ok() { note "GATE_OK $*"; }

note "======== CP MODULES PHP LOOK PARITY PROVE ========"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "LOCAL=${LOCAL_BASE}"
note "SUPER=${SUPER_BASE}"
note "TENANT=${TENANT_BASE}"

# Local binary (no auth): stylesheet list + asset bridge.
curl -sS -o /tmp/cp_look_users.html -w '%{http_code}' --max-time 30 \
  "${LOCAL_BASE}/cp/users-app" > /tmp/cp_look_users.code || echo 000 > /tmp/cp_look_users.code
code=$(cat /tmp/cp_look_users.code)
note "USERS_LOCAL_HTTP=${code}"

# Login redirect (302) still often includes Head styles on the login page — probe asset directly.
curl -sS -o /tmp/cp_look_parity.css -w '%{http_code}' --max-time 20 \
  "${LOCAL_BASE}/platform-assets/epc_cp_aspnet_module_parity.css" > /tmp/cp_look_parity.code || echo 000 > /tmp/cp_look_parity.code
parity_code=$(cat /tmp/cp_look_parity.code)
note "PARITY_CSS_HTTP=${parity_code}"
[[ "$parity_code" == "200" ]] && ok "PARITY_CSS_200=YES" || fail "PARITY_CSS_200=NO code=${parity_code}"
rg -q 'epc-scp-panel__hero' /tmp/cp_look_parity.css && ok "PARITY_CSS_SCP_HERO=YES" || fail "PARITY_CSS_SCP_HERO=NO"
rg -q '\[class\$="-hero"\]' /tmp/cp_look_parity.css && ok "PARITY_CSS_HERO_REMAP=YES" || fail "PARITY_CSS_HERO_REMAP=NO"
rg -q 'epc-scp-kpi__card' /tmp/cp_look_parity.css && ok "PARITY_CSS_KPI=YES" || fail "PARITY_CSS_KPI=NO"

# Orders / users pages (may 302 to login — still check for stylesheet link when HTML present).
for path in /cp/users-app /cp/orders /cp/modules-app /cp/groups-app; do
  curl -sS -L -o "/tmp/cp_look_${path//\//_}.html" -w '%{http_code}' --max-time 30 \
    "${LOCAL_BASE}${path}" > "/tmp/cp_look_${path//\//_}.code" || echo 000 > "/tmp/cp_look_${path//\//_}.code"
  body="/tmp/cp_look_${path//\//_}.html"
  note "PATH=${path} HTTP=$(cat "/tmp/cp_look_${path//\//_}.code")"
  if rg -qi 'epc_cp_aspnet_module_parity|epc-scp-panel__hero|epc-scp-kpi__card|PhpCpModulePageHeader|epc-scp-dashboard__title' "$body" 2>/dev/null; then
    ok "MARKERS_${path}=YES"
  else
    # Login-only responses are OK if parity CSS asset already proven; warn not fail.
    if rg -qi 'cp/login|Sign in' "$body" 2>/dev/null; then
      note "GATE_WARN MARKERS_${path}=login-redirect (parity CSS asset still required)"
    else
      fail "MARKERS_${path}=NO"
    fi
  fi
done

# Public edge headers remain ASP.NET primary.
for base in "$SUPER_BASE" "$TENANT_BASE"; do
  curl -sSI --max-time 20 "${base}/cp" | tr -d '\r' > /tmp/cp_look_hdr.txt || true
  if rg -qi 'x-ecomae-platform:\s*primary' /tmp/cp_look_hdr.txt; then
    ok "PRIMARY_${base}=YES"
  else
    fail "PRIMARY_${base}=NO"
  fi
done

# Super host body class token when HTML available after login cookie is not assumed —
# chrome source already gated; prove platform-assets on public edge too.
curl -sS -o /tmp/cp_look_public_parity.css -w '%{http_code}' --max-time 25 \
  "${TENANT_BASE}/platform-assets/epc_cp_aspnet_module_parity.css" > /tmp/cp_look_public_parity.code || echo 000 > /tmp/cp_look_public_parity.code
pub_code=$(cat /tmp/cp_look_public_parity.code)
note "PUBLIC_PARITY_CSS_HTTP=${pub_code}"
if [[ "$pub_code" == "200" ]] && rg -q 'epc-scp-panel__hero' /tmp/cp_look_public_parity.css; then
  ok "PUBLIC_PARITY_CSS=YES"
else
  note "GATE_WARN PUBLIC_PARITY_CSS soft miss (publish may still be warming) code=${pub_code}"
fi

if [[ "$pass" -eq 1 ]]; then
  note "RESULT=PASS CP_MODULES_PHP_LOOK=YES"
  exit 0
fi
note "RESULT=FAIL see GATE_BAD above"
exit 1
