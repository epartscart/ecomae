#!/usr/bin/env bash
# CloudPanel/CI operator: catalog/API contract floor (+ price-lookup contract).
# Offline by default over migration goldens. Always asserts cutoverAllowed=false.
# Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

COMPARE_OUT="${ECOMAE_CATALOG_API_COMPARE_OUT:-$ROOT/docs/migration/evidence/catalog-api/compare-result.json}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

echo "catalog-api dual-sample operator: contract floor -> ${COMPARE_OUT}"
python3 "$ROOT/scripts/compare_catalog_api_contract_floor.py" --out "$COMPARE_OUT"

ECOMAE_CATALOG_API_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_CATALOG_API_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
if not doc.get("ok"):
    raise SystemExit(f"FAIL: catalog-api contract floor failed={doc.get('failed')}")
print(
    f"PASS: catalogGoldensChecked={doc.get('catalogGoldensChecked')} "
    f"priceLookupOk={doc.get('priceLookupOk')} cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
