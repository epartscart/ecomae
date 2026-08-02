#!/usr/bin/env bash
# Gated PHP decommission helper for CloudPanel.
# Refuses unless /migration/php-decommission-readiness reports readyToRemovePhp=true
# AND ECOMAE_CONFIRM_PHP_DECOMMISSION=YES.
# Never enables broad /api /cp /erp /bos /storefront cutover.
set -euo pipefail

ASPNET_BASE="${ECOMAE_ASPNET_BASE_URL:-http://127.0.0.1:5100}"
CONFIRM="${ECOMAE_CONFIRM_PHP_DECOMMISSION:-}"
READINESS_URL="${ASPNET_BASE}/migration/php-decommission-readiness"

printf '== CloudPanel PHP decommission (gated) ==\n'
printf 'Readiness: %s\n' "$READINESS_URL"

if [[ "$CONFIRM" != "YES" ]]; then
  printf 'REFUSED: set ECOMAE_CONFIRM_PHP_DECOMMISSION=YES after ReadyToRemovePhp is true.\n' >&2
  printf 'This script will not invent approval or smoke evidence.\n' >&2
  exit 2
fi

tmp="$(mktemp)"
code="$(curl -sS -m 20 -o "$tmp" -w '%{http_code}' "$READINESS_URL" || true)"
if [[ "$code" != "200" ]]; then
  printf 'REFUSED: readiness endpoint HTTP %s\n' "$code" >&2
  rm -f "$tmp"
  exit 3
fi

ready="$(python3 - "$tmp" <<'PY'
import json,sys
d=json.load(open(sys.argv[1], encoding='utf-8'))
print('true' if d.get('readyToRemovePhp') is True else 'false')
print('complete', d.get('checklistCompleteCount'), '/', d.get('checklistTotalCount'), file=sys.stderr)
for item in d.get('checklist', []):
    if item.get('status') != 'present':
        print('missing', item.get('id'), file=sys.stderr)
PY
)"
rm -f "$tmp"

if [[ "$ready" != "true" ]]; then
  printf 'REFUSED: readyToRemovePhp is not true. Attach staging-smoke + RELEASE_OWNER_APPROVAL.md first.\n' >&2
  exit 4
fi

printf 'Gate is green. Exact-route PHP fallback removal steps (operator executes carefully):\n'
printf '  1) Ensure only approved location = nginx shadows are enabled (never broad trees).\n'
printf '  2) Disable PHP exact-route fallbacks for those shadowed paths only.\n'
printf '  3) Keep PHP-FPM available until observation window ends.\n'
printf '  4) Rollback: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback\n'
printf '\nThis script does NOT stop php-fpm or delete PHP source automatically.\n'
printf 'Manual final package removal remains a human CloudPanel change after observation.\n'
exit 0
