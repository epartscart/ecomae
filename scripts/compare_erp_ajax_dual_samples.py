#!/usr/bin/env python3
"""Compare ERP ajax_erp dry-run dual-sample evidence.

Contract-only mode pairs aspnet-* with php-* migration-contract baselines.
Always cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

CONTRACT_KEYS = (
    "ok",
    "surface",
    "action",
    "status",
    "writes",
    "cutoverAllowed",
    "readyForPhpRemoval",
    "phpAuthoritative",
)


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def is_migration_baseline(path: Path) -> bool:
    try:
        doc = load(path)
    except Exception:  # noqa: BLE001
        return False
    return isinstance(doc, dict) and doc.get("dualSampleBaseline") == "migration-contract-golden"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default="docs/migration/evidence/erp-ajax-dual-samples")
    ap.add_argument("--out", default="docs/migration/evidence/erp-ajax-dual-samples/compare-result.json")
    ap.add_argument(
        "--contract-only",
        action="store_true",
        help="Require php-* migration-contract baselines paired with each aspnet golden",
    )
    args = ap.parse_args()
    evidence = Path(args.dir)
    errors: list[str] = []
    samples_ok = 0
    samples_checked = 0
    contract_pairs = 0
    contract_pairs_ok = 0
    migration_baseline_pairs = 0
    missing_php: list[str] = []

    catalog_path = evidence / "aspnet-catalog.json"
    inv_path = evidence / "php-authoritative-inventory.json"
    if not catalog_path.is_file():
        errors.append("missing aspnet-catalog.json")
    else:
        catalog = load(catalog_path)
        if catalog.get("cutoverAllowed") is True or catalog.get("readyForPhpRemoval") is True:
            errors.append("catalog invents cutover/removal")
        if int(catalog.get("totalActions") or 0) < 321:
            errors.append(f"catalog totalActions={catalog.get('totalActions')} < 321")
        if int(catalog.get("coveragePct") or 0) < 100:
            errors.append("catalog coveragePct < 100")

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

        if args.contract_only:
            stem = path.name[len("aspnet-") : -len(".json")]
            php = evidence / f"php-{stem}.json"
            contract_pairs += 1
            if not php.is_file():
                missing_php.append(stem)
                errors.append(f"missing php-{stem}.json contract baseline")
                continue
            php_doc = load(php)
            if not is_migration_baseline(php):
                errors.append(f"php-{stem}.json: expected dualSampleBaseline=migration-contract-golden")
                continue
            if php_doc.get("cutoverAllowed") is True or php_doc.get("readyForPhpRemoval") is True:
                errors.append(f"php-{stem}.json invents cutover/removal")
                continue
            if php_doc.get("writes") not in (0, None):
                errors.append(f"php-{stem}.json: writes must be 0")
                continue
            if php_doc.get("phpAuthoritative") is not True:
                errors.append(f"php-{stem}.json: phpAuthoritative must be true")
                continue
            missing = [k for k in CONTRACT_KEYS if k not in php_doc]
            if missing:
                errors.append(f"php-{stem}.json missing {missing}")
                continue
            asp_action = str(doc.get("action") or stem)
            if str(php_doc.get("action") or "") != asp_action:
                errors.append(f"php-{stem}.json action mismatch")
                continue
            contract_pairs_ok += 1
            migration_baseline_pairs += 1

    if samples_checked < 321:
        errors.append(f"samplesChecked={samples_checked} < 321")

    result = {
        "role": "erp-ajax-dual-sample-compare",
        "generatedAtUnix": int(time.time()),
        "ok": not errors,
        "samplesChecked": samples_checked,
        "samplesOk": samples_ok,
        "contractOnly": bool(args.contract_only),
        "contractPairs": contract_pairs,
        "contractPairsOk": contract_pairs_ok,
        "migrationBaselinePairs": migration_baseline_pairs,
        "missingPhpSides": missing_php,
        "missingPhpSideCount": len(missing_php),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "errors": errors,
        "errorCount": len(errors),
        "note": (
            "ERP ajax dry-runs are writes=0. Contract-only pairs use migration-contract-golden "
            "baselines — not live PHP. PHP ajax_erp.php remains authoritative until field "
            "dual-sample + RELEASE_OWNER_APPROVAL.md."
        ),
    }
    Path(args.out).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: {len(errors)} error(s)", file=sys.stderr)
        return 1
    print(
        f"PASS: erp-ajax dual-sample compare cutoverAllowed=false "
        f"contractPairsOk={contract_pairs_ok} -> {args.out}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
