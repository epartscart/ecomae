#!/usr/bin/env bash
# Pre-PHP-removal functional suite for named product flows.
# Never removes PHP. Never invents RELEASE_OWNER_APPROVAL.md.
# cutoverAllowed stays false.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="$ROOT/docs/migration/evidence/decommission/functional-flows"
mkdir -p "$OUT_DIR"

echo "== Pre-decommission functional suite (7 named flows) =="
echo "Flows: warehouse offers · ERP external reports · e-invoice · CT/UMAPI · process flow · OMS/checkout · Super CP tenant control"

SKIP_PHP=()
if [[ "${ECOMAE_FUNC_SKIP_PHP:-}" == "1" ]]; then
  SKIP_PHP=(--skip-php-tests)
  echo "(ECOMAE_FUNC_SKIP_PHP=1 — skipping PHP CLI tests)"
fi

set +e
python3 "$ROOT/scripts/validate_pre_decommission_functional_suite.py" \
  --root "$ROOT" \
  --matrix "$OUT_DIR/required-flows.json" \
  --out "$OUT_DIR/www-functional-flow-suite.json" \
  "${SKIP_PHP[@]}"
rc=$?
set -e

python3 - <<'PY'
import json
from pathlib import Path
p = Path("docs/migration/evidence/decommission/functional-flows/www-functional-flow-suite.json")
doc = json.loads(p.read_text(encoding="utf-8"))
assert doc.get("cutoverAllowed") is False
assert doc.get("readyForPhpRemoval") is False
assert doc.get("readyToRemovePhp") is False
assert doc.get("aspNetInteractiveComplete") == 0
print(f"Suite artifact OK: passed={doc.get('passed')} blocked={doc.get('blocked')} failed={doc.get('failed')}")
print(f"PHP removal allowed: False — {doc.get('phpRemovalBlockedReason')}")
PY

echo "Artifact: $OUT_DIR/www-functional-flow-suite.json"
echo "PHP was NOT decommissioned."
exit "$rc"
