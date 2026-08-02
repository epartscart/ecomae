#!/usr/bin/env bash
# Field / function / presentation parity harness for CP, ERP, BOS, storefront, API.
# Never enables nginx cutover and never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${ECOMAE_PARITY_OUT_DIR:-$ROOT/docs/migration/evidence/surface-parity}"
SAMPLES="$OUT_DIR/samples"
REPORT="$OUT_DIR/harness-report.json"
BASE_URL="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
ASPNET_BASE="${ECOMAE_ASPNET_BASE_URL:-}"
mkdir -p "$SAMPLES" "$OUT_DIR"

pass=0
fail=0
block=0
tmp="$(mktemp)"
: >"$tmp"

record() {
  local area="$1" status="$2" detail="$3"
  printf '%s\t%s\t%s\n' "$area" "$status" "$detail" >>"$tmp"
  case "$status" in
    pass) pass=$((pass + 1)); printf '  PASS  %s — %s\n' "$area" "$detail" ;;
    fail) fail=$((fail + 1)); printf '  FAIL  %s — %s\n' "$area" "$detail" ;;
    blocked) block=$((block + 1)); printf '  BLOCK %s — %s\n' "$area" "$detail" ;;
  esac
}

echo "== Surface field/function/presentation parity harness =="
echo "PHP remains authoritative. Cutover is not enabled by this script."

if (cd "$ROOT/aspnet" && dotnet test tests/EcomAE.Platform.Tests/EcomAE.Platform.Tests.csproj --nologo --filter "FullyQualifiedName~SurfaceFieldParity|FullyQualifiedName~LegacyHtmlShell|FullyQualifiedName~PresentationParity|FullyQualifiedName~SurfaceShell" -v q); then
  record "unit-contracts" pass "surface/presentation contract tests green"
else
  record "unit-contracts" fail "surface/presentation contract tests failed"
fi

# Presentation assets must resolve on the live PHP host (same hrefs ASP.NET shells use).
python3 - "$ROOT" "$BASE_URL" <<'PY' >"$OUT_DIR/presentation-asset-check.json"
import json, subprocess, sys, datetime
from pathlib import Path
root = Path(sys.argv[1])
base = sys.argv[2].rstrip("/")
# Pull stylesheet lists from C# source constants via a tiny parse of LegacyPresentationAssets.cs
text = (root / "aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs").read_text(encoding="utf-8")
import re
blocks = {
  "cp": "ControlPanelStylesheets",
  "erp": "ErpStylesheets",
  "bos": "BosStylesheets",
  "storefront": "StorefrontStylesheets",
}
assets=[]
for surface, name in blocks.items():
    m=re.search(rf"readonly IReadOnlyList<string> {name}\s*=\s*\[(.*?)\];", text, re.S)
    hrefs=re.findall(r'"([^"]+)"', m.group(1) if m else "")
    for href in hrefs:
        url = href if href.startswith("http") else base + href
        try:
            code=subprocess.check_output(["curl","-sS","-o","/dev/null","-w","%{http_code}","-m","20","-L",url], text=True).strip()
        except Exception:
            code="000"
        assets.append({"surface": surface, "href": href, "url": url, "status": int(code) if code.isdigit() else 0, "ok": code in {"200","301","302"}})
payload={
  "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00","Z"),
  "baseUrl": base,
  "assets": assets,
  "okCount": sum(1 for a in assets if a["ok"]),
  "total": len(assets),
}
print(json.dumps(payload, indent=2))
PY

ok_n="$(python3 -c 'import json,sys; d=json.load(open(sys.argv[1],encoding="utf-8")); print(d["okCount"])' "$OUT_DIR/presentation-asset-check.json")"
total_n="$(python3 -c 'import json,sys; d=json.load(open(sys.argv[1],encoding="utf-8")); print(d["total"])' "$OUT_DIR/presentation-asset-check.json")"
if [[ "$ok_n" -eq "$total_n" && "$total_n" -gt 0 ]]; then
  record "presentation-assets" pass "$ok_n/$total_n PHP chrome CSS URLs resolve on $BASE_URL"
else
  record "presentation-assets" fail "$ok_n/$total_n PHP chrome CSS URLs resolve on $BASE_URL"
fi

# Brand mark
brand_code="$(curl -sS -m 20 -o /dev/null -w '%{http_code}' -L "$BASE_URL/content/general_pages/epc_ecomae_logo_svg.php" || echo 000)"
if [[ "$brand_code" == "200" ]]; then
  record "presentation-brand" pass "ECOM AE brand mark URL returns HTTP 200"
