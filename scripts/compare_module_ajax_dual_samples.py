#!/usr/bin/env python3
"""Compare CP/storefront module-ajax dual-sample evidence.

Always emits cutoverAllowed=false / readyForPhpRemoval=false.
Never invents RELEASE_OWNER_APPROVAL.md. PHP remains authoritative.
"""
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
    ap.add_argument(
        "--dir",
        default="docs/migration/evidence/module-ajax-dual-samples",
        help="Evidence directory",
    )
    ap.add_argument(
        "--out",
        default="docs/migration/evidence/module-ajax-dual-samples/compare-result.json",
        help="Compare result JSON path",
    )
    args = ap.parse_args()
    evidence = Path(args.dir)
    out = Path(args.out)

    errors: list[str] = []
    warnings: list[str] = []
    samples_ok = 0
    samples_checked = 0

    catalog_path = evidence / "aspnet-catalog.json"
    inv_path = evidence / "php-authoritative-inventory.json"
    if not catalog_path.is_file():
        errors.append("missing aspnet-catalog.json — run cloudpanel_capture_module_ajax_dual_samples.sh")
    else:
        catalog = load(catalog_path)
        if catalog.get("cutoverAllowed") is True or catalog.get("readyForPhpRemoval") is True:
            errors.append("catalog invents cutoverAllowed/readyForPhpRemoval")
        if int(catalog.get("totalActions") or 0) < 1:
            errors.append("catalog totalActions empty")
        if int(catalog.get("coveragePct") or 0) < 100:
            warnings.append(f"catalog coveragePct={catalog.get('coveragePct')} (expected 100 for inventoried actions)")

    if not inv_path.is_file():
        errors.append("missing php-authoritative-inventory.json")
    else:
        inv = load(inv_path)
        if inv.get("role") != "php-module-ajax-authoritative-inventory":
            errors.append("inventory role mismatch")
        if inv.get("phpAuthoritative") is not True:
            errors.append("inventory must set phpAuthoritative=true")
        if inv.get("cutoverAllowed") is True or inv.get("readyForPhpRemoval") is True:
            errors.append("inventory invents cutover/removal")

    for path in sorted(evidence.glob("aspnet-*.json")):
        if path.name == "aspnet-catalog.json":
            continue
        samples_checked += 1
        doc = load(path)
        if doc.get("writes") not in (0, None) and doc.get("writes") != 0:
            errors.append(f"{path.name}: writes must be 0")
        if doc.get("cutoverAllowed") is True:
            errors.append(f"{path.name}: cutoverAllowed must be false")
        if doc.get("writes") == 0 and doc.get("cutoverAllowed") is not True:
            samples_ok += 1

    result = {
        "role": "module-ajax-dual-sample-compare",
        "generatedAtUnix": int(time.time()),
        "ok": not errors,
        "samplesChecked": samples_checked,
        "samplesOk": samples_ok,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "errors": errors,
        "warnings": warnings,
        "note": (
            "ASP.NET module-ajax dry-runs are writes=0 gates only. "
            "Live PHP ajax/forms remain authoritative until field-level dual-sample + human RELEASE_OWNER_APPROVAL.md."
        ),
    }
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: module-ajax dual-sample compare -> {out}", file=sys.stderr)
        return 1
    print(f"PASS: module-ajax dual-sample compare cutoverAllowed=false -> {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
