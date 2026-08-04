#!/usr/bin/env bash
# Offline/CI: digest + hybrid UI + on-premises contract floors (writes=0).
# Never invents cutover. Never deletes PHP. Live cookie capture is separate.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

printf '== Digest / hybrid / on-premises contract floors ==\n'

printf '\n-- digest (contract-only) --\n'
bash "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh"

printf '\n-- hybrid-ui (stubs / contract-only) --\n'
ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES="${ECOMAE_OVERWRITE_HYBRID_UI_SAMPLES:-0}" \
  bash "$ROOT/scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh"

printf '\n-- on-premises (contract-only) --\n'
bash "$ROOT/scripts/cloudpanel_run_on_premises_dual_sample_operator.sh"

printf '\n-- allowlist sync --\n'
python3 "$ROOT/scripts/validate_presentation_hybrid_allowlist_sync.py"
python3 "$ROOT/scripts/validate_surface_digest_allowlist_sync.py"

printf '\nPASS: digest + hybrid + on-premises contract floors green; cutoverAllowed stays false; PHP remains authoritative.\n'
printf 'Next (CloudPanel): ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES bash scripts/cloudpanel_www_shadow_closeout_operator.sh\n'
printf 'Then live dual-sample with cookies — never invent RELEASE_OWNER_APPROVAL.md.\n'
printf 'PHP decommission remains refused until /migration/php-decommission-readiness → readyToRemovePhp=true.\n'