else
  record "presentation-brand" fail "brand mark HTTP $brand_code"
fi

# Live migration reporters
for route in /migration/presentation-parity /migration/surface-parity /migration/live-surface-links; do
  code="$(curl -sS -m 20 -o /tmp/parity.body -w '%{http_code}' "$BASE_URL$route" || echo 000)"
  if [[ "$code" == "200" ]]; then
    record "live$route" pass "HTTP 200"
  else
    record "live$route" fail "HTTP $code"
  fi
done

# surface-field-parity may 404 until this PR is deployed
code="$(curl -sS -m 20 -o /tmp/parity.body -w '%{http_code}' "$BASE_URL/migration/surface-field-parity" || echo 000)"
if [[ "$code" == "200" ]]; then
  record "live/migration/surface-field-parity" pass "HTTP 200"
elif [[ "$code" == "404" ]]; then
  record "live/migration/surface-field-parity" blocked "HTTP 404 until redeploy includes this PR"
else
  record "live/migration/surface-field-parity" fail "HTTP $code"
fi

# Operator chrome still PHP (expected)
for path in /CP/ /ERP/ /BOS/; do
  ctype="$(curl -sS -m 20 -D - -o /dev/null -L "$BASE_URL$path" | awk -F': ' 'tolower($1)=="content-type"{print $2; exit}' | tr -d '\r' || true)"
  if grep -qi 'text/html' <<<"$ctype"; then
    record "php-chrome$path" pass "still PHP HTML (no cutover)"
  else
    record "php-chrome$path" fail "unexpected content-type $ctype"
  fi
done

# Optional authenticated dual-sample capture against ASP.NET loopback/base
if [[ -n "$ASPNET_BASE" && ( -n "${ECOMAE_ADMIN_COOKIE_HEADER:-}" || -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ) ]]; then
  auth_args=()
  if [[ -n "${ECOMAE_ADMIN_COOKIE_JAR:-}" ]]; then
    auth_args+=(-b "$ECOMAE_ADMIN_COOKIE_JAR")
  else
    auth_args+=(-H "Cookie: ${ECOMAE_ADMIN_COOKIE_HEADER}")
  fi
  for route in \
    /cp/dashboard-summary \
    /cp/config-items?limit=5 \
    /erp/dashboard-summary \
    /erp/accounts-summary \
    /erp/cash-accounts?limit=5 \
    /bos/fleet-summary \
    /bos/tenants?limit=5 \
    /bos/fleet-health
  do
    path_only="${route%%\?*}"
    name="$(echo "$path_only" | tr '/' '_')"
    out="$SAMPLES/aspnet${name}.json"
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' "${auth_args[@]}" "${ASPNET_BASE}${route}" || echo 000)"
    if [[ "$code" == "200" ]]; then
      record "aspnet$path_only" pass "captured $out"
      case "$path_only" in
        /cp/dashboard-summary)
          req="users,adminSessions,portalTenants,activePortalTenants,source,message"
          path_arg="summary"
          ;;
        /erp/dashboard-summary|/erp/accounts-summary)
          req="cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message"
          path_arg="summary"
          ;;
        /bos/fleet-summary|/bos/fleet-health)
          req="portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message"
          path_arg="summary"
          ;;
        *)
          req=""
          path_arg=""
          ;;
      esac
      if [[ -n "$req" ]]; then
        if python3 "$ROOT/scripts/compare_surface_payload_parity.py" --left "$out" --right "$out" --path "$path_arg" --contract-only --require "$req"; then
          record "contract$path_only" pass "summary field contract satisfied"
        else
          record "contract$path_only" fail "summary field contract failed"
        fi
      fi
    elif [[ "$code" == "401" ]]; then
      record "aspnet$path_only" blocked "HTTP 401 with provided cookie"
    else
      record "aspnet$path_only" fail "HTTP $code"
    fi
  done
else
  record "authenticated-digest-capture" blocked "set ECOMAE_ASPNET_BASE_URL and admin cookie to capture dual samples"
fi

