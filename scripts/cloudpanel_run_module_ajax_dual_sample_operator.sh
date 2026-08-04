#!/usr/bin/env bash
# CloudPanel operator: CP/storefront module-ajax dual-sample floor.
# Captures ASP.NET dry-run samples + PHP authoritative inventory, then compares.
# Never invents RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

OUT_DIR="${ECOMAE_MODULE_AJAX_EVIDENCE_DIR:-$ROOT/docs/migration/evidence/module-ajax-dual-samples}"
mkdir -p "$OUT_DIR"

bash "$ROOT/scripts/cloudpanel_capture_module_ajax_dual_samples.sh"
python3 "$ROOT/scripts/compare_module_ajax_dual_samples.py" --dir "$OUT_DIR" --out "$OUT_DIR/compare-result.json"
