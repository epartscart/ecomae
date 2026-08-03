#!/usr/bin/env bash
# Surface-field parity offline operator.
# Default: validate checked-in contract board + digest/catalog migration floors (no live curls).
# Optional live harness: ECOMAE_SURFACE_FIELD_LIVE=1 bash scripts/run_surface_parity_harness.sh
# Always asserts cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="${ECOMAE_PARITY_OUT_DIR:-$ROOT/docs/migration/evidence/surface-parity}"
BOARD="${ECOMAE_SURFACE_FIELD_BOARD:-$OUT_DIR/www-surface-field-parity.json}"
COMPARE_OUT="${ECOMAE_SURFACE_FIELD_COMPARE_OUT:-$OUT_DIR/surface-field-offline-result.json}"
LIVE="${ECOMAE_SURFACE_FIELD_LIVE:-0}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

FAIL=0
if [[ "$LIVE" == "1" ]]; then
  echo "surface-field parity operator: live harness"
  if ! bash "$ROOT/scripts/run_surface_parity_harness.sh"; then
    FAIL=1
  fi
else
  echo "surface-field parity operator: offline contract floor (set ECOMAE_SURFACE_FIELD_LIVE=1 for harness)"
fi

echo "surface-field parity operator: digest + catalog contract floors"
if ! bash "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh"; then
  FAIL=1
fi
if ! bash "$ROOT/scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh"; then
  FAIL=1
fi

ECOMAE_SURFACE_FIELD_BOARD="$BOARD" \
ECOMAE_SURFACE_FIELD_COMPARE_OUT="$COMPARE_OUT" \
ECOMAE_SURFACE_FIELD_FAIL="$FAIL" \
python3 - <<'PY'
import json
import os
from pathlib import Path

board_path = Path(os.environ["ECOMAE_SURFACE_FIELD_BOARD"])
out_path = Path(os.environ["ECOMAE_SURFACE_FIELD_COMPARE_OUT"])
prior_fail = os.environ.get("ECOMAE_SURFACE_FIELD_FAIL", "0") == "1"

if not board_path.is_file():
    raise SystemExit(f"FAIL: missing surface-field board: {board_path}")
board = json.loads(board_path.read_text(encoding="utf-8"))
errors = []
if board.get("cutoverAllowed") is not False:
    errors.append("board cutoverAllowed must be explicitly false")
if board.get("readyForPhpRemoval") is not False:
    errors.append("board readyForPhpRemoval must be explicitly false")
contracts = board.get("contracts") or []
if not isinstance(contracts, list) or len(contracts) < 53:
    errors.append(f"board contracts expected >=53, got {len(contracts) if isinstance(contracts, list) else type(contracts)}")
status = str(board.get("status") or "")
if "cutover-blocked" not in status and status != "field-function-presentation-contracts-locked-cutover-blocked":
    # Keep honest statuses only.
    if "cutover" not in status.lower() and "blocked" not in status.lower():
        errors.append(f"unexpected board status {status!r}")

out = {
    "role": "compare-result",
    "ok": not errors and not prior_fail,
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "contractCount": len(contracts) if isinstance(contracts, list) else 0,
    "boardStatus": status,
    "errors": errors,
    "note": (
        "Offline surface-field floor. Live harness remains optional via ECOMAE_SURFACE_FIELD_LIVE=1. "
        "Never invents RELEASE_OWNER_APPROVAL.md."
    ),
}
out_path.parent.mkdir(parents=True, exist_ok=True)
out_path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"ok": out["ok"], "contractCount": out["contractCount"], "cutoverAllowed": False}, indent=2))
if errors:
    print("FAIL: surface-field board locks", flush=True)
    for err in errors:
        print(f"  - {err}", flush=True)
    raise SystemExit(1)
if prior_fail:
    raise SystemExit("FAIL: prior digest/catalog/live steps failed")
print(
    f"PASS: contractCount={out['contractCount']} boardStatus={status} cutoverAllowed=false",
    flush=True,
)
PY

if [[ "$FAIL" -ne 0 ]]; then
  exit 1
fi
