#!/usr/bin/env bash
# Compatibility wrapper — use FORCE_LIVE_NOW (proves PUBLIC epartscart, not only :5100).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec bash "$ROOT/scripts/cloudpanel_FORCE_LIVE_NOW.sh"
