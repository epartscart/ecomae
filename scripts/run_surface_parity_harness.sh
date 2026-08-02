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
  for route in /cp/dashboard-summary /erp/dashboard-summary /bos/fleet-summary; do
    name="$(echo "$route" | tr '/?' '__')"
    out="$SAMPLES/aspnet${name}.json"
    code="$(curl -sS -m 30 -o "$out" -w '%{http_code}' "${auth_args[@]}" "${ASPNET_BASE}${route}" || echo 000)"
    if [[ "$code" == "200" ]]; then
      record "aspnet$route" pass "captured $out"
      req=""
      case "$route" in
        /cp/dashboard-summary) req="users,adminSessions,portalTenants,activePortalTenants,source,message" ;;
        /erp/dashboard-summary) req="cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message" ;;
        /bos/fleet-summary) req="portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message" ;;
      esac
      if python3 "$ROOT/scripts/compare_surface_payload_parity.py" --left "$out" --right "$out" --path summary --contract-only --require "$req"; then
        record "contract$route" pass "summary field contract satisfied"
      else
        record "contract$route" fail "summary field contract failed"
      fi
    elif [[ "$code" == "401" ]]; then
      record "aspnet$route" blocked "HTTP 401 with provided cookie"
    else
      record "aspnet$route" fail "HTTP $code"
    fi
  done
else
  record "authenticated-digest-capture" blocked "set ECOMAE_ASPNET_BASE_URL and admin cookie to capture dual samples"
fi

# Migration-mode contract samples (no secrets) must satisfy locked field contracts.
python3 "$ROOT/scripts/generate_migration_digest_contract_samples.py" >/dev/null
declare -A CONTRACT_REQUIREMENTS=(
  [cp-dashboard-summary.json]="users,adminSessions,portalTenants,activePortalTenants,source,message"
  [erp-dashboard-summary.json]="cashPosition,supplierCredit,supplierDebit,supplierNet,cashAccounts,activeSuppliers,activePurchases,source,message"
  [bos-fleet-summary.json]="portalTenants,activePortalTenants,adminSessions,withDatabase,erpOnly,source,message"
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