# Optional customer-session storefront digest capture
if [[ -n "$ASPNET_BASE" && ( -n "${ECOMAE_CUSTOMER_COOKIE_HEADER:-}" || -n "${ECOMAE_CUSTOMER_COOKIE_JAR:-}" ) ]]; then
  cust_args=()
  if [[ -n "${ECOMAE_CUSTOMER_COOKIE_JAR:-}" ]]; then
    cust_args+=(-b "$ECOMAE_CUSTOMER_COOKIE_JAR")
  else
    cust_args+=(-H "Cookie: ${ECOMAE_CUSTOMER_COOKIE_HEADER}")
  fi
  for route in /storefront/account-summary /storefront/orders?limit=5 /storefront/garage?limit=5 /storefront/profile; do
    path_only="${route%%\?*}"
    name="$(echo "$path_only" | tr '/' '_')"
    out="$SAMPLES/aspnet${name}.json"
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' "${cust_args[@]}" "${ASPNET_BASE}${route}" || echo 000)"
    if [[ "$code" == "200" ]]; then
      record "aspnet$path_only" pass "captured $out"
    elif [[ "$code" == "401" ]]; then
      record "aspnet$path_only" blocked "HTTP 401 with customer cookie"
    else
      record "aspnet$path_only" fail "HTTP $code"
    fi
  done
else
  record "storefront-digest-capture" blocked "set ECOMAE_CUSTOMER_COOKIE_HEADER/JAR (session=...; u_id=...) for storefront digests"
fi

# Migration-mode contract samples (no secrets) must satisfy locked field contracts.
python3 "$ROOT/scripts/generate_migration_digest_contract_samples.py" >/dev/null
declare -A CONTRACT_REQUIREMENTS=(
  [cp-dashboard-summary.json]="users,adminSessions,portalTenants,activePortalTenants,source,message"
  [erp-dashboard-summary.json]="cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message"
  [erp-accounts-summary.json]="cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message"
  [bos-fleet-summary.json]="portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message"
  [bos-fleet-health.json]="portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message"
  [storefront-account-summary.json]="userId,orders,sessions,garageVehicles,source,message"
  [erp-inventory-stock.json]="rowCount,qtyOnHand,stockValue,warehouseCount,itemCount,source,message"
)
mig_fail=0
for sample in "${!CONTRACT_REQUIREMENTS[@]}"; do
  path="$SAMPLES/migration/$sample"
  req="${CONTRACT_REQUIREMENTS[$sample]}"
  path_arg="summary"
  if python3 "$ROOT/scripts/compare_surface_payload_parity.py" --left "$path" --right "$path" --path "$path_arg" --contract-only --require "$req"; then
    record "migration-contract/$sample" pass "field contract satisfied"
  else
    record "migration-contract/$sample" fail "field contract failed"
    mig_fail=$((mig_fail + 1))
  fi
done
if python3 "$ROOT/scripts/compare_surface_payload_parity.py" --left "$SAMPLES/migration/bos-fleet-readiness.json" --right "$SAMPLES/migration/bos-fleet-readiness.json" --path readiness --contract-only \
  --require tenants,pass,warn,fail,active,withDatabase,erpOnly,source,message; then
  record "migration-contract/bos-fleet-readiness.json" pass "field contract satisfied"
else
  record "migration-contract/bos-fleet-readiness.json" fail "field contract failed"
fi

# Empty list digests must still expose the contracted collection key + envelope fields.
declare -A LIST_CONTRACTS=(
  [cp-tenants.json]=tenants
  [cp-users.json]=users
  [cp-groups.json]=groups
  [cp-modules.json]=modules
  [cp-menus.json]=menus
  [cp-pages.json]=pages
  [cp-currencies.json]=currencies
  [cp-api-clients.json]=clients
  [cp-config-items.json]=items
  [cp-admin-sessions.json]=sessions
  [cp-storages.json]=storages
  [erp-suppliers.json]=suppliers
  [erp-purchases.json]=purchases
  [erp-cash-accounts.json]=accounts
  [erp-cash-entries.json]=entries
  [erp-coa-accounts.json]=accounts
  [erp-warehouses.json]=warehouses
  [erp-sales-orders.json]=orders
  [erp-purchase-orders.json]=orders
  [erp-invoices.json]=invoices
  [erp-gl-journals.json]=journals
  [bos-tenants.json]=tenants
  [bos-audit-log.json]=entries
  [storefront-orders.json]=orders
  [storefront-garage.json]=vehicles
)
for sample in "${!LIST_CONTRACTS[@]}"; do
  path="$SAMPLES/migration/$sample"
  key="${LIST_CONTRACTS[$sample]}"
  if python3 - "$path" "$key" <<'PY'
