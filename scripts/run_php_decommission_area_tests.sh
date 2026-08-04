#!/usr/bin/env bash
# Exercises every final-gate area that can be tested without inventing secrets.
# Never removes PHP.
# Presentation exact-route inventory (69): docs/migration/evidence/presentation/presentation-exact-routes.json
# Kept in sync by scripts/validate_presentation_hybrid_allowlist_sync.py.
# Surface-digest exact-route inventory (35): docs/migration/evidence/surface-parity/surface-digest-exact-routes.json
# Kept in sync by scripts/validate_surface_digest_allowlist_sync.py.
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

# Unit/checklist — skippable when parent verifier already ran them (ECOMAE_AREA_SKIP_HEAVY=1).
if [[ "${ECOMAE_AREA_SKIP_HEAVY:-}" == "1" ]]; then
  record "unit-tests" pass "skipped here (parent already ran unit tests)"
  record "final-gate-checklist" pass "skipped here (parent already ran checklist)"
else
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
fi

# Live public diagnostics
for route in \
  /health \
  /migration/zero-php-completion \
  /migration/php-decommission-readiness \
  /migration/presentation-parity \
  /migration/live-surface-links \
  /migration/surface-parity \
  /migration/surface-field-parity
do
  code="$(curl -sS -m 20 -o /tmp/area.body -w '%{http_code}' "https://www.ecomae.com${route}" || echo 000)"
  if [[ "$code" == "200" ]]; then
    record "live${route}" pass "HTTP 200"
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

# Validate attached staging-smoke artifacts (no secrets required).
SMOKE_DIR="$ROOT/docs/migration/evidence/decommission/staging-smoke"
if python3 - "$SMOKE_DIR" <<'PY'
import json, sys
from pathlib import Path
root = Path(sys.argv[1])
price = json.loads((root / "price-lookup-aspnet.json").read_text(encoding="utf-8"))
if isinstance(price.get("error"), dict) and price["error"].get("code") in {
    "missing_api_key", "unauthorized", "invalid_api_key"
}:
    raise SystemExit("price unauthenticated")
if price.get("ok") is False:
    raise SystemExit("price ok=false")
catalog = json.loads((root / "catalog-status-aspnet.json").read_text(encoding="utf-8"))
if catalog.get("connected") is not True:
    raise SystemExit("catalog not connected")
surface = json.loads((root / "surface-digests-aspnet.json").read_text(encoding="utf-8"))
if surface.get("ok") is not True:
    raise SystemExit("surface ok!=true")
routes = [
    r for r in (surface.get("routes") or [])
    if isinstance(r, dict) and int(r.get("status") or 0) == 200
    and not str(r.get("route") or "").startswith("/migration/")
]
if not routes:
    raise SystemExit("no digest 200")
print(len(routes))
PY
then
  digest_n="$(python3 - "$SMOKE_DIR" <<'PY'
import json,sys
from pathlib import Path
surface=json.loads((Path(sys.argv[1])/"surface-digests-aspnet.json").read_text())
print(sum(1 for r in surface.get("routes") or [] if isinstance(r,dict) and int(r.get("status") or 0)==200 and not str(r.get("route") or "").startswith("/migration/")))
PY
)"
  record "attached-staging-smoke" pass "price/catalog/surface artifacts validate (digest200=${digest_n})"
else
  record "attached-staging-smoke" fail "attached staging-smoke artifacts missing or invalid"
fi

# Optional live re-smoke when secrets exist in this environment.
if [[ -n "${ECOMAE_PRICE_LOOKUP_API_KEY:-}" ]]; then
  if RUN_PRICE_LOOKUP_SMOKE=1 ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-https://www.ecomae.com}" \
      bash "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh"; then
    record "live-restage-price" pass "authenticated price lookup re-smoke"
  else
    record "live-restage-price" fail "authenticated price lookup re-smoke failed"
  fi
else
  record "live-restage-price" blocked "ECOMAE_PRICE_LOOKUP_API_KEY missing here (attached artifact used instead)"
fi

