#!/usr/bin/env bash
# CloudPanel/CI operator: compare price-lookup dual samples.
# Default uses checked-in evidence samples. Always asserts cutoverAllowed=false.
# Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

SAMPLES_DIR="${ECOMAE_PRICE_LOOKUP_SAMPLES_DIR:-$ROOT/docs/migration/evidence/price-lookup}"
PHP_SAMPLE="${ECOMAE_PRICE_LOOKUP_PHP_SAMPLE:-$SAMPLES_DIR/php-baseline-sample.json}"
ASPNET_SAMPLE="${ECOMAE_PRICE_LOOKUP_ASPNET_SAMPLE:-$SAMPLES_DIR/aspnet-output-sample.json}"
COMPARE_OUT="${ECOMAE_PRICE_LOOKUP_COMPARE_OUT:-$SAMPLES_DIR/compare-result.json}"
CONTRACT_ONLY="${ECOMAE_PRICE_LOOKUP_CONTRACT_ONLY:-0}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

FLAGS=()
if [[ "$CONTRACT_ONLY" == "1" ]]; then
  FLAGS+=(--contract-only)
fi

echo "price-lookup dual-sample operator: compare -> ${COMPARE_OUT}"
python3 "$ROOT/scripts/compare_price_lookup_parity.py" \
  "$PHP_SAMPLE" \
  "$ASPNET_SAMPLE" \
  "${FLAGS[@]}" \
  --out "$COMPARE_OUT"

ECOMAE_PRICE_LOOKUP_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_PRICE_LOOKUP_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
if not doc.get("ok"):
    raise SystemExit("FAIL: price-lookup compare not ok")
print(
    f"PASS: offerCount={doc.get('offerCount')} ok={doc.get('ok')} "
    f"cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