import json, sys
path, key = sys.argv[1], sys.argv[2]
doc = json.load(open(path, encoding="utf-8"))
required = ["ok", "surface", key, "count", "source", "message", "session", "note"]
missing = [k for k in required if k not in doc]
sys.exit(1 if missing else 0)
PY
  then
    record "migration-list-contract/$sample" pass "envelope + $key present"
  else
    record "migration-list-contract/$sample" fail "missing envelope fields for $key"
    mig_fail=$((mig_fail + 1))
  fi
done

if python3 - "$SAMPLES/migration/api-catalog-status.json" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
for key in ("connected", "message", "status_code", "counts", "source"):
    if key not in doc:
        raise SystemExit(1)
counts = doc["counts"]
for key in ("manufacturers", "models", "modifications", "brands", "vins"):
    if key not in counts:
        raise SystemExit(1)
PY
then
  record "migration-contract/api-catalog-status.json" pass "PHP-shaped catalog status contract satisfied"
else
  record "migration-contract/api-catalog-status.json" fail "catalog status contract failed"
  mig_fail=$((mig_fail + 1))
fi

# Catalog list / offline-cache / vin / brand-parts migration samples via compare scripts.
for kind in manufacturers models modifications brands suppliers; do
  sample="$SAMPLES/migration/api-catalog-${kind}.json"
  if [[ -f "$sample" ]] && python3 "$ROOT/scripts/compare_catalog_list_parity.py" "$kind" "$sample" "$sample" --contract-only; then
    record "migration-catalog-list/$kind" pass "envelope contract via compare_catalog_list_parity.py"
  else
    record "migration-catalog-list/$kind" fail "catalog list contract failed for $kind"
    mig_fail=$((mig_fail + 1))
  fi
done
for kind in engines analogs article-brands categories products engine-search article-links article articles engine; do
  sample="$SAMPLES/migration/api-catalog-${kind}.json"
  if [[ -f "$sample" ]] && python3 "$ROOT/scripts/compare_catalog_offline_cache_parity.py" "$kind" "$sample" "$sample" --contract-only; then
    record "migration-catalog-offline/$kind" pass "offline-cache envelope contract"
  else
    record "migration-catalog-offline/$kind" fail "offline-cache contract failed for $kind"
    mig_fail=$((mig_fail + 1))
  fi
done
if [[ -f "$SAMPLES/migration/api-catalog-vin.json" ]] \
  && python3 "$ROOT/scripts/compare_catalog_vin_parity.py" \
    "$SAMPLES/migration/api-catalog-vin.json" "$SAMPLES/migration/api-catalog-vin.json" --contract-only; then
  record "migration-catalog-vin" pass "VIN envelope contract"
else
  record "migration-catalog-vin" fail "VIN contract failed"
  mig_fail=$((mig_fail + 1))
fi
if [[ -f "$SAMPLES/migration/api-catalog-brand-parts.json" ]] \
  && python3 "$ROOT/scripts/compare_catalog_brand_parts_parity.py" \
    "$SAMPLES/migration/api-catalog-brand-parts.json" "$SAMPLES/migration/api-catalog-brand-parts.json" --contract-only; then
  record "migration-catalog-brand-parts" pass "brand-parts envelope contract"
else
  record "migration-catalog-brand-parts" fail "brand-parts contract failed"
  mig_fail=$((mig_fail + 1))
fi

# Optional API-key dual-sample capture against ASP.NET (never invents keys).
if [[ -n "$ASPNET_BASE" && -n "${ECOMAE_CATALOG_API_KEY:-}" ]]; then
  for route in /api/v1/catalog/status /api/v1/catalog/manufacturers?section=passenger /api/v1/catalog/brands; do
    path_only="${route%%\?*}"
    name="$(echo "$path_only" | tr '/' '_')"
    out="$SAMPLES/aspnet${name}.json"
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' \
      -H "X-API-Key: ${ECOMAE_CATALOG_API_KEY}" \
      "${ASPNET_BASE}${route}" || echo 000)"
    if [[ "$code" == "200" ]]; then
      record "aspnet-catalog$path_only" pass "captured $out"
    elif [[ "$code" == "401" ]]; then
      record "aspnet-catalog$path_only" blocked "HTTP 401 — check ECOMAE_CATALOG_API_KEY"
    else
      record "aspnet-catalog$path_only" fail "HTTP $code"
    fi
  done
