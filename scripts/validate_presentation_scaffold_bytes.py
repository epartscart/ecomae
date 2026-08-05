#!/usr/bin/env python3
"""Fail-closed offline lock for presentation scaffold byte floors + login chrome markers.

Runs estimate_presentation_scaffold_bytes.py, then asserts storefront/BOS/ERP floors and
markers. Does NOT invent php-vs-aspnet-recheck status=pass, cutoverAllowed=true, or
RELEASE_OWNER_APPROVAL.md. Live recheck remains soft-fail until CloudPanel redeploy.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_FLOORS = ("storefront_app", "bos_login", "erp_login")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root", type=Path, default=ROOT)
    ap.add_argument(
        "--estimate",
        type=Path,
        default=Path("scripts/estimate_presentation_scaffold_bytes.py"),
    )
    ap.add_argument(
        "--report",
        type=Path,
        default=Path(
            "docs/migration/evidence/presentation/presentation-scaffold-bytes-estimate.json"
        ),
    )
    ap.add_argument("--skip-estimate", action="store_true")
    args = ap.parse_args()
    root = args.root.resolve()
    errors: list[str] = []

    if not args.skip_estimate:
        est = root / args.estimate
        if not est.is_file():
            print(f"FAIL: missing estimator {est}", file=sys.stderr)
            return 1
        proc = subprocess.run(
            [sys.executable, str(est)],
            cwd=str(root),
            capture_output=True,
            text=True,
        )
        if proc.returncode != 0:
            print(proc.stdout)
            print(proc.stderr, file=sys.stderr)
            print("FAIL: estimate_presentation_scaffold_bytes.py exited non-zero", file=sys.stderr)
            return 1

    report_path = root / args.report
    if not report_path.is_file():
        errors.append(f"missing report {report_path}")
        report: dict = {}
    else:
        report = json.loads(report_path.read_text(encoding="utf-8"))

    if report.get("cutoverAllowed") is not False:
        errors.append("report cutoverAllowed must be false")
    if report.get("readyForPhpRemoval") is not False:
        errors.append("report readyForPhpRemoval must be false")
    if report.get("aspNetInteractiveComplete") not in (0, False):
        errors.append("report aspNetInteractiveComplete must stay 0")

    floors = report.get("floors") or {}
    for key in REQUIRED_FLOORS:
        floor = floors.get(key) if isinstance(floors, dict) else None
        if not isinstance(floor, dict):
            errors.append(f"missing floors.{key}")
            continue
        if floor.get("meetsFloor") is not True:
            errors.append(f"floors.{key}.meetsFloor is not true: {floor}")
        if key in {"bos_login", "erp_login"} and floor.get("markerOk") is not True:
            errors.append(f"floors.{key}.markerOk is not true: {floor}")

    files = report.get("files") or {}
    for key in ("bos_login", "erp_login", "cp_login", "storefront_home_depth"):
        info = files.get(key) if isinstance(files, dict) else None
        if not isinstance(info, dict):
            errors.append(f"missing files.{key}")
            continue
        if info.get("markersOk") is not True:
            errors.append(
                f"files.{key}.markersOk false missing={info.get('markersMissing')}"
            )

    # Never invent live recheck pass.
    recheck = root / "docs/migration/evidence/presentation/php-vs-aspnet-recheck.json"
    if recheck.is_file():
        doc = json.loads(recheck.read_text(encoding="utf-8"))
        if doc.get("status") == "pass" and doc.get("cutoverAllowed") is True:
            errors.append("recheck invents status=pass with cutoverAllowed=true")
        if doc.get("readyForPhpRemoval") is True:
            errors.append("recheck invents readyForPhpRemoval=true")

    if errors:
        print("FAIL: presentation scaffold bytes", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        "PASS: presentation scaffold floors storefront/bos/erp meetFloor=true; "
        "login markers ok; cutoverAllowed=false; recheck remains soft-fail until live"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
