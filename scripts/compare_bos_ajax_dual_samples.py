#!/usr/bin/env python3
"""Compare BOS ajax dry-run dual-sample evidence. Always cutoverAllowed=false."""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default="docs/migration/evidence/bos-ajax-dual-samples")
    ap.add_argument("--out", default="docs/migration/evidence/bos-ajax-dual-samples/compare-result.json")
    args = ap.parse_args()
    evidence = Path(args.dir)
    errors: list[str] = []
    samples_ok = 0
    samples_checked = 0

    catalog_path = evidence / "aspnet-catalog.json"
    inv_path = evidence / "php-authoritative-inventory.json"
    if not catalog_path.is_file():
        errors.append("missing aspnet-catalog.json")
    else:
        catalog = load(catalog_path)
        if catalog.get("cutoverAllowed") is True or catalog.get("readyForPhpRemoval") is True:
            errors.append("catalog invents cutover/removal")
        if int(catalog.get("totalActions") or 0) < 231:
            errors.append(f"catalog totalActions={catalog.get('totalActions')} < 231")

    if not inv_path.is_file():
        errors.append("missing php-authoritative-inventory.json")
    else:
        inv = load(inv_path)
        if inv.get("phpAuthoritative") is not True:
            errors.append("inventory must set phpAuthoritative=true")
        if inv.get("cutoverAllowed") is True or inv.get("readyForPhpRemoval") is True:
            errors.append("inventory invents cutover/removal")

    for path in sorted(evidence.glob("aspnet-*.json")):
        if path.name == "aspnet-catalog.json":
            continue
        samples_checked += 1
        doc = load(path)
        if doc.get("writes") not in (0, None):
            errors.append(f"{path.name}: writes must be 0")
        if doc.get("cutoverAllowed") is True:
            errors.append(f"{path.name}: cutoverAllowed must be false")
        if doc.get("writes") == 0:
            samples_ok += 1

    if samples_checked < 231:
        errors.append(f"samplesChecked={samples_checked} < 231")

    result = {
        "role": "bos-ajax-dual-sample-compare",
        "generatedAtUnix": int(time.time()),
        "ok": not errors,
        "samplesChecked": samples_checked,
        "samplesOk": samples_ok,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "errors": errors,
        "note": "BOS ajax dry-runs are writes=0. PHP bos/ajax remains authoritative until dual-sample + RELEASE_OWNER_APPROVAL.md.",
    }
    Path(args.out).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