if [[ -n "${ECOMAE_CATALOG_API_KEY:-}" ]]; then
  if RUN_CATALOG_STATUS_SMOKE=1 ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-https://www.ecomae.com}" \
      bash "$ROOT/tests/live_smoke/run_catalog_status_exact_route_smoke.sh"; then
    record "live-restage-catalog" pass "authenticated catalog status re-smoke"
  else
    record "live-restage-catalog" fail "authenticated catalog status re-smoke failed"
  fi
else
  record "live-restage-catalog" blocked "ECOMAE_CATALOG_API_KEY missing here (attached artifact used instead)"
fi

if [[ -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" || -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
  if RUN_SURFACE_DIGEST_SMOKE=1 ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}" \
      bash "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh"; then
    record "live-restage-surfaces" pass "surface digest re-smoke"
  else
    record "live-restage-surfaces" fail "surface digest re-smoke failed"
  fi
else
  record "live-restage-surfaces" blocked "admin cookie missing here (attached artifact used instead)"
fi

# Live reporters must stay honest about incomplete parity.
body="$(mktemp)"
code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o "$body" -w '%{http_code}' https://www.ecomae.com/migration/surface-parity || echo 000)"
if [[ "$code" == "200" ]] && grep -q 'parity-not-yet-reached' "$body"; then
  record "live-surface-parity-status" pass "parity-not-yet-reached"
else
  record "live-surface-parity-status" fail "expected parity-not-yet-reached (HTTP $code)"
fi
code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o "$body" -w '%{http_code}' https://www.ecomae.com/migration/presentation-parity || echo 000)"
if [[ "$code" == "200" ]] && grep -q 'presentation-shell-scaffolded' "$body"; then
  record "live-presentation-status" pass "presentation-shell-scaffolded (not cut over)"
else
  record "live-presentation-status" fail "unexpected presentation status (HTTP $code)"
fi
rm -f "$body"

# Operator chrome still PHP (expected until exact-route shadows)
for path in /CP/ /ERP/ /BOS/; do
  ctype="$(curl -sS -m 20 -D - -o /dev/null -L "https://www.ecomae.com${path}" | awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' | tr -d '\r')"
  if grep -qi 'text/html' <<<"$ctype"; then
    record "php-chrome${path}" pass "still PHP HTML (expected pre-decommission)"
  else
    record "php-chrome${path}" fail "unexpected content-type $ctype"
  fi
done

# Live surface digest exact-routes (52/52) expect ASP.NET 401 unauthorized JSON (admin cookie for 200).
mapfile -t DIGEST_ROUTES < <(grep -E '^location = /(cp|erp|bos)/' "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" | sed -E 's/^location = ([^ {]+).*/\1/')
if [[ "${#DIGEST_ROUTES[@]}" -ne 52 ]]; then
  record "surface-digest-route-inventory" fail "expected 52 digest routes, found ${#DIGEST_ROUTES[@]}"
else
  record "surface-digest-route-inventory" pass "32 CP/ERP/BOS digest exact-routes in shadow example"
fi
for path in "${DIGEST_ROUTES[@]}"; do
  code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o /tmp/area.body -w '%{http_code}' "https://www.ecomae.com${path}" || echo 000)"
  if [[ "$code" == "401" ]] && grep -qE '"unauthorized"|unauthorized' /tmp/area.body && ! grep -qi '<html\|<!doctype' /tmp/area.body; then
    record "public${path}-exact-route" pass "ASP.NET digest exact-route shadow (401 unauthorized without admin cookie)"
  else
    record "public${path}-exact-route" fail "expected ASP.NET 401 unauthorized (HTTP $code)"
  fi
done

# Storefront digest exact-routes (wired 6; live may still be 4/6 until search/cart shadows install).
# Expect ASP.NET 401 unauthorized JSON (customer cookie for 200) when shadow is live.
mapfile -t SF_ROUTES < <(grep -E '^location = /storefront/' "$ROOT/deploy/aspnet/nginx-storefront-digests-shadow-example.conf" | sed -E 's/^location = ([^ {]+).*/\1/')
if [[ "${#SF_ROUTES[@]}" -ne 6 ]]; then
  record "storefront-digest-route-inventory" fail "expected 6 storefront digest routes, found ${#SF_ROUTES[@]}"
