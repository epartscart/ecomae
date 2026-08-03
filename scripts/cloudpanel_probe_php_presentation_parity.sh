#!/usr/bin/env bash
# Probe live PHP vs ASP.NET presentation. Default exits non-zero (honest fail) until parity.
# Soft mode for CI attach: ECOMAE_PRESENTATION_SOFT=1
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export ECOMAE_PRESENTATION_SOFT="${ECOMAE_PRESENTATION_SOFT:-0}"
python3 "$ROOT/scripts/compare_php_aspnet_presentation.py"
