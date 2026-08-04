#!/usr/bin/env bash
# *.ecomae.com industry frontend parity gate (PHP live chrome + ASP.NET www preview catalog).
# Never removes PHP. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="$ROOT/docs/migration/evidence/tenant-safety"
mkdir -p "$OUT_DIR"

echo "== industry *.ecomae.com frontend parity gate =="
echo "Live industry hosts stay PHP-identical. ASP.NET compare = www /marketing/industries only."

LIVE_ARGS=()
if [[ "${ECOMAE_INDUSTRY_LIVE:-}" == "1" ]]; then
  LIVE_ARGS=(--live)
  echo "(ECOMAE_INDUSTRY_LIVE=1 — probing all catalogued industry hosts)"
fi

set +e
python3 "$ROOT/scripts/validate_industry_ecomae_frontend_parity.py" \
  --root "$ROOT" \
  --out "$OUT_DIR/industry-ecomae-frontend-parity.json" \
  --matrix-out "$OUT_DIR/industry-ecomae-coverage-matrix.json" \
  "${LIVE_ARGS[@]}"
rc=$?
set -e

python3 - <<'PY'
import json
from pathlib import Path
for rel in (
    "docs/migration/evidence/tenant-safety/industry-ecomae-frontend-parity.json",
    "docs/migration/evidence/tenant-safety/industry-ecomae-coverage-matrix.json",
):
    doc = json.loads(Path(rel).read_text(encoding="utf-8"))
    assert doc.get("cutoverAllowed") is False
    assert doc.get("readyForPhpRemoval") is False
    assert doc.get("aspNetInteractiveComplete") == 0
    print(f"OK locks: {rel}")
print("PHP removal allowed: False")
PY

echo "Artifacts:"
echo "  $OUT_DIR/industry-ecomae-frontend-parity.json"
echo "  $OUT_DIR/industry-ecomae-coverage-matrix.json"
echo "PHP was NOT decommissioned on industry *.ecomae.com hosts."
exit "$rc"
