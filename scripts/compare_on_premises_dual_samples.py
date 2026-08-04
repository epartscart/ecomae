#!/usr/bin/env python3
"""Compare on-premises dry-run dual-sample contract floor (writes=0).

--contract-only requires php-{stem}.json migration-contract-golden baselines
for every aspnet-* golden. Never invents cutoverAllowed=true /
RELEASE_OWNER_APPROVAL.md. PHP deploy/on-premises remains authoritative.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DIR = ROOT / "docs/migration/evidence/on-premises-dual-samples"
MIN_ACTIONS = 6

CONTRACT_KEYS = (
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


def is_migration_baseline(path: Path) -> bool:
    try:
        return load(path).get("dualSampleBaseline") == "migration-contract-golden"
    except Exception:  # noqa: BLE001
        return False


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dir", type=Path, default=DEFAULT_DIR)
    ap.add_argument(
        "--out",
        type=Path,
        default=DEFAULT_DIR / "compare-result.json",
    )
    ap.add_argument(
        "--contract-only",
        action="store_true",
        help="Require php-* migration-contract-golden baseline per aspnet golden",
    )
    args = ap.parse_args()
    evidence = args.dir
    errors: list[str] = []
    samples_checked = 0
    samples_ok = 0
    contract_pairs = 0
    contract_pairs_ok = 0
    migration_baseline_pairs = 0
    missing_php: list[str] = []

    if not evidence.is_dir():
        print(f"FAIL: missing evidence dir {evidence}", file=sys.stderr)
        return 1

    catalog_path = evidence / "aspnet-catalog.json"
    inv_path = evidence / "php-authoritative-inventory.json"
    if not catalog_path.is_file():
        errors.append("missing aspnet-catalog.json")
    else:
        catalog = load(catalog_path)
        if catalog.get("cutoverAllowed") is True or catalog.get("readyForPhpRemoval") is True:
            errors.append("catalog invents cutover/removal")
        if int(catalog.get("totalActions") or 0) < MIN_ACTIONS:
            errors.append(f"catalog totalActions={catalog.get('totalActions')} < {MIN_ACTIONS}")
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
        actions = inv.get("actions") or []
        if not isinstance(actions, list) or len(actions) < MIN_ACTIONS:
            errors.append(f"inventory actions count {len(actions) if isinstance(actions, list) else 0} < {MIN_ACTIONS}")

    for path in sorted(evidence.glob("aspnet-*.json")):
        if path.name == "aspnet-catalog.json":
            continue
        samples_checked += 1
        doc = load(path)
        if doc.get("writes") not in (0, None):
            errors.append(f"{path.name}: writes must be 0")
        if doc.get("cutoverAllowed") is True:
            errors.append(f"{path.name}: cutoverAllowed must be false")
        if doc.get("readyForPhpRemoval") is True:
            errors.append(f"{path.name}: readyForPhpRemoval must be false")
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
                errors.append(
                    f"php-{stem}.json: expected dualSampleBaseline=migration-contract-golden"
                )
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

    if samples_checked < MIN_ACTIONS:
        errors.append(f"samplesChecked={samples_checked} < {MIN_ACTIONS}")

    result = {
        "role": "on-premises-dual-sample-compare",
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
            "On-premises dry-runs are writes=0. Contract-only pairs use "
            "migration-contract-golden baselines — not live PHP. "
            "PHP deploy/on-premises remains authoritative until field dual-sample + "
            "RELEASE_OWNER_APPROVAL.md."
        ),
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: {len(errors)} error(s)", file=sys.stderr)
        return 1
    print(
        f"PASS: on-premises dual-sample compare cutoverAllowed=false "
        f"contractPairsOk={contract_pairs_ok} -> {args.out}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
