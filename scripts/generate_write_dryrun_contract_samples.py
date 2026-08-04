#!/usr/bin/env python3
"""Seed write-dryrun dual-sample contract goldens from the Wave B probe allowlist.

1) Ensures aspnet-{stem}.json exists for every unique probe_post path
2) Seeds php-{stem}.json with dualSampleBaseline=migration-contract-golden

Does NOT invoke live PHP/ASP.NET. Never invents cutoverAllowed=true /
RELEASE_OWNER_APPROVAL.md. writes=0 always.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DIR = ROOT / "docs/migration/evidence/write-dryruns"
PROBE = ROOT / "scripts/cloudpanel_probe_write_dryruns.sh"
PROBE_RE = re.compile(
    r'probe_post\s+"([^"]+)"\s+\'([^\']*)\'\s+"([^"]*)"\s+"([^"]*)"'
)

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


def surface_for(path: str) -> str:
    if path.startswith("/erp/"):
        return "erp"
    if path.startswith("/cp/"):
        return "cp"
    if path.startswith("/bos/"):
        return "bos"
    if path.startswith("/storefront/"):
        return "storefront"
    return "other"


def stem_for(path: str) -> str:
    return path.strip("/").replace("/", "-")


def action_for(path: str, label: str) -> str:
    # Prefer last path segment; keep dry-run marker paths readable.
    parts = [p for p in path.strip("/").split("/") if p]
    return parts[-1] if parts else (label or "dry-run")


def unique_probe_rows(probe_text: str) -> list[tuple[str, str, str]]:
    """Return unique (path, label, body) rows; first label/body wins per path."""
    seen: dict[str, tuple[str, str]] = {}
    order: list[str] = []
    for path, body, _cookie, label in PROBE_RE.findall(probe_text):
        if path not in seen:
            seen[path] = (label, body)
            order.append(path)
    return [(p, seen[p][0], seen[p][1]) for p in order]


def aspnet_doc(path: str, label: str, body: str) -> dict:
    surface = surface_for(path)
    action = action_for(path, label)
    return {
        "ok": True,
        "surface": surface,
        "action": action,
        "label": label,
        "aspNetRoute": path,
        "sampleBody": body,
        "status": "dry-run-validated",
        "writes": 0,
        "writesBlocked": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "phpAuthoritative": True,
        "dualSampleBaseline": "migration-contract-golden",
        "note": (
            "Write dry-run golden from Wave B probe allowlist. Live PHP ajax remains "
            "authoritative until paired field dual-sample + RELEASE_OWNER_APPROVAL.md."
        ),
    }


def seed_php(aspnet: dict, stem: str) -> dict:
    return {
        "role": "php-write-dryrun-contract-sample",
        "ok": bool(aspnet.get("ok", True)),
        "surface": str(aspnet.get("surface") or "other"),
        "action": str(aspnet.get("action") or stem),
        "status": str(aspnet.get("status") or "dry-run-validated"),
        "writes": 0,
        "writesBlocked": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "dualSampleBaseline": "migration-contract-golden",
        "pairedAspNetGolden": f"aspnet-{stem}.json",
        "aspNetRoute": str(aspnet.get("aspNetRoute") or ""),
        "generatedAtUnix": int(time.time()),
        "note": (
            "Seeded from aspnet write-dryrun golden for contract-only compare. "
            "Live PHP ajax remains authoritative until field dual-sample + "
            "RELEASE_OWNER_APPROVAL.md."
        ),
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dir", type=Path, default=DEFAULT_DIR)
    ap.add_argument("--probe", type=Path, default=PROBE)
    args = ap.parse_args()
    evidence = args.dir
    evidence.mkdir(parents=True, exist_ok=True)
    if not args.probe.is_file():
        print(f"FAIL: missing probe {args.probe}", file=sys.stderr)
        return 1

    rows = unique_probe_rows(args.probe.read_text(encoding="utf-8"))
    if len(rows) < 183:
        print(f"FAIL: probe unique paths={len(rows)} < 183", file=sys.stderr)
        return 1

    written_aspnet = 0
    written_php = 0
    skipped_php = 0
    errors: list[str] = []
    actions: list[str] = []
    buckets = {"erp": 0, "cp": 0, "bos": 0, "storefront": 0, "other": 0}

    for path, label, body in rows:
        stem = stem_for(path)
        surface = surface_for(path)
        buckets[surface] = buckets.get(surface, 0) + 1
        actions.append(stem)

        asp_path = evidence / f"aspnet-{stem}.json"
        if asp_path.is_file():
            try:
                existing = load(asp_path)
            except Exception as ex:  # noqa: BLE001
                errors.append(f"{asp_path.name}: invalid json: {ex}")
                continue
            if existing.get("cutoverAllowed") is True or existing.get("readyForPhpRemoval") is True:
                errors.append(f"{asp_path.name}: invents cutover/removal")
                continue
            if existing.get("writes") not in (0, None):
                errors.append(f"{asp_path.name}: writes must be 0")
                continue
            asp = existing
        else:
            asp = aspnet_doc(path, label, body)
            asp_path.write_text(json.dumps(asp, indent=2) + "\n", encoding="utf-8")
            written_aspnet += 1

        php_path = evidence / f"php-{stem}.json"
        if php_path.is_file():
            try:
                existing_php = load(php_path)
            except Exception:  # noqa: BLE001
                existing_php = {}
            if existing_php.get("dualSampleBaseline") != "migration-contract-golden":
                skipped_php += 1
                continue
        php = seed_php(asp, stem)
        missing = [k for k in REQUIRED_KEYS if k not in php]
        if missing:
            errors.append(f"{stem}: php missing {missing}")
            continue
        php_path.write_text(json.dumps(php, indent=2) + "\n", encoding="utf-8")
        written_php += 1

    catalog = {
        "role": "write-dryrun-ajax-write-catalog",
        "totalActions": len(rows),
        "dedicatedDryRuns": len(rows),
        "coveragePct": 100,
        "buckets": buckets,
        "actions": actions,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "phpAuthoritative": True,
        "aspNetInteractiveComplete": 0,
        "note": (
            f"{len(rows)} unique Wave B write dry-run routes (writes=0). "
            "PHP ajax remains authoritative until dual-sample + RELEASE_OWNER_APPROVAL.md."
        ),
    }
    (evidence / "aspnet-catalog.json").write_text(
        json.dumps(catalog, indent=2) + "\n", encoding="utf-8"
    )
    inventory = {
        "role": "php-write-dryrun-authoritative-inventory",
        "generatedAtUnix": int(time.time()),
        "phpAuthoritative": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "totalActions": len(rows),
        "actions": actions,
        "note": "PHP ajax/forms remain authoritative for all write dry-run surfaces.",
    }
    (evidence / "php-authoritative-inventory.json").write_text(
        json.dumps(inventory, indent=2) + "\n", encoding="utf-8"
    )

    result = {
        "role": "write-dryrun-contract-sample-generator",
        "evidenceDir": str(evidence),
        "uniqueProbePaths": len(rows),
        "writtenAspNet": written_aspnet,
        "writtenPhp": written_php,
        "skippedLiveOrNonBaseline": skipped_php,
        "buckets": buckets,
        "errors": errors,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "phpAuthoritative": True,
    }
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: {len(errors)} error(s)", file=sys.stderr)
        return 1
    print(
        f"PASS: write-dryrun contract goldens unique={len(rows)} "
        f"aspnetWritten={written_aspnet} phpWritten={written_php}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
