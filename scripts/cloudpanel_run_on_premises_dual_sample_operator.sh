#!/usr/bin/env bash
# Offline/CI: seed + compare on-premises dry-run contract floor (writes=0).
# Without live cookies this is contract-only. Never invents cutover.
# Never deletes PHP. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

EVIDENCE="${ECOMAE_ONPREM_SAMPLES_DIR:-$ROOT/docs/migration/evidence/on-premises-dual-samples}"
COMPARE_OUT="${ECOMAE_ONPREM_COMPARE_OUT:-$EVIDENCE/compare-result.json}"

printf '== On-premises dual-sample contract floor ==\n'
python3 "$ROOT/scripts/generate_on_premises_contract_samples.py" --dir "$EVIDENCE"
python3 "$ROOT/scripts/compare_on_premises_dual_samples.py" \
  --dir "$EVIDENCE" \
  --contract-only \
  --out "$COMPARE_OUT"

ECOMAE_ONPREM_COMPARE_OUT="$COMPARE_OUT" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_ONPREM_COMPARE_OUT"])
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
    raise SystemExit("FAIL: compare-result must keep cutoverAllowed/readyForPhpRemoval false")
if not doc.get("ok"):
    raise SystemExit(f"FAIL: on-premises compare not ok errors={doc.get('errors')}")
print(
    f"PASS: samplesChecked={doc.get('samplesChecked')} "
    f"contractPairsOk={doc.get('contractPairsOk')} "
    f"cutoverAllowed={doc.get('cutoverAllowed')}"
)
PY
