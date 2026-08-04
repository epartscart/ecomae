#!/usr/bin/env bash
# CloudPanel operator: CP/storefront module-ajax dual-sample floor.
# Captures ASP.NET dry-run samples + PHP authoritative inventory, then compares.
# Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="${ECOMAE_MODULE_AJAX_EVIDENCE_DIR:-$ROOT/docs/migration/evidence/module-ajax-dual-samples}"
mkdir -p "$OUT_DIR"

# Seed php-* contract baselines from aspnet goldens (no live PHP).
python3 "$ROOT/scripts/generate_module_ajax_contract_samples.py" --dir "$OUT_DIR"

if [[ "${ECOMAE_MODULE_AJAX_SKIP_CAPTURE:-}" != "1" ]]; then
  bash "$ROOT/scripts/cloudpanel_capture_module_ajax_dual_samples.sh"
else
  echo "SKIP capture (ECOMAE_MODULE_AJAX_SKIP_CAPTURE=1) — using checked-in aspnet goldens"
fi

python3 "$ROOT/scripts/compare_module_ajax_dual_samples.py" \
  --dir "$OUT_DIR" \
  --out "$OUT_DIR/compare-result.json" \
  --contract-only
