#!/usr/bin/env bash
# Regenerate all design-only YARP JSON packs from nginx exact-route allowlists.
# Always asserts cutoverAllowed=false. Does NOT enable YARP.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

python3 "$ROOT/scripts/generate_yarp_exact_routes_example.py"

python3 - <<'PY'
import json
from pathlib import Path

expected = {
    "deploy/aspnet/yarp-exact-routes-example.json": 61,
    "deploy/aspnet/yarp-surface-digests-example.json": 44,
    "deploy/aspnet/yarp-storefront-digests-example.json": 6,
    # Keep in sync with scripts/validate_catalog_api_allowlist_sync.py (nginx exact-route floor).
    "deploy/aspnet/yarp-catalog-api-example.json": 19,
}
for path, count in expected.items():
    doc = json.loads(Path(path).read_text(encoding="utf-8"))
    if doc.get("cutoverAllowed") is not False or doc.get("readyForPhpRemoval") is not False:
        raise SystemExit(f"FAIL: {path} must keep cutoverAllowed/readyForPhpRemoval false")
    if doc.get("routeCount") != count:
        raise SystemExit(f"FAIL: {path} routeCount={doc.get('routeCount')} expected={count}")
    routes = (doc.get("ReverseProxy") or {}).get("Routes") or {}
    if not isinstance(routes, dict) or len(routes) != count:
        raise SystemExit(
            f"FAIL: {path} ReverseProxy.Routes len={len(routes) if isinstance(routes, dict) else type(routes)} expected={count}"
        )
    print(f"PASS {path} routeCount={count} cutoverAllowed=false")
PY
