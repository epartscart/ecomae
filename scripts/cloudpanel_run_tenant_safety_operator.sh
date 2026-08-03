#!/usr/bin/env bash
# Tenant same-to-same safety operator.
# Default: validate checked-in evidence (offline CI floor).
# Live probes: ECOMAE_TENANT_LIVE=1 (runs chrome probe + same-to-same verify).
# Always asserts cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="${ECOMAE_PROBE_OUT_DIR:-$ROOT/docs/migration/evidence/tenant-safety}"
CHROME_JSON="${ECOMAE_TENANT_CHROME_JSON:-$OUT_DIR/live-tenant-php-chrome.json}"
VERIFY_JSON="${ECOMAE_SAME_TO_SAME_VERIFY_JSON:-$OUT_DIR/same-to-same-verify.json}"
LIVE="${ECOMAE_TENANT_LIVE:-0}"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

if [[ "$LIVE" == "1" ]]; then
  echo "tenant-safety operator: live probes"
  export ECOMAE_PROBE_OUT_DIR="$OUT_DIR"
  bash "$ROOT/scripts/cloudpanel_verify_tenant_hosts_still_php.sh"
else
  echo "tenant-safety operator: validate cached evidence (set ECOMAE_TENANT_LIVE=1 for probes)"
fi

ECOMAE_TENANT_CHROME_JSON="$CHROME_JSON" \
ECOMAE_SAME_TO_SAME_VERIFY_JSON="$VERIFY_JSON" \
python3 - <<'PY'
import json
import os
from pathlib import Path

chrome_path = Path(os.environ["ECOMAE_TENANT_CHROME_JSON"])
verify_path = Path(os.environ["ECOMAE_SAME_TO_SAME_VERIFY_JSON"])
for path in (chrome_path, verify_path):
    if not path.is_file():
        raise SystemExit(f"FAIL: missing tenant-safety evidence: {path}")

chrome = json.loads(chrome_path.read_text(encoding="utf-8"))
verify = json.loads(verify_path.read_text(encoding="utf-8"))

for label, doc in (("live-tenant-php-chrome", chrome), ("same-to-same-verify", verify)):
    if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
        raise SystemExit(f"FAIL: {label} must keep cutoverAllowed/readyForPhpRemoval false")
    if "cutoverAllowed" not in doc or "readyForPhpRemoval" not in doc:
        raise SystemExit(f"FAIL: {label} must explicitly set cutoverAllowed=false and readyForPhpRemoval=false")
    if doc.get("status") != "pass":
        raise SystemExit(f"FAIL: {label} status={doc.get('status')!r} (expected pass for cached floor)")

if int(chrome.get("failCount") or 0) != 0:
    raise SystemExit("FAIL: live-tenant-php-chrome failCount must be 0")
if int(chrome.get("passCount") or 0) <= 0:
    raise SystemExit("FAIL: live-tenant-php-chrome passCount must be > 0")

print(
    f"PASS: chromeStatus={chrome.get('status')} verifyStatus={verify.get('status')} "
    f"passCount={chrome.get('passCount')} cutoverAllowed=false "
    f"(same-to-same tenant chrome stays PHP; Batch 6 blocked)"
)
PY
