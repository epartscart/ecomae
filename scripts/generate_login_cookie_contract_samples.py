#!/usr/bin/env python3
"""Seed PHP-side login-bridge contract samples from aspnet-*-login-bridge.json.

Does NOT invoke live PHP. Writes php-{surface}-login-bridge.json with
dualSampleBaseline=migration-contract-golden. Never invents cutover/approval.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DIR = ROOT / "docs/migration/evidence/login-session-bridge"
SURFACES = ("cp", "erp", "bos", "storefront")


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def seed_from_aspnet(aspnet: dict, surface: str) -> dict:
    return {
        "role": "php-login-bridge-contract-sample",
        "surface": surface,
        "phpAuthoritative": True,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "dualSampleBaseline": "migration-contract-golden",
        "pairedAspNetGolden": f"aspnet-{surface}-login-bridge.json",
        "setCookie": list(aspnet.get("setCookie") or aspnet.get("set_cookie") or []),
        "probe": dict(aspnet.get("probe") or {}),
        "generatedAtUnix": int(time.time()),
        "note": (
            "Seeded from aspnet login-bridge golden for contract-only compare. "
            "Live PHP login/session remains authoritative until field dual-sample + "
            "RELEASE_OWNER_APPROVAL.md."
        ),
    }


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dir", type=Path, default=DEFAULT_DIR)
    args = ap.parse_args()
    evidence = args.dir
    if not evidence.is_dir():
        print(f"FAIL: missing {evidence}", file=sys.stderr)
        return 1

    written = 0
    errors: list[str] = []
    for surface in SURFACES:
        asp = evidence / f"aspnet-{surface}-login-bridge.json"
        if not asp.is_file():
            errors.append(f"missing {asp.name}")
            continue
        try:
            doc = load(asp)
        except Exception as ex:  # noqa: BLE001
            errors.append(f"{asp.name}: {ex}")
            continue
        if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
            errors.append(f"{asp.name}: invents cutover/removal")
            continue
        php_path = evidence / f"php-{surface}-login-bridge.json"
        php = seed_from_aspnet(doc, surface)
        php_path.write_text(json.dumps(php, indent=2) + "\n", encoding="utf-8")
        written += 1

    result = {
        "role": "login-cookie-contract-sample-generator",
        "written": written,
        "expected": len(SURFACES),
        "errors": errors,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "phpAuthoritative": True,
    }
    print(json.dumps(result, indent=2))
    if errors or written != len(SURFACES):
        print("FAIL: login-cookie contract samples incomplete", file=sys.stderr)
        return 1
    print(f"PASS: wrote {written}/4 php-* login-bridge contract baselines")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
