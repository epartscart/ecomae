#!/usr/bin/env bash
# CloudPanel operator helper: capture + compare login-cookie dual samples (Batch 3).
# Always asserts cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SAMPLES_DIR="${ECOMAE_LOGIN_COOKIE_SAMPLES_DIR:-$ROOT/docs/migration/evidence/login-session-bridge}"
COMPARE_OUT="${ECOMAE_LOGIN_COOKIE_COMPARE_OUT:-$SAMPLES_DIR/compare-result.json}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

export ECOMAE_OVERWRITE_LOGIN_SAMPLES="${ECOMAE_OVERWRITE_LOGIN_SAMPLES:-0}"
export ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"

echo "login-cookie dual-sample operator: capture (overwrite=${ECOMAE_OVERWRITE_LOGIN_SAMPLES})"
bash "$ROOT/scripts/cloudpanel_capture_login_cookie_dual_samples.sh"

echo "login-cookie dual-sample operator: seed php contract baselines"
python3 "$ROOT/scripts/generate_login_cookie_contract_samples.py" --dir "$SAMPLES_DIR"

echo "login-cookie dual-sample operator: compare -> ${COMPARE_OUT}"
python3 "$ROOT/scripts/compare_login_cookie_dual_samples.py" \
  --samples-dir "$SAMPLES_DIR" \
  --contract-only \
  --out "$COMPARE_OUT"

ECOMAE_LOGIN_COOKIE_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_LOGIN_COOKIE_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
if not doc.get("ok"):
    raise SystemExit(f"FAIL: login-cookie compare not ok samples={doc.get('samples')}")
if int(doc.get("contractPairsOk") or 0) < 4:
    raise SystemExit(f"FAIL: contractPairsOk={doc.get('contractPairsOk')} < 4")
print(
    f"PASS: sampleCount={doc.get('sampleCount')} contractPairsOk={doc.get('contractPairsOk')} "
    f"ok={doc.get('ok')} cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
