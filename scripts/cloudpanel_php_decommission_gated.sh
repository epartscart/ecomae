#!/usr/bin/env bash
# Gated PHP decommission — ONE step at a time, only when readiness is true.
# Never invents RELEASE_OWNER_APPROVAL.md. Never deletes PHP source while blocked.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

BASE="${ECOMAE_PUBLIC_BASE_URL:-https://www.ecomae.com}"
STEP="${1:-status}"

printf '== PHP decommission gate ==\n'
printf 'Base: %s\n' "$BASE"
printf 'Step: %s\n' "$STEP"

readiness="$(curl -fsS -A 'EcomAE-php-decommission-gate' \
  "${BASE}/migration/php-decommission-readiness" || true)"
if [[ -z "$readiness" ]]; then
  printf 'ERROR: could not fetch /migration/php-decommission-readiness\n' >&2
  exit 2
fi

python3 - "$readiness" "$STEP" "$ROOT" <<'PY'
import json, os, sys
from pathlib import Path

raw, step, root = sys.argv[1], sys.argv[2], Path(sys.argv[3])
doc = json.loads(raw)
ready = bool(doc.get("readyToRemovePhp"))
status = doc.get("status")
blockers = doc.get("blockers") or []
print(f"readyToRemovePhp={ready} status={status} blockerCount={len(blockers)}")
for b in blockers[:12]:
    print(f"  - {b if isinstance(b, str) else b}")

approval = root / "RELEASE_OWNER_APPROVAL.md"
if approval.is_file() and "APPROVED_TO_REMOVE_PHP_FALLBACK" in approval.read_text(encoding="utf-8", errors="replace"):
    print("approvalFile=present")
else:
    print("approvalFile=missing (do not invent)")

if step in ("status", "check"):
    sys.exit(0 if ready else 3)

if step == "remove-php-runtime":
    if not ready:
        print("REFUSE: ReadyToRemovePhp is false — keeping PHP-FPM/cron/rewrites/source.")
        sys.exit(4)
    confirm = os.environ.get("ECOMAE_CONFIRM_PHP_DECOMMISSION", "")
    if confirm != "YES":
        print("REFUSE: set ECOMAE_CONFIRM_PHP_DECOMMISSION=YES after human approval.")
        sys.exit(5)
    print("OK to invoke scripts/cloudpanel_php_decommission.sh (exact-route fallback only).")
    sys.exit(0)

if step == "delete-php-source":
    print("REFUSE: PHP source deletion is not automated here.")
    print("Even after runtime decommission, source removal requires a separate human-owned PR")
    print("after ReadyToRemovePhp=true AND approval — never invent that gate.")
    sys.exit(6)

print(f"Unknown step: {step}", file=sys.stderr)
sys.exit(2)
PY
