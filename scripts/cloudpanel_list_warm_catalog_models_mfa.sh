#!/usr/bin/env bash
# Compatibility wrapper → cloudpanel_list_warm_catalog_vehicle_ids.sh models
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec bash "$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh" models "$@"
