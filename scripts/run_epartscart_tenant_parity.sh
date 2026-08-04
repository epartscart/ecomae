#!/usr/bin/env bash
# epartscart.com frontend + CP same-to-same PHP parity gate.
# Never removes PHP. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="$ROOT/docs/migration/evidence/tenant-safety"
mkdir -p "$OUT_DIR"

echo "== epartscart.com frontend + CP parity gate =="
echo "Tenant must stay PHP-identical (theme/colour/structure/fonts/fields). No ASP.NET hybrid on tenant hosts."

LIVE_ARGS=()
if [[ "${ECOMAE_EPARTSCART_LIVE:-}" == "1" ]]; then
  LIVE_ARGS=(--live)
  echo "(ECOMAE_EPARTSCART_LIVE=1 — probing https://epartscart.com + www)"
fi

set +e
python3 "$ROOT/scripts/validate_epartscart_tenant_parity.py" \
  --root "$ROOT" \
  --out "$OUT_DIR/epartscart-frontend-cp-parity.json" \
  --matrix-out "$OUT_DIR/epartscart-coverage-matrix.json" \
  "${LIVE_ARGS[@]}"
rc=$?
set -e

python3 - <<'PY'
import json
from pathlib import Path
for rel in (
    "docs/migration/evidence/tenant-safety/epartscart-frontend-cp-parity.json",
    "docs/migration/evidence/tenant-safety/epartscart-coverage-matrix.json",
):
    doc = json.loads(Path(rel).read_text(encoding="utf-8"))
    assert doc.get("cutoverAllowed") is False
    assert doc.get("readyForPhpRemoval") is False
    assert doc.get("aspNetInteractiveComplete") == 0
    print(f"OK locks: {rel}")
print("PHP removal allowed: False")
PY

echo "Artifacts:"
echo "  $OUT_DIR/epartscart-frontend-cp-parity.json"
echo "  $OUT_DIR/epartscart-coverage-matrix.json"
echo "PHP was NOT decommissioned on epartscart.com."
exit "$rc"
