#!/usr/bin/env bash
# Force-live publish ePartsCart so PHP same-style parts UI reaches public :5100.
# Paste-back required: RESULT=PASS from prove script.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
note() { printf '%s\n' "$*"; }

note "======== EPARTSCART PARTS PHP SAME-STYLE FORCE LIVE ========"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "Expect main tip includes epc-brand-picker-table + epc-part-type-split + ?v=20260811x"

if [[ -x "${ROOT}/scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh" ]]; then
  bash "${ROOT}/scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh" || true
elif [[ -x "${ROOT}/scripts/cloudpanel_FORCE_LIVE_NOW.sh" ]]; then
  bash "${ROOT}/scripts/cloudpanel_FORCE_LIVE_NOW.sh" || true
else
  note "GATE_WARN no LIVE_PUBLISH helper — run CloudPanel publish for epartscart :5100 manually"
fi

bash "${ROOT}/scripts/cloudpanel_EPARTSCART_PARTS_PHP_SAME_STYLE_PROVE.sh"
