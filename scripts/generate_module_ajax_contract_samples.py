#!/usr/bin/env python3
"""Seed PHP-side module-ajax contract samples from checked-in aspnet-* goldens.

Does NOT invoke live PHP. Writes php-{stem}.json with dualSampleBaseline=
migration-contract-golden for contract-only dual-sample compare.
Never invents cutoverAllowed=true / RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DIR = ROOT / "docs/migration/evidence/module-ajax-dual-samples"

REQUIRED_KEYS = (
    "ok",
    "surface",
    "module",
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
    module = str(aspnet_doc.get("module") or "")
    action = str(aspnet_doc.get("action") or "")
    if not module or not action:
        # Derive from stem when legacy golden omits explicit fields.
        if "-" in stem:
            module, action = stem.split("-", 1)
        else:
            module, action = stem, "dry_run"
    return {
        "role": "php-module-ajax-contract-sample",
        "ok": bool(aspnet_doc.get("ok", True)),
        "surface": str(aspnet_doc.get("surface") or "cp"),
        "module": module,
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
        "generatedAtUnix": int(time.time()),
        "note": (
            "Seeded from aspnet migration-contract golden for contract-only compare. "
            "Live PHP ajax remains authoritative until paired field dual-sample + "
            "RELEASE_OWNER_APPROVAL.md."
        ),
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dir", type=Path, default=DEFAULT_DIR, help="Evidence directory")
    ap.add_argument(
        "--also-migration-dir",
        action="store_true",
        help="Also write migration/{stem}.json mirrors (digest-style layout)",
    )
    args = ap.parse_args()
    evidence = args.dir
    if not evidence.is_dir():
        print(f"FAIL: missing evidence dir {evidence}", file=sys.stderr)
        return 1

    written = 0
    skipped = 0
    errors: list[str] = []
    mig_dir = evidence / "migration"
    if args.also_migration_dir:
        mig_dir.mkdir(parents=True, exist_ok=True)

    for aspnet_path in sorted(evidence.glob("aspnet-*.json")):
        if aspnet_path.name == "aspnet-catalog.json":
            continue
        stem = aspnet_path.name[len("aspnet-"):-len(".json")]
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

        if args.also_migration_dir:
            mig_path = mig_dir / f"{stem}.json"
            mig_doc = dict(php_doc)
            mig_doc["role"] = "module-ajax-migration-contract-golden"
            mig_path.write_text(json.dumps(mig_doc, indent=2) + "\n", encoding="utf-8")

    summary = {
        "role": "module-ajax-contract-sample-generation",
        "generatedAtUnix": int(time.time()),
        "evidenceDir": str(evidence),
        "written": written,
        "skippedLiveOrNonBaseline": skipped,
        "errors": errors,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "note": "Contract-only PHP sides seeded from aspnet goldens; no live PHP invoked.",
    }
    print(json.dumps(summary, indent=2))
    if errors:
        print(f"FAIL: {len(errors)} error(s)", file=sys.stderr)
        return 1
    print(f"OK: wrote {written} php-* contract sample(s), skipped {skipped} non-baseline php captures")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
