#!/usr/bin/env python3
"""Seed PHP-side ajax contract samples for CP/ERP/BOS evidence dirs.

Reads aspnet-*.json goldens and writes php-{stem}.json with
dualSampleBaseline=migration-contract-golden. Never invokes live PHP.
Never invents cutoverAllowed=true / RELEASE_OWNER_APPROVAL.md. writes=0.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

SURFACE_DIRS = {
    "cp": ROOT / "docs/migration/evidence/module-ajax-dual-samples",
    "erp": ROOT / "docs/migration/evidence/erp-ajax-dual-samples",
    "bos": ROOT / "docs/migration/evidence/bos-ajax-dual-samples",
}

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


def seed_from_aspnet(aspnet_doc: dict, stem: str, surface: str) -> dict:
    action = str(aspnet_doc.get("action") or stem)
    module = str(aspnet_doc.get("module") or surface)
    return {
        "role": f"php-{surface}-ajax-contract-sample",
        "ok": bool(aspnet_doc.get("ok", True)),
        "surface": surface,
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
            f"Seeded from aspnet {surface} ajax golden for contract-only compare. "
            "Live PHP ajax remains authoritative until paired field dual-sample + "
            "RELEASE_OWNER_APPROVAL.md."
        ),
    }


def generate_for_dir(evidence: Path, surface: str) -> dict:
    written = 0
    skipped = 0
    errors: list[str] = []
    if not evidence.is_dir():
        return {
            "surface": surface,
            "written": 0,
            "skipped": 0,
            "errors": [f"missing evidence dir {evidence}"],
        }

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

        php_doc = seed_from_aspnet(aspnet_doc, stem, surface)
        missing = [k for k in REQUIRED_KEYS if k not in php_doc]
        if missing:
            errors.append(f"{stem}: seeded doc missing {missing}")
            continue
        php_path.write_text(json.dumps(php_doc, indent=2) + "\n", encoding="utf-8")
        written += 1

    return {
        "surface": surface,
        "evidenceDir": str(evidence),
        "written": written,
        "skippedLiveOrNonBaseline": skipped,
        "errors": errors,
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--surface",
        choices=sorted(SURFACE_DIRS) + ["all"],
        default="all",
        help="Which ajax surface evidence dir to seed",
    )
    ap.add_argument("--dir", type=Path, default=None, help="Override evidence directory")
    args = ap.parse_args()

    surfaces = list(SURFACE_DIRS) if args.surface == "all" else [args.surface]
    results = []
    all_errors: list[str] = []
    total_written = 0
    for surface in surfaces:
        evidence = args.dir if args.dir is not None else SURFACE_DIRS[surface]
        result = generate_for_dir(evidence, surface)
        results.append(result)
        total_written += int(result.get("written") or 0)
        all_errors.extend(f"{surface}:{e}" for e in (result.get("errors") or []))

    summary = {
        "role": "ajax-surface-contract-sample-generation",
        "generatedAtUnix": int(time.time()),
        "surfaces": results,
        "writtenTotal": total_written,
        "errors": all_errors,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "note": "Contract-only PHP sides seeded from aspnet goldens; no live PHP invoked.",
    }
    print(json.dumps(summary, indent=2))
    if all_errors:
        print(f"FAIL: {len(all_errors)} error(s)", file=sys.stderr)
        return 1
    print(f"OK: wrote {total_written} php-* contract sample(s) across {len(surfaces)} surface(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
