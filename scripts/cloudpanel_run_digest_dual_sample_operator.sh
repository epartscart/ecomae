#!/usr/bin/env bash
# CloudPanel operator helper: capture (when cookie present) + compare digest dual samples.
# Without ECOMAE_ADMIN_COOKIE_HEADER, runs migration contract-only compare (CI floor).
# Storefront stems need ECOMAE_CUSTOMER_COOKIE_HEADER (session=...; u_id=...).
# Always asserts cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SAMPLES_DIR="${ECOMAE_DIGEST_SAMPLES_DIR:-$ROOT/docs/migration/evidence/surface-parity/samples}"
COMPARE_OUT="${ECOMAE_DIGEST_COMPARE_OUT:-$ROOT/docs/migration/evidence/surface-parity/digest-compare-result.json}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

export ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
COOKIE="${ECOMAE_ADMIN_COOKIE_HEADER:-}"
CUSTOMER="${ECOMAE_CUSTOMER_COOKIE_HEADER:-}"

if [[ -n "$COOKIE" ]]; then
  if [[ -n "$CUSTOMER" ]]; then
    echo "digest dual-sample operator: capture with admin + customer cookies (compare deferred)"
  else
    echo "digest dual-sample operator: capture with admin cookie; storefront stems need ECOMAE_CUSTOMER_COOKIE_HEADER"
  fi
  ECOMAE_DIGEST_DUAL_COMPARE=0 bash "$ROOT/scripts/cloudpanel_capture_digest_dual_samples.sh"
  CONTRACT_ONLY_FLAG=()
else
  echo "digest dual-sample operator: no admin cookie — migration contract-only compare"
  CONTRACT_ONLY_FLAG=(--contract-only)
fi

echo "digest dual-sample operator: compare -> ${COMPARE_OUT}"
python3 "$ROOT/scripts/compare_digest_dual_samples.py" \
  --samples-dir "$SAMPLES_DIR" \
  "${CONTRACT_ONLY_FLAG[@]}" \
  --out "$COMPARE_OUT"

ECOMAE_DIGEST_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_DIGEST_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
print(
    f"PASS: pairsChecked={doc.get('pairsChecked')} failed={doc.get('failed')} "
    f"cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
