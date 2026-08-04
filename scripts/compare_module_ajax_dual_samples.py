#!/usr/bin/env python3
"""Compare CP/storefront module-ajax dual-sample evidence.

Contract-only mode pairs aspnet-* goldens with php-* / migration/ baselines
(dualSampleBaseline=migration-contract-golden). Live PHP captures win when
present and not seeded baselines.

Always emits cutoverAllowed=false / readyForPhpRemoval=false.
Never invents RELEASE_OWNER_APPROVAL.md. PHP remains authoritative.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

CONTRACT_KEYS = (
    "ok",
    "surface",
    "module",
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
    if path.parent.name == "migration":
        return True
    try:
        doc = load(path)
    except Exception:  # noqa: BLE001
        return False
    if not isinstance(doc, dict):
        return False
    return doc.get("dualSampleBaseline") == "migration-contract-golden"


def resolve_php(evidence: Path, stem: str) -> tuple[Path | None, bool]:
    """Return (php_path, used_migration_baseline)."""
    php = evidence / f"php-{stem}.json"
    if php.exists() and not is_migration_baseline(php):
        return php, False
    mig = evidence / "migration" / f"{stem}.json"
    if mig.exists():
        return mig, True
    if php.exists() and is_migration_baseline(php):
        return php, True
    return None, False


def validate_aspnet_sample(path: Path, doc: dict, errors: list[str]) -> bool:
    if doc.get("writes") not in (0, None) and doc.get("writes") != 0:
        errors.append(f"{path.name}: writes must be 0")
        return False
    if doc.get("cutoverAllowed") is True:
        errors.append(f"{path.name}: cutoverAllowed must be false")
        return False
    if doc.get("readyForPhpRemoval") is True:
        errors.append(f"{path.name}: readyForPhpRemoval must be false")
        return False
    return doc.get("writes") == 0 and doc.get("cutoverAllowed") is not True


def validate_php_contract(path: Path, aspnet_doc: dict, errors: list[str], warnings: list[str]) -> bool:
    try:
        doc = load(path)
    except Exception as ex:  # noqa: BLE001
        errors.append(f"{path.name}: invalid json: {ex}")
        return False
    if doc.get("cutoverAllowed") is True or doc.get("readyForPhpRemoval") is True:
        errors.append(f"{path.name}: php side invents cutover/removal")
        return False
    if doc.get("writes") not in (0, None):
        errors.append(f"{path.name}: writes must be 0")
        return False
    if doc.get("phpAuthoritative") is not True:
        errors.append(f"{path.name}: phpAuthoritative must be true")
        return False
    missing = [k for k in CONTRACT_KEYS if k not in doc]
    if missing:
        errors.append(f"{path.name}: missing contract keys {missing}")
        return False
    asp_module = str(aspnet_doc.get("module") or "")
    asp_action = str(aspnet_doc.get("action") or "")
    if asp_module and str(doc.get("module") or "") != asp_module:
        errors.append(f"{path.name}: module mismatch vs aspnet ({doc.get('module')} != {asp_module})")
        return False
    if asp_action and str(doc.get("action") or "") != asp_action:
        errors.append(f"{path.name}: action mismatch vs aspnet ({doc.get('action')} != {asp_action})")
        return False
    if is_migration_baseline(path) and doc.get("dualSampleBaseline") != "migration-contract-golden":
        warnings.append(f"{path.name}: migration baseline missing dualSampleBaseline tag")
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--dir",
        default="docs/migration/evidence/module-ajax-dual-samples",
        help="Evidence directory",
    )
    ap.add_argument(
        "--out",
        default="docs/migration/evidence/module-ajax-dual-samples/compare-result.json",
        help="Compare result JSON path",
    )
    ap.add_argument(
        "--contract-only",
        action="store_true",
        help="Pair aspnet goldens with php/migration contract baselines",
    )
    args = ap.parse_args()
    evidence = Path(args.dir)
    out = Path(args.out)

    errors: list[str] = []
    warnings: list[str] = []
    samples_ok = 0
    samples_checked = 0
    contract_pairs = 0
    contract_pairs_ok = 0
    migration_baseline_pairs = 0
    missing_php_sides: list[str] = []

    catalog_path = evidence / "aspnet-catalog.json"
    inv_path = evidence / "php-authoritative-inventory.json"
    if not catalog_path.is_file():
        errors.append("missing aspnet-catalog.json — run cloudpanel_capture_module_ajax_dual_samples.sh")
    else:
        catalog = load(catalog_path)
        if catalog.get("cutoverAllowed") is True or catalog.get("readyForPhpRemoval") is True:
            errors.append("catalog invents cutoverAllowed/readyForPhpRemoval")
        if int(catalog.get("totalActions") or 0) < 1:
            errors.append("catalog totalActions empty")
        if int(catalog.get("coveragePct") or 0) < 100:
            warnings.append(f"catalog coveragePct={catalog.get('coveragePct')} (expected 100 for inventoried actions)")

    if not inv_path.is_file():
        errors.append("missing php-authoritative-inventory.json")
    else:
        inv = load(inv_path)
        if inv.get("role") != "php-module-ajax-authoritative-inventory":
            errors.append("inventory role mismatch")
        if inv.get("phpAuthoritative") is not True:
            errors.append("inventory must set phpAuthoritative=true")
        if inv.get("cutoverAllowed") is True or inv.get("readyForPhpRemoval") is True:
            errors.append("inventory invents cutover/removal")

    aspnet_stems: list[str] = []
    for path in sorted(evidence.glob("aspnet-*.json")):
        if path.name == "aspnet-catalog.json":
            continue
        aspnet_stems.append(path.name[len("aspnet-"):-len(".json")])
        samples_checked += 1
        doc = load(path)
        if validate_aspnet_sample(path, doc, errors):
            samples_ok += 1

    contract_only = bool(args.contract_only)
    if contract_only or any((evidence / f"php-{s}.json").exists() for s in aspnet_stems[:5]):
        contract_only = True

    if contract_only:
        for stem in aspnet_stems:
            aspnet_path = evidence / f"aspnet-{stem}.json"
            aspnet_doc = load(aspnet_path)
            php_path, used_migration = resolve_php(evidence, stem)
            if php_path is None:
                missing_php_sides.append(stem)
                errors.append(f"missing php/migration side for aspnet-{stem}.json")
                continue
            contract_pairs += 1
            if used_migration:
                migration_baseline_pairs += 1
            if validate_php_contract(php_path, aspnet_doc, errors, warnings):
                contract_pairs_ok += 1

        if missing_php_sides:
            warnings.append(
                f"{len(missing_php_sides)} aspnet golden(s) lack php/migration contract side — "
                "run generate_module_ajax_contract_samples.py"
            )

    result = {
        "role": "module-ajax-dual-sample-compare",
        "generatedAtUnix": int(time.time()),
        "ok": not errors,
        "samplesChecked": samples_checked,
        "samplesOk": samples_ok,
        "contractOnly": contract_only,
        "contractPairs": contract_pairs,
        "contractPairsOk": contract_pairs_ok,
        "migrationBaselinePairs": migration_baseline_pairs,
        "missingPhpSides": missing_php_sides[:20],
        "missingPhpSideCount": len(missing_php_sides),
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "aspNetInteractiveComplete": 0,
        "phpAuthoritative": True,
        "errors": errors[:50],
        "errorCount": len(errors),
        "warnings": warnings,
        "note": (
            "ASP.NET module-ajax dry-runs are writes=0 gates only. "
            "Contract-only pairs use migration-contract-golden baselines — not live PHP. "
            "Live PHP ajax/forms remain authoritative until field-level dual-sample + "
            "human RELEASE_OWNER_APPROVAL.md."
        ),
    }
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if errors:
        print(f"FAIL: module-ajax dual-sample compare -> {out}", file=sys.stderr)
        return 1
    print(f"PASS: module-ajax dual-sample compare cutoverAllowed=false -> {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
