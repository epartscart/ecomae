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
    "deploy/aspnet/yarp-exact-routes-example.json": 47,
    "deploy/aspnet/yarp-surface-digests-example.json": 30,
    "deploy/aspnet/yarp-storefront-digests-example.json": 4,
}
# catalog/api count is derived; only assert cutover flags + non-empty
for path, count in expected.items():
    doc = json.loads(Path(path).read_text(encoding="utf-8"))
    if doc.get("cutoverAllowed") is not False or doc.get("readyForPhpRemoval") is not False:
        raise SystemExit(f"FAIL: {path} must keep cutoverAllowed/readyForPhpRemoval false")
    if doc.get("routeCount") != count:
        raise SystemExit(f"FAIL: {path} routeCount={doc.get('routeCount')} expected={count}")
    print(f"PASS {path} routeCount={count} cutoverAllowed=false")

catalog = Path("deploy/aspnet/yarp-catalog-api-example.json")
doc = json.loads(catalog.read_text(encoding="utf-8"))
if doc.get("cutoverAllowed") is not False or doc.get("readyForPhpRemoval") is not False:
    raise SystemExit(f"FAIL: {catalog} must keep cutoverAllowed/readyForPhpRemoval false")
if int(doc.get("routeCount") or 0) < 1:
    raise SystemExit(f"FAIL: {catalog} routeCount must be > 0")
print(f"PASS {catalog} routeCount={doc['routeCount']} cutoverAllowed=false")
PY
