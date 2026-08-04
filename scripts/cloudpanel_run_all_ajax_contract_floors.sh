#!/usr/bin/env bash
# Offline/CI: seed + compare CP/ERP/BOS ajax contract floors (writes=0).
# Never invents cutover. Never deletes PHP. Live capture is separate.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

printf '== Ajax surface contract floors (CP + ERP + BOS) ==\n'
python3 "$ROOT/scripts/generate_ajax_surface_contract_samples.py" --surface all
printf '\n-- module-ajax (CP/storefront) --\n'
python3 "$ROOT/scripts/compare_module_ajax_dual_samples.py" --contract-only
printf '\n-- erp-ajax --\n'
python3 "$ROOT/scripts/compare_erp_ajax_dual_samples.py" --contract-only
printf '\n-- bos-ajax --\n'
python3 "$ROOT/scripts/compare_bos_ajax_dual_samples.py" --contract-only
printf '\nPASS: all ajax contract floors green; cutoverAllowed stays false; PHP remains authoritative.\n'
printf 'Next (CloudPanel): ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES bash scripts/cloudpanel_www_shadow_closeout_operator.sh\n'
printf 'Then live dual-sample with cookies — never invent RELEASE_OWNER_APPROVAL.md.\n'
