#!/usr/bin/env python3
"""Lock module-function inventory ↔ PHP catalog coverage board consistency.

Never invents RELEASE_OWNER_APPROVAL.md / MODULE_FUNCTION_TEST_PASS.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MIN_TOTAL = 714

STATUS_TO_COVERAGE = {
    "digest-only": "digest-contract",
    "digest-only+hybrid-deeplink": "digest-contract",
    "hybrid-deeplink": "hybrid-directory-only",
    "php-only": "php-only-deeplink",
}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--inventory",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/module-function-parity/module-function-inventory.json",
    )
    ap.add_argument(
        "--coverage-board",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/surface-parity/php-catalog-coverage-board.json",
    )
    ap.add_argument(
        "--evidence-out",
        type=Path,
        default=ROOT
        / "docs/migration/evidence/module-function-parity/coverage-consistency.json",
    )
    args = ap.parse_args()
    errors: list[str] = []

    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    board = json.loads(args.coverage_board.read_text(encoding="utf-8"))

    for label, doc in (("inventory", inventory), ("coverageBoard", board)):
        if doc.get("cutoverAllowed") is not False:
            errors.append(f"{label}.cutoverAllowed must be false")
        if doc.get("readyForPhpRemoval") is not False:
            errors.append(f"{label}.readyForPhpRemoval must be false")

    if inventory.get("aspnetCompleteCount") != 0:
        errors.append("inventory.aspnetCompleteCount must stay 0")
    if board.get("aspNetInteractiveComplete") != 0:
        errors.append("coverageBoard.aspNetInteractiveComplete must stay 0")
    if int(board.get("missingCount") or 0) != 0:
        errors.append(f"coverageBoard.missingCount={board.get('missingCount')} expected 0")
    if int(board.get("totalTracked") or 0) < MIN_TOTAL:
        errors.append(f"coverageBoard.totalTracked={board.get('totalTracked')} < {MIN_TOTAL}")

    inv_rows = {
        str(m.get("id")): m
        for m in (inventory.get("modules") or [])
        if isinstance(m, dict) and m.get("kind") != "hybrid-preview" and m.get("id")
    }
    board_rows = {
        str(i.get("id")): i
        for i in (board.get("items") or [])
        if isinstance(i, dict) and i.get("id")
    }

    if len(inv_rows) < MIN_TOTAL:
        errors.append(f"inventory catalog rows={len(inv_rows)} < {MIN_TOTAL}")
    if len(board_rows) < MIN_TOTAL:
        errors.append(f"coverage board rows={len(board_rows)} < {MIN_TOTAL}")

    missing_on_board = sorted(set(inv_rows) - set(board_rows))[:20]
    missing_on_inv = sorted(set(board_rows) - set(inv_rows))[:20]
    if missing_on_board:
        errors.append(f"inventory ids missing on coverage board sample={missing_on_board}")
    if missing_on_inv:
        errors.append(f"coverage board ids missing on inventory sample={missing_on_inv}")

    mismatched = 0
    for mid, inv in inv_rows.items():
        cov = board_rows.get(mid)
        if not cov:
            continue
        expected = STATUS_TO_COVERAGE.get(str(inv.get("status") or ""))
        actual = str(cov.get("coverage") or "")
        # digest-status may map to hybrid-directory-only when digestRoute is not contracted
        if expected == "digest-contract" and actual in {
            "digest-contract",
            "hybrid-directory-only",
        }:
            continue
        if expected is None:
            errors.append(f"{mid}: unexpected inventory status {inv.get('status')!r}")
            continue
        if actual != expected:
            mismatched += 1
            if mismatched <= 10:
                errors.append(
                    f"{mid}: inventory status={inv.get('status')!r} "
                    f"coverage={actual!r} expected={expected!r}"
                )

    if mismatched > 10:
        errors.append(f"...and {mismatched - 10} more status/coverage mismatches")

    out = {
        "role": "module-function-coverage-consistency",
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspnetCompleteCount": 0,
        "inventoryCatalogRows": len(inv_rows),
        "coverageBoardRows": len(board_rows),
        "matchedIds": len(set(inv_rows) & set(board_rows)),
        "coverageCounts": board.get("coverageCounts") or {},
        "inventoryStatusCounts": {
            "php-only": sum(1 for m in inv_rows.values() if m.get("status") == "php-only"),
            "digest-only+hybrid-deeplink": sum(
                1
                for m in inv_rows.values()
                if m.get("status") == "digest-only+hybrid-deeplink"
            ),
            "hybrid-deeplink": sum(
                1 for m in inv_rows.values() if m.get("status") == "hybrid-deeplink"
            ),
        },
        "ok": not errors,
        "errors": errors,
        "note": (
            "Inventory and coverage board must enumerate the same 714 PHP catalog ids. "
            "Interactive aspnet-complete stays 0 until human MODULE_FUNCTION_TEST_PASS.md. "
            "Never invents RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.evidence_out.parent.mkdir(parents=True, exist_ok=True)
    args.evidence_out.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")

    if errors:
        print("FAIL: module-function coverage consistency", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1

    print(
        f"PASS: matchedIds={out['matchedIds']} coverageCounts={out['coverageCounts']} "
        f"cutoverAllowed=false aspnetCompleteCount=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
