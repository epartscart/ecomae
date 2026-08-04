#!/usr/bin/env bash
# Presentation recheck operator (honest fail until chrome+module parity).
# Default: validate checked-in evidence (offline CI floor).
# Live probe: ECOMAE_PRESENTATION_LIVE=1 (uses ECOMAE_PRESENTATION_SOFT=1; never claims PHP removal).
# Never invents RELEASE_OWNER_APPROVAL.md / MODULE_FUNCTION_TEST_PASS.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

EVIDENCE="${ECOMAE_PRESENTATION_RECHECK_JSON:-$ROOT/docs/migration/evidence/presentation/php-vs-aspnet-recheck.json}"
LIVE="${ECOMAE_PRESENTATION_LIVE:-0}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

if [[ "$LIVE" == "1" ]]; then
  echo "presentation recheck operator: live soft probe"
  export ECOMAE_PRESENTATION_SOFT="${ECOMAE_PRESENTATION_SOFT:-1}"
  bash "$ROOT/scripts/cloudpanel_probe_php_presentation_parity.sh"
else
  echo "presentation recheck operator: validate cached evidence (set ECOMAE_PRESENTATION_LIVE=1 for probe)"
fi

ECOMAE_PRESENTATION_RECHECK_JSON="$EVIDENCE" python3 - <<'PY'
import json
import os
from pathlib import Path

path = Path(os.environ["ECOMAE_PRESENTATION_RECHECK_JSON"])
if not path.is_file():
    raise SystemExit(f"FAIL: missing presentation recheck evidence: {path}")
doc = json.loads(path.read_text(encoding="utf-8"))
if doc.get("readyForPhpRemoval") is True or doc.get("cutoverAllowed") is True:
    raise SystemExit("FAIL: presentation recheck must keep readyForPhpRemoval/cutoverAllowed false")
status = str(doc.get("status") or "")
# Honest statuses only — never invent pass without real chrome+module parity.
allowed = {"fail", "soft-fail", "chrome-pass-functionality-pending"}
if status not in allowed:
    raise SystemExit(f"FAIL: unexpected presentation status {status!r} (allowed={sorted(allowed)})")
print(
    f"PASS: status={status} failureCount={doc.get('failureCount')} "
    f"readyForPhpRemoval={doc.get('readyForPhpRemoval')} "
    f"(PHP remains authoritative; Batch 6 blocked)"
)
PY
