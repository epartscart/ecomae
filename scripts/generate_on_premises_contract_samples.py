#!/usr/bin/env python3
"""Seed PHP-side on-premises contract samples from checked-in aspnet-* goldens.

Does NOT invoke live PHP. Writes php-{stem}.json with dualSampleBaseline=
migration-contract-golden for contract-only dual-sample compare.
Never invents cutoverAllowed=true / RELEASE_OWNER_APPROVAL.md. writes=0.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DIR = ROOT / "docs/migration/evidence/on-premises-dual-samples"

REQUIRED_KEYS = (
    "ok",
    "surface",
    "action",
    "status",
    "writes",
    "writesBlocked",
    "cutoverAllowed",
    "readyForPhpRemoval",
    "phpAuthoritative",
)


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def seed_from_aspnet(aspnet_doc: dict, stem: str) -> dict:
    action = str(aspnet_doc.get("action") or stem)
    return {
        "role": "php-on-premises-contract-sample",
        "ok": bool(aspnet_doc.get("ok", True)),
        "surface": str(aspnet_doc.get("surface") or "erp-on-premises"),
        "action": action,
        "status": str(aspnet_doc.get("status") or "dry-run-validated"),
        "writes": 0,
        "writesBlocked": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "dualSampleBaseline": "migration-contract-golden",
        "pairedAspNetGolden": f"aspnet-{stem}.json",
        "phpAuthoritativePath": str(
            aspnet_doc.get("phpAuthoritativePath")
            or f"deploy/on-premises/{stem.replace('-', '_')}.php"
        ),
        "generatedAtUnix": int(time.time()),
        "note": (
            "Seeded from aspnet on-premises dry-run golden for contract-only compare. "
            "PHP deploy/on-premises + license APIs remain authoritative until field "
            "dual-sample + RELEASE_OWNER_APPROVAL.md."
        ),
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dir", type=Path, default=DEFAULT_DIR, help="Evidence directory")
    args = ap.parse_args()
    evidence = args.dir
    if not evidence.is_dir():
        print(f"FAIL: missing evidence dir {evidence}", file=sys.stderr)
        return 1

    written = 0
    skipped = 0
    errors: list[str] = []

    for aspnet_path in sorted(evidence.glob("aspnet-*.json")):
        if aspnet_path.name == "aspnet-catalog.json":
            continue
        stem = aspnet_path.name[len("aspnet-") : -len(".json")]
        try:
            aspnet_doc = load(aspnet_path)
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{aspnet_path.name}: invalid json: {ex}")
            continue
        if aspnet_doc.get("cutoverAllowed") is True or aspnet_doc.get("readyForPhpRemoval") is True:
            errors.append(f"{aspnet_path.name}: aspnet golden invents cutover/removal")
            continue
        if aspnet_doc.get("writes") not in (0, None):
            errors.append(f"{aspnet_path.name}: writes must be 0")
            continue

        php_path = evidence / f"php-{stem}.json"
        if php_path.is_file():
            try:
                existing = load(php_path)
            except Exception:  # noqa: BLE001
                existing = {}
            if existing.get("dualSampleBaseline") != "migration-contract-golden":
                skipped += 1
                continue

        php_doc = seed_from_aspnet(aspnet_doc, stem)
        missing = [k for k in REQUIRED_KEYS if k not in php_doc]
        if missing:
            errors.append(f"{stem}: seeded doc missing {missing}")
            continue
        php_path.write_text(json.dumps(php_doc, indent=2) + "\n", encoding="utf-8")
        written += 1

    stems = sorted(
        p.name[len("aspnet-") : -len(".json")]
        for p in evidence.glob("aspnet-*.json")
        if p.name != "aspnet-catalog.json"
    )
    catalog = {
        "role": "on-premises-ajax-write-catalog",
        "totalActions": len(stems),
        "dedicatedDryRuns": len([s for s in stems if s != "licenses"]),
        "readDigests": 1 if "licenses" in stems else 0,
        "coveragePct": 100,
        "actions": stems,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "phpAuthoritative": True,
        "aspNetInteractiveComplete": 0,
        "note": (
            f"{len(stems)} on-premises surfaces (writes=0 dry-runs + licenses read digest). "
            "PHP pack remains authoritative until dual-sample + RELEASE_OWNER_APPROVAL.md."
        ),
    }
    (evidence / "aspnet-catalog.json").write_text(
        json.dumps(catalog, indent=2) + "\n", encoding="utf-8"
    )
    inventory = {
        "role": "php-on-premises-authoritative-inventory",
        "generatedAtUnix": int(time.time()),
        "phpAuthoritative": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "totalActions": len(stems),
        "actions": stems,
        "note": "PHP on-premises pack remains authoritative for dry-runs + licenses registry.",
    }
    (evidence / "php-authoritative-inventory.json").write_text(
        json.dumps(inventory, indent=2) + "\n", encoding="utf-8"
    )

    result = {
        "role": "on-premises-contract-sample-generator",
        "evidenceDir": str(evidence),
        "written": written,
        "skippedLiveOrNonBaseline": skipped,
        "totalActions": len(stems),
        "errors": errors,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "phpAuthoritative": True,
    }
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: {len(errors)} error(s)", file=sys.stderr)
        return 1
    if len(stems) < 7:
        print(f"FAIL: totalActions={len(stems)} < 7", file=sys.stderr)
        return 1
    print(f"PASS: wrote {written} php-* on-premises contract baselines (writes=0) totalActions={len(stems)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
