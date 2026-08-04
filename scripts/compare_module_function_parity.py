#!/usr/bin/env python3
"""Compare module-function parity inventory/samples (contract floor).

Does NOT authorize PHP removal. Always emits cutoverAllowed=false.
Never invents MODULE_FUNCTION_TEST_PASS.md / RELEASE_OWNER_APPROVAL.md.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ALLOWED_STATUS = frozenset(
    {
        "php-only",
        "digest-only",
        "hybrid-deeplink",
        "digest-only+hybrid-deeplink",
        "not-started",
        # aspnet-complete is allowed only when human pass evidence exists (never invented here).
        "aspnet-complete",
    }
)

# Full PHP catalog floors (from generate_php_module_catalog.py / php_module_catalog_counts.json).
PHP_CATALOG_FLOORS = {
    "cpBrochureFeatures": 405,
    "erpAreas": 35,
    "erpTabs": 154,
    "bosModules": 99,
    "storefrontSurfaces": 12,
}
MIN_HYBRID_PREVIEW_COUNT = 37


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--samples-dir",
        type=Path,
        default=Path("docs/migration/evidence/module-function-parity"),
    )
    ap.add_argument("--out", type=Path, default=None)
    args = ap.parse_args()

    samples_dir: Path = args.samples_dir
    errors: list[str] = []
    inventory_path = samples_dir / "module-function-inventory.json"
    if not inventory_path.is_file():
        print(f"FAIL: missing {inventory_path}", file=sys.stderr)
        return 2

    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    if inventory.get("cutoverAllowed") is True or inventory.get("readyForPhpRemoval") is True:
        errors.append("inventory must keep cutoverAllowed/readyForPhpRemoval false")
    if inventory.get("role") != "module-function-inventory":
        errors.append("inventory role must be module-function-inventory")

    modules = inventory.get("modules") or []
    if not isinstance(modules, list) or not modules:
        errors.append("inventory.modules must be a non-empty list")
    elif len(modules) < MIN_HYBRID_PREVIEW_COUNT:
        errors.append(
            f"inventory.modules count={len(modules)} expected >={MIN_HYBRID_PREVIEW_COUNT} hybrid previews"
        )

    php_counts = inventory.get("phpCatalogCounts")
    if not isinstance(php_counts, dict) or not php_counts:
        errors.append("inventory.phpCatalogCounts must be a non-empty object (full PHP catalog scope)")
    else:
        for key, floor in PHP_CATALOG_FLOORS.items():
            try:
                value = int(php_counts.get(key))
            except (TypeError, ValueError):
                errors.append(f"inventory.phpCatalogCounts.{key} missing/invalid")
                continue
            if value < floor:
                errors.append(
                    f"inventory.phpCatalogCounts.{key}={value} below floor {floor} "
                    "(regenerate via python3 scripts/generate_php_module_catalog.py)"
                )

    complete = 0
    status_counts: dict[str, int] = {}
    for idx, mod in enumerate(modules):
        if not isinstance(mod, dict):
            errors.append(f"modules[{idx}] must be object")
            continue
        status = str(mod.get("status") or "")
        status_counts[status] = status_counts.get(status, 0) + 1
        if status not in ALLOWED_STATUS:
            errors.append(f"modules[{idx}].status invalid: {status!r}")
        if not mod.get("id") or not mod.get("surface") or not mod.get("aspnetRoute"):
            errors.append(f"modules[{idx}] requires id/surface/aspnetRoute")
        if status == "aspnet-complete":
            complete += 1
            if mod.get("writesRemainPhp") is not True:
                # Interactive complete still usually keeps some PHP writes; require explicit note.
                if not mod.get("humanFunctionalEvidence"):
                    errors.append(
                        f"modules[{idx}] aspnet-complete requires humanFunctionalEvidence or writesRemainPhp=true"
                    )

    # Contract floor: repo stubs must not claim interactive completion without the human pass file.
    pass_path = Path("docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md")
    if complete > 0 and not pass_path.is_file():
        errors.append(
            f"aspnet-complete count={complete} but {pass_path} is absent "
            "(do not invent MODULE_FUNCTION_PARITY_PASS)"
        )
    if pass_path.is_file():
        text = pass_path.read_text(encoding="utf-8")
        if "MODULE_FUNCTION_PARITY_PASS" not in text:
            errors.append(f"{pass_path} present but missing MODULE_FUNCTION_PARITY_PASS marker")

    declared_complete = inventory.get("aspnetCompleteCount")
    if declared_complete is None:
        errors.append("inventory.aspnetCompleteCount must be explicitly 0 (or match computed complete)")
    elif declared_complete != complete:
        errors.append(
            f"inventory.aspnetCompleteCount={declared_complete} != computed complete={complete}"
        )
    if complete != 0 and not pass_path.is_file():
        # already errored above; keep aspnetCompleteCount floor honest in compare-result
        pass

    out = {
        "role": "compare-result",
        "ok": not errors,
        "cutoverAllowed": False,
        "readyForPhpRemoval": False,
        "moduleCount": len(modules) if isinstance(modules, list) else 0,
        "hybridPreviewCount": len(modules) if isinstance(modules, list) else 0,
        "aspnetCompleteCount": complete,
        "phpCatalogCounts": php_counts if isinstance(php_counts, dict) else {},
        "phpCatalogFloors": PHP_CATALOG_FLOORS,
        "statusCounts": status_counts,
        "errors": errors,
        "note": (
            "Contract floor only. Hybrid previews != full PHP catalog scope. "
            "Interactive aspnet-complete remains 0 until human "
            "MODULE_FUNCTION_TEST_PASS.md exists. Never invents approval."
        ),
    }

    text = json.dumps(out, indent=2) + "\n"
    out_path = args.out or (samples_dir / "compare-result.json")
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(text, encoding="utf-8")
    print(text, end="")

    if errors:
        print(f"FAIL: module-function parity ({len(errors)} errors)", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
        return 1
    print(
        f"PASS: moduleCount={out['moduleCount']} aspnetCompleteCount={complete} "
        f"cutoverAllowed=false",
        file=sys.stderr,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
