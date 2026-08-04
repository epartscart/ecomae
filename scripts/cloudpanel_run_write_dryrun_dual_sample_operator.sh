#!/usr/bin/env bash
# CloudPanel operator: Wave B write dry-run dual-sample floor.
# Runs probe_write_dryruns.sh (asserts writes=0 / cutoverAllowed=false on ASP.NET),
# then records a contract result that never invents RELEASE_OWNER_APPROVAL.md.
# PHP ajax remains authoritative until human dual-sample promotion.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="${ECOMAE_WRITE_DRYRUN_EVIDENCE_DIR:-$ROOT/docs/migration/evidence/write-dryruns}"
OUT_JSON="${ECOMAE_WRITE_DRYRUN_COMPARE_OUT:-$OUT_DIR/write-dryrun-operator-result.json}"
mkdir -p "$OUT_DIR"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

export ECOMAE_ASPNET_BASE_URL="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"

echo "write dry-run dual-sample operator: probe ASP.NET dry-runs against ${ECOMAE_ASPNET_BASE_URL}"
PROBE_OK=0
if bash "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh"; then
  PROBE_OK=1
fi

python3 - "$OUT_JSON" "$PROBE_OK" <<'PY'
import json, sys, time
from pathlib import Path

out, probe_ok = Path(sys.argv[1]), int(sys.argv[2])
doc = {
    "role": "write-dryrun-dual-sample-operator",
    "generatedAtUnix": int(time.time()),
    "probePassed": bool(probe_ok),
    "cutoverAllowed": False,
    "readyForPhpRemoval": False,
    "aspNetInteractiveComplete": 0,
    "phpAuthoritative": True,
    "note": (
        "ASP.NET write dry-runs must keep writes=0. Live PHP ajax remains authoritative. "
        "Do not invent RELEASE_OWNER_APPROVAL.md. Promote writes only after field-level dual-sample."
    ),
    "next": [
        "Capture paired PHP ajax vs ASP.NET dry-run JSON samples per surface",
        "Compare intended/simulated fields; keep cutoverAllowed=false until human sign-off",
    ],
}
out.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
if not probe_ok:
    raise SystemExit(f"FAIL: write dry-run probe failed; result written to {out}")
if doc["cutoverAllowed"] or doc["readyForPhpRemoval"]:
    raise SystemExit("FAIL: operator must keep cutoverAllowed/readyForPhpRemoval false")
print(f"PASS: write dry-run operator probePassed=true cutoverAllowed=false -> {out}")
PY
