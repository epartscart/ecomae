#!/usr/bin/env python3
"""Assert the 7 named pre-decommission flows have static floors built and live-smoke blocked.

Honest offline floor: static evidence green, live-smoke stubs remain status=blocked.
Never invents cutover / RELEASE_OWNER_APPROVAL.md / status=captured without artifacts.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

EXPECTED_FLOWS = (
    "warehouse-search-offers",
    "erp-external-report-fetch",
    "einvoice",
    "ct-catalog-umapi",
    "process-flow",
    "oms-checkout",
    "super-cp-tenant-control",
)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root", type=Path, default=Path("."))
    ap.add_argument(
        "--out",
        type=Path,
        default=Path(
            "docs/migration/evidence/decommission/functional-flows/functional-static-floors.json"
        ),
    )
    args = ap.parse_args()
    root = args.root.resolve()
    evidence = root / "docs/migration/evidence"
    errors: list[str] = []

    matrix_path = evidence / "decommission/functional-flows/required-flows.json"
    built_path = evidence / "decommission/functional-flows/built-vs-pending.json"
    smoke_dir = evidence / "decommission/functional-flows/live-smoke"

    if not matrix_path.is_file():
        errors.append(f"missing {matrix_path}")
        matrix = {}
    else:
        matrix = json.loads(matrix_path.read_text(encoding="utf-8"))
        if matrix.get("cutoverAllowed") is not False:
            errors.append("required-flows invents cutover")
        flow_ids = [f.get("id") for f in (matrix.get("flows") or [])]
        if flow_ids != list(EXPECTED_FLOWS):
            errors.append(f"required-flows ids mismatch: {flow_ids}")

    if not built_path.is_file():
        errors.append(f"missing {built_path}")
        built = {}
    else:
        built = json.loads(built_path.read_text(encoding="utf-8"))
        if built.get("cutoverAllowed") is not False:
            errors.append("built-vs-pending invents cutover")
        built_ids = [b.get("id") for b in (built.get("built") or [])]
        if built_ids != list(EXPECTED_FLOWS):
            errors.append(f"built-vs-pending ids mismatch: {built_ids}")
        bad_status = [
            b.get("id")
            for b in (built.get("built") or [])
            if b.get("status") != "static-floors-green"
        ]
        if bad_status:
            errors.append(f"built statuses not static-floors-green: {bad_status}")

    smoke_blocked = 0
    smoke_captured = 0
    for fid in EXPECTED_FLOWS:
        stub = smoke_dir / f"{fid}.json"
        if not stub.is_file():
            errors.append(f"missing live-smoke stub {stub.name}")
            continue
        doc = json.loads(stub.read_text(encoding="utf-8"))
        if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
            errors.append(f"{stub.name} invents cutover/removal")
        status = str(doc.get("status") or "")
        if status == "blocked":
            smoke_blocked += 1
        elif status == "captured":
            smoke_captured += 1
            # captured is allowed only with evidence — still not PHP removal
            if not (doc.get("capturedEvidence") or []):
                errors.append(f"{stub.name}: status=captured but capturedEvidence empty")
        else:
            errors.append(f"{stub.name}: unexpected status={status!r}")

        # Static floors listed in matrix must exist.
        for flow in matrix.get("flows") or []:
            if flow.get("id") != fid:
                continue
            for rel in flow.get("requiredFloors") or []:
                if not (evidence / rel).is_file():
                    errors.append(f"{fid}: missing required floor {rel}")
            for rel in flow.get("requiredEvidenceAll") or []:
                if not (evidence / rel).is_file():
                    errors.append(f"{fid}: missing requiredEvidenceAll {rel}")

    if smoke_blocked != 7 and smoke_captured == 0:
        # Prefer all blocked until real captures; mixing is ok if captured has evidence.
        if smoke_blocked + smoke_captured != 7:
            errors.append(
                f"live-smoke stubs blocked+captured={smoke_blocked}+{smoke_captured} != 7"
            )

    result = {
        "role": "functional-static-floors",
        "generatedAtUnix": int(time.time()),
        "ok": not errors,
        "flowCount": 7,
        "staticFloorsGreen": 7 if not errors else 0,
        "liveSmokeBlocked": smoke_blocked,
        "liveSmokeCaptured": smoke_captured,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "errors": errors,
        "errorCount": len(errors),
        "note": (
            "Offline static floors for all 7 named flows are green. Live-smoke stubs remain "
            "blocked until CloudPanel artifacts. Never invent RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: {len(errors)} error(s)", file=sys.stderr)
        return 1
    print(
        f"PASS: functional static floors 7/7 green; liveSmokeBlocked={smoke_blocked} "
        f"captured={smoke_captured} cutoverAllowed=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