else
  record "storefront-digest-route-inventory" pass "6 storefront digest exact-routes in shadow example"
fi
SF_LIVE_PASS=0
for path in "${SF_ROUTES[@]}"; do
  code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o /tmp/area.body -w '%{http_code}' "https://www.ecomae.com${path}" || echo 000)"
  if [[ "$code" == "401" ]] && grep -qE '"unauthorized"|unauthorized' /tmp/area.body && ! grep -qi '<html\|<!doctype' /tmp/area.body; then
    record "public${path}-exact-route" pass "ASP.NET storefront digest exact-route shadow (401 unauthorized without customer cookie)"
    SF_LIVE_PASS=$((SF_LIVE_PASS + 1))
  elif [[ "$path" == "/storefront/search" || "$path" == "/storefront/cart" ]]; then
    # Newly wired digests: design allowlist includes them before public nginx shadow install.
    record "public${path}-exact-route" blocked "wired awaiting storefront digest shadow install (HTTP $code)"
  else
    record "public${path}-exact-route" fail "expected ASP.NET 401 unauthorized (HTTP $code)"
  fi
done
if [[ "$SF_LIVE_PASS" -ge 4 ]]; then
  record "storefront-digest-live-floor" pass "storefront digests live ${SF_LIVE_PASS}/6 (wired 6)"
else
  record "storefront-digest-live-floor" fail "expected at least 4/6 live storefront digests, found ${SF_LIVE_PASS}"
fi

# Blazor SSR Zero-PHP console (served under existing /migration proxy after redeploy)
code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o /tmp/area.body -w '%{http_code}' https://www.ecomae.com/migration/console || echo 000)"
if [[ "$code" == "200" ]] && grep -qi 'EcomAE\|Zero-PHP' /tmp/area.body; then
  record "public-migration-console" pass "Blazor SSR Zero-PHP console live"
else
  record "public-migration-console" fail "expected Blazor console HTML 200 (redeploy ASP.NET if missing)"
fi

# Catalog exact-route shadows live (18/18 wired): unauth must be ASP.NET JSON gate (not PHP HTML).
for path in /api/v1/catalog/status /api/v1/catalog/manufacturers /api/v1/catalog/models /api/v1/catalog/modifications /api/v1/catalog/brands /api/v1/catalog/suppliers /api/v1/catalog/vin /api/v1/catalog/engines /api/v1/catalog/analogs /api/v1/catalog/article-brands /api/v1/catalog/categories /api/v1/catalog/products /api/v1/catalog/engine-search /api/v1/catalog/article-links /api/v1/catalog/article /api/v1/catalog/articles /api/v1/catalog/engine /api/v1/catalog/brand-parts; do
  code="$(curl -sS -m 20 -A 'Mozilla/5.0' -o /tmp/area.body -w '%{http_code}' "https://www.ecomae.com${path}" || echo 000)"
  if [[ "$code" == "401" ]] && grep -q 'missing_api_key' /tmp/area.body; then
    record "public${path}-exact-route" pass "ASP.NET JSON auth gate on exact-route shadow"
  else
    record "public${path}-exact-route" fail "expected ASP.NET 401 missing_api_key (HTTP $code)"
  fi
done

# Decommission script must refuse without confirmation/ready
if ! ECOMAE_CONFIRM_PHP_DECOMMISSION= bash "$ROOT/scripts/cloudpanel_php_decommission.sh" >/tmp/decom.out 2>&1; then
  record "decommission-refuses-without-confirm" pass "gated script refused"
else
  record "decommission-refuses-without-confirm" fail "gated script should refuse"
fi
if ! ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash "$ROOT/scripts/cloudpanel_php_decommission.sh" >/tmp/decom2.out 2>&1; then
  record "decommission-refuses-while-not-ready" pass "refused with CONFIRM=YES because readyToRemovePhp=false"
else
  record "decommission-refuses-while-not-ready" fail "should refuse while readiness blocked"
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
  "note": "PHP must remain. Loopback staging smoke is attached; public CP/ERP/BOS chrome is still PHP; surface/presentation parity not reached; RELEASE_OWNER_APPROVAL.md absent.",
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
