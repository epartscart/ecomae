#!/usr/bin/env bash
# Exercises every final-gate area that can be tested without inventing secrets.
# Never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_GATE_OUT_DIR:-$ROOT/docs/migration/evidence/decommission/public-probes}"
mkdir -p "$OUT_DIR"
REPORT="$OUT_DIR/www-final-gate-area-tests.json"
pass=0
fail=0
block=0
results_tmp="$(mktemp)"

record() {
  local area="$1" status="$2" detail="$3"
  printf '%s\t%s\t%s\n' "$area" "$status" "$detail" >>"$results_tmp"
  case "$status" in
    pass) pass=$((pass + 1)); printf '  PASS  %s — %s\n' "$area" "$detail" ;;
    fail) fail=$((fail + 1)); printf '  FAIL  %s — %s\n' "$area" "$detail" ;;
    blocked) block=$((block + 1)); printf '  BLOCK %s — %s\n' "$area" "$detail" ;;
  esac
}

echo "== Final-gate area tests (no PHP removal) =="

# Unit/foundation
if (cd "$ROOT/aspnet" && dotnet test tests/EcomAE.Platform.Tests/EcomAE.Platform.Tests.csproj --nologo -v q); then
  record "unit-tests" pass "EcomAE.Platform.Tests green"
else
  record "unit-tests" fail "EcomAE.Platform.Tests failed"
fi

if bash "$ROOT/scripts/run_zero_php_final_gate_checklist.sh" >/tmp/final-gate-checklist.out 2>&1; then
  record "final-gate-checklist" pass "checklist script exited 0"
else
  record "final-gate-checklist" fail "checklist script failed"
fi

# Live public diagnostics
for route in \
  /health \
  /migration/zero-php-completion \
  /migration/php-decommission-readiness \
  /migration/presentation-parity \
  /migration/live-surface-links \
  /migration/surface-parity
do
  code="$(curl -sS -m 20 -o /tmp/area.body -w '%{http_code}' "https://www.ecomae.com${route}" || echo 000)"
  if [[ "$code" == "200" ]]; then
    record "live${route}" pass "HTTP 200"
  elif [[ "$route" == "/migration/live-surface-links" && "$code" == "404" ]]; then
    record "live${route}" blocked "HTTP 404 until CloudPanel redeploy of main includes live-surface-links"
  else
    record "live${route}" fail "HTTP $code"
  fi
done

# Price lookup already on ASP.NET (unauthenticated gate)
code="$(curl -sS -m 20 -o /tmp/area.body -w '%{http_code}' "https://www.ecomae.com/api/v1/price/lookup" || echo 000)"
if [[ "$code" == "401" ]] && grep -q missing_api_key /tmp/area.body; then
  record "live-price-lookup-unauth" pass "ASP.NET JSON auth gate"
else
  record "live-price-lookup-unauth" fail "expected ASP.NET 401 missing_api_key"
fi

# Authenticated smokes — blocked without secrets
if [[ -z "${ECOMAE_PRICE_LOOKUP_API_KEY:-}" ]]; then
  record "staging-smoke-price" blocked "ECOMAE_PRICE_LOOKUP_API_KEY missing in this environment"
else
  if RUN_PRICE_LOOKUP_SMOKE=1 ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-https://www.ecomae.com}" \
      bash "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh"; then
    record "staging-smoke-price" pass "authenticated price lookup smoke"
  else
    record "staging-smoke-price" fail "authenticated price lookup smoke failed"
  fi
fi

if [[ -z "${ECOMAE_CATALOG_API_KEY:-}" ]]; then
  record "staging-smoke-catalog" blocked "ECOMAE_CATALOG_API_KEY missing in this environment"
else
  if RUN_CATALOG_STATUS_SMOKE=1 ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-https://www.ecomae.com}" \
      bash "$ROOT/tests/live_smoke/run_catalog_status_exact_route_smoke.sh"; then
    record "staging-smoke-catalog" pass "authenticated catalog status smoke"
  else
    record "staging-smoke-catalog" fail "authenticated catalog status smoke failed"
  fi
fi

if [[ -z "${ECOMAE_ADMIN_COOKIE_HEADER:-}" && -z "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  record "staging-smoke-surfaces" blocked "admin cookie missing in this environment"
else
  if RUN_SURFACE_DIGEST_SMOKE=1 ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}" \
      bash "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh"; then
    record "staging-smoke-surfaces" pass "surface digest smoke"
  else
    record "staging-smoke-surfaces" fail "surface digest smoke failed"
  fi
fi

# Operator chrome still PHP (expected until exact-route shadows)
for path in /CP/ /ERP/ /BOS/; do
  ctype="$(curl -sS -m 20 -D - -o /dev/null -L "https://www.ecomae.com${path}" | awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' | tr -d '\r')"
  if grep -qi 'text/html' <<<"$ctype"; then
    record "php-chrome${path}" pass "still PHP HTML (expected pre-decommission)"
  else
    record "php-chrome${path}" fail "unexpected content-type $ctype"
  fi
done

# Decommission script must refuse without confirmation/ready
if ! ECOMAE_CONFIRM_PHP_DECOMMISSION= bash "$ROOT/scripts/cloudpanel_php_decommission.sh" >/tmp/decom.out 2>&1; then
  record "decommission-refuses-without-confirm" pass "gated script refused"
else
  record "decommission-refuses-without-confirm" fail "gated script should refuse"
fi

python3 - "$REPORT" "$results_tmp" "$pass" "$fail" "$block" <<'PY'
import json, sys, datetime
out, src, p, f, b = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
rows=[]
with open(src, encoding='utf-8') as fh:
    for line in fh:
        area, status, detail = line.rstrip('\n').split('\t', 2)
        rows.append({"area": area, "status": status, "detail": detail})
payload={
  "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace('+00:00','Z'),
  "passed": p,
  "failed": f,
  "blocked": b,
  "readyToRemovePhp": False,
  "note": "PHP decommission blocked until authenticated smoke + RELEASE_OWNER_APPROVAL.md exist on CloudPanel.",
  "results": rows,
}
with open(out,'w',encoding='utf-8') as fh:
    json.dump(payload, fh, indent=2)
    fh.write('\n')
print(out)
PY

rm -f "$results_tmp"
echo "----------------------------"
echo "Passed: $pass  Blocked: $block  Failed: $fail"
echo "Artifact: $REPORT"
echo "PHP was NOT decommissioned."
exit $(( fail > 0 ? 1 : 0 ))
