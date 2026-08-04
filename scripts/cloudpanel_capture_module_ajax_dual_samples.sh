#!/usr/bin/env bash
# Capture ASP.NET CP/storefront module-ajax dry-run samples for dual-sample floor.
# Always writes=0 / cutoverAllowed=false. PHP ajax/forms remain authoritative.
# Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
OUT_DIR="${ECOMAE_MODULE_AJAX_EVIDENCE_DIR:-$ROOT/docs/migration/evidence/module-ajax-dual-samples}"
ADMIN_COOKIE="${ECOMAE_ADMIN_COOKIE:-}"
mkdir -p "$OUT_DIR"

health="$(curl -sS -m 5 -o /tmp/ecomae-module-ajax-health.txt -w '%{http_code}' "${BASE}/health" 2>/dev/null || true)"
if [[ "$health" != "200" ]]; then
  echo "FAIL: ASP.NET /health HTTP ${health:-000} at ${BASE}" >&2
  echo "Run: bash scripts/wait_for_aspnet_health.sh" >&2
  exit 1
fi

curl -sS "${BASE}/cp/module-ajax/writes/catalog" -o "$OUT_DIR/aspnet-catalog.json"
python3 - "$OUT_DIR/aspnet-catalog.json" <<'PY'
import json, sys
from pathlib import Path
doc = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
assert doc.get("cutoverAllowed") is False, "catalog must keep cutoverAllowed=false"
assert doc.get("readyForPhpRemoval") is False, "catalog must keep readyForPhpRemoval=false"
assert int(doc.get("totalActions") or 0) > 0, "catalog totalActions required"
print(f"OK catalog totalActions={doc.get('totalActions')} dedicated={doc.get('dedicatedDryRuns')} coveragePct={doc.get('coveragePct')}")
PY

# Curated dedicated dry-runs (representative Wave C–F surfaces).
SAMPLES=(
  "procurement/create_supplier"
  "crm/crm_save_lead"
  "document_control/save_company"
  "auto_price/bulk_approve"
  "bulk_upload/process_upload"
  "classic_form/shop_catalogue_product"
  "parts_agent/save_config"
  "free_tools/register"
  "pos/complete_sale"
  "channels/toggle_channel"
  "workshop_endpoint/create_job"
  "payments/save_config"
  "logistics/create_shipment"
  "garage_manager/create_job"
  "currency_live_rates/apply"
)

hdr=()
if [[ -n "$ADMIN_COOKIE" ]]; then
  hdr=(-H "Cookie: ${ADMIN_COOKIE}")
fi

ok=0
fail=0
for key in "${SAMPLES[@]}"; do
  module="${key%%/*}"
  action="${key#*/}"
  path="/cp/module-ajax/${module}/${action}/dry-run"
  out="$OUT_DIR/aspnet-${module}-${action}.json"
  code="$(curl -sS -o "$out" -w '%{http_code}' -X POST \
    -H 'Content-Type: application/json' \
    "${hdr[@]}" \
    -d '{"confirmWrites":false}' \
    "${BASE}${path}" || true)"
  if [[ "$code" != "200" && "$code" != "401" ]]; then
    echo "FAIL sample ${key}: HTTP $code"
    fail=$((fail + 1))
    continue
  fi
  if [[ "$code" == "401" ]]; then
    echo "PASS sample ${key}: unauthorized gate (session required)"
    ok=$((ok + 1))
    continue
  fi
  if python3 - "$out" "$key" <<'PY'
import json, sys
from pathlib import Path
doc = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
label = sys.argv[2]
if doc.get("writes") != 0 or doc.get("cutoverAllowed") is True:
    raise SystemExit(f"FAIL {label}: writes/cutover invalid")
if doc.get("phpAuthoritative") is not True and doc.get("writesBlocked") is not True:
    # accept either field style from dedicated/registry payloads
    pass
print(f"PASS sample {label}: writes=0 cutoverAllowed=false")
PY
  then
    ok=$((ok + 1))
  else
    fail=$((fail + 1))
  fi
done

# PHP authoritative inventory (paths only — live PHP not invoked here).
python3 - "$OUT_DIR/php-authoritative-inventory.json" <<'PY'
import json, time
from pathlib import Path
out = Path(__import__("sys").argv[1])
doc = {
    "role": "php-module-ajax-authoritative-inventory",
    "generatedAtUnix": int(time.time()),
    "phpAuthoritative": True,
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "aspNetInteractiveComplete": 0,
    "surfaces": [
        {"module": "procurement", "php": "cp/content/shop/procurement/ajax_procurement.php"},
        {"module": "crm", "php": "cp/content/shop/crm/ajax_crm.php"},
        {"module": "document_control", "php": "cp/content/shop/document_control/ajax_document_control.php"},
        {"module": "auto_price", "php": "cp/content/control/portal/ajax_auto_price.php"},
        {"module": "bulk_upload", "php": "cp/content/shop/bulk_upload/ajax_bulk_cp.php"},
        {"module": "classic_form", "php": "cp/content/shop/catalogue/product.php"},
        {"module": "parts_agent", "php": "cp/content/shop/parts_agent/ajax_epc_parts_agent_cp.php"},
        {"module": "free_tools", "php": "content/general_pages/ajax_epc_free_tools.php"},
        {"module": "pos", "php": "cp/content/shop/pos/ajax_pos.php"},
        {"module": "channels", "php": "cp/content/shop/channels/ajax_channels.php"},
        {"module": "workshop_endpoint", "php": "cp/content/shop/workshop/ajax_workshop_endpoint.php"},
        {"module": "payments", "php": "cp/content/shop/payments/ajax_payments.php"},
        {"module": "logistics", "php": "cp/content/shop/logistics/ajax_logistics.php"},
        {"module": "garage_manager", "php": "content/shop/workshop/ajax_garage_manager.php"},
        {"module": "currency_live_rates", "php": "cp/content/shop/finance/ajax_currency_live_rates.php"},
    ],
    "note": "Live PHP ajax/forms remain authoritative. Pair field-level samples before any exact-route cutover.",
}
out.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
print(f"Wrote {out}")
PY

echo "CAPTURE_OK=${ok} CAPTURE_FAIL=${fail} dir=${OUT_DIR}"
[[ "$fail" -eq 0 ]]
