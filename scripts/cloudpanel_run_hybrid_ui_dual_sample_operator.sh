#!/usr/bin/env bash
# CloudPanel operator helper: capture + compare hybrid UI dual samples.
# Always asserts cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SAMPLES_DIR="${ECOMAE_HYBRID_UI_SAMPLES_DIR:-$ROOT/docs/migration/evidence/hybrid-ui-dual-samples}"
COMPARE_OUT="${ECOMAE_HYBRID_UI_COMPARE_OUT:-$SAMPLES_DIR/compare-result.json}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

export ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES="${ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES:-0}"
export ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"

echo "hybrid-ui dual-sample operator: capture (overwrite=${ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES})"
bash "$ROOT/scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"

echo "hybrid-ui dual-sample operator: compare -> ${COMPARE_OUT}"
python3 "$ROOT/scripts/compare_hybrid_ui_dual_samples.py" \
  --samples-dir "$SAMPLES_DIR" \
  --out "$COMPARE_OUT"

ECOMAE_HYBRID_UI_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_HYBRID_UI_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
print(
    f"PASS: pairsChecked={doc.get('pairsChecked')} "
    f"hybridUiSamplesOk={doc.get('hybridUiSamplesOk')} "
    f"cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
