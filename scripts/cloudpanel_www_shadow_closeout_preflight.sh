#!/usr/bin/env bash
# Offline preflight for www shadow closeout floors (storefront + functional + marketing).
# Does not invent cutoverAllowed / readyForPhpRemoval / aspNetInteractiveComplete>0.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

fail=0

echo "== storefront shadow allowlist sync =="
if ! python3 scripts/validate_storefront_shadow_allowlist_sync.py; then
  fail=1
fi

echo "== functional static floors =="
if ! python3 scripts/validate_functional_static_floors.py; then
  fail=1
fi

echo "== marketing app allowlist sync =="
if ! python3 scripts/validate_marketing_app_allowlist_sync.py; then
  fail=1
fi

echo "== presentation scaffold bytes (offline; does not invent recheck pass) =="
if ! python3 scripts/validate_presentation_scaffold_bytes.py; then
  fail=1
fi

if [[ "$fail" -ne 0 ]]; then
  echo "FAIL: www shadow closeout preflight"
  exit 1
fi

echo "PASS: www shadow closeout offline preflight"
echo "NEXT_LIVE: ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES bash scripts/cloudpanel_www_shadow_closeout_operator.sh"
echo "PHP_DELETE: still REFUSE until ReadyToRemovePhp=true + human approval"
