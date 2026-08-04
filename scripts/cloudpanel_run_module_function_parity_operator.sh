#!/usr/bin/env bash
# CloudPanel/CI operator: refresh module-function inventory stubs + compare.
# Always asserts cutoverAllowed=false and aspnetCompleteCount=0 without human pass file.
# Never invents MODULE_FUNCTION_TEST_PASS.md / RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SAMPLES_DIR="${ECOMAE_MODULE_FUNCTION_SAMPLES_DIR:-$ROOT/docs/migration/evidence/module-function-parity}"
COMPARE_OUT="${ECOMAE_MODULE_FUNCTION_COMPARE_OUT:-$SAMPLES_DIR/compare-result.json}"

export ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES="${ECOMAE_OVERWRITE_MODULE_FUNCTION_SAMPLES:-1}"

echo "module-function parity operator: capture/refresh inventory stubs"
bash "$ROOT/scripts/cloudpanel_capture_module_function_parity.sh"

echo "module-function parity operator: compare -> ${COMPARE_OUT}"
python3 "$ROOT/scripts/compare_module_function_parity.py" \
  --samples-dir "$SAMPLES_DIR" \
  --out "$COMPARE_OUT"

ECOMAE_MODULE_FUNCTION_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_MODULE_FUNCTION_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
if int(doc.get("aspnetCompleteCount") or 0) != 0:
    raise SystemExit("FAIL: aspnetCompleteCount must remain 0 without human pass evidence")
if not doc.get("ok"):
    raise SystemExit("FAIL: module-function compare not ok")
print(
    f"PASS: moduleCount={doc.get('moduleCount')} aspnetCompleteCount={doc.get('aspnetCompleteCount')} "
    f"cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