else
  record "catalog-api-key-capture" blocked "set ECOMAE_ASPNET_BASE_URL and ECOMAE_CATALOG_API_KEY to capture catalog dual samples"
fi

# Price lookup contract sample + optional live capture
price_sample="$ROOT/docs/migration/evidence/price-lookup/aspnet-output-sample.json"
if [[ -f "$price_sample" ]] \
  && python3 "$ROOT/scripts/compare_price_lookup_parity.py" "$price_sample" "$price_sample" --contract-only; then
  record "migration-price-lookup-contract" pass "offer envelope contract via compare_price_lookup_parity.py"
else
  record "migration-price-lookup-contract" fail "price lookup contract sample failed"
fi
if [[ -n "$ASPNET_BASE" && -n "${ECOMAE_PRICE_LOOKUP_API_KEY:-}" ]]; then
  out="$SAMPLES/aspnet_api_v1_price_lookup.json"
  code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' \
    -H "X-API-Key: ${ECOMAE_PRICE_LOOKUP_API_KEY}" \
    "${ASPNET_BASE}/api/v1/price/lookup?brand=TOYOTA&article=04465-0K020" || echo 000)"
  if [[ "$code" == "200" ]]; then
    record "aspnet-price-lookup" pass "captured $out"
    if python3 "$ROOT/scripts/compare_price_lookup_parity.py" "$out" "$out" --contract-only; then
      record "contract-price-lookup" pass "live offer envelope contract"
    else
      record "contract-price-lookup" fail "live offer envelope contract failed"
    fi
  elif [[ "$code" == "401" ]]; then
    record "aspnet-price-lookup" blocked "HTTP 401 — check ECOMAE_PRICE_LOOKUP_API_KEY"
  else
    record "aspnet-price-lookup" fail "HTTP $code"
  fi
else
  record "price-api-key-capture" blocked "set ECOMAE_ASPNET_BASE_URL and ECOMAE_PRICE_LOOKUP_API_KEY to capture price dual samples"
fi

# Storefront profile envelope (not a list digest)
if python3 - "$SAMPLES/migration/storefront-profile.json" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
for key in ("ok", "surface", "user_id", "email", "source", "message", "session", "note"):
    if key not in doc:
        raise SystemExit(1)
PY
then
  record "migration-contract/storefront-profile.json" pass "profile envelope present"
else
  record "migration-contract/storefront-profile.json" fail "profile envelope missing fields"
fi

# Fixture self-test for compare script
fixture_left="$SAMPLES/_fixture-left.json"
fixture_right="$SAMPLES/_fixture-right.json"
cat >"$fixture_left" <<'JSON'
{"ok":true,"summary":{"users":1,"adminSessions":2,"portalTenants":3,"activePortalTenants":2,"source":"database","message":""},"note":"x"}
JSON
cat >"$fixture_right" <<'JSON'
{"ok":true,"summary":{"users":1,"adminSessions":2,"portalTenants":3,"activePortalTenants":2,"source":"database","message":""},"note":"y"}
JSON
if python3 "$ROOT/scripts/compare_surface_payload_parity.py" --left "$fixture_left" --right "$fixture_right" --path summary \
  --require users,adminSessions,portalTenants,activePortalTenants,source,message; then
  record "compare-script" pass "field-by-field compare ignores note and matches summary"
else
  record "compare-script" fail "compare script self-test failed"
fi

python3 - "$REPORT" "$tmp" "$pass" "$fail" "$block" <<'PY'
import json, sys, datetime
out, src, p, f, b = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
rows=[]
with open(src, encoding='utf-8') as fh:
    for line in fh:
        area, status, detail = line.rstrip('\n').split('\t', 2)
        rows.append({"area": area, "status": status, "detail": detail})
payload={
  "capturedAtUtc": datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00","Z"),
  "passed": p,
  "failed": f,
  "blocked": b,
  "cutoverAllowed": False,
  "phpAuthoritative": True,
  "note": "No nginx cutover and no PHP removal. Dual authenticated samples still required before any exact-route shadow promotion.",
  "results": rows,
}
with open(out, "w", encoding="utf-8") as fh:
    json.dump(payload, fh, indent=2)
    fh.write("\n")
print(out)
PY

rm -f "$tmp"
echo "----------------------------"
echo "Passed: $pass  Blocked: $block  Failed: $fail"
echo "Artifact: $REPORT"
echo "Cutover NOT enabled. PHP remains authoritative."
exit $(( fail > 0 ? 1 : 0 ))
